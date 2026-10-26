/* Vấn Đạo · js/vault.js: Kho Cổ Phù & Tiên Châu, Khảm Nạm & Lò Hợp */

(function () {
  let vaultTab = 'socket'; // 'socket' | 'vault' | 'cube'
  let socketSlot = 'weapon';
  let socketIdx = 0;
  let selectedGemId = null;
  let pickerTab = 'all'; // 'all' | 'gem' | 'stone'
  let cubeCategory = 'all'; // 'all' | 'stone' | 'transcend' | 'gem' | 'herb'
  let selectedRecipeId = null;
  let cubeSel = []; // 5 ô lò hợp tương tác
  let isCraftingAnim = false;
  let lastTap = { key: '', t: 0 };

  const FUSE_GRID_SVG = '<svg viewBox="0 0 16 16"><rect x="1" y="1" width="4" height="4"/><rect x="6" y="1" width="4" height="4"/><rect x="11" y="1" width="4" height="4"/><rect x="1" y="6" width="4" height="4"/><rect x="6" y="6" width="4" height="4"/><rect x="11" y="6" width="4" height="4"/><rect x="1" y="11" width="4" height="4"/><rect x="6" y="11" width="4" height="4"/><rect x="11" y="11" width="4" height="4"/></svg>';
  const FUSE_GO_SVG = '<svg viewBox="0 0 24 24"><path d="M4 12h12M12 6l6 6-6 6"/><path d="M20 5v14"/></svg>';
  const CLEAR_SVG = '<svg viewBox="0 0 24 24"><path d="M18 6L6 18M6 6l12 12"/></svg>';

  const SO_STONES = new Set(['stone_viem_so', 'stone_bang_so', 'stone_loi_so', 'stone_kim_so', 'stone_tho_so']);
  const CAO_STONES = new Set(['stone_viem_cao', 'stone_bang_cao', 'stone_loi_cao', 'stone_kim_cao', 'stone_tho_cao']);
  const DIVINE_STONES = new Set(['stone_liet_loi', 'stone_huyen_bang', 'stone_am_duong']);
  const CUI_GEMS = new Set(['gem_ruby_cui', 'gem_sapphire_cui', 'gem_topaz_cui', 'gem_emerald_cui', 'gem_amethyst_cui']);
  const XIN_GEMS = new Set(['gem_ruby_xin', 'gem_sapphire_xin', 'gem_topaz_xin', 'gem_emerald_xin', 'gem_amethyst_xin']);

  const SO_TO_CAO = {
    stone_viem_so: 'stone_viem_cao',
    stone_bang_so: 'stone_bang_cao',
    stone_loi_so: 'stone_loi_cao',
    stone_kim_so: 'stone_kim_cao',
    stone_tho_so: 'stone_tho_cao'
  };

  const CUI_TO_XIN = {
    gem_ruby_cui: 'gem_ruby_xin',
    gem_sapphire_cui: 'gem_sapphire_xin',
    gem_topaz_cui: 'gem_topaz_xin',
    gem_emerald_cui: 'gem_emerald_xin',
    gem_amethyst_cui: 'gem_amethyst_xin'
  };

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
    stone_am_duong: '#eb2f96', stone_socket: '#c9a24a'
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
          <div class="dd-cult"><span>Chú Linh Cổ Phù & Tiên Châu · Lò Hợp Thần Khí</span></div>
          <div class="dd-stone" data-tip="Linh thạch hiện tại"><img src="img/stone/linh_thach.png" alt=""><span>Linh thạch</span><b>${fmt(S.inventory?.linh_thach || 0)}</b></div>
        </header>

        <section class="dd-main vt-main">
          <nav class="vt-nav" role="tablist" aria-label="Chú linh">
            <button class="vt-tab-btn ${vaultTab === 'socket' ? 'on active' : ''}" data-vtab="socket">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="4"/></svg>
              Vị Trí Chú Linh
            </button>
            <button class="vt-tab-btn ${vaultTab === 'vault' ? 'on active' : ''}" data-vtab="vault">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 22 8.5 18 22 6 22 2 8.5"/></svg>
              Kho
            </button>
            <button class="vt-tab-btn ${vaultTab === 'cube' ? 'on active' : ''}" data-vtab="cube">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/></svg>
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
        </section>
      </div>
    `;

    pane.innerHTML = html;
    bindVaultEvents(pane);
  }

  // =========================================================================
  // 1. RENDER CHÚ LINH (SOCKET VIEW)
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
      const sMax = maxSockets[slotId] || (S.realm >= 4 ? 3 : S.realm >= 2 ? 2 : 1);
      const sGems = slots[slotId] || [];
      const isActive = slotId === socketSlot;
      const hasGems = sGems.some(Boolean);

      let pipsHtml = '';
      for (let i = 0; i < sMax; i++) {
        const gemId = sGems[i];
        if (gemId && ITEM[gemId]) {
          pipsHtml += `<span class="p" style="background:${getGemColor(gemId)};box-shadow:0 0 5px ${getGemColor(gemId)}"></span>`;
        } else {
          pipsHtml += `<span class="p" style="background:#332b24"></span>`;
        }
      }

      const tipText = eqId
        ? (typeof itemTip === 'function' ? itemTip(eqId, 'Nhấp chuột phải: tháo nhanh Cổ Phù / Tiên Châu', slotId) : eq.name)
        : `${SLOT_NAMES[slotId] || slotId} (Chưa trang bị)`;

      return `
        <button class="socket-slot-btn ${isActive ? 'active' : ''}" data-select-slot="${slotId}"
          ${eqId ? `data-card="vault-slot" data-k="${eqId}" data-slot="${slotId}"` : ''}
          data-tip="${eqId ? '1' : tipText}" data-has-gems="${hasGems ? '1' : '0'}">
          <div class="socket-slot-icon r-${eq?.rarity || 'common'}">
            ${eqId ? itemImg(eqId) : `<span style="font-size:10px;color:#777">${SLOT_NAMES[slotId]?.slice(0, 2)}</span>`}
          </div>
          <div class="socket-slot-info">
            <span class="title">${SLOT_NAMES[slotId] || slotId}</span>
            <span class="item-name ${eq ? `rc-${eq.rarity}` : ''}">${eq ? eq.name : '(Trống)'}</span>
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

      let orbBoxClass = 'socket-orb-box' + (isSelected ? ' selected' : '') + (gemDef ? ' filled' : '') + (isLocked ? ' locked' : '');
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
        inner = `<span style="font-size:24px;color:rgba(184,146,90,0.6)">+</span>`;
      }

      const orbTip = gemDef
        ? '1'
        : isLocked ? 'Lỗ chú linh đang khóa\nMở khi thăng cấp cảnh giới hoặc dùng Khai Khiếu Cổ Phù.' : 'Lỗ chú linh đang trống\nChọn Cổ Phù hoặc Tiên Châu bên phải để chú linh vào lỗ này.';

      orbsHtml += `
        <div class="${orbBoxClass}" data-select-orb="${i}" data-orb-idx="${i}" style="cursor:pointer;">
          <div class="${orbClass}" data-select-orb="${i}" data-orb-idx="${i}"
            ${gemDef ? `data-card="gem-orb" data-k="${gemId}"` : ''}
            data-tip="${orbTip}"
            style="${gemDef ? `--gem-c:${getGemColor(gemId)};` : ''}">
            ${inner}
          </div>
          <span class="socket-orb-title">Lỗ ${i + 1}</span>
          <span class="orb-label ${gemDef ? `rc-${gemDef.rarity}` : ''}">${isLocked ? 'Khóa' : gemDef ? gemDef.name : 'Trống'}</span>
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
          <h4>Lỗ chú linh số ${socketIdx + 1} đang bị khóa</h4>
          <p>Mở tự động khi thăng cấp cảnh giới (Hóa Thần mở đủ 3 lỗ) hoặc dùng Khai Khiếu Cổ Phù để khai mở sớm.</p>
          <div style="margin-top:10px;">
            <button class="btn-unlock-socket" data-act-unlock="${socketSlot}" ${stoneSocketCount > 0 ? '' : 'disabled'}>
              Dùng Khai Khiếu Cổ Phù (${stoneSocketCount}/1)
            </button>
          </div>
        </div>
      `;
    } else if (curSelectedGemDef) {
      // Currently has a gem
      actionCardHtml = `
        <div class="socket-action-card filled-active">
          <h4>Lỗ ${socketIdx + 1}: <span class="rc-${curSelectedGemDef.rarity}">${curSelectedGemDef.name}</span></h4>
          <div style="display:flex;align-items:center;justify-content:center;gap:8px;margin:8px 0;">
            <div class="selected-gem-preview" data-card="gem-orb" data-k="${curGems[socketIdx]}" data-orb-idx="${socketIdx}" data-tip="1" style="cursor:pointer;">
              <img src="${curSelectedGemDef.icon}" style="width:36px;height:36px;">
            </div>
            <b class="rc-${curSelectedGemDef.rarity}">${curSelectedGemDef.name}</b>
          </div>
          <p>${curSelectedGemDef.desc}</p>
          <div class="socket-preview-stats">
            Đang kích hoạt: <b>${curSelectedGemDef.special || 'Gia tăng sức mạnh'}</b>
          </div>
        </div>
      `;
    } else {
      // Empty socket
      actionCardHtml = `
        <div class="socket-action-card">
          <h4>Lỗ ${socketIdx + 1} đang trống</h4>
          <p>Nhấp đúp chuột trái vào Cổ Phù hoặc Tiên Châu bên phải để chú linh vào lỗ này.</p>
        </div>
      `;
    }

    // Tổng hợp chỉ số Chú Linh của trang bị này
    let totalAtk = 0, totalDef = 0, totalHp = 0, totalCrit = 0, totalSpeed = 0;
    const summarySlotRows = [];
    let totalFilled = 0;

    for (let i = 0; i < curMax; i++) {
      const gId = curGems[i];
      const gDef = gId ? ITEM[gId] : null;
      if (gDef) {
        totalFilled++;
        if (gDef.atk) totalAtk += gDef.atk;
        if (gDef.def) totalDef += gDef.def;
        if (gDef.hp) totalHp += gDef.hp;
        if (gDef.critPct) totalCrit += gDef.critPct;
        if (gDef.speedPct) totalSpeed += gDef.speedPct;
        const rawStat = gDef.special || (gDef.atk ? `+${fmt(gDef.atk)} Công` : gDef.def ? `+${fmt(gDef.def)} Thủ` : `+${fmt(gDef.hp)} Máu`);
        const gStat = rawStat.replace(/^(?:Khảm nạm|Chú linh):\s*/i, '');
        summarySlotRows.push(`
          <div class="socket-summary-slot-row ${i === socketIdx ? 'selected' : ''}"
            data-select-orb="${i}" style="cursor:pointer;"
            title="Nhấp để chọn Lỗ ${i + 1}">
            <span class="name"><img src="${gDef.icon}"> <b class="rc-${gDef.rarity}">Lỗ ${i + 1}: ${gDef.name}</b></span>
            <span class="stat">${gStat}</span>
          </div>
        `);
      } else {
        summarySlotRows.push(`
          <div class="socket-summary-slot-row empty ${i === socketIdx ? 'selected' : ''}"
            data-select-orb="${i}" style="cursor:pointer;"
            title="Nhấp để chọn Lỗ ${i + 1}">
            <span class="name" style="color:#777">Lỗ ${i + 1}: (Trống)</span>
            <span class="stat" style="color:#555">Chưa chú linh</span>
          </div>
        `);
      }
    }

    const specialEffects = [];
    for (let i = 0; i < curMax; i++) {
      const gId = curGems[i];
      const gDef = gId ? ITEM[gId] : null;
      if (gDef) {
        if (gDef.effects) {
          for (const eff of gDef.effects) {
            const txt = eff.text || eff.desc || eff.name;
            if (txt && !specialEffects.includes(txt)) specialEffects.push(txt);
          }
        }
      }
    }

    const totalParts = [];
    if (totalAtk) totalParts.push(`<span class="val">+${fmt(totalAtk)} Công</span>`);
    if (totalDef) totalParts.push(`<span class="val">+${fmt(totalDef)} Thủ</span>`);
    if (totalHp) totalParts.push(`<span class="val">+${fmt(totalHp)} Máu</span>`);
    if (totalCrit) totalParts.push(`<span class="val">+${totalCrit}% Bạo</span>`);
    if (totalSpeed) totalParts.push(`<span class="val">+${totalSpeed} Thân pháp</span>`);

    const summaryCardHtml = `
      <div class="socket-summary-card">
        <div class="socket-summary-header">
          <h4>Tổng Hiệu Quả Chú Linh Trang Bị</h4>
          <span class="socket-summary-count">${totalFilled}/${curMax} Lỗ đã Chú Linh</span>
        </div>
        <div class="socket-summary-slots">
          ${summarySlotRows.join('')}
        </div>
        <div class="socket-summary-totals">
          <span class="lbl">Tổng thuộc tính:</span>
          ${totalParts.length ? totalParts.join('') : '<span style="color:#777;font-size:11px;">Chưa có thuộc tính kích hoạt</span>'}
          ${specialEffects.length ? `<div class="passive"><b>Hiệu ứng đặc biệt:</b> ${specialEffects.join(' · ')}</div>` : ''}
        </div>
      </div>
    `;

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
        <div class="picker-item ${isPick ? 'selected' : ''}" data-pick-gem="${id}"
          data-card="gem-pick" data-k="${id}" data-tip="1">
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

    const quickStats = equippedItem && typeof itemStats === 'function' ? itemStats(equippedItem).slice(0, 3).join(' · ') : '';

    return `
      <div class="socket-container">
        <!-- Cột 1: 10 Ô trang bị -->
        <div class="socket-slot-list">
          <div class="vt-sec-hdr">VỊ TRÍ CHÚ LINH (10 Ô)</div>
          <div class="socket-slot-grid">
            ${slotListHtml}
          </div>
          <div class="socket-slot-guide">
            <div class="guide-title">✦ Quy Tắc Chú Linh</div>
            <p>• Chú linh theo <b>10 vị trí trang bị</b>, khi thay đồ mới thuộc tính giữ nguyên 100%.</p>
            <p>• Cảnh giới mở tối đa 3 lỗ. Có thể dùng <i>Khai Khiếu Cổ Phù</i> để mở sớm.</p>
            <p>• Nhấp chuột phải vào lỗ hoặc trang bị để <b>tháo nhanh</b> Cổ Phù / Tiên Châu về Kho.</p>
          </div>
        </div>

        <!-- Cột 2: Center Stage -->
        <div class="socket-stage">
          <div class="socket-main-card"
            ${equippedItemId ? `data-card="vault-slot" data-k="${equippedItemId}" data-slot="${socketSlot}" data-tip="1"` : ''}>
            <div class="socket-slot-icon r-${equippedItem?.rarity || 'common'}" style="width:48px;height:48px;">
              ${equippedItemId ? itemImg(equippedItemId) : '<span style="font-size:12px;color:#777">Trống</span>'}
            </div>
            <div class="details">
              <h3>
                <span>${SLOT_NAMES[socketSlot] || socketSlot}:</span>
                <span class="${equippedItem ? `rc-${equippedItem.rarity}` : ''}">${equippedItem ? equippedItem.name : '(Chưa trang bị)'}</span>
              </h3>
              ${quickStats ? `<div class="item-quick-stats">${quickStats}</div>` : ''}
              <p>Chú linh theo vị trí ô đồ. Khi thay trang bị mới, Cổ Phù và Tiên Châu vẫn giữ nguyên vẹn 100%!</p>
            </div>
          </div>

          <div class="socket-orbs-wrap">
            ${orbsHtml}
          </div>

          ${actionCardHtml}

          ${summaryCardHtml}
        </div>

        <!-- Cột 3: Kho Picker -->
        <div class="socket-vault-picker">
          <div class="vt-sec-hdr" style="margin-bottom:6px;">KHO VẬT PHẨM CHÚ LINH</div>
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
        <div class="vault-card ${cnt > 0 ? 'has-items' : ''}"
          data-card="gem-vault" data-k="${it.id}" data-tip="1">
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

  function cubeClean(gs) {
    const vault = gs.vault || {};
    const used = {};
    cubeSel = cubeSel.filter(id => {
      const isHerb = ITEM[id]?.type === 'herb';
      const have = (isHerb ? S?.inventory?.[id] : vault[id]) || S?.inventory?.[id] || 0;
      const unit = isHerb ? 10 : 1;
      const cur = (used[id] || 0) + unit;
      if (cur <= have) {
        used[id] = cur;
        return true;
      }
      return false;
    });
  }

  function calcMaxCraft(inputs, stones, vault, inv) {
    let max = 999999;
    for (const [id, need] of Object.entries(inputs)) {
      const isHerb = ITEM[id]?.type === 'herb';
      const have = (isHerb ? inv[id] : vault[id]) || inv[id] || 0;
      max = Math.min(max, Math.floor(have / need));
    }
    if (stones > 0) {
      const haveGold = inv.linh_thach || 0;
      max = Math.min(max, Math.floor(haveGold / stones));
    }
    const maxCraft = Math.max(0, max);
    return {
      maxCraft,
      canCraftNow: maxCraft > 0
    };
  }

  function evaluateCube(sel, gs) {
    const recipes = gs?.recipes || [];
    const vault = gs?.vault || {};
    const inv = S?.inventory || {};
    const counts = {};
    for (const id of sel) {
      const isHerb = ITEM[id]?.type === 'herb';
      const u = isHerb ? 10 : 1;
      counts[id] = (counts[id] || 0) + u;
    }

    const activeEntries = Object.entries(counts).filter(([, n]) => n > 0);
    const totalSlots = sel.length;

    if (totalSlots === 0) {
      return {
        isValid: false,
        recipeId: null,
        name: 'Lò Hợp Đang Trống',
        desc: 'Chọn nguyên liệu từ Kho bên trái hoặc bấm Gợi ý công thức',
        resultItem: null,
        possibleResults: [],
        chancePct: 0,
        stones: 0,
        requiredInputs: {},
        hint: 'Chọn nguyên liệu từ Kho bên trái để bắt đầu',
        maxCraft: 0,
        canCraftNow: false
      };
    }

    // 1. Check Exact Recipe in recipes
    for (const r of recipes) {
      const reqEntries = Object.entries(r.inputs || {});
      if (reqEntries.length === activeEntries.length &&
          reqEntries.every(([k, v]) => counts[k] === v)) {
        return {
          isValid: true,
          recipeId: r.id,
          name: r.name,
          desc: r.desc,
          resultItem: r.resultItem,
          possibleResults: [r.resultItem],
          chancePct: Math.round((r.chance ?? 1.0) * 100),
          stones: r.stones || 0,
          requiredInputs: r.inputs,
          hint: null,
          ...calcMaxCraft(r.inputs, r.stones || 0, vault, inv)
        };
      }
    }

    // 2. Any 3 stone_*_so (mixed) -> random stone_*_cao
    if (totalSlots === 3 && sel.every(id => SO_STONES.has(id))) {
      const pool = sel.map(id => SO_TO_CAO[id]).filter(Boolean);
      const distinct = [...new Set(pool)];
      return {
        isValid: true,
        recipeId: 'free_so_stones',
        name: 'Dung Hợp Cổ Phù Sơ (Hỗn Hợp)',
        desc: `Dung hợp 3 Cổ Phù Sơ khác hệ thành 1 Cổ Phù Cao (${distinct.map(k => ITEM[k]?.name || k).join(', ')}).`,
        resultItem: distinct.length === 1 ? distinct[0] : null,
        possibleResults: pool,
        chancePct: 75,
        stones: 250,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 250, vault, inv)
      };
    }

    // 3. Any 2 stone_*_cao -> random divine stone
    if (totalSlots === 2 && sel.every(id => CAO_STONES.has(id))) {
      const pool = ['stone_liet_loi', 'stone_huyen_bang', 'stone_am_duong'];
      return {
        isValid: true,
        recipeId: 'free_cao_stones',
        name: 'Biến Dị Cổ Phù Cao Cấp',
        desc: 'Dung hợp 2 Cổ Phù Cao cấp thành 1 Cổ Phù Biến Dị (Liệt Lôi, Huyền Băng hoặc Âm Dương).',
        resultItem: null,
        possibleResults: pool,
        chancePct: 70,
        stones: 600,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 600, vault, inv)
      };
    }

    // 4. Any 3 gem_*_cui (mixed) -> random gem_*_xin
    if (totalSlots === 3 && sel.every(id => CUI_GEMS.has(id))) {
      const pool = sel.map(id => CUI_TO_XIN[id]).filter(Boolean);
      const distinct = [...new Set(pool)];
      return {
        isValid: true,
        recipeId: 'free_cui_gems',
        name: 'Dung Hợp Tiên Châu Thô (Đa Sắc)',
        desc: `Dung hợp 3 Tiên Châu Thô khác màu thành 1 Tiên Châu Hoàn Mỹ (${distinct.map(k => ITEM[k]?.name || k).join(', ')}).`,
        resultItem: distinct.length === 1 ? distinct[0] : null,
        possibleResults: pool,
        chancePct: 65,
        stones: 450,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 450, vault, inv)
      };
    }

    // 5. 1 gem_*_cui + (2 or 3 stone_*_so) -> corresponding gem_*_xin
    const cuiItems = sel.filter(id => CUI_GEMS.has(id));
    const soItems = sel.filter(id => SO_STONES.has(id));
    if (cuiItems.length === 1 && (soItems.length === 2 || soItems.length === 3) && totalSlots === 1 + soItems.length) {
      const cuiId = cuiItems[0];
      const xinId = CUI_TO_XIN[cuiId];
      const xinDef = ITEM[xinId];
      const cuiDef = ITEM[cuiId];
      return {
        isValid: true,
        recipeId: 'free_ascend_gem',
        name: `Thăng Hoa Tự Do (${xinDef?.name || xinId})`,
        desc: `Dùng Cổ Phù Sơ kích phát linh tính thăng hoa 1 ${cuiDef?.name || cuiId} thành 1 ${xinDef?.name || xinId}.`,
        resultItem: xinId,
        possibleResults: [xinId],
        chancePct: 75,
        stones: 800,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 800, vault, inv)
      };
    }

    // 6. 1 gem_*_xin + (1 Divine OR 2 Cao) -> gem_thien_phu
    const xinItems = sel.filter(id => XIN_GEMS.has(id));
    const divineItems = sel.filter(id => DIVINE_STONES.has(id));
    const caoItems = sel.filter(id => CAO_STONES.has(id));
    if (xinItems.length === 1 && ((divineItems.length === 1 && totalSlots === 2) || (caoItems.length === 2 && totalSlots === 3))) {
      return {
        isValid: true,
        recipeId: 'free_ascend_thien_phu',
        name: 'Đại Đạo Hợp Nhất · Cổ Phù Tiên Châu',
        desc: 'Dung hợp Tiên Châu Hoàn Mỹ cùng Cổ Phù biến dị/cao cấp rèn thành Cổ Phù Tiên Châu chí tôn phẩm Đỏ.',
        resultItem: 'gem_thien_phu',
        possibleResults: ['gem_thien_phu'],
        chancePct: 65,
        stones: 5000,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 5000, vault, inv)
      };
    }

    // 7. 2 Divine stones -> gem_thien_phu
    if (divineItems.length === 2 && totalSlots === 2) {
      return {
        isValid: true,
        recipeId: 'free_divine_fusion',
        name: 'Song Phù Quy Nhất · Cổ Phù Tiên Châu',
        desc: 'Dung hợp 2 Cổ Phù Biến Dị thượng thừa thăng hoa thành Cổ Phù Tiên Châu phẩm Đỏ.',
        resultItem: 'gem_thien_phu',
        possibleResults: ['gem_thien_phu'],
        chancePct: 70,
        stones: 3000,
        requiredInputs: counts,
        hint: null,
        ...calcMaxCraft(counts, 3000, vault, inv)
      };
    }

    // Dynamic Hints
    let hint = 'Tổ hợp nguyên liệu chưa phù hợp để hợp thành';
    if (sel.every(id => SO_STONES.has(id))) {
      if (totalSlots === 1) hint = 'Cần thêm 2 Cổ Phù Sơ nữa để dung hợp lên Cổ Phù Cao';
      else if (totalSlots === 2) hint = 'Cần thêm 1 Cổ Phù Sơ nữa để dung hợp lên Cổ Phù Cao';
      else if (totalSlots === 4 && new Set(sel).size === 4) hint = 'Cần thêm 1 Cổ Phù Sơ hệ còn lại để luyện Khai Khiếu Cổ Phù';
      else hint = 'Lò chỉ chứa tổ hợp 3 Cổ Phù Sơ (lên Cao) hoặc 5 Cổ Phù Sơ khác hệ (Khai Khiếu)';
    } else if (sel.every(id => CAO_STONES.has(id))) {
      if (totalSlots === 1) hint = 'Cần thêm 1 Cổ Phù Cao nữa để biến dị (Liệt Lôi, Huyền Băng, Âm Dương)';
      else hint = 'Tổ hợp Cổ Phù Cao không phù hợp (chỉ nhận đúng 2 Cổ Phù Cao)';
    } else if (sel.every(id => CUI_GEMS.has(id))) {
      if (totalSlots === 1) hint = 'Cần thêm 2 Tiên Châu Thô nữa để dung hợp lên Tiên Châu Hoàn Mỹ';
      else if (totalSlots === 2) hint = 'Cần thêm 1 Tiên Châu Thô nữa để dung hợp lên Tiên Châu Hoàn Mỹ';
      else hint = 'Lò chỉ nhận đúng 3 Tiên Châu Thô để dung hợp lên Hoàn Mỹ';
    } else if (cuiItems.length === 1 && soItems.length === 1 && totalSlots === 2) {
      hint = 'Cần thêm 1 hoặc 2 Cổ Phù Sơ nữa để thăng hoa Tiên Châu lên Hoàn Mỹ';
    } else if (xinItems.length === 1 && totalSlots === 1) {
      hint = 'Cần thêm 1 Cổ Phù Biến Dị hoặc 2 Cổ Phù Cao để dung hợp cùng Tiên Châu Hoàn Mỹ';
    } else if (divineItems.length === 1 && totalSlots === 1) {
      hint = 'Cần thêm 1 Cổ Phù Biến Dị nữa hoặc 1 Tiên Châu Hoàn Mỹ để luyện Cổ Phù Tiên Châu';
    }

    return {
      isValid: false,
      recipeId: null,
      name: 'Chưa Đủ Nguyên Liệu',
      desc: hint,
      resultItem: null,
      possibleResults: [],
      chancePct: 0,
      stones: 0,
      requiredInputs: counts,
      hint,
      maxCraft: 0,
      canCraftNow: false
    };
  }

  function renderCubeView(gs) {
    cubeClean(gs);

    const recipes = gs.recipes || [];
    const vault = gs.vault || {};

    const filtered = recipes.filter(r => cubeCategory === 'all' || r.category === cubeCategory);

    // Active recipe selection
    let curRecipe = recipes.find(r => r.id === selectedRecipeId);
    if (!curRecipe && filtered.length > 0) {
      curRecipe = filtered[0];
      selectedRecipeId = curRecipe.id;
    }

    const combo = evaluateCube(cubeSel, gs);

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

    const ownedHerbs = Object.keys(S.inventory || {})
      .filter(id => (S.inventory[id] || 0) > 0 && ITEM[id]?.type === 'herb')
      .map(id => [id, S.inventory[id]]);

    const allOwned = (cubeCategory === 'herb') ? ownedHerbs
      : (cubeCategory === 'all') ? ownedVault.concat(ownedHerbs)
      : ownedVault;

    let ownedItemsHtml = allOwned.map(([id, cnt]) => {
      const it = ITEM[id];
      const isHerb = it?.type === 'herb';
      const unitNeed = isHerb ? 10 : 1;
      const placedUnits = cubeSel.filter(x => x === id).length;
      const placedTotal = placedUnits * unitNeed;
      const remain = cnt - placedTotal;
      const isDepleted = remain < unitNeed;
      const isSelected = placedUnits > 0;

      return `
        <div class="cube-owned-item ${isSelected ? 'selected' : ''} ${isDepleted ? 'depleted' : ''}"
          data-cube-pick-item="${id}"
          data-card="cube-pick" data-k="${id}" data-tip="1">
          <img src="${it.icon}" alt="${it.name}">
          ${placedUnits > 0 ? `<span class="in-furnace-tag">Lò:${placedUnits}</span>` : ''}
          <span class="qty">${remain}/${cnt}</span>
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

    // 2. Cột Phải: Lò Hợp Nhất (5 Interactive Furnace Slots)
    let inputSlotsHtml = '';
    for (let i = 0; i < 5; i++) {
      if (i < cubeSel.length) {
        const ingId = cubeSel[i];
        const ingDef = ITEM[ingId];
        const isHerb = ingDef?.type === 'herb';
        inputSlotsHtml += `
          <div class="cube-slot interactive filled" data-cube-slot-idx="${i}"
            data-card="cube-slot" data-k="${ingId}" data-tip="1">
            <div class="cube-slot-box ok">
              <img src="${ingDef?.icon || ''}" alt="">
              ${isHerb ? '<span class="badge">×10</span>' : ''}
              <span class="slot-remove-btn" title="Lấy ra">×</span>
            </div>
            <span class="cube-slot-name">${ingDef?.name || ingId}</span>
          </div>
        `;
      } else {
        inputSlotsHtml += `
          <div class="cube-slot interactive empty" data-cube-slot-idx="${i}"
            data-tip="Ô Lò Hợp trống · Nhấp vật phẩm bên Kho để đặt vào">
            <div class="cube-slot-box empty-box">
              <span class="empty-plus">+</span>
            </div>
            <span class="cube-slot-name">Ô trống</span>
          </div>
        `;
      }
    }

    // Output Box setup
    let outSlotHtml = '';
    let outTitle = '';
    let outDesc = '';
    let titleClass = '';

    if (combo.isValid) {
      if (combo.resultItem) {
        const outDef = ITEM[combo.resultItem];
        titleClass = `rc-${outDef?.rarity || 'common'}`;
        outSlotHtml = `
          <div class="cube-out-slot r-${outDef?.rarity || 'common'}" id="cubeOutSlot"
            data-card="slot" data-k="${outDef.id}" data-tip="${typeof itemTip === 'function' ? itemTip(outDef.id) : outDef.name}">
            <img src="${outDef.icon}" alt="">
          </div>
        `;
      } else if (combo.possibleResults.length > 0) {
        const firstPoolDef = ITEM[combo.possibleResults[0]];
        const poolTip = `Ngẫu nhiên nhận 1 trong các vật phẩm:&#10;${combo.possibleResults.map(k => ITEM[k]?.name || k).join('&#10;')}`;
        titleClass = 'rc-mythic';
        outSlotHtml = `
          <div class="cube-out-slot random-pool r-mythic" id="cubeOutSlot" data-tip="${poolTip}">
            <img src="${firstPoolDef?.icon || ''}" alt="" style="opacity:0.85">
            <span class="pool-badge">?</span>
          </div>
        `;
      }
      outTitle = combo.name;
      outDesc = combo.desc;
    } else {
      if (cubeSel.length === 0 && curRecipe) {
        const outDef = ITEM[curRecipe.resultItem];
        outSlotHtml = `
          <div class="cube-out-slot ghost r-${outDef?.rarity || 'common'}" id="cubeOutSlot"
            data-tip="Gợi ý: ${curRecipe.name}">
            <img src="${outDef?.icon || ''}" alt="">
          </div>
        `;
        outTitle = curRecipe.name;
        outDesc = `${curRecipe.desc} · Nhấp [Tự động điền] hoặc nhấp nguyên liệu bên Kho`;
        titleClass = `rc-${outDef?.rarity || 'common'}`;
      } else {
        outSlotHtml = `<div class="cube-out-slot empty" id="cubeOutSlot"><span class="empty-q">?</span></div>`;
        outTitle = cubeSel.length === 0 ? 'Lò Hợp Đang Trống' : 'Chưa Đủ Nguyên Liệu';
        outDesc = combo.hint;
        titleClass = 'empty';
      }
    }

    const chancePct = combo.isValid ? combo.chancePct : (cubeSel.length === 0 && curRecipe ? Math.round((curRecipe.chance ?? 1.0) * 100) : null);
    const chanceDisplay = chancePct !== null ? `${chancePct}%` : '--%';
    const chanceClass = chancePct !== null ? (chancePct >= 90 ? 'gold' : chancePct >= 75 ? 'high' : 'mid') : '';
    const costStones = combo.isValid ? combo.stones : (cubeSel.length === 0 && curRecipe ? (curRecipe.stones || 0) : 0);
    const haveGold = S.inventory?.linh_thach || 0;
    const goldOk = haveGold >= costStones;

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
            <div class="vt-sec-hdr">KHO (${allOwned.length}) · NHẤP ĐỂ BỎ VÀO LÒ</div>
            <div class="cube-owned-grid">
              ${ownedItemsHtml}
            </div>
          </div>

          <!-- Danh sách công thức hợp thành gợi ý -->
          <div class="cube-recipes-section">
            <div class="vt-sec-hdr">GỢI Ý CÔNG THỨC (${filtered.length})</div>
            <div class="cube-recipe-list">
              ${recipeListHtml}
            </div>
          </div>
        </section>

        <!-- Cột Phải: Lò Hợp Nhất (5 Interactive Furnace Slots) -->
        <aside class="cube-forge-panel">
          <div class="cube-forge-header">
            <h3>LÒ HỢP CỔ PHÙ & TIÊN CHÂU</h3>
            <p>Tự do đặt tối đa 5 nguyên liệu vào lò để dung hợp thăng cấp, biến dị hoặc tạo Cổ Phù Tiên Châu</p>
          </div>

          <!-- 5 Slots lò hợp tương tác -->
          <div class="cube-forge-stage ${isCraftingAnim ? 'forging' : ''}" id="cubeForgeStage">
            <div class="cube-input-slots">
              ${inputSlotsHtml}
            </div>

            <div class="cube-forge-arrow">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12l7 7 7-7"/></svg>
            </div>

            <!-- Output Slot -->
            <div class="cube-output-box">
              ${outSlotHtml}
              <div class="cube-out-info">
                <h4 class="${titleClass}">${outTitle}</h4>
                <span class="cube-out-desc">${outDesc}</span>
              </div>
            </div>
          </div>

          <!-- Tỉ lệ thành công & Quy tắc -->
          <div class="cube-rate-box">
            <div class="cube-chance">
              <span>Tỉ lệ thành công:</span>
              <b class="${chanceClass}">${chanceDisplay}</b>
            </div>
            <ul class="cube-rules">
              ${combo.isValid ? `
                <li class="ok">✓ Thành công: Nhận 1 ${combo.resultItem ? (ITEM[combo.resultItem]?.name || '') : 'vật phẩm phẩm cao hơn ngẫu nhiên'}</li>
                <li class="${combo.chancePct < 100 ? 'bad' : 'ok'}">
                  ${combo.chancePct === 100 ? '✓ Tỉ lệ 100%: Chắc chắn thành công, không có rủi ro thất bại.' : '⚠ Thất bại: Tiêu hao linh thạch, bảo lưu 1 phần nguyên liệu về Kho.'}
                </li>
              ` : `
                <li>${combo.hint}</li>
              `}
            </ul>
            <div class="cube-fee-row">
              <span>Chi phí:</span>
              <img src="img/stone/linh_thach.png" alt="">
              <b class="${goldOk ? '' : 'bad'}">${fmt(costStones)}</b> linh thạch
              ${!goldOk && costStones > 0 ? '<span style="color:#ff7875;font-size:10px;margin-left:4px;">(Không đủ)</span>' : ''}
            </div>
          </div>

          <!-- Nút hành động -->
          <div class="cube-actions fz-act">
            <button class="btn-cube-clear" data-cube-act="clear" ${cubeSel.length > 0 ? '' : 'disabled'} data-tip="Bỏ chọn toàn bộ nguyên liệu trong Lò">
              ${CLEAR_SVG}<span>Xóa lò</span>
            </button>
            <button class="btn-cube-auto fz-af" data-cube-act="auto" data-tip="Tự động điền nguyên liệu cho công thức phù hợp">
              ${FUSE_GRID_SVG}<span>Tự động điền</span>
            </button>
            <button class="btn-cube-go dd-synth-btn fz-go" data-cube-act="go" ${combo.canCraftNow && !busy ? '' : 'disabled'}>
              ${FUSE_GO_SVG}<span>Hợp Thành</span>
            </button>
            ${combo.maxCraft > 1 ? `
            <button class="btn-cube-max-times dd-all" data-cube-act="max" ${!busy ? '' : 'disabled'} data-tip="Hợp thành tối đa&#10;Hợp thành nhiều lần nhất có thể với nguyên liệu trong kho">
              Tối đa ×${combo.maxCraft}
            </button>` : ''}
          </div>
        </aside>
      </div>
    `;
  }

  // =========================================================================
  // BINDING SỰ KIỆN TƯƠNG TÁC
  // =========================================================================
  function bindVaultEvents(pane) {
    if (!pane || pane._vaultBound) return;
    pane._vaultBound = true;

    let lastSocketAt = 0;
    let lastUnsocketAt = 0;

    async function doSocketGem(gemId) {
      const now = performance.now();
      if (busy || !gemId || (now - lastSocketAt < 350)) return;
      lastSocketAt = now;
      const gs = getVaultState();
      const curMax = gs.maxSockets?.[socketSlot] || (S?.realm >= 4 ? 3 : S?.realm >= 2 ? 2 : 1);
      const curGems = gs.slots?.[socketSlot] || [];

      let targetIdx = socketIdx;
      if (targetIdx < 0 || targetIdx >= curMax) {
        const firstEmptyIdx = [0, 1, 2].slice(0, curMax).find(i => !curGems[i]);
        targetIdx = firstEmptyIdx !== undefined ? firstEmptyIdx : 0;
      }

      if (typeof hideTip === 'function') hideTip();
      selectedGemId = null;
      lastTap = { key: '', t: 0 };
      socketIdx = targetIdx;
      await act(`gem/socket?slot=${socketSlot}&socketIndex=${targetIdx}&itemId=${gemId}`);
    }

    async function doUnsocketGem(idx) {
      const now = performance.now();
      if (busy || isNaN(idx) || (now - lastUnsocketAt < 350)) return;
      lastUnsocketAt = now;
      if (typeof hideTip === 'function') hideTip();
      selectedGemId = null;
      lastTap = { key: '', t: 0 };
      await act(`gem/unsocket?slot=${socketSlot}&socketIndex=${idx}`);
    }

    // A. CHUỘT PHẢI (CONTEXTMENU): Tháo Cổ Phù / Tiên Châu nhanh
    pane.addEventListener('contextmenu', async e => {
      // 1. Chuột phải vào ô Lỗ Khảm ở giữa hoặc ảnh xem trước ngọc
      const filledOrb = e.target.closest('.socket-orb.filled, .socket-orb-box.filled, .selected-gem-preview');
      if (filledOrb) {
        e.preventDefault();
        const orbEl = filledOrb.classList.contains('socket-orb') ? filledOrb : (filledOrb.querySelector('.socket-orb') || filledOrb);
        const idx = parseInt(orbEl?.dataset?.orbIdx ?? filledOrb.dataset.orbIdx, 10);
        if (!isNaN(idx)) {
          await doUnsocketGem(idx);
        }
        return;
      }

      // 2. Chuột phải vào ô trang bị ở cột trái (tháo nhanh viên cuối cùng)
      const slotBtn = e.target.closest('.socket-slot-btn');
      if (slotBtn) {
        e.preventDefault();
        if (busy) return;
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
            selectedGemId = null;
            lastTap = { key: '', t: 0 };
            await act(`gem/unsocket?slot=${slot}&socketIndex=${lastIdx}`);
          }
        }
        return;
      }

      // 3. Chuột phải vào thẻ trang bị ở giữa (tháo nhanh viên cuối cùng)
      const mainCard = e.target.closest('.socket-main-card');
      if (mainCard) {
        e.preventDefault();
        if (busy) return;
        const slot = socketSlot;
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
            selectedGemId = null;
            lastTap = { key: '', t: 0 };
            await act(`gem/unsocket?slot=${slot}&socketIndex=${lastIdx}`);
          }
        }
        return;
      }

      // 4. Chuột phải vào ô trong Lò Hợp -> Lấy ra khỏi Lò
      const cubeSlotEl = e.target.closest('[data-cube-slot-idx]');
      if (cubeSlotEl) {
        e.preventDefault();
        const idx = parseInt(cubeSlotEl.dataset.cubeSlotIdx, 10);
        if (!isNaN(idx) && idx >= 0 && idx < cubeSel.length) {
          if (typeof hideTip === 'function') hideTip();
          cubeSel.splice(idx, 1);
          renderVault();
        }
        return;
      }

      // 5. Chuột phải vào vật phẩm trong KHO (bên Lò Hợp) -> Lấy 1 viên ra khỏi Lò nếu đang có trong lò
      const cubePickItem = e.target.closest('[data-cube-pick-item]');
      if (cubePickItem) {
        e.preventDefault();
        const id = cubePickItem.dataset.cubePickItem;
        const lastIdx = cubeSel.lastIndexOf(id);
        if (lastIdx >= 0) {
          if (typeof hideTip === 'function') hideTip();
          cubeSel.splice(lastIdx, 1);
          renderVault();
        }
        return;
      }
    });

    // B. NHẤP ĐÚP CHUỘT TRÁI (DBLCLICK) - native fallback
    pane.addEventListener('dblclick', async e => {
      // 1. Nhấp đúp vào Cổ Phù / Tiên Châu trong Kho Picker (cột phải) -> Chú Linh ngay vào lỗ
      const pickItem = e.target.closest('[data-pick-gem]');
      if (pickItem) {
        e.preventDefault();
        const gemId = pickItem.dataset.pickGem;
        lastTap = { key: '', t: 0 };
        if (gemId) await doSocketGem(gemId);
        return;
      }

      // 2. Nhấp đúp vào Lỗ Khảm ở giữa đang có ngọc hoặc ảnh xem trước ngọc -> Tháo về Kho
      const filledOrb = e.target.closest('.socket-orb.filled, .socket-orb-box.filled, .selected-gem-preview');
      if (filledOrb) {
        e.preventDefault();
        const orbEl = filledOrb.classList.contains('socket-orb') ? filledOrb : (filledOrb.querySelector('.socket-orb') || filledOrb);
        const idx = parseInt(orbEl?.dataset?.orbIdx ?? filledOrb.dataset.orbIdx, 10);
        if (!isNaN(idx)) await doUnsocketGem(idx);
        return;
      }
    });

    // C. CHUỘT TRÁI (CLICK)
    pane.addEventListener('click', async e => {
      // 1. Chuyển tab phụ (Chú Linh / Kho / Lò Hợp)
      const vtabBtn = e.target.closest('[data-vtab]');
      if (vtabBtn) {
        vaultTab = vtabBtn.dataset.vtab;
        renderVault();
        return;
      }

      // 2. Chọn ô trang bị trong Chú Linh
      const slotBtn = e.target.closest('[data-select-slot]');
      if (slotBtn) {
        socketSlot = slotBtn.dataset.selectSlot;
        socketIdx = 0;
        selectedGemId = null;
        renderVault();
        return;
      }

      // 3. Chọn lỗ khảm (hoặc nhấp đúp tháo nếu có ngọc)
      const orbBtn = e.target.closest('[data-select-orb], .selected-gem-preview');
      if (orbBtn) {
        const idx = parseInt(orbBtn.dataset.selectOrb ?? orbBtn.dataset.orbIdx, 10);
        if (!isNaN(idx)) {
          const now = performance.now();
          const isDbl = (lastTap.key === 'orb_' + idx) && (now - lastTap.t < 400);
          lastTap = isDbl ? { key: '', t: 0 } : { key: 'orb_' + idx, t: now };

          const gs = getVaultState();
          const curGems = gs.slots?.[socketSlot] || [];
          const hasGem = !!curGems[idx];

          if (isDbl && hasGem) {
            await doUnsocketGem(idx);
            return;
          }

          if (socketIdx !== idx) {
            socketIdx = idx;
            selectedGemId = null;
            renderVault();
          }
          return;
        }
      }

      // 4. Lọc picker tab
      const ptabBtn = e.target.closest('[data-ptab]');
      if (ptabBtn) {
        pickerTab = ptabBtn.dataset.ptab;
        renderVault();
        return;
      }

      // 5. Nhấp đúp hoặc nhấp nhanh gem từ picker để chú linh vào lỗ đang chọn
      const pickItem = e.target.closest('[data-pick-gem]');
      if (pickItem) {
        const gemId = pickItem.dataset.pickGem;
        const now = performance.now();
        const isDbl = (lastTap.key === 'pick_' + gemId) && (now - lastTap.t < 400);
        lastTap = isDbl ? { key: '', t: 0 } : { key: 'pick_' + gemId, t: now };

        if (isDbl) {
          lastTap = { key: '', t: 0 };
          await doSocketGem(gemId);
        }
        return;
      }

      // 6. Hủy chọn gem
      if (e.target.closest('[data-cancel-pick]')) {
        selectedGemId = null;
        renderVault();
        return;
      }

      // 7. Thực hiện Chú Linh
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

      // 8. Mở lỗ khảm bằng Khai Khiếu Cổ Phù
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

      // 9. Hợp nhanh
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

      // 10. Lọc danh mục lò hợp
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

      // 11. Click ô trong Lò Hợp -> Lấy ra khỏi lò
      const cubeSlotBtn = e.target.closest('[data-cube-slot-idx]');
      if (cubeSlotBtn) {
        const idx = parseInt(cubeSlotBtn.dataset.cubeSlotIdx, 10);
        if (!isNaN(idx) && idx >= 0 && idx < cubeSel.length) {
          cubeSel.splice(idx, 1);
          renderVault();
        }
        return;
      }

      // 12. Chọn vật phẩm từ kho bên trái Lò Hợp -> Bỏ 1 viên vào Lò
      const cubePickItemBtn = e.target.closest('[data-cube-pick-item]');
      if (cubePickItemBtn) {
        const pickedId = cubePickItemBtn.dataset.cubePickItem;
        const gs = getVaultState();
        const vault = gs.vault || {};
        const isHerb = ITEM[pickedId]?.type === 'herb';
        const totalOwned = (isHerb ? S.inventory?.[pickedId] : vault[pickedId]) || S.inventory?.[pickedId] || 0;
        const unitNeed = isHerb ? 10 : 1;
        const placedUnits = cubeSel.filter(x => x === pickedId).length;
        const placedTotal = placedUnits * unitNeed;
        const remain = totalOwned - placedTotal;

        if (cubeSel.length >= 5) {
          localLog('Lò Hợp chỉ chứa tối đa 5 nguyên liệu.', 'bad');
          return;
        }
        if (remain < unitNeed) {
          localLog(`Đã đặt toàn bộ ${ITEM[pickedId]?.name || pickedId} có trong Kho vào Lò.`, 'bad');
          return;
        }

        cubeSel.push(pickedId);
        renderVault();
        return;
      }

      // 13. Chọn công thức trong danh sách gợi ý Lò Hợp
      const recipeItemBtn = e.target.closest('[data-select-recipe]');
      if (recipeItemBtn) {
        selectedRecipeId = recipeItemBtn.dataset.selectRecipe;
        const gs = getVaultState();
        const recipe = (gs.recipes || []).find(r => r.id === selectedRecipeId);
        if (recipe) {
          const vault = gs.vault || {};
          cubeSel = [];
          for (const [ingId, need] of Object.entries(recipe.inputs || {})) {
            const isHerb = ITEM[ingId]?.type === 'herb';
            const have = (isHerb ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
            if (isHerb) {
              if (have >= 10 && cubeSel.length < 5) cubeSel.push(ingId);
            } else {
              const put = Math.min(need, have, 5 - cubeSel.length);
              for (let k = 0; k < put; k++) cubeSel.push(ingId);
            }
          }
        }
        renderVault();
        return;
      }

      // 15. Nút [Xóa lò] / [Bỏ chọn] trong Lò Hợp
      if (e.target.closest('[data-cube-act="clear"]')) {
        cubeSel = [];
        renderVault();
        return;
      }

      // 16. Nút [Tự động điền] trong Lò Hợp
      if (e.target.closest('[data-cube-act="auto"]')) {
        const gs = getVaultState();
        const recipes = gs.recipes || [];
        const vault = gs.vault || {};
        const canCraft = (r) => {
          for (const [ingId, need] of Object.entries(r.inputs || {})) {
            const have = (ITEM[ingId]?.type === 'herb' ? S.inventory?.[ingId] : vault[ingId]) || S.inventory?.[ingId] || 0;
            if (have < need) return false;
          }
          if (r.stones > 0 && (S.inventory?.linh_thach || 0) < r.stones) return false;
          return true;
        };

        const targetRecipe = recipes.find(r => r.id === selectedRecipeId && canCraft(r))
          || recipes.find(r => (cubeCategory === 'all' || r.category === cubeCategory) && canCraft(r))
          || recipes.find(r => canCraft(r));

        if (targetRecipe) {
          selectedRecipeId = targetRecipe.id;
          if (cubeCategory !== 'all' && targetRecipe.category !== cubeCategory) cubeCategory = targetRecipe.category;
          cubeSel = [];
          for (const [ingId, need] of Object.entries(targetRecipe.inputs || {})) {
            const isHerb = ITEM[ingId]?.type === 'herb';
            if (isHerb) {
              if (cubeSel.length < 5) cubeSel.push(ingId);
            } else {
              for (let k = 0; k < need && cubeSel.length < 5; k++) cubeSel.push(ingId);
            }
          }
          renderVault();
        } else {
          localLog('Không đủ nguyên liệu hoặc linh thạch cho công thức nào trong danh sách.', 'bad');
        }

        const stage = $('cubeForgeStage');
        if (stage) {
          stage.classList.remove('forging');
          void stage.offsetWidth;
          stage.classList.add('forging');
          setTimeout(() => stage.classList.remove('forging'), 500);
        }
        return;
      }

      // 17. Nút [Hợp Thành] hoặc [Tối đa ×N] trong Lò Hợp
      const cubeActGo = e.target.closest('[data-cube-act="go"]');
      const cubeActMax = e.target.closest('[data-cube-act="max"]');
      if (cubeActGo || cubeActMax) {
        const btn = cubeActGo || cubeActMax;
        if (btn.disabled || busy || cubeSel.length === 0) return;

        const gs = getVaultState();
        const evalResult = evaluateCube(cubeSel, gs);
        if (!evalResult.isValid || !evalResult.canCraftNow) return;

        const times = cubeActMax ? Math.max(1, evalResult.maxCraft) : 1;

        btn.disabled = true;
        const stage = $('cubeForgeStage');
        if (stage) stage.classList.add('forging');

        try {
          const itemsParam = cubeSel.join(',');
          const ok = await act(`gem/fuse-free?items=${encodeURIComponent(itemsParam)}&n=${times}`);
          
          cubeSel = []; // Xóa các ô lò sau khi hợp xong

          // Sau khi act() hoàn thành, renderAll/renderVault đã vẽ lại DOM mới
          const freshStage = $('cubeForgeStage');
          const freshOutSlot = $('cubeOutSlot');
          if (freshStage) freshStage.classList.remove('forging');

          const lastLog = S?.logs?.[0];
          const isFail = lastLog?.kind === 'bad';

          if (ok && !isFail && freshOutSlot) {
            freshOutSlot.classList.remove('success-burst');
            void freshOutSlot.offsetWidth;
            freshOutSlot.classList.add('success-burst');
            setTimeout(() => freshOutSlot?.classList.remove('success-burst'), 1200);
          } else if (freshStage) {
            freshStage.classList.remove('fail-shake');
            void freshStage.offsetWidth;
            freshStage.classList.add('fail-shake');
            setTimeout(() => freshStage?.classList.remove('fail-shake'), 600);
          }
        } finally {
          btn.disabled = false;
        }
        return;
      }
    });
  }

  window.renderVault = renderVault;
  window.getVaultState = getVaultState;
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
