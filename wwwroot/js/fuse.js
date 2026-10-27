/* Vấn Đạo · js/fuse.js: Luyện khí: ghép 9 trang bị, menu Luyện chế. Tách từ index.html (tools/split_index.py). */
// ================= Luyện khí: ghép 9 trang bị cùng phẩm → 1 trang bị phẩm cao hơn =================
const FUSE_R = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic', 'divine', 'immortal', 'dao', 'supreme'];
const FUSE_N = ['Phàm', 'Linh', 'Huyền', 'Địa', 'Thiên', 'Thánh', 'Thần', 'Tiên', 'Đạo', 'Huyền Tiên'];
let fuseSel = [];
/** Tự chọn theo phẩm: 'low' = phẩm thấp nhất đủ 9 món, hoặc số phẩm 0..8. */
let fuseAutoQ = 'low';   // tự động điền: lấy từ phẩm thấp nhất đủ 9 món
const FUSE_GRID = '<svg viewBox="0 0 16 16"><rect x="1" y="1" width="4" height="4"/><rect x="6" y="1" width="4" height="4"/><rect x="11" y="1" width="4" height="4"/><rect x="1" y="6" width="4" height="4"/><rect x="6" y="6" width="4" height="4"/><rect x="11" y="6" width="4" height="4"/><rect x="1" y="11" width="4" height="4"/><rect x="6" y="11" width="4" height="4"/><rect x="11" y="11" width="4" height="4"/></svg>';
const FUSE_GO = '<svg viewBox="0 0 24 24"><path d="M4 12h12M12 6l6 6-6 6"/><path d="M20 5v14"/></svg>';
/** Menu Luyện chế: tab con đang mở ('fuse' Luyện khí · 'craft' Đan dược · 'vault' Khảm Linh Thạch · 'petfuse' Linh thú · 'sell' Bán · 'spin' Vòng quay). Bấm tab Luyện chế luôn mở Luyện khí. */
const LC_SUBS = ['fuse', 'craft', 'vault', 'petfuse', 'sell', 'spin'];
let lcSub = 'fuse';   // luôn mở Luyện khí trước; chỉ nhớ tab con trong phiên đang chơi
const lcNav = on => `<nav class="lc-tabs" role="tablist" aria-label="Luyện chế">${[['fuse', 'Luyện khí'], ['craft', 'Đan dược'], ['vault', 'Khảm Linh Thạch'], ['sell', 'Bán'], ['spin', 'Vòng quay']].map(([id, nm]) =>
  `<button class="${id === on ? 'on' : ''}" data-lc="${id}" role="tab" aria-selected="${id === on}">${nm}</button>`).join('')}</nav>`;
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
const fuseTypePicker = type => `<label class="gem-view-switch">Loại hợp thành <select data-fuse-type aria-label="Loại hợp thành">${[['gear','Hợp trang bị'],['gem','Hợp Linh Thạch'],['pet','Trang bị Linh thú']].map(([id,label]) => `<option value="${id}" ${type === id ? 'selected' : ''}>${label}</option>`).join('')}</select></label>`;
let fuseType = 'gear';
let fuseGemSel = [];
const GEM_FUSE_GRADES = FUSE_R;
const GEM_FUSE_CHANCES = [.95, .90, .85, .80, .70, .60, .50, .40, .30, 0];
const gemFuseLevel = id => ITEM[id]?.type === 'gem' ? GEM_FUSE_GRADES.indexOf(ITEM[id].rarity) : -1;
const gemFuseVault = () => S.gemState?.vault || {};
function gemFuseClean(){
  const left = {...gemFuseVault()};
  fuseGemSel = fuseGemSel.filter(id => gemFuseLevel(id) >= 0 && left[id]-- > 0).slice(0, 9);
}
function gemFuseAuto(apply = true){
  gemFuseClean();
  const vault = gemFuseVault();
  const ids = Object.keys(vault).filter(id => gemFuseLevel(id) >= 0);
  let tier = fuseGemSel.length ? gemFuseLevel(fuseGemSel[0]) : -1;
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
function renderFuse(){
  const pane = $('pane-fuse'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0;
  const isGem = fuseType === 'gem';
  if (isGem) gemFuseClean(); else fuseClean();
  const selection = isGem ? fuseGemSel : fuseSel;
  const N = 9, q = selection.length ? (isGem ? gemFuseLevel(selection[0]) : fuseQOf(selection[0])) : -1;
  const grades = isGem ? GEM_FUSE_GRADES : FUSE_R;
  const chanceList = isGem ? (DATA.gemUpgradeChances || GEM_FUSE_CHANCES) : DATA.fuseChance || [];
  const chance = q >= 0 ? chanceList[q] || 0 : null;
  const noun = isGem ? 'ngọc' : 'trang bị';
  const vault = gemFuseVault();
  const bag = isGem ? Object.keys(vault).filter(id => ITEM[id]?.type === 'gem' && vault[id] > 0).sort((a,b) => gemFuseLevel(a) - gemFuseLevel(b)).map(k => ({k,n:vault[k]})) : lcBagList();
  const used = {};
  const cells = bag.map(({k,n}) => {
    const it = ITEM[k], rank = isGem ? gemFuseLevel(k) : fuseQOf(k);
    const can = isGem ? rank >= 0 && rank < grades.length - 1 : it.slot && it.base && rank >= 0 && rank < grades.length - 1;
    if (!can) return `<div class="iv-cell r-${it.rarity || 'common'} off na" data-tip="${itemTip(k, rank === grades.length - 1 ? 'Đã là phẩm cao nhất hiện có' : 'Không phải trang bị để hợp thành')}">${itemImg(k)}${lcCellTail(k,n)}</div>`;
    const selectedCount = selection.filter(id => id === k).length;
    const j = used[k] = (used[k] || 0) + 1;
    const on = isGem ? selectedCount > 0 : j <= selectedCount;
    const off = q >= 0 && rank !== q;
    const depleted = isGem && n <= selectedCount;
    return `<button class="iv-cell eq r-${it.rarity} ${on ? 'msel' : ''} ${off ? 'off' : ''}" ${isGem ? 'data-fg' : 'data-fu'}="${k}" ${isGem && (off || depleted || selection.length >= N) ? 'disabled' : ''} aria-label="${it.name}" data-tip="${itemTip(k, off ? 'Chỉ chọn thêm vật phẩm cùng phẩm chất' : 'Nhấp để bỏ vào lò')}">${itemImg(k)}${isGem ? '<span class="gem-level">'+it.gemLevel+'</span><span class="qty">' + fmt(n - selectedCount) + '</span>' : '<span class="lv">' + itemLv(it) + '</span>'}</button>`;
  }).join('');
  const slots = Array.from({length:N}, (_,i) => {
    const k = selection[i];
    return k ? `<button class="iv-cell eq r-${ITEM[k].rarity}" ${isGem ? 'data-fgs' : 'data-fs'}="${i}" aria-label="${ITEM[k].name}" data-tip="${itemTip(k,'Nhấp để lấy ra')}">${itemImg(k)}${isGem ? '' : '<span class="lv">' + itemLv(ITEM[k]) + '</span>'}</button>` : '<div class="iv-cell empty"></div>';
  }).join('');
  const kinds = [...new Set(selection.map(k => ITEM[k].name))];
  const kindTxt = kinds.length > 3 ? kinds.length + ' loại đã chọn' : kinds.join(', ');
  const gradeName = grade => FUSE_N[FUSE_R.indexOf(grade)] || grade;
  const rates = grades.slice(0,-1).map((grade,i) => gradeName(grade) + ' → ' + gradeName(grades[i+1]) + ': ' + Math.round((chanceList[i] || 0)*100) + '%').join('&#10;');
  const total = isGem ? bag.reduce((n,item) => n + item.n,0) : Object.keys(S.inventory).filter(k => ITEM[k]?.slot).reduce((n,k) => n + S.inventory[k],0);
  const eligible = isGem ? bag.filter(item => gemFuseLevel(item.k) < grades.length - 1).reduce((n,item) => n + item.n,0) : fuseUnits().length;
  pane.innerHTML = `<div class="dd fz">
    <header class="dd-top">${lcNav('fuse')}<div class="dd-cult"><span>Ghép <b>9 ${noun} cùng phẩm</b> thành 1 ${noun} phẩm cao hơn · thất bại nhận 1 ${noun} cùng phẩm</span></div><div class="dd-stone"><span>${isGem ? 'Linh Thạch' : 'Trang bị'}</span><b>× ${fmt(total)}</b></div></header>
    <section class="dd-main fz-bag" aria-label="${isGem ? 'Kho Linh Thạch' : 'Hành trang'}">
      ${fuseTypePicker(fuseType)}
      <p class="dd-sec">${isGem ? 'Linh Thạch · cấp 1 → 10' : 'Hành trang · trang bị phẩm thấp → cao, rồi vật phẩm khác'}</p>
      <div class="fz-bar"><span>Ghép được ${fmt(eligible)} ${isGem ? 'viên' : 'món'} · đã chọn ${selection.length}/9${q >= 0 ? ' (' + qWord(grades[q]) + ')' : ''}</span><button data-fz="clear" ${selection.length ? '' : 'disabled'}>Bỏ chọn</button></div>
      <div class="iv-grid fz-grid">${cells || '<p class="fz-empty">Chưa có ' + noun + '.</p>'}</div>
    </section>
    <aside class="fz-forge" aria-label="Lò hợp thành"><p class="dd-sec">${isGem ? 'Lò hợp Linh Thạch' : 'Lò luyện khí'}</p><div class="fz-slots">${slots}</div><div class="fz-arrow">↓</div>
      <div class="fz-out" data-tip="Tỉ lệ thành công&#10;${rates}"><div class="iv-cell eq ${q >= 0 ? 'r-' + grades[q+1] : ''}"><span class="q">?</span></div><div class="fz-rate">${q >= 0 ? '<b>' + Math.round(chance*100) + '%</b> thành công' : 'Chọn ' + noun + ' để xem tỉ lệ'}</div></div>
      <ul class="fz-rules">${q >= 0 ? '<li class="ok">Thành công: 1 ' + noun + ' ' + qWord(grades[q+1]) + ' ngẫu nhiên' + (isGem ? '.' : ' trong ' + kindTxt) + '</li><li class="bad">' + (chance === 1 ? 'Chắc chắn thành công.' : 'Thất bại: nhận 1 ' + noun + ' ' + qWord(grades[q]) + ' ngẫu nhiên, mất 8 ' + (isGem ? 'viên' : 'món') + '.') + '</li>' : '<li>Chọn 9 ' + noun + ' cùng phẩm vào lò.</li>'}
      <li>${isGem ? 'Có thể trộn các loại ngọc cùng phẩm. Không tốn linh thạch.' : 'Loại và cảnh giới đầu ra được chọn từ 9 món đã bỏ vào.'}</li></ul>
      <div class="fz-act"><div class="fz-auto"><button class="fz-af" data-fz="auto" ${(isGem ? gemFuseAuto(false) : fuseCanAuto()) ? '' : 'disabled'}>${FUSE_GRID}<span>Tự động điền</span></button></div><button class="dd-synth-btn fz-go" data-fz="go" ${selection.length === N && !busy ? '' : 'disabled'}>${FUSE_GO}${isGem ? 'Hợp Linh Thạch' : 'Luyện khí'}</button></div>
    </aside></div>`;
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
}
function changeFuseType(e){
  if (!e.target.matches('[data-fuse-type]')) return;
  if (e.target.value === 'pet') { openTab('petfuse', true); renderPetFuse(); return; }
  fuseType = e.target.value === 'gem' ? 'gem' : 'gear';
  openTab('fuse', true); renderFuse();
}
$('pane-fuse').addEventListener('change', changeFuseType);
$('pane-petfuse').addEventListener('change', changeFuseType);
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
let petFuseSel = [];   // các món đã bỏ vào lò (cùng một id)
const petFuseRules = () => S.pet?.training || {gear: [], tierNames: [], fuseCount: 3, fuseStones: []};
const petGearOf = k => (petFuseRules().gear || []).find(g => g.id === k);
/** Trang bị linh thú trong túi: phẩm thấp → cao, rồi theo ô. */
function petFuseBag(){
  return (petFuseRules().gear || []).filter(g => (S.inventory[g.id] || 0) > 0)
    .sort((a, b) => a.tier - b.tier || a.slot.localeCompare(b.slot)).map(g => g.id);
}
function petFuseCan(k){ const g = petGearOf(k), R = petFuseRules(); return !!g && g.tier < (R.tierNames || []).length - 1; }
function petFuseClean(){
  const k = petFuseSel[0];
  if (k && (!petFuseCan(k) || (S.inventory[k] || 0) < petFuseSel.length)) petFuseSel = [];
}
/** Tự động điền: phẩm thấp nhất có đủ 3 món cùng ô. */
function petFuseAutoId(){ const n = petFuseRules().fuseCount || 3; return petFuseBag().find(k => petFuseCan(k) && (S.inventory[k] || 0) >= n); }
function renderPetFuse(){
  const pane = $('pane-petfuse'); if (!pane || !S) return;
  const bagTop = pane.querySelector('.fz-grid')?.scrollTop || 0;
  petFuseClean();
  const R = petFuseRules(), N = R.fuseCount || 3, names = R.tierNames || [], k = petFuseSel[0], g = k ? petGearOf(k) : null;
  const out = g ? (R.gear || []).find(x => x.slot === g.slot && x.tier === g.tier + 1) : null;
  const cost = g ? (R.fuseStones?.[g.tier] || 0) : 0, stoneOk = stones() >= cost;
  const bag = petFuseBag();
  const cells = bag.map(id => {
    const it = ITEM[id], n = S.inventory[id], picked = petFuseSel.length && id === k ? petFuseSel.length : 0;
    if (!petFuseCan(id)) return `<div class="iv-cell eq r-${it.rarity} off na" aria-label="${it.name}" data-tip="${itemTip(id, `${names.at(-1)}: đã là phẩm cao nhất, không hợp thành được`)}">${itemImg(id)}<span class="qty">${short(n)}</span></div>`;
    const off = k && id !== k;
    return `<button class="iv-cell eq r-${it.rarity} ${picked ? 'msel' : ''} ${off ? 'off' : ''}" data-pf="${id}" aria-label="${it.name}" data-tip="${itemTip(id, off ? 'Lò đang hợp thành món khác, chỉ bỏ thêm được món cùng ô, cùng phẩm' : 'Nhấp để bỏ 1 món vào lò')}">${itemImg(id)}<span class="qty">${short(n - picked)}</span></button>`;
  }).join('');
  const slots = Array.from({length: N}, (_, i) => petFuseSel[i]
    ? `<button class="iv-cell eq r-${ITEM[k].rarity}" data-pfs="${i}" aria-label="${ITEM[k].name}" data-tip="${itemTip(k, 'Nhấp để lấy ra')}">${itemImg(k)}</button>`
    : '<div class="iv-cell empty"></div>').join('');
  const ready = petFuseSel.length === N && !!out && stoneOk;
  const times = g ? Math.min(Math.floor((S.inventory[k] || 0) / N), cost > 0 ? Math.floor(stones() / cost) : Infinity) : 0;
  const unitN = bag.reduce((a, id) => a + S.inventory[id], 0);
  pane.innerHTML = `<div class="dd fz pf">
      <header class="dd-top">${lcNav('fuse')}<div class="dd-cult"><span>Hợp thành <b>${N} trang bị linh thú cùng ô, cùng phẩm</b> thành 1 món phẩm kế · chắc chắn thành công</span></div>
        <div class="dd-stone" data-tip="Linh thạch đang có"><span>Linh thạch</span><b>× ${fmt(stones())}</b></div></header>
      <section class="dd-main fz-bag" aria-label="Trang bị linh thú">${fuseTypePicker('pet')}<p class="dd-sec">Trang bị linh thú · phẩm thấp → cao</p>
        <div class="fz-bar"><span>Có ${fmt(unitN)} món · đã chọn ${petFuseSel.length}/${N}${g ? ` (${qWord(ITEM[k].rarity)})` : ''}</span>
          <button data-pfz="clear" ${petFuseSel.length ? '' : 'disabled'}>Bỏ chọn</button></div>
        <div class="iv-grid fz-grid">${cells || '<p class="fz-empty">Chưa có trang bị linh thú. Rơi ngẫu nhiên khi hạ Boss.</p>'}</div>
      </section>
      <aside class="fz-forge" aria-label="Lò hợp thành linh thú"><p class="dd-sec">Lò hợp thành</p>
        <div class="fz-slots">${slots}</div>
        <div class="fz-arrow">↓</div>
        <div class="fz-out">${out ? `<div class="iv-cell eq r-${ITEM[out.id].rarity}" data-tip="${itemTip(out.id)}">${itemImg(out.id)}</div>` : '<div class="iv-cell eq"><span class="q">?</span></div>'}
          <div class="fz-rate">${out ? `<b>${ITEM[out.id].name}</b>` : 'Chọn trang bị linh thú để xem thành phẩm'}</div></div>
        <ul class="fz-rules">${g ? `<li class="ok">Thành phẩm: 1 ${ITEM[out.id].name} (+${out.pct}%)</li>
          <li class="${stoneOk ? '' : 'bad'}">Tốn ${fmt(cost)} linh thạch mỗi lần hợp thành</li>` : `<li>Bỏ ${N} món cùng ô, cùng phẩm vào lò.</li>`}
          <li>Phẩm ${names[names.length - 1] || 'Thánh'} không rơi ở map, chỉ nhận từ hợp thành.</li></ul>
        <div class="fz-act">
          <div class="fz-auto">
            <button class="fz-af" data-pfz="auto" ${petFuseAutoId() ? '' : 'disabled'} data-tip="Tự động điền&#10;Lấy phẩm thấp nhất có đủ ${N} món cùng ô">${FUSE_GRID}<span>Tự động điền</span></button>
            <button class="fz-af" data-pfz="all" ${ready && times > 1 && !busy ? '' : 'disabled'} data-tip="Hợp thành tối đa&#10;Dùng hết món cùng loại và linh thạch đang có">${FUSE_GO}<span>Hợp thành ×${fmt(Math.max(1, times))}</span></button>
          </div>
          <button class="dd-synth-btn fz-go" data-pfz="go" ${ready && !busy ? '' : 'disabled'}>${FUSE_GO}Hợp thành</button>
        </div>
      </aside>
    </div>`;
  fitLcBag(pane);
  pane.querySelector('.fz-grid').scrollTop = bagTop;
}
$('pane-petfuse').addEventListener('click', async e => {
  const b = e.target.closest('button'); if (!b || b.disabled) return;
  const N = petFuseRules().fuseCount || 3;
  if (b.dataset.pf){
    const k = b.dataset.pf;
    if (petFuseSel.length && petFuseSel[0] !== k) petFuseSel = [];   // chọn món khác: làm lại lò
    if (petFuseSel.length < N && (S.inventory[k] || 0) > petFuseSel.length) petFuseSel.push(k);
    renderPetFuse(); return;
  }
  if (b.dataset.pfs){ petFuseSel.splice(+b.dataset.pfs, 1); renderPetFuse(); return; }
  if (b.dataset.pfz === 'clear'){ petFuseSel = []; renderPetFuse(); return; }
  if (b.dataset.pfz === 'auto'){ const k = petFuseAutoId(); petFuseSel = k ? Array(N).fill(k) : []; renderPetFuse(); return; }
  if ((b.dataset.pfz === 'go' || b.dataset.pfz === 'all') && petFuseSel.length === N){
    const k = petFuseSel[0]; petFuseSel = [];
    await act(`petgear/fuse/${encodeURIComponent(k)}?n=${b.dataset.pfz === 'all' ? 0 : 1}`);
    renderPetFuse();
  }
});
