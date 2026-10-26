/* Vấn Đạo · js/fuse.js: Luyện khí: ghép 9 trang bị, menu Luyện chế. Tách từ index.html (tools/split_index.py). */
// ================= Luyện khí: ghép 9 trang bị cùng phẩm → 1 trang bị phẩm cao hơn =================
const FUSE_R = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic', 'divine', 'immortal', 'dao', 'supreme'];
const FUSE_N = ['Phàm', 'Linh', 'Huyền', 'Địa', 'Thiên', 'Thánh', 'Thần', 'Tiên', 'Đạo', 'Huyền Tiên'];
let fuseSel = [];
/** Tự chọn theo phẩm: 'low' = phẩm thấp nhất đủ 9 món, hoặc số phẩm 0..8. */
let fuseAutoQ = 'low';   // tự động điền: lấy từ phẩm thấp nhất đủ 9 món
const FUSE_GRID = '<svg viewBox="0 0 16 16"><rect x="1" y="1" width="4" height="4"/><rect x="6" y="1" width="4" height="4"/><rect x="11" y="1" width="4" height="4"/><rect x="1" y="6" width="4" height="4"/><rect x="6" y="6" width="4" height="4"/><rect x="11" y="6" width="4" height="4"/><rect x="1" y="11" width="4" height="4"/><rect x="6" y="11" width="4" height="4"/><rect x="11" y="11" width="4" height="4"/></svg>';
const FUSE_GO = '<svg viewBox="0 0 24 24"><path d="M4 12h12M12 6l6 6-6 6"/><path d="M20 5v14"/></svg>';
/** Menu Luyện chế: tab con đang mở ('fuse' Luyện khí · 'craft' Đan dược · 'sell' Bán · 'spin' Vòng quay). Bấm tab Luyện chế luôn mở Luyện khí. */
const LC_SUBS = ['fuse', 'craft', 'sell', 'spin'];
let lcSub = 'fuse';   // luôn mở Luyện khí trước; chỉ nhớ tab con trong phiên đang chơi
const lcNav = on => `<nav class="lc-tabs" role="tablist" aria-label="Luyện chế">${[['fuse', 'Luyện khí'], ['craft', 'Đan dược'], ['sell', 'Bán'], ['spin', 'Vòng quay']].map(([id, nm]) =>
  `<button class="${id === on ? 'on' : ''}" data-lc="${id}" role="tab" aria-selected="${id === on}">${nm}</button>`).join('')}</nav>`;
document.addEventListener('click', e => { const b = e.target.closest('[data-lc]'); if (b) openTab(b.dataset.lc, true); });
const fuseQOf = k => FUSE_R.indexOf(ITEM[k]?.rarity);
/** Cả hành trang cho cột trái của Luyện khí / Bán: trang bị mỗi món một ô (phẩm thấp → cao, cấp, tên), rồi vật phẩm khác gộp thành một ô kèm số lượng. */
function lcBagList(){
  const own = Object.keys(S.inventory).filter(k => ITEM[k] && S.inventory[k] > 0);
  const gear = own.filter(k => ITEM[k].slot).sort((a, b) => fuseQOf(a) - fuseQOf(b) || (ITEM[a].itemLevel || 0) - (ITEM[b].itemLevel || 0) || ITEM[a].name.localeCompare(ITEM[b].name, 'vi'));
  const rest = own.filter(k => !ITEM[k].slot).sort((a, b) => (ITEM[a].type || '').localeCompare(ITEM[b].type || '') || ITEM[a].name.localeCompare(ITEM[b].name, 'vi'));
  return gear.flatMap(k => Array.from({length: S.inventory[k]}, () => ({k, n: 1}))).concat(rest.map(k => ({k, n: S.inventory[k]})));
}
const lcCellTail = (k, n) => n > 1 ? `<span class="qty">${short(n)}</span>` : ITEM[k].slot ? `<span class="lv">${itemLv(ITEM[k])}</span>` : '';
/** Trang bị dưới Huyền Tiên, mỗi món một ô; xếp phẩm thấp → cao, rồi cấp, tên. */
function fuseUnits(){
  const out = [];
  Object.keys(S.inventory).filter(k => ITEM[k]?.slot && ITEM[k].base && S.inventory[k] > 0 && fuseQOf(k) >= 0 && fuseQOf(k) < FUSE_R.length - 1)
    .sort((a, b) => fuseQOf(a) - fuseQOf(b) || (ITEM[a].itemLevel || 0) - (ITEM[b].itemLevel || 0) || ITEM[a].name.localeCompare(ITEM[b].name))
    .forEach(k => { for (let j = 0; j < S.inventory[k]; j++) out.push(k); });
  return out;
}
function fuseClean(){                                       // bỏ món đã chọn mà túi không còn (vừa bán, mặc, ghép…)
  const left = {...S.inventory};
  fuseSel = fuseSel.filter(k => left[k]-- > 0);
  if (fuseSel.some(k => fuseQOf(k) !== fuseQOf(fuseSel[0]))) fuseSel = [];
}
/** Tự chọn: đang có món trong lò thì bù cùng phẩm; lò trống thì lấy phẩm thấp nhất đủ 9 món. Trong một phẩm ưu tiên món bán rẻ. */
function fuseAuto(pref = fuseAutoQ){
  if (pref !== 'low' && fuseSel.length && fuseQOf(fuseSel[0]) !== pref) fuseSel = [];   // chọn phẩm khác với lò đang có → làm lại
  const left = {...S.inventory}; fuseSel.forEach(k => left[k]--);
  const pool = fuseUnits().filter(k => left[k]-- > 0);
  let q = fuseSel.length ? fuseQOf(fuseSel[0]) : pref === 'low' ? -1 : pref;
  if (q < 0) for (let i = 0; i < FUSE_R.length - 1 && q < 0; i++) if (pool.filter(k => fuseQOf(k) === i).length >= 9) q = i;
  if (q < 0) return false;
  const pick = pool.filter(k => fuseQOf(k) === q).sort((a, c) => (ITEM[a].sell || 0) - (ITEM[c].sell || 0));
  fuseSel = fuseSel.concat(pick.slice(0, 9 - fuseSel.length));
  return true;
}
function fuseCanAuto(pref = fuseAutoQ){
  const units = fuseUnits();
  if (pref !== 'low' && !(fuseSel.length && fuseQOf(fuseSel[0]) === pref)) return units.filter(k => fuseQOf(k) === pref).length >= 9;
  if (fuseSel.length) return fuseSel.length < 9 && units.filter(k => fuseQOf(k) === fuseQOf(fuseSel[0])).length >= 9;
  return FUSE_R.slice(0, -1).some((_, q) => units.filter(k => fuseQOf(k) === q).length >= 9);
}
function renderFuse(){
  const pane = $('pane-fuse'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0;
  fuseClean();
  const units = fuseUnits(), N = 9, q = fuseSel.length ? fuseQOf(fuseSel[0]) : -1;
  const chance = q >= 0 ? (DATA.fuseChance?.[q] ?? 0) : null;
  const used = {}, bag = lcBagList();
  // cột trái: cả hành trang; món không ghép được (đan, nguyên liệu, Thiên cấp…) hiện mờ
  const cells = bag.map(({k, n}) => {
    const it = ITEM[k], can = it.slot && it.base && fuseQOf(k) >= 0 && fuseQOf(k) < FUSE_R.length - 1;
    if (!can) return `<div class="iv-cell ${it.slot ? 'eq ' : ''}r-${it.rarity || 'common'} off na" aria-label="${it.name}" data-tip="${itemTip(k, it.slot ? `${FUSE_N.at(-1)}: đã là phẩm cao nhất, không ghép được` : 'Không phải trang bị, không ghép được')}">${itemImg(k)}${lcCellTail(k, n)}</div>`;
    const j = used[k] = (used[k] || 0) + 1, on = j <= fuseSel.filter(x => x === k).length, off = q >= 0 && fuseQOf(k) !== q;
    return `<button class="iv-cell eq r-${it.rarity} ${on ? 'msel' : ''} ${off ? 'off' : ''}" data-fu="${k}" aria-label="${it.name}" data-tip="${itemTip(k, off ? `Lò đang ghép ${FUSE_N[q]}, chỉ bỏ thêm được món cùng phẩm` : on ? 'Nhấp để bỏ khỏi lò' : 'Nhấp để bỏ vào lò luyện khí')}">${itemImg(k)}<span class="lv">${itemLv(it)}</span></button>`;
  }).join('');
  const slots = Array.from({length: N}, (_, i) => {
    const k = fuseSel[i];
    return k ? `<button class="iv-cell eq r-${ITEM[k].rarity}" data-fs="${i}" aria-label="${ITEM[k].name}" data-tip="${itemTip(k, 'Nhấp để lấy ra')}">${itemImg(k)}<span class="lv">${itemLv(ITEM[k])}</span></button>`
             : '<div class="iv-cell empty"></div>';
  }).join('');
  const kinds = [...new Set(fuseSel.map(k => ITEM[k].name))];
  const kindTxt = kinds.length ? (kinds.length > 3 ? `${kinds.length} loại đã chọn` : kinds.join(', ')) : 'các món đã chọn';
  const rates = FUSE_N.slice(0, -1).map((_, i) => `${FUSE_N[i]} → ${FUSE_N[i + 1]}: ${Math.round((DATA.fuseChance?.[i] ?? 0) * 100)}%`).join('&#10;');
  const gearN = Object.keys(S.inventory).filter(k => ITEM[k]?.slot && S.inventory[k] > 0).reduce((a, k) => a + S.inventory[k], 0);
  pane.innerHTML = `<div class="dd fz">
      <header class="dd-top">${lcNav('fuse')}<div class="dd-cult"><span>Ghép <b>${N} trang bị cùng phẩm</b> thành 1 trang bị phẩm cao hơn · thất bại vẫn nhận lại 1 món cùng phẩm</span></div>
        <div class="dd-stone" data-tip="Trang bị trong hành trang&#10;Không tính đồ đang mặc"><span>Trang bị</span><b>× ${fmt(gearN)}</b></div></header>
      <section class="dd-main fz-bag" aria-label="Hành trang"><p class="dd-sec">Hành trang · trang bị phẩm thấp → cao, rồi vật phẩm khác</p>
        <div class="fz-bar"><span>Ghép được ${units.length} món · đã chọn ${fuseSel.length}/${N}${q >= 0 ? ` (${qWord(FUSE_R[q])})` : ''}</span>
          <button data-fz="clear" ${fuseSel.length ? '' : 'disabled'}>Bỏ chọn</button></div>
        <div class="iv-grid fz-grid">${cells || '<p class="fz-empty">Hành trang trống.</p>'}</div>
      </section>
      <aside class="fz-forge" aria-label="Lò luyện khí"><p class="dd-sec">Lò luyện khí</p>
        <div class="fz-slots">${slots}</div>
        <div class="fz-arrow">↓</div>
        <div class="fz-out" data-tip="Tỉ lệ thành công&#10;${rates}"><div class="iv-cell eq ${q >= 0 ? 'r-' + FUSE_R[q + 1] : ''}"><span class="q">?</span></div>
          <div class="fz-rate">${q >= 0 ? `<b>${Math.round(chance * 100)}%</b> thành công` : 'Chọn trang bị để xem tỉ lệ'}</div></div>
        <ul class="fz-rules">${q >= 0 ? `<li class="ok">Thành công: 1 món ${qWord(FUSE_R[q + 1])} ngẫu nhiên trong ${kindTxt}</li>
          <li class="bad">${chance === 1 ? 'Chắc chắn thành công, không có nhánh thất bại.' : `Thất bại: trả lại 1 món ${qWord(FUSE_R[q])}, mất ${N - 1} món.`}</li>
          <li>Loại và cảnh giới trang bị đầu ra được chọn từ ${N} món đã bỏ vào.</li>` : `<li>Bỏ ${N} trang bị cùng phẩm vào lò.</li>`}</ul>
        <div class="fz-act">
          <div class="fz-auto">
            <button class="fz-af" data-fz="auto" ${fuseCanAuto() ? '' : 'disabled'} data-tip="Tự động điền&#10;Lấy từ phẩm thấp nhất có đủ ${N} món (lò đang có món thì bù cùng phẩm), ưu tiên món bán rẻ">${FUSE_GRID}<span>Tự động điền</span></button>
          </div>
          <button class="dd-synth-btn fz-go" data-fz="go" ${fuseSel.length === N && !busy ? '' : 'disabled'}>${FUSE_GO}Luyện khí</button>
        </div>
      </aside>
    </div>`;
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
}
// Use the inventory's cell sizing rule in both crafting bags.
function fitLcBag(pane){
  const grid = pane.querySelector('.fz-grid');
  if (!grid?.clientWidth) return;
  const {cell} = bagCellSize(grid.clientWidth - 4);
  grid.style.setProperty('--cell', cell + 'px');
}
if (window.ResizeObserver){
  const lcBagObserver = new ResizeObserver(entries => entries.forEach(e => fitLcBag(e.target)));
  ['pane-fuse', 'pane-sell'].forEach(id => lcBagObserver.observe($(id)));
}
$('pane-fuse').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled) return;
  if (b.dataset.fu){
    const k = b.dataset.fu, all = [...$('pane-fuse').querySelectorAll(`[data-fu="${CSS.escape(k)}"]`)], j = all.indexOf(b) + 1;
    const n = fuseSel.filter(x => x === k).length;
    if (j <= n) fuseSel.splice(fuseSel.lastIndexOf(k), 1);
    else if (fuseSel.length < 9 && (!fuseSel.length || fuseQOf(fuseSel[0]) === fuseQOf(k))) fuseSel.push(k);
    renderFuse(); return;
  }
  if (b.dataset.fs){ fuseSel.splice(+b.dataset.fs, 1); renderFuse(); return; }
  if (b.dataset.fz === 'clear'){ fuseSel = []; renderFuse(); return; }
  if (b.dataset.fz === 'auto'){ fuseAuto(); renderFuse(); return; }
  if (b.dataset.fz === 'go' && fuseSel.length === 9){
    const items = fuseSel.join(','); fuseSel = [];
    await act(`fuse?items=${encodeURIComponent(items)}`);
    renderFuse();
  }
});
