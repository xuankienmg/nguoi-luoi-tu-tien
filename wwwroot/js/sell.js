/* Vấn Đạo · js/sell.js: Luyện chế · Bán — chọn món trong hành trang (cột trái) bỏ vào quầy bán (cột phải), bán một lần.
   Trang bị: mỗi món một ô quầy. Đan dược: cả chồng vào một ô, chỉnh số món bằng −/+ ở dưới quầy. */
let sellSel = [];          // các ô quầy: {k, n} — trang bị n = 1 (mỗi món một phần tử), đan dược mỗi loại một phần tử
let sellConfirm = 0;
let quickRarity = '';
let quickArm = null;       // bán nhanh phẩm Thánh trở lên: {r, t} — bấm lần 2 trong 3 giây
const QUICK_HI = FUSE_R.indexOf('mythic');   // từ phẩm này trở lên phải bấm 2 lần       // bấm Bán lần 1 khi có đồ Lam / đan Trúc Cơ trở lên: chờ bấm lần 2 trong 3 giây
const SELL_N = 9;          // số ô của quầy bán
const SELL_GO = '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8"/><path d="M12 7v10M9.5 9.5h3.8a1.7 1.7 0 0 1 0 3.4h-2.6a1.7 1.7 0 0 0 0 3.4H14.5"/></svg>';
const sellInventory = () => ({...S.inventory, ...(S.gemState?.vault || {})});
const sellStack = k => !ITEM[k]?.slot;                  // món xếp chồng (đan dược…): bán theo số món
const sellHi = k => (ITEM[k].tier || 0) >= 2 || fuseQOf(k) >= 2;
function sellClean(){                                   // bỏ / bớt món trong quầy mà túi không còn đủ (vừa mặc, ghép, dùng, bán…)
  const left = sellInventory();
  sellSel = sellSel.filter(e => {
    if (!ITEM[e.k]?.sell) return false;
    if (sellStack(e.k)){ e.n = Math.min(e.n, left[e.k] || 0); left[e.k] = (left[e.k] || 0) - e.n; return e.n > 0; }
    return left[e.k]-- > 0;
  });
}
/** Trang bị bán được còn chưa bỏ vào quầy, theo thứ tự cột trái (Tự động điền chỉ lấy trang bị, không đụng đan). */
function sellFree(){
  const left = sellInventory(); sellSel.forEach(e => left[e.k] -= e.n);
  return lcBagList(sellInventory()).filter(c => ITEM[c.k].sell && ITEM[c.k].slot && left[c.k]-- > 0).map(c => c.k);
}
function sellQtyRows(){
  const rows = sellSel.filter(e => sellStack(e.k)).map(e => {
    const it = ITEM[e.k], own = sellInventory()[e.k] || 0;
    return `<li><span class="nm rc-${it.rarity}">${it.name}</span>
      <button data-pq="${e.k}" data-d="-1" ${e.n > 1 ? '' : 'disabled'} data-tip="Bớt 1 món (Shift: 10)">−</button>
      <input data-pqi="${e.k}" type="number" min="1" max="${own}" value="${e.n}" aria-label="Số ${it.name} bán">
      <button data-pq="${e.k}" data-d="1" ${e.n < own ? '' : 'disabled'} data-tip="Thêm 1 món (Shift: 10)">+</button>
      <button data-pq="${e.k}" data-d="all" class="all" ${e.n < own ? '' : 'disabled'} data-tip="Bán cả ${fmt(own)} món">Tất cả</button></li>`;
  }).join('');
  return rows ? `<ul class="sl-qty" aria-label="Số lượng vật phẩm bán">${rows}</ul>` : '';
}
/** Trang bị theo phẩm; các vật phẩm khác theo loại. Danh sách nhóm ổn định khi túi thay đổi. */
function quickPools(){
  const out = {};
  for (const r of FUSE_R) out[r] = {pool: {}, n: 0, gain: 0};
  for (const type of [...new Set(Object.values(ITEM).filter(it => !it.slot && it.type !== 'ticket').map(it => it.type || 'other'))].sort())
    out['type:' + type] = {pool: {}, n: 0, gain: 0};
  for (const [k, c] of Object.entries(sellInventory())){
    const it = ITEM[k]; if (!(c > 0) || !it?.sell) continue;
    const key = it.slot ? it.rarity : 'type:' + (it.type || 'other');
    const o = out[key]; if (!o) continue;
    o.pool[k] = c; o.n += c; o.gain += it.sell * c;
  }
  return out;
}
const QUICK_TYPE_NAMES = {herb:'Linh thảo', pill:'Đan dược', elixir:'Đan đặc biệt', gem:'Linh Thạch khảm', chest:'Rương', eventchest:'Rương sự kiện', egg:'Thú đản', petgear:'Trang bị Linh thú', petmaterial:'Nguyên liệu Linh thú', petfood:'Thức ăn Linh thú', material:'Nguyên liệu', boss:'Nguyên liệu Boss', manual:'Công pháp', skin:'Trang phục', ticket:'Vé quay', key:'Chìa khóa', stone:'Đạo cụ', other:'Vật phẩm khác'};
function quickRow(){
  const pools = quickPools(), selected = pools[quickRarity];
  const armed = quickArm && quickArm.r === quickRarity && Date.now() - quickArm.t < 3000;
  const options = Object.keys(pools).map(r => {
    const o = pools[r], nm = r.startsWith('type:') ? (QUICK_TYPE_NAMES[r.slice(5)] || r.slice(5)) : 'Trang bị ' + (RARITY[r]?.n ?? r);
    return `<option value="${r}" ${r === quickRarity ? 'selected' : ''} ${o.n ? '' : 'disabled'}>${nm} · ${fmt(o.n)} món · +${fmt(o.gain)} linh thạch</option>`;
  }).join('');
  return `<div class="sl-quick" role="group" aria-label="Bán nhanh theo phẩm hoặc nhóm vật phẩm"><label for="slQuickRarity">Bán nhanh</label><select id="slQuickRarity" ${busy ? 'disabled' : ''}><option value="">Chọn phẩm / nhóm</option>${options}</select><button class="sl-quick-go ${armed ? 'armed' : ''}" data-sq="${quickRarity}" ${selected?.n && !busy ? '' : 'disabled'} title="Bán toàn bộ vật phẩm bán được thuộc nhóm đã chọn trong túi/kho. Nhóm vật phẩm và trang bị từ phẩm Thánh cần bấm hai lần để xác nhận.">${armed ? 'Xác nhận' : 'Bán'}</button></div>`;
}

/** Vẽ lại quầy mà không gỡ ô chọn "Bán nhanh" khỏi trang (nhặt được đồ, đồng bộ… vẽ lại liên tục):
    gỡ <select> ra là danh sách đang mở bị đóng và mất lựa chọn. Thay mọi phần tử xung quanh bằng bản mới,
    còn ô chọn chỉ cập nhật chữ / trạng thái từng lựa chọn và nút Bán. */
function sellPaint(pane, html){
  const old = pane.querySelector('.sl-quick'), tmp = document.createElement('div');
  tmp.innerHTML = html;
  const fresh = tmp.querySelector('.sl-quick');
  if (!old || !fresh){ pane.innerHTML = html; return; }
  const os = old.querySelector('select'), ns = fresh.querySelector('select');
  if (os.options.length !== ns.options.length){ pane.innerHTML = html; return; }
  [...ns.options].forEach((o, i) => { const p = os.options[i]; if (p.text !== o.text) p.text = o.text; p.disabled = o.disabled; });
  if (os.value !== quickRarity) os.value = quickRarity;
  if (os.disabled !== ns.disabled) os.disabled = ns.disabled;
  old.replaceChild(fresh.querySelector('.sl-quick-go'), old.querySelector('.sl-quick-go'));
  // đi ngược lên tới pane: ở mỗi tầng thay anh em của nhánh chứa ô chọn, giữ nguyên nhánh đó
  for (let o = old, f = fresh; o !== pane; o = o.parentNode, f = f.parentNode){
    const op = o.parentNode, fp = f.parentNode, kids = [...fp.childNodes], i = kids.indexOf(f);
    [...op.childNodes].forEach(c => { if (c !== o) c.remove(); });
    kids.slice(0, i).forEach(c => op.insertBefore(c, o));
    kids.slice(i + 1).forEach(c => op.appendChild(c));
    if (op !== pane){
      [...op.attributes].forEach(a => { if (!fp.hasAttribute(a.name)) op.removeAttribute(a.name); });
      [...fp.attributes].forEach(a => { if (op.getAttribute(a.name) !== a.value) op.setAttribute(a.name, a.value); });
    }
  }
}

function renderSell(){
  const pane = $('pane-sell'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0;
  sellClean();
  const bag = lcBagList(sellInventory()), used = {}, picked = {}, full = sellSel.length >= SELL_N;
  sellSel.forEach(e => picked[e.k] = (picked[e.k] || 0) + 1);
  const cells = bag.map(({k, n}) => {
    const it = ITEM[k];
    if (!it.sell) return `<div class="iv-cell ${it.slot ? 'eq ' : ''}r-${it.rarity || 'common'} off na" aria-label="${it.name}" data-tip="${itemTip(k, 'Không bán được')}">${itemImg(k)}${lcCellTail(k, n)}</div>`;
    if (sellStack(k)){
      const on = !!picked[k], off = !on && full;
      return `<button class="iv-cell r-${it.rarity} ${on ? 'msel' : ''} ${off ? 'off' : ''}" data-sp="${k}" aria-label="${it.name}" data-tip="${itemTip(k, on ? 'Nhấp để lấy khỏi quầy' : off ? `Quầy đã đủ ${SELL_N} ô: bán trước hoặc lấy bớt ra` : `Nhấp để bỏ cả ${fmt(n)} món vào quầy · ${fmt(it.sell)} linh thạch/món`)}">${itemImg(k)}${lcCellTail(k, n)}</button>`;
    }
    const j = used[k] = (used[k] || 0) + 1, on = j <= (picked[k] || 0), off = !on && full;
    return `<button class="iv-cell eq r-${it.rarity} ${on ? 'msel' : ''} ${off ? 'off' : ''}" data-sl="${k}" aria-label="${it.name}" data-tip="${itemTip(k, on ? 'Nhấp để lấy khỏi quầy' : off ? `Quầy đã đủ ${SELL_N} ô: bán trước hoặc lấy bớt ra` : `Nhấp để bỏ vào quầy · ${fmt(it.sell)} linh thạch`)}">${itemImg(k)}<span class="lv">${itemLv(it)}</span></button>`;
  }).join('');
  const gain = sellSel.reduce((a, e) => a + ITEM[e.k].sell * e.n, 0), count = sellSel.reduce((a, e) => a + e.n, 0), hi = sellSel.some(e => sellHi(e.k));
  const armed = sellConfirm && Date.now() - sellConfirm < 3000;
  const sellable = bag.filter(c => ITEM[c.k].sell).length;
  const slots = Array.from({length: SELL_N}, (_, i) => { const e = sellSel[i]; if (!e) return '<div class="iv-cell empty"></div>';
    const it = ITEM[e.k], stack = sellStack(e.k);
    return `<button class="iv-cell ${stack ? '' : 'eq '}r-${it.rarity}" data-ss="${i}" aria-label="${it.name}${stack ? ' ×' + e.n : ''}" data-tip="${itemTip(e.k, stack ? `${fmt(e.n)} món · +${fmt(it.sell * e.n)} linh thạch&#10;Nhấp để lấy ra` : 'Nhấp để lấy ra')}">${itemImg(e.k)}${stack ? `<span class="qty">${short(e.n)}</span>` : `<span class="lv">${itemLv(it)}</span>`}</button>`; }).join('');
  sellPaint(pane, `<div class="dd fz sl">
      <header class="dd-top">${lcNav('sell')}<div class="dd-cult"><span>Bỏ tối đa <b>${SELL_N} ô</b> vào quầy rồi bán một lần · vật phẩm bán theo chồng · đồ đang mặc không tính</span></div></header>
      <section class="dd-main fz-bag" aria-label="Hành trang"><p class="dd-sec">Hành trang · trang bị phẩm thấp → cao, rồi tất cả vật phẩm trong túi</p>
        <div class="fz-bar"><span>Bán được ${sellable} ô · đã chọn ${sellSel.length}/${SELL_N}</span>${quickRow()}</div>
        <div class="iv-grid fz-grid">${cells || '<p class="fz-empty">Hành trang trống.</p>'}</div>
      </section>
      <aside class="fz-forge sl-desk" aria-label="Quầy bán"><p class="dd-sec">Quầy bán</p>
        <div class="fz-slots">${slots}</div>
        ${sellQtyRows()}
        <div class="fz-arrow">↓</div>
        <div class="sl-sum"><span>${fmt(count)} món</span><b>+${fmt(gain)}</b>${itemImg('linh_thach')}</div>
        ${hi ? `<p class="sl-warn">Có vật phẩm từ ${qWord('rare')} / đan từ Trúc Cơ trở lên · bấm Bán 2 lần để xác nhận</p>` : ''}
        <div class="fz-act">
          <div class="fz-auto"><button class="fz-af" data-sz="auto" ${!full && sellFree().length ? '' : 'disabled'} data-tip="Tự động điền&#10;Bù đủ ${SELL_N} ô bằng trang bị phẩm thấp nhất, bán rẻ nhất trước (không lấy đan dược)">${FUSE_GRID}<span>Tự động điền</span></button></div>
          <button class="dd-synth-btn fz-go ${armed ? 'sl-arm' : ''}" data-sz="go" ${sellSel.length && !busy ? '' : 'disabled'}>${SELL_GO}${armed ? 'Bấm lần nữa' : 'Bán'}</button>
        </div>
      </aside>
    </div>`);
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
}
function sellSetQty(k, n){
  const e = sellSel.find(x => x.k === k); if (!e) return;
  e.n = Math.max(1, Math.min(sellInventory()[k] || 0, Math.floor(n) || 1)); sellConfirm = 0; renderSell();
}
$('pane-sell').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled) return;
  hideTip();
  if (b.dataset.sl){
    const k = b.dataset.sl, all = [...$('pane-sell').querySelectorAll(`[data-sl="${CSS.escape(k)}"]`)], j = all.indexOf(b) + 1;
    if (j <= sellSel.filter(x => x.k === k).length) sellSel.splice(sellSel.map(x => x.k).lastIndexOf(k), 1);
    else if (sellSel.length < SELL_N) sellSel.push({k, n: 1});
    sellConfirm = 0; renderSell(); return;
  }
  if (b.dataset.sp){                                       // đan dược: bỏ cả chồng vào / lấy cả chồng ra
    const k = b.dataset.sp, i = sellSel.findIndex(x => x.k === k);
    if (i >= 0) sellSel.splice(i, 1); else if (sellSel.length < SELL_N) sellSel.push({k, n: sellInventory()[k] || 0});
    sellConfirm = 0; renderSell(); return;
  }
  if (b.dataset.pq){
    const k = b.dataset.pq, d = b.dataset.d, cur = sellSel.find(x => x.k === k)?.n || 0;
    sellSetQty(k, d === 'all' ? Infinity : cur + (+d) * (e.shiftKey ? 10 : 1)); return;
  }
  if (b.dataset.sq){                                       // bán nhanh cả phẩm / nhóm; nhóm vật phẩm cần xác nhận
    if (busy) return;
    const r = b.dataset.sq, o = quickPools()[r]; if (!o?.n) return;
    if ((r.startsWith('type:') || FUSE_R.indexOf(r) >= QUICK_HI) && !(quickArm && quickArm.r === r && Date.now() - quickArm.t < 3000)){
      quickArm = {r, t: Date.now()}; renderSell();
      setTimeout(() => { if (quickArm && quickArm.r === r && Date.now() - quickArm.t >= 3000){ quickArm = null; renderSell(); } }, 3100);
      return;
    }
    quickArm = null; sellConfirm = 0;
    await act('sell-bulk?max=9&pick=true&items=' + encodeURIComponent(Object.entries(o.pool).map(([k, c]) => k + ':' + c).join(',')));
    renderSell(); return;
  }
  if (b.dataset.ss){ sellSel.splice(+b.dataset.ss, 1); sellConfirm = 0; renderSell(); return; }
  const a = b.dataset.sz;
  if (a === 'auto'){                                       // phẩm thấp nhất trước, trong cùng phẩm món bán rẻ trước
    const pick = sellFree().sort((x, y) => fuseQOf(x) - fuseQOf(y) || ITEM[x].sell - ITEM[y].sell);
    sellSel = sellSel.concat(pick.slice(0, SELL_N - sellSel.length).map(k => ({k, n: 1}))); sellConfirm = 0; renderSell(); return;
  }
  if (a === 'go' && sellSel.length){
    if (sellSel.some(x => sellHi(x.k)) && !(sellConfirm && Date.now() - sellConfirm < 3000)){
      sellConfirm = Date.now(); renderSell();
      setTimeout(() => { if (sellConfirm && Date.now() - sellConfirm >= 3000){ sellConfirm = 0; renderSell(); } }, 3100);
      return;
    }
    if (busy) return;                                      // đang có thao tác khác: giữ nguyên quầy, không bán hụt
    const pool = {}; sellSel.forEach(x => pool[x.k] = (pool[x.k] || 0) + x.n);
    sellConfirm = 0;
    if (await act('sell-bulk?max=9&pick=true&items=' + encodeURIComponent(Object.entries(pool).map(([k, c]) => k + ':' + c).join(',')))) sellSel = [];
    renderSell();
  }
});
$('pane-sell').addEventListener('change', e => {
  if (e.target.id === 'slQuickRarity'){ quickRarity = e.target.value; quickArm = null; renderSell(); $('slQuickRarity')?.focus(); return; }
  const k = e.target.dataset?.pqi; if (k) sellSetQty(k, +e.target.value); });
$('pane-sell').addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.dataset?.pqi) e.target.blur(); });
