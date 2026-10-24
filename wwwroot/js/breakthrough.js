/* Vấn Đạo · js/breakthrough.js: tab Đột phá. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// ĐỘT PHÁ (bố cục theo bản thiết kế breakthrough_assets):
//   trái: danh sách 10 cảnh giới · giữa: bảng tên cảnh giới, tu sĩ thiền trên đài đá giữa trận pháp vàng, thẻ "Kế tiếp", tu vi + nút Đột phá
//   phải: điều kiện đột phá, tỉ lệ thành công, sau khi đột phá · bảng kết quả phủ lên toàn khung
// =====================================================================
let btProc = null, btResult = null;
let btLock = false;   // khóa nút Đột phá từ lúc bấm đến khi hiện bảng kết quả (kể cả lúc chạy hoạt ảnh)
const sleep = ms => new Promise(r => setTimeout(r, ms));
const secTxt = ms => { const s = Math.ceil(ms / 1000); return s >= 60 ? `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}` : `${s}s`; };
const btLeft = ms => Math.max(0, ms - (performance.now() - recvAt));
const BT_PAL = {
  luyen_khi: {ring:'#4f7a86', rune:'#7fb8c0', glow:'#5f8e96', mote:'#9fc6cc'},     // lam đậm, xám, lam xỉn
  truc_co:   {ring:'#3f8fb0', rune:'#7fe3e0', glow:'#5fd0e0', mote:'#e6fbff'},     // lam sâu, lam ngọc, bạch ngọc
  kim_dan:   {ring:'#8a7a40', rune:'#f2d27a', glow:'#e6c46a', mote:'#fff3c8'},
  nguyen_anh:{ring:'#6a4a9a', rune:'#d4a8ff', glow:'#a77ae6', mote:'#f0e0ff'},     // tím, tử vân
  hoa_than:  {ring:'#7a8aa0', rune:'#e8f0ff', glow:'#c8d8f0', mote:'#ffffff'},     // bạch kim
  luyen_hu:  {ring:'#3f6fd0', rune:'#8fc8ff', glow:'#4f8fff', mote:'#dcefff'},     // lam hư không
  hop_the:   {ring:'#b05a2a', rune:'#ffc890', glow:'#ff9a5a', mote:'#fff0e0'},     // đỏ cam, khí huyết hợp nhất
  dai_thua:  {ring:'#b08a2a', rune:'#ffe6a0', glow:'#ffd86a', mote:'#fffbe8'},     // kim quang, đạo vận viên mãn
};
/** 10 cảnh giới ở cột trái: tên Việt + tên Anh (bản thiết kế). Cảnh giới chưa có trong DATA hiện khóa. */
const BT_REALMS = [['Luyện Khí','Qi Refining'],['Trúc Cơ','Foundation Establishment'],['Kim Đan','Golden Core'],['Nguyên Anh','Nascent Soul'],['Hóa Thần','Spirit Severing'],
  ['Luyện Hư','Void Refining'],['Hợp Thể','Body Integration'],['Đại Thừa','Mahayana'],['Độ Kiếp','Tribulation Transcendence'],['Chân Tiên','True Immortal']];
const BT_IC = {qi:'img/bt/icon_tuvi.png', body:'img/bt/icon_the.png', stone:'img/bt/icon_linhthach.png', pill:'img/bt/icon_dan.png'};
const BT_SVG = {   // biểu tượng nét vàng cho mục "Sau khi đột phá"
  up:   '<path d="M12 3l6 7h-4v6h-4v-6H6z"/><path d="M5 20h14"/>',
  gate: '<path d="M4 21V10l8-6 8 6v11"/><path d="M9 21v-6h6v6"/>',
  stat: '<circle cx="12" cy="7" r="3"/><path d="M5 21c0-4 3-7 7-7s7 3 7 7"/><path d="M17 4l2-2M19 6h2"/>',
  bag:  '<path d="M5 8h14l-1 13H6z"/><path d="M9 8V6a3 3 0 0 1 6 0v2"/>',
  slot: '<rect x="4" y="4" width="16" height="16" rx="2"/><path d="M12 8v8M8 12h8"/>',
  star: '<path d="M12 3l2.6 5.6 6 .7-4.5 4.1 1.2 6L12 16.5 6.7 19.4l1.2-6L3.4 9.3l6-.7z"/>',
};
const btSvg = k => `<svg viewBox="0 0 24 24" aria-hidden="true">${BT_SVG[k]}</svg>`;
const BT_CHECK = '<svg class="ck" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M7 12.5l3.2 3L17 9"/></svg>';
const BT_WARN = '<svg class="wn" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 7v6M12 16.5v.5"/></svg>';

/** Trạng thái điều kiện: tu vi, vật phẩm, viên mãn, thể trạng. */
function btReqs(){
  const q = qiNow(), rows = [];
  rows.push({ok: q >= S.qiReq, key: 'qi', name: 'Tu vi', val: `<span class="btq" title="${fmtFull(q)} / ${fmtFull(S.qiReq)}">${fmtCompact(q)} / ${fmtCompact(S.qiReq)}</span>`});
  for (const c of S.targetCost){
    const it = ITEM[c.item], ok = c.have >= c.need;
    rows.push({ok, ic: c.item, name: it.name, val: `${fmt(c.have)} / ${fmt(c.need)}`, boss: it.type === 'boss',
      hint: ok ? '' : it.type === 'boss' ? `<a href="#" data-goboss>Đánh Boss ${bossChapterFor(realm().bossItem)?.boss || realm().boss}${bossChapterTag(realm().bossItem)}</a>` : it.type === 'pill' ? '<a href="#" data-gotab="craft">Luyện chế đan</a>' : ''});
  }
  if (S.isPeak) rows.push({ok: true, key: 'peak', name: 'Tu luyện viên mãn', val: S.stageName});
  const debuff = btLeft(S.break.debuffMs), cd = btLeft(S.break.cooldownMs);
  rows.push({ok: !debuff && !cd, key: 'body', name: 'Thể trạng', val: `<span class="btbody">${debuff ? 'Tẩu hỏa · ' + secTxt(debuff) : cd ? 'Điều tức · ' + secTxt(cd) : 'Ổn định'}</span>`});
  return rows;
}
function btReady(){ return !(S.isPeak && !S.hasNext) && btReqs().every(r => r.ok); }

/** Ô biểu tượng của một điều kiện: ảnh thiết kế cho tu vi / thể trạng / linh thạch, còn lại dùng ảnh vật phẩm. */
function btReqIcon(r){
  if (r.key === 'qi' || r.key === 'peak') return `<img src="${BT_IC.qi}" alt="">`;
  if (r.key === 'body') return `<img src="${BT_IC.body}" alt="">`;
  if (r.ic === 'linh_thach') return `<img src="${BT_IC.stone}" alt="">`;
  return itemImg(r.ic);
}

function btNavHtml(){
  const names = BT_REALMS.map(r => r[0]);
  for (const r of DATA.realms) if (!names.includes(r.name)) names.push(r.name);
  return `<p class="bt-h">Cảnh Giới Tu Tiên</p><ol class="bt-rl">${names.slice(0, 10).map((n, i) => {
    const ri = DATA.realms.findIndex(r => r.name === n), en = (BT_REALMS.find(r => r[0] === n) || [])[1] || '';
    const st = ri < 0 ? 'lock' : ri < S.realm ? 'done' : ri === S.realm ? 'cur' : 'next';
    const tip = ri < 0 ? `${n}: cảnh giới chưa khai mở` : `${n}${DATA.realms[ri].desc ? '&#10;' + DATA.realms[ri].desc : ''}`;
    return `<li class="${st} b${i % 10}" data-tip="${tip}" ${st === 'cur' ? 'aria-current="step"' : ''}><i class="dia"><b>${i + 1}</b></i><span><b>${n}</b><small>${en}</small></span>${st === 'lock' ? LOCK_SVG : st === 'done' ? '<em class="dn">✓</em>' : ''}</li>`;
  }).join('')}</ol>`;
}

function renderReq(){
  const pane = $('pane-req');
  if (!$('btCanvas')){
    pane.innerHTML = `<div class="bt">
      <nav class="bt-nav" id="btNav" aria-label="Cảnh giới tu tiên"></nav>
      <section class="bt-mid">
        <div class="bt-stage"><canvas id="btCanvas" width="96" height="84" aria-label="Tu sĩ ngồi thiền giữa trận pháp"></canvas></div>
        <header class="bt-plaque" id="btPlaque"></header>
        <div class="bt-bottom"><div class="bt-next" id="btNext"></div><div class="bt-foot" id="btFoot"></div></div>
      </section>
      <aside class="bt-side">
        <section class="bt-box bt-req" id="btReq"></section>
        <section class="bt-box bt-rbox" id="btRate"></section>
        <section class="bt-box bt-after" id="btAfter"></section>
      </aside>
      <div class="bt-result" id="btResult" hidden></div>
    </div>`;
    const st = pane.querySelector('.bt-stage');
    if (window.ResizeObserver) new ResizeObserver(() => { const c = $('btCanvas'); if (c) c._sized = false; }).observe(st);
  }
  const top = S.isPeak && !S.hasNext, peak = S.isPeak && S.hasNext, pv = S.break.preview;
  const nextName = peak ? DATA.realms[S.realm + 1].name : top ? DATA.lockedRealms[0] : S.targetTitle.replace(' · ', ' ');
  const nextDesc = peak ? DATA.realms[S.realm + 1].desc.split('.')[0] + '.' : top ? 'Cảnh giới chưa khai mở.' : 'Đột phá tầng kế tiếp trong cùng cảnh giới.';

  $('btNav').innerHTML = btNavHtml();
  $('btPlaque').innerHTML = `<b>${S.realmName}</b><span>${S.realmName} ${S.stageName}</span>`;
  const detail = BT_DETAILS[Math.min(S.realm, BT_DETAILS.length - 1)];
  const unlocked = BT_DETAILS.slice(0, Math.min(S.realm + 1, BT_DETAILS.length));
  $('btPlaque').innerHTML += `<small class="bt-detail" data-tip="${unlocked.map(d => d.name + ': ' + d.desc).join('&#10;')}">${unlocked.length} dấu ấn · ${detail.name}</small>`;
  const portrait = $('btCanvas');
  portrait.setAttribute('aria-label', `${S.realmName} ${S.stageName}: ${unlocked.map(d => d.name).join(', ')}`);
  portrait._sized = false;

  // thẻ "Kế tiếp": tên cảnh giới / tầng kế tiếp + chỉ số tăng
  const gains = [['Máu', pv.hp], ['Tấn công', pv.atk], ['Phòng thủ', pv.def]].filter(g => g[1]).map(([n, v]) => `<li>${n} <b>+${fmt(v)}</b></li>`).join('')
    + `<li class="${pv.skillPoints ? '' : 'dim'}">Điểm kỹ năng <b>+${pv.skillPoints || 0}</b></li>`;
  $('btNext').className = `bt-next ${peak ? 'major' : ''} ${top ? 'locked' : ''}`;
  $('btNext').innerHTML = `<p class="bt-nl">Kế tiếp <i>»</i>${peak ? '<em>Đại cảnh giới</em>' : ''}</p>
    <p class="bt-nn">${nextName}</p><p class="bt-nd" data-tip="${peak ? DATA.realms[S.realm + 1].desc : nextDesc}">${nextDesc}</p>${top ? '' : `<ul class="bt-gain">${gains}</ul>`}`;

  // điều kiện
  const reqs = btReqs();
  $('btReq').innerHTML = `<p class="bt-h">Điều Kiện Đột Phá</p>` + (top
    ? `<p class="bt-note">Đã đạt đỉnh phong ${S.realmName}. Cảnh giới ${DATA.lockedRealms[0]} chưa khai mở.</p>`
    : `<ul class="bt-list">${reqs.map(r => `<li class="${r.ok ? 'ok' : 'no'} ${r.boss ? 'boss' : ''}">
        <span class="ti">${btReqIcon(r)}</span>
        <span class="nm">${r.name}<b>${r.val}</b>${r.hint ? `<small>${r.hint}</small>` : ''}</span>
        ${r.key === 'qi' && !r.ok ? '<button type="button" class="bt-more" data-gotab="map" data-tip="Tu vi tăng theo thời gian; thám hiểm, đánh quái hoặc dùng đan dược tu vi để nhanh hơn"><span class="lg">Tăng tu vi </span><i>»</i></button>' : r.ok ? BT_CHECK : BT_WARN}</li>`).join('')}</ul>`);

  // tỉ lệ thành công
  const chance = peak ? S.chance : 1, crit = chance * .2, fail = 1 - chance, major = fail * .25;
  const outs = peak ? [
    ['ok', 'Thành công', chance - crit, `Bước vào ${nextName}`],
    ['crit', 'Đại thành công', crit, 'Đạo cơ vững chắc · +10% máu, thủ vĩnh viễn'],
    ['bad', 'Thất bại', fail - major, 'Linh khí phản phệ · −10% tu vi · điều tức 1 phút'],
    ['major', 'Tẩu hỏa nhập ma', major, '−25% tu vi · suy yếu 5 phút'],
  ] : [['ok', 'Thành công', 1, 'Đột phá chắc chắn']];
  const rateTip = `Tỉ lệ gốc ${Math.round((peak ? S.break.baseChance : 1) * 100)}%${S.bonus && peak ? `&#10;+${Math.round(S.bonus * 100)}% kinh nghiệm từ các lần thất bại` : ''}${S.pillBonus && peak ? `&#10;+${Math.round(S.pillBonus * 100)}% Phá Chướng Đan` : ''}&#10;Tối đa 95%`;
  const rateSub = peak ? ((S.bonus || S.pillBonus) ? `gốc ${Math.round(S.break.baseChance * 100)}%${S.bonus ? ` · +${Math.round(S.bonus * 100)}% kinh nghiệm` : ''}${S.pillBonus ? ` · +${Math.round(S.pillBonus * 100)}% đan` : ''}` : 'đại cảnh giới') : 'đột phá tầng';
  $('btRate').hidden = top;
  $('btRate').innerHTML = `<p class="bt-h">Tỉ Lệ Thành Công</p>
    <div class="bt-rate" data-tip="${rateTip}"><b>${Math.round(chance * 100)}%</b><small>${rateSub}</small></div>
    <ul class="bt-outs ${outs.length > 1 ? 'many' : ''}">${outs.map(([k, n, p, d]) => `<li class="${k}" data-tip="${n}: ${d}"><span>${n}</span><b>${Math.round(p * 100)}%</b><small>→ ${d}</small></li>`).join('')}</ul>`;

  // sau khi đột phá
  const after = top ? [] : [
    ['gate', peak ? `Mở khóa cảnh giới ${nextName}` : `Tiến vào ${nextName}`],
    pv.ratePct && ['up', `Tốc độ tu luyện +${pv.ratePct}%`],
    (pv.hp || pv.atk || pv.def) && ['stat', peak ? 'Thuộc tính cơ bản tăng mạnh' : 'Thuộc tính cơ bản tăng'],
    pv.bagSlots && ['bag', `Hành trang +${pv.bagSlots} ô`],
    pv.newSlots.length && ['slot', `Ô trang bị: ${pv.newSlots.join(', ')}`],
    pv.newStats.length && ['stat', `Chỉ số mới: ${pv.newStats.join(', ')}`],
    pv.skillPoints && ['star', `Điểm kỹ năng +${pv.skillPoints}`],
  ].filter(Boolean);
  $('btAfter').hidden = top;
  $('btAfter').innerHTML = `<p class="bt-h">Sau Khi Đột Phá</p><ul class="bt-al">${after.map(([k, t]) => `<li><i>${btSvg(k)}</i><span>${t}</span></li>`).join('')}</ul>`;

  // tu vi + nút đột phá
  const ready = btReady(), running = !!btProc || btLock;
  const why = top ? '' : reqs.find(r => !r.ok);
  $('btFoot').innerHTML = `<p class="bt-ql">Thời gian đủ tu vi</p><p class="bt-qv" id="btQiTime" title="Ước tính theo tốc độ tu luyện hiện tại, chưa tính tu vi nhận từ thám hiểm."></p>
    <div class="bt-bar"><i id="btBar"></i><em id="btPct"></em></div>
    ${top ? '' : `<button class="bt-go ${ready ? 'ready' : ''}" id="btGo" ${ready && !running ? '' : 'disabled'} aria-label="${peak ? `Thử đột phá ${nextName}` : `${S.realmName} ${S.stageName} → ${nextName}`}"><b>${running ? 'Đang đột phá…' : 'Đột Phá'}</b></button>
    <p class="bt-why ${ready ? '' : 'bad'}" id="btWhy">${running ? '&nbsp;' : ready ? 'Đủ điều kiện' : `Chưa đủ: ${why ? why.name : ''}`}</p>`}`;
  btShowResult();
  liveBreak();
}

function liveBreak(){
  if (!S || !$('btFoot')) return;
  const q = qiNow(), pct = Math.min(1, q / S.qiReq);
  const timer = $('btQiTime');
  if (timer){
    const seconds = S.rate > 0 ? Math.ceil(Math.max(0, S.qiReq - q) / S.rate) : Infinity;
    timer.textContent = q >= S.qiReq ? 'Đã đủ tu vi' : !Number.isFinite(seconds) ? 'Tạm dừng' :
      [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60].map(n => String(n).padStart(2, '0')).join(':');
  }
  document.querySelectorAll('#pane-req .btq').forEach(el => setQiDisplay(el, q, S.qiReq));
  const bar = $('btBar'); if (bar){ bar.style.width = (pct * 100).toFixed(1) + '%'; bar.classList.toggle('full', pct >= 1); $('btPct').textContent = Math.floor(pct * 100) + '%'; }
  const body = document.querySelector('#pane-req .btbody');
  if (body){ const d = btLeft(S.break.debuffMs), c = btLeft(S.break.cooldownMs); body.textContent = d ? 'Tẩu hỏa · ' + secTxt(d) : c ? 'Điều tức · ' + secTxt(c) : 'Ổn định'; }
  // nút đột phá bật lại khi đủ tu vi / hết thời gian điều tức
  const go = $('btGo'); if (go && !btProc && !btLock){ const ok = btReady(); if (go.disabled === ok) renderReq(); }
}

function btShowResult(){
  const el = $('btResult'); if (!el) return;
  if (!btResult){ el.hidden = true; el._html = ''; return; }
  const {kind, before, after, msg} = btResult;
  const win = kind === 'gold' || kind === 'crit' || kind === 'good', major = before.peak;
  const d = (a, b) => b - a;
  const st = after.stats, bs = before.stats;
  let html;
  if (win){
    const rows = [[`Máu`, d(bs.hp, st.hp)], [`Tấn công`, d(bs.atk, st.atk)], [`Phòng thủ`, d(bs.def, st.def)]].filter(r => r[1])
      .map(([n, v]) => `<li>${n} <b>+${fmt(v)}</b></li>`).join('')
      + (after.rate > before.rate ? `<li>Tốc độ tu luyện <b>+${Math.round((after.rate / before.rate - 1) * 100)}%</b></li>` : '')
      + (after.bagCap > before.bagCap ? `<li>Hành trang <b>+${after.bagCap - before.bagCap} ô</b></li>` : '')
      + (after.skillPointsTotal > before.skillPts ? `<li>Điểm kỹ năng <b>+${after.skillPointsTotal - before.skillPts}</b></li>` : '');
    const pv = before.preview;
    const unl = major ? [pv.newSlots.length && `Ô trang bị: ${pv.newSlots.join(', ')}`, pv.newStats.length && `Chỉ số: ${pv.newStats.join(', ')}`,
      bossChapterHint(after.realm)].filter(Boolean) : [];
    html = `<div class="bt-card win ${kind}">
      <p class="t">${major ? (kind === 'crit' ? 'Đại thành công' : 'Đột phá thành công') : 'Đột phá thành công'}</p>
      ${kind === 'crit' ? '<p class="crit">Đạo cơ vững chắc</p>' : ''}
      <p class="lab">Cảnh giới hiện tại</p><p class="realm">${after.realmName}</p><p class="stg">${after.realmName} ${after.stageName}</p>
      <p class="lab">Phần thưởng</p><ul class="rw">${rows}</ul>
      ${kind === 'crit' ? `<p class="note">${msg.split(': ').slice(1).join(': ')}</p>` : ''}
      ${unl.length ? `<p class="lab">Mở khóa</p><ul class="unl">${unl.map(u => `<li>${u}</li>`).join('')}</ul>` : ''}
      <button class="bt-ok" data-btok>Tiếp tục</button></div>`;
  } else {
    const isMajor = kind === 'major';
    html = `<div class="bt-card lose ${kind}">
      <p class="t">${isMajor ? 'Tẩu hỏa nhập ma' : 'Đột phá thất bại'}</p>
      <p class="lab">Hậu quả</p><ul class="rw">
        <li>Tổn <b>${isMajor ? 25 : 10}%</b> tu vi</li>
        <li>${isMajor ? 'Kinh mạch hỗn loạn' : 'Khí tức bất ổn'}: điều tức <b>${isMajor ? '2 phút' : '1 phút'}</b> trước lần thử sau</li>
        ${isMajor ? '<li>Suy yếu <b>5 phút</b>: tu vi ×0.7, công / thủ −10%</li>' : ''}
        <li>Đan dược và linh thạch đã tiêu hao, yêu hạch Boss được giữ lại</li>
        <li>Lần sau tỉ lệ thành công <b>+10%</b></li></ul>
      <button class="bt-ok" data-btok>Tiếp tục</button></div>`;
  }
  if (el._html !== html){ el.innerHTML = html; el._html = html; }   // renderReq chạy lại liên tục: chỉ thay khi nội dung đổi, tránh thẻ nháy
  el.hidden = false;
}

async function btStart(){
  if (busy || btProc || btLock || !btReady()) return;
  btLock = true; const go = $('btGo'); if (go) go.disabled = true;      // khóa ngay khi bấm, chống bấm lặp
  const peak = S.isPeak && S.hasNext;
  const before = {peak, realm: S.realm, stats: {...S.stats}, rate: S.rate, bagCap: S.bagCap, skillPts: S.skillPointsTotal, preview: S.break.preview};
  busy = true; btResult = null;
  btProc = {t0: performance.now(), dur: peak ? 3000 : 1200, peak, res: null, err: null};   // thời gian diễn hoạt (ms) trước khi hiện kết quả
  renderReq();
  try { btProc.res = await api('POST', `/api/players/${S.id}/advance`); } catch(e){ btProc.err = e.message; }
  const wait = btProc.dur - (performance.now() - btProc.t0); if (wait > 0) await sleep(wait);
  const {res, err} = btProc;
  btProc = null;
  if (err){ busy = false; btLock = false; localLog(err, 'bad'); renderReq(); $('btWhy').textContent = err; $('btWhy').className = 'bt-why bad'; return; }
  btFx = {kind: res.kind, t0: performance.now(), fromRealm: before.realm, target: res.state};
  const result = {kind: res.kind, msg: res.message || '', before, after: res.state};
  // bảng kết quả hiện sau khi hoạt ảnh thành công (tia sáng ~2.6s) / thất bại (~1.4s) chạy xong.
  // Trạng thái mới (renderAll vẽ lại mọi tab — nặng) chỉ áp dụng lúc đó, không chen giữa hoạt ảnh gây giật;
  // busy giữ tới lúc ấy để sync() định kỳ cũng không vẽ lại giữa chừng.
  const fx0 = btFx, win = ['gold', 'crit', 'good'].includes(res.kind);
  setTimeout(() => {
    busy = false;
    btLock = false;                                // trạng thái mới đã áp dụng: nút tự tính lại theo điều kiện mới
    setState(res.state);
    // bảng hiện ở khung hình sau lần vẽ lại nặng, để hoạt ảnh xuất hiện chạy trọn từ đầu
    requestAnimationFrame(() => requestAnimationFrame(() => { if (btFx === fx0 && !btProc){ btResult = result; renderReq(); } }));
  }, win ? 2600 : 1400);
  notifyHost('changed'); notifyHost('break', {kind: res.kind === 'crit' ? 'gold' : res.kind === 'major' ? 'bad' : res.kind});
}

// ---- cảnh: trời sao + núi đá xa, trận pháp phù văn vàng sau lưng, tu sĩ thiền trên đài đá lơ lửng ----
// Toạ độ cảnh theo lưới 96×84 (giữ như cũ), phóng theo chỗ trống của khung giữa; nền vẽ theo pixel thật.
const btImgs = {};
let btFx = null, btMotes = [];
/** Vòng sáng sau lưng (RealmDef.Halo): góc xoay cộng dồn theo thời gian thực, đổi cảnh giới thì vòng mới nở ra từ tâm. */
let btHalo = {a: 0, last: 0, src: null, born: 0};
const BT_HALO_TURN = 40;                                          // giây mỗi vòng (chiều kim đồng hồ)
const BT_GLYPHS = '道法乾坤天地玄黃宇宙洪荒日月星辰陰陽五行';
const btRnd = s => () => { s = (s * 16807) % 2147483647; return (s - 1) / 2147483646; };
let btBg = null, btCircle = null, btSpin = 0, btSpinLast = 0;

/** Nền (vẽ một lần theo cỡ khung): trời đêm lam, sao, tinh vân, hai lớp cột đá có phù văn, sương. */
function btBackdrop(W, H, pal){
  const key = `${W}x${H}:${pal.glow}`;
  if (btBg && btBg.key === key) return btBg.c;
  const c = document.createElement('canvas'); c.width = W; c.height = H;
  const g = c.getContext('2d'), R = btRnd(7);
  let gr = g.createLinearGradient(0, 0, 0, H);
  gr.addColorStop(0, '#050913'); gr.addColorStop(.45, '#0b1730'); gr.addColorStop(.8, '#0a1222'); gr.addColorStop(1, '#04060b');
  g.fillStyle = gr; g.fillRect(0, 0, W, H);
  gr = g.createRadialGradient(W / 2, H * .42, 0, W / 2, H * .42, Math.max(W, H) * .6);
  gr.addColorStop(0, pal.glow + '40'); gr.addColorStop(.5, pal.glow + '14'); gr.addColorStop(1, 'transparent');
  g.fillStyle = gr; g.fillRect(0, 0, W, H);
  for (let i = 0; i < W * H / 2600; i++){ g.globalAlpha = .2 + R() * .6; g.fillStyle = R() < .2 ? '#cfe2ff' : '#ffffff'; const r = R() < .1 ? 1.4 : .7; g.beginPath(); g.arc(R() * W, R() * H * .7, r * W / 700 + .3, 0, 7); g.fill(); }
  g.globalAlpha = 1;
  // cột đá: lớp xa (mờ, thấp) rồi lớp gần (đậm, cao, có phù văn lam)
  for (const [n, col, hMin, hMax, glow] of [[14, '#101a2e', .25, .5, 0], [9, '#080d18', .35, .75, 1]]){
    for (let i = 0; i < n; i++){
      const side = i % 2 ? R() * .32 : .68 + R() * .32;            // hai bên, chừa khoảng giữa cho tu sĩ
      const x = side * W, w = W * (.025 + R() * .045), h = H * (hMin + R() * (hMax - hMin)), y = H - h;
      g.fillStyle = col; g.beginPath();
      g.moveTo(x - w / 2, H); g.lineTo(x - w / 2 + w * .1, y + w * .6); g.lineTo(x - w * .2, y + w * .2); g.lineTo(x + w * .05, y); g.lineTo(x + w * .3, y + w * .35); g.lineTo(x + w / 2 - w * .08, y + w * .7); g.lineTo(x + w / 2, H); g.fill();
      if (R() < .35){ g.fillRect(x - w * .55, y + w * .5, w * 1.1, w * .12); g.beginPath(); g.moveTo(x - w * .7, y + w * .55); g.lineTo(x, y - w * .25); g.lineTo(x + w * .7, y + w * .55); g.fill(); }  // mái đình trên đỉnh
      if (glow) for (let k = 0; k < 4; k++){ if (R() < .5) continue; g.globalAlpha = .35 + R() * .4; g.fillStyle = pal.rune; g.fillRect(x - w * .08, y + h * (.2 + k * .15), w * .16, h * .06); }
      g.globalAlpha = 1;
    }
  }
  // sương
  gr = g.createLinearGradient(0, H * .55, 0, H);
  gr.addColorStop(0, 'rgba(120,160,220,0)'); gr.addColorStop(.6, 'rgba(120,160,220,.10)'); gr.addColorStop(1, 'rgba(10,14,24,.85)');
  g.fillStyle = gr; g.fillRect(0, H * .55, W, H * .45);
  btBg = {key, c};
  return c;
}

/** Trận pháp vàng (vẽ sẵn theo cỡ pixel thật): hai vòng kép, vạch chia, 4 nút thoi, 20 chữ phù văn. */
function btRuneCircle(px){
  const key = Math.round(px);
  if (btCircle && btCircle.key === key) return btCircle.c;
  const S2 = key * 2 + 8, c = document.createElement('canvas'); c.width = c.height = S2;
  const g = c.getContext('2d'), o = S2 / 2, r = key;
  g.translate(o, o); g.strokeStyle = g.fillStyle = '#f6d47a';
  g.shadowColor = '#ffb030'; g.shadowBlur = Math.max(3, r * .07);
  const ring = (rr, w) => { g.lineWidth = w; g.beginPath(); g.arc(0, 0, rr, 0, 7); g.stroke(); };
  ring(r * .99, r * .018); ring(r * .94, r * .008); ring(r * .74, r * .012); ring(r * .7, r * .006); ring(r * .52, r * .006);
  for (let i = 0; i < 72; i++){ const a = i / 72 * Math.PI * 2, l = i % 6 ? .02 : .045; g.lineWidth = r * .006; g.beginPath(); g.moveTo(Math.cos(a) * r * .74, Math.sin(a) * r * .74); g.lineTo(Math.cos(a) * r * (.74 - l), Math.sin(a) * r * (.74 - l)); g.stroke(); }
  g.font = `700 ${r * .14}px "Noto Serif CJK SC","Noto Serif SC","SimSun","Microsoft YaHei",serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
  for (let i = 0; i < 20; i++){ const a = i / 20 * Math.PI * 2; g.save(); g.rotate(a); g.translate(0, -r * .84); g.fillText(BT_GLYPHS[i], 0, 0); g.restore(); }
  for (let i = 0; i < 4; i++){ g.save(); g.rotate(i * Math.PI / 2); g.translate(0, -r * .99); g.beginPath(); g.moveTo(0, -r * .06); g.lineTo(r * .035, 0); g.lineTo(0, r * .06); g.lineTo(-r * .035, 0); g.closePath(); g.fill(); g.restore(); }
  btCircle = {key, c};
  return c;
}

function btFrame(now){
  requestAnimationFrame(btFrame);
  if (paneOff('req')) return;
  const c = $('btCanvas');
  if (!c || !S || !c.offsetParent) return;
  const dpr = Math.min(2, devicePixelRatio || 1);
  if (!c._sized){
    const box = c.parentElement.getBoundingClientRect();
    if (!box.width || !box.height) return;
    c.style.width = box.width + 'px'; c.style.height = box.height + 'px';
    c.width = Math.round(box.width * dpr); c.height = Math.round(box.height * dpr);
    // chỗ trống cho tu sĩ: chừa bảng tên ở trên, thẻ kế tiếp / nút ở dưới (đè một phần đài đá như bản thiết kế)
    const pl = $('btPlaque'), bt = c.closest('.bt-mid')?.querySelector('.bt-bottom');
    c._top = (pl ? pl.offsetTop + pl.offsetHeight : 0) * dpr;
    c._bot = (bt ? bt.offsetHeight * .62 : 0) * dpr;
    c._sized = true;
  }
  const W = c.width, H = c.height, g = c.getContext('2d');
  const availH = Math.max(40, H - c._top - c._bot);
  const sc = Math.min(W / 96 * .98, availH / 84);                 // pixel thật mỗi đơn vị lưới
  const ox = W / 2 - 48 * sc, oy = c._top + (availH - 84 * sc) * .55;
  const t = reduce ? 0 : now / 1000;
  const k = btProc ? Math.min(1, (now - btProc.t0) / btProc.dur) : 0;     // tiến độ 5 giai đoạn
  const fx = btFx ? (now - btFx.t0) / 1000 : 99, win = btFx && ['gold','crit','good'].includes(btFx.kind), lose = btFx && !win;
  const fxOn = fx < 2.4;
  const visual = win && fx < 2.6 && btFx.target ? btFx.target : S;
  const visualRealm = DATA.realms[visual.realm], pal = BT_PAL[visualRealm.id] || BT_PAL.luyen_khi;
  const evolution = btEvolutionState(visual.realm, visual.stage, visualRealm.stages.length, t,
    reduce ? 0 : k, win && fx < 2.6 && !reduce ? fx : null, btFx?.fromRealm ?? S.realm);
  const ready = btReady();
  g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1;
  g.drawImage(btBackdrop(W, H, pal), 0, 0);
  g.setTransform(sc, 0, 0, sc, ox, oy);
  const dot = (x, y, col, a = 1, r = .45) => { g.globalAlpha = a; g.fillStyle = col; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill(); };
  // quầng sáng sau lưng theo năng lượng
  const energy = btProc ? .25 + k * .75 : fxOn ? (1 - fx / 2.4) : ready ? .2 : .08;
  const glowCol = lose && fxOn ? '#5a1a14' : win && fxOn ? '#2a5a66' : pal.glow;
  let gr = g.createRadialGradient(48, 42, 2, 48, 42, 46);
  gr.addColorStop(0, glowCol); gr.addColorStop(1, 'transparent');
  g.globalAlpha = .25 + .5 * energy; g.fillStyle = gr; g.fillRect(0, -10, 96, 96);
  // trận pháp vàng đứng sau lưng, xoay chậm; đột phá thì sáng và xoay nhanh dần
  const dts = btSpinLast ? Math.min(.1, (now - btSpinLast) / 1000) : 0; btSpinLast = now;
  if (!reduce) btSpin += dts * (btProc ? .15 + k * .9 : .05);
  const RC = 37;
  const rc = btRuneCircle(RC * sc);
  let ca = btProc ? .85 + .15 * k : fxOn && win ? 1 : ready ? 1 : .8;
  ca *= .28 + evolution.rank * .1;
  if (lose && fxOn) ca *= reduce ? .5 : .5 + Math.sin(fx * 4) * .15;
  g.save(); g.globalAlpha = ca; g.translate(48, 40); g.rotate(btSpin); g.drawImage(rc, -rc.width / sc / 2, -rc.height / sc / 2, rc.width / sc, rc.height / sc);
  if (btProc || ready){ g.globalCompositeOperation = 'lighter'; g.globalAlpha = ca * (btProc ? .25 + .5 * k : .18); g.drawImage(rc, -rc.width / sc / 2, -rc.height / sc / 2, rc.width / sc, rc.height / sc); }
  g.restore();
  // vòng sáng cảnh giới bên trong trận pháp
  const hsrc = visualRealm.halo;
  if (hsrc){
    if (!btImgs[hsrc]){ btImgs[hsrc] = new Image(); btImgs[hsrc].src = hsrc; }
    if (btHalo.src !== hsrc){ if (btHalo.src) btHalo.born = now; btHalo.src = hsrc; }
    const dh = btHalo.last ? Math.min(.1, (now - btHalo.last) / 1000) : 0; btHalo.last = now;
    if (!reduce) btHalo.a += dh * Math.PI * 2 / BT_HALO_TURN * (btProc ? 1 + k * 3 : 1);
    const hi = btImgs[hsrc];
    if (hi.complete && hi.naturalWidth){
      const grow = reduce || !btHalo.born ? 1 : Math.min(1, (now - btHalo.born) / 800), e = 1 - (1 - grow) ** 3;
      const D = 52 * (.35 + .65 * e);
      let ha = (btProc ? .7 + .3 * k : ready ? .9 : .65) * e;
      if (lose && fxOn) ha *= .5;
      g.save(); g.globalAlpha = ha; g.translate(48, 40); g.rotate(btHalo.a); g.drawImage(hi, -D / 2, -D / 2, D, D); g.restore();
    }
  }
  // đài đá lơ lửng: mặt trên phát sáng lam, thân đá lởm chởm, vết nứt và dòng linh khí chảy xuống
  const cx = 48, cy = 75, rx = 40, ry = 7.5;
  g.globalAlpha = 1;
  g.beginPath(); g.ellipse(cx, cy, rx, ry, 0, 0, Math.PI);
  const jag = [[40, 0], [36, 5], [30, 7], [24, 11], [17, 12], [10, 16], [4, 15], [0, 19], [-5, 15], [-11, 17], [-18, 12], [-25, 11], [-31, 7], [-37, 5], [-40, 0]];
  for (const [dx, dy] of jag) g.lineTo(cx + dx, cy + ry * .6 + dy);
  g.closePath();
  gr = g.createLinearGradient(0, cy, 0, cy + 26); gr.addColorStop(0, '#34486e'); gr.addColorStop(.45, '#18233a'); gr.addColorStop(1, '#070a12');
  g.fillStyle = gr; g.fill();
  const flow = lose && fxOn ? '#e06a40' : pal.rune, fa = btProc ? .5 + .5 * k : ready ? .55 : .35;
  g.strokeStyle = flow; g.lineWidth = .35;
  for (const [x0, y0, x1, y1] of [[-30, 8, -27, 15], [-14, 9, -12, 18], [6, 10, 4, 20], [21, 9, 23, 16], [33, 6, 34, 11]]){
    const p = (t * .4 + x0 * .01) % 1;
    g.globalAlpha = fa * .6; g.beginPath(); g.moveTo(cx + x0, cy + y0); g.lineTo(cx + x1, cy + y1); g.stroke();
    g.globalAlpha = fa; dot(cx + x0 + (x1 - x0) * p, cy + y0 + (y1 - y0) * p, '#e6f4ff', fa, .35);
  }
  g.globalAlpha = 1;
  g.beginPath(); g.ellipse(cx, cy, rx, ry, 0, 0, 7);
  gr = g.createRadialGradient(cx, cy, 2, cx, cy, rx); gr.addColorStop(0, '#3a5a90'); gr.addColorStop(.6, '#1e2e4c'); gr.addColorStop(1, '#141e34');
  g.fillStyle = gr; g.fill();
  g.save(); g.shadowColor = pal.rune; g.shadowBlur = 4 * sc / 4; g.strokeStyle = pal.rune; g.globalAlpha = .55 + .3 * energy; g.lineWidth = .6; g.stroke(); g.restore();
  // vòng phù văn trên mặt đài: sáng dần theo giai đoạn
  const ringCol = lose && fxOn ? '#c0503a' : win && fxOn ? '#e6fbff' : pal.rune;
  const ringA = btProc ? .5 + k * .5 : fxOn ? 1 - fx / 2.4 * .6 : ready ? .7 : .4;
  g.save(); g.shadowColor = ringCol; g.shadowBlur = 3 * sc * (btProc ? .5 + k : .6);
  g.strokeStyle = ringCol;
  for (const [r2, w, dash, sp] of [[.86, .45, [3, 1.5], 1], [.62, .35, [1.5, 1.5], -1.6], [.38, .3, [], 0]]){
    g.globalAlpha = ringA; g.lineWidth = w; g.setLineDash(dash); g.lineDashOffset = -t * 3 * sp * (btProc ? 1 + k * 3 : 1);
    g.beginPath(); g.ellipse(cx, cy, rx * r2, ry * r2, 0, 0, 7); g.stroke();
  }
  g.setLineDash([]);
  const lit = btProc ? Math.floor(k * 8.99) : fxOn && win ? 8 : ready ? 8 : 0;
  for (let i = 0; i < 8; i++){
    const a = i / 8 * Math.PI * 2 - t * .12;
    const on = i < lit, col = lose && fxOn ? '#e06a40' : on ? '#ffffff' : '#3a5070';
    dot(cx + Math.cos(a) * rx * .74, cy + Math.sin(a) * ry * .74, col, on ? .7 + .3 * Math.sin(t * 4 + i) : .6, .7);
  }
  g.restore();
  // tu sĩ ngồi thiền theo trang phục đang mặc (img/med_<cảnh giới>.png)
  const src = curSkin()?.heroMed || visualRealm.heroMed || 'img/med_ref.png';
  if (!btImgs[src]){ btImgs[src] = new Image(); btImgs[src].src = src; }
  const im = btImgs[src];
  if (im.complete && im.naturalWidth){
    const breathScale = reduce ? 1 : 1 + evolution.breath * .006;
    const h = 56 * breathScale, w = 56 * im.naturalWidth / im.naturalHeight;
    const sx = 48 - w / 2, sy = 75 - h - evolution.lift;
    // viền linh khí quanh người (silhouette lệch nhẹ, sáng dần)
    const aur = btProc ? .25 + k * .5 : fxOn && win ? .7 * (1 - fx / 2.4) : .14;
    if (!im._sil){ const o = document.createElement('canvas'); o.width = im.naturalWidth; o.height = im.naturalHeight; const og = o.getContext('2d'); og.drawImage(im, 0, 0); og.globalCompositeOperation = 'source-in'; og.fillStyle = '#e6fbff'; og.fillRect(0, 0, o.width, o.height); im._sil = o; }
    btEvolutionLayer(g, 'back', evolution, pal, im, {w,h});
    g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
    g.globalAlpha = aur;
    for (const [dx, dy] of [[-.6,0],[.6,0],[0,-.6],[0,.6]]) g.drawImage(im._sil, sx + dx, sy + dy, w, h);
    g.globalAlpha = 1;
    if (lose && fxOn) g.globalAlpha = .8;
    g.drawImage(im, sx, sy, w, h);
    g.globalAlpha = 1;
    btEvolutionLayer(g, 'front', evolution, pal, im, {w,h});
  }
  // đột phá thành công: từng tia sáng vàng bắn vút lên quanh tu sĩ (đại cảnh giới nhiều tia hơn)
  if (win && fx < 2.6 && !reduce){
    const n = btFx.kind === 'good' ? 16 : 28, h = i => { const x = Math.sin(i * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); };
    if (fx < .35){ g.globalAlpha = .55 * (1 - fx / .35); g.fillStyle = '#fff3c4'; g.fillRect(44, -20, 8, 96); g.globalAlpha = .25 * (1 - fx / .35); g.fillRect(38, -20, 20, 96); }   // cột sáng lóe lúc đầu
    for (let i = 0; i < n; i++){
      const delay = h(i) * 1.1, life = .7 + h(i + 50) * .5, p = (fx - delay) / life;
      if (p <= 0 || p >= 1) continue;
      const x0 = 48 + (h(i + 100) - .5) * 46, y = 76 - p * (70 + h(i + 150) * 20), len = 6 + h(i + 200) * 10, w = h(i + 250) < .3 ? 1 : .5;
      const x = x0 + (x0 - 48) * p * .25, a = p < .15 ? p / .15 : 1 - (p - .15) / .85;
      const gr2 = g.createLinearGradient(0, y, 0, y + len);
      gr2.addColorStop(0, '#fffbe6'); gr2.addColorStop(.25, '#f8d25a'); gr2.addColorStop(1, 'rgba(242,201,76,0)');
      g.globalAlpha = a; g.fillStyle = gr2; g.fillRect(x - w / 2, y, w, len);
      g.fillStyle = '#ffffff'; g.fillRect(x - w / 2, y, w, .75);             // đầu tia sáng trắng
    }
    g.globalAlpha = 1;
  }
  // hạt linh khí: lúc thường bay lên lững lờ, khi đột phá xoáy vào người, thất bại thì văng ra
  const want = reduce ? 6 : btProc ? 18 + k * 28 : 6 + evolution.rank * 2;
  while (btMotes.length < want) btMotes.push({a: Math.random() * Math.PI * 2, r: 30 + Math.random() * 16, y: 10 + Math.random() * 64, s: .5 + Math.random(), life: Math.random()});
  if (btMotes.length > want + 4) btMotes.length = Math.round(want);
  for (const m of btMotes){
    const dt = reduce ? 0 : dts;
    if (btProc){ m.r -= (8 + k * 30) * m.s * dt; m.a += (1 + k * 3) * m.s * dt; if (m.r < 4){ m.r = 30 + Math.random() * 16; m.y = 10 + Math.random() * 64; } }
    else if (lose && fxOn){ m.r += 40 * dt; }
    else { m.y -= m.s * 6 * dt; if (m.y < -6){ m.y = 76; m.r = 18 + Math.random() * 30; } m.a += .2 * dt; }
    const x = 48 + Math.cos(m.a) * m.r, y = btProc ? 46 + Math.sin(m.a) * m.r * .45 : m.y;
    const col = lose && fxOn ? (m.s > 1 ? '#e06a40' : '#8a2a1a') : (btProc && k > .8) || (win && fxOn) ? (m.s > 1.2 ? '#f2d27a' : '#ffffff') : pal.mote;
    dot(x, y, col, (btProc ? .5 + k * .5 : .55) * (m.s > 1.2 ? 1 : .7), m.s > 1.2 ? .5 : .35);
  }
  // giai đoạn cuối: cột linh quang mảnh, không quá chói
  if (!reduce && ((btProc && k > .8) || (win && fx < 1.2))){
    const a = btProc ? (k - .8) * 4 : 1 - fx / 1.2;
    const gc = g.createLinearGradient(0, -20, 0, 74); gc.addColorStop(0, 'rgba(230,251,255,0)'); gc.addColorStop(1, 'rgba(255,255,255,1)');
    g.globalAlpha = a * .5; g.fillStyle = gc; g.fillRect(46.5, -20, 3, 94); g.globalAlpha = a * .8; g.fillRect(47.6, -20, .8, 94);
    if (win) for (let i = 0; i < 6; i++){ const ang = t * 2 + i; dot(48 + Math.cos(ang) * (6 + fx * 30), 44 + Math.sin(ang) * (4 + fx * 14), '#f2c94c', a, .5); }
  }
  g.globalAlpha = 1;
}
requestAnimationFrame(btFrame);
addEventListener('resize', () => { const c = $('btCanvas'); if (c) c._sized = false; });

function initBreak(){
  $('pane-req').addEventListener('click', e => {
    if (e.target.closest('#btGo')){ btStart(); return; }
    if (e.target.closest('[data-btok]')){ btResult = null; renderReq(); return; }
    if (e.target.closest('[data-goboss]')){ e.preventDefault(); ddGoBoss(); return; }
    const gt = e.target.closest('[data-gotab]'); if (gt){ e.preventDefault(); openTab(gt.dataset.gotab, true); }
  });
}

const pct = v => `+${Math.round(v)}%`;
// hành trang chia trang: 6 cột × 7 hàng mỗi trang, thanh "‹ 1 2 3 ›" dưới lưới
let BAG_PER_PAGE = 48;                                          // 8 cột × 6 hàng; cửa sổ Tu luyện: fitBagGrid() đổi theo số cột × hàng vừa khung
let bagPage = 0;
/** Thanh phân trang: ‹ trước · số trang (rút gọn bằng … khi nhiều) · sau ›. cur bắt đầu từ 0. */
function pagerHtml(cur, pages, attr){
  const nums = [];
  for (let p = 0; p < pages; p++){
    if (pages <= 7 || p === 0 || p === pages - 1 || Math.abs(p - cur) <= 1) nums.push(p);
    else if (nums[nums.length - 1] !== '…') nums.push('…');
  }
  const btn = (p, label, dis, aria) => `<button type="button" ${attr}="${p}" ${dis ? 'disabled' : ''} aria-label="${aria}">${label}</button>`;
  return btn(cur - 1, '‹', cur <= 0, 'Trang trước')
    + nums.map(p => p === '…' ? '<span class="pg-gap">…</span>'
      : `<button type="button" ${attr}="${p}" ${p === cur ? 'aria-current="page"' : ''} aria-label="Trang ${p + 1}">${p + 1}</button>`).join('')
    + btn(cur + 1, '›', cur >= pages - 1, 'Trang sau');
}
// sắp xếp trang bị trong túi (nhớ lựa chọn trên máy này)
const GEAR_SORTS = {
  slot: {name:'Loại', key: it => it.slot ? DATA.slots.findIndex(s => s.id === it.slot) : DATA.items.indexOf(it)},
  grade:{name:'Phẩm cấp', key: it => -(RAR_KEYS.indexOf(it.rarity || 'common') * 1000 + (it.itemLevel || 0) * 10) + (it.slot ? DATA.slots.findIndex(s => s.id === it.slot) : 0) / 100},   // Thiên → Phàm, cùng phẩm thì cấp cao trước
  atk:  {name:'Tấn công', key: it => it.slot ? -(it.atk||0) : DATA.items.indexOf(it)},
  hp:   {name:'Máu', key: it => it.slot ? -(it.hp||0) : DATA.items.indexOf(it)},
  def:  {name:'Phòng thủ', key: it => it.slot ? -(it.def||0) : DATA.items.indexOf(it)},
  name: {name:'Tên A–Z', key: it => it.name},
};
let gearSort = 'slot', sortOpen = false;
try { const v = localStorage.getItem('van-dao-gear-sort'); if (v && GEAR_SORTS[v]) gearSort = v; } catch(e){}
