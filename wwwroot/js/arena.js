/* js/arena.js: Lôi đài (tab trong Thiên bảng) · top 3 + đối thủ trong tầm, khiêu chiến, diễn lại trận do Supabase tính. */
let arViewTier = -1, arTierBoard = null;   // hạng đang xem trên thang hạng + người chơi của hạng đó
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
    const tb = e.target.closest('[data-ar-tier]');
    if (tb){ e.stopPropagation(); arShowTier(+tb.dataset.arTier); return; }
    if (e.target.closest('[data-ar-tier-close]')){ e.stopPropagation(); arViewTier = -1; arTierBoard = null; renderArena(); return; }
    const c = e.target.closest('[data-ar-claim]');
    if (c){ e.stopPropagation(); arClaim(c.dataset.arClaim); return; }
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
  const score = !r.practice && r.rank > (d.me?.rank ?? 0);   // hạng cao nhất: đánh người xếp dưới vẫn tính điểm (không đổi chỗ)
  const tip = out ? (r.rank > (d.me?.rank ?? 0) ? 'Xếp dưới bạn' : `Ngoài tầm&#10;Chỉ khiêu chiến trong ${d.window} hạng phía trên`)
    : empty ? 'Đã hết lượt hôm nay&#10;Làm mới lúc 0h' : wait ? 'Đang điều tức'
    : r.practice ? `Khiêu chiến ${lbEsc(r.name)}&#10;Xếp dưới bạn: không đổi hạng, không tính điểm`
    : score ? `Khiêu chiến ${lbEsc(r.name)}&#10;Xếp dưới bạn: thắng +25 điểm, thua −15`
    : `Khiêu chiến ${lbEsc(r.name)}&#10;Thắng +25 đến +40 điểm, thua −15 (chỗ đứng theo điểm, không đổi chỗ)`;
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
  // dòng giải thích kết quả của trận (chỉ khi có hệ hạng và đánh được): đổi chỗ + điểm, hoặc đấu điểm ở hạng cao nhất
  const note = r.me || !d.tier || !r.canFight || r.practice ? '' : r.rank > mine
    ? '<small class="ar-note score">Thắng: +25 điểm</small>' : '<small class="ar-note">Thắng: +25 đến +40 điểm</small>';
  return `<li class="lb-row ar-row${r.me ? ' me' : ''}${r.canFight ? ' in' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
    <span class="lb-rk">${r.rank}</span>
    <span class="ar-av${r.me ? ' me' : ''}" aria-hidden="true">${lbEsc(arInit(r.name))}</span>
    <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${d.tier ? `<span class="ar-pt"><b>${fmt(r.points || 0)}</b> điểm</span> · ` : ''}${lbEsc(r.realmName)} · ${lbEsc(r.stageName)} · Lực chiến ${fmt(r.power)} · ${fmt(r.wins)}T/${fmt(r.losses)}B</small>${note}</span>
    <span class="lb-val">${arButton(r, d)}</span></li>`;
}

/* ---- Hạng Lôi đài (chỉ Lôi đài có hệ hạng): huy hiệu + thanh điểm, thang hạng, thưởng ngày / tuần, chip luật ---- */
const AR_TIER_COL = {dong: '#d39a67', bac: '#cfd8dc', vang: '#f2c94c', bach_kim: '#7fe3d4', kim_cuong: '#7fb8ff', tong_su: '#ff7a5c'};
const arTierCol = id => AR_TIER_COL[id] || AR_TIER_COL.dong;

/** Huy hiệu hạng: khiên + dấu chữ thập; hạng cao nhất (tong_su = Cao Thủ) là vương miện. */
function arMedal(id, name){
  const c = arTierCol(id);
  const mark = id === 'tong_su'
    ? '<path d="M22 62l6-26 14 14 6-20 6 20 14-14 6 26z" fill="#4a2a22" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><path d="M24 70h48" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" opacity=".7"/>'
    : '<path d="M48 16l24 8v22c0 15-10 26-24 32C34 72 24 61 24 46V24z" fill="#1f2a2c" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/><path d="M48 28v38M36 38h24" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" opacity=".8"/>';
  return `<svg class="ar-medal" width="96" height="96" viewBox="0 0 96 96" role="img" aria-label="Huy hiệu hạng ${lbEsc(name)}" style="color:${c}"><circle cx="48" cy="48" r="45" fill="#171f21" stroke="currentColor" stroke-width="2"/>${mark}</svg>`;
}

function arTierHero(t, d){
  const top = !t.next, pct = top ? 100 : Math.max(0, Math.min(100, Math.round((t.points - t.min) / (t.next - t.min) * 100)));
  const keep = top ? `Đã đạt hạng cao nhất. Giữ trên <b>${fmt(t.min)} điểm</b> để không xuống hạng.` : `Còn <b>${fmt(t.next - t.points)} điểm</b> lên hạng <b>${lbEsc(t.nextName)}</b>`;
  return `<section class="ar-hero t-${lbEsc(t.id)}" aria-label="Hạng hiện tại" style="--tc:${arTierCol(t.id)}">
    ${arMedal(t.id, t.name)}
    <div class="ar-hero-body">
      <div class="ar-hero-top">
        <div><span class="ar-eyebrow">Hạng hiện tại${top ? ' · cao nhất' : ''} ${arInfoBtn(d)}</span><b class="ar-tname">${lbEsc(t.name)}</b></div>
        <div class="ar-pts"><b>${fmt(t.points)}</b>${top ? ' điểm' : ` / ${fmt(t.next)} điểm`}</div>
      </div>
      <div class="ar-prog" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${pct}" aria-label="${top ? 'Đã ở hạng cao nhất' : 'Điểm tới ' + lbEsc(t.nextName)}"><i style="width:${pct}%"></i></div>
      <div class="ar-hero-foot"><span>${keep}</span>${t.protect ? `<span class="ar-chip jade" data-tip="Thua không bị trừ điểm">Bảo vệ ${t.protect} trận thua</span>` : ''}</div>
    </div>
  </section>`;
}

/** Bốn ô chỉ số: hạng trong hạng, chiến tích, lượt hôm nay (vạch), điều tức. */
function arStats(d, cd){
  const me = d.me, left = Math.max(0, d.daily - d.used), t = d.tier;
  const pips = d.daily <= 40 ? `<span class="ar-pips" aria-hidden="true">${Array.from({length: d.daily}, (_, i) => `<i${i < left ? ' class="on"' : ''}></i>`).join('')}</span>` : '';
  const rest = cd > 0 ? `<b class="ar-cd"><i class="dot wait"></i>${Math.ceil(cd / 1000)} giây</b><small>${left ? 'Đang điều tức' : 'Hết lượt, làm mới 0h'}</small>`
    : `<b class="ar-cd"><i class="dot"></i>Sẵn sàng</b><small>${left ? 'Nghỉ ngắn giữa các trận' : 'Hết lượt, làm mới 0h'}</small>`;
  return `<section class="ar-stats" aria-label="Chỉ số của bạn">
    <div><span>${t ? 'Hạng trong ' + lbEsc(t.name) : 'Hạng của bạn'}</span><b>${me ? '#' + me.rank : '—'}</b><small>/ ${fmt(d.total || 0)} tu sĩ${t ? ' cùng hạng' : ''}</small></div>
    <div><span>Chiến tích</span><b><em class="w">${fmt(me?.wins || 0)}</em><i> T </i><em class="l">${fmt(me?.losses || 0)}</em><i> B</i></b><small>${t ? 'Mọi trận đều tính điểm' : 'Tổng thắng / bại'}</small></div>
    <div><span>Lượt hôm nay</span><b>${fmt(left)}<i> / ${d.daily}</i></b>${pips}</div>
    <div><span>Điều tức</span>${rest}</div>
  </section>`;
}

/** Thang hạng: sáu nấc nối nhau, các nấc đã đạt đặc, nấc hiện tại có vòng sáng. */
function arLadder(t){
  const idx = Math.max(0, t.all.findIndex(x => x.id === t.id));
  // mỗi nấc là nút: bấm để xem người chơi ở hạng đó (cao hơn hoặc thấp hơn hạng của mình)
  return `<section class="ar-ladder" aria-label="Thang hạng"><ol style="--n:${t.all.length};--cur:${idx}">${t.all.map((x, i) => `<li class="${i < idx ? 'done' : i === idx ? 'on' : ''}${i === arViewTier ? ' sel' : ''}" style="--c:${arTierCol(x.id)}"><button type="button" class="ar-tier-btn" data-ar-tier="${i}" aria-pressed="${i === arViewTier}" aria-label="Xem người chơi hạng ${lbEsc(x.name)}" data-tip="Xem người chơi hạng ${lbEsc(x.name)}"><span class="ar-node"></span><b>${lbEsc(x.name)}</b><small>${fmt(x.min)}</small></button></li>`).join('')}</ol><p class="ar-ladder-hint">Bấm vào một hạng để xem người chơi ở hạng đó</p></section>`;
}

/** Bảng người chơi của hạng đang xem (bấm nấc trên thang hạng): xếp theo điểm, chỉ để xem, bấm tên mở trang bị. */
function arTierPanel(d){
  if (arViewTier < 0 || !d.tier) return '';
  const info = d.tier.all[arViewTier], col = arTierCol(info.id);
  if (!arTierBoard || arTierBoard.tier !== arViewTier)
    return `<section class="ar-tboard" style="--tc:${col}" aria-live="polite"><div class="ar-tboard-head"><b>Hạng ${lbEsc(info.name)}</b><button type="button" class="ar-tboard-x" data-ar-tier-close aria-label="Đóng">Đóng</button></div><p class="lb-empty">Đang tải…</p></section>`;
  const rows = arTierBoard.rows.map(r => `<li class="lb-row ar-row ar-view${r.me ? ' me' : ''}" data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}">
      <span class="lb-rk">${r.rank}</span><span class="ar-av${r.me ? ' me' : ''}" aria-hidden="true">${lbEsc(arInit(r.name))}</span>
      <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)} · Lực chiến ${fmt(r.power)} · ${fmt(r.wins)}T/${fmt(r.losses)}B</small></span>
      <span class="ar-pt ar-pt-big"><b>${fmt(r.points || 0)}</b> điểm</span></li>`).join('');
  return `<section class="ar-tboard" style="--tc:${col}" aria-live="polite">
    <div class="ar-tboard-head"><b>Hạng ${lbEsc(info.name)}</b><small>${fmt(arTierBoard.total)} người · từ ${fmt(info.min)} điểm</small><button type="button" class="ar-tboard-x" data-ar-tier-close aria-label="Đóng">Đóng</button></div>
    ${rows ? `<ol class="lb-list ar-list">${rows}</ol>` : '<p class="lb-empty">Chưa có ai ở hạng này.</p>'}
  </section>`;
}

async function arShowTier(i){
  if (arViewTier === i){ arViewTier = -1; arTierBoard = null; renderArena(); return; }   // bấm lại để đóng
  arViewTier = i; arTierBoard = null; renderArena();
  try {
    const b = await api('GET', `/api/players/${S.id}/arena/tier/${i}`);
    if (arViewTier === i){ arTierBoard = b; renderArena(); }
  } catch(e){ if (arViewTier === i){ arViewTier = -1; renderArena(); arToast(e.message); } }
}

/** Thưởng ngày / tuần: nhận được thì nổi bật, đã nhận hiện dấu tích, chưa tới hạn thì khóa kèm lý do. */
function arClaims(t){
  const name = id => (typeof ITEM !== 'undefined' && ITEM[id]?.name) || id;
  const rw = c => c.rewards.map(r => `${fmt(r.qty)} ${lbEsc(name(r.item))}`).join(' · ');
  const claim = (c, kind, label) => `<div class="ar-claim${c.can ? ' ready' : ''}"><span><b>${label}</b><small>${rw(c)}</small></span>${c.claimed
    ? '<em><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12l5 5 9-10"/></svg>Đã nhận</em>'
    : `<button type="button" class="ar-go${c.can ? ' primary' : ''}" data-ar-claim="${kind}" ${c.can && !arBusy ? '' : 'disabled'}${c.why ? ` data-tip="${lbEsc(c.why)}"` : ''}>${c.can || !c.why ? 'Nhận' : kind === 'weekly' ? 'Chủ nhật' : 'Chưa đủ'}</button>`}</div>`;
  return `<section class="ar-claims" aria-label="Thưởng hạng">${claim(t.daily, 'daily', 'Thưởng ngày')}${claim(t.weekly, 'weekly', 'Thưởng tuần · Chủ nhật')}</section>`;
}

/** Luật Lôi đài gói trong một tooltip (nút ⓘ) cho gọn giao diện; mỗi dòng một ý, dòng cuối riêng cho Lôi đài giả (dev). */
function arInfoTip(d){
  const t = d.tier, lines = ['Luật Lôi đài',
    `Khiêu chiến người xếp trên trong ${d.window} hạng.${t ? ' Khiêu chiến không đổi chỗ: chỗ đứng trong hạng xếp theo điểm.' : ' Thắng thì đổi vị trí với đối thủ, thua giữ nguyên hạng.'}`,
    ...(t ? ['Chỉ thách đấu người cùng hạng.', 'Thắng +25 đến +40 điểm · thua −15.', 'Lên hạng được bảo vệ 3 trận thua.'] : []),
    ...(t ? ['Khiêu chiến người xếp dưới vẫn tính điểm (+25 / −15).'] : ['Khiêu chiến người xếp dưới: không đổi hạng, không tính điểm.']),
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
  const flash = arFlash && performance.now() < arFlash.until ? `<div class="lb-note ar-toast ${arFlash.kind || ""}">${lbEsc(arFlash.text)}</div>` : "";
  v.innerHTML = `${flash}
    ${d.tier ? arTierHero(d.tier, d) : `<div class="ar-infobar">${arInfoBtn(d)}<span>Luật Lôi đài</span></div>`}
    ${arStats(d, cd)}
    ${d.tier ? arLadder(d.tier) + arTierPanel(d) + arClaims(d.tier) : ''}
    ${d.tier ? '' : `<h4 class="ar-h">Tam cường</h4>
    <div class="ar-podium">${d.top.length ? [d.top[1], d.top[0], d.top[2]].filter(Boolean).map(r => arCard(r, d)).join('') : '<p class="lb-empty">Chưa có ai trong Lôi đài.</p>'}</div>`}
    <h4 class="ar-h">${d.tier ? 'Đối thủ cùng hạng' : 'Đối thủ gần hạng bạn'}</h4>
    <ol class="lb-list ar-list">${d.targets.length ? d.targets.map(r => arRow(r, d)).join('') : `<li class="lb-empty">${d.tier ? 'Chưa có đối thủ nào khác cùng hạng.' : 'Chưa có đối thủ nào khác ngoài Tam cường.'}</li>`}</ol>
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
