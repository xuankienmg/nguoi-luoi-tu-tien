/* Vấn Đạo · js/spin.js: Luyện chế → Vòng quay: đổi linh thạch lấy vé, quay vòng 10 phẩm trang bị (Phàm → Huyền Tiên)
   hoặc vòng Linh Thú 6 ô (Thú Đản Phàm → Thượng Cổ, vé riêng: Vé Linh Thú). */
const SPIN_TICKET = 've_tai_xiu';
const PET_TICKET = 've_linh_thu', PET_EGGS = ['thu_dan_pham', 'thu_dan_linh', 'thu_dan_huyen', 'thu_dan_dia', 'thu_dan_thien', 'thu_dan_thuong_co'];
/** Cấu hình vòng đang xem: trang bị 10 ô × 36° · Linh Thú 6 ô × 60°. */
const spinCfg = () => spinMode === 'pet'
  ? {n: 6, ticket: PET_TICKET, price: DATA.petSpinPrice || 3e5, w: DATA.petSpinWeights || [45, 30, 17, 7, .9, .1], icon: q => PET_EGGS[q], title: 'Quay Linh Thú', mode: '&mode=pet', what: 'Thú Đản', big: 3}
  : {n: 10, ticket: SPIN_TICKET, price: DATA.spinPrice || 1e5, w: spinW(), icon: q => spinIcon(q), title: 'Vòng quay', mode: '', what: 'trang bị', big: 4};
const spinRarities = () => spinMode==='pet' ? ['common','uncommon','rare','epic','legendary','ancient'] : FUSE_R;
const spinSeg = () => 360 / spinCfg().n;
let spinBusy = false, spinRot = 0, spinLast = null, spinKey = '';
let spinMode='gear', spinModePicked=false;   // chưa tự chọn vòng: mở theo vòng của sự kiện đang chạy
const spinTickets = () => S.inventory[spinCfg().ticket] || 0;
const spinQuota = () => (spinMode === 'pet' ? S.petSpinDaily : S.spinDaily);
const spinDailyLimit = () => spinQuota()?.limit ?? (spinMode === 'pet' ? 300 : 1000);
const spinRemaining = () => Math.max(0, spinQuota()?.remaining ?? spinDailyLimit());
const spinPrice = () => spinCfg().price;
const spinW = () => DATA.spinWeights || [32, 25, 17, 11, 6.5, 4, 2.3, 1.4, .75, .05];
const spinPct = w => `${w >= 1 ? +w.toFixed(1) : +w.toFixed(2)}%`.replace('.', ',');
const spinReduced = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

/** Bộ trang bị vòng quay đang ra: cảnh giới cao nhất có đồ, không vượt cảnh giới hiện tại (giống GameData.SpinPool). */
function spinPoolRealm(){
  const lv = DATA.items.filter(i => i.base && i.slot).map(i => i.minRealm || 0).filter(r => r <= S.realm);
  return lv.length ? Math.max(...lv) : 0;
}
function spinPool(q){
  const r = spinPoolRealm();
  return DATA.items.filter(i => i.base && i.slot && (i.minRealm || 0) === r && i.rarity === FUSE_R[q]);
}
/** Món minh họa cho ô q: ưu tiên vũ khí. */
const spinIcon = q => { const p = spinPool(q); return (p.find(i => i.slot === 'weapon') || p[0])?.id; };

const spinPt = (r, a) => [r * Math.sin(a * Math.PI / 180), -r * Math.cos(a * Math.PI / 180)];
function spinDisc(){
  // pad: chừa chỗ cho quầng sáng quanh icon (foreignObject cắt mọi thứ tràn ra ngoài khung)
  const R1 = 92, R0 = 26, cfg = spinCfg(), SEG = 360 / cfg.n, ic = cfg.n <= 5 ? 34 : 26, pad = 10;
  const segs = spinRarities().slice(0, cfg.n).map((r, i) => {
    const a0 = -SEG / 2, a1 = SEG / 2;
    const [x0, y0] = spinPt(R1, a0), [x1, y1] = spinPt(R1, a1), [x2, y2] = spinPt(R0, a1), [x3, y3] = spinPt(R0, a0);
    const col = RARITY[r].c, k = cfg.icon(i), nm = RARITY[r].n.split(' ');
    const name = nm.length > 1 ? `<text class="sp-nm" y="-83">${nm[0]}</text><text class="sp-nm" y="-75">${nm[1]}</text>` : `<text class="sp-nm" y="-79">${nm[0]}</text>`;
    return `<g class="sp-seg" data-q="${i}" transform="rotate(${i * SEG})" style="--c:${col}">
      <path class="sp-bg ${i % 2 ? 'alt' : ''}" d="M${x0},${y0} A${R1},${R1} 0 0 1 ${x1},${y1} L${x2},${y2} A${R0},${R0} 0 0 0 ${x3},${y3} Z"/>
      <path class="sp-rim" d="M${x0},${y0} A${R1},${R1} 0 0 1 ${x1},${y1}"/>
      ${name}
      ${k ? `<foreignObject x="${-ic / 2 - pad}" y="${-44 - ic - pad}" width="${ic + pad * 2}" height="${ic + pad * 2}"><div xmlns="http://www.w3.org/1999/xhtml" class="sp-ic">${itemImg(k, '', true)}</div></foreignObject>` : ''}
      </g>`;
  }).join('');
  const dots = Array.from({length: 20}, (_, i) => { const [x, y] = spinPt(96, i * 18 + 9); return `<circle class="sp-dot ${i % 2 ? 'b' : ''}" cx="${x}" cy="${y}" r="1.7"/>`; }).join('');
  return `<svg class="sp-svg" viewBox="-100 -100 200 200" aria-hidden="true">
    <circle class="sp-ring" r="99"/><g class="sp-disc" id="spDisc" style="transform:rotate(${spinRot}deg)"><circle class="sp-anchor" r="99" fill="transparent"/>${segs}</g>${dots}</svg>`;
}

function renderSpin(){
  const pane = $('pane-spin'); if (!pane || !S) return;
  if (!spinModePicked && S.event?.active) spinMode = S.event.track === 'pet' ? 'pet' : 'gear';
  const cfg = spinCfg();
  if (!$('spWheel') || pane.dataset.mode !== spinMode){
    pane.dataset.mode = spinMode; spinKey = '';
    // Bố cục theo thiết kế bảng Sự kiện (11/2026): đầu màn + bánh xe bên trái + cột phải (đổi vé · lượt/bảo hiểm · tỉ lệ).
    pane.innerHTML = `<div class="sp2 ${spinMode === 'pet' ? 'sp-pet' : ''}">
      <header class="sp2-head"><div><small id="spKick"></small><h2>Vòng quay</h2></div>
        <div class="sp2-tools"><div class="sp2-seg" role="group" aria-label="Loại vòng quay">
            <button type="button" data-sp-mode="gear" aria-pressed="${spinMode === 'gear'}">Trang bị</button><button type="button" data-sp-mode="pet" aria-pressed="${spinMode === 'pet'}">Linh Thú</button></div>
          <button class="sp-hbtn sp2-hist" id="spHist" aria-label="Lịch sử quay" aria-expanded="false"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 12a9 9 0 1 0 3-6.7"/><path d="M3 4v5h5"/><path d="M12 7v5l3 2"/></svg><em id="spHistN"></em></button></div></header>
      <div class="sp2-grid">
        <section class="sp2-wheel" aria-label="Bánh xe">
          <div class="sp-stage">
            <div class="sp-wheel" id="spWheel"></div>
            <i class="sp-ptr" aria-hidden="true"></i>
            <button class="sp-hub" data-sp-go="1" id="spHub" aria-label="Quay 1 lượt">Quay</button>
          </div>
          <div class="sp2-act" id="spAct"></div>
        </section>
        <aside class="sp2-side">
          <section class="sp2-card gold" id="spBuy" aria-label="Đổi vé"></section>
          <section class="sp2-card" id="spLim" aria-label="Lượt hôm nay và bảo hiểm"></section>
          <section class="sp2-card sp2-odds" aria-label="Tỉ lệ"><small class="sp2-h" id="spOddsH"></small><ol id="spOdds"></ol></section>
        </aside>
      </div>
    </div>`;
  }
  const key = `${spinMode}:${spinPoolRealm()}`;
  if (key !== spinKey && !spinBusy){ spinKey = key; $('spWheel').innerHTML = spinDisc(); }
  const tk = spinTickets(), st = S.inventory.linh_thach || 0, price = spinPrice(), canBuy = Math.floor(st / price);
  const set = ITEM[spinIcon(0)]?.set;
  $('spKick').textContent = spinMode === 'pet' ? 'QUAY LINH THÚ · MỖI VÉ 1 THÚ ĐẢN' : `QUAY TRANG BỊ · ${set ? 'BỘ ' + set.toUpperCase() : S.realmName.toUpperCase()}`;
  const off = spinBusy || busy, remaining = spinRemaining(), dailyLimit = spinDailyLimit();
  const pityThreshold = spinQuota()?.pityThreshold ?? 100, pityUsed = spinQuota()?.pityUsed ?? 0;
  const pityReady = pityUsed >= pityThreshold - 1;
  const pityReward = spinMode === 'pet' ? 'chắc chắn Thượng Cổ Thú Đản' : 'Tiên 40% · Đạo 30% · Huyền Tiên 30%';
  $('spHub').disabled = off || tk < 1 || remaining < 1;
  $('spWheel').classList.toggle('spinning', spinBusy);
  $('spAct').innerHTML = `<div class="sp2-btns"><button class="sp2-go" data-sp-go="1" ${off || tk < 1 || remaining < 1 ? 'disabled' : ''}>Quay ×1</button>
    <button class="sp2-go alt" data-sp-go="10" ${off || tk < 10 || remaining < 10 ? 'disabled' : ''} data-tip="Quay 10 lượt một lần&#10;Cần 10 vé, 10 lượt quay trong ngày và 10 ô trống trong hành trang">Quay ×10</button></div>
    <p class="sp2-note" role="status">${tk ? `Còn <b>${fmt(tk)}</b> vé` : '<b class="miss">Chưa có vé</b> · đổi vé ở cột bên phải'} · ${spinMode === 'pet' ? 'Thú Đản vào Hành trang, ấp ở Linh thú' : 'trang bị vào Hành trang'}</p>`;
  const btn = (n, lab, cls = '') => `<button class="sp2-buy ${cls}" data-sp-buy="${n}" ${off || canBuy < Math.max(1, n) ? 'disabled' : ''}>${lab}</button>`;
  $('spBuy').innerHTML = `<div class="sp2-tk"><span class="ev-cell lg">${itemImg(cfg.ticket)}</span>
      <span><small>${ITEM[cfg.ticket]?.name || 'Vé'}</small><b class="ev-serif">${fmt(tk)}</b></span>
      <span class="sp2-price"><b>${fmt(price)}</b><br>linh thạch / vé</span></div>
    <div class="sp2-buys">${btn(1, 'Đổi 1')}${btn(10, 'Đổi 10')}${btn(0, `Tối đa${canBuy > 0 ? ` ×${fmt(canBuy)}` : ''}`, 'max')}</div>
    <p class="sp2-sub">${canBuy > 0 ? `Đủ linh thạch đổi ${fmt(canBuy)} vé.` : `Cần thêm <b>${fmt(price - st % price)}</b> linh thạch cho vé kế tiếp.`}</p>`;
  $('spLim').innerHTML = `<div class="sp2-row"><span>Lượt hôm nay</span><b>còn ${fmt(remaining)} / ${fmt(dailyLimit)}</b></div>
    <span class="ev-bar sp2-bar"><i style="width:${remaining / Math.max(1, dailyLimit) * 100}%;background:#5aa575"></i></span>
    <div class="sp2-row"><span>Bảo hiểm</span><b>${pityUsed} / ${pityThreshold}</b></div>
    <span class="ev-bar sp2-bar"><i style="width:${Math.max(2, pityUsed / pityThreshold * 100)}%"></i></span>
    <p class="sp2-sub">${pityReady ? 'Lượt tiếp theo' : `Còn ${pityThreshold - pityUsed} lượt`}: ${pityReward}. Tính riêng từng vòng, giữ qua ngày; lượt hôm nay đặt lại 00:00 giờ Việt Nam.</p>`;
  const w = pityReady ? (spinMode === 'pet' ? [0, 0, 0, 0, 0, 100] : [0, 0, 0, 0, 0, 0, 0, 40, 30, 30]) : cfg.w;
  const hn = spinHistory().length; $('spHistN').textContent = hn || '';
  { const hp = $('spHistPop');                                   // chỉ vẽ lại khi có lượt mới, giữ vị trí cuộn
    if (hp && !hp.hidden){ const h = spinHistHtml(); if (hp.dataset.h !== h){ const top = hp.scrollTop; hp.innerHTML = h; hp.dataset.h = h; hp.scrollTop = top; } } }
  $('spOddsH').textContent = pityReady ? 'TỈ LỆ LƯỢT BẢO HIỂM' : `TỈ LỆ ${cfg.n} Ô`;
  const top = Math.max(...w.slice(0, cfg.n), 1);
  $('spOdds').innerHTML = spinRarities().slice(0, cfg.n).map((r, i) => `<li class="${spinLast?.q === i ? 'on' : ''}" style="--c:${RARITY[r]?.c || '#ccc'}"><span class="sp2-q">${RARITY[r]?.n || (r === 'ancient' ? 'Thượng Cổ' : r)}</span><span class="sp2-ob"><i style="width:${Math.max(2, w[i] / top * 100)}%"></i></span><b>${spinPct(w[i])}</b></li>`).join('');
}

/** Sự kiện quay tích lũy Thiên Cơ (server tính điểm theo giờ máy chủ): tuần Trang bị / tuần Linh Thú luân phiên. */
function spinEventLeft(ms){
  const s = Math.max(0, Math.floor((ms - Date.now()) / 1000)), d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60);
  return d ? `${d} ngày ${h} giờ` : h ? `${h} giờ ${m} phút` : `${m} phút`;
}

/** Thông báo nổi sau khi nhận quà sự kiện: gộp món trùng, món quý nhất lên đầu. */
function spinEventToast(pick){
  const el = $('craftToast'); if (!el || !pick.length) return;
  const sum = {}; pick.forEach(m => m.rewards.forEach(r => { sum[r.item] = (sum[r.item] || 0) + r.qty; }));
  const R = k => FUSE_R.indexOf(ITEM[k]?.rarity);
  const items = Object.keys(sum).filter(k => ITEM[k]).sort((a, b) => R(b) - R(a));
  if (!items.length) return;
  const list = items.map(k => `<span>${itemImg(k)}<b class="rc-${ITEM[k].rarity}">${ITEM[k].name}</b> × ${fmt(sum[k])}</span>`).join('');
  el.classList.remove('brief', 'fast');
  el.innerHTML = `<span class="ct-ic">${itemImg(items[0])}</span><span class="ct-t">Nhận quà Thiên Cơ</span>
    <span class="ct-o">${pick.length > 1 ? `${pick.length} mốc` : `Mốc ${fmt(pick[0].points)}`} · đã vào Hành trang</span><span class="ct-u sp-ev-got">${list}</span>`;
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 3200 + 300 * Math.min(items.length, 8));
}
/** Biểu tượng sự kiện ở màn hình chính: hiện khi có đợt đang chạy, chấm đỏ khi có mốc quà nhận được; bấm mở tab Vòng quay. */
function spinEventButton(){
  const b = $('btnEvent'); if (!b) return;
  const ev = S?.event, on = !!ev?.active;
  const ready = on ? (ev.milestones || []).filter(m => m.ready).length : 0;
  const next = on ? (ev.milestones || []).find(m => ev.points < m.points) : null;
  const tip = on ? [ev.name, `${fmt(ev.points)}${next ? '/' + fmt(next.points) : ''} điểm · còn ${spinEventLeft(ev.endsAt)}`,
    ready ? `Có ${ready} mốc quà chờ nhận` : next ? `Mốc kế: ${fmt(next.points)} lượt ${ev.track === 'pet' ? 'Quay Linh Thú' : 'quay trang bị'}` : 'Đã nhận mọi mốc',
    ev.online ? '' : 'Mất kết nối: lượt quay chưa được tính điểm'].filter(Boolean).join('&#10;') : '';
  const key = `${on}|${ready}|${tip}`;
  if (b.dataset.k === key) return;
  b.dataset.k = key; b.hidden = !on; b.classList.toggle('ready', ready > 0); b.classList.toggle('pet', ev?.track === 'pet');
  b.dataset.tip = tip.replace(/&#10;/g, '\n');
}
if ($('btnEvent')) $('btnEvent').onclick = () => { if (!S?.event?.active) return; MODE === 'strip' ? notifyHost('open-panel', {tab: 'spin'}) : openTab('spin'); };
setInterval(() => { if (S) spinEventButton(); }, 1000);

/** Danh sách món của một lượt quay, phẩm cao → thấp, gộp món trùng. */
function spinItemsHtml(ids, cls){
  const n = {}; ids.forEach(k => { if (ITEM[k]) n[k] = (n[k] || 0) + 1; });
  return Object.keys(n).sort((a, b) => FUSE_R.indexOf(ITEM[b].rarity) - FUSE_R.indexOf(ITEM[a].rarity)).map(k => { const it = ITEM[k];
    return `<span class="${cls} r-${it.rarity}">${itemImg(k)}${n[k] > 1 ? `<em>×${n[k]}</em>` : ''}<b class="rc-${it.rarity}">${it.name}</b></span>`; }).join('');
}

// ---- thông báo kết quả giữa màn hình ----
let spinPopTimer = 0;
function spinShowPop(r, err){
  let el = $('spinPop');
  if (!el){
    el = document.createElement('div'); el.id = 'spinPop'; el.className = 'sp-pop'; el.setAttribute('role', 'status'); el.setAttribute('aria-live', 'polite'); el.hidden = true;
    document.body.appendChild(el);
    el.addEventListener('click', () => spinHidePop());
    el.addEventListener('mouseenter', () => clearTimeout(spinPopTimer));
    el.addEventListener('mouseleave', () => { clearTimeout(spinPopTimer); spinPopTimer = setTimeout(spinHidePop, 2500); });
  }
  if (err){
    el.className = 'sp-pop bad'; el.style.setProperty('--c', '#e58472');
    el.innerHTML = `<div class="sp-pop-box"><p class="sp-pop-t">Không quay được</p><p class="sp-pop-nm">${err}</p><p class="sp-pop-hint">Bấm để đóng</p></div>`;
    el.hidden = true; void el.offsetWidth; el.hidden = false;
    clearTimeout(spinPopTimer); spinPopTimer = setTimeout(spinHidePop, 3500); return;
  }
  const ids = r.outs.flatMap(o => Array(o.qty || 1).fill(o.item));
  const best = ids.slice().sort((a, b) => spinRarities().indexOf(ITEM[b]?.rarity) - spinRarities().indexOf(ITEM[a]?.rarity))[0];
  const big = r.q >= spinCfg().big, it = ITEM[best];
  el.className = `sp-pop ${big ? 'big' : ''}`; el.style.setProperty('--c', RARITY[spinRarities()[r.q]].c);
  el.innerHTML = `<div class="sp-pop-box">
    <p class="sp-pop-t">${big ? 'Đại vận!' : spinCfg().title} · ${qWord(spinRarities()[r.q])}</p>
    ${it ? `<div class="sp-pop-best r-${it.rarity}">${itemImg(best)}</div><p class="sp-pop-nm rc-${it.rarity}">${it.name}</p>` : ''}
    ${ids.length > 1 ? `<div class="sp-pop-list">${spinItemsHtml(ids, 'sp-item')}</div>` : ''}
    <p class="sp-pop-hint">Đã vào hành trang · bấm để đóng</p></div>`;
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(spinPopTimer); spinPopTimer = setTimeout(spinHidePop, ids.length > 1 ? 6000 : 3500);
}
function spinHidePop(){ clearTimeout(spinPopTimer); const el = $('spinPop'); if (el) el.hidden = true; }
document.addEventListener('keydown', e => { if (e.key === 'Escape'){ spinHidePop(); spinHistShow(false); } });

// ---- lịch sử quay (server lưu 30 lần bấm quay gần nhất, xóa khi tu lại từ đầu) ----
const spinHistory = () => (S?.spinHistory || '').split(';').filter(Boolean).map(e => { const [t, ids] = e.split('|'); return {t: +t, ids: (ids || '').split(',').filter(Boolean)}; });
function spinHistHtml(){
  const h = spinHistory();
  if (!h.length) return '<p class="dd-sec">Lịch sử quay</p><p class="sp-wait">Chưa có lượt quay nào.</p>';
  const p2 = n => String(n).padStart(2, '0');
  const tf = t => { const d = new Date(t * 1000); return `${p2(d.getHours())}:${p2(d.getMinutes())} · ${p2(d.getDate())}/${p2(d.getMonth() + 1)}`; };
  return `<p class="dd-sec">Lịch sử quay · ${h.length} lần gần nhất</p><ol>${h.map(e => {
    const historyRarities=[...FUSE_R,"ancient"], top = Math.max(...e.ids.map(k => historyRarities.indexOf(ITEM[k]?.rarity)));
    return `<li><span class="sp-ht"><time>${tf(e.t)}</time> · quay ×${e.ids.length}${top >= 0 ? ` · cao nhất ${qWord(historyRarities[top])}` : ''}</span><div class="sp-hl">${spinItemsHtml(e.ids, 'sp-hi')}</div></li>`; }).join('')}</ol>`;
}
function spinHistShow(on){
  const b = $('spHist'); let pop = $('spHistPop');
  if (!on){ if (pop) pop.hidden = true; b?.setAttribute('aria-expanded', 'false'); return; }
  if (!b) return;
  if (!pop){ pop = document.createElement('div'); pop.id = 'spHistPop'; pop.className = 'sp-hpop'; pop.setAttribute('role', 'tooltip'); document.body.appendChild(pop); }
  const h = spinHistHtml(); if (pop.dataset.h !== h || pop.hidden){ pop.innerHTML = h; pop.dataset.h = h; pop.scrollTop = 0; } pop.hidden = false; b.setAttribute('aria-expanded', 'true');
  const r = b.getBoundingClientRect(), t = pop.getBoundingClientRect();
  pop.style.left = Math.max(4, Math.min(r.right - t.width, innerWidth - t.width - 4)) + 'px';
  pop.style.top = (r.bottom + 6 + t.height <= innerHeight - 4 ? r.bottom + 6 : Math.max(4, r.top - 6 - t.height)) + 'px';
}
// rê chuột vào nút hoặc bảng lịch sử thì giữ bảng mở (để cuộn); rời cả hai thì đóng sau 250 ms (đi qua khe giữa nút và bảng không bị tắt)
let spinHistHide = 0;
const spinHistZone = el => el?.closest?.('#spHist, #spHistPop');
document.addEventListener('mouseover', e => {
  if (!spinHistZone(e.target)) return;
  clearTimeout(spinHistHide);
  if (e.target.closest('#spHist') && ($('spHistPop')?.hidden ?? true)) spinHistShow(true);
});
document.addEventListener('mouseout', e => {
  if (!spinHistZone(e.target) || spinHistZone(e.relatedTarget)) return;
  clearTimeout(spinHistHide); spinHistHide = setTimeout(() => spinHistShow(false), 250);
});
document.addEventListener('focusin', e => { if (e.target.id === 'spHist') spinHistShow(true); });
document.addEventListener('focusout', e => { if (e.target.id === 'spHist' && !spinHistZone(e.relatedTarget) && !spinHistZone(document.querySelector('#spHistPop:hover'))) spinHistShow(false); });

async function spinGo(n){
  if (busy || spinBusy || spinTickets() < n || spinRemaining() < n) return;
  const cfg = spinCfg(), SEG = 360 / cfg.n;
  spinBusy = true; busy = true; spinLast = null; renderSpin();
  let res;
  try { res = await api('POST', `/api/players/${S.id}/spin?n=${n}${cfg.mode}`); }
  catch(e){ spinBusy = false; busy = false; localLog(e.message, 'bad'); renderSpin(); spinShowPop(null, e.message); return; }
  const outs0 = res.craft?.outputs?.length ? res.craft.outputs : [res.craft.output];
  // ô dừng: trang bị theo recipe "spin:q"; Linh Thú theo Thú Đản phẩm cao nhất trong lượt
  const q = spinMode === 'pet' ? Math.max(0, ...outs0.map(o => spinRarities().indexOf(ITEM[o.item]?.rarity)))
    : Math.max(0, +(res.craft?.recipe || 'spin:0').split(':')[1] || 0);
  const disc = $('spDisc');
  disc?.querySelectorAll('.sp-seg.on').forEach(x => x.classList.remove('on'));
  // ô q dừng dưới kim (đỉnh vòng): góc đích ≡ −q·SEG, lệch ngẫu nhiên trong ô cho tự nhiên
  const jitter = (Math.random() - .5) * SEG * .7;
  const target = ((-q * SEG + jitter - spinRot) % 360 + 360) % 360;
  const fast = spinReduced();
  spinRot += (fast ? 0 : 360 * 6) + target;
  if (disc){ disc.style.transition = fast ? 'none' : ''; disc.style.transform = `rotate(${spinRot}deg)`; }
  setTimeout(() => {
    spinBusy = false; busy = false;
    spinLast = {q, outs: outs0};
    setState(res.state); notifyHost('changed');
    $('spDisc')?.querySelector(`.sp-seg[data-q="${q}"]`)?.classList.add('on');
    if (q >= cfg.big && typeof rings !== 'undefined') rings.push({t: 0, c: RARITY[spinRarities()[q]].c, big: q >= cfg.big + 2 || (spinMode === 'pet' && q >= 4)});
    renderSpin(); live();
    spinShowPop(spinLast);
  }, fast ? 250 : 4400);
}

$('pane-spin').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled || spinBusy) return;
  if(b.dataset.spMode){if(busy)return;spinMode=b.dataset.spMode;spinModePicked=true;spinKey='';spinLast=null;renderSpin();return;}
  if (b.id === 'spHist'){ const p = $('spHistPop'); spinHistShow(!p || p.hidden); return; }
  if (b.dataset.spGo){ spinGo(+b.dataset.spGo); return; }
  if (b.dataset.evClaim !== undefined){
    const i = +b.dataset.evClaim, ms = S.event?.milestones || [];
    const pick = ms.filter((m, j) => m.ready && (i < 0 || j === i));        // quà sắp nhận (server kiểm lại khi nhận)
    if (await act(`event/claim?i=${i}`)) spinEventToast(pick);
    renderSpin(); return;
  }
  if (b.dataset.spBuy !== undefined){ await act(`spin/buy?n=${b.dataset.spBuy}${spinCfg().mode}`); renderSpin(); }
});

/** Code quà tặng (gọi từ menu Cài đặt của cửa sổ desktop, hoặc nút cài đặt khi chơi trên trình duyệt). */
window.redeemCode = async code => {
  if (!S) return;
  const el = $('craftToast');
  const toast = (ok, txt) => {
    if (!el) return;
    el.classList.remove('brief', 'fast');
    el.innerHTML = `<span class="ct-ic">${ok ? itemImg(SPIN_TICKET) : ''}</span><span class="ct-t ${ok ? '' : 'bad'}">${ok ? 'Nhận quà' : 'Không nhận được'}</span><span class="ct-o">${txt}</span>`;
    el.hidden = true; void el.offsetWidth; el.hidden = false;
    clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 2600);
  };
  try {
    const res = await api('POST', `/api/players/${S.id}/redeem?code=${encodeURIComponent(code)}`);
    setState(res.state); notifyHost('changed');
    toast(true, res.message || 'Đã nhận quà.');
  } catch(e){ localLog(e.message, 'bad'); toast(false, e.message); }
};
