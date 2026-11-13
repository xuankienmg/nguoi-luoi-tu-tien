/* js/forge.js: Lò Rèn — rèn ô trang bị (Luyện chế → Lò Rèn). Server roll kết quả; đây chỉ hiển thị S.forge. */
const FORGE_KINDS = [
  {kind:'great',   label:'Đại thành',  effect:'+2 cấp'},
  {kind:'success', label:'Thành công', effect:'+1 cấp'},
  {kind:'hold',    label:'Bất biến',   effect:'giữ nguyên'},
  {kind:'damage',  label:'Tổn hại',    effect:'−1 cấp'},
  {kind:'break',   label:'Vỡ nát',     effect:'ô về +0'},
];
const FORGE_UNDO = {kind:'undo', label:'Hồi Thiên'};
const forgePct = x => (x * 100).toFixed(1).replace('.', ',') + '%';
function forgeOddsRows(odds){ return FORGE_KINDS.map((k, i) => ({...k, p: odds[i] || 0, pct: forgePct(odds[i] || 0)})); }
function forgeLogCounts(log){
  const c = {all: log.length};
  for (const k of [...FORGE_KINDS, FORGE_UNDO]) c[k.kind] = log.filter(e => e.kind === k.kind).length;
  return c;
}
function forgeLogFilter(log, kind){ return kind === 'all' ? log : log.filter(e => e.kind === kind); }

/** Dòng máy đổi: trang bị trong túi (có slot + base), số lượng > 0, điểm mỗi món theo phẩm; phẩm cao trước. */
function forgeSalvageRows(inv, items, rarities, points){
  return Object.entries(inv || {}).filter(([k, n]) => n > 0 && items[k]?.slot && items[k]?.base)
    .map(([k, n]) => ({id: k, name: items[k].name, n, each: points[rarities.indexOf(items[k].rarity)] || 0}))
    .filter(r => r.each > 0).sort((a, b) => b.each - a.each || a.name.localeCompare(b.name));
}

let forgeDiscardArm = null;   // bảo vật đang chờ bấm "Bỏ" lần 2
const forgeRelicDef = id => (DATA.forgeRelics || []).find(r => r.id === id) || {id, name: id, desc: '', price: 0};
let forgeSlot = null, forgeLogKind = 'all', forgeFlash = null, forgeFlashTimer = 0;
const forgeKindLabel = k => (k === 'undo' ? FORGE_UNDO : FORGE_KINDS.find(x => x.kind === k))?.label || k;
const forgeSlotName = id => (DATA.slots.find(s => s.id === id) || {}).name || id;
const forgeLvClass = l => l >= 50 ? ' hi' : l >= 10 ? ' mid' : '';
function playForgeSound(name){
  new Audio(`audio/forge-${name}.wav`).play().catch(() => {});
}
const FORGE_ANVIL = '<img class="fg-anvil-image" src="img/forge-anvil-side-v2.png" width="1774" height="887" alt="">';
/** Khu đe rèn: món đang rèn nằm trên đe; tia lửa theo kết quả nhát vừa rèn, lửa khi Thần Hỏa. */
function forgeAnvil(item, itemId, fire){
  const sparks = forgeFlash ? `<span class="fg-sparks">${Array.from({length: 10}, (_, i) => `<i style="--a:${i * 36 + 8}deg;--d:${48 + (i % 3) * 16}px"></i>`).join('')}</span>` : '';
  const flames = fire ? '<span class="fg-flames"><i></i><i></i><i></i><i></i><i></i></span>' : '';
  const art = item ? itemImg(itemId, 'fg-item-img') : '<span class="fg-item-empty">Ô trống</span>';
  return `<div class="fg-anvil${fire ? ' fire' : ''}${forgeFlash ? ` hit k-${forgeFlash}` : ''}" aria-hidden="true">
    <div class="fg-glow"></div>${flames}<div class="fg-anvil-art">${FORGE_ANVIL}<div class="fg-item">${art}</div>${sparks}</div></div>`;
}

function renderForge(){
  const pane = $('pane-forge'); if (!pane || !S?.forge) return;
  const F = S.forge;
  if (!F.slots.some(s => s.slot === forgeSlot)) forgeSlot = (F.slots.find(s => s.itemId) || F.slots[0])?.slot;
  const cur = F.slots.find(s => s.slot === forgeSlot);
  const item = cur?.itemId ? ITEM[cur.itemId] : null;
  const fire = cur && cur.frenzy > 0;
  const iron = S.inventory?.tinh_thiet || 0, talis = S.inventory?.hoi_thien_phu || 0;
  const maxed = cur && cur.level >= F.maxLevel;
  const canStrike = !!(cur && item && !maxed && (S.inventory?.linh_thach || 0) >= cur.stone && iron >= cur.iron && !busy);
  const seg = Array.from({length: F.frenzyCharge}, (_, i) => `<i class="${fire ? 'fire' : i < cur?.charge ? 'on' : ''}"></i>`).join('');
  const oldList = pane.querySelector('.fg-slots');
  const listTop = oldList?.scrollTop, listLeft = oldList?.scrollLeft;
  pane.innerHTML = `<div class="fg">
    <section class="fg-slots" aria-label="Chọn ô trang bị">${F.slots.map(s => `
      <button type="button" class="fg-slot${s.slot === forgeSlot ? ' sel' : ''}" data-fg-slot="${s.slot}" aria-pressed="${s.slot === forgeSlot}">
        <span class="fg-slot-name">${forgeSlotName(s.slot)}</span>
        <span class="fg-slot-item">${s.itemId ? ITEM[s.itemId]?.name || '' : 'Chưa mặc'}</span>
        <b class="fg-lv${forgeLvClass(s.level)}">+${s.level}</b>
      </button>`).join('')}</section>
    <section class="fg-stage${fire ? ' fire' : ''}">
      <header><div><small>${forgeSlotName(forgeSlot)}</small><h3>${item ? item.name : 'Ô trống'}</h3></div><b class="fg-big">+${cur?.level ?? 0}</b></header>
      ${forgeAnvil(item, cur?.itemId, fire)}
      <div class="fg-workbench">
      <p class="fg-bonus">Chỉ số món ở ô <strong>+${fmt(Math.round(cur?.bonusPct || 0))}%${maxed ? '' : ` → +${fmt(Math.round(cur?.nextBonusPct || 0))}%`}</strong></p>
      <div class="fg-gauge-head"><span>${fire ? `Thần Hỏa · còn ${cur.frenzy} nhát không vỡ` : `Hỏa Khí ${cur?.charge || 0}/${F.frenzyCharge}`}</span></div>
      <div class="fg-gauge" role="img" aria-label="Hỏa Khí">${seg}</div>
      <div class="fg-actions">
        <button type="button" class="fg-strike" data-fg="strike" ${canStrike ? '' : 'disabled'}>${fire ? 'Rèn · Thần Hỏa hộ thể' : 'Rèn'}</button>
        <button type="button" class="fg-undo" data-fg="undo" ${F.canUndo && talis > 0 && !busy ? '' : 'disabled'}>Hồi Thiên (${talis})</button>
      </div>
      <p class="fg-cost">${!item ? 'Mặc trang bị vào ô này để rèn.' : maxed ? `Đã đạt cực hạn +${F.maxLevel}.` : `Mỗi nhát: ${fmt(cur.stone)} linh thạch · ${cur.iron} Tinh Thiết (còn ${fmt(iron)})`}</p>
      </div>
      <section class="fg-relic-tray" aria-label="Kệ bảo vật">
      <div class="fg-tray-head"><h4>Bảo vật</h4><button type="button" class="fg-shop-open" data-fg="shop">Máy đổi bảo vật <b>${fmt(F.points || 0)} điểm</b><span aria-hidden="true">›</span></button></div>
      <div class="fg-relics">${Array.from({length: F.relicSlots || 4}, (_, i) => {
        const id = (F.relics || [])[i]; if (!id) return '<div class="fg-relic empty">Ô trống</div>';
        const r = forgeRelicDef(id);
        return `<div class="fg-relic"><b>${r.name}</b><small>${r.desc}</small>
          <button type="button" data-fg-discard="${id}">${forgeDiscardArm === id ? 'Bỏ thật?' : 'Bỏ'}</button></div>`;
      }).join('')}</div>
      </section>
    </section>
    <aside class="fg-side">
      <section class="fg-odds" aria-label="Tỉ lệ nhát kế tiếp"><h4>Nhát kế tiếp</h4>${forgeOddsRows(cur?.odds || []).map(r => `
        <div class="fg-odd k-${r.kind}${forgeFlash === r.kind ? ' flash' : ''}" data-odds="${r.kind}">
          <div><i></i><span>${r.label} <em>· ${r.effect}</em></span><b>${r.pct}</b></div>
          <div class="fg-bar"><s style="width:${Math.max(r.p * 100, 0.6)}%"></s></div>
        </div>`).join('')}</section>
      <section class="fg-log"><header><h4>Nhật ký</h4><button type="button" data-fg="log">Xem toàn bộ (${F.log.length})</button></header>
        ${F.log.length ? F.log.slice(0, 6).map(e => `<div class="fg-log-row k-${e.kind}"><span>${forgeSlotName(e.slot)}</span><b>${e.kind === 'break' ? `+${e.from} · Vỡ nát` : `+${e.from} → +${e.to} · ${forgeKindLabel(e.kind)}`}</b></div>`).join('') : '<p>Chưa có nhát rèn nào.</p>'}
      </section>
    </aside>
  </div>`;
  const list = pane.querySelector('.fg-slots');
  if (list){
    if (oldList) {
      list.scrollTop = listTop;
      list.scrollLeft = listLeft;
    }
    else list.querySelector('.fg-slot.sel')?.scrollIntoView({block: 'nearest', inline: 'nearest'});
  }
  if ($('forgeLog')?.open) openForgeLog();   // nhật ký đang mở: cập nhật theo trạng thái mới
  if ($('forgeShop')?.open) openForgeShop();
}

function openForgeLog(){
  let dlg = $('forgeLog');
  if (!dlg){ dlg = document.createElement('dialog'); dlg.id = 'forgeLog'; dlg.className = 'fg-dialog'; document.body.appendChild(dlg); }
  const log = S.forge.log, counts = forgeLogCounts(log);
  const hits = log.filter(e => e.kind !== 'undo'), ups = hits.filter(e => e.kind === 'great' || e.kind === 'success').length;
  dlg.innerHTML = `<header><div><h3>Nhật ký lò rèn</h3><p>${hits.length ? `Đã rèn ${hits.length} nhát · lên cấp ${Math.round(ups / hits.length * 100)}% · vỡ ${counts.break} lần` : 'Chưa có nhát rèn nào.'}</p></div>
      <button type="button" data-fg="log-close" aria-label="Đóng nhật ký">×</button></header>
    <div class="fg-filters" role="group" aria-label="Lọc theo kết quả">${[{kind:'all', label:'Tất cả'}, ...FORGE_KINDS, FORGE_UNDO].map(k =>
      `<button type="button" class="k-${k.kind}" data-fg-filter="${k.kind}" aria-pressed="${forgeLogKind === k.kind}">${k.label} <em>${counts[k.kind] || 0}</em></button>`).join('')}</div>
    <div class="fg-list">${forgeLogFilter(log, forgeLogKind).map(e => `<div class="fg-log-row k-${e.kind}"><span>#${e.n}</span><span>${forgeSlotName(e.slot)} · ${ITEM[e.itemId]?.name || ''}</span><b>${e.kind === 'break' ? `+${e.from} · Vỡ nát` : `+${e.from} → +${e.to} · ${forgeKindLabel(e.kind)}`}</b></div>`).join('') || '<p>Không có nhát rèn nào khớp bộ lọc.</p>'}</div>`;
  if (!dlg.open) dlg.showModal();
}

function openForgeShop(){
  let dlg = $('forgeShop');
  if (!dlg){ dlg = document.createElement('dialog'); dlg.id = 'forgeShop'; dlg.className = 'fg-dialog fg-shop'; dlg.setAttribute('aria-labelledby', 'forge-shop-title'); document.body.appendChild(dlg); }
  const F = S.forge, rows = forgeSalvageRows(S.inventory, ITEM, FUSE_R, DATA.forgeSalvagePoints || []);
  const body0 = dlg.querySelector('.fg-shop-body'), top0 = body0 ? body0.scrollTop : 0;
  const used = (F.relics || []).length, capacity = F.relicSlots || 4;
  const full = used >= capacity;
  dlg.innerHTML = `<header><div><h3 id="forge-shop-title">Máy đổi bảo vật</h3>
      <dl class="fg-shop-stats"><div><dt>Điểm Luyện Khí</dt><dd>${fmt(F.points || 0)}</dd></div><div><dt>Ô bảo vật</dt><dd>${used}/${capacity}</dd></div></dl></div>
      <button type="button" data-fg="shop-close" aria-label="Đóng máy đổi">×</button></header>
    <div class="fg-shop-body">
      <section aria-label="Kệ hôm nay"><h4>Kệ hôm nay</h4>${(F.shelf || []).map(o => { const r = forgeRelicDef(o.id);
        return `<div class="fg-offer"><span class="fg-relic-mark" aria-hidden="true">✦</span><b>${r.name}</b><small>${r.desc}</small><span class="fg-price">${fmt(r.price)} điểm</span>
          ${o.bought ? `<em>${o.owned ? 'Đã mua' : 'Đã mua hôm nay'}</em>` : o.owned ? '<button type="button" disabled>Đã có</button>' : `<button type="button" data-fg-buy="${o.id}" ${full || (F.points || 0) < r.price ? 'disabled' : ''}>Mua</button>`}</div>`; }).join('')}
        ${full ? '<p class="fg-warn">Kệ bảo vật đã đủ: bỏ bớt 1 món trước khi mua.</p>' : ''}
        <button type="button" class="fg-reroll" data-fg="reroll" ${(S.inventory?.linh_thach || 0) < (F.rerollStone || 0) ? "disabled" : ""}>Làm mới · ${fmt(F.rerollStone || 0)} linh thạch</button></section>
      <section aria-label="Đổi trang bị lấy điểm"><h4>Đổi trang bị lấy điểm</h4>${rows.map(r => `<div class="fg-salv">
        <div class="fg-salv-icon" aria-hidden="true">${itemImg(r.id)}</div><span>${r.name} <em>×${fmt(r.n)}</em></span><small>+${fmt(r.each)} điểm/món</small>
        <div class="fg-salv-actions"><button type="button" data-fg-salv="${r.id}" data-n="1">Đổi 1</button>
        ${r.n > 1 ? `<button type="button" data-fg-salv="${r.id}" data-n="${r.n}">Đổi hết</button>` : ""}</div></div>`).join('') || '<p class="fg-shop-empty">Không có trang bị nào trong túi để đổi.</p>'}</section>
    </div>`;
  if (!dlg.open) dlg.showModal();
  const body1 = dlg.querySelector('.fg-shop-body'); if (body1) body1.scrollTop = top0;
}

document.addEventListener('click', async e => {
  const slot = e.target.closest('[data-fg-slot]');
  if (slot){ forgeSlot = slot.dataset.fgSlot; forgeFlash = null; forgeDiscardArm = null; renderForge(); return; }
  const filter = e.target.closest('[data-fg-filter]');
  if (filter){ forgeLogKind = filter.dataset.fgFilter; openForgeLog(); return; }
  const salv = e.target.closest('[data-fg-salv]');
  if (salv){ await act(`forge/salvage?items=${encodeURIComponent(salv.dataset.fgSalv + ':' + salv.dataset.n)}`); renderForge(); return; }
  const buy = e.target.closest('[data-fg-buy]');
  if (buy){ await act(`forge/relic/buy/${buy.dataset.fgBuy}`); renderForge(); return; }
  const dis = e.target.closest('[data-fg-discard]');
  if (dis){
    const id = dis.dataset.fgDiscard;
    if (forgeDiscardArm !== id){ forgeDiscardArm = id; renderForge(); return; }
    forgeDiscardArm = null; await act(`forge/relic/discard/${id}`); renderForge(); return;
  }
  const b = e.target.closest('[data-fg]'); if (!b || b.disabled) return;
  if (b.dataset.fg === 'log') return openForgeLog();
  if (b.dataset.fg === 'log-close') return $('forgeLog')?.close();
  if (b.dataset.fg === 'shop'){ forgeDiscardArm = null; return openForgeShop(); }
  if (b.dataset.fg === 'shop-close'){ forgeDiscardArm = null; return $('forgeShop')?.close(); }
  if (b.dataset.fg === 'reroll'){ await act('forge/shelf/reroll'); renderForge(); return; }
  if (b.dataset.fg === 'strike' || b.dataset.fg === 'undo'){
    const strike = b.dataset.fg === 'strike'; forgeDiscardArm = null;
    if (strike) playForgeSound('strike');
    const ok = await act(strike ? `forge/strike/${forgeSlot}` : 'forge/undo');
    if (strike && ok){
      forgeFlash = S.forge.log[0]?.kind || null;
      if (forgeFlash === 'break') playForgeSound('break');
    }
    renderForge();   // act() vẽ lúc busy còn true → vẽ lại để mở khóa nút
    if (strike && ok){
      clearTimeout(forgeFlashTimer);
      forgeFlashTimer = setTimeout(() => {
        forgeFlash = null;
        document.querySelectorAll('.fg-odd.flash').forEach(x => x.classList.remove('flash'));
        document.querySelectorAll('.fg-anvil.hit .fg-sparks').forEach(x => x.remove());   // tia lửa tắt, giữ nguyên phần còn lại
      }, 650);
    }
  }
});
