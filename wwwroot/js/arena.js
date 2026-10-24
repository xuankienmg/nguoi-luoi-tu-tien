/* Vấn Đạo · js/arena.js: Lôi đài (tab trong Thiên bảng) · top 3 + đối thủ trong tầm, khiêu chiến, diễn lại trận do Supabase tính. */
let arData = null, arLoading = false, arAt = 0, arBusy = false, arTimer = 0, arFlash = null;   // arFlash: dòng báo kết quả trận, giữ qua các lần vẽ lại

function arenaFighting(){ return arBusy; }

async function loadArena(force){
  if (!S || arLoading) return;
  if (!force && arData && performance.now() - arAt < 20000){ renderArena(); return; }
  arLoading = true; renderRank();
  try { arData = await api('GET', `/api/players/${S.id}/arena`); arAt = performance.now(); }
  catch(e){ arData = {enabled: true, top: [], targets: [], history: [], error: e.message}; }
  finally { arLoading = false; renderRank(); }
}

function arInitView(){
  const v = $('arenaView'); if (!v || v.dataset.ready) return v;
  v.dataset.ready = 1;
  v.addEventListener('click', e => {
    const b = e.target.closest('[data-ar-fight]');
    if (!b) return;
    e.stopPropagation();                                   // không mở hồ sơ trang bị của dòng
    arFight(b.dataset.arFight, b.dataset.arName);
  });
  return v;
}

/** Nút Khiêu chiến: chỉ người trong tầm phía trên mới bấm được. */
function arButton(r, d){
  if (r.me) return '<span class="ar-self">Bạn</span>';
  const out = !r.canFight, empty = d.used >= d.daily, wait = arCooldownLeft() > 0;
  const tip = out ? (r.rank > (d.me?.rank ?? 0) ? 'Xếp dưới bạn' : `Ngoài tầm&#10;Chỉ khiêu chiến trong ${d.window} hạng phía trên`)
    : empty ? 'Đã hết lượt hôm nay&#10;Làm mới lúc 0h' : wait ? 'Đang điều tức'
    : r.practice ? `Tỷ thí với ${lbEsc(r.name)}&#10;Xếp dưới bạn: không đổi hạng, không tính thắng thua` : `Khiêu chiến ${lbEsc(r.name)}&#10;Thắng thì đổi hạng với đối thủ`;
  return `<button type="button" class="ar-go${r.practice ? ' practice' : ''}" data-ar-fight="${lbEsc(r.id)}" data-ar-name="${lbEsc(r.name)}" ${out || empty || wait || arBusy ? 'disabled' : ''} data-tip="${tip}">${r.practice ? '⚔ Tỷ thí' : '⚔ Khiêu chiến'}</button>`;
}

function arCard(r, d){
  const hero = DATA.realms[r.realm]?.hero;
  return `<div class="ar-card t${r.rank}${r.me ? ' me' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
    <span class="ar-rk">${r.rank}</span>
    ${hero ? `<img src="${lbEsc(hero)}" alt="">` : ''}
    <b>${lbEsc(r.name)}</b>
    <div class="lb-titles">${lbTitles(r.titles)}</div>
    <small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}</small>
    <small>Lực chiến <em>${fmt(r.power)}</em></small>
    ${arButton(r, d)}
  </div>`;
}

function arRow(r, d){
  return `<li class="lb-row ar-row${r.me ? ' me' : ''}${r.canFight ? ' in' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
    <span class="lb-rk">${r.rank}</span>
    <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)} · Lực chiến ${fmt(r.power)} · ${fmt(r.wins)}T/${fmt(r.losses)}B</small></span>
    <span class="lb-val">${arButton(r, d)}</span></li>`;
}

function arCooldownLeft(){ return arData?.readyInMs ? Math.max(0, arData.readyInMs - (performance.now() - arAt)) : 0; }

function arAgo(at){
  const s = Math.max(0, (Date.now() - new Date(at)) / 1000);
  return s < 60 ? 'vừa xong' : s < 3600 ? `${Math.floor(s / 60)} phút trước` : s < 86400 ? `${Math.floor(s / 3600)} giờ trước` : `${Math.floor(s / 86400)} ngày trước`;
}

function arHistory(h){
  const moved = h.to !== h.from ? ` · hạng #${h.from} → #${h.to}` : '';
  const text = h.practice
    ? (h.attacker ? `Bạn tỷ thí với ${lbEsc(h.other)}: ${h.win ? 'thắng' : 'thua'}` : `${lbEsc(h.other)} tỷ thí với bạn: bạn ${h.win ? 'thắng' : 'thua'}`)
    : h.attacker
    ? (h.win ? `Bạn khiêu chiến và đánh bại ${lbEsc(h.other)}` : `Bạn khiêu chiến ${lbEsc(h.other)} nhưng thất bại`)
    : (h.win ? `${lbEsc(h.other)} khiêu chiến, bạn giữ vững vị trí` : `${lbEsc(h.other)} đánh bại bạn`);
  return `<li class="${h.win ? 'good' : 'bad'}"><span>${text}${moved}</span><small>${arAgo(h.at)}</small></li>`;
}

function renderArena(){
  const v = arInitView(); if (!v || !S) return;
  const d = arData;
  clearTimeout(arTimer);
  if (!d){ v.innerHTML = `<p class="lb-empty">${arLoading ? 'Đang vào Lôi đài…' : ''}</p>`; return; }
  let msg = '';
  if (!d.enabled) msg = 'Lôi đài online chưa được cấu hình (appsettings.json → Leaderboard: Url, AnonKey).';
  else if (d.blocked) msg = d.blocked;
  else if (d.error) msg = d.error;
  if (msg){ v.innerHTML = `<div class="lb-note">${lbEsc(msg)}</div>`; return; }
  const cd = arCooldownLeft();
  const me = d.me;
  const flash = arFlash && performance.now() < arFlash.until ? `<div class="lb-note ar-toast ${arFlash.kind || ""}">${lbEsc(arFlash.text)}</div>` : "";
  v.innerHTML = `${flash}
    <div class="ar-bar">
      <div><span>Hạng của bạn</span><b>${me ? '#' + me.rank : '—'}</b><small>/ ${fmt(d.total || 0)} tu sĩ</small></div>
      <div><span>Chiến tích</span><b>${fmt(me?.wins || 0)}<i>T</i> ${fmt(me?.losses || 0)}<i>B</i></b></div>
      <div><span>Lượt hôm nay</span><b>${fmt(Math.max(0, d.daily - d.used))}<small>/ ${d.daily}</small></b><small>${cd > 0 ? `Điều tức ${Math.ceil(cd / 1000)}s` : 'Làm mới lúc 0h'}</small></div>
    </div>
    <p class="ar-rule">Khiêu chiến người xếp trên trong <b>${d.window}</b> hạng. Thắng thì <b>đổi vị trí</b> với đối thủ, thua giữ nguyên hạng. Đánh người xếp dưới là <b>tỷ thí</b>: không đổi hạng, không tính thắng thua. Hai bên mang đủ kỹ năng, trận diễn ở màn hình chính; hạng 1 nhận danh hiệu <span class="lb-title tt-arena">Lôi Đài Bá Chủ</span>.</p>
    ${d.mock ? '<div class="lb-note ar-mock">Lôi đài giả (dev): đối thủ Hóa Thần – Luyện Hư sinh trên máy, không gửi lên Supabase. Tắt game là xóa hết.</div>' : ''}
    <h4 class="ar-h">Tam cường</h4>
    <div class="ar-podium">${d.top.length ? [d.top[1], d.top[0], d.top[2]].filter(Boolean).map(r => arCard(r, d)).join('') : '<p class="lb-empty">Chưa có ai trong Lôi đài.</p>'}</div>
    <h4 class="ar-h">Đối thủ gần hạng bạn</h4>
    <ol class="lb-list ar-list">${d.targets.length ? d.targets.map(r => arRow(r, d)).join('') : `<li class="lb-empty">Chưa có đối thủ nào khác ngoài Tam cường.</li>`}</ol>
    ${d.history?.length ? `<details class="ar-log"${d.history.some(h => !h.attacker) ? ' open' : ''}><summary>Nhật ký Lôi đài</summary><ul>${d.history.map(arHistory).join('')}</ul></details>` : ''}`;
  if (cd > 0) arTimer = setTimeout(renderArena, Math.min(1000, cd + 30));
}

/* ---- Khiêu chiến: trận diễn ở màn hình chính (js/arena-stage.js), bảng chỉ báo kết quả ---- */
async function arFight(id, name){
  if (arBusy || !S) return;
  arBusy = true; renderArena();
  let res;
  try { res = await api('POST', `/api/players/${S.id}/arena/fight/${id}`); }
  catch(e){ arBusy = false; localLog(e.message, 'bad'); await loadArena(true); arToast(e.message); return; }
  setState(res.state); notifyHost('changed');                  // cửa sổ chính đồng bộ ngay → thấy S.arena và diễn trận
  arBusy = false;
  await loadArena(true);                                         // vẽ lại bảng trước, báo sau (vẽ lại sẽ xóa dòng báo)
  arToast(`${res.message || ''} Trận đấu đang diễn ở màn hình chính.`, res.fight?.win ? 'good' : 'bad');
  if (window.chrome?.webview) setTimeout(() => notifyHost('close-panel'), 1500);  // cửa sổ desktop: đóng bảng để xem trận
}

function arToast(text, kind){
  arFlash = {text, kind, until: performance.now() + 6000};
  renderArena(); setTimeout(renderArena, 6050);
}
