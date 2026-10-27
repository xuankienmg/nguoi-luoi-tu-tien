/* Vấn Đạo · js/leaderboard.js: Bảng xếp hạng online (Supabase qua server game) · 2 tab Cảnh giới / Lực chiến. */
let lbBy = 'realm', lbData = null, lbLoading = false, lbAt = 0;
try { const saved = localStorage.getItem('van-dao-lb-by'); lbBy = ['realm','power','progress','pet','arena'].includes(saved) ? saved : 'realm'; } catch(e){}
const lbEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));

function initRank(){
  const pane = $('pane-rank'); if (!pane || pane.dataset.ready) return;
  pane.dataset.ready = 1;
  pane.innerHTML = `<div class="lb">
    ${lbSymbols}
    <header class="lb-heading">${lbIcon('board')}<div><h2>Thiên bảng</h2><p>Luận thực lực · Định tiên vị</p></div><span class="lb-seal">Vạn đạo tranh phong</span></header>
    <div class="lb-top">
      <div class="lb-tabs" role="tablist">
        <button data-lb="power" role="tab">${lbIcon('power')}Lực chiến</button>
        <button data-lb="realm" role="tab">${lbIcon('realm')}Cảnh giới</button>
        <button data-lb="progress" role="tab">${lbIcon('board')}Vượt ải</button>
        <button data-lb="pet" role="tab">${lbIcon('realm')}Linh thú</button>
        <button data-lb="arena" role="tab">${lbIcon('power')}Lôi đài</button>
      </div>
      <button class="lb-ref" id="lbRef" aria-label="Làm mới bảng xếp hạng" data-tip="Làm mới&#10;Bảng tự cập nhật mỗi 30 giây">↻</button>
    </div>
    <form class="lb-name" id="lbName">
      <label for="lbNameIn">Đạo hiệu</label>
      <input id="lbNameIn" maxlength="20" autocomplete="off" spellcheck="false">
      <button type="submit">Đổi</button>
    </form>
    <div class="lb-note" id="lbNote" hidden></div>
    <div class="lb-body"><aside id="lbChampion" class="lb-champion" hidden></aside><section class="lb-results"><div class="lb-columns"><span>Hạng</span><span>Đạo hữu</span><span id="lbColumn">Cảnh giới</span></div><ol class="lb-list" id="lbList"></ol></section></div>
    <div class="lb-me" id="lbMe"></div>
    <div class="ar" id="arenaView" hidden></div>
    <footer class="lb-footer">Tự cập nhật mỗi 30 giây</footer>
  </div>`;
  pane.addEventListener('click', async e => {
    const clear = e.target.closest('[data-lb-clear]');
    if (clear && devMode) {
      e.stopPropagation();
      clear.disabled = true;
      try {
        const result = await api('POST', `/api/players/${S.id}/admin/clearflag?${new URLSearchParams({arg: clear.dataset.lbClear})}`);
        if (result.state) setState(result.state);
        await loadRank(true);
      } catch (error) { clear.disabled = false; localLog(error.message, 'bad'); }
      return;
    }
    const t = e.target.closest('[data-lb]');
    if (t && t.dataset.lb !== lbBy){ lbBy = t.dataset.lb; try { localStorage.setItem('van-dao-lb-by', lbBy); } catch(e){} loadRank(); }
    if (e.target.closest('#lbRef')) loadRank(true);
    const row = e.target.closest('[data-pid]'); if (row) lbShowProfile(row.dataset.pid);
  });
  pane.addEventListener('keydown', e => {
    if (e.target.closest('[data-lb-clear]')) return;
    const row = e.target.closest('[data-pid]');
    if (row && (e.key === 'Enter' || e.key === ' ')){ e.preventDefault(); lbShowProfile(row.dataset.pid); }
  });
  $('lbName').addEventListener('submit', async e => {
    e.preventDefault();
    const name = $('lbNameIn').value.trim();
    if (!name || name === S.name) return;
    await act(`name?${new URLSearchParams({name})}`);
    setTimeout(() => loadRank(true), 1500);        // điểm gửi nền → đợi một chút rồi lấy lại bảng
  });
  setInterval(() => { if (!$('pane-rank').hidden && !document.hidden && !arenaFighting()) loadRank(true); }, 30000);
}

async function loadRank(force){
  initRank();
  if (lbBy === 'arena'){ renderRank(); return loadArena(force); }
  if (!S || lbLoading) return;
  if (!force && lbData?.by === lbBy && performance.now() - lbAt < 20000){ renderRank(); return; }
  lbLoading = true; renderRank();
  try { lbData = await api('GET', `/api/players/${S.id}/leaderboard?by=${lbBy}`); lbAt = performance.now(); }
  catch(e){ lbData = {enabled: true, by: lbBy, rows: [], error: e.message}; }
  finally { lbLoading = false; renderRank(); }
}

/** Chú thích dấu * đỏ: nhân vật từng mở bằng bản game cũ (SaveGuard) — vẫn xếp hạng nhưng không vào top 10, không giao dịch ở Chợ. */
const LB_FLAG_TIP = 'Dữ liệu từng được mở bằng bản game cũ&#10;Vẫn được xếp hạng nhưng không vào top 10 và không giao dịch ở Chợ';
const lbLabels = {realm: 'Cảnh giới', power: 'Lực chiến', progress: 'Vượt ải', pet: 'Linh thú', arena: 'Lôi đài'};
/** Danh hiệu Đệ nhất (Game/Arena.cs → Titles) cạnh tên người chơi. */
function lbTitles(list){
  return (list || []).map(t => `<span class="lb-title tt-${lbEsc(t.id)}" data-tip="${lbEsc(t.desc)}">${lbEsc(t.name)}</span>`).join('');
}
function lbProgressText(value){
  const n = Number(value) || 0;
  if (!n) return 'Chưa vượt ải';
  const difficulty = Math.floor(n / 10000), chapter = Math.floor(n % 10000 / 100) + 1, area = n % 100;
  return `${['Thường', 'Khó', 'Ác mộng', 'Địa Ngục 1'][difficulty] || 'Thường'} · ${chapter}-${area}`;
}
function lbScore(r, by){
  return by === 'progress' ? lbEsc(lbProgressText(r.progress)) : by === 'pet' ? (r.pet
    ? `<span class="lb-pet"><img src="${lbEsc(r.pet.image)}" alt="${lbEsc(r.pet.name)}"><span><b>${lbEsc(r.pet.name)}</b><small>Cấp ${fmt(r.pet.level)} · ${fmt(r.pet.stars)} ★</small><small>Lực chiến ${fmt(r.petPower || 0)}</small></span></span>`
    : `<span class="lb-pet-pending">${fmt(r.petPower || 0)}<small>${r.petPower ? 'Lực chiến' : 'Chưa có linh thú'}</small></span>`)
    : by === 'power' ? fmt(r.power) : `${lbEsc(r.realmName)}<small>${lbEsc(r.stageName)}</small>`;
}
/** Thẻ Đệ nhất của bảng Linh thú: ảnh lớn của linh thú, sao, cấp, lực chiến, rồi tới chủ nhân. */
function lbPetChampion(r){
  const p = r.pet, hd = String(p.image || '').replace(/(img\/pet\/[^/]+_[0-2])\.png$/, '$1_hd.png');
  const stars = Math.max(0, Math.min(5, Number(p.stars) || 0));
  return `<div class="lb-first">Đệ nhất</div><div class="lb-pet-hero"><img src="${lbEsc(hd)}" alt="${lbEsc(p.name)}" onerror="this.onerror=null;this.src='${lbEsc(p.image)}'"></div>`
    + `<h3>${lbEsc(p.name)}</h3><div class="lb-stars" aria-label="${stars} sao">${'★'.repeat(stars)}<i>${'★'.repeat(5 - stars)}</i></div><p>Cấp ${fmt(p.level)}</p>`
    + `<strong>${fmt(r.petPower || 0)}</strong><p>Lực chiến linh thú</p>`
    + `<div class="lb-owner">Chủ nhân<b>${lbEsc(r.name)}</b><small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}</small></div><div class="lb-motto">Độc bộ thiên hạ</div>`;
}
function lbRow(r, by){
  const top = r.rank <= 3 ? ` t${r.rank}` : '';
  const realmTxt = `${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}`;
  const main = lbScore(r, by);
  const sub = by === 'pet' || by === 'power' ? realmTxt : `Lực chiến ${fmt(r.power)}`;
  return `<li class="lb-row${top}${r.me ? ' me' : ''}"${r.id ? ` data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}" data-tip="Xem trang bị&#10;${lbEsc(r.name)}"` : ''}><span class="lb-rk">${r.rank}</span>
    <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.flagged ? `<sup class="lb-flag" aria-label="Đã đánh dấu" data-tip="${LB_FLAG_TIP}">*</sup>` : ''}${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${sub}</small></span><span class="lb-val">${main}${devMode && r.flagged && r.id ? `<button type="button" data-lb-clear="${lbEsc(r.id)}" aria-label="Gỡ cờ dữ liệu cho ${lbEsc(r.name)}">Gỡ cờ</button>` : ''}</span></li>`;
}

function renderRank(){
  initRank();
  const pane = $('pane-rank'); if (!pane || !S) return;
  pane.querySelectorAll('[data-lb]').forEach(b => { const on = b.dataset.lb === lbBy; b.classList.toggle('on', on); b.setAttribute('aria-selected', on); });
  const inp = $('lbNameIn'); if (document.activeElement !== inp) inp.value = S.name;
  const arena = lbBy === 'arena';
  $('arenaView').hidden = !arena;
  pane.querySelector('.lb-body').hidden = arena; $('lbMe').hidden = arena;
  if (arena){ pane.dataset.by = 'arena'; $('lbNote').hidden = true; $('lbFlagNote')?.setAttribute('hidden', ''); $('lbRef').disabled = arLoading; renderArena(); return; }
  $('lbRef').disabled = lbLoading;
  const d = lbData?.by === lbBy ? lbData : null, note = $('lbNote');
  pane.dataset.by = lbBy;
  $('lbColumn').textContent = lbLabels[lbBy] || 'Cảnh giới';
  const champion = d?.enabled && !d.error ? d.rows.find(r => r.rank === 1) : null;
  const hero = champion ? DATA.realms[champion.realm]?.hero : null;
  $('lbChampion').hidden = !champion;
  $('lbChampion').innerHTML = champion && lbBy === 'pet' && champion.pet ? lbPetChampion(champion) : champion ? `<div class="lb-first">Đệ nhất</div>${hero ? `<img src="${lbEsc(hero)}" alt="">` : lbIcon(lbBy === 'power' ? 'power' : 'realm')}<h3>${lbEsc(champion.name)}</h3>${champion.titles?.length ? `<div class="lb-titles">${lbTitles(champion.titles)}</div>` : ''}<p>${lbEsc(champion.realmName)} · ${lbEsc(champion.stageName)}</p><strong>${lbScore(champion, lbBy)}</strong><p>${lbLabels[lbBy]}</p><div class="lb-motto">Độc bộ thiên hạ</div>` : '';
  let msg = '';
  if (d && !d.enabled) msg = 'Bảng xếp hạng online chưa được cấu hình (appsettings.json → Leaderboard: Url, AnonKey).';
  else if (d?.error) msg = d.error;
  else if (d?.tainted) msg = 'Nhân vật này đã dùng admin mode nên không được xếp hạng. Tu lại từ đầu (không bật cờ admin) để tham gia.';
  else if (d?.flagged) msg = 'Dữ liệu nhân vật của bạn từng được mở bằng bản game cũ: vẫn được xếp hạng nhưng mang dấu * đỏ, không vào top 10 và không giao dịch ở Chợ.';
  note.hidden = !msg; note.textContent = msg;
  $('lbMe').innerHTML = !d || !d.enabled || d.tainted || d.error ? ''
    : d.me ? `<div>Hạng của bạn <b>#${d.me.rank}</b></div><ol class="lb-list">${lbRow({...d.me, me:true}, lbBy)}</ol>`
    : 'Chưa có hạng: tiến độ sẽ được gửi lên trong giây lát.';
  $('lbList').innerHTML = !d ? `<li class="lb-empty">${lbLoading ? 'Đang tải…' : ''}</li>`
    : d.rows.length ? d.rows.map(r => lbRow(r, lbBy)).join('')
    : d.enabled && !d.error ? '<li class="lb-empty">Chưa có tu sĩ nào trên bảng.</li>' : '';
  // chú thích cuối bảng khi có người mang dấu *
  let fn = $('lbFlagNote');
  if (!fn){ fn = document.createElement('p'); fn.id = 'lbFlagNote'; fn.className = 'lb-flag-note'; $('lbList').after(fn); }
  const anyFlag = !!d && (d.rows.some(r => r.flagged) || d.me?.flagged);
  fn.hidden = !anyFlag;
  fn.innerHTML = anyFlag ? '<span class="lb-flag">*</span> Dữ liệu từng được mở bằng bản game cũ: vẫn xếp hạng nhưng không vào top 10 và không giao dịch ở Chợ.' : '';
}

const lbSymbols = "<svg width=\"0\" height=\"0\" style=\"position:absolute\" aria-hidden=\"true\"><defs>\n<symbol id=\"lb-board\" viewBox=\"0 0 64 64\"><path d=\"M15 10h34v44H15z\" fill=\"#272b27\" stroke=\"currentColor\" stroke-width=\"2\"/><path d=\"M11 15h42M11 49h42M22 5v10m20-10v10\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3\"/><path d=\"M23 40V30h6v10m5 0V23h6v17M20 44h24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/><path d=\"m32 8 4 5-4 5-4-5z\" fill=\"currentColor\"/></symbol>\n<symbol id=\"lb-power\" viewBox=\"0 0 64 64\"><path d=\"m32 3 25 14v30L32 61 7 47V17z\" fill=\"#322324\" stroke=\"currentColor\"/><g fill=\"#ead2b2\" stroke=\"#b97a61\" stroke-width=\"1.5\"><path d=\"m17 10 8 5 20 30-6 4-20-30z\"/><path d=\"m47 10-8 5-20 30 6 4 20-30z\"/></g><path d=\"m14 38 15 10m6 0 15-10M20 45l-6 9m30-9 6 9\" stroke=\"currentColor\" stroke-width=\"4\"/><path d=\"m32 24 4 8-4 8-4-8z\" fill=\"#efb375\"/></symbol>\n<symbol id=\"lb-realm\" viewBox=\"0 0 64 64\"><circle cx=\"32\" cy=\"32\" r=\"27\" fill=\"#182e2b\" stroke=\"currentColor\"/><circle cx=\"32\" cy=\"32\" r=\"22\" fill=\"none\" stroke=\"currentColor\" opacity=\".3\"/><g fill=\"#294b42\" stroke=\"currentColor\" stroke-width=\"1.6\"><path d=\"M32 46C18 34 25 19 32 12c7 7 14 22 0 34z\"/><path d=\"M32 46C16 48 9 34 10 26c11 0 22 6 22 20zM32 46c16 2 23-12 22-20-11 0-22 6-22 20z\"/><path d=\"M32 47C19 51 14 45 12 39c9-2 16 0 20 8zm0 0c13 4 18-2 20-8-9-2-16 0-20 8z\"/></g><path d=\"m32 22 3 8-3 8-3-8z\" fill=\"#c9ead6\"/></symbol></defs></svg>";
function lbIcon(name){return `<svg class="lb-icon" viewBox="0 0 64 64" aria-hidden="true"><use href="#lb-${name}"/></svg>`}

/* ---- Hồ sơ: bấm một dòng → trang bị đang mặc + chỉ số (lúc gửi điểm gần nhất) ---- */
let lbPfReq = 0;
function lbCloseProfile(){ document.getElementById('lbPf')?.remove(); lbPfReq++; }
async function lbShowProfile(pid){
  lbCloseProfile();
  const req = ++lbPfReq;
  const el = document.createElement('div');
  el.id = 'lbPf'; el.className = 'lb-pf'; el.setAttribute('role', 'dialog'); el.setAttribute('aria-modal', 'true');
  el.innerHTML = '<div class="lb-pf-card"><p class="lb-pf-wait">Đang tải trang bị…</p></div>';
  el.addEventListener('click', e => { if (e.target === el || e.target.closest('[data-pf-x]')) lbCloseProfile(); });
  document.body.appendChild(el);
  let pf;
  try { pf = await api('GET', `/api/players/${S.id}/leaderboard/${pid}`); }
  catch(err){ if (req === lbPfReq) el.querySelector('.lb-pf-card').innerHTML = `<p class="lb-pf-wait">${lbEsc(err.message)}</p><button class="lb-pf-x" data-pf-x>Đóng</button>`; return; }
  if (req !== lbPfReq) return;
  const slots = DATA.slots.map(sd => {
    const k = pf.equipment?.[sd.id], it = k && ITEM[k];
    if (!k) return `<div class="lb-pf-slot empty"><span class="lb-pf-ic"></span><span><small>${sd.name}</small><em>Trống</em></span></div>`;
    if (!it) return `<div class="lb-pf-slot"><span class="lb-pf-ic">?</span><span><small>${sd.name}</small><em>Vật phẩm bản game khác</em></span></div>`;
    return `<div class="lb-pf-slot r-${it.rarity}" data-card="bag" data-k="${k}" tabindex="0"><span class="lb-pf-ic">${itemImg(k)}</span>
      <span><small>${sd.name} · cấp ${itemLv(it)}</small><b class="rc-${it.rarity}">${lbEsc(it.name)}</b></span></div>`;
  }).join('');
  const st = pf.stats || {}, has = Object.keys(st).length > 0;
  const stat = (n, v) => `<div><span>${n}</span><b>${v}</b></div>`;
  el.querySelector('.lb-pf-card').innerHTML = `
    <header><div><h3>${lbEsc(pf.name)}${pf.me ? ' <em class="lb-you">Bạn</em>' : ''}</h3>${pf.titles?.length ? `<div class="lb-titles">${lbTitles(pf.titles)}</div>` : ''}<p>${lbEsc(pf.realmName)} · ${lbEsc(pf.stageName)} · Lực chiến <b>${fmt(pf.power)}</b></p></div>
      <button class="lb-pf-x" data-pf-x aria-label="Đóng">✕</button></header>
    ${has ? `<div class="lb-pf-stats">${stat('Tấn công', fmt(st.atk || 0))}${stat('Phòng thủ', fmt(st.def || 0))}${stat('Máu', fmt(st.hp || 0))}${stat('Bạo kích', Math.round(st.crit || 0) + '%')}${stat('Thân pháp', Math.round(st.speed || 0) + '%')}</div>` : ''}
    <div class="lb-pf-gear">${slots}</div>
    <small class="lb-pf-at">Cập nhật ${new Date(pf.updatedAt).toLocaleString('vi-VN')}${has ? '' : ' · người này chưa chạy bản có gửi trang bị'}</small>`;
  el.querySelector('[data-pf-x]')?.focus();
}
addEventListener('keydown', e => { if (e.key === 'Escape' && document.getElementById('lbPf')){ e.stopPropagation(); lbCloseProfile(); } }, true);
