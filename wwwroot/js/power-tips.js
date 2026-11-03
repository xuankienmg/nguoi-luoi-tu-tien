/* Vấn Đạo · js/power-tips.js: thua trận (ải thường, Boss chương, Lôi đài) → bảng đề xuất tăng lực chiến.
   Gợi ý tính ở client từ trạng thái S: trang bị, Tinh Thạch, công pháp, linh thú và đột phá; mỗi gợi ý có nút đưa tới đúng chỗ. */
let ptShownKey = null, ptLastArea = 0;
const PT_AREA_GAP = 10 * 60 * 1000;        // ải thường thua liên tục khi treo máy: tự hiện tối đa 10 phút một lần
const ptOffKey = 'van-dao-power-tips-area-off';
const ptAreaOff = () => { try { return localStorage.getItem(ptOffKey) === '1'; } catch(e){ return false; } };

/** Gọi mỗi khung hình từ stage.js khi trận đang diễn ở pha thua; key = mã trận để mỗi trận chỉ hiện một lần. */
function powerTipsOnLose(kind, key){
  if (MODE === 'panel' || !S || key === ptShownKey) return;
  ptShownKey = key;
  if (kind === 'area'){
    if (ptAreaOff() || performance.now() - ptLastArea < PT_AREA_GAP && ptLastArea) return;
    ptLastArea = performance.now();
  }
  if (!document.getElementById('ptPanel')) showPowerTips(kind);   // dải desktop (strip): hiện gọn ngay trên màn hình chính
}

/** Đi tới chỗ của một gợi ý: dải desktop nhờ cửa sổ Tu luyện mở đúng mục, bản web mở tab ngay trong trang. */
function ptGo(t){
  if (MODE === 'strip'){ notifyHost('open-panel', {tab: t.gem ? 'vault' : t.go}); return; }
  if (t.gem && typeof openVaultSlot === 'function') openVaultSlot(t.gem); else openTab(t.go, true);
}

/** Điểm chỉ số một món trang bị, cùng hệ số với lực chiến (inventory.js power): công ×2, thủ ×3, máu ×0,4, mỗi điểm % phụ ×4. */
const PT_PCT = ['critPct', 'speedPct', 'accuracyPct', 'critResistPct', 'comboPct', 'comboResistPct', 'reflectPct', 'reflectResistPct'];
const ptGearRank = it => (it.atk || 0) * 2 + (it.def || 0) * 3 + (it.hp || 0) * .4 + PT_PCT.reduce((n, k) => n + (it[k] || 0), 0) * 4;

function powerTips(){
  const tips = [], slotName = id => DATA.slots.find(s => s.id === id)?.name || id;
  // Trang bị: ô trống có đồ mặc được, hoặc trong túi có món điểm chỉ số cao hơn đồ đang mặc
  const bag = Object.keys(S.inventory || {}).filter(k => S.inventory[k] > 0 && ITEM[k]?.slot && canWear(ITEM[k]));
  const gear = [];
  for (const slot of S.equipSlots || []){
    const cur = S.equipment?.[slot] && ITEM[S.equipment[slot]];
    const best = bag.filter(k => ITEM[k].slot === slot).sort((a, b) => ptGearRank(ITEM[b]) - ptGearRank(ITEM[a]))[0];
    if (best && (!cur || ptGearRank(ITEM[best]) > ptGearRank(cur))) gear.push([slot, best, !cur]);
  }
  if (gear.length) tips.push({ic: gear[0][1], title: 'Thay trang bị tốt hơn',
    text: gear.map(([s, k, empty]) => `${slotName(s)}${empty ? ' (đang trống)' : ''}: ${ITEM[k].name}`).join(' · '), go: 'inv', label: 'Hành trang'});

  // Tinh Thạch: lỗ khảm trống mà túi có viên hợp loại · đủ 9 viên cùng loại để hợp lên phẩm cao hơn
  const gs = S.gemState || {}, vault = gs.vault || {};
  const catOf = slot => ['head', 'feet', 'body', 'hands', 'legs'].includes(slot) ? 'defense' : 'attack';
  const open = Object.keys(S.equipment || {}).filter(slot => {
    const max = gs.maxSockets?.[slot] || 0, filled = (gs.slots?.[slot] || []).filter(Boolean).length;
    return filled < max && Object.keys(vault).some(id => vault[id] > 0 && ITEM[id]?.gemCategory === catOf(slot));
  });
  if (open.length) tips.push({ic: Object.keys(vault).find(id => vault[id] > 0 && ITEM[id]), title: 'Khảm Tinh Thạch vào lỗ trống',
    text: `Còn lỗ khảm trống ở ${open.map(slotName).join(', ')} mà túi có Tinh Thạch hợp loại.`, gem: open[0], label: 'Khảm ngay'});
  const fusable = Object.keys(vault).filter(id => vault[id] >= 9 && ITEM[id] && RAR_KEYS.indexOf(ITEM[id].rarity) < RAR_KEYS.length - 1);
  if (fusable.length) tips.push({ic: fusable[0], title: 'Hợp Tinh Thạch lên phẩm cao',
    text: `Đủ 9 viên để hợp: ${fusable.slice(0, 3).map(id => ITEM[id].name).join(', ')}${fusable.length > 3 ? '…' : ''}.`, go: 'fuse', label: 'Lò Hợp'});

  // Công pháp: kỹ năng lĩnh ngộ / nâng cấp được bằng điểm hiện có, ô tuyệt kỹ còn trống
  const avail = (DATA.skillTree || []).filter(s => skState(s).st === 'avail');
  if (avail.length) tips.push({sk: true, title: 'Cộng điểm công pháp',
    text: `Còn ${fmt(S.skillPoints || 0)} điểm kỹ năng · ${avail.length} kỹ năng nâng được: ${avail.slice(0, 3).map(s => s.name).join(', ')}${avail.length > 3 ? '…' : ''}.`, go: 'skill', label: 'Công pháp'});
  if (!S.skillSlots?.[1] && (DATA.skillTree || []).some(s => s.tree === 'active' && skLv(s.id) > 0))
    tips.push({sk: true, title: 'Chọn tuyệt kỹ xuất chiến', text: 'Ô tuyệt kỹ đang trống dù đã học kỹ năng chủ động.', go: 'skill', label: 'Công pháp'});

  // Linh thú: chưa có con xuất chiến · con đang ra trận còn trống ô trang bị mà túi có đồ hợp
  const pet = S.pet || {}, owned = pet.owned || [];
  if (owned.length && !owned.some(p => p.slot > 0))
    tips.push({img: owned[0].image, title: 'Mang linh thú ra trận', text: `Có ${owned.length} linh thú nhưng chưa con nào xuất chiến.`, go: 'pet', label: 'Linh thú'});
  const gearRules = pet.training?.gear || [];
  const bare = owned.filter(p => p.slot > 0).filter(p => (pet.training?.slots || []).some(s =>
    !p.training?.gear?.[s.id] && gearRules.some(g => g.slot === s.id && (S.inventory?.[g.id] || 0) > 0)));
  if (bare.length) tips.push({img: bare[0].image, title: 'Mặc trang bị linh thú',
    text: `${bare.map(p => p.name).join(', ')} còn ô trang bị trống mà túi có đồ hợp.`, go: 'pet', label: 'Linh thú'});

  // Đột phá: đủ điều kiện mà chưa đột phá
  if (typeof btReady === 'function' && btReady())
    tips.push({bt: true, title: 'Đột phá ngay', text: `Đã đủ điều kiện lên ${S.isPeak ? S.nextRealmName : 'tầng kế tiếp'}: chỉ số gốc tăng mạnh.`, go: 'req', label: 'Đột phá'});
  return tips;
}

const PT_TITLE = {area: 'Thua ải', boss: 'Thua Boss', arena: 'Thua Lôi đài'};
function showPowerTips(kind){
  const tips = powerTips(), el = document.createElement('div');
  el.id = 'ptPanel'; el.className = 'pt'; el.setAttribute('role', 'dialog'); el.setAttribute('aria-modal', 'true'); el.setAttribute('aria-labelledby', 'ptTitle');
  const ic = t => t.ic && ITEM[t.ic] ? icon(t.ic) : t.img ? `<img class="ic" src="${t.img}" alt="">` : `<span class="pt-glyph">${t.bt ? '⇧' : t.sk ? '✦' : '◆'}</span>`;
  el.innerHTML = `<div class="pt-card">
    <header><div><small>${PT_TITLE[kind] || 'Thất bại'}</small><h3 id="ptTitle">Đề xuất tăng lực chiến</h3><p>Lực chiến hiện tại <b>${fmt(power(S.stats))}</b></p></div>
      <button class="pt-x" data-pt-x aria-label="Đóng">✕</button></header>
    ${tips.length ? `<ul class="pt-list">${tips.map((t, i) => `<li>${ic(t)}<span><b>${t.title}</b><small>${t.text}</small></span><button data-pt-go="${i}">${t.label} ›</button></li>`).join('')}</ul>`
      : '<p class="pt-none">Đồ, Tinh Thạch, công pháp và linh thú đều đã tận dụng hết. Tu luyện thêm, đánh ải thấp để lấy đồ tốt hơn rồi quay lại.</p>'}
    ${kind === 'area' ? `<label class="pt-off"><input type="checkbox" data-pt-off ${ptAreaOff() ? 'checked' : ''}> Không tự hiện khi thua ải thường</label>` : ''}
  </div>`;
  const close = () => { el.remove(); removeEventListener('keydown', esc, true); };
  const esc = e => { if (e.key === 'Escape'){ e.stopPropagation(); close(); } };
  el.addEventListener('click', e => {
    if (e.target === el || e.target.closest('[data-pt-x]')){ close(); return; }
    const go = e.target.closest('[data-pt-go]'); if (!go) return;
    const t = tips[+go.dataset.ptGo]; close(); ptGo(t);
  });
  el.addEventListener('change', e => {
    if (e.target.matches('[data-pt-off]')) try { localStorage.setItem(ptOffKey, e.target.checked ? '1' : '0'); } catch(err){}
  });
  addEventListener('keydown', esc, true);
  document.body.appendChild(el);
  (el.querySelector('[data-pt-go]') || el.querySelector('[data-pt-x]')).focus();
}
