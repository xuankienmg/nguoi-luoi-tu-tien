/* js/fuse.js: Luyện khí: ghép 9 trang bị, menu Luyện chế. Tách từ index.html (tools/split_index.py). */
// ================= Luyện khí: ghép 9 trang bị cùng phẩm → 1 trang bị phẩm cao hơn =================
const FUSE_R = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic', 'divine', 'immortal', 'dao', 'supreme', 'kimtien'];
const FUSE_N = ['Phàm', 'Linh', 'Huyền', 'Địa', 'Thiên', 'Thánh', 'Thần', 'Tiên', 'Đạo', 'Huyền Tiên', 'Kim Tiên'];
let fuseSel = [];
/** Tự chọn theo phẩm: 'low' = phẩm thấp nhất đủ 9 món, hoặc số phẩm 0..8. */
let fuseAutoQ = 'low';   // tự động điền: lấy từ phẩm thấp nhất đủ 9 món
const FUSE_GRID = '<svg viewBox="0 0 16 16"><rect x="1" y="1" width="4" height="4"/><rect x="6" y="1" width="4" height="4"/><rect x="11" y="1" width="4" height="4"/><rect x="1" y="6" width="4" height="4"/><rect x="6" y="6" width="4" height="4"/><rect x="11" y="6" width="4" height="4"/><rect x="1" y="11" width="4" height="4"/><rect x="6" y="11" width="4" height="4"/><rect x="11" y="11" width="4" height="4"/></svg>';
const FUSE_GO = '<svg viewBox="0 0 24 24"><path d="M4 12h12M12 6l6 6-6 6"/><path d="M20 5v14"/></svg>';
/** Menu Luyện chế: tab con đang mở ('fuse' Luyện khí · 'craft' Đan dược · 'vault' Khảm Tinh Thạch · 'petfuse' Linh thú · 'sell' Bán). Bấm tab Luyện chế luôn mở Luyện khí. Vòng quay đã chuyển sang bảng Sự kiện. */
const LC_SUBS = ['fuse', 'craft', 'vault', 'petfuse', 'sell'];
let lcSub = 'fuse';   // luôn mở Luyện khí trước; chỉ nhớ tab con trong phiên đang chơi
const lcNav = () => '';   // tab con Luyện chế nằm ở hàng .subtabs[data-group="craft"] trong index.html (cùng kiểu tab nhóm Tu luyện)
document.addEventListener('click', e => { const b = e.target.closest('[data-lc]'); if (b) openTab(b.dataset.lc, true); });
const fuseQOf = k => FUSE_R.indexOf(ITEM[k]?.rarity);
/** Cả hành trang cho cột trái của Luyện khí / Bán: trang bị mỗi món một ô (phẩm thấp → cao, cấp, tên), rồi vật phẩm khác gộp thành một ô kèm số lượng. */
function lcBagList(inventory = S.inventory){
  const own = Object.keys(inventory).filter(k => ITEM[k] && inventory[k] > 0);
  const gear = own.filter(k => ITEM[k].slot).sort((a, b) => fuseQOf(a) - fuseQOf(b) || (ITEM[a].itemLevel || 0) - (ITEM[b].itemLevel || 0) || ITEM[a].name.localeCompare(ITEM[b].name, 'vi'));
  const rest = own.filter(k => !ITEM[k].slot).sort((a, b) => (ITEM[a].type || '').localeCompare(ITEM[b].type || '') || ITEM[a].name.localeCompare(ITEM[b].name, 'vi'));
  return gear.flatMap(k => Array.from({length: inventory[k]}, () => ({k, n: 1}))).concat(rest.map(k => ({k, n: inventory[k]})));
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
const fuseTypePicker = type => `<div class="fk-type" role="group" aria-label="Loại hợp thành">${[['gear','Trang bị'],['gem','Tinh Thạch'],['pet','Linh Thú']].map(([id,label]) => `<button type="button" data-fuse-type value="${id}" aria-pressed="${type === id}" class="${type === id ? 'on' : ''}">${label}</button>`).join('')}</div>`;
let fuseType = 'gear';
let fuseGemSel = [];
const GEM_FUSE_GRADES = FUSE_R.slice(0, 10);   // tinh thạch chỉ có 10 cấp
const GEM_FUSE_CHANCES = [.97, .93, .88, .83, .74, .65, .55, .45, .35, 0];
const gemFuseLevel = id => ITEM[id]?.type === 'gem' ? GEM_FUSE_GRADES.indexOf(ITEM[id].rarity) : -1;
const gemFuseVault = () => S.gemState?.vault || {};
function gemFuseClean(){
  const left = {...gemFuseVault()};
  fuseGemSel = fuseGemSel.filter(id => gemFuseLevel(id) >= 0 && left[id]-- > 0).slice(0, 9);
}
function gemFuseAuto(apply = true, pref = -1){
  gemFuseClean();
  const vault = gemFuseVault();
  const ids = Object.keys(vault).filter(id => gemFuseLevel(id) >= 0);
  let tier = fuseGemSel.length ? gemFuseLevel(fuseGemSel[0]) : pref;
  if (tier < 0) tier = GEM_FUSE_GRADES.findIndex((_, i) => i < GEM_FUSE_GRADES.length - 1 && ids.filter(id => gemFuseLevel(id) === i).reduce((n,id) => n + vault[id], 0) >= 9);
  if (tier < 0 || tier >= GEM_FUSE_GRADES.length - 1 || fuseGemSel.length >= 9) return false;
  const selected = [...fuseGemSel];
  for (const id of ids.filter(id => gemFuseLevel(id) === tier)) {
    const take = Math.min(9 - selected.length, vault[id] - selected.filter(k => k === id).length);
    for (let i = 0; i < take; i++) selected.push(id);
  }
  if (selected.length !== 9) return false;
  if (apply) fuseGemSel = selected;
  return true;
}
window.openGemFusion = function(){ fuseType = 'gem'; openTab('fuse', true); renderFuse(); };
/** Số lò hợp nhanh theo từng phẩm (tối đa 1.000 lượt mỗi phẩm, giống server). */
function fuseQuickLots(isGem){
  const counts = Array(FUSE_R.length - 1).fill(0);
  for (const [id, n] of Object.entries(isGem ? gemFuseVault() : S.inventory)) {
    const q = isGem ? gemFuseLevel(id) : fuseQOf(id);
    if (n > 0 && q >= 0 && q < counts.length && (isGem || (ITEM[id]?.slot && ITEM[id]?.base))) counts[q] += n;
  }
  return counts.map(n => Math.min(1000, Math.floor(n / 9)));
}
const fuseQuickCount = isGem => fuseQuickLots(isGem).reduce((a, b) => a + b, 0);
let fuseQF = 'all';   // lọc túi theo phẩm: 'all' hoặc chỉ số phẩm
const fuseFocus = sel => $('pane-fuse').querySelector(sel)?.focus();   // vẽ lại xong: giữ focus bàn phím trên nút vừa bấm
function renderFuse(){
  const pane = $('pane-fuse'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0, forgeTop = pane.querySelector('.fz-forge')?.scrollTop || 0;   // vẽ lại thay mới cả lò: giữ vị trí cuộn
  const isGem = fuseType === 'gem';
  if (isGem) gemFuseClean(); else fuseClean();
  const selection = isGem ? fuseGemSel : fuseSel;
  const N = 9, q = selection.length ? (isGem ? gemFuseLevel(selection[0]) : fuseQOf(selection[0])) : -1;
  const grades = isGem ? GEM_FUSE_GRADES : FUSE_R;
  const chanceList = isGem ? (DATA.gemUpgradeChances || GEM_FUSE_CHANCES) : DATA.fuseChance || [];
  const chance = q >= 0 ? chanceList[q] || 0 : null;
  const noun = isGem ? 'ngọc' : 'trang bị';
  const quickCount = fuseQuickCount(isGem);
  const vault = gemFuseVault();
  const bag = isGem ? Object.keys(vault).filter(id => ITEM[id]?.type === 'gem' && vault[id] > 0).sort((a,b) => gemFuseLevel(a) - gemFuseLevel(b)).map(k => ({k,n:vault[k]})) : lcBagList();
  const used = {};
  const rankOf = k => isGem ? gemFuseLevel(k) : fuseQOf(k);
  const canFuse = k => { const it = ITEM[k], r = rankOf(k); return (isGem || (it.slot && it.base)) && r >= 0 && r < grades.length - 1; };
  // túi chỉ hiện món ghép được, chia nhóm theo phẩm (thấp → cao)
  const elig = bag.filter(({k}) => canFuse(k));
  const hidden = bag.length - elig.length;
  const cellOf = ({k,n}) => {
    const it = ITEM[k], rank = rankOf(k);
    const selectedCount = selection.filter(id => id === k).length;
    const j = used[k] = (used[k] || 0) + 1;
    const on = isGem ? selectedCount > 0 : j <= selectedCount;
    const off = q >= 0 && rank !== q;
    const depleted = isGem && n <= selectedCount;
    return `<button class="iv-cell eq r-${it.rarity} ${on ? 'msel' : ''} ${off ? 'off' : ''}" ${isGem ? 'data-fg' : 'data-fu'}="${k}" ${isGem && (off || depleted || selection.length >= N) ? 'disabled' : ''} aria-label="${it.name}" data-tip="${itemTip(k, off ? 'Chỉ chọn thêm vật phẩm cùng phẩm chất' : 'Nhấp để bỏ vào lò')}">${itemImg(k)}${isGem ? '<span class="gem-level">'+it.gemLevel+'</span><span class="qty">' + fmt(n - selectedCount) + '</span>' : '<span class="lv">' + itemLv(it) + '</span>'}</button>`;
  };
  const gradeName = grade => FUSE_N[FUSE_R.indexOf(grade)] || grade;
  const unit = isGem ? 'viên' : 'món';
  const byRank = grades.slice(0, -1).map((g, i) => { const items = elig.filter(x => rankOf(x.k) === i); return {i, g, items, n: items.reduce((a, x) => a + x.n, 0)}; }).filter(x => x.n);
  if (fuseQF !== 'all' && !byRank.some(x => x.i === fuseQF)) fuseQF = 'all';
  const chips = `<div class="fk-chips" role="group" aria-label="Lọc theo phẩm"><button class="fk-chip ${fuseQF === 'all' ? 'on' : ''}" data-fzq-f="all" aria-pressed="${fuseQF === 'all'}">Tất cả<small>${fmt(byRank.reduce((a, x) => a + x.n, 0))}</small></button>`
    + byRank.map(x => `<button class="fk-chip ${fuseQF === x.i ? 'on' : ''}" data-fzq-f="${x.i}" aria-pressed="${fuseQF === x.i}"><i class="fk-dot rc-${x.g}"></i><span class="rc-${x.g}">${gradeName(x.g)}</span><small>${fmt(x.n)}</small></button>`).join('') + '</div>';
  const cells = byRank.filter(x => fuseQF === 'all' || x.i === fuseQF).map(x => {
    const lots = Math.floor(x.n / 9);
    return `<div class="fk-grp"><div class="fk-gh"><b class="rc-${x.g}">${gradeName(x.g)} phẩm</b><span>${fmt(x.n)} ${unit} · ${lots ? 'đủ ' + fmt(lots) + ' lò' : 'thiếu ' + (9 - x.n % 9) + ' ' + unit}</span><span class="fk-gr">→ <span class="rc-${grades[x.i+1]}">${gradeName(grades[x.i+1])}</span> · ${Math.round((chanceList[x.i] || 0) * 100)}%</span>${lots && !selection.length ? `<button class="fk-fill" data-fzq="${x.i}">Bỏ 9 ${unit} vào lò</button>` : ''}${isGem && lots && !selection.length ? `<button class="fk-fill" data-fzq-all="${x.i}" ${busy ? 'disabled' : ''} title="Hợp tất cả nhóm đủ 9 viên phẩm này (tối đa 1.000 lượt)">Hợp hết ×${fmt(Math.min(1000, lots))}</button>` : ''}</div><div class="fk-cells">${x.items.map(cellOf).join('')}</div></div>`;
  }).join('');
  const slots = Array.from({length:N}, (_,i) => {
    const k = selection[i];
    return k ? `<button class="iv-cell eq r-${ITEM[k].rarity}" ${isGem ? 'data-fgs' : 'data-fs'}="${i}" aria-label="${ITEM[k].name}" data-tip="${itemTip(k,'Nhấp để lấy ra')}">${itemImg(k)}${isGem ? '' : '<span class="lv">' + itemLv(ITEM[k]) + '</span>'}</button>` : '<div class="iv-cell empty"></div>';
  }).join('');
  const kinds = [...new Set(selection.map(k => ITEM[k].name))];
  const kindTxt = kinds.length > 3 ? kinds.length + ' loại đã chọn' : kinds.join(', ');
  const pct = i => Math.round((chanceList[i] || 0) * 100);
  const ladder = grades.slice(0,-1).map((grade,i) => `<li class="${i === q ? 'cur' : ''}"><span><span class="rc-${grade}">${gradeName(grade)}</span> → <span class="rc-${grades[i+1]}">${gradeName(grades[i+1])}</span></span><b>${pct(i)}%</b></li>`).join('');
  const quickLots = fuseQuickLots(isGem), quickTx = quickLots.map((n, i) => n ? gradeName(grades[i]) + ' ×' + fmt(n) : '').filter(Boolean).join(' · ');
  const total = isGem ? bag.reduce((n,item) => n + item.n,0) : Object.keys(S.inventory).filter(k => ITEM[k]?.slot).reduce((n,k) => n + S.inventory[k],0);
  const eligible = isGem ? bag.filter(item => gemFuseLevel(item.k) < grades.length - 1).reduce((n,item) => n + item.n,0) : fuseUnits().length;
  const res = q >= 0
    ? `<div class="fk-rh"><span><span class="rc-${grades[q]}">${gradeName(grades[q])}</span> → <span class="rc-${grades[q+1]}">${gradeName(grades[q+1])} phẩm</span></span><b>${pct(q)}%</b></div><div class="fk-bar"><i style="width:${pct(q)}%"></i></div>
      <p class="ok">Thành công: 1 ${noun} ${qWord(grades[q+1])} ngẫu nhiên${isGem ? '.' : ' trong ' + kindTxt + '.'}</p><p class="bad">${chance === 1 ? 'Chắc chắn thành công.' : 'Thất bại: nhận 1 ' + noun + ' ' + qWord(grades[q]) + ' ngẫu nhiên, mất 8 ' + unit + '.'}</p>`
    : `<p class="fk-hint">Chọn 9 ${noun} cùng phẩm, hoặc bấm <b>Tự động điền</b>. Tỉ lệ thành công hiện ở đây.</p>`;
  pane.innerHTML = `<div class="dd fz fk">
    <header class="dd-top">${lcNav('fuse')}<div class="dd-cult"><span>Ghép <b>9 ${noun} cùng phẩm</b> thành 1 ${noun} phẩm cao hơn · thất bại nhận 1 ${noun} cùng phẩm</span></div><div class="dd-stone"><span>${isGem ? 'Tinh Thạch' : 'Trang bị'}</span><b>× ${fmt(total)}</b></div></header>
    <section class="dd-main fz-bag" aria-label="${isGem ? 'Kho Tinh Thạch' : 'Hành trang'}">
      ${fuseTypePicker(fuseType)}
      ${chips}
      <div class="fz-bar"><span>Ghép được ${fmt(eligible)} ${unit}${hidden ? ' · ẩn ' + fmt(hidden) + ' ô không ghép được' : ''}</span></div>
      <div class="iv-grid fz-grid fk-list">${cells || '<p class="fz-empty">Chưa có ' + noun + ' ghép được.</p>'}</div>
    </section>
    <aside class="fz-forge fk-forge" aria-label="Lò hợp thành">
      <div class="fk-fh"><p class="dd-sec">${isGem ? 'Lò hợp Tinh Thạch' : 'Lò luyện khí'}</p><span>Đã chọn <b>${selection.length}</b>/9 <button data-fz="clear" ${selection.length ? '' : 'disabled'}>Bỏ chọn</button></span></div>
      <div class="fz-slots">${slots}</div>
      <div class="fk-res">${res}</div>
      <button class="dd-synth-btn fz-go fk-go" data-fz="go" ${selection.length === N && !busy ? '' : 'disabled'}>${FUSE_GO}${isGem ? 'Hợp Tinh Thạch' : 'Luyện khí'}</button>
      <div class="fk-sub"><button class="fz-af" data-fz="auto" ${(isGem ? gemFuseAuto(false) : fuseCanAuto()) ? '' : 'disabled'}>${FUSE_GRID}<span>Tự động điền</span></button><button class="fz-af" data-fz="quick" ${quickCount && !busy ? '' : 'disabled'} title="Hợp các nhóm đủ 9 món cùng phẩm trong túi/kho. Không hợp tiếp vật phẩm vừa tạo; tối đa 1.000 lượt mỗi phẩm.">${FUSE_GO}<span>Hợp nhanh${quickCount ? ' ×' + fmt(quickCount) : ''}</span></button></div>
      <p class="fk-qtx">Hợp nhanh: ${quickTx || 'chưa có nhóm nào đủ 9 ' + unit}</p>
      <div class="fk-lad"><p class="dd-sec">Tỉ lệ theo phẩm</p><ul>${ladder}</ul></div>
      ${isGem ? '<p class="fk-qtx">Có thể trộn các loại ngọc cùng phẩm. Không tốn linh thạch.</p>' : ''}
    </aside></div>`;
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
  pane.querySelector('.fz-forge').scrollTop = forgeTop;
}
function changeFuseType(e){
  const t = e.target.closest('[data-fuse-type]'); if (!t) return;
  e.stopImmediatePropagation();   // nút chọn loại: không để click chạy tiếp vào xử lý lò
  if (t.value === 'pet') { openTab('petfuse', true); renderPetFuse(); return; }
  const next = t.value === 'gem' ? 'gem' : 'gear';
  if (next !== fuseType) fuseQF = 'all';
  fuseType = next;
  openTab('fuse', true); renderFuse();
}
$('pane-fuse').addEventListener('click', changeFuseType);
$('pane-petfuse').addEventListener('click', changeFuseType);
// Use the inventory's cell sizing rule in both crafting bags.
function fitLcBag(pane){
  const grid = pane.querySelector('.fz-grid');
  if (!grid?.clientWidth) return;
  const {cell} = bagCellSize(grid.clientWidth - 4);
  grid.style.setProperty('--cell', cell + 'px');
}
if (window.ResizeObserver){
  const lcBagObserver = new ResizeObserver(entries => entries.forEach(e => fitLcBag(e.target)));
  ['pane-fuse', 'pane-petfuse', 'pane-sell'].forEach(id => lcBagObserver.observe($(id)));
}
$('pane-fuse').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled) return;
  if (b.dataset.fzqF !== undefined) { fuseQF = b.dataset.fzqF === 'all' ? 'all' : +b.dataset.fzqF; renderFuse(); fuseFocus(`[data-fzq-f="${b.dataset.fzqF}"]`); return; }
  if (b.dataset.fzq !== undefined) { const i = +b.dataset.fzq; if (fuseType === 'gem') gemFuseAuto(true, i); else fuseAuto(i); renderFuse(); return; }
  if (b.dataset.fzqAll !== undefined) {   // hợp nhanh riêng một phẩm tinh thạch
    if (busy) return;
    b.disabled = true;
    if (await act('gem/quick-fuse?level=' + +b.dataset.fzqAll)) fuseGemSel = [];
    renderFuse(); return;
  }
  if (b.dataset.fz === 'quick') {
    if (busy) return;
    const isGem = fuseType === 'gem';
    b.disabled = true;
    if (await act(isGem ? 'gem/quick-fuse' : 'fuse/quick')) {
      if (isGem) fuseGemSel = []; else fuseSel = [];
    }
    renderFuse(); return;
  }
  if (fuseType === 'gem') {
    if (b.dataset.fg) {
      const id = b.dataset.fg;
      if (fuseGemSel.length < 9 && gemFuseLevel(id) < GEM_FUSE_GRADES.length - 1 && (!fuseGemSel.length || gemFuseLevel(id) === gemFuseLevel(fuseGemSel[0])) && fuseGemSel.filter(k => k === id).length < (gemFuseVault()[id] || 0)) fuseGemSel.push(id);
    } else if (b.hasAttribute('data-fgs')) fuseGemSel.splice(+b.dataset.fgs,1);
    else if (b.dataset.fz === 'clear') fuseGemSel = [];
    else if (b.dataset.fz === 'auto') gemFuseAuto();
    else if (b.dataset.fz === 'go' && !busy && fuseGemSel.length === 9) {
      b.disabled = true;
      if (await act('gem/fuse-free?items=' + encodeURIComponent(fuseGemSel.join(',')))) fuseGemSel = [];
    }
    renderFuse(); return;
  }
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

// ================= Linh thú: hợp thành 3 trang bị linh thú cùng ô, cùng phẩm → 1 món phẩm kế (chắc chắn thành công) =================
let petFuseSel = [];   // các món đã bỏ vào lò, cùng phẩm: cùng một id = hợp thường (ô giữ nguyên), lẫn nhiều ô = hợp hỗn hợp (ô ngẫu nhiên)
const petFuseRules = () => S.pet?.training || {gear: [], tierNames: [], fuseCount: 3, fuseStones: []};
const petGearOf = k => (petFuseRules().gear || []).find(g => g.id === k);
/** Trang bị linh thú trong túi: phẩm thấp → cao, rồi theo ô. */
function petFuseBag(){
  return (petFuseRules().gear || []).filter(g => (S.inventory[g.id] || 0) > 0)
    .sort((a, b) => a.tier - b.tier || a.slot.localeCompare(b.slot)).map(g => g.id);
}
function petFuseCan(k){ const g = petGearOf(k), R = petFuseRules(); return !!g && g.tier < (R.tierNames || []).length - 1; }
function petFuseClean(){
  const t0 = petGearOf(petFuseSel[0])?.tier, cnt = {};
  for (const id of petFuseSel) cnt[id] = (cnt[id] || 0) + 1;
  if (petFuseSel.some(id => !petFuseCan(id) || petGearOf(id).tier !== t0) || Object.entries(cnt).some(([id, n]) => (S.inventory[id] || 0) < n)) petFuseSel = [];
}
/** Tự động điền: phẩm thấp nhất đủ 3 món. Ưu tiên 3 món cùng ô; không có thì lấy lẫn các ô (hợp hỗn hợp). */
function petFuseAuto(){
  const N = petFuseRules().fuseCount || 3, bag = petFuseBag().filter(petFuseCan);
  for (const t of [...new Set(bag.map(k => petGearOf(k).tier))]) {
    const ids = bag.filter(k => petGearOf(k).tier === t);
    const same = ids.find(k => (S.inventory[k] || 0) >= N);
    if (same) return Array(N).fill(same);
    if (ids.reduce((a, k) => a + (S.inventory[k] || 0), 0) >= N) {
      const left = Object.fromEntries(ids.map(k => [k, S.inventory[k] || 0])), pick = [];
      while (pick.length < N) { const k = ids.reduce((a, b) => left[b] > left[a] ? b : a); left[k]--; pick.push(k); }
      return pick;
    }
  }
  return [];
}
/** Số lượt hợp nhanh theo từng phẩm (túi hiện tại, phẩm thấp trước tới khi hết linh thạch, giống server): nhóm cùng ô trước, món lẻ gom hỗn hợp. */
function petFuseQuickLots(){
  const R = petFuseRules(), N = R.fuseCount || 3, lots = {};
  let left = stones();
  const bag = petFuseBag().filter(petFuseCan);
  for (const t of [...new Set(bag.map(k => petGearOf(k).tier))]) {
    const cost = R.fuseStones?.[t] || 0, rem = [];
    for (const k of bag.filter(k => petGearOf(k).tier === t)) {
      const c = S.inventory[k] || 0, times = Math.min(Math.floor(c / N), cost > 0 ? Math.floor(left / cost) : Infinity);
      left -= cost * times; if (times) lots[t] = (lots[t] || 0) + times;
      rem.push(c - times * N);
    }
    while (cost > 0 && left >= cost && rem.reduce((a, b) => a + b, 0) >= N && rem.filter(n => n > 0).length > 1) {
      for (let j = 0; j < N; j++) rem[rem.indexOf(Math.max(...rem))]--;
      left -= cost; lots[t] = (lots[t] || 0) + 1;
    }
  }
  return lots;   // {bậc: số lần}
}
const petFuseQuickCount = () => Object.values(petFuseQuickLots()).reduce((a, b) => a + b, 0);
let petQF = 'all';   // lọc túi linh thú theo bậc
function renderPetFuse(){
  const pane = $('pane-petfuse'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0, forgeTop = pane.querySelector('.fz-forge')?.scrollTop || 0;   // vẽ lại thay mới cả lò: giữ vị trí cuộn
  petFuseClean();
  const R = petFuseRules(), N = R.fuseCount || 3, names = R.tierNames || [], k = petFuseSel[0], g = k ? petGearOf(k) : null;
  const mixed = new Set(petFuseSel).size > 1;   // lẫn nhiều ô: thành phẩm có ô ngẫu nhiên
  const out = g && !mixed ? (R.gear || []).find(x => x.slot === g.slot && x.tier === g.tier + 1) : null;
  const outs = g && mixed ? (R.gear || []).filter(x => x.tier === g.tier + 1) : [];
  const cost = g ? (R.fuseStones?.[g.tier] || 0) : 0, stoneOk = stones() >= cost;
  const bag = petFuseBag();
  const elig = bag.filter(petFuseCan), hidden = bag.length - elig.length;
  const cellOf = id => {
    const it = ITEM[id], n = S.inventory[id], picked = petFuseSel.filter(x => x === id).length;
    const off = g && petGearOf(id).tier !== g.tier;
    return `<button class="iv-cell eq r-${it.rarity} ${picked ? 'msel' : ''} ${off ? 'off' : ''}" data-pf="${id}" aria-label="${it.name}" data-tip="${itemTip(id, off ? 'Lò đang hợp thành phẩm khác, chỉ bỏ thêm được món cùng phẩm' : 'Nhấp để bỏ 1 món vào lò')}">${itemImg(id)}<span class="qty">${short(n - picked)}</span></button>`;
  };
  // nhóm theo bậc (thấp → cao), màu theo phẩm của món đầu nhóm
  const tierRc = t => ITEM[(R.gear || []).find(x => x.tier === t)?.id]?.rarity || 'common';
  const byTier = names.slice(0, -1).map((nm, t) => { const ids = elig.filter(id => petGearOf(id).tier === t); return {t, nm, ids, n: ids.reduce((a, id) => a + S.inventory[id], 0), lots: ids.reduce((a, id) => a + Math.floor(S.inventory[id] / N), 0)}; }).filter(x => x.n);
  if (petQF !== 'all' && !byTier.some(x => x.t === petQF)) petQF = 'all';
  const chips = `<div class="fk-chips" role="group" aria-label="Lọc theo phẩm"><button class="fk-chip ${petQF === 'all' ? 'on' : ''}" data-pfq-f="all" aria-pressed="${petQF === 'all'}">Tất cả<small>${fmt(byTier.reduce((a, x) => a + x.n, 0))}</small></button>`
    + byTier.map(x => `<button class="fk-chip ${petQF === x.t ? 'on' : ''}" data-pfq-f="${x.t}" aria-pressed="${petQF === x.t}"><i class="fk-dot rc-${tierRc(x.t)}"></i><span class="rc-${tierRc(x.t)}">${x.nm}</span><small>${fmt(x.n)}</small></button>`).join('') + '</div>';
  const cells = byTier.filter(x => petQF === 'all' || x.t === petQF).map(x =>
    `<div class="fk-grp"><div class="fk-gh"><b class="rc-${tierRc(x.t)}">${x.nm}</b><span>${fmt(x.n)} món · ${x.lots ? 'đủ ' + fmt(x.lots) + ' lò' : 'chưa đủ ' + N + ' món cùng ô'}</span><span class="fk-gr">→ <span class="rc-${tierRc(x.t + 1)}">${names[x.t + 1]}</span> · ${fmt(R.fuseStones?.[x.t] || 0)} linh thạch</span>${x.lots && !petFuseSel.length ? `<button class="fk-fill" data-pfq="${x.t}">Bỏ ${N} món vào lò</button>` : ''}</div><div class="fk-cells">${x.ids.map(cellOf).join('')}</div></div>`).join('');
  const qLots = petFuseQuickLots(), quickTx = Object.entries(qLots).map(([t, n]) => names[t] + ' ×' + fmt(n)).join(' · ');
  const ladder = names.slice(0, -1).map((nm, t) => `<li class="${g && g.tier === t ? 'cur' : ''}"><span><span class="rc-${tierRc(t)}">${nm}</span> → <span class="rc-${tierRc(t + 1)}">${names[t + 1]}</span></span><b>${fmt(R.fuseStones?.[t] || 0)}</b></li>`).join('');
  const slots = Array.from({length: N}, (_, i) => petFuseSel[i]
    ? `<button class="iv-cell eq r-${ITEM[petFuseSel[i]].rarity}" data-pfs="${i}" aria-label="${ITEM[petFuseSel[i]].name}" data-tip="${itemTip(petFuseSel[i], 'Nhấp để lấy ra')}">${itemImg(petFuseSel[i])}</button>`
    : '<div class="iv-cell empty"></div>').join('');
  const ready = petFuseSel.length === N && (!!out || outs.length > 0) && stoneOk;
  const quickCount = petFuseQuickCount();
  const res = g && outs.length
    ? `<div class="fk-rh"><span><span class="rc-${tierRc(g.tier)}">${names[g.tier]}</span> → <span class="rc-${tierRc(g.tier + 1)}">${names[g.tier + 1]}</span></span><b>100%</b></div>
      <div class="fk-outp">${outs.map(o => `<span class="iv-cell eq r-${ITEM[o.id].rarity}" data-tip="${itemTip(o.id)}">${itemImg(o.id)}</span>`).join('')}<span><b>Ô ngẫu nhiên</b><small>1 trong ${outs.length} ô, mỗi ô cùng tỉ lệ</small></span></div>
      <p class="ok">Chắc chắn thành công.</p><p class="${stoneOk ? 'fk-hint' : 'bad'}">Phí ${fmt(cost)} linh thạch${stoneOk ? '' : ' · không đủ linh thạch'}</p>`
    : g && out
    ? `<div class="fk-rh"><span><span class="rc-${tierRc(g.tier)}">${names[g.tier]}</span> → <span class="rc-${tierRc(g.tier + 1)}">${names[g.tier + 1]}</span></span><b>100%</b></div>
      <div class="fk-outp"><span class="iv-cell eq r-${ITEM[out.id].rarity}" data-tip="${itemTip(out.id)}">${itemImg(out.id)}</span><span><b>${ITEM[out.id].name}</b><small>+${out.pct}% chỉ số</small></span></div>
      <p class="ok">Chắc chắn thành công.</p><p class="${stoneOk ? 'fk-hint' : 'bad'}">Phí ${fmt(cost)} linh thạch${stoneOk ? '' : ' · không đủ linh thạch'}</p>`
    : `<p class="fk-hint">Chọn ${N} món <b>cùng phẩm</b>: cùng ô thì ô giữ nguyên, <b>khác ô</b> thì nhận về ô <b>ngẫu nhiên</b>. Hoặc bấm <b>Tự động điền</b>. Thành phẩm hiện ở đây.</p>`;
  pane.innerHTML = `<div class="dd fz pf fk">
      <header class="dd-top">${lcNav('fuse')}<div class="dd-cult"><span>Hợp thành <b>${N} trang bị linh thú cùng phẩm</b> thành 1 món phẩm kế (khác ô thì ô ngẫu nhiên) · chắc chắn thành công</span></div>
        <div class="dd-stone" data-tip="Linh thạch đang có"><span>Linh thạch</span><b>× ${fmt(stones())}</b></div></header>
      <section class="dd-main fz-bag" aria-label="Trang bị linh thú">${fuseTypePicker('pet')}
        ${chips}
        <div class="fz-bar"><span>Hợp thành được ${fmt(byTier.reduce((a, x) => a + x.n, 0))} món${hidden ? ` · ẩn ${fmt(hidden)} ô phẩm ${names.at(-1) || 'cao nhất'}` : ''}</span></div>
        <div class="iv-grid fz-grid fk-list">${cells || '<p class="fz-empty">Chưa có trang bị linh thú hợp thành được. Rơi ngẫu nhiên khi hạ Boss.</p>'}</div>
      </section>
      <aside class="fz-forge fk-forge" aria-label="Lò hợp thành linh thú">
        <div class="fk-fh"><p class="dd-sec">Lò hợp thành</p><span>Đã chọn <b>${petFuseSel.length}</b>/${N} <button data-pfz="clear" ${petFuseSel.length ? '' : 'disabled'}>Bỏ chọn</button></span></div>
        <div class="fz-slots">${slots}</div>
        <div class="fk-res">${res}</div>
        <button class="dd-synth-btn fz-go fk-go" data-pfz="go" ${ready && !busy ? '' : 'disabled'}>${FUSE_GO}Hợp thành</button>
        <div class="fk-sub"><button class="fz-af" data-pfz="auto" ${petFuseAuto().length ? '' : 'disabled'} data-tip="Tự động điền&#10;Lấy phẩm thấp nhất có đủ ${N} món: ưu tiên cùng ô, không có thì lẫn ô">${FUSE_GRID}<span>Tự động điền</span></button><button class="fz-af" data-pfz="quick" ${quickCount && !busy ? '' : 'disabled'} data-tip="Hợp thành nhanh&#10;Hợp mọi nhóm đủ ${N} món cùng phẩm trong túi (cùng ô trước, món lẻ hợp hỗn hợp), phẩm thấp trước tới khi hết linh thạch&#10;Không hợp tiếp món vừa tạo">${FUSE_GO}<span>Hợp nhanh${quickCount ? ' ×' + fmt(quickCount) : ''}</span></button></div>
        <p class="fk-qtx">Hợp nhanh: ${quickTx || 'chưa có loại nào đủ ' + N + ' món (hoặc thiếu linh thạch)'}</p>
        <div class="fk-lad"><p class="dd-sec">Phí linh thạch theo phẩm</p><ul>${ladder}</ul></div>
        <p class="fk-qtx">Phẩm ${names.at(-1) || 'cao nhất'} không rơi ở map, chỉ nhận từ hợp thành.</p>
      </aside>
    </div>`;
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
  pane.querySelector('.fz-forge').scrollTop = forgeTop;
}
$('pane-petfuse').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled) return;
  const N = petFuseRules().fuseCount || 3;
  if (b.dataset.pfqF !== undefined){ petQF = b.dataset.pfqF === 'all' ? 'all' : +b.dataset.pfqF; renderPetFuse(); $('pane-petfuse').querySelector(`[data-pfq-f="${b.dataset.pfqF}"]`)?.focus(); return; }
  if (b.dataset.pfq !== undefined){ const t = +b.dataset.pfq, k = petFuseBag().find(id => petFuseCan(id) && petGearOf(id).tier === t && (S.inventory[id] || 0) >= N); petFuseSel = k ? Array(N).fill(k) : []; renderPetFuse(); return; }
  if (b.dataset.pf){
    const k = b.dataset.pf;
    if (petFuseSel.length && petGearOf(petFuseSel[0])?.tier !== petGearOf(k)?.tier) petFuseSel = [];   // khác phẩm: làm lại lò
    if (petFuseSel.length < N && (S.inventory[k] || 0) > petFuseSel.filter(x => x === k).length) petFuseSel.push(k);
    renderPetFuse(); return;
  }
  if (b.dataset.pfs){ petFuseSel.splice(+b.dataset.pfs, 1); renderPetFuse(); return; }
  if (b.dataset.pfz === 'clear'){ petFuseSel = []; renderPetFuse(); return; }
  if (b.dataset.pfz === 'auto'){ petFuseSel = petFuseAuto(); renderPetFuse(); return; }
  if (b.dataset.pfz === 'quick'){
    if (busy) return;
    b.disabled = true;
    if (await act('petgear/quick-fuse')) petFuseSel = [];
    renderPetFuse(); return;
  }
  if (b.dataset.pfz === 'go' && petFuseSel.length === N){
    const sel = petFuseSel; petFuseSel = [];
    await act(new Set(sel).size > 1 ? `petgear/fuse-mix?items=${sel.map(encodeURIComponent).join(',')}` : `petgear/fuse/${encodeURIComponent(sel[0])}?n=1`);
    renderPetFuse();
  }
});
