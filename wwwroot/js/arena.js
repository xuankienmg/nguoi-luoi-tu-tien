/* js/arena.js: Lôi đài · một bảng xếp hạng chung theo điểm, khiêu chiến và diễn lại trận. */
let arData = null, arLoading = false, arAt = 0, arBusy = false, arTimer = 0, arFlash = null;   // arFlash: dòng báo kết quả trận, giữ qua các lần vẽ lại
let arPage = -1;   // trang BXH đang xem (-1 = trang có mình)
const AR_PAGE = 50;   // số dòng mỗi trang BXH

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
    const pg = e.target.closest('[data-ar-page]');
    if (pg){ e.stopPropagation(); arPage = +pg.dataset.arPage; renderArena(); v.querySelector('.ar-list')?.scrollIntoView({block: 'nearest'}); return; }
    const c = e.target.closest('[data-ar-claim]');
    if (c){ e.stopPropagation(); arClaim(c.dataset.arClaim); return; }
    const b = e.target.closest('[data-ar-fight]');
    if (!b) return;
    e.stopPropagation();                                   // không mở hồ sơ trang bị của dòng
    arFight(b.dataset.arFight, b.dataset.arName);
  });
  return v;
}

/** Điểm thắng dự kiến, khớp ArenaTierData.WinPoints (server): người xếp trên +25 cộng 1 mỗi hạng chênh (tối đa +40);
 *  người xếp dưới −2 mỗi hạng chênh, ít nhất +5 (cân bằng 11/2026). */
function arWinPoints(myRank, foeRank){
  return foeRank <= myRank ? 25 + Math.min(Math.max(myRank - foeRank, 0), 15) : Math.max(5, 25 - 2 * (foeRank - myRank));
}

/** Nút Khiêu chiến: chỉ người trong tầm phía trên mới bấm được. */
function arButton(r, d){
  if (r.me) return '<span class="ar-self">Bạn</span>';
  const out = !r.canFight, empty = d.used >= d.daily, wait = arCooldownLeft() > 0;
  const score = !r.practice && r.rank > (d.me?.rank ?? 0);   // đánh người xếp dưới vẫn tính điểm (ít hơn)
  const pts = arWinPoints(d.me?.rank ?? r.rank, r.rank);
  const tip = out ? (r.rank > (d.me?.rank ?? 0) ? 'Xếp dưới bạn' : `Ngoài tầm&#10;Chỉ khiêu chiến trong ${d.window} hạng phía trên`)
    : empty ? 'Đã hết lượt hôm nay&#10;Làm mới lúc 0h' : wait ? 'Đang điều tức'
    : r.practice ? `Khiêu chiến ${lbEsc(r.name)}&#10;Xếp dưới bạn: không đổi hạng, không tính điểm`
    : score ? `Khiêu chiến ${lbEsc(r.name)}&#10;Xếp dưới bạn: thắng +${pts} điểm, thua −15`
    : `Khiêu chiến ${lbEsc(r.name)}&#10;Thắng +${pts} điểm, thua −15 (chỗ đứng theo điểm, không đổi chỗ)`;
  const label = out ? (r.rank > (d.me?.rank ?? 0) ? 'Xếp dưới' : 'Ngoài tầm') : empty ? 'Hết lượt' : 'Khiêu chiến';
  return `<button type="button" class="ar-go${r.practice ? ' practice' : ''}" data-ar-fight="${lbEsc(r.id)}" data-ar-name="${lbEsc(r.name)}" ${out || empty || wait || arBusy ? 'disabled' : ''} data-tip="${tip}">${label}</button>`;
}

function arCard(r, d){
  const hero = r.hero || DATA.realms[r.realm]?.hero;   // trang phục đang mặc, chưa có thì ảnh cảnh giới
  return `<div class="ar-card t${r.rank}${r.me ? ' me' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
    <span class="ar-rk" aria-label="Hạng ${r.rank}">${r.rank === 1 ? '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 18l2-10 5 5 2-8 2 8 5-5 2 10z"/><rect x="3" y="19.5" width="18" height="2" rx="1"/></svg>' : r.rank}</span>
    ${hero ? lbFigure(hero, r.realm, r.name) : ''}
    <b class="ar-nm">${lbEsc(r.name)}</b>
    <div class="lb-titles">${lbTitles(r.titles)}</div>
    <small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}</small>
    ${d.tier ? `<small class="ar-pt"><b>${fmt(r.points || 0)}</b> điểm</small>` : ''}
    <small>Lực chiến <em>${fmt(r.power)}</em></small>
    ${arButton(r, d)}
  </div>`;
}

/** Chữ cái đầu của tên (tối đa 2) cho vòng tròn avatar trong danh sách. */
const arInit = name => String(name || '?').trim().split(/\s+/).slice(0, 2).map(w => [...w][0] || '').join('').toUpperCase();

function arRow(r, d){
  const mine = d.me?.rank ?? 0;
  // điểm nhận được khi thắng đối thủ có thể khiêu chiến
  const note = r.me || !d.tier || !r.canFight || r.practice ? ''
    : `<small class="ar-note${r.rank > mine ? ' score' : ''}">Thắng: +${arWinPoints(mine || r.rank, r.rank)} điểm</small>`;
  return `<li class="lb-row ar-row${r.me ? ' me' : ''}${r.canFight ? ' in' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
    <span class="lb-rk">${r.rank}</span>
    <span class="ar-av${r.me ? ' me' : ''}" aria-hidden="true">${lbEsc(arInit(r.name))}</span>
    <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${d.tier ? `<span class="ar-pt"><b>${fmt(r.points || 0)}</b> điểm</span> · ` : ''}${lbEsc(r.realmName)} · ${lbEsc(r.stageName)} · Lực chiến ${fmt(r.power)} · ${fmt(r.wins)}T/${fmt(r.losses)}B</small>${note}</span>
    <span class="lb-val">${arButton(r, d)}</span></li>`;
}

/** Điểm trên bảng xếp hạng chung: một dòng gọn, luật nằm trong nút ⓘ. */
function arScoreHero(t, d){
  return `<section class="ar-hero" aria-label="Điểm Lôi đài">
    <span class="ar-eyebrow">Lôi đài</span><b class="ar-tname">Bảng xếp hạng chung</b>${arInfoBtn(d)}
    <div class="ar-pts"><b>${fmt(t.points)}</b> điểm</div>
  </section>`;
}

/** Bốn ô chỉ số: hạng toàn bảng, chiến tích, lượt hôm nay (vạch), điều tức. */
function arStats(d, cd){
  const me = d.me, left = Math.max(0, d.daily - d.used), t = d.tier;
  const pips = d.daily <= 40 ? `<span class="ar-pips" aria-hidden="true">${Array.from({length: d.daily}, (_, i) => `<i${i < left ? ' class="on"' : ''}></i>`).join('')}</span>` : '';
  const rest = cd > 0 ? `<b class="ar-cd"><i class="dot wait"></i>${Math.ceil(cd / 1000)} giây</b><small>${left ? 'Đang điều tức' : 'Hết lượt, làm mới 0h'}</small>`
    : `<b class="ar-cd"><i class="dot"></i>Sẵn sàng</b><small>${left ? 'Nghỉ ngắn giữa các trận' : 'Hết lượt, làm mới 0h'}</small>`;
  return `<section class="ar-stats" aria-label="Chỉ số của bạn">
    <div><span>Hạng của bạn</span><b>${me ? '#' + me.rank : '—'}</b><small>/ ${fmt(d.total || 0)} tu sĩ</small></div>
    <div><span>Chiến tích</span><b><em class="w">${fmt(me?.wins || 0)}</em><i> T </i><em class="l">${fmt(me?.losses || 0)}</em><i> B</i></b><small>${t ? 'Mọi trận đều tính điểm' : 'Tổng thắng / bại'}</small></div>
    <div><span>Lượt hôm nay</span><b>${fmt(left)}<i> / ${d.daily}</i></b>${pips}</div>
    <div><span>Điều tức</span>${rest}</div>
  </section>`;
}

/** Thưởng ngày / tuần: nhận được thì nổi bật, đã nhận hiện dấu tích, chưa tới hạn thì khóa kèm lý do. */
function arClaims(t){
  const name = id => (typeof ITEM !== 'undefined' && ITEM[id]?.name) || id;
  const rw = c => c.rewards.map(r => `${fmt(r.qty)} ${lbEsc(name(r.item))}`).join(' · ');
  const claim = (c, kind, label) => `<div class="ar-claim${c.can ? ' ready' : ''}"><span><b>${label}</b><small>${rw(c)}</small></span>${c.claimed
    ? '<em><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12l5 5 9-10"/></svg>Đã nhận</em>'
    : `<button type="button" class="ar-go${c.can ? ' primary' : ''}" data-ar-claim="${kind}" ${c.can && !arBusy ? '' : 'disabled'}${c.why ? ` data-tip="${lbEsc(c.why)}"` : ''}>${c.can || !c.why ? 'Nhận' : kind === 'weekly' ? 'Chủ nhật' : 'Chưa đủ'}</button>`}</div>`;
  return `<section class="ar-claims" aria-label="Thưởng Lôi đài">${claim(t.daily, 'daily', 'Thưởng ngày')}${claim(t.weekly, 'weekly', 'Thưởng tuần · Chủ nhật')}</section>`;
}

/** Luật Lôi đài gói trong một tooltip (nút ⓘ) cho gọn giao diện; mỗi dòng một ý, dòng cuối riêng cho Lôi đài giả (dev). */
function arInfoTip(d){
  const t = d.tier, lines = ['Luật Lôi đài',
    `Khiêu chiến người xếp trên trong ${d.window} hạng.${t ? ' Khiêu chiến không đổi chỗ: chỗ đứng trên bảng chung xếp theo điểm.' : ' Thắng thì đổi vị trí với đối thủ, thua giữ nguyên hạng.'}`,
    ...(t ? ['Một bảng xếp hạng chung cho mọi tu sĩ.', 'Thắng người xếp trên +25 đến +40 điểm · thua −15 · điểm tối thiểu 0.'] : []),
    ...(t ? ['Thắng người xếp dưới ít điểm hơn: kém 1 hạng +23, mỗi hạng kém thêm −2, ít nhất +5.'] : ['Khiêu chiến người xếp dưới: không đổi hạng, không tính điểm.']),
    'Máu hai bên ×3. Hai bên mang đủ kỹ năng, trận diễn ngay trong tab.',
    'Hạng 1 nhận danh hiệu Lôi Đài Bá Chủ.',
    ...(d.mock ? ['Lôi đài giả (dev): đối thủ sinh trên máy, không gửi lên Supabase. Tắt game là xóa hết.'] : [])];
  return lines.map(lbEsc).join('&#10;');
}
const arInfoBtn = d => `<button type="button" class="ar-info" aria-label="Luật Lôi đài" data-tip="${arInfoTip(d)}">i</button>`;

function arCooldownLeft(){ return arData?.readyInMs ? Math.max(0, arData.readyInMs - (performance.now() - arAt)) : 0; }

function arAgo(at){
  const s = Math.max(0, (Date.now() - new Date(at)) / 1000);
  return s < 60 ? 'vừa xong' : s < 3600 ? `${Math.floor(s / 60)} phút trước` : s < 86400 ? `${Math.floor(s / 3600)} giờ trước` : `${Math.floor(s / 86400)} ngày trước`;
}

function arHistory(h){
  const moved = h.to !== h.from ? ` · hạng #${h.from} → #${h.to}` : '';
  const text = h.practice
    ? (h.attacker ? `Bạn khiêu chiến ${lbEsc(h.other)}: ${h.win ? 'thắng' : 'thua'} (không tính điểm)` : `${lbEsc(h.other)} khiêu chiến bạn (không tính điểm): bạn ${h.win ? 'thắng' : 'thua'}`)
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
  const pages = Math.max(1, Math.ceil(d.targets.length / AR_PAGE));
  if (arPage < 0){ const i = d.targets.findIndex(r => r.me); arPage = i < 0 ? 0 : Math.floor(i / AR_PAGE); }   // lần đầu: mở trang có mình
  const page = arPage = Math.min(arPage, pages - 1), rows = d.targets.slice(page * AR_PAGE, (page + 1) * AR_PAGE);
  const flash = arFlash && performance.now() < arFlash.until ? `<div class="lb-note ar-toast ${arFlash.kind || ""}">${lbEsc(arFlash.text)}</div>` : "";
  v.innerHTML = `${flash}
    ${d.tier ? arScoreHero(d.tier, d) : `<div class="ar-infobar">${arInfoBtn(d)}<span>Luật Lôi đài</span></div>`}
    ${arStats(d, cd)}
    ${d.tier ? arClaims(d.tier) : ''}
    ${d.tier ? '' : `<h4 class="ar-h">Tam cường</h4>
    <div class="ar-podium">${d.top.length ? [d.top[1], d.top[0], d.top[2]].filter(Boolean).map(r => arCard(r, d)).join('') : '<p class="lb-empty">Chưa có ai trong Lôi đài.</p>'}</div>`}
    <h4 class="ar-h">Bảng xếp hạng Lôi đài</h4>
    <ol class="lb-list ar-list">${rows.length ? rows.map(r => arRow(r, d)).join('') : `<li class="lb-empty">Chưa có đối thủ nào khác.</li>`}</ol>
    ${arPager(pages, page, d.targets.length)}
    ${d.history?.length ? `<details class="ar-log"${d.history.some(h => !h.attacker) ? ' open' : ''}><summary>Nhật ký Lôi đài</summary><ul>${d.history.map(arHistory).join('')}</ul></details>` : ''}`;
  if (cd > 0) arTimer = setTimeout(renderArena, Math.min(1000, cd + 30));
}

/** Phân trang BXH Lôi đài (AR_PAGE dòng/trang), dùng nút chung lbPager (js/leaderboard.js). */
function arPager(pages, page, total){
  const html = lbPager(page, pages, 'data-ar-page', `Hạng ${page * AR_PAGE + 1}–${Math.min((page + 1) * AR_PAGE, total)} / ${fmt(total)}`);
  return html && `<nav class="pager iv-pager ar-pager" aria-label="Trang bảng xếp hạng">${html}</nav>`;
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
  if (res.fight?.duel) duelShow(res.fight.duel, {win: res.fight.win, verdict: res.fight.win ? 'Thắng' : 'Bại', message: res.message, title: 'Lôi đài'});          // avatar tròn lao vào nhau + thanh máu ngay trong tab
  else arToast(res.message || '', res.fight?.win ? 'good' : 'bad');
}

async function arClaim(kind){
  if (arBusy || !S) return;
  arBusy = true; renderArena();
  try { const res = await api('POST', `/api/players/${S.id}/arena/claim/${kind}`); setState(res.state); notifyHost('changed'); arBusy = false; await loadArena(true); arToast(res.message || 'Đã nhận thưởng.', 'good'); }
  catch(e){ arBusy = false; await loadArena(true); arToast(e.message); }
}

function arToast(text, kind){
  arFlash = {text, kind, until: performance.now() + 6000};
  renderArena(); setTimeout(renderArena, 6050);
}
