/* Khảm Linh Thạch: one material family, weapon-quality backgrounds, nine-gem upgrades. */
(function () {
  let selectedSlot = 'weapon', selectedGem = null;
  const grades = ['common','uncommon','rare','epic','legendary','mythic','divine','immortal','dao','supreme'];
  const slotNames = { weapon: 'Vũ khí', offhand: 'Pháp bảo', head: 'Mũ', body: 'Áo', hands: 'Hộ uyển', legs: 'Quần', feet: 'Giày', necklace: 'Dây chuyền', charm: 'Hộ phù', ring: 'Nhẫn' };
  const state = () => S?.gemState || { vault: {}, slots: {}, maxSockets: {} };
  const level = id => ITEM[id]?.type === 'gem' ? grades.indexOf(ITEM[id].rarity) : -1;
  const gems = () => Object.keys(ITEM).filter(id => level(id) >= 0).sort((a,b) => level(a)-level(b) || ITEM[a].name.localeCompare(ITEM[b].name, 'vi'));
  const cell = (id, count, attrs = '', disabled = false) => `<button class="iv-cell gem-cell r-${ITEM[id].rarity}" ${attrs} ${disabled ? 'disabled' : ''} data-card="gem-pick" data-k="${id}" data-tip="1" aria-label="${ITEM[id].name}">${itemImg(id)}${count !== null ? `<span class="gem-level">${ITEM[id].gemLevel || grades.indexOf(ITEM[id].rarity)+1}</span><span class="qty">${fmt(count)}</span>` : ''}</button>`;
  const slotCategory = () => ['head','feet','body','hands','legs'].includes(selectedSlot) ? 'defense' : 'attack';
  const categoryName = () => slotCategory() === 'defense' ? 'phòng thủ' : 'tấn công';
  const compatible = id => ITEM[id]?.gemCategory === slotCategory();
  const removeCost = id => state().unsocketCosts?.[id] ?? 1000 * (level(id) + 1);
  const canRemove = id => (S.inventory?.linh_thach || 0) >= removeCost(id);
  function room(gs = state()) {
    const max = gs.maxSockets?.[selectedSlot] ?? 0;
    const slots = gs.slots?.[selectedSlot] || [];
    return Array.from({ length: max }, (_, i) => i).find(i => !slots[i]);
  }
  function renderSocket(gs) {
    const equipped = Object.entries(S.equipment || {}).filter(([slot, id]) => slotNames[slot] && ITEM[id]);
    if (!equipped.some(([slot]) => slot === selectedSlot)) selectedSlot = equipped[0]?.[0] || 'weapon';
    const equipId = S.equipment?.[selectedSlot];
    const free = equipId ? room(gs) : undefined;
    const blocked = free === undefined;
    if (blocked || !compatible(selectedGem) || !(gs.vault[selectedGem] > 0)) selectedGem = null;
    const max = gs.maxSockets?.[selectedSlot] ?? 0;
    const slots = gs.slots?.[selectedSlot] || [];
    const gear = equipped.map(([slot, id]) => `<button class="gem-equipment ${slot === selectedSlot ? 'selected' : ''}" data-gem-slot="${slot}" data-card="vault-slot" data-k="${id}" data-slot="${slot}" data-tip="1"><span class="iv-slot r-${ITEM[id].rarity}">${itemImg(id)}</span><span>${slotNames[slot]}<b class="rc-${ITEM[id].rarity}">${ITEM[id].name}</b><small>${(gs.slots[slot] || []).slice(0,gs.maxSockets[slot] ?? 0).filter(Boolean).length}/${gs.maxSockets[slot] ?? 0} lỗ khảm</small></span></button>`).join('');
    const socketed = (id, index, inactive = false) => `<div class="gem-socket-cell ${inactive ? 'gem-inactive' : ''}">${cell(id, null, `data-socketed="${index}" title="Chuột phải hoặc bấm Gỡ để trả Linh Thạch về túi"`)}${inactive ? '<small>Không kích hoạt</small>' : ''}<button class="gem-remove-btn" data-remove-gem="${index}" ${busy || !canRemove(id) ? 'disabled' : ''} title="Phí gỡ: ${fmt(removeCost(id))} linh thạch" aria-label="Gỡ ${ITEM[id].name} ở lỗ ${index + 1}">Gỡ · ${fmt(removeCost(id))}</button></div>`;
    const holes = Array.from({ length: 3 }, (_, i) => {
      if (i >= max) return slots[i] ? socketed(slots[i], i, true) : `<div class="gem-hole locked" title="Phàm–Địa: 1 lỗ; Thiên–Thần: 2 lỗ; Tiên–Huyền Tiên: 3 lỗ">Khóa<small>Theo phẩm</small></div>`;
      if (i === free && selectedGem) return `<div class="gem-pending" data-pending-gem="${selectedGem}">${cell(selectedGem, null, 'data-cancel-selected title="Chưa tiêu hao · Nhấp để bỏ chọn"')}<small>Chờ khảm</small></div>`;
      return slots[i] ? socketed(slots[i], i) : `<div class="gem-hole"><small>Trống</small></div>`;
    }).join('');
    const available = gems().filter(id => gs.vault[id] > 0 && compatible(id));
    const filled = slots.slice(0,max).filter(Boolean).length;
    return `<div class="gem-socket-layout gem-atelier">
      <section class="gem-panel gem-gear-panel"><div class="gem-section-title"><small>01 · TRANG BỊ</small><h3>Chọn trang bị</h3></div><div class="gem-equipment-list">${gear || '<p>Hãy mặc trang bị trước khi khảm Linh Thạch.</p>'}</div></section>
      <section class="gem-panel gem-center-panel">
        <div class="gem-socket-help"><button type="button" class="gem-help-icon" aria-label="Quy tắc mở lỗ" aria-describedby="gem-socket-rules">?</button><div class="gem-help-tooltip" id="gem-socket-rules" role="tooltip"><b>Quy tắc mở lỗ</b><p>Phàm–Địa: 1 lỗ<br>Thiên–Thần: 2 lỗ<br>Tiên–Huyền Tiên: 3 lỗ</p><p>Linh Thạch ở lỗ vượt phẩm được giữ lại nhưng tạm ngừng cộng chỉ số.</p></div></div>
        <div class="gem-ritual"><div class="gem-ritual-ring" aria-hidden="true"></div><div class="gem-ritual-item iv-slot r-${equipId ? ITEM[equipId].rarity : 'common'}" ${equipId ? 'data-card="vault-slot" data-k="'+equipId+'" data-slot="'+selectedSlot+'" data-tip="1"' : ''}>${equipId ? itemImg(equipId) : '<span>Chọn<br>trang bị</span>'}</div></div>
        <div class="gem-center-name">${equipId ? '<span>'+slotNames[selectedSlot]+'</span><h4 class="rc-'+ITEM[equipId].rarity+'">'+ITEM[equipId].name+'</h4>' : '<h4>Chưa có trang bị</h4>'}</div>
        <div class="gem-holes" aria-label="Các lỗ khảm trên trang bị">${holes}</div>
        <div class="gem-selection">${selectedGem ? cell(selectedGem,null)+'<div><b class="rc-'+ITEM[selectedGem].rarity+'">'+ITEM[selectedGem].name+'</b><p>'+(ITEM[selectedGem].special || ITEM[selectedGem].desc)+'</p></div>' : '<span>Chọn Linh Thạch bên phải<br><small>để đưa vào lỗ khảm trống</small></span>'}</div>
        <button class="dd-synth-btn gem-socket-confirm" data-socket-go ${selectedGem && !blocked && !busy ? '' : 'disabled'}>Khảm Linh Thạch</button>
      </section>
      <section class="gem-panel gem-picker-panel"><div class="gem-section-title"><small>02 · LINH TINH</small><h3>Linh Thạch ${categoryName()}</h3></div><p>${blocked ? 'Trang bị chưa có hoặc đã hết lỗ trống. Gỡ Linh Thạch hoặc đổi trang bị phẩm cao hơn để mở thêm lỗ.' : 'Nhấp hoặc chuột phải để đưa Linh Thạch vào ô chờ, sau đó bấm Khảm Linh Thạch để xác nhận.'}</p><div class="gem-grid">${available.map(id => cell(id,gs.vault[id],`data-select-gem="${id}" aria-pressed="${selectedGem === id}"`,blocked)).join('') || '<p>Túi chưa có Linh Thạch '+categoryName()+'.</p>'}</div></section>
      </div>`;
  }

  function renderVault() {
    const pane = $('pane-vault'); if (!pane || !S) return;
    const selectors = ['.gem-content', '.gem-gear-panel', '.gem-equipment-list', '.gem-center-panel', '.gem-picker-panel', '.gem-grid'];
    const scrolls = selectors.map(selector => {
      const el = pane.querySelector?.(selector);
      return [selector, el?.scrollTop || 0, el?.scrollLeft || 0];
    });
    const ancestors = [];
    for (let el = pane; el; el = el.parentElement) ancestors.push([el, el.scrollTop || 0, el.scrollLeft || 0]);
    const gs = state();
    pane.innerHTML = `<div class="dd vt"><header class="dd-top">${lcNav('vault')}<div class="dd-cult"><span>Linh Thạch · Khảm trang bị và hợp thành</span></div></header><section class="dd-main vt-main"><div class="gem-content">${renderSocket(gs)}</div></section></div>`;
    for (const [selector, top, left] of scrolls) {
      const el = pane.querySelector?.(selector);
      if (el) { el.scrollTop = top; el.scrollLeft = left; }
    }
    for (const [el, top, left] of ancestors) { el.scrollTop = top; el.scrollLeft = left; }
    if (pane._gemBound) return; pane._gemBound = true;
    pane.addEventListener('contextmenu', async e => {
      const pick = e.target.closest('[data-select-gem]');
      const remove = e.target.closest('[data-socketed]');
      if (!pick && !remove) return;
      e.preventDefault(); e.stopPropagation();
      if (busy) return;
      if (remove) { await unsocket(+remove.dataset.socketed); return; }
      else if (!pick.disabled && compatible(pick.dataset.selectGem) && room() !== undefined) {
        if (typeof hideTip === 'function') hideTip();
        selectedGem = pick.dataset.selectGem;
        renderVault();
      }
    });
    pane.addEventListener('click', async e => {
      const btn = e.target.closest('button'); if (!btn || btn.disabled) return;
      if (btn.hasAttribute('data-remove-gem')) { await unsocket(+btn.dataset.removeGem); return; }
      if (btn.dataset.gemSlot) { selectedSlot = btn.dataset.gemSlot; selectedGem = null; renderVault(); return; }
      if (btn.dataset.selectGem && compatible(btn.dataset.selectGem)) { selectedGem = btn.dataset.selectGem; renderVault(); return; }
      if (btn.hasAttribute('data-cancel-selected')) { selectedGem = null; renderVault(); return; }
      if (btn.hasAttribute('data-socket-go')) { await socket(selectedGem); return; }
      if (busy) return;

    });
  }
  async function socket(id) {
    const free = room();
    if (busy || !id || !compatible(id) || !S.equipment?.[selectedSlot] || free === undefined || !(state().vault[id] > 0)) return;
    if (typeof hideTip === 'function') hideTip();
    selectedGem = null;
    await act(`gem/socket?slot=${selectedSlot}&socketIndex=${free}&itemId=${encodeURIComponent(id)}`);
  }
  async function unsocket(index) {
    if (busy || !Number.isInteger(index) || !state().slots?.[selectedSlot]?.[index]) return;
    if (!canRemove(state().slots[selectedSlot][index])) { localLog('Không đủ linh thạch để gỡ viên này.', 'bad'); return; }
    if (typeof hideTip === 'function') hideTip();
    selectedGem = null;
    await act(`gem/unsocket?slot=${selectedSlot}&socketIndex=${index}`);
  }
  window.renderVault = renderVault;
  window.getVaultState = state;
  window.openVaultSlot = function (slot) { if (slotNames[slot]) selectedSlot = slot; selectedGem = null; openTab('vault', true); renderVault(); };
})();
