/* Vấn Đạo · js/vault.js: Bảo Khố Đá Ngọc, Khảm Nạm & Lò Hợp Nhất (Diablo 2 style) */

(function () {
  let vaultTab = 'socket'; // 'socket' | 'vault' | 'cube'
  let socketSlot = 'weapon';
  let socketIdx = 0;
  let selectedGemId = null;
  let pickerTab = 'all'; // 'all' | 'gem' | 'stone'
  let cubeCategory = 'all'; // 'all' | 'stone' | 'transcend' | 'gem' | 'herb'

  const SLOT_NAMES = {
    weapon: 'Vũ khí',
    offhand: 'Pháp bảo',
    head: 'Mũ',
    body: 'Áo',
    hands: 'Hộ uyển',
    legs: 'Quần',
    feet: 'Giày',
    necklace: 'Dây chuyền',
    charm: 'Hộ phù',
    ring: 'Nhẫn'
  };

  const GEM_COLOR = {
    gem_ruby_cui: '#ff4d4f', gem_ruby_xin: '#f5222d',
    gem_sapphire_cui: '#40a9ff', gem_sapphire_xin: '#1890ff',
    gem_topaz_cui: '#faad14', gem_topaz_xin: '#d48806',
    gem_emerald_cui: '#52c41a', gem_emerald_xin: '#389e0d',
    gem_amethyst_cui: '#9254de', gem_amethyst_xin: '#722ed1',
    gem_thien_phu: '#eb2f96',
    stone_viem_so: '#ff4d4f', stone_viem_cao: '#ff7a45',
    stone_bang_so: '#40a9ff', stone_bang_cao: '#1890ff',
    stone_loi_so: '#9254de', stone_loi_cao: '#722ed1',
    stone_kim_so: '#faad14', stone_kim_cao: '#d48806',
    stone_tho_so: '#a07040', stone_tho_cao: '#874d00',
    stone_liet_loi: '#f5222d', stone_huyen_bang: '#13c2c2',
    stone_am_duong: '#eb2f96', stone_socket: '#ffd700'
  };

  function getGemColor(id) {
    return GEM_COLOR[id] || (ITEM[id]?.color || '#ffd700');
  }

  function getVaultState() {
    return S?.gemState || {
      vault: {},
      slots: {},
      maxSockets: {},
      recipes: []
    };
  }

  function renderVault() {
    const pane = $('pane-vault');
    if (!pane || !S) return;

    const gs = getVaultState();
    const vault = gs.vault || {};

    let html = `
      <div class="vault-wrap">
        <header class="vault-header">
          <div class="vault-nav">
            <button class="vault-tab-btn ${vaultTab === 'socket' ? 'active' : ''}" data-vtab="socket">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="4"/></svg>
              Khảm Nạm
            </button>
            <button class="vault-tab-btn ${vaultTab === 'vault' ? 'active' : ''}" data-vtab="vault">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 22 8.5 18 22 6 22 2 8.5"/></svg>
              Bảo Khố
            </button>
            <button class="vault-tab-btn ${vaultTab === 'cube' ? 'active' : ''}" data-vtab="cube">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
              Lò Hợp Nhất
            </button>
          </div>
          <div class="concept-wallet" title="Linh thạch hiện tại">
            <img src="img/stone/linh_thach.png" alt="">
            <b class="concept-balance">${fmt(S.inventory?.linh_thach || 0)}</b>
          </div>
        </header>
        <div class="vault-body">
    `;

    if (vaultTab === 'socket') {
      html += renderSocketView(gs);
    } else if (vaultTab === 'vault') {
      html += renderVaultStorageView(gs);
    } else if (vaultTab === 'cube') {
      html += renderCubeView(gs);
    }

    html += `
        </div>
      </div>
    `;

    pane.innerHTML = html;
    bindVaultEvents(pane);
  }

  // =========================================================================
  // 1. RENDER KHẢM NẠM (SOCKET VIEW)
  // =========================================================================
  function renderSocketView(gs) {
    const slots = gs.slots || {};
    const maxSockets = gs.maxSockets || {};
    const vault = gs.vault || {};

    const curMax = maxSockets[socketSlot] || (S.realm >= 4 ? 3 : S.realm >= 2 ? 2 : 1);
    const curGems = slots[socketSlot] || [];
    const equippedItemId = S.equipment?.[socketSlot];
    const equippedItem = equippedItemId ? ITEM[equippedItemId] : null;

    // Cột 1: Danh sách 10 ô trang bị
    const equipSlotsOrder = ['weapon', 'offhand', 'head', 'body', 'hands', 'legs', 'feet', 'necklace', 'charm', 'ring'];
    let slotListHtml = equipSlotsOrder.map(slotId => {
      const eqId = S.equipment?.[slotId];
      const eq = eqId ? ITEM[eqId] : null;
      const sMax = maxSockets[slotId] || 1;
      const sGems = slots[slotId] || [];
      const isActive = slotId === socketSlot;

      let pipsHtml = '';
      for (let i = 0; i < sMax; i++) {
        const gemId = sGems[i];
        if (gemId && ITEM[gemId]) {
          pipsHtml += `<span class="p" style="background:${getGemColor(gemId)};box-shadow:0 0 4px ${getGemColor(gemId)}"></span>`;
        } else {
          pipsHtml += `<span class="p" style="background:#444"></span>`;
        }
      }

      return `
        <button class="socket-slot-btn ${isActive ? 'active' : ''}" data-select-slot="${slotId}">
          <div class="socket-slot-icon r-${eq?.rarity || 'common'}">
            ${eqId ? itemImg(eqId) : `<span style="font-size:10px;color:#666">${SLOT_NAMES[slotId]?.slice(0, 2)}</span>`}
          </div>
          <div class="socket-slot-info">
            <span class="title">${SLOT_NAMES[slotId] || slotId}</span>
            <span class="item-name">${eq ? eq.name : '(Trống)'}</span>
            <div class="socket-slot-pips">${pipsHtml}</div>
          </div>
        </button>
      `;
    }).join('');

    // Cột 2: Sockets & Action
    let orbsHtml = '';
    for (let i = 0; i < 3; i++) {
      const isLocked = i >= curMax;
      const gemId = isLocked ? null : curGems[i];
      const isSelected = i === socketIdx && !isLocked;
      const gemDef = gemId ? ITEM[gemId] : null;

      let orbClass = 'socket-orb';
      if (isLocked) orbClass += ' locked';
      else if (gemDef) orbClass += ' filled';
      if (isSelected) orbClass += ' selected';

      let inner = '';
      if (isLocked) {
        inner = `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#666" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>`;
      } else if (gemDef) {
        inner = `<img src="${gemDef.icon}" alt="${gemDef.name}" style="color:${getGemColor(gemId)}">`;
      } else {
        inner = `<span style="font-size:24px;color:rgba(212,175,55,0.4)">+</span>`;
      }

      orbsHtml += `
        <div class="socket-orb-box">
          <div class="${orbClass}" data-select-orb="${i}" style="${gemDef ? `border-color:${getGemColor(gemId)};box-shadow:0 0 12px ${getGemColor(gemId)}55` : ''}">
            ${inner}
          </div>
          <span class="socket-orb-title">Lỗ ${i + 1}</span>
          <span class="orb-label">${isLocked ? 'Khóa' : gemDef ? gemDef.name : 'Trống'}</span>
        </div>
      `;
    }

    // Action card for selected socket
    let actionCardHtml = '';
    const curSelectedGem = curGems[socketIdx];
    const curSelectedGemDef = curSelectedGem ? ITEM[curSelectedGem] : null;

    if (socketIdx >= curMax) {
      // Locked socket
      const stoneSocketCount = vault['stone_socket'] || 0;
      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Lỗ khảm số ${socketIdx + 1} đang bị khóa</h4>
          <p>Mở tự động khi thăng cấp cảnh giới (Hóa Thần mở đủ 3 lỗ) hoặc dùng Thiên Công Đục Thạch để khai mở sớm.</p>
          <div style="margin-top:12px;">
            <button class="btn-unlock-socket" data-act-unlock="${socketSlot}" ${stoneSocketCount > 0 ? '' : 'disabled'}>
              Dùng Thiên Công Đục Thạch (${stoneSocketCount}/1)
            </button>
          </div>
        </div>
      `;
    } else if (selectedGemId && ITEM[selectedGemId]) {
      // Player selected a gem to socket
      const pickDef = ITEM[selectedGemId];
      const previewText = pickDef.special || (pickDef.atk ? `+${pickDef.atk} Tấn công` : pickDef.hp ? `+${pickDef.hp} Máu` : `+${pickDef.def} Phòng thủ`);
      const hasCount = vault[selectedGemId] || 0;

      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Chuẩn bị khảm vào Lỗ ${socketIdx + 1}</h4>
          <div style="display:flex;align-items:center;justify-content:center;gap:8px;margin:8px 0;">
            <img src="${pickDef.icon}" style="width:36px;height:36px;">
            <b class="rc-${pickDef.rarity}">${pickDef.name}</b>
            <span style="font-size:12px;color:#aaa">(Còn ×${hasCount})</span>
          </div>
          <p>${pickDef.desc}</p>
          <div class="socket-preview-stats">
            Hiệu quả khảm: <b>${previewText}</b>
          </div>
          <div class="socket-btn-row">
            <button class="btn-socket-do" data-act-socket="${selectedGemId}" ${hasCount > 0 ? '' : 'disabled'}>
              Đập Ngọc / Khảm Nạm
            </button>
            <button class="btn-unsocket" data-cancel-pick="1">Hủy</button>
          </div>
        </div>
      `;
    } else if (curSelectedGemDef) {
      // Currently has a gem
      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Lỗ ${socketIdx + 1}: ${curSelectedGemDef.name}</h4>
          <div style="display:flex;align-items:center;justify-content:center;gap:8px;margin:8px 0;">
            <img src="${curSelectedGemDef.icon}" style="width:36px;height:36px;">
            <b class="rc-${curSelectedGemDef.rarity}">${curSelectedGemDef.name}</b>
          </div>
          <p>${curSelectedGemDef.desc}</p>
          <div class="socket-preview-stats">
            Đang kích hoạt: <b>${curSelectedGemDef.special || 'Gia tăng sức mạnh'}</b>
          </div>
          <div class="socket-btn-row">
            <button class="btn-unsocket" data-act-unsocket="${socketSlot}" data-orb-idx="${socketIdx}">
              Tháo Ngọc (Hoàn 100% về Bảo Khố)
            </button>
          </div>
        </div>
      `;
    } else {
      // Empty socket
      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Lỗ ${socketIdx + 1} đang trống</h4>
          <p>Hãy chọn một viên Phù Thạch hoặc Linh Ngọc từ bảng bên phải để khảm vào trang bị.</p>
        </div>
      `;
    }

    // Cột 3: Vault Picker
    const availableItems = Object.entries(vault)
      .filter(([id, cnt]) => cnt > 0 && ITEM[id])
      .filter(([id]) => {
        if (pickerTab === 'gem') return ITEM[id].type === 'gem';
        if (pickerTab === 'stone') return ITEM[id].type === 'stone' && id !== 'stone_socket';
        return id !== 'stone_socket';
      });

    let pickerGridHtml = availableItems.map(([id, cnt]) => {
      const it = ITEM[id];
      const isPick = id === selectedGemId;
      return `
        <div class="picker-item ${isPick ? 'selected' : ''}" data-pick-gem="${id}" data-tip="${itemTip(id, 'Nhấp để chọn khảm')}">
          <img src="${it.icon}" alt="${it.name}">
          <span class="count-badge">${cnt}</span>
        </div>
      `;
    }).join('');

    if (availableItems.length === 0) {
      pickerGridHtml = `<div style="grid-column:1/-1;text-align:center;padding:24px 8px;color:#777;font-size:12px;">Bảo Khố chưa có loại đá/ngọc này. Vượt ải hoặc hạ Boss để nhận thêm!</div>`;
    }

    return `
      <div class="socket-container">
        <!-- Cột 1: 10 Ô trang bị -->
        <div class="socket-slot-list">
          <div style="font-size:11px;font-weight:700;color:#aaa;padding:4px 6px;">VỊ TRÍ KHẢM (10 Ô)</div>
          ${slotListHtml}
        </div>

        <!-- Cột 2: Center Stage -->
        <div class="socket-stage">
          <div class="socket-main-card">
            <div class="socket-slot-icon r-${equippedItem?.rarity || 'common'}" style="width:48px;height:48px;">
              ${equippedItemId ? itemImg(equippedItemId) : ''}
            </div>
            <div class="details">
              <h3>${SLOT_NAMES[socketSlot] || socketSlot}: ${equippedItem ? equippedItem.name : '(Chưa mặc trang bị)'}</h3>
              <p>Khảm theo vị trí ô đồ. Khi thay đồ mới, toàn bộ đá và ngọc vẫn giữ nguyên vẹn 100%!</p>
            </div>
          </div>

          <div class="socket-orbs-wrap">
            ${orbsHtml}
          </div>

          ${actionCardHtml}
        </div>

        <!-- Cột 3: Bảo Khố Picker -->
        <div class="socket-vault-picker">
          <div class="picker-tabs">
            <button class="picker-tab ${pickerTab === 'all' ? 'active' : ''}" data-ptab="all">Tất cả</button>
            <button class="picker-tab ${pickerTab === 'gem' ? 'active' : ''}" data-ptab="gem">Linh Ngọc</button>
            <button class="picker-tab ${pickerTab === 'stone' ? 'active' : ''}" data-ptab="stone">Phù Thạch</button>
          </div>
          <div class="picker-grid">
            ${pickerGridHtml}
          </div>
        </div>
      </div>
    `;
  }

  // =========================================================================
  // 2. RENDER BẢO KHỐ (VAULT STORAGE VIEW)
  // =========================================================================
  function renderVaultStorageView(gs) {
    const vault = gs.vault || {};

    const allGems = DATA.items.filter(i => i.type === 'gem');
    const allStones = DATA.items.filter(i => i.type === 'stone');

    const totalStones = allStones.reduce((sum, it) => sum + (vault[it.id] || 0), 0);
    const totalGems = allGems.reduce((sum, it) => sum + (vault[it.id] || 0), 0);

    const renderCard = (it) => {
      const cnt = vault[it.id] || 0;
      return `
        <div class="vault-card ${cnt > 0 ? 'has-items' : ''}" data-tip="${itemTip(it.id)}">
          <img src="${it.icon}" alt="${it.name}">
          <div class="vault-card-info">
            <span class="name rc-${it.rarity}">${it.name}</span>
            <span class="desc">${it.special || it.desc}</span>
          </div>
          <span class="vault-card-count">×${cnt}</span>
        </div>
      `;
    };

    return `
      <div class="vault-storage-view">
        <div class="vault-storage-top">
          <div class="vault-storage-summary">
            <span>Tổng Phù Thạch: <b>${totalStones}</b> viên</span>
            <span>Tổng Linh Ngọc: <b>${totalGems}</b> viên</span>
          </div>
          <button class="btn-quick-fuse" id="btnQuickFuse">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
            Hợp Nhanh (1 Chạm)
          </button>
        </div>

        <!-- Ngăn 1: Linh Ngọc -->
        <div class="vault-tray">
          <h3 class="vault-tray-title">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#d4af37" stroke-width="2"><polygon points="12 2 22 8.5 18 22 6 22 2 8.5"/></svg>
            Ngăn Linh Ngọc (Gems) · Tăng mạnh chỉ số cơ bản lõi
          </h3>
          <div class="vault-tray-grid">
            ${allGems.map(renderCard).join('')}
          </div>
        </div>

        <!-- Ngăn 2: Phù Thạch -->
        <div class="vault-tray">
          <h3 class="vault-tray-title">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#1890ff" stroke-width="2"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="4"/></svg>
            Ngăn Phù Thạch (Stones) · Kích hoạt hiệu ứng đặc dị & đục lỗ
          </h3>
          <div class="vault-tray-grid">
            ${allStones.map(renderCard).join('')}
          </div>
        </div>
      </div>
    `;
  }

  // =========================================================================
  // 3. RENDER LÒ HỢP NHẤT (HORADRIC CUBE VIEW)
  // =========================================================================
  function renderCubeView(gs) {
    const recipes = gs.recipes || [];
    const vault = gs.vault || {};

    const filtered = recipes.filter(r => cubeCategory === 'all' || r.category === cubeCategory);

    const recipeCardsHtml = filtered.map(r => {
      const outDef = ITEM[r.resultItem];
      if (!outDef) return '';

      // Check ingredients
      let maxCraft = 999999;
      const ingListHtml = Object.entries(r.inputs).map(([ingId, need]) => {
        const have = (ITEM[ingId]?.type === 'herb' || ITEM[ingId]?.type === 'pill' ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
        const ok = have >= need;
        maxCraft = Math.min(maxCraft, Math.floor(have / need));
        const ingDef = ITEM[ingId];
        return `
          <span class="cube-ing ${ok ? 'ok' : 'miss'}" data-tip="${itemTip(ingId)}">
            <img src="${ingDef?.icon || ''}" alt="">
            ${ingDef?.name || ingId} ${have}/${need}
          </span>
        `;
      }).join('');

      if (r.stones > 0) {
        const haveStones = S.inventory?.linh_thach || 0;
        maxCraft = Math.min(maxCraft, Math.floor(haveStones / r.stones));
      }

      const canCraft = maxCraft > 0;

      return `
        <div class="cube-recipe-card ${canCraft ? 'can-craft' : ''}">
          <div class="cube-recipe-head">
            <img src="${outDef.icon}" alt="${outDef.name}" class="r-${outDef.rarity}">
            <div class="title-box">
              <h4 class="rc-${outDef.rarity}">${r.name}</h4>
              <p>${r.desc}</p>
            </div>
          </div>
          <div class="cube-ingredients">
            ${ingListHtml}
          </div>
          <div class="cube-fee">
            Phí linh thạch: <img src="img/stone/linh_thach.png" alt=""> <b>${fmt(r.stones)}</b>
          </div>
          <div class="cube-action-row">
            <button class="btn-cube-craft" data-craft-recipe="${r.id}" data-craft-times="1" ${canCraft ? '' : 'disabled'}>
              Hợp Thành
            </button>
            <button class="btn-cube-max" data-craft-recipe="${r.id}" data-craft-times="${maxCraft}" ${maxCraft > 1 ? '' : 'disabled'}>
              Tối đa ×${maxCraft > 1 ? maxCraft : 1}
            </button>
          </div>
        </div>
      `;
    }).join('');

    return `
      <div class="cube-view">
        <div class="cube-filter">
          <button class="cube-filter-btn ${cubeCategory === 'all' ? 'active' : ''}" data-cube-cat="all">Tất cả</button>
          <button class="cube-filter-btn ${cubeCategory === 'stone' ? 'active' : ''}" data-cube-cat="stone">Đá + Đá</button>
          <button class="cube-filter-btn ${cubeCategory === 'transcend' ? 'active' : ''}" data-cube-cat="transcend">Đá + Ngọc</button>
          <button class="cube-filter-btn ${cubeCategory === 'gem' ? 'active' : ''}" data-cube-cat="gem">Ngọc + Ngọc</button>
          <button class="cube-filter-btn ${cubeCategory === 'herb' ? 'active' : ''}" data-cube-cat="herb">Luyện Ngọc</button>
        </div>
        <div class="cube-grid">
          ${recipeCardsHtml}
        </div>
      </div>
    `;
  }

  // =========================================================================
  // =========================================================================
  // SỰ KIỆN TƯƠNG TÁC (EVENT LISTENERS)
  // =========================================================================
  let vaultEventsBound = false;
  function bindVaultEvents(pane) {
    if (vaultEventsBound) return;
    vaultEventsBound = true;
    pane.addEventListener('click', async (e) => {
      // 1. Chuyển sub-tab
      const tabBtn = e.target.closest('[data-vtab]');
      if (tabBtn) {
        vaultTab = tabBtn.dataset.vtab;
        selectedGemId = null;
        renderVault();
        return;
      }

      // 2. Chọn ô trang bị trong Khảm Nạm
      const slotBtn = e.target.closest('[data-select-slot]');
      if (slotBtn) {
        socketSlot = slotBtn.dataset.selectSlot;
        socketIdx = 0;
        selectedGemId = null;
        renderVault();
        return;
      }

      // 3. Chọn lỗ socket
      const orbBox = e.target.closest('[data-select-orb]');
      if (orbBox) {
        socketIdx = parseInt(orbBox.dataset.selectOrb, 10);
        selectedGemId = null;
        renderVault();
        return;
      }

      // 4. Lọc picker tab
      const ptabBtn = e.target.closest('[data-ptab]');
      if (ptabBtn) {
        pickerTab = ptabBtn.dataset.ptab;
        renderVault();
        return;
      }

      // 5. Chọn gem từ picker để khảm
      const pickItem = e.target.closest('[data-pick-gem]');
      if (pickItem) {
        selectedGemId = pickItem.dataset.pickGem;
        renderVault();
        return;
      }

      // 6. Hủy chọn gem
      if (e.target.closest('[data-cancel-pick]')) {
        selectedGemId = null;
        renderVault();
        return;
      }

      // 7. Thực hiện Đập Ngọc / Khảm Nạm
      const socketConfirmBtn = e.target.closest('[data-act-socket]');
      if (socketConfirmBtn) {
        const itemId = socketConfirmBtn.dataset.actSocket;
        if (!itemId || socketConfirmBtn.disabled) return;
        socketConfirmBtn.disabled = true;
        selectedGemId = null;
        try {
          await act(`gem/socket?slot=${socketSlot}&socketIndex=${socketIdx}&itemId=${itemId}`);
        } finally {
          socketConfirmBtn.disabled = false;
        }
        return;
      }

      // 8. Thực hiện Tháo Ngọc
      const unsocketBtn = e.target.closest('[data-act-unsocket]');
      if (unsocketBtn) {
        if (unsocketBtn.disabled) return;
        unsocketBtn.disabled = true;
        try {
          const slot = unsocketBtn.dataset.actUnsocket;
          const idx = parseInt(unsocketBtn.dataset.orbIdx, 10);
          await act(`gem/unsocket?slot=${slot}&socketIndex=${idx}`);
        } finally {
          unsocketBtn.disabled = false;
        }
        return;
      }

      // 9. Mở lỗ khảm bằng Đá Đục Lỗ
      const unlockBtn = e.target.closest('[data-act-unlock]');
      if (unlockBtn) {
        if (unlockBtn.disabled) return;
        unlockBtn.disabled = true;
        try {
          const slot = unlockBtn.dataset.actUnlock;
          await act(`gem/unlock?slot=${slot}`);
        } finally {
          unlockBtn.disabled = false;
        }
        return;
      }

      // 10. Hợp nhanh
      const quickFuseBtn = e.target.closest('#btnQuickFuse');
      if (quickFuseBtn) {
        if (quickFuseBtn.disabled) return;
        quickFuseBtn.disabled = true;
        try {
          await act('gem/quick-fuse');
        } finally {
          quickFuseBtn.disabled = false;
        }
        return;
      }

      // 11. Lọc danh mục lò hợp
      const cubeCatBtn = e.target.closest('[data-cube-cat]');
      if (cubeCatBtn) {
        cubeCategory = cubeCatBtn.dataset.cubeCat;
        renderVault();
        return;
      }

      // 12. Hợp chế công thức
      const craftRecipeBtn = e.target.closest('[data-craft-recipe]');
      if (craftRecipeBtn) {
        if (craftRecipeBtn.disabled) return;
        craftRecipeBtn.disabled = true;
        try {
          const recipeId = craftRecipeBtn.dataset.craftRecipe;
          const times = parseInt(craftRecipeBtn.dataset.craftTimes || '1', 10);
          await act(`gem/craft/${recipeId}?n=${times}`);
        } finally {
          craftRecipeBtn.disabled = false;
        }
        return;
      }
    });
  }

  window.renderVault = renderVault;
  window.openVaultSlot = function (slot) {
    if (slot && SLOT_NAMES[slot]) {
      socketSlot = slot;
      socketIdx = 0;
      selectedGemId = null;
    }
    vaultTab = 'socket';
    openTab('vault');
  };
})();
