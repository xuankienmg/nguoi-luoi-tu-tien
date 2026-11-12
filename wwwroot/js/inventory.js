/* js/inventory.js: tab Hành trang: trang bị, túi, bán, kéo thả. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// HÀNH TRANG: nhân vật ↔ trang bị ↔ hành trang ↔ so sánh ↔ mặc ↔ chỉ số
// =====================================================================
// 10 phẩm (GameData.QualityName / QualityColor): khóa giữ tên cũ cho 6 phẩm đầu để không đổi dữ liệu đã lưu
const RARITY = {
  common:    {n:'Phàm',        c:'#FFFFFF', v:3},
  uncommon:  {n:'Linh',        c:'#00FF7F', v:12},
  rare:      {n:'Huyền',       c:'#00E5FF', v:50},
  epic:      {n:'Địa',         c:'#1E90FF', v:200},
  legendary: {n:'Thiên',       c:'#BF00FF', v:800},
  mythic:    {n:'Thánh',       c:'#FF8C00', v:3200},
  divine:    {n:'Thần',        c:'#FF0033', v:12800},
  immortal:  {n:'Tiên',        c:'#FF69B4', v:51200},
  dao:       {n:'Đạo',         c:'#FFD700', v:204800},
  supreme:   {n:'Huyền Tiên', c:'#E0FFFF', v:819200},
};
// Pet-only quality: non-enumerable so the ten gear/spin quality slots stay unchanged.
Object.defineProperty(RARITY,'ancient',{value:{n:'Thượng Cổ',c:'#d7d4ff',v:0},enumerable:false});
const CATS = [['all','Tất cả'],['weapon','Vũ khí'],['armor','Phòng cụ'],['accessory','Trang sức'],['consumable','Đan dược'],['material','Nguyên liệu'],['quest','Nhiệm vụ']];
const SLOT_LEFT = ['head','body','hands','legs','feet'], SLOT_RIGHT = ['necklace','charm','ring','weapon','offhand'];
const STAT_KEYS = [['atk','Tấn công',''],['def','Phòng thủ',''],['hp','Máu',''],['critPct','Bạo kích','%'],['critResistPct','Kháng bạo kích','%'],['accuracyPct','Chính xác','%'],['speedPct','Né tránh','%'],['comboPct','Liên kích','%'],['comboResistPct','Kháng liên kích','%'],['reflectPct','Phản đòn','%'],['reflectResistPct','Kháng phản đòn','%'],['qiPct','Tu vi','%']];
const fmtRating = n => Number(n || 0).toLocaleString('vi-VN', {maximumFractionDigits: 2});
/** Vật phẩm dùng được từ túi: đan hồi linh khí, luyện thể, mở rương. */
const canUse = it => !!it && !!(it.useQiSeconds || it.temperHp || it.type === 'chest' || it.type === 'eventchest' || it.type === 'egg' || it.type === 'elixir' || it.type === 'manual' || it.type === 'skin');
const catOf = it => ['quest','key','boss'].includes(it.type) ? 'quest' : it.slot ? (['weapon','offhand'].includes(it.slot) ? 'weapon' : ['necklace','ring','charm'].includes(it.slot) ? 'accessory' : 'armor') : it.type === 'pill' || it.type === 'elixir' || it.type === 'manual' ? 'consumable' : 'material';
const rar = it => RARITY[it.rarity] || RARITY.common;
function levelOf(realmIdx){ let n = 1; for (let i = 0; i < realmIdx; i++) n += DATA.realms[i].stages.length; return n; }
const reqLevel = it => levelOf(it.minRealm || 0);
/** Cấp trang bị (1 Luyện Khí · 10 Trúc Cơ · 15 Kim Đan) hiện ở góc ô; món không có cấp thì hiện cấp nhân vật tối thiểu của cảnh giới yêu cầu. */
const itemLv = it => it.itemLevel || reqLevel(it);
const RAR_KEYS = Object.keys(RARITY);

/** Hiệu ứng ô phẩm cao (inventory.css: hạt sáng, viền xoay, nhịp sáng, ảnh phát sáng) neo chung đồng hồ của trang:
 *  Hành trang / Tinh Thạch vẽ lại ô mỗi lần đồng bộ (~4 giây), ô mới mà chạy lại từ đầu thì viền giật về vị trí xuất phát.
 *  Đặt startTime = 0 thì ô mới tiếp tục đúng nhịp ô cũ (độ lệch animation-delay theo từng ô vẫn giữ). */
const RARITY_ANIMS = new Set(['q-rise', 'rd-rim', 'q-pulse', 'q-glow', 'q-glow-rb']);
let rarityAnimSync = 0;
function syncRarityAnims(){
  rarityAnimSync = 0;
  for (const anim of document.getAnimations())
    if (anim.startTime !== 0 && anim.playState === 'running' && RARITY_ANIMS.has(anim.animationName)) anim.startTime = 0;
}
new MutationObserver(list => {
  // chỉ khi vừa thêm ô có phẩm (class r-…): chữ nhật ký, số liệu cập nhật liên tục thì bỏ qua
  const rarity = n => n.nodeType === 1 && (n.matches('[class*=" r-"],[class^="r-"]') || !!n.querySelector('[class*=" r-"],[class^="r-"]'));
  if (rarityAnimSync || !list.some(m => [...m.addedNodes].some(rarity))) return;
  rarityAnimSync = 1; queueMicrotask(syncRarityAnims);   // getAnimations() tự tính lại style, không cần đợi khung vẽ
}).observe(document.body, {childList: true, subtree: true});
/** Tên phẩm tô đúng màu phẩm: nền tối (bảng, nhật ký) dùng rc-*, thẻ giấy da dùng qc-* (màu đậm hơn để đọc rõ). */
const qWord = (r, card) => `<b class="${card ? 'qc' : 'qn'} ${card ? 'qc' : 'rc'}-${r}">${RARITY[r]?.n ?? r}</b>`;
/** Tô màu tên phẩm trong câu chữ từ server (nhật ký): "[cấp 25 · Đỏ]", "trang bị Vàng". */
// tên phẩm tiếng Việt và bản dịch (khi dùng English, nhật ký được dịch trước khi tô màu)
let qRe = null, qKey = null;
const qColorize = t => {
  if (!qRe){
    qKey = {};
    RAR_KEYS.forEach((k, i) => { qKey[Q_NAMES[i]] = k; qKey[window.tr(Q_NAMES[i])] = k; });
    const names = Object.keys(qKey).sort((a, b) => b.length - a.length).map(s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
    qRe = new RegExp(`(· |trang bị )(${names.join('|')})(?=\\]| |\\.|,|$)`, 'g');
  }
  return t.replace(qRe, (m, pre, n) => pre + qWord(qKey[n]));
};
const SET_COLOR = {'Thanh Vân': '#4fb0d8', 'Huyền Băng': '#3a7fc8', 'Xích Kim': '#c0631e', 'Huyền Long': '#fc246c'};          // màu riêng từng bộ trang bị (dòng ◆ Bộ … trong thẻ)
let sellOpen = false, sellArm = 0;
const canWear = it => (it.minRealm || 0) <= S.realm;
const slotsFor = it => !it.slot ? [] : [it.slot];
function itemStats(it){ return STAT_KEYS.filter(([k]) => it[k]).map(([k, n, u]) => `${n} +${u === '%' ? fmtRating(it[k]) : fmtDecimal(it[k])}${u}`); }
// tooltip khi rê chuột (dùng chung cho các tab)
/** Đan trong danh sách luyện chế và linh thảo: chuột phải mở Luyện chế · Đan dược. */
const rcCraft = it => !!it && (it.type === 'herb' || (it.type === 'pill' && !canUse(it)));
/** Icon chuột trong chú thích: MS_R chuột phải (nút phải tô sáng), MS_L2 nhấp đúp chuột trái; KBD(k): phím tô đậm. */
const MS_SVG = (side, tip) => `<svg class="ms" viewBox="0 0 12 16" role="img" aria-label="${tip}"><title>${tip}</title><rect x="1" y="1" width="10" height="14" rx="5" fill="none" stroke="currentColor" stroke-width="1.3"/><path d="M6 1v6" stroke="currentColor" stroke-width="1.1"/><path d="${side === 'r' ? 'M6.6 1.7A4.4 4.4 0 0 1 10.3 6.2V6.4H6.6Z' : 'M5.4 1.7A4.4 4.4 0 0 0 1.7 6.2V6.4H5.4Z'}" fill="currentColor"/></svg>`;
const MS_R = `<span class="msk r">${MS_SVG('r', 'Chuột phải')}</span>`;
const MS_L2 = `<span class="msk l2">${MS_SVG('l', 'Nhấp đúp')}<sup>2</sup></span>`;
const KBD = k => `<kbd>${k}</kbd>`;
/** Chú thích thao tác chuột cho ô trong túi (hiện ở dòng cuối tooltip). */
function rcHint(it){
  if (!it) return '';
  if (it.slot) return `${MS_R}/${MS_L2} mặc · kéo vào ô trang bị`;
  if (it.type === 'salvage') return `${KBD('B')} bán · chỉ dùng để bán lấy linh thạch`;
  if (it.type === 'chest') return `${MS_R}/${MS_L2} mở · ${KBD('Ctrl')}+${MS_R} mở hết`;
  if (it.useQiSeconds) return `${MS_R}/${MS_L2} dùng · ${KBD('Ctrl')}+${MS_R} dùng đến đầy tu vi`;
  if (it.id === 'ngo_dao_dan') return `${MS_R}/${MS_L2} dùng · ${KBD('Ctrl')}+${MS_R} dùng hết`;
  if (canUse(it)) return `${MS_R}/${MS_L2} dùng`;
  if (rcCraft(it)) return `${MS_R} mở Đan dược (${it.type === 'herb' ? 'luyện đan' : 'luyện chế'})`;
  return '';
}
/** Cực phẩm linh thạch: chưa có nguồn nhận, tạm hiện số dư (0) cạnh linh thạch. */
const HP_STONE = 'thuong_pham_linh_thach';
const hpStoneHtml = cls => ITEM[HP_STONE] ? `<span class="${cls}" data-tip="Cực phẩm linh thạch&#10;Chưa mở: sẽ có trong bản cập nhật sau">${itemImg(HP_STONE)}<b>${fmt(S.inventory[HP_STONE] || 0)}</b></span>` : '';
function formatItemGemEffectText(id, val, val2, fallbackText){
  const v = fmtRating(Math.round(val * 100) / 100);
  const v2 = fmtDecimal(Math.round((val2 || 0) * 100) / 100);
  switch (id) {
    case 'linh_ap': return `+${v}% sát thương kỹ năng`;
    case 'ho_the': return `Giảm ${v}% sát thương nhận`;
    case 'thien_loi': return `Mỗi lượt ${v}% cơ hội giáng sét ${v2}% công`;
    case 'pha_giap': return `Bỏ qua ${v}% phòng thủ quái`;
    case 'lien_kich': return `+${v}% liên kích`;
    case 'gem_base_atk': return `+${v}% công`;
    case 'gem_base_def': return `+${v}% thủ`;
    case 'gem_base_hp': return `+${v}% máu`;
    case 'gem_crit_resist': return `+${v}% kháng bạo kích`;
    case 'gem_accuracy': return `+${v}% chính xác`;
    case 'gem_dodge': return `+${v}% né tránh`;
    case 'gem_combo_resist': return `+${v}% kháng liên kích`;
    case 'gem_reflect_resist': return `+${v}% kháng phản đòn`;
    case 'tho_thuan': return `Khiên đầu trận +${v}% máu`;
    case 'dia_mach': return `+${v}% phản đòn`;
    case 'hoi_khi': return `Hồi ${v}% máu mỗi lượt`;
    case 'tinh_tam': return `Vào trận với ${v} nộ`;
    default: return fallbackText || '';
  }
}
function mergeItemGemEffects(baseEffects, gemEffects){
  const merged = (baseEffects || []).map(e => ({ ...e }));
  for (const fx of gemEffects) {
    if (!fx || !fx.id || fx.id === 'hap_linh') continue;
    const existing = merged.find(e => e.id === fx.id);
    if (existing) {
      existing.value = Math.round(((existing.value || 0) + (fx.value || 0)) * 100) / 100;
      existing.value2 = Math.max(existing.value2 || 0, fx.value2 || 0);
      existing.text = formatItemGemEffectText(existing.id, existing.value, existing.value2, existing.text);
    } else {
      const val = Math.round((fx.value || 0) * 100) / 100;
      const val2 = fx.value2 || 0;
      merged.push({
        id: fx.id,
        name: fx.name || fx.id,
        value: val,
        value2: val2,
        text: formatItemGemEffectText(fx.id, val, val2, fx.text),
        rarity: fx.rarity
      });
    }
  }
  return merged;
}
function itemTip(k, hint, slotId){
  const it = ITEM[k], lines = [it.name, `${it.itemLevel ? `Cấp ${it.itemLevel} · ` : ''}${rar(it).n} · ${it.kind || ''}${it.type === 'gem' ? ' · '+(it.gemCategory === 'defense' ? 'Phòng thủ' : 'Tấn công') : ''}`];
  if (it.slot) lines.push(`Yêu cầu cảnh giới: ${DATA.realms[it.minRealm||0].name}`);
  const targetSlot = slotId;
  const gemTotals = Object.fromEntries(STAT_KEYS.map(([key]) => [key, 0]));
  const gemEffects = [];
  if (targetSlot && S?.gemState?.slots?.[targetSlot]) {
    const sGems = S.gemState.slots[targetSlot] || [];
    const maxS = (S.gemState?.maxSockets?.[targetSlot] ?? 0);
    for (let i = 0; i < maxS; i++) {
      const gDef = sGems[i] ? ITEM[sGems[i]] : null;
      if (!gDef) continue;
      for (const [key] of STAT_KEYS) if (gDef[key]) gemTotals[key] += gDef[key];
      for (const fx of gDef.effects || []) {
        if (fx?.id === 'hap_linh') gemTotals.qiPct += (fx.value || 0);
        else if (fx?.id) gemEffects.push({ ...fx, rarity: gDef.rarity });
      }
    }
  }
  const sts = STAT_KEYS
    .map(([key, n, u]) => [n, u, Math.round(((it[key] || 0) + (gemTotals[key] || 0)) * 100) / 100])
    .filter(([, , val]) => val > 0)
    .map(([n, u, val]) => `${n} +${u === "%" ? fmtRating(val) : fmtDecimal(val)}${u}`);
  if (sts.length) lines.push(`<span class='rc-${it.rarity}'>${sts.join(' · ')}</span>`);
  if (it.special && it.type !== 'gem') lines.push(fmtNumberText(it.special));
  const mergedEffects = mergeItemGemEffects(it.effects, gemEffects);
  for (const effect of mergedEffects) lines.push(`<span class='rc-${effect.rarity || it.rarity}'>${fmtNumberText(effect.text)}</span>`);
  if (it.use) lines.push(fmtNumberText(it.use));
  if (it.type === 'herb') lines.push(...herbSrcLines(k));
  if (adminOn && it.type === 'chest') lines.push(...chestOddsLines(k));
  if (it.set) {
    const state = S?.gearEffects?.sets?.find(s => s.id === it.setId);
    lines.push(`Bộ ${it.set}: ${state?.count || 0}/10 món đang mặc`);
    const def = (DATA.equipmentSets || []).find(s => s.id === it.setId);
    for (const m of def?.milestones || []) lines.push(`${state?.active?.includes(m.count) ? '✓' : '○'} ${m.count} món: ${m.bonuses.map(b => b.text).join(' · ')}`);
  }
  if (targetSlot && S?.gemState?.slots?.[targetSlot]) {
    const sGems = S.gemState.slots[targetSlot] || [];
    const maxS = (S.gemState?.maxSockets?.[targetSlot] ?? 0);
    const filledCount = sGems.filter(Boolean).length;
    if (slotId || filledCount > 0) {
      lines.push(`◆ Khảm Tinh Thạch (${filledCount}/${maxS}):`);
      for (let i = 0; i < maxS; i++) {
        const gId = sGems[i];
        if (gId && ITEM[gId]) {
          const gDef = ITEM[gId];
          const rawStat = gDef.special || (gDef.atk ? `+${fmt(gDef.atk)} Công` : gDef.def ? `+${fmt(gDef.def)} Thủ` : `+${fmt(gDef.hp)} Máu`);
          const gStat = rawStat.replace(/^(?:Khảm nạm|Khảm Tinh Thạch):\s*/i, '');
          lines.push(`<span class='rc-${gDef.rarity}'>${gStat}</span>`);
        } else {
          lines.push(`  • Trống`);
        }
      }
    }
  }
  lines.push(fmtNumberText(it.desc));
  if (hint) lines.push(hint);
  return lines.join('\n').replace(/"/g, '&quot;');
}
/** Nguồn linh thảo; xác suất từng cây phụ thuộc các cây đã mở trong nhóm rơi chung. */
function herbSrcLines(k){
  const src = (DATA.herbDrops || []).filter(h => h.item === k); if (!src.length) return [];
  return ['Nơi rơi (nhóm linh thảo 30%; xem tỉ lệ từng cây trên map):', ...src.map(h => `• ${h.chapters.map(c => `Chương ${c + 1} · ${DATA.chapters?.[c]?.name || ''}`).join(', ')} · ${h.minArea > 1 ? `từ ải ${h.minArea}` : 'mọi ải thường'}${h.max > 1 ? ` (${h.min}–${h.max} cây gốc)` : ''}`)];
}
// ---- admin: tỉ lệ phần thưởng khi mở rương (S.chestOdds, tính ở server đúng như lúc mở) ----
const Q_NAMES = RAR_KEYS.map(k => RARITY[k].n);
const topPct = q => q.slice(3).reduce((a, x) => a + (x || 0), 0);          // % ra đồ Địa phẩm trở lên
const pctS = v => (Math.round(v * 100) / 100) + '%';
function chestOdds(k){ return (S.chestOdds || []).find(o => o.chest === k); }
/** Tên món gốc (pháp bảo / trang bị) kèm cấp, lấy từ món sinh ra cùng gốc. */
const gearName = base => { const v = DATA.items.find(i => i.base === base); return v ? `${v.name} (cấp ${v.itemLevel})` : base; };
function chestOddsLines(k){
  const o = chestOdds(k); if (!o) return [];
  const pt = o.treasure.reduce((a, x) => a + x.pct, 0), pg = o.gear.reduce((a, x) => a + x.pct, 0);
  return [`[Admin] Tỉ lệ mỗi lần mở`,
    `Phẩm khi nhận trang bị/pháp bảo: ${o.quality.map((q, i) => `${Q_NAMES[i]} ${pctS(q)}`).join(' · ')}`,
    `Trang bị (gồm pháp bảo) ${pctS(pt + pg)}${o.pill > 0 ? ` · ${ITEM[o.pillItem]?.name || 'Đan'} ${pctS(o.pill)} (${o.pillMin}–${o.pillMax} viên)` : ''}`,
    `Linh thảo ${pctS(o.herb || 0)} (${o.herbMin || 1}–${o.herbMax || 1} món) · Ngộ Đạo Đan ${pctS(o.enlightenment || 0)} (1 viên, từ Nguyên Anh)`,
    ...(o.pets || []).map(x=>`${ITEM[x.item]?.name || x.item}: ${pctS(x.pct)}`),
    ...(o.manuals || []).map(x=>`${ITEM[x.item]?.name || x.item}: ${pctS(x.pct)}`),
    ...(o.gems || []).map(x=>`${ITEM[x.item]?.name || x.item}: ${pctS(x.pct)}`),
    `Ra đồ Địa trở lên: ${pctS((pt + pg) * topPct(o.quality) / 100)}`];
}
function chestOddsHtml(k){
  const o = chestOdds(k); if (!o) return '';
  const pt = o.treasure.reduce((a, x) => a + x.pct, 0), pg = o.gear.reduce((a, x) => a + x.pct, 0);
  const list = xs => xs.map(x => `${gearName(x.item)} <b>${pctS(x.pct)}</b>`).join(' · ') || '—';
  return `<div class="iv-odds"><p class="t">Tỉ lệ mỗi lần mở (admin)</p>
    <p>Phẩm khi nhận trang bị/pháp bảo: ${o.quality.map((q, i) => `<span class="rc-${RAR_KEYS[i]}">${Q_NAMES[i]} ${pctS(q)}</span>`).join(' · ')} · <i>ra đồ Địa trở lên ${pctS((pt + pg) * topPct(o.quality) / 100)}</i></p>
    <p><b>Trang bị (gồm pháp bảo) ${pctS(pt + pg)}</b>: ${list(o.gear.concat(o.treasure))}</p>
    ${o.pill > 0 ? `<p><b>${ITEM[o.pillItem]?.name || 'Đan dược'} ${pctS(o.pill)}</b>: ${o.pillMin}–${o.pillMax} viên</p>` : ''}
    <p><b>Linh thảo ${pctS(o.herb || 0)}</b>: ${o.herbMin || 1}–${o.herbMax || 1} món · ${(o.herbs || []).map(x=>`${ITEM[x.item]?.name || x.item} ${pctS(x.pct)}`).join(' · ')}</p>
    <p><b>Ngộ Đạo Đan ${pctS(o.enlightenment || 0)}</b>: 1 viên · chỉ từ Nguyên Anh</p>
    ${(o.pets || []).map(x=>`<p><b>${ITEM[x.item]?.name || x.item} ${pctS(x.pct)}</b></p>`).join('')}
    ${(o.manuals || []).map(x=>`<p><b>${ITEM[x.item]?.name || x.item} ${pctS(x.pct)}</b> · Công pháp</p>`).join('')}
    <p><b>Tinh Thạch ${pctS((o.gems || []).reduce((sum,x)=>sum+x.pct,0))}</b></p>
    ${(o.gems || []).map(x=>`<p>${ITEM[x.item]?.name || x.item} ${pctS(x.pct)}</p>`).join('')}</div>`;
}
function itemImg(k, cls = '', detail = false){
  const it = ITEM[k];
  const ticketClass = k === 've_tai_xiu' ? ' ticket-premium ticket-gear' : k === 've_linh_thu' ? ' ticket-premium ticket-pet' : '';
  const imageSrc = detail && k === 'thu_dan_thuong_co' ? 'img/pet/egg_thuong_co_hd.png' : it.icon;
  const eggClass = k === 'thu_dan_thuong_co' ? ' ancient-egg-art' : '';
  return it.icon ? `<img class="px ${cls}${ticketClass}${eggClass}" src="${imageSrc}" alt="" style="${it.tint ? `filter:${it.tint}` : ''}">` : icon(k);
}
const LOCK_SVG = '<svg viewBox="0 0 24 24" class="lk"><rect x="5" y="11" width="14" height="10" rx="1"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg>';

let invCat = 'all', invSel = null, compareOn = true, dropArm = 0;
// ---- Trang phục (skin): nút trường bào góc trên cột nhân vật → bảng chọn trang phục ----
let wardOpen = false;
const WARD_ICON = '<img src="img/ui_trang_phuc.png" width="24" height="24" alt="" aria-hidden="true">';
const wardrobeThumb = src => src?.replace(/_stand\.png$/, '_thumb.webp');
async function changeWardrobeGender(gender){
  if (busy || gender === skinGender()) return;
  const buttons = () => document.querySelectorAll('#pane-inv [data-gender]');
  buttons().forEach(button => { button.disabled = true; });
  try { await act('gender?gender=' + encodeURIComponent(gender)); }
  finally { buttons().forEach(button => { button.disabled = busy; }); }
}
function wardrobeHtml(){
  const online = typeof onlineSession !== 'undefined' && onlineSession.enabled;   // online: ẩn skin nữ chưa mở khóa; local vẫn thấy hết
  const gender = skinGender(), skins = (DATA.skins || []).filter(s => (s.gender || 'male') === gender && !(online && s.gender === 'female' && !skinOpen(s)));
  const wearing = curSkin() || (DATA.skins || []).find(s => !s.unlockItem && s.heroStand === realm().heroStand);
  const cur = wearing?.id || '';
  const card = (id, name, img, note, locked) => `<button class="wd-card ${cur === id ? 'on' : ''} ${locked ? 'lock' : ''}" data-skin="${id}" ${locked ? 'disabled' : ''} aria-pressed="${cur === id}">
      <span class="wd-fig">${img ? `<img src="${wardrobeThumb(img)}" alt="" decoding="async">` : '<b>⟳</b>'}</span><b class="wd-nm">${name}</b><small>${note}</small></button>`;
  const list = skins.map(s => { const shut = !skinOpen(s), locked = !skinWearable(s);
      // trang phục cần rương: chưa mở rương thì khóa, ghi tên rương cần mở
      return card(s.id, s.name, s.heroStand, shut ? `${LOCK_SVG} Mở ${ITEM[s.unlockItem]?.name || 'rương trang phục'}` : locked ? `${LOCK_SVG} Đạt ${DATA.realms[s.minRealm].name}` : cur === s.id ? 'Đang mặc' : s.unlockItem ? 'Đã mở khóa' : DATA.realms[s.minRealm].name, locked); });
  return `<button class="iv-ward-btn ${wardOpen ? 'on' : ''}" id="wardBtn" aria-label="Trang phục" aria-expanded="${wardOpen}" ${wardOpen ? '' : 'data-tip="Trang phục&#10;Đổi ngoại hình nhân vật ở Hành trang, Đột phá và màn hình chính"'}>${WARD_ICON}</button>
    <div class="iv-ward" ${wardOpen ? '' : 'hidden'} role="dialog" aria-label="Trang phục">
      <p class="wd-hd"><b>Trang phục</b><span>Chỉ đổi ngoại hình, không đổi chỉ số</span><button class="wd-x" data-wardclose aria-label="Đóng">×</button></p>
      <div class="wd-genders" role="group" aria-label="Giới tính nhân vật">${[['male','Nam'],['female','Nữ']].map(([id,label]) => `<button type="button" data-gender="${id}" aria-pressed="${gender === id}" ${busy ? 'disabled' : ''}>${label}</button>`).join('')}</div>
      <div class="wd-list">${list.join('')}</div></div>`;
}
// ---- chọn nhiều món để bán: Ctrl + nhấp chọn từng món, Shift + nhấp chọn một dải, hoặc bật chế độ "Chọn nhiều" (nhấp là chọn) ----
const multiSel = new Map();          // chỉ số ô trong túi → id món (chỉ món bán được)
let msMode = false, msAnchor = -1, msArm = 0;
function msValidate(list){ for (const [i, k] of [...multiSel]) if (list[i]?.k !== k) multiSel.delete(i); }
function msSummary(){
  const pool = {}; let gain = 0, n = 0, hi = false;
  for (const k of multiSel.values()){ pool[k] = (pool[k] || 0) + 1; gain += ITEM[k].sell; n++; if (RAR_KEYS.indexOf(ITEM[k].rarity) >= 2) hi = true; }
  return {pool, gain, n, hi};
}
/** Bán nhanh trong Hành trang (chọn nhiều, bán theo phẩm) chỉ áp cho trang bị; đan dược bán ở Luyện chế · Bán. */
const gearSell = k => !!(ITEM[k]?.sell && ITEM[k].slot);
function msToggle(i, k){ if (!gearSell(k)) return; if (multiSel.has(i)) multiSel.delete(i); else multiSel.set(i, k); msAnchor = i; msArm = 0; }
function msRange(i){
  const list = bagList(), a = msAnchor >= 0 ? msAnchor : invSel?.src === 'bag' ? invSel.idx : i;
  for (let j = Math.min(a, i); j <= Math.max(a, i); j++){ const c = list[j]; if (c && gearSell(c.k)) multiSel.set(j, c.k); }
  msAnchor = i; msArm = 0;
}
function msPage(){
  const list = bagList(), first = bagPage * BAG_PER_PAGE;
  for (let j = first; j < Math.min(list.length, first + BAG_PER_PAGE); j++) if (gearSell(list[j].k)) multiSel.set(j, list[j].k);
  msArm = 0;
}
function msClear(){ multiSel.clear(); msAnchor = -1; msArm = 0; }
/** Bán các món đã chọn; có món từ Huyền cấp trở lên thì bấm 2 lần để xác nhận. */
function msSell(){
  const s = msSummary(); if (!s.n) return;
  if (s.hi && Date.now() - msArm >= 3000){ msArm = Date.now(); renderInv(); setTimeout(() => { if (msArm && Date.now() - msArm >= 3000){ msArm = 0; renderInv(); } }, 3100); return; }
  const q = 'sell-bulk?max=9&pick=true&items=' + encodeURIComponent(Object.entries(s.pool).map(([k, c]) => k + ':' + c).join(','));
  msClear(); act(q);
}
function msBarHtml(){
  if (!multiSel.size && !msMode) return '';
  const s = msSummary(), armed = msArm && Date.now() - msArm < 3000;
  return `<div class="iv-msbar" role="toolbar" aria-label="Bán nhiều món">
    <span class="sp">${s.n ? `Đã chọn <b>${s.n}</b> món · <b>+${fmt(s.gain)}</b> linh thạch` : 'Nhấp vào món để chọn · Shift + nhấp chọn một dải'}</span>
    <button data-ms="page" data-tip="Chọn mọi món bán được ở trang đang xem">Chọn cả trang</button>
    ${msMode ? `<button data-ms="done" data-tip="Thoát chế độ chọn từng món · Esc">Xong</button>` : ''}
    ${s.n ? `<button data-ms="clear" data-tip="Bỏ chọn tất cả · Esc">Bỏ chọn</button>
    <button data-ms="sell" class="${armed ? 'arm' : 'go'}" data-tip="Bán các món đã chọn · phím B${s.hi ? '&#10;Có đồ từ Lam trở lên: bấm 2 lần để xác nhận' : ''}">${armed ? 'Bấm lần nữa để bán' : `Bán ${s.n} món`}</button>` : ''}
  </div>`;
}
try { invCat = localStorage.getItem('van-dao-inv-cat') || 'all'; } catch(e){}

// Gems keep their save storage but appear alongside ordinary inventory items.
const bagCount = k => ITEM[k]?.type === 'gem' ? (S.gemState?.vault?.[k] || 0) : (S.inventory[k] || 0);
/** Xếp danh sách id theo kiểu sắp xếp đang chọn. */
function sortBagIds(ids){
  const sk = GEAR_SORTS[gearSort].key;
  const gearFirst = gearSort === 'atk' || gearSort === 'hp' || gearSort === 'def' || gearSort === 'grade';
  return ids.sort((a, b) => {
    const A = ITEM[a], B = ITEM[b];
    if (gearSort !== 'name' && !!A.slot !== !!B.slot) return (A.slot ? -1 : 1) * (gearFirst ? 1 : -1);
    const x = sk(A), y = sk(B);
    return (typeof x === 'string' ? x.localeCompare(y, 'vi') : (x - y)) || A.name.localeCompare(B.name, 'vi');
  });
}
/** Thứ tự ô trong túi (lưu theo nhân vật): giữ nguyên giữa các lần đồng bộ, vật phẩm mới nhận nối vào cuối.
 *  Chỉ sắp xếp lại khi người chơi chọn kiểu sắp xếp hoặc bấm Sắp xếp (resetBagOrder). */
let bagOrder = null, bagOrderFor = null;
const bagOrderKey = () => 'van-dao-bag-order-' + (S.id || '');
function resetBagOrder(){
  bagOrder = []; bagOrderFor = S.id;
  try { localStorage.removeItem(bagOrderKey()); } catch(e){}
}
/** Linh thạch và Cực phẩm linh thạch là tiền tệ: hiện ở thanh trên cùng, không chiếm ô và không nằm trong lưới hành trang (khớp GameService.BagUsed). */
const BAG_HIDDEN = new Set(['linh_thach', 'thuong_pham_linh_thach']);
function bagList(){
  const owned = DATA.items.map(i => i.id).filter(k => !BAG_HIDDEN.has(k) && (bagCount(k)||0) > 0);
  if (bagOrderFor !== S.id){
    bagOrderFor = S.id; bagOrder = [];
    try { const v = JSON.parse(localStorage.getItem(bagOrderKey()) || '[]'); if (Array.isArray(v)) bagOrder = v; } catch(e){}
  }
  const has = new Set(owned), keep = bagOrder.filter(k => has.has(k) && ITEM[k]);
  const inOrder = new Set(keep), fresh = owned.filter(k => !inOrder.has(k));
  // túi trống thứ tự (lần đầu / vừa bấm Sắp xếp): xếp toàn bộ; còn lại: món mới nối cuối, giữa chúng theo kiểu đang chọn
  const next = keep.length ? keep.concat(sortBagIds(fresh)) : sortBagIds(fresh);
  if (next.length !== bagOrder.length || next.some((k, i) => k !== bagOrder[i])){
    bagOrder = next;
    try { localStorage.setItem(bagOrderKey(), JSON.stringify(next)); } catch(e){}
  }
  // lọc theo danh mục; trang bị: mỗi món một ô; vật phẩm: một ô kèm số lượng
  return bagOrder.filter(k => invCat === 'all' || catOf(ITEM[k]) === invCat)
    .flatMap(k => ITEM[k].slot ? Array.from({length: bagCount(k)}, (_, copy) => ({k, n: 1, copy})) : [{k, n: bagCount(k)}]);
}
function bagGemPips(k, copy){
  const gems=(S.gemState?.bagSlots?.[k]?.[copy] || []).filter(id=>id&&ITEM[id]);
  if(!gems.length)return '';
  return `<span class="iv-pips">${gems.map(id=>`<span class="iv-pip filled r-${ITEM[id].rarity}"></span>`).join('')}</span>`;
}
// Lực chiến: cùng công thức với CombatRatings.Power (server) — Công×2 + Thủ×3 + Máu×0,4 + tổng 8 chỉ số đối kháng×4.
const POWER_RATINGS = ['crit', 'critResist', 'accuracy', 'dodge', 'combo', 'comboResist', 'reflect', 'reflectResist'];
function power(st){ return Math.round(st.atk*2 + st.def*3 + st.hp*.4 + POWER_RATINGS.reduce((n, k) => n + (st[k] || 0), 0)*4); }
/** Bậc màu ô Lực chiến (chỉ hiển thị): 1 ngọc bích < 100K ≤ 2 bạch kim < 1M ≤ 3 hoàng kim < 100M ≤ 4 xích kim < 10B ≤ 5 tử kim. */
function powTier(p){ return p >= 1e10 ? 5 : p >= 1e8 ? 4 : p >= 1e6 ? 3 : p >= 1e5 ? 2 : 1; }

/** Vẽ lại Hành trang mà giữ nguyên vị trí cuộn (cột nhân vật, túi, khung thông tin) và trạng thái mở của mục "Nguồn chỉ số và giới hạn": renderInv chạy lại mỗi lần trạng thái đổi. */
function drawInventoryIdle(g, image, shape, t, reduced){
  const {fw,fh,ax,ay,bodyHeight} = shape, scale = 278 / bodyHeight;
  const left = 160 - ax * scale, top = 306 - ay * scale;
  g.save(); g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  if (reduced){
    g.drawImage(image,0,0,fw,fh,left,top,fw*scale,fh*scale);
  } else {
    // Warp one stable pose continuously; never dissolve between mismatched faces.
    // Both offsets taper to zero at the boots, preserving the ground anchor.
    const breath = Math.sin(t * Math.PI * 2 / 3.6);
    const wind = Math.sin(t * Math.PI * 2 / 7.2);
    const point = y => {
      const up = Math.max(0,Math.min(1,(ay-y)/bodyHeight));
      const weight = Math.max(0,Math.min(1,(up-.14)/.86));
      return {x:left + wind * 8 * Math.sin(Math.PI*weight),
        y:top + y*scale - breath * 3.6 * weight};
    };
    for(let y=0;y<fh;y+=3){
      const sh=Math.min(3,fh-y), a=point(y), b=point(y+sh);
      const sy=(b.y-a.y)/sh;
      // Sample the full pose through each overlapping clip, so filtering does
      // not introduce transparent scanlines along individual strip edges.
      g.save(); g.beginPath();
      g.rect(a.x-1,a.y-.7,fw*scale+2,b.y-a.y+1.4); g.clip();
      g.drawImage(image,0,0,fw,fh,a.x,a.y-y*sy,fw*scale,fh*sy);
      g.restore();
    }
  }
  g.restore();
}
/** Trận pháp Hành trang: 10 bậc theo cảnh giới, quay thuận chiều kim đồng hồ sau nhân vật. */
function drawInventoryFormation(g, realmIdx, time, reduced=false){
  const tier=Math.max(0,Math.min(9,Math.floor(Number(realmIdx)||0)))+1;
  const image=skImg(`img/fx/inventory-formations-v1/tier-${String(tier).padStart(2,'0')}.png`);
  g.save();
  try{
    g.setTransform(1,0,0,1,0,0);
    g.globalAlpha=1;g.globalCompositeOperation='source-over';
    g.clearRect(0,0,320,320);
    if(!image)return false;
    const seconds=reduced?0:(Number.isFinite(time)?Math.max(0,time):0);
    g.translate(160,154);
    // Canvas angles increase clockwise; one complete revolution every 40 seconds.
    g.rotate((seconds%40)*Math.PI*2/40);
    g.globalAlpha=.78;
    g.drawImage(image,-143,-143,286,286);
    return true;
  }finally{g.restore();}
}
/** Hiệu ứng nền cũ dùng cho phần xem nhân vật trên Bảng xếp hạng. */
function drawStandAura(g, hero, realmIdx, t){
  if (typeof BT_PAL === 'undefined') return;
  const W = 320, H = 320, realmDef = DATA.realms[realmIdx] || DATA.realms[0];
  if (typeof drawInventoryRealmFx === 'function'){
    g.setTransform(1,0,0,1,0,0);g.globalAlpha=1;g.clearRect(0,0,W,H);
    drawInventoryRealmFx(g,realmIdx,t,reduce);
    return;
  }
  const pal = BT_PAL[realmDef.id] || BT_PAL.luyen_khi;
  const pulse = reduce ? .5 : .5 + .5 * Math.sin(t * 1.3);
  g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.clearRect(0, 0, W, H);
  const cx = W / 2, cy = H * .48, R = W * (.72 + .04 * pulse);   // vòng sáng mở rộng gần hết khung
  const gr = g.createRadialGradient(cx, cy, 0, cx, cy, R);
  gr.addColorStop(0, pal.glow); gr.addColorStop(.45, pal.glow + '99'); gr.addColorStop(1, 'transparent');
  g.globalAlpha = .62 + .2 * pulse; g.fillStyle = gr; g.fillRect(0, 0, W, H);
  g.globalAlpha = 1;
  if(typeof drawSkinFx==='function')drawSkinFx(g,hero,'back',160,306,278,t,'idle',0,reduce);
}
/** Nhân vật đứng của trang phục hero (hoạt ảnh + hiệu ứng phía trước) vẽ chồng lên g; false = chưa có hoạt ảnh, dùng ảnh tĩnh. */
function drawStandHero(g, hero, realmIdx, t, ambient=true){
  const profile = SKIN_ACTOR_SHEETS[hero];
  const thanhSam = hero === 'img/hero_luyenkhi.png';
  const image = thanhSam ? skImg('img/skin/luyenkhi_inventory_breeze.png')
    : profile && !profile.static ? skImg(profile.inventorySrc || `img/skin/${profile.id}_actions.png`) : null;
  const sheetDrawn = typeof drawInventorySprite === 'function' && drawInventorySprite(g,hero,t,reduce);
  const drawn = sheetDrawn || !!image;
  if (!sheetDrawn && image){
    drawInventoryIdle(g,image,thanhSam ? {fw:384,fh:320,ax:192,ay:308,bodyHeight:210}
      : {fw:480,fh:440,ax:240,ay:420,bodyHeight:profile.bodyHeight},t,reduce);
  }
  if(ambient&&typeof drawSkinFx==='function')drawSkinFx(g,hero,'front',160,306,278,t,'idle',0,reduce);
  if(sheetDrawn && typeof drawInventoryWeaponFx==='function')drawInventoryWeaponFx(g,hero,t,reduce,realmIdx);
  return drawn;
}
/** Khung nhân vật Hành trang cao/hẹp (bố cục v2): vẽ ở toạ độ logic 320×320 vào bộ đệm, rồi chép lên canvas hiển thị
 *  theo đúng kích thước khung với tỉ lệ đều (trước đây canvas 320×320 bị kéo giãn → nhân vật méo/vỡ). */
const IV_BUF = {}, IV_VIEW_W = 220;   // bề ngang logic tối thiểu luôn hiện đủ (nhân vật + tay áo / kiếm)
function ivBuffer(key){ return IV_BUF[key] ||= Object.assign(document.createElement('canvas'), {width: 320, height: 320}); }
function blitInventoryCanvas(canvas, buf){
  const dpr = devicePixelRatio || 1, w = Math.round(canvas.clientWidth * dpr), h = Math.round(canvas.clientHeight * dpr);
  if (!w || !h) return;
  if (canvas.width !== w || canvas.height !== h){ canvas.width = w; canvas.height = h; }
  const g = canvas.getContext('2d'), k = Math.min(h / 320, w / IV_VIEW_W);
  g.setTransform(1, 0, 0, 1, 0, 0); g.clearRect(0, 0, w, h);
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  g.drawImage(buf, (w - 320 * k) / 2, (h - 320 * k) / 2, 320 * k, 320 * k);
}
function drawInventoryAura(t){
  const canvas = $('ivAura'); if (!S || !canvas || !canvas.offsetParent) return;
  const buf = ivBuffer('aura'), g = buf.getContext('2d');
  drawInventoryFormation(g, S.realm, t, reduce);
  if(typeof drawInventoryFootRealmFx==='function')drawInventoryFootRealmFx(g, S.realm, t, reduce);
  blitInventoryCanvas(canvas, buf);
}
function drawInventoryAvatar(t){
  drawInventoryAura(t);
  const canvas = $('ivAvatar');
  if (!S || !canvas || !canvas.offsetParent){
    if ($('ivZoomPop')?.hidden === false){ heroZoomHover = heroZoomPinned = false; heroZoom(false); }   // rời Hành trang khi đang xem lớn
    return;
  }
  const buf = ivBuffer('avatar'), g = buf.getContext('2d'), still = canvas.previousElementSibling;
  g.setTransform(1,0,0,1,0,0); g.clearRect(0,0,320,320);
  const drawn = drawStandHero(g, skinArt('hero'), S.realm, t, false);
  blitInventoryCanvas(canvas, buf);
  canvas.style.visibility = drawn ? 'visible' : 'hidden';
  if (still) still.hidden = drawn;
  drawHeroZoom(buf, still, drawn);
}
/** Khung xem lớn (nút +) chép đúng khung đứng mỗi khung hình: nền hiệu ứng cảnh giới (#ivAura) rồi nhân vật đang chuyển động (#ivAvatar),
 *  ảnh tĩnh khi chưa có hoạt ảnh. Cùng toạ độ 320×320 nên không phải vẽ lại nhân vật lần hai. */
function drawHeroZoom(avatar, still, drawn){
  const pop = $('ivZoomPop'); if (!pop || pop.hidden) return;
  const ready = still?.complete && still.naturalWidth;
  if (!drawn && !ready) return;   // ảnh tĩnh mới (sau khi vẽ lại Hành trang) chưa tải: giữ khung hình trước, không xoá trắng
  const g = pop.querySelector('canvas').getContext('2d'), aura = IV_BUF.aura;
  g.clearRect(0, 0, 320, 320);
  if (aura) g.drawImage(aura, 0, 0);
  if (drawn) g.drawImage(avatar, 0, 0);
  else {   // .hero.ref: cao 138/150 khung, căn giữa
    const h = 320 * 138 / 150, w = Math.min(320, h * still.naturalWidth / still.naturalHeight);
    g.drawImage(still, (320 - w) / 2, (320 - h) / 2, w, h);
  }
}
function renderInv(){
  const pane = $('pane-inv'), host = pane?.closest('aside.book'), hostTop = host?.scrollTop, SCROLLERS = '.iv-char-in, #ivGrid, .iv-cats';
  const keep = pane ? [pane.scrollTop, [...pane.querySelectorAll(SCROLLERS)].map(el => [el.scrollTop, el.scrollLeft])] : null;
  const fxStates = new Map([...(pane?.querySelectorAll('details.iv-fx') || [])].map(el => [el.classList.contains('iv-detail') ? 'detail' : el.classList.contains('iv-divine') ? 'divine' : 'sources', el.open]));
  renderInvInner();
  pane?.querySelectorAll('details.iv-fx').forEach(el => {
    const key = el.classList.contains('iv-detail') ? 'detail' : el.classList.contains('iv-divine') ? 'divine' : 'sources';
    if (fxStates.has(key)) el.open = fxStates.get(key);
  });
  fitBagGrid();
  if (host) host.scrollTop = hostTop;
  if (keep){ pane.scrollTop = keep[0]; pane.querySelectorAll(SCROLLERS).forEach((el, i) => { const k = keep[1][i]; if (k){ el.scrollTop = k[0]; el.scrollLeft = k[1]; } }); }
  if ($('ivZoomPop')?.hidden === false) heroZoom(heroZoomHover || heroZoomPinned);
}
/** Nút + ở khung nhân vật: rê chuột / focus (chạm trên màn cảm ứng) hiện khung nhân vật phóng to, giữ chuyển động và nền phía sau
 *  (drawHeroZoom chép lại mỗi khung hình). Khung nổi gắn vào body để không bị cột nhân vật (tự cuộn) cắt mất. */
let heroZoomPinned = false, heroZoomHover = false;   // bấm ghim (màn cảm ứng) · chuột / focus đang ở nút +
function heroZoom(show){
  let pop = $('ivZoomPop');
  const btn = show ? $('ivZoom') : null;
  if (!btn || !btn.offsetParent){ heroZoomPinned = false; if (pop) pop.hidden = true; return; }
  if (!pop){
    pop = document.createElement('div'); pop.id = 'ivZoomPop'; pop.className = 'iv-zoom-pop'; pop.setAttribute('aria-hidden', 'true');
    pop.innerHTML = '<canvas width="320" height="320"></canvas>'; document.body.appendChild(pop);
  }
  pop.hidden = false;
  const r = btn.closest('.iv-stage').getBoundingClientRect(), w = pop.offsetWidth, h = pop.offsetHeight;
  const x = r.right + 10 + w <= innerWidth - 8 ? r.right + 10 : Math.max(8, r.left - 10 - w);   // bên phải khung nhân vật, hết chỗ thì sang trái
  pop.style.left = x + 'px'; pop.style.top = Math.max(8, Math.min(innerHeight - h - 8, r.top + r.height / 2 - h / 2)) + 'px';
}
/** Cửa sổ Tu luyện: ô túi nhỏ cỡ ~BAG_CELL px, số cột × hàng tự tăng cho kín khung (không còn lưới 8 × 6 ô to với khoảng trống hai bên).
 *  Ô giãn nhẹ để lấp đủ bề ngang; số ô mỗi trang (BAG_PER_PAGE) = cột × hàng. Màn hẹp (≤ 620px) giữ lưới co theo bề ngang. */
const BAG_CELL = 58, BAG_GAP = 3;
function bagCellSize(width){
  const cols = Math.max(6, Math.floor((width + BAG_GAP) / (BAG_CELL + BAG_GAP)));
  return {cols, cell: Math.max(32, Math.min(72, Math.floor((width - (cols - 1) * BAG_GAP) / cols)))};
}
function fitBagGrid(){
  const g = $('ivGrid'); if (!g) return;
  if (!document.body.classList.contains('book-fit') || innerWidth <= 620 || !g.clientHeight){ g.style.removeProperty('--cell'); g.style.removeProperty('--cols'); return; }
  const w = g.clientWidth - 4, h = g.clientHeight - 4;
  const {cols, cell} = bagCellSize(w);
  const rows = Math.max(3, Math.floor((h + BAG_GAP) / (cell + BAG_GAP)));
  g.style.setProperty('--cell', cell + 'px'); g.style.setProperty('--cols', cols);
  const per = cols * rows;
  if (per !== BAG_PER_PAGE){                                  // đổi số ô / trang: giữ món đầu trang đang xem rồi vẽ lại
    const first = bagPage * BAG_PER_PAGE;
    BAG_PER_PAGE = per; bagPage = Math.floor(first / per);
    requestAnimationFrame(() => renderInv());
  }
}
if (window.ResizeObserver) new ResizeObserver(() => fitBagGrid()).observe($('pane-inv'));
function renderInvInner(){
  const st = S.stats, unlocked = new Set(S.equipSlots), r = realm();
  if (invSel?.src === 'bag' && !(bagCount(invSel.k) > 0)) invSel = null;
  if (invSel?.src === 'slot' && S.equipment[invSel.slot] !== invSel.k) invSel = null;
  const sel = invSel ? ITEM[invSel.k] : null;
  const compat = new Set(sel && invSel.src === 'bag' ? slotsFor(sel).filter(s => unlocked.has(s)) : []);

  // ---- ô trang bị quanh nhân vật ----
  const slotHtml = id => {
    const sd = DATA.slots.find(s => s.id === id), k = S.equipment[id];
    if (!unlocked.has(id)){
      const opens = DATA.realms.find(x => x.equipSlots.includes(id));
      return `<div class="iv-slot locked" data-tip="${sd.name}&#10;Mở ở cảnh giới ${opens ? opens.name : 'cao hơn'}">${LOCK_SVG}<span class="nm">${sd.name}</span></div>`;
    }
    const cls = `${compat.has(id) ? 'compat' : ''} ${invSel?.src === 'slot' && invSel.slot === id ? 'sel' : ''}`;

    const maxS = (S.gemState?.maxSockets?.[id] ?? 0);
    const sGems = S.gemState?.slots?.[id] || [];
    let pipsHtml = '';
    for (let i = 0; i < maxS; i++) {
      const gId = sGems[i];
      if (gId && ITEM[gId]) {
        pipsHtml += `<span class="iv-pip filled r-${ITEM[gId].rarity || 'common'}" title="${ITEM[gId].name}"></span>`;
      }
    }
    const pipsContainer = pipsHtml ? `<div class="iv-pips" data-tip="Lỗ khảm Tinh Thạch&#10;Nhấp để mở giao diện Khảm Tinh Thạch">${pipsHtml}</div>` : '';

    if (k) return `<button class="iv-slot has r-${ITEM[k].rarity} ${cls}" data-slot="${id}" data-k="${k}" data-card="slot" draggable="true" aria-label="${sd.name}: ${ITEM[k].name}" data-tip="${itemTip(k, `${MS_R}/${MS_L2} tháo · kéo vào hành trang&#10;Shift + nhấp mở Khảm Tinh Thạch`, id)}">${itemImg(k)}<span class="lv">${itemLv(ITEM[k])}</span>${pipsContainer}</button>`;
    return `<button class="iv-slot empty ${cls}" data-slot="${id}" aria-label="${sd.name} (trống)" data-tip="${sd.name}&#10;Trống · kéo trang bị vào đây&#10;Nhấp mở Khảm Tinh Thạch"><span class="nm">${sd.name}</span>${pipsContainer}</button>`;
  };

  // ---- hành trang ----
  const list = bagList();
  msValidate(list);
  const cap = S.bagCap, used = S.bagUsed;
  const shown = invCat === 'all' ? Math.max(cap, list.length) : Math.max(32, Math.ceil(list.length / 8) * 8);
  const pages = Math.max(1, Math.ceil(shown / BAG_PER_PAGE));
  if (invSel?.src === 'bag' && invSel._follow){ bagPage = Math.floor(invSel.idx / BAG_PER_PAGE); delete invSel._follow; }   // phím mũi tên đi qua trang khác
  bagPage = Math.max(0, Math.min(pages - 1, bagPage));
  const first = bagPage * BAG_PER_PAGE;
  const cells = Array.from({length: BAG_PER_PAGE}, (_, j) => {
    const i = first + j, c = list[i];
    if (i >= shown) return '<div class="iv-cell empty void"></div>';            // giữ lưới đủ 7 hàng ở trang cuối
    if (!c) return `<div class="iv-cell empty ${invCat === 'all' && i >= cap ? 'over' : ''}"></div>`;
    const it = ITEM[c.k], isSel = invSel?.src === 'bag' && invSel.idx === i;
    return `<button class="iv-cell r-${it.rarity} ${it.slot ? 'eq' : ''} ${isSel ? 'sel' : ''} ${multiSel.has(i) ? 'msel' : ''} ${msMode && gearSell(c.k) ? 'mspick' : ''} ${it.slot && !canWear(it) ? 'req' : ''}" data-idx="${i}" data-copy="${c.copy ?? 0}" data-k="${c.k}" data-card="bag" ${it.slot ? 'draggable="true"' : ''} aria-label="${it.name}${c.n > 1 ? ' × ' + c.n : ''}" data-tip="${itemTip(c.k, rcHint(it))}">${itemImg(c.k)}${it.slot ? bagGemPips(c.k,c.copy ?? 0) : ''}${it.type === 'gem' ? '<span class="gem-level">'+it.gemLevel+'</span>' : ''}${c.n > 1 ? `<span class="qty">${short(c.n)}</span>` : it.slot ? `<span class="lv">${itemLv(it)}</span>` : ''}${isSel && Date.now() - sellArm < 3000 ? '<span class="arm">B lần nữa để bán</span>' : isSel && Date.now() - dropArm < 3000 ? '<span class="arm">Del lần nữa để bỏ</span>' : ''}</button>`;
  }).join('');
  const counts = Object.fromEntries(CATS.map(([id]) => [id, 0]));
  for (const k of DATA.items.map(i => i.id)) if (!BAG_HIDDEN.has(k) && (bagCount(k)||0) > 0){ counts.all++; counts[catOf(ITEM[k])]++; }


  const nextBag = DATA.realms[S.realm+1]?.bagSlots, nextNote = S.atTop ? '' : `Lên ${S.nextRealmName}: ${nextBag ? `hành trang ${nextBag} ô, ` : ''}${(DATA.realms[S.realm+1]?.equipSlots || []).some(x => !S.equipSlots.includes(x)) ? 'thêm ô trang bị và ' : ''}chỉ số mới`;
  const full = used >= cap, capPct = Math.min(100, cap ? used / cap * 100 : 0);
  const charHtml = `<section class="iv-char" aria-label="Nhân vật">${wardrobeHtml()}<div class="iv-char-in">
      <header class="iv-name"><button type="button" id="renameBtn" class="iv-rename" aria-label="Đổi đạo hiệu ${S.name}" data-tip="Đổi đạo hiệu&#10;Mỗi lần đổi tiêu 1 Thẻ đổi tên · đang có ${fmt(S.inventory?.the_doi_ten || 0)}"><b>${S.name}</b></button><span>Cấp ${S.level} · <em>${S.realmName}</em> ${S.stageName}</span></header>
      <div class="iv-doll">
        <div class="iv-col">${SLOT_LEFT.map(slotHtml).join('')}</div>
        <div class="iv-fig"><div class="iv-stage"><canvas id="ivAura" class="iv-aura" width="320" height="320" aria-hidden="true"></canvas><img class="hero ref" src="${skinArt('heroStand') || 'img/hero_ref_stand.png'}" alt="${S.name}"><canvas id="ivAvatar" class="iv-avatar" width="320" height="320" role="img" aria-label="Tạo hình nhân vật" style="visibility:hidden"></canvas><button type="button" id="ivZoom" class="iv-zoom" aria-label="Xem nhân vật lớn">+</button></div>
          </div>
        <div class="iv-col">${SLOT_RIGHT.map(slotHtml).join('')}</div>
      </div>
      <div class="iv-powcard pw-t${powTier(power(st))}" style="--pw-sync:-${(Date.now() % 6000) / 1000}s"><div><small>Lực chiến</small><b>${fmt(power(st))}</b></div><button type="button" class="iv-act${ivStats ? ' on' : ''}" id="ivStatBtn" aria-pressed="${ivStats}" data-tip="Chỉ số nhân vật">Chỉ số</button></div>
      <div class="iv-mini"><span><small>Công</small><b>${fmt(st.atk)}</b></span><span><small>Thủ</small><b>${fmt(st.def)}</b></span><span><small>Máu</small><b title="${fmtFull(st.hp)}">${fmt(st.hp)}</b></span></div>
      <div class="iv-bars">
        <div class="iv-bar qi" data-tip="Tu vi&#10;Đủ để đột phá tầng tiếp theo"><span>Tu vi</span><div><i id="ivQiFill"></i></div><em id="ivQiTxt"></em></div>
        <div class="iv-bar ql" data-tip="Linh khí&#10;Tốc độ hấp thu linh khí mỗi giây"><span>Linh khí</span><div><i style="width:${Math.min(100, 20 + S.rate)}%"></i></div><em>${fmtDecimal(Math.round(S.rate*10)/10)}/giây</em></div>
      </div>
      <div class="iv-foot"><div class="iv-skills" data-goskill>${skSlotsHtml('mini')}</div>${nextNote ? `<p class="iv-next">${nextNote}</p>` : ''}</div>
    </div></section>`;
  const bagHtml = ivStats ? invStatsHtml(st) : `<section class="iv-bag" aria-label="Hành trang">
      <nav class="iv-cats" role="tablist">${CATS.map(([id, n], i) => `<button role="tab" data-cat="${id}" aria-selected="${invCat === id}" data-tip="${n}&#10;Phím ${i+1}">${n}<small>${counts[id]}</small></button>`).join('')}</nav>
      <div class="iv-head"><div class="iv-cap${full ? ' full' : ''}"><p><span>Hành trang</span><b>${used} / ${cap} ô</b></p><span class="iv-capbar"><i style="width:${capPct.toFixed(1)}%"></i></span></div>
        <button class="sell-btn" id="quickEquipBtn" aria-label="Trang bị nhanh" data-tip="Trang bị nhanh · phím Q&#10;Mỗi ô mặc món có Chiến lực cao nhất trong hành trang&#10;(tính cả thưởng cùng bộ, chỉ món đủ cảnh giới)"><svg viewBox="0 0 24 24"><path d="M12 3 5 6v5c0 4.5 3 8.3 7 10 4-1.7 7-5.5 7-10V6z"/><path d="m13 7-3 5h4l-3 5"/></svg><span>Mặc nhanh</span></button>
        <button class="sell-btn" id="vaultOpenBtn" data-tip="Khảm Tinh Thạch vào trang bị"><svg class="gem-socket-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M8 3H4v5M16 3h4v5M20 16v5h-4M8 21H4v-5"/><path class="gem-socket-stone" d="m12 4 6 8-6 8-6-8Z"/><path class="gem-socket-facet" d="m12 4 2 8-2 8-2-8Zm-6 8h12"/><path d="m4 3 3 4m13-4-3 4M4 21l3-4m13 4-3-4"/></svg><span>Khảm Tinh Thạch</span></button>
        <button class="sell-btn" id="sellBtn" aria-label="Bán" data-tip="Bán&#10;Mở quầy bán ở Luyện chế"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8"/><path d="M12 7v10M9.5 9.5h3.8a1.7 1.7 0 0 1 0 3.4h-2.6a1.7 1.7 0 0 0 0 3.4H14.5"/></svg><span>Bán</span></button>
        <button class="ib sm" id="arrangeBtn" aria-label="Tự xếp" data-tip="Tự xếp&#10;Về danh mục Tất cả, xếp theo loại · phím A"><svg viewBox="0 0 24 24"><rect x="4" y="4" width="6" height="6"/><rect x="14" y="4" width="6" height="6"/><rect x="4" y="14" width="6" height="6"/><path d="M14 17h6M17 14v6"/></svg></button>
        <div class="sort">
          <button class="ib sm" id="sortBtn" aria-haspopup="listbox" aria-expanded="${sortOpen}" aria-label="Sắp xếp: ${GEAR_SORTS[gearSort].name}" data-tip="Sắp xếp&#10;Đang chọn: ${GEAR_SORTS[gearSort].name}"><svg viewBox="0 0 24 24"><path d="M7 4v16M3.5 16.5 7 20l3.5-3.5"/><path d="M14 6h7M14 11h5M14 16h3"/></svg></button>
          <ul class="sort-menu" role="listbox" aria-label="Sắp xếp hành trang" ${sortOpen ? '' : 'hidden'}>${Object.entries(GEAR_SORTS).map(([k, v]) => `<li role="option" data-sort="${k}" tabindex="0" aria-selected="${k === gearSort}">${v.name}</li>`).join('')}</ul>
        </div></div>
      ${msBarHtml()}
      <div class="iv-bagbody"><div class="iv-grid" id="ivGrid">${cells}</div></div>
      <nav class="pager iv-pager" aria-label="Trang hành trang">${pagerHtml(bagPage, pages, 'data-bagpage')}<span class="pg-info">Trang ${bagPage + 1}/${pages}</span></nav>
    </section>`;
  const sheet = invSel && !ivStats && ivMobile() ? invSheetHtml() : '';
  $('pane-inv').innerHTML = `<div class="iv iv2" style="--aura:${r.aura}">${charHtml}${bagHtml}${sheet}</div>`;
  liveInv();
}
/* ===== Giao diện Hành trang 11/2026: màn Chỉ số (bỏ khung chi tiết bên phải: xem bằng tooltip, thao tác bằng nhấp đúp / chuột phải / phím tắt); bán hàng loạt ở Luyện chế · Bán ===== */
let ivStats = false;
/** Điện thoại (bản web ≤ 620px, khớp css/mobile.css): không có rê chuột → chạm ô mở bảng thao tác dưới đáy. */
const IV_MOBILE = window.matchMedia?.('(max-width: 620px)');
const ivMobile = () => MODE === 'full' && !!IV_MOBILE?.matches;
const ivBtn = (act, label, cls = '') => `<button type="button" class="iv-act ${cls}" data-act="${act}">${label}</button>`;
/** Bảng thao tác món đang chọn (chỉ trên điện thoại): thẻ vật phẩm + các nút. Bán/vứt vẫn bấm hai lần để xác nhận. */
function invSheetHtml(){
  const it = ITEM[invSel?.k]; if (!it) return '';
  const bag = invSel.src === 'bag', acts = [];
  const sellArmed = sellArm && Date.now() - sellArm < 3000, dropArmed = dropArm && Date.now() - dropArm < 3000;
  if (bag && it.slot) acts.push(canWear(it) ? ivBtn('equip', 'Mặc', 'gold wide') : `<button type="button" class="iv-act gold wide" disabled>Cần cảnh giới ${DATA.realms[it.minRealm || 0]?.name || ''}</button>`);
  if (bag && canUse(it)) acts.push(ivBtn('use', 'Dùng', 'gold wide'));
  if (!bag) acts.push(ivBtn('unequip', 'Tháo xuống', 'wide'));
  if (!bag && typeof window.openVaultSlot === 'function') acts.push(`<button type="button" class="iv-act" data-ivgem="${invSel.slot}">Khảm Tinh Thạch</button>`);
  if (bag && it.sell) acts.push(ivBtn('sell', sellArmed ? 'Bấm lần nữa để bán' : `Bán · +${fmt(it.sell)}`, sellArmed ? 'danger' : ''));
  if (bag) acts.push(ivBtn('drop', dropArmed ? 'Bấm lần nữa để vứt' : 'Vứt bỏ', dropArmed ? 'danger' : ''));
  return `<div class="iv-sheet-bg" data-ivclose></div><section class="iv-sheet" role="dialog" aria-label="${it.name}">
    <button type="button" class="iv-sheet-x" data-ivclose aria-label="Đóng">×</button>
    ${itemCardHtml(invSel.k, invSel.src, invSel.slot, invSel.copy || 0)}<div class="iv-acts">${acts.join('')}</div></section>`;
}
/** Màn Chỉ số nhân vật (thay chỗ lưới hành trang). */
function invStatsHtml(st){
  const big = [['Tấn công', fmt(st.atk)], ['Phòng thủ', fmt(st.def)], ['Máu', fmt(st.hp)], ['Tốc độ di chuyển', `+${fmtDecimal(Math.round((st.moveSpeed || 0) * 10) / 10)}%`]];
  const pairs = [['Bạo kích','crit','Kháng bạo kích','critResist'],['Chính xác','accuracy','Né tránh','dodge'],['Liên kích','combo','Kháng liên kích','comboResist'],['Phản đòn','reflect','Kháng phản đòn','reflectResist']];
  const fx = gearFxHtml().replace(/<details (?!class="iv-fx iv-detail")/g, '<details open ').replace('<details class="iv-fx iv-detail">', '<details class="iv-fx iv-detail" open>');
  return `<section class="iv-bag iv-statview" aria-label="Chỉ số nhân vật">
    <header class="iv-sv-head"><h3>Chỉ số nhân vật</h3><button type="button" class="iv-act" id="ivStatClose">Về hành trang</button></header>
    <div class="iv-sv-body">
      <div class="iv-bigstats">${big.map(([n, v]) => `<div><small>${n}</small><b>${v}</b></div>`).join('')}</div>
      <h4 class="iv-sec">Chỉ số chiến đấu</h4>
      <div class="iv-pairs">${pairs.map(([a, ka, b, kb]) => `<div><span>${a}</span><b>${fmtRating(st[ka])}%</b><span>${b}</span><b>${fmtRating(st[kb])}%</b></div>`).join('')}</div>
      <p class="iv-note">Chỉ số phụ cộng dồn không giới hạn, trừ chỉ số kháng của đối phương trước khi tính tỉ lệ kích hoạt.</p>
      <h4 class="iv-sec">Nguồn chỉ số và hiệu ứng</h4>
      <div class="iv-sv-fx">${fx}</div>
    </div></section>`;
}
/** Thẻ chi tiết vật phẩm (giấy da) hiện trong tooltip khi rê chuột: chỉ số, yêu cầu, giá bán, so sánh với đồ đang mặc. */
// ---- hiệu ứng riêng của trang bị đang mặc (giống GameService.GearEffects: cộng dồn, áp trần) ----
function gemCombatStatsHtml(){
  const b = S.gemState?.bonuses; if (!b) return '';
  const ratings = [['crit','Bạo kích'],['critResist','Kháng bạo kích'],['accuracy','Chính xác'],['dodge','Né tránh'],['extraHit','Liên kích'],['comboResist','Kháng liên kích'],['reflect','Phản đòn'],['reflectResist','Kháng phản đòn']];
  const ratingKeys = new Set(ratings.map(([key]) => key));
  const ratingRows = ratings.map(([key,label]) => '<p>'+label+': <b>+'+fmtRating(b[key])+'%</b></p>').join('');
  const fields = [['atk','Tấn công',''],['def','Phòng thủ',''],['hp','Máu',''],['accuracy','Chính xác','%'],['dodge','Né tránh','%'],['crit','Bạo kích','%'],['critDamage','Sát thương bạo kích cộng thêm','%'],['critResist','Kháng bạo','%'],['block','Block (đỡ 50% sát thương)','%'],['eliteReduction','Giảm sát thương từ quái tinh anh','%'],['bossReduction','Giảm sát thương từ boss','%'],['lifeSteal','Hút máu','%'],['moveSpeed','Tốc độ chạy','%'],['regen','Hồi máu mỗi lượt','%'],['absorb','Hấp thụ sát thương','%'],['absorbHealChance','Chuyển hấp thụ thành máu','%'],['ragePerHit','Nộ mỗi đòn đánh trúng',''],['extraAttacks','Đòn thường chắc chắn mỗi lượt',''],['extraHit','Liên kích','%']];
  const rows = fields.filter(([key]) => !ratingKeys.has(key) && b[key] > 0).map(([key,label,unit]) => '<p>'+label+': <b>+'+fmtDecimal(b[key])+unit+'</b></p>').join('');
  return '<div class="iv-gem-combat"><b>Thuộc tính Tinh Thạch · chỉ số áp dụng trong chiến đấu</b>'+ratingRows+'<small>Tám chỉ số cộng dồn không giới hạn. Phản đòn là cơ hội phản công sau khi mất máu và còn sống; trừ Kháng phản đòn đối phương. Từ 100% sau đối kháng chỉ kích hoạt một lần.</small>'+rows+'</div>';
}
function gearFxHtml(){
  const snapshot = S?.gearEffects;
  if (!snapshot) return '';
  const effects = (snapshot.effects || []).map(e => {
    const capped = e.raw > e.effective;
    return `<span>${e.name}</span> <b>+${fmtDecimal(e.effective)}${e.unit}</b>${capped ? ` <small>(tổng ${fmtDecimal(e.raw)}${e.cap == null ? ", không giới hạn" : ", trần " + e.cap})</small>` : ''}`;
  });
  const sets = (snapshot.sets || []).filter(s => s.count).map(s =>
    `<span>${s.name} ${s.count}/10</span> <b>${s.active.length ? `mốc ${s.active.join(' · ')}` : 'chưa kích hoạt'}</b>${s.next ? ` <small>mốc kế: ${s.next}</small>` : ''}`);
  const sourceFields = [['atk','công',''],['def','thủ',''],['hp','máu',''],['atkPct','công','%'],['defPct','thủ','%'],['hpPct','máu','%'],['crit','bạo kích',' điểm %'],['critResist','kháng bạo kích',' điểm %'],['dodge','né tránh',' điểm %'],['accuracy','chính xác',' điểm %'],['combo','liên kích',' điểm %'],['comboResist','kháng liên kích',' điểm %'],['reflect','phản đòn',' điểm %'],['reflectResist','kháng phản đòn',' điểm %'],['qi','tu luyện','%']];
  const sources = (S.statSources || []).map(s => {
    const values = sourceFields.filter(([key]) => s[key]).map(([key, name, unit]) => `+${fmtDecimal(s[key])}${unit} ${name}`);
    return values.length ? `<p><b>${s.name}:</b> ${values.join(' · ')}</p>` : '';
  }).join('');
  const limits = (S.combatLimits || []).map(e => `<p>${e.name}: <b>${fmtDecimal(e.effective)}${e.unit}</b> <small>(tổng ${fmtDecimal(e.raw)}${e.cap == null ? ", không giới hạn" : ", trần " + e.cap})</small></p>`).join('');
  const divine=snapshot.divine;
  const divineHtml=divine && !divine.tier ? `<div class="iv-fx">◇ Thần thông bộ · Chưa kích hoạt</div>` : divine ? `<details class="iv-fx iv-divine" ${divine.tier ? 'open' : ''}><summary>◇ Thần thông bộ · ${divine.tier ? divine.name : 'Chưa kích hoạt'}</summary><p>${divine.setName ? `Bộ ${divine.setName} · ` : ''}Cần 10 món cùng bộ; chỉ kích hoạt mốc phẩm cao nhất.${divine.setRealmName ? ` Bộ cảnh giới ${divine.setRealmName}: chỉ số thần thông mạnh theo cảnh giới của bộ (mỗi cảnh giới +5%).` : ''}</p>${(divine.progress||[]).filter(p=>p.active).map(p=>`<p style="opacity:${p.active?1:.65}">${p.active?'◆':'◇'} <b>${p.quality} · ${p.name} (${p.count}/10)</b><br>${p.description}</p>`).join('')}</details>` : '';
  return `<details class="iv-fx iv-detail"><summary>Chi tiết</summary><div class="iv-fx" data-tip="Hiệu ứng trang bị và bộ do server tính; trần tổng với công pháp/kỹ năng áp dụng khi chiến đấu.">${sets.concat(effects).join(' · ')}</div>${divineHtml}<details class="iv-fx"><summary>Nguồn chỉ số và giới hạn</summary>${sources}${gemCombatStatsHtml()}${limits}<small>Giá trị nguồn trước trần và suy yếu; chỉ số cuối ở bảng nhân vật. Né tránh cộng từ cảnh giới, trang bị, công pháp và linh thú. Chính xác giảm khả năng né của đối phương trước khi tính xác suất 0–100%. Hai chỉ số không thay đổi thời gian di chuyển hoặc giao chiến.</small></details></details>`;
}
function itemSetHtml(it){
  const def = (DATA.equipmentSets || []).find(s => s.id === it.setId);
  if (!def) return it.set ? `<p class="set">◆ Bộ ${it.set}</p>` : '';
  const state = S?.gearEffects?.sets?.find(s => s.id === def.id);
  return `<div class="set" style="--sc:${SET_COLOR[it.set] || '#2e8b6a'}"><p>◆ ${def.name} (${state?.count || 0}/10)</p>${def.milestones.map(m => {
    const active = state?.active?.includes(m.count);
    return `<p style="opacity:${active ? 1 : .5}">${active ? '✓' : '○'} ${m.count} món: ${m.bonuses.map(b => b.text).join(' · ')}</p>`;
  }).join('')}</div>`;
}
function itemCardHtml(k, src, slotId, copy = 0){
  const it = ITEM[k]; if (!it) return '';
  const okRealm = canWear(it), unlocked = new Set(S.equipSlots);
  let cmp = '';
  if (compareOn && src === 'bag' && it.slot){
    const target = slotsFor(it).filter(s => unlocked.has(s));
    const cur = target.map(s => S.equipment[s]).find(Boolean);
    const curIt = cur ? ITEM[cur] : null;
    const rows = STAT_KEYS.map(([key, n, u]) => {
      const a = it[key] || 0, b = curIt ? (curIt[key] || 0) : 0, d = a - b;
      return d ? `<li class="${d > 0 ? 'up' : 'down'}">${n} ${d > 0 ? '+' : ''}${fmtDecimal(Math.round(d*10)/10)}${u}</li>` : '';
    }).join('');
    cmp = `<div class="iv-cmp"><p>${curIt ? `So với đang mặc: <b class="rc-${curIt.rarity}">${curIt.name}</b>` : 'Ô đang trống: mọi chỉ số đều là tăng thêm'}</p><ul>${rows || '<li>Không đổi chỉ số</li>'}</ul></div>`;
  }
  const targetSlot = slotId || (src === 'slot' ? it.slot : null);
  const gemTotals = Object.fromEntries(STAT_KEYS.map(([key]) => [key, 0]));
  let gemBlock = '';
  const bagGems = src === 'bag' ? S.gemState?.bagSlots?.[k]?.[copy] : null;
  if (it.slot && (bagGems || (targetSlot && S?.gemState?.slots?.[targetSlot]))) {
    const sGems = bagGems || S.gemState.slots[targetSlot] || [];
    const grade = RAR_KEYS.indexOf(it.rarity);
    const maxS = bagGems ? (grade < 4 ? 1 : grade < 7 ? 2 : 3) : (S.gemState?.maxSockets?.[targetSlot] ?? 0);
    const filledCount = sGems.filter(Boolean).length;
    if (slotId || filledCount > 0) {
      const gemRows = [];
      for (let i = 0; i < maxS; i++) {
        const gId = sGems[i];
        if (gId && ITEM[gId]) {
          const gDef = ITEM[gId];
          for (const [key] of STAT_KEYS) if (key !== 'qiPct' && gDef[key]) gemTotals[key] += gDef[key];
          const rawStat = gDef.special || (gDef.atk ? `+${fmt(gDef.atk)} Công` : gDef.def ? `+${fmt(gDef.def)} Thủ` : `+${fmt(gDef.hp)} Máu`);
          const gStat = rawStat.replace(/^(?:Khảm nạm|Khảm Tinh Thạch):\s*/i, '');
          gemRows.push(`<div class="iv-gem-row iv-gem-bonus"><span class="rc-${gDef.rarity}">${gStat}</span></div>`);
        } else {
          gemRows.push(`<div class="iv-gem-row iv-gem-bonus empty"><span>Trống</span></div>`);
        }
      }

      gemBlock = `
        <div class="iv-gem-card">
          <div class="iv-gem-hdr"><b>◆ Tinh Thạch đã khảm</b> <span class="iv-gem-badge">${filledCount}/${maxS}</span></div>
          ${gemRows.join('')}
        </div>
      `;
    }
  }
  // Socket bonuses are already listed in the dedicated gem card below.
  const mergedEffects = it.effects || [];
  const del = `${KBD('Del')} bỏ`, act2 = `${MS_R}<i>/</i>${MS_L2}`;
  let hint = '';
  if (src === 'view') {
    hint = '';   // chỉ xem (trang bị của đạo hữu khác ở Thiên bảng): không có thao tác
  } else if (src === 'admin-item') {
    hint = 'Nhấp để chọn · nhập số lượng rồi bấm Nhận món';
  } else if (src === 'gem-orb') {
    hint = `${act2} tháo về Kho <i>·</i> Nhấp trái xem thông tin`;
  } else if (src === 'gem-pick' || src === 'gem-picker') {
    hint = `${MS_L2} khảm Tinh Thạch vào lỗ đang chọn`;
  } else if (src === 'gem-vault') {
    hint = `Dùng ở tab Vị Trí Khảm Tinh Thạch hoặc Lò Hợp`;
  } else if (src === 'cube-pick') {
    hint = `Nhấp bỏ vào Lò Hợp · ${MS_R} lấy 1 ra khỏi Lò`;
  } else if (src === 'cube-slot') {
    hint = `${MS_R}<i>/</i>Nhấp lấy ra khỏi Lò Hợp`;
  } else if (src === 'vault-slot') {
    const sGems = (targetSlot && S?.gemState?.slots?.[targetSlot]) || [];
    const hasGems = sGems.some(Boolean);
    hint = hasGems ? `${MS_R} tháo nhanh Tinh Thạch · Nhấp chọn ô` : `Nhấp chọn ô để Khảm Tinh Thạch`;
  } else if (src === 'slot') {
    hint = `${act2} tháo · kéo vào hành trang · ${KBD('U')} tháo`;
  } else if (it.slot) {
    hint = `${act2} mặc · kéo vào ô trang bị · ${KBD('B')} bán · ${del}`;
  } else if (it.type === 'chest') {
    hint = `${act2} mở · ${KBD('Ctrl')}+${MS_R} mở hết · ${del}`;
  } else if (it.type === 'skin') {
    hint = `${act2} mở · ${KBD('B')} bán · ${del}`;
  } else if (it.type === 'salvage') {
    hint = `${KBD('B')} bán · Luyện chế → Bán để bán cả chồng · ${del}`;
  } else if (canUse(it)) {
    hint = `${act2} dùng${it.useQiSeconds ? ` · ${KBD('Ctrl')}+${MS_R} dùng đến đầy tu vi` : it.id === 'ngo_dao_dan' ? ` · ${KBD('Ctrl')}+${MS_R} dùng hết` : ''} · ${del}`;
  } else if (rcCraft(it)) {
    hint = `${MS_R} mở Luyện chế · Đan dược · ${del}`;
  } else if (it.type === 'gem' || it.type === 'stone') {
    hint = `${MS_L2} khảm Tinh Thạch vào lỗ trang bị`;
  } else {
    hint = del;
  }

  const vCnt = (typeof getVaultState === 'function' ? getVaultState()?.vault?.[k] : S?.gemState?.vault?.[k]) || 0;
  const count = bagCount(k);
  const countText = src !== 'view' && count > 1 && !it.slot && it.type !== 'gem' ? ` · có ${fmt(count)}` : '';
  const sellText = it.sell ? `Giá bán: ${fmt(it.sell)} linh thạch` : 'Không bán được';

  return `<div class="iv-card">
    <p class="nm rc-${it.rarity}">${it.name}</p>
    <p class="sub">${it.itemLevel ? `Cấp ${it.itemLevel} · ` : ''}${qWord(it.rarity || 'common', true)} · ${it.kind || ''}${it.type === 'gem' ? ' · '+(it.gemCategory === 'defense' ? 'Phòng thủ' : 'Tấn công') : ''}${countText}</p>
    ${it.slot ? `<p class="req ${okRealm ? '' : 'bad'}">Yêu cầu cảnh giới: ${DATA.realms[it.minRealm||0].name}</p>` : ''}
    ${it.slot ? [
      ['Thuộc tính cơ bản', STAT_KEYS.filter(([key]) => key !== 'qiPct')],
      ['Thuộc tính hỗ trợ', STAT_KEYS.filter(([key]) => key === 'qiPct')]
    ].map(([title, keys]) => {
      const lines = keys
        .map(([key, name, unit]) => [name, unit, Math.round(((it[key] || 0) + (gemTotals[key] || 0)) * 100) / 100])
        .filter(([, , val]) => val > 0)
        .map(([name, unit, val]) => `${name}: +${fmtDecimal(val)}${unit}`);
      return lines.length ? `<p class="sts"><b>${title}</b><br><span class="qc-${it.rarity}">${lines.join('<br>')}</span></p>` : '';
    }).join('') : ((it.type === 'gem' || !it.special) && itemStats(it).length) ? `<p class="sts"><span class="qc-${it.rarity}">${itemStats(it).join('<br>')}</span></p>` : ''}
    ${it.special && it.type !== 'gem' ? `<p class="spc">${fmtNumberText(it.special)}</p>` : ''}
    ${it.use ? `<p class="spc">${fmtNumberText(it.use)}</p>` : ''}
    ${boNguyenLine(it)}
    ${mergedEffects.length ? `<p class="eff">${it.slot ? '<b>Thuộc tính hỗ trợ</b><br>' : ''}${mergedEffects.map(e => `<span class="qc-${e.rarity || it.rarity}">◇ ${fmtNumberText(e.text)}</span>`).join('<br>')}</p>` : ''}
    ${gemBlock}
    ${itemSetHtml(it)}
    <p class="ds">“${fmtNumberText(it.desc)}”</p>
    <p class="sv">${sellText}</p>
    ${adminOn && it.type === 'chest' ? chestOddsHtml(k) : ''}${cmp}${hint ? `<p class="hn">${hint}</p>` : ''}</div>`;
}
/** Bổ Nguyên Đan: không giới hạn lượt, hiệu quả giảm theo giờ tu luyện đã nhận từ đan trong ngày (cả ba phẩm tính chung). */
function boNguyenLine(it) {
  const b = S?.boNguyen;
  if (!b || !it?.id?.startsWith('bo_nguyen_dan')) return '';
  const h = fmtDecimal(b.hoursToday);
  const next = b.nextDropHours != null ? `<br>Từ giờ thứ ${fmtDecimal(b.nextDropHours)}: hiệu quả ${b.nextPct}%.` : '';
  return `<p class="spc">Hiệu quả hôm nay: ${b.efficiencyPct}% (đã nhận ${h} giờ tu luyện từ Bổ Nguyên Đan).${next}<br>Không giới hạn lượt dùng, làm mới lúc 0h.</p>`;
}
function liveInv(){
  const f = $('ivQiFill'); if (!f || !S) return;
  const q = qiNow(); f.style.width = Math.min(100, q / S.qiReq * 100) + '%'; setQiDisplay($('ivQiTxt'), q, S.qiReq);
}

// ---- tương tác: chọn, nhấp đúp, kéo thả, phím tắt ----
function invDo(action){
  const sel = invSel ? ITEM[invSel.k] : null;
  if (action === 'equip' && invSel?.src === 'bag' && sel?.slot) act('equip/' + invSel.k + '?copy=' + (invSel.copy ?? 0));
  else if (action === 'unequip' && invSel?.src === 'slot') act('unequip/' + invSel.slot);
  else if (action === 'use' && invSel?.src === 'bag' && canUse(sel)) act('use/' + invSel.k);
  else if (action === 'drop' && invSel?.src === 'bag'){
    if (Date.now() - dropArm < 3000){ dropArm = 0; act('drop/' + invSel.k + '?copy=' + (invSel.copy ?? 0)); }
    else { dropArm = Date.now(); renderInv(); setTimeout(() => { if (dropArm && Date.now() - dropArm >= 3000){ dropArm = 0; renderInv(); } }, 3100); }
  }
  else if (action === 'sell' && invSel?.src === 'bag' && sel?.sell){
    // đồ từ Huyền cấp trở lên phải bấm hai lần để tránh bán nhầm
    if (RAR_KEYS.indexOf(sel.rarity) < 2 || Date.now() - sellArm < 3000){ sellArm = 0; act('sell/' + invSel.k + '?copy=' + (invSel.copy ?? 0)); }
    else { sellArm = Date.now(); renderInv(); setTimeout(() => { if (sellArm && Date.now() - sellArm >= 3000){ sellArm = 0; renderInv(); } }, 3100); }
  }
  else if (action === 'compare'){ compareOn = !compareOn; renderInv(); }
  else if (action === 'sort'){ sortOpen = !sortOpen; hideTip(); renderInv(); }
  else if (action === 'arrange'){ setCat('all'); gearSort = 'slot'; resetBagOrder(); try { localStorage.setItem('van-dao-gear-sort', 'slot'); } catch(e){} renderInv(); $('ivGrid') && ($('ivGrid').scrollTop = 0); }
}
function setCat(c){ invCat = c; bagPage = 0; invSel = invSel?.src === 'slot' ? invSel : null; try { localStorage.setItem('van-dao-inv-cat', c); } catch(e){} renderInv(); }
/** Đổi đạo hiệu (tên trên bảng xếp hạng): mỗi lần tiêu 1 Thẻ đổi tên, mua ở Chợ → Tạp Hóa bà Dĩm. */
function showRenameDialog(){
  let dialog = $('renameDialog');
  if (!dialog){
    dialog = document.createElement('dialog'); dialog.id = 'renameDialog'; dialog.className = 'game-settings';
    dialog.setAttribute('aria-labelledby', 'renameTitle');
    dialog.innerHTML = `<h2 id="renameTitle">Đổi đạo hiệu</h2>
      <form method="dialog" id="renameForm"><input id="renameIn" maxlength="20" autocomplete="off" spellcheck="false" aria-label="Đạo hiệu mới">
      <p id="renameNote"></p>
      <div class="game-settings-actions"><button type="submit" id="renameOk">Đổi</button><button type="button" data-rename-close>Đóng</button></div></form>`;
    document.body.appendChild(dialog);
    dialog.querySelector('[data-rename-close]').onclick = () => dialog.close();
    dialog.querySelector('#renameForm').addEventListener('submit', async e => {
      e.preventDefault();
      const name = $('renameIn').value.trim();
      if (!name || name === S.name) return;
      if (await act(`name?${new URLSearchParams({name})}`)) dialog.close();
    });
  }
  const cards = S.inventory?.the_doi_ten || 0;
  $('renameIn').value = S.name;
  $('renameNote').textContent = cards ? `Tiêu 1 Thẻ đổi tên · đang có ${fmt(cards)}. Đạo hiệu 2–20 ký tự.` : 'Cần 1 Thẻ đổi tên để đổi đạo hiệu. Mua ở Chợ → Tạp Hóa bà Dĩm.';
  $('renameOk').disabled = !cards;
  if (!dialog.open) dialog.showModal();
  $('renameIn').select();
}

function initInventory(){
  const pane = $('pane-inv');
  let lastTap = {key:'', t:0};
  const pickSort = k => { gearSort = k; sortOpen = false; resetBagOrder(); try { localStorage.setItem('van-dao-gear-sort', gearSort); } catch(err){} hideTip(); renderInv(); };
  document.addEventListener('click', e => {
    if (sortOpen && !e.target.closest('.sort') && !e.target.closest('[data-act="sort"]')){ sortOpen = false; renderInv(); }
    if (sellOpen && !e.target.closest('.sort')){ sellOpen = false; renderInv(); }
  });
  pane.addEventListener('pointerover', e => { if (e.target.closest('#ivZoom') && !heroZoomHover){ heroZoomHover = true; heroZoom(true); } });
  pane.addEventListener('pointerout', e => { if (e.target.closest('#ivZoom') && !e.relatedTarget?.closest?.('#ivZoom')){ heroZoomHover = false; if (!heroZoomPinned) heroZoom(false); } });
  pane.addEventListener('focusin', e => { if (e.target.closest('#ivZoom')){ heroZoomHover = true; heroZoom(true); } });
  pane.addEventListener('focusout', e => { if (e.target.closest('#ivZoom')){ heroZoomHover = false; if (!heroZoomPinned) heroZoom(false); } });
  document.addEventListener('pointerdown', e => { if (heroZoomPinned && !e.target.closest('#ivZoom')) heroZoom(false); });
  pane.addEventListener('click', e => {
    if (e.target.closest('#ivZoom')){ hideTip(); heroZoomPinned = !heroZoomPinned; heroZoom(heroZoomPinned || heroZoomHover); return; }   // chạm: bấm để mở / đóng
    if (e.target.closest('#renameBtn')){ hideTip(); showRenameDialog(); return; }
    if (e.target.closest('#wardBtn')){ wardOpen = !wardOpen; hideTip(); renderInv(); return; }
    if (e.target.closest('[data-wardclose]')){ wardOpen = false; renderInv(); return; }
    const gender = e.target.closest('[data-gender]');
    if (gender){ if (!gender.disabled) changeWardrobeGender(gender.dataset.gender); return; }
    const sk = e.target.closest('[data-skin]');
    if (sk){ if (!sk.disabled && sk.dataset.skin !== (S.skin || '')) act('skin?skin=' + encodeURIComponent(sk.dataset.skin)); return; }
    if (wardOpen && !e.target.closest('.iv-ward')){ wardOpen = false; renderInv(); }
    if (e.target.closest('#ivStatBtn')){ hideTip(); ivStats = !ivStats; msMode = false; msClear(); renderInv(); return; }
    if (e.target.closest('#ivStatClose')){ ivStats = false; renderInv(); return; }
    if (e.target.closest('[data-ivclose]')){ invSel = null; sellArm = 0; dropArm = 0; renderInv(); return; }   // bảng thao tác trên điện thoại
    const ivg = e.target.closest('[data-ivgem]'); if (ivg){ hideTip(); window.openVaultSlot?.(ivg.dataset.ivgem); return; }
    if (e.target.closest('#sortBtn')){ sortOpen = !sortOpen; sellOpen = false; hideTip(); renderInv(); return; }
    if (e.target.closest('[data-goskill]')){ const sl = e.target.closest('[data-slot]'), id = sl && S.skillSlots[+sl.dataset.slot]; openTab('skill'); if (id && skById(id)){ skSel = id; skEl = skById(id).element; renderSkill(); } return; }
    if (e.target.closest('#arrangeBtn')){ hideTip(); invDo('arrange'); return; }
    if (e.target.closest('[data-msmode]')){ msMode = !msMode; if (!msMode) msClear(); sellOpen = false; hideTip(); renderInv(); return; }
    const msb = e.target.closest('[data-ms]');
    if (msb){ const a = msb.dataset.ms; hideTip(); if (a === 'sell') msSell(); else { if (a === 'clear') msClear(); else if (a === 'page') msPage(); else if (a === 'done'){ msMode = false; msClear(); } renderInv(); } return; }
    if (e.target.closest('#quickEquipBtn')){ sellOpen = false; sortOpen = false; hideTip(); act('equip-best'); return; }
    if (e.target.closest('#vaultOpenBtn')){ sellOpen = false; sortOpen = false; hideTip(); if (typeof window.openVaultSlot === 'function') window.openVaultSlot(); else openTab('vault'); return; }
    if (e.target.closest('#sellBtn')){ sellOpen = false; sortOpen = false; hideTip(); openTab('sell', true); return; }   // nút Bán → quầy bán ở Luyện chế
    if (e.target.closest('.bs-head,.bs-foot')) return;
    const so = e.target.closest('[data-sort]'); if (so){ pickSort(so.dataset.sort); return; }
    const ca = e.target.closest('[data-cat]'); if (ca){ setCat(ca.dataset.cat); return; }
    const pg = e.target.closest('[data-bagpage]'); if (pg){ if (!pg.disabled){ bagPage = +pg.dataset.bagpage; renderInv(); } return; }
    const ac = e.target.closest('[data-act]'); if (ac){ if (!ac.disabled) invDo(ac.dataset.act); return; }
    const pips = e.target.closest('.iv-pips');
    if (pips && typeof window.openVaultSlot === 'function'){
      const parentSlot = pips.closest('.iv-slot');
      if (parentSlot && parentSlot.dataset.slot){
        hideTip();
        window.openVaultSlot(parentSlot.dataset.slot);
        return;
      }
    }
    // nhấp đúp tự nhận diện vì mỗi lần nhấp vẽ lại ô (sự kiện dblclick gốc bị mất khi phần tử bị thay)
    const cell = e.target.closest('.iv-cell[data-k]'), slot = e.target.closest('.iv-slot.has');
    const key = cell ? 'b' + cell.dataset.idx + cell.dataset.k : slot ? 's' + slot.dataset.slot : '';
    const dbl = key && lastTap.key === key && performance.now() - lastTap.t < 400;
    lastTap = dbl ? {key:'', t:0} : {key, t: performance.now()};
    if (cell && (e.shiftKey || e.ctrlKey || e.metaKey || msMode)){       // chọn nhiều để bán
      const i = +cell.dataset.idx;
      if (e.shiftKey) msRange(i); else msToggle(i, cell.dataset.k);
      lastTap = {key:'', t:0}; renderInv(); return;
    }
    if (cell){
      invSel = {src:'bag', k: cell.dataset.k, copy: +(cell.dataset.copy || 0), idx: +cell.dataset.idx}; dropArm = 0; sellArm = 0;
      const it = ITEM[cell.dataset.k];
      if (ivMobile()) hideTip();   // điện thoại: chạm = mở bảng thao tác, không để tooltip che
      if (dbl && it.slot) invDo('equip'); else if (dbl && canUse(it)) invDo('use'); else renderInv();
      return;
    }
    if (slot){
      if (e.shiftKey && typeof window.openVaultSlot === 'function'){
        hideTip();
        window.openVaultSlot(slot.dataset.slot);
        return;
      }
      invSel = {src:'slot', k: slot.dataset.k, slot: slot.dataset.slot};
      if (ivMobile()) hideTip();
      if (dbl) invDo('unequip'); else renderInv();
      return;
    }
    const emptySlot = e.target.closest('.iv-slot.empty');
    if (emptySlot && typeof window.openVaultSlot === 'function'){
      hideTip();
      window.openVaultSlot(emptySlot.dataset.slot);
      return;
    }
  });
  // chuột phải: mặc / dùng đồ trong túi, tháo đồ đang mặc, tháo kỹ năng trong ô
  pane.addEventListener('contextmenu', e => {
    const cell = e.target.closest('.iv-cell[data-k]'), slot = e.target.closest('.iv-slot.has'), ss = e.target.closest('.sk-slot[data-slotsk]');
    if (!cell && !slot && !ss) return;
    e.preventDefault(); hideTip(); lastTap = {key:'', t:0};
    if (ss){ act(`skillslot/${ss.dataset.slot}`); return; }
    if (cell){
      invSel = {src:'bag', k: cell.dataset.k, copy: +(cell.dataset.copy || 0), idx: +cell.dataset.idx}; dropArm = 0; sellArm = 0;
      const it = ITEM[cell.dataset.k];
      if (it?.type === 'chest' && e.ctrlKey) act(`use/${cell.dataset.k}?n=0`);     // Ctrl + chuột phải: mở hết rương loại này
      else if (it?.useQiSeconds && e.ctrlKey) act(`use/${cell.dataset.k}?n=0`);   // Dừng khi đầy tu vi, giữ đan còn dư.
      else if (it?.id === 'ngo_dao_dan' && e.ctrlKey) act(`use/${cell.dataset.k}?n=0`);   // Ngộ Đạo Đan: dùng hết
      else if (it?.slot) invDo('equip'); else if (it && canUse(it)) invDo('use');
      else if (rcCraft(it)){                                                      // đan / linh thảo: mở Luyện chế · Đan dược
        if (DATA.pillChain.includes(cell.dataset.k)){ ddSel = cell.dataset.k; try { localStorage.setItem('van-dao-dd-sel', ddSel); } catch(err){} }
        openTab('craft', true); renderCraft();
      } else renderInv();
      return;
    }
    invSel = {src:'slot', k: slot.dataset.k, slot: slot.dataset.slot}; invDo('unequip');
  });
  // kéo thả giữa túi và ô trang bị
  let drag = null;
  pane.addEventListener('dragstart', e => {
    const cell = e.target.closest('.iv-cell[data-k]'), slot = e.target.closest('.iv-slot.has');
    drag = cell ? {src:'bag', k: cell.dataset.k, copy: +(cell.dataset.copy || 0)} : slot ? {src:'slot', k: slot.dataset.k, slot: slot.dataset.slot} : null;
    if (!drag) return;
    e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', drag.k);
    hideTip();
    if (drag.src === 'bag') for (const s of slotsFor(ITEM[drag.k])) pane.querySelector(`.iv-slot[data-slot="${s}"]`)?.classList.add('compat');
  });
  pane.addEventListener('dragend', () => { drag = null; pane.querySelectorAll('.drop-ok').forEach(x => x.classList.remove('drop-ok')); });
  pane.addEventListener('dragover', e => {
    if (!drag) return;
    const slot = e.target.closest('.iv-slot[data-slot]'), grid = e.target.closest('.iv-grid');
    const ok = (drag.src === 'bag' && slot && slotsFor(ITEM[drag.k]).includes(slot.dataset.slot)) || (drag.src === 'slot' && grid);
    if (ok){ e.preventDefault(); (slot || grid).classList.add('drop-ok'); }
  });
  pane.addEventListener('dragleave', e => { e.target.closest?.('.drop-ok')?.classList.remove('drop-ok'); });
  pane.addEventListener('drop', e => {
    if (!drag) return; e.preventDefault();
    const slot = e.target.closest('.iv-slot[data-slot]'), grid = e.target.closest('.iv-grid');
    if (drag.src === 'bag' && slot) act(`equip/${drag.k}?slot=${slot.dataset.slot}&copy=${drag.copy ?? 0}`);
    else if (drag.src === 'slot' && grid) act('unequip/' + drag.slot);
    drag = null;
  });
  // phím tắt khi đang xem Hành trang
  document.addEventListener('keydown', e => {
    if ($('pane-inv').hidden || e.target.closest('input,select,textarea') || e.ctrlKey || e.metaKey || e.altKey) return;
    const k = e.key.toLowerCase();
    if (sortOpen){ const li = e.target.closest?.('[data-sort]'); if (li && (k === 'enter' || k === ' ')){ e.preventDefault(); pickSort(li.dataset.sort); } if (k === 'escape'){ sortOpen = false; renderInv(); } return; }
    if (sellOpen){ const li = e.target.closest?.('[data-msmode]'); if (li && (k === 'enter' || k === ' ')){ e.preventDefault(); li.click(); return; } if (k === 'escape'){ sellOpen = false; renderInv(); return; } }
    if (k === 'escape' && (multiSel.size || msMode)){ msClear(); msMode = false; renderInv(); return; }
    if (k === 'escape' && ivStats){ ivStats = false; renderInv(); return; }
    if (k === 'escape' && invSel){ invSel = null; renderInv(); return; }
    if (k === 'b' && multiSel.size){ e.preventDefault(); msSell(); return; }
    if (k === 'q' && !e.ctrlKey && !e.altKey && !e.metaKey){ act('equip-best'); return; }   // trang bị nhanh
    const map = {e:'equip', u:'unequip', r:'use', delete:'drop', b:'sell', c:'compare', s:'sort', a:'arrange'};
    if (map[k]){ e.preventDefault(); invDo(map[k]); return; }
    if (/^[1-7]$/.test(k)){ setCat(CATS[+k - 1][0]); return; }
    if (k === 'enter' && invSel?.src === 'bag'){ const it = ITEM[invSel.k]; invDo(it.slot ? 'equip' : 'use'); return; }
    if (['arrowleft','arrowright','arrowup','arrowdown'].includes(k)){
      const list = bagList(); if (!list.length) return;
      e.preventDefault();
      let i = invSel?.src === 'bag' ? invSel.idx : -1;
      const cols = +getComputedStyle($('ivGrid')).getPropertyValue('--cols') || 8;   // số cột thật của lưới
      i += {arrowleft:-1, arrowright:1, arrowup:-cols, arrowdown:cols}[k];
      i = Math.max(0, Math.min(list.length - 1, i));
      invSel = {src:'bag', k: list[i].k, copy: list[i].copy ?? 0, idx: i, _follow: true}; renderInv();
      pane.querySelector(`.iv-cell[data-idx="${i}"]`)?.scrollIntoView({block:'nearest'});
    }
    if (k === 'pageup' || k === 'pagedown'){ e.preventDefault(); bagPage += k === 'pagedown' ? 1 : -1; renderInv(); }
  });
}

const short = n => Math.abs(n) >= 1e6 ? fmtCompact(n) : n >= 1e4 ? Math.floor(n/1e3) + 'K' : String(n);
