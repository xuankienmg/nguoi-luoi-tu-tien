/* Vấn Đạo · js/vault.js: Kho Cổ Phù & Tiên Châu, Khảm Nạm & Lò Hợp */

(function () {
  let vaultTab = 'socket'; // 'socket' | 'vault' | 'cube'
  let socketSlot = 'weapon';
  let socketIdx = 0;
  let selectedGemId = null;
  let pickerTab = 'all'; // 'all' | 'gem' | 'stone'
  let cubeCategory = 'all'; // 'all' | 'stone' | 'transcend' | 'gem' | 'herb'
  let selectedRecipeId = null;
  let isCraftingAnim = false;

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
    return GEM_COLOR[id] || (ITEM[id]?.color || '#c9a24a');
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

    let html = `
      <div class="dd vt">
        <header class="dd-top">
          ${typeof lcNav === 'function' ? lcNav('vault') : ''}
          <div class="dd-cult"><span>Khảm nạm Cổ Phù & Tiên Châu · Lò Hợp Thần Khí</span></div>
          <div class="concept-wallet" title="Linh thạch hiện tại">
            <img src="img/stone/linh_thach.png" alt="">
            <b class="concept-balance">${fmt(S.inventory?.linh_thach || 0)}</b>
          </div>
        </header>

        <nav class="vt-nav" role="tablist">
          <button class="vt-tab-btn ${vaultTab === 'socket' ? 'active' : ''}" data-vtab="socket">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="4"/></svg>
            Khảm Nạm
          </button>
          <button class="vt-tab-btn ${vaultTab === 'vault' ? 'active' : ''}" data-vtab="vault">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 22 8.5 18 22 6 22 2 8.5"/></svg>
            Kho
          </button>
          <button class="vt-tab-btn ${vaultTab === 'cube' ? 'active' : ''}" data-vtab="cube">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
            Lò Hợp
          </button>
        </nav>

        <div class="vt-body">
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

    // Cột 1: Danh sách 10 ô trang bị (rộng 260px)
    const equipSlotsOrder = ['weapon', 'offhand', 'head', 'body', 'hands', 'legs', 'feet', 'necklace', 'charm', 'ring'];
    let slotListHtml = equipSlotsOrder.map(slotId => {
      const eqId = S.equipment?.[slotId];
      const eq = eqId ? ITEM[eqId] : null;
      const sMax = maxSockets[slotId] || 1;
      const sGems = slots[slotId] || [];
      const isActive = slotId === socketSlot;
      const hasGems = sGems.some(Boolean);

      let pipsHtml = '';
      for (let i = 0; i < sMax; i++) {
        const gemId = sGems[i];
        if (gemId && ITEM[gemId]) {
          pipsHtml += `<span class="p" style="background:${getGemColor(gemId)};box-shadow:0 0 4px ${getGemColor(gemId)}"></span>`;
        } else {
          pipsHtml += `<span class="p" style="background:#332b24"></span>`;
        }
      }

      const tipText = eqId
        ? (typeof itemTip === 'function' ? itemTip(eqId, 'Nhấp chuột phải: tháo nhanh Cổ Phù / Tiên Châu', slotId) : eq.name)
        : `${SLOT_NAMES[slotId] || slotId} (Chưa trang bị)`;

      return `
        <button class="socket-slot-btn ${isActive ? 'active' : ''}" data-select-slot="${slotId}"
          ${eqId ? `data-card="slot" data-k="${eqId}" data-slot="${slotId}"` : ''}
          data-tip="${tipText}" data-has-gems="${hasGems ? '1' : '0'}">
          <div class="socket-slot-icon r-${eq?.rarity || 'common'}">
            ${eqId ? itemImg(eqId) : `<span style="font-size:10px;color:#777">${SLOT_NAMES[slotId]?.slice(0, 2)}</span>`}
          </div>
          <div class="socket-slot-info">
            <span class="title">${SLOT_NAMES[slotId] || slotId}</span>
            <span class="item-name">${eq ? eq.name : '(Trống)'}</span>
            <div class="socket-slot-pips">${pipsHtml}</div>
          </div>
        </button>
      `;
    }).join('');

    // Cột 2: Sockets & Action (Center Stage)
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
        inner = `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#555" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>`;
      } else if (gemDef) {
        inner = `<img src="${gemDef.icon}" alt="${gemDef.name}" style="color:${getGemColor(gemId)}">`;
      } else {
        inner = `<span style="font-size:22px;color:rgba(184,146,90,0.5)">+</span>`;
      }

      const orbTip = gemDef
        ? (typeof itemTip === 'function' ? itemTip(gemId, 'Nhấp chuột phải: tháo Cổ Phù / Tiên Châu này về Kho') : gemDef.name)
        : isLocked ? 'Lỗ khảm đang khóa' : 'Lỗ khảm đang trống';

      orbsHtml += `
        <div class="socket-orb-box">
          <div class="${orbClass}" data-select-orb="${i}" data-orb-idx="${i}" data-slot="${socketSlot}"
            data-tip="${orbTip}"
            style="${gemDef ? `border-color:${getGemColor(gemId)};box-shadow:0 0 10px ${getGemColor(gemId)}44` : ''}">
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
          <p>Mở tự động khi thăng cấp cảnh giới (Hóa Thần mở đủ 3 lỗ) hoặc dùng Khai Khiếu Cổ Phù để khai mở sớm.</p>
          <div style="margin-top:10px;">
            <button class="btn-unlock-socket" data-act-unlock="${socketSlot}" ${stoneSocketCount > 0 ? '' : 'disabled'}>
              Dùng Khai Khiếu Cổ Phù (${stoneSocketCount}/1)
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
            <img src="${pickDef.icon}" style="width:34px;height:34px;">
            <b class="rc-${pickDef.rarity}">${pickDef.name}</b>
            <span style="font-size:12px;color:#a08c66">(Còn ×${hasCount})</span>
          </div>
          <p>${pickDef.desc}</p>
          <div class="socket-preview-stats">
            Hiệu quả khảm: <b>${previewText}</b>
          </div>
          <div class="socket-btn-row">
            <button class="btn-socket-do" data-act-socket="${selectedGemId}" ${hasCount > 0 ? '' : 'disabled'}>
              Khảm Nạm
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
            <img src="${curSelectedGemDef.icon}" style="width:34px;height:34px;">
            <b class="rc-${curSelectedGemDef.rarity}">${curSelectedGemDef.name}</b>
          </div>
          <p>${curSelectedGemDef.desc}</p>
          <div class="socket-preview-stats">
            Đang kích hoạt: <b>${curSelectedGemDef.special || 'Gia tăng sức mạnh'}</b>
          </div>
          <div class="socket-btn-row">
            <button class="btn-unsocket" data-act-unsocket="${socketSlot}" data-orb-idx="${socketIdx}">
              Tháo Khảm (Hoàn 100% về Kho)
            </button>
          </div>
        </div>
      `;
    } else {
      // Empty socket
      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Lỗ ${socketIdx + 1} đang trống</h4>
          <p>Hãy chọn một Cổ Phù hoặc Tiên Châu từ bảng bên phải để khảm vào trang bị.</p>
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
        <div class="picker-item ${isPick ? 'selected' : ''}" data-pick-gem="${id}" data-tip="${typeof itemTip === 'function' ? itemTip(id, 'Nhấp để chọn khảm') : it.name}">
          <img src="${it.icon}" alt="${it.name}">
          <span class="count-badge">${cnt}</span>
        </div>
      `;
    }).join('');

    if (availableItems.length === 0) {
      pickerGridHtml = `<div style="grid-column:1/-1;text-align:center;padding:24px 8px;color:#777;font-size:11.5px;">Kho chưa có Cổ Phù hoặc Tiên Châu loại này. Vượt ải hoặc hạ Boss để nhận thêm!</div>`;
    }

    const mainCardTip = equippedItemId
      ? (typeof itemTip === 'function' ? itemTip(equippedItemId, '', socketSlot) : equippedItem?.name)
      : '';

    return `
      <div class="socket-container">
        <!-- Cột 1: 10 Ô trang bị -->
        <div class="socket-slot-list">
          <div class="vt-sec-hdr">VỊ TRÍ KHẢM (10 Ô)</div>
          ${slotListHtml}
        </div>

        <!-- Cột 2: Center Stage -->
        <div class="socket-stage">
          <div class="socket-main-card"
            ${equippedItemId ? `data-card="slot" data-k="${equippedItemId}" data-slot="${socketSlot}" data-tip="${mainCardTip}"` : ''}>
            <div class="socket-slot-icon r-${equippedItem?.rarity || 'common'}" style="width:44px;height:44px;">
              ${equippedItemId ? itemImg(equippedItemId) : ''}
            </div>
            <div class="details">
              <h3>${SLOT_NAMES[socketSlot] || socketSlot}: ${equippedItem ? equippedItem.name : '(Chưa trang bị)'}</h3>
              <p>Khảm theo vị trí ô đồ. Khi thay đồ mới, Cổ Phù và Tiên Châu vẫn giữ nguyên vẹn 100%!</p>
            </div>
          </div>

          <div class="socket-orbs-wrap">
            ${orbsHtml}
          </div>

          ${actionCardHtml}
        </div>

        <!-- Cột 3: Kho Picker -->
        <div class="socket-vault-picker">
          <div class="picker-tabs">
            <button class="picker-tab ${pickerTab === 'all' ? 'active' : ''}" data-ptab="all">Tất cả</button>
            <button class="picker-tab ${pickerTab === 'gem' ? 'active' : ''}" data-ptab="gem">Tiên Châu</button>
            <button class="picker-tab ${pickerTab === 'stone' ? 'active' : ''}" data-ptab="stone">Cổ Phù</button>
          </div>
          <div class="picker-grid">
            ${pickerGridHtml}
          </div>
        </div>
      </div>
    `;
  }

  // =========================================================================
  // 2. RENDER KHO CỔ PHÙ & TIÊN CHÂU (VAULT STORAGE VIEW)
  // =========================================================================
  function renderVaultStorageView(gs) {
    const vault = gs.vault || {};

    const allGems = (DATA.items || []).filter(i => i.type === 'gem');
    const allStones = (DATA.items || []).filter(i => i.type === 'stone');

    const totalStones = allStones.reduce((sum, it) => sum + (vault[it.id] || 0), 0);
    const totalGems = allGems.reduce((sum, it) => sum + (vault[it.id] || 0), 0);

    const renderCard = (it) => {
      const cnt = vault[it.id] || 0;
      return `
        <div class="vault-card ${cnt > 0 ? 'has-items' : ''}" data-tip="${typeof itemTip === 'function' ? itemTip(it.id) : it.name}">
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
            <span>Tổng Cổ Phù: <b>${totalStones}</b> viên</span>
            <span>Tổng Tiên Châu: <b>${totalGems}</b> viên</span>
          </div>
          <button class="btn-quick-fuse" id="btnQuickFuse">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
            Hợp Nhanh (1 Chạm)
          </button>
        </div>

        <!-- Ngăn 1: Tiên Châu -->
        <div class="vault-tray">
          <h3 class="vault-tray-title">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#c9a24a" stroke-width="2"><polygon points="12 2 22 8.5 18 22 6 22 2 8.5"/></svg>
            Ngăn Tiên Châu · Tăng mạnh chỉ số cơ bản lõi
          </h3>
          <div class="vault-tray-grid">
            ${allGems.map(renderCard).join('')}
          </div>
        </div>

        <!-- Ngăn 2: Cổ Phù -->
        <div class="vault-tray">
          <h3 class="vault-tray-title">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#1890ff" stroke-width="2"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="4"/></svg>
            Ngăn Cổ Phù · Kích hoạt hiệu ứng đặc dị & khai khiếu
          </h3>
          <div class="vault-tray-grid">
            ${allStones.map(renderCard).join('')}
          </div>
        </div>
      </div>
    `;
  }

  // =========================================================================
  // 3. RENDER LÒ HỢP CỔ PHÙ & TIÊN CHÂU (HORADRIC CUBE VIEW)
  // =========================================================================
  function renderCubeView(gs) {
    const recipes = gs.recipes || [];
    const vault = gs.vault || {};

    const filtered = recipes.filter(r => cubeCategory === 'all' || r.category === cubeCategory);

    // Active recipe selection
    let curRecipe = recipes.find(r => r.id === selectedRecipeId);
    if (!curRecipe && filtered.length > 0) {
      curRecipe = filtered[0];
      selectedRecipeId = curRecipe.id;
    }

    // 1. Cột Trái: Danh sách vật phẩm đang có
    const ownedVault = Object.entries(vault)
      .filter(([id, cnt]) => cnt > 0 && ITEM[id])
      .filter(([id]) => {
        if (cubeCategory === 'stone') return ITEM[id].type === 'stone';
        if (cubeCategory === 'gem') return ITEM[id].type === 'gem';
        if (cubeCategory === 'transcend') return true;
        if (cubeCategory === 'herb') return false;
        return true;
      });

    const herbIds = ['huyet_can_thao', 'tu_linh_thao', 'thien_nien_sam', 'bich_ngoc_linh_thao', 'tu_van_sam'];
    const ownedHerbs = herbIds
      .filter(id => (S.inventory?.[id] || 0) > 0 && ITEM[id])
      .map(id => [id, S.inventory[id]]);

    const allOwned = (cubeCategory === 'herb') ? ownedHerbs
      : (cubeCategory === 'all') ? ownedVault.concat(ownedHerbs)
      : ownedVault;

    let ownedItemsHtml = allOwned.map(([id, cnt]) => {
      const it = ITEM[id];
      const isUsedInCur = curRecipe && !!curRecipe.inputs?.[id];
      return `
        <div class="cube-owned-item ${isUsedInCur ? 'selected' : ''}" data-cube-pick-item="${id}"
          data-tip="${typeof itemTip === 'function' ? itemTip(id, 'Nhấp để đặt vào Lò Hợp') : it.name}">
          <img src="${it.icon}" alt="${it.name}">
          <span class="qty">×${cnt}</span>
        </div>
      `;
    }).join('');

    if (allOwned.length === 0) {
      ownedItemsHtml = `<div style="grid-column:1/-1;text-align:center;padding:12px;color:#777;font-size:11px;">Chưa có vật phẩm loại này trong Kho.</div>`;
    }

    // Danh sách công thức hợp thành gợi ý
    let recipeListHtml = filtered.map(r => {
      const outDef = ITEM[r.resultItem];
      if (!outDef) return '';

      // Check if player has materials
      let canCraft = true;
      for (const [ingId, need] of Object.entries(r.inputs || {})) {
        const have = (ITEM[ingId]?.type === 'herb' ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
        if (have < need) { canCraft = false; break; }
      }
      if (r.stones > 0 && (S.inventory?.linh_thach || 0) < r.stones) {
        canCraft = false;
      }

      const isCur = r.id === curRecipe?.id;
      const chancePct = Math.round((r.chance ?? 1.0) * 100);

      const inputDesc = Object.entries(r.inputs || {})
        .map(([ingId, need]) => `${need} ${ITEM[ingId]?.name || ingId}`)
        .join(' + ');

      return `
        <div class="cube-recipe-item ${isCur ? 'active' : ''} ${canCraft ? 'can-craft' : ''}" data-select-recipe="${r.id}">
          <img src="${outDef.icon}" alt="${outDef.name}" class="r-${outDef.rarity}">
          <div class="info">
            <span class="nm rc-${outDef.rarity}">${r.name}</span>
            <span class="req">${inputDesc}</span>
          </div>
          <span class="badge">${canCraft ? 'Đủ' : 'Thiếu'} · ${chancePct}%</span>
        </div>
      `;
    }).join('');

    // 2. Cột Phải: Lò Hợp Nhất (Interactive Furnace Stage)
    let inputSlotsHtml = '';
    let maxCraft = 0;
    let canCraftNow = false;

    if (curRecipe) {
      maxCraft = 999999;
      inputSlotsHtml = Object.entries(curRecipe.inputs || {}).map(([ingId, need]) => {
        const ingDef = ITEM[ingId];
        const have = (ingDef?.type === 'herb' ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
        const ok = have >= need;
        maxCraft = Math.min(maxCraft, Math.floor(have / need));

        return `
          <div class="cube-slot">
            <div class="cube-slot-box ${ok ? 'ok' : 'miss'}" data-tip="${typeof itemTip === 'function' ? itemTip(ingId) : ingDef?.name}">
              <img src="${ingDef?.icon || ''}" alt="">
              <span class="badge">${have}/${need}</span>
            </div>
            <span class="cube-slot-name">${ingDef?.name || ingId}</span>
          </div>
        `;
      }).join('');

      if (curRecipe.stones > 0) {
        const haveStones = S.inventory?.linh_thach || 0;
        maxCraft = Math.min(maxCraft, Math.floor(haveStones / curRecipe.stones));
      }

      canCraftNow = maxCraft > 0;
    }

    const outDef = curRecipe ? ITEM[curRecipe.resultItem] : null;
    const chancePct = curRecipe ? Math.round((curRecipe.chance ?? 1.0) * 100) : 100;
    const chanceClass = chancePct >= 90 ? 'gold' : chancePct >= 75 ? 'high' : 'mid';

    return `
      <div class="cube-container">
        <!-- Cột Trái: Danh sách Cổ Phù & Tiên Châu đang có + Phím tắt Công Thức -->
        <section class="cube-left-panel">
          <div class="cube-left-tabs">
            <button class="cube-filter-btn ${cubeCategory === 'all' ? 'active' : ''}" data-cube-cat="all">Tất cả</button>
            <button class="cube-filter-btn ${cubeCategory === 'stone' ? 'active' : ''}" data-cube-cat="stone">Cổ Phù</button>
            <button class="cube-filter-btn ${cubeCategory === 'gem' ? 'active' : ''}" data-cube-cat="gem">Tiên Châu</button>
            <button class="cube-filter-btn ${cubeCategory === 'transcend' ? 'active' : ''}" data-cube-cat="transcend">Thăng Hoa</button>
            <button class="cube-filter-btn ${cubeCategory === 'herb' ? 'active' : ''}" data-cube-cat="herb">Linh Thảo</button>
          </div>

          <!-- Danh sách vật phẩm đang sở hữu -->
          <div class="cube-owned-section">
            <div class="vt-sec-hdr">KHO CỔ PHÙ & TIÊN CHÂU (${allOwned.length})</div>
            <div class="cube-owned-grid">
              ${ownedItemsHtml}
            </div>
          </div>

          <!-- Danh sách công thức hợp thành gợi ý -->
          <div class="cube-recipes-section">
            <div class="vt-sec-hdr">CÔNG THỨC HỢP CHẾ (${filtered.length})</div>
            <div class="cube-recipe-list">
              ${recipeListHtml}
            </div>
          </div>
        </section>

        <!-- Cột Phải: Lò Hợp Nhất (Interactive Furnace) -->
        <aside class="cube-forge-panel">
          <div class="cube-forge-header">
            <h3>LÒ HỢP CỔ PHÙ & TIÊN CHÂU</h3>
            <p>Đặt Cổ Phù hoặc Tiên Châu vào lò để dung hợp thăng cấp hoặc biến dị</p>
          </div>

          <!-- Slots lò hợp -->
          <div class="cube-forge-stage ${isCraftingAnim ? 'forging' : ''}" id="cubeForgeStage">
            <div class="cube-input-slots">
              ${inputSlotsHtml || '<span style="color:#777;font-size:12px;">Chưa chọn công thức</span>'}
            </div>

            <div class="cube-forge-arrow">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12l7 7 7-7"/></svg>
            </div>

            <!-- Output Slot -->
            <div class="cube-output-box">
              <div class="cube-out-slot r-${outDef?.rarity || 'common'}" id="cubeOutSlot"
                ${outDef ? `data-card="slot" data-k="${outDef.id}" data-tip="${typeof itemTip === 'function' ? itemTip(outDef.id) : outDef.name}"` : ''}>
                ${outDef ? `<img src="${outDef.icon}" alt="">` : '<span class="empty-q">?</span>'}
              </div>
              <div class="cube-out-info">
                <h4 class="rc-${outDef?.rarity || 'common'}">${curRecipe ? curRecipe.name : 'Chọn công thức'}</h4>
                <span class="cube-out-desc">${curRecipe ? curRecipe.desc : 'Chọn nguyên liệu từ cột bên trái để bắt đầu'}</span>
              </div>
            </div>
          </div>

          <!-- Tỉ lệ thành công & Quy tắc -->
          <div class="cube-rate-box">
            <div class="cube-chance">
              <span>Tỉ lệ thành công:</span>
              <b class="${chanceClass}">${chancePct}%</b>
            </div>
            <ul class="cube-rules">
              <li class="ok">✓ Thành công: Nhận 1 ${outDef?.name || 'vật phẩm phẩm cao hơn'}</li>
              <li class="${chancePct < 100 ? 'bad' : 'ok'}">
                ${chancePct === 100 ? '✓ Tỉ lệ 100%: Chắc chắn thành công, không có rủi ro thất bại.' : '⚠ Thất bại: Tiêu hao linh thạch, bảo lưu 1 phần nguyên liệu về Kho.'}
              </li>
            </ul>
            <div class="cube-fee-row">
              <span>Chi phí:</span>
              <img src="img/stone/linh_thach.png" alt="">
              <b>${fmt(curRecipe?.stones || 0)}</b> linh thạch
            </div>
          </div>

          <!-- Nút hành động -->
          <div class="cube-actions">
            <button class="btn-cube-auto" data-cube-act="auto" ${canCraftNow ? '' : 'disabled'} data-tip="Tự động điền đủ nguyên liệu cho công thức này">
              <svg viewBox="0 0 16 16" width="14" height="14" fill="currentColor"><rect x="1" y="1" width="4" height="4"/><rect x="6" y="1" width="4" height="4"/><rect x="11" y="1" width="4" height="4"/><rect x="1" y="6" width="4" height="4"/><rect x="6" y="6" width="4" height="4"/><rect x="11" y="6" width="4" height="4"/><rect x="1" y="11" width="4" height="4"/><rect x="6" y="11" width="4" height="4"/><rect x="11" y="11" width="4" height="4"/></svg>
              Tự động điền
            </button>
            <button class="btn-cube-go dd-synth-btn" data-cube-act="go" ${canCraftNow && !busy ? '' : 'disabled'}>
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
              Hợp Thành
            </button>
            <button class="btn-cube-max-times" data-cube-act="max" ${maxCraft > 1 && !busy ? '' : 'disabled'}>
              Tối đa ×${maxCraft > 1 ? maxCraft : 1}
            </button>
          </div>
        </aside>
      </div>
    `;
  }

  // =========================================================================
  // BINDING SỰ KIỆN TƯƠNG TÁC
  // =========================================================================
  function bindVaultEvents(pane) {
    if (!pane) return;

    // A. CHUỘT PHẢI (CONTEXTMENU): Tháo Cổ Phù / Tiên Châu nhanh
    pane.addEventListener('contextmenu', async e => {
      // 1. Chuột phải vào ô Lỗ Khảm ở giữa
      const filledOrb = e.target.closest('.socket-orb.filled');
      if (filledOrb) {
        e.preventDefault();
        const slot = socketSlot;
        const idx = parseInt(filledOrb.dataset.orbIdx, 10);
        if (!isNaN(idx)) {
          if (typeof hideTip === 'function') hideTip();
          await act(`gem/unsocket?slot=${slot}&socketIndex=${idx}`);
          renderVault();
        }
        return;
      }

      // 2. Chuột phải vào ô trang bị ở cột trái (tháo nhanh viên cuối cùng)
      const slotBtn = e.target.closest('.socket-slot-btn');
      if (slotBtn) {
        e.preventDefault();
        const slot = slotBtn.dataset.selectSlot;
        const gs = getVaultState();
        const curGems = (gs.slots?.[slot] || []).filter(Boolean);
        if (curGems.length > 0) {
          const slotsArr = gs.slots?.[slot] || [];
          let lastIdx = -1;
          for (let i = slotsArr.length - 1; i >= 0; i--) {
            if (slotsArr[i]) { lastIdx = i; break; }
          }
          if (lastIdx >= 0) {
            if (typeof hideTip === 'function') hideTip();
            await act(`gem/unsocket?slot=${slot}&socketIndex=${lastIdx}`);
            renderVault();
          }
        }
        return;
      }
    });

    // B. CHUỘT TRÁI (CLICK)
    pane.addEventListener('click', async e => {
      // 1. Chuyển tab phụ (Khảm Nạm / Kho / Lò Hợp)
      const vtabBtn = e.target.closest('[data-vtab]');
      if (vtabBtn) {
        vaultTab = vtabBtn.dataset.vtab;
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

      // 3. Chọn lỗ khảm
      const orbBtn = e.target.closest('[data-select-orb]');
      if (orbBtn) {
        const idx = parseInt(orbBtn.dataset.selectOrb, 10);
        socketIdx = idx;
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

      // 7. Thực hiện Khảm Nạm
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

      // 8. Thực hiện Tháo Ngọc qua nút
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

      // 9. Mở lỗ khảm bằng Khai Khiếu Cổ Phù
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
        const gs = getVaultState();
        const filtered = (gs.recipes || []).filter(r => cubeCategory === 'all' || r.category === cubeCategory);
        if (filtered.length > 0 && !filtered.some(r => r.id === selectedRecipeId)) {
          selectedRecipeId = filtered[0].id;
        }
        renderVault();
        return;
      }

      // 12. Chọn công thức trong lò hợp
      const recipeItemBtn = e.target.closest('[data-select-recipe]');
      if (recipeItemBtn) {
        selectedRecipeId = recipeItemBtn.dataset.selectRecipe;
        renderVault();
        return;
      }

      // 13. Chọn vật phẩm từ kho bên trái Lò Hợp -> Tự tìm công thức phù hợp
      const cubePickItemBtn = e.target.closest('[data-cube-pick-item]');
      if (cubePickItemBtn) {
        const pickedId = cubePickItemBtn.dataset.cubePickItem;
        const gs = getVaultState();
        const recipes = gs.recipes || [];
        const match = recipes.find(r => r.inputs?.[pickedId] && (cubeCategory === 'all' || r.category === cubeCategory))
          || recipes.find(r => r.inputs?.[pickedId]);
        if (match) {
          selectedRecipeId = match.id;
          if (cubeCategory !== 'all' && match.category !== cubeCategory) {
            cubeCategory = match.category;
          }
          renderVault();
        }
        return;
      }

      // 14. Nút [Tự động điền] trong Lò Hợp
      if (e.target.closest('[data-cube-act="auto"]')) {
        // Tự động kiểm tra và chuẩn bị công thức hiện tại
        const stage = $('cubeForgeStage');
        if (stage) {
          stage.classList.remove('forging');
          void stage.offsetWidth;
          stage.classList.add('forging');
          setTimeout(() => stage.classList.remove('forging'), 600);
        }
        return;
      }

      // 15. Nút [Hợp Thành] hoặc [Tối đa ×N] trong Lò Hợp
      const cubeActGo = e.target.closest('[data-cube-act="go"]');
      const cubeActMax = e.target.closest('[data-cube-act="max"]');
      if (cubeActGo || cubeActMax) {
        const btn = cubeActGo || cubeActMax;
        if (btn.disabled || busy || !selectedRecipeId) return;

        const gs = getVaultState();
        const recipe = (gs.recipes || []).find(r => r.id === selectedRecipeId);
        if (!recipe) return;

        let times = 1;
        if (cubeActMax) {
          const vault = gs.vault || {};
          let maxT = 999999;
          for (const [ingId, need] of Object.entries(recipe.inputs || {})) {
            const have = (ITEM[ingId]?.type === 'herb' ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
            maxT = Math.min(maxT, Math.floor(have / need));
          }
          if (recipe.stones > 0) {
            maxT = Math.min(maxT, Math.floor((S.inventory?.linh_thach || 0) / recipe.stones));
          }
          times = Math.max(1, maxT);
        }

        btn.disabled = true;
        const stage = $('cubeForgeStage');
        const outSlot = $('cubeOutSlot');
        if (stage) stage.classList.add('forging');

        try {
          const ok = await act(`gem/craft/${selectedRecipeId}?n=${times}`);
          if (stage) stage.classList.remove('forging');

          if (ok && outSlot) {
            outSlot.classList.remove('success-burst');
            void outSlot.offsetWidth;
            outSlot.classList.add('success-burst');
          } else if (!ok && stage) {
            stage.classList.remove('fail-shake');
            void stage.offsetWidth;
            stage.classList.add('fail-shake');
          }
        } finally {
          btn.disabled = false;
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
