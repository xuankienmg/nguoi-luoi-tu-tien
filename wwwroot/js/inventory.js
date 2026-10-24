/* Vấn Đạo · js/inventory.js: tab Hành trang: trang bị, túi, bán, kéo thả. Tách từ index.html (tools/split_index.py). */
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
const STAT_KEYS = [['atk','Tấn công',''],['def','Phòng thủ',''],['hp','Máu',''],['critPct','Bạo kích','%'],['speedPct','Thân pháp','%'],['qiPct','Tu vi','%']];
/** Vật phẩm dùng được từ túi: đan hồi linh khí, luyện thể, mở rương. */
const canUse = it => !!it && !!(it.useQiSeconds || it.temperHp || it.type === 'chest' || it.type === 'eventchest' || it.type === 'egg' || it.type === 'elixir' || it.type === 'manual' || it.type === 'skin');
const catOf = it => ['quest','key','boss'].includes(it.type) ? 'quest' : it.slot ? (['weapon','offhand'].includes(it.slot) ? 'weapon' : ['necklace','ring','charm'].includes(it.slot) ? 'accessory' : 'armor') : it.type === 'pill' || it.type === 'elixir' || it.type === 'manual' ? 'consumable' : 'material';
const rar = it => RARITY[it.rarity] || RARITY.common;
function levelOf(realmIdx){ let n = 1; for (let i = 0; i < realmIdx; i++) n += DATA.realms[i].stages.length; return n; }
const reqLevel = it => levelOf(it.minRealm || 0);
/** Cấp trang bị (1 Luyện Khí · 10 Trúc Cơ · 15 Kim Đan) hiện ở góc ô; món không có cấp thì hiện cấp nhân vật tối thiểu của cảnh giới yêu cầu. */
const itemLv = it => it.itemLevel || reqLevel(it);
const RAR_KEYS = Object.keys(RARITY);
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
let sellOpen = false, sellArm = 0, bulkArm = null;
// bán hàng loạt: phạm vi (cả hành trang / trang đang xem) và chỉ trang bị — nhớ trên máy này
let bulkScope = 'all', bulkGear = true;
try { bulkScope = localStorage.getItem('van-dao-bulk-scope') || 'all'; bulkGear = localStorage.getItem('van-dao-bulk-gear') !== '0'; } catch(e){}
const canWear = it => (it.minRealm || 0) <= S.realm;
const slotsFor = it => !it.slot ? [] : [it.slot];
function itemStats(it){ return STAT_KEYS.filter(([k]) => it[k]).map(([k, n, u]) => `${n} +${fmtDecimal(it[k])}${u}`); }
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
  if (it.type === 'chest') return `${MS_R}/${MS_L2} mở · ${KBD('Ctrl')}+${MS_R} mở hết`;
  if (canUse(it)) return `${MS_R}/${MS_L2} dùng`;
  if (rcCraft(it)) return `${MS_R} mở Đan dược (${it.type === 'herb' ? 'luyện đan' : 'luyện chế'})`;
  return '';
}
/** Thượng phẩm linh thạch: chưa có nguồn nhận, tạm hiện số dư (0) cạnh linh thạch. */
const HP_STONE = 'thuong_pham_linh_thach';
const hpStoneHtml = cls => ITEM[HP_STONE] ? `<span class="${cls}" data-tip="Thượng phẩm linh thạch&#10;Chưa mở: sẽ có trong bản cập nhật sau">${itemImg(HP_STONE)}<b>${fmt(S.inventory[HP_STONE] || 0)}</b></span>` : '';
function itemTip(k, hint){
  const it = ITEM[k], lines = [it.name, `${it.itemLevel ? `Cấp ${it.itemLevel} · ` : ''}${rar(it).n} · ${it.kind || ''}`];
  if (it.slot) lines.push(`Yêu cầu cảnh giới: ${DATA.realms[it.minRealm||0].name}`);
  const sts = itemStats(it); if (sts.length) lines.push(sts.join(' · '));
  if (it.special) lines.push(fmtNumberText(it.special));
  for (const effect of it.effects || []) lines.push(`${effect.name}: ${fmtNumberText(effect.text)}`);
  if (it.use) lines.push(fmtNumberText(it.use));
  if (it.type === 'herb') lines.push(...herbSrcLines(k));
  if (adminOn && it.type === 'chest') lines.push(...chestOddsLines(k));
  if (it.set) {
    const state = S?.gearEffects?.sets?.find(s => s.id === it.setId);
    lines.push(`Bộ ${it.set}: ${state?.count || 0}/10 món đang mặc`);
    const def = (DATA.equipmentSets || []).find(s => s.id === it.setId);
    for (const m of def?.milestones || []) lines.push(`${state?.active?.includes(m.count) ? '✓' : '○'} ${m.count} món, cộng thêm: ${m.bonuses.map(b => b.text).join(' · ')}`);
  }
  lines.push(fmtNumberText(it.desc));
  if (hint) lines.push(hint);
  return lines.join('\n').replace(/"/g, '&quot;');
}
/** Nơi rơi linh thảo (DATA.herbDrops): "Chương 1 · Hắc Phong Lâm · ải 10–15 · 8%/chuyến". */
function herbSrcLines(k){
  const src = (DATA.herbDrops || []).filter(h => h.item === k); if (!src.length) return [];
  const pct = v => (Math.round(v * 1000) / 10).toLocaleString('vi-VN') + '%';
  return ['Nơi rơi:', ...src.map(h => `• ${h.chapters.map(c => `Chương ${c + 1} · ${DATA.chapters?.[c]?.name || ''}`).join(', ')} · ${h.minArea > 1 ? `ải ${h.minArea}–${DATA.areas}` : 'mọi ải'} · ${pct(h.chance)}/chuyến${h.max > 1 ? ` (${h.min}–${h.max} cây)` : ''}`)];
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
    ${(o.manuals || []).map(x=>`<p><b>${ITEM[x.item]?.name || x.item} ${pctS(x.pct)}</b> · Công pháp</p>`).join('')}</div>`;
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
  const gender = skinGender(), skins = (DATA.skins || []).filter(s => (s.gender || 'male') === gender);
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

function bagList(){
  // nguyên liệu/trang bị theo kiểu sắp xếp đang chọn, lọc theo danh mục
  const owned = DATA.items.map(i => i.id).filter(k => (S.inventory[k]||0) > 0 && (invCat === 'all' || catOf(ITEM[k]) === invCat));
  const sk = GEAR_SORTS[gearSort].key;
  const gearFirst = gearSort === 'atk' || gearSort === 'hp' || gearSort === 'def' || gearSort === 'grade';
  owned.sort((a, b) => {
    const A = ITEM[a], B = ITEM[b];
    if (gearSort !== 'name' && !!A.slot !== !!B.slot) return (A.slot ? -1 : 1) * (gearFirst ? 1 : -1);
    const x = sk(A), y = sk(B);
    return (typeof x === 'string' ? x.localeCompare(y, 'vi') : (x - y)) || A.name.localeCompare(B.name, 'vi');
  });
  // trang bị: mỗi món một ô; vật phẩm: một ô kèm số lượng
  return owned.flatMap(k => ITEM[k].slot ? Array.from({length: S.inventory[k]}, () => ({k, n: 1})) : [{k, n: S.inventory[k]}]);
}
function power(st){ return Math.round(st.atk*2 + st.def*3 + st.hp*.4 + (st.crit + st.speed)*4); }

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
/** Nền sau nhân vật ở Hành trang: chỉ còn quầng sáng theo màu cảnh giới (BT_PAL của màn Đột phá), tỏa rộng gần kín khung, sáng tối nhẹ như đang thở. */
function drawInventoryAura(t){
  const canvas = $('ivAura'); if (!S || !canvas || !canvas.offsetParent || typeof BT_PAL === 'undefined') return;
  const g = canvas.getContext('2d'), W = 320, H = 320, realmDef = DATA.realms[S.realm] || DATA.realms[0];
  const pal = BT_PAL[realmDef.id] || BT_PAL.luyen_khi;
  const pulse = reduce ? .5 : .5 + .5 * Math.sin(t * 1.3);
  g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.clearRect(0, 0, W, H);
  const cx = W / 2, cy = H * .48, R = W * (.72 + .04 * pulse);   // vòng sáng mở rộng gần hết khung
  const gr = g.createRadialGradient(cx, cy, 0, cx, cy, R);
  gr.addColorStop(0, pal.glow); gr.addColorStop(.45, pal.glow + '99'); gr.addColorStop(1, 'transparent');
  g.globalAlpha = .62 + .2 * pulse; g.fillStyle = gr; g.fillRect(0, 0, W, H);
  g.globalAlpha = 1;
}
function drawInventoryAvatar(t){
  drawInventoryAura(t);
  const canvas = $('ivAvatar');
  if (!S || !canvas || !canvas.offsetParent) return;
  const g = canvas.getContext('2d'), still = canvas.previousElementSibling;
  const hero = skinArt('hero');
  g.clearRect(0,0,320,320);
  const profile = SKIN_ACTOR_SHEETS[hero];
  const thanhSam = hero === 'img/hero_luyenkhi.png';
  const image = thanhSam ? skImg('img/skin/luyenkhi_inventory_breeze.png')
    : profile && !profile.static ? skImg(profile.inventorySrc || `img/skin/${profile.id}_actions.png`) : null;
  const drawn = !!image;
  if (image){
    drawInventoryIdle(g,image,thanhSam ? {fw:384,fh:320,ax:192,ay:308,bodyHeight:210}
      : {fw:480,fh:440,ax:240,ay:420,bodyHeight:profile.bodyHeight},t,reduce);
  }
  canvas.style.visibility = drawn ? 'visible' : 'hidden';
  if (still) still.hidden = drawn;
}
function renderInv(){
  const pane = $('pane-inv'), host = pane?.closest('aside.book'), hostTop = host?.scrollTop, SCROLLERS = '.iv-char-in, #ivGrid, .iv-cats';
  const keep = pane ? [pane.scrollTop, [...pane.querySelectorAll(SCROLLERS)].map(el => [el.scrollTop, el.scrollLeft])] : null;
  const fxStates = new Map([...(pane?.querySelectorAll('details.iv-fx') || [])].map(el => [el.classList.contains('iv-divine') ? 'divine' : 'sources', el.open]));
  renderInvInner();
  pane?.querySelectorAll('details.iv-fx').forEach(el => {
    const key = el.classList.contains('iv-divine') ? 'divine' : 'sources';
    if (fxStates.has(key)) el.open = fxStates.get(key);
  });
  fitBagGrid();
  if (host) host.scrollTop = hostTop;
  if (keep){ pane.scrollTop = keep[0]; pane.querySelectorAll(SCROLLERS).forEach((el, i) => { const k = keep[1][i]; if (k){ el.scrollTop = k[0]; el.scrollLeft = k[1]; } }); }
}
/** Cửa sổ Tu luyện: ô túi nhỏ cỡ ~BAG_CELL px, số cột × hàng tự tăng cho kín khung (không còn lưới 8 × 6 ô to với khoảng trống hai bên).
 *  Ô giãn nhẹ để lấp đủ bề ngang; số ô mỗi trang (BAG_PER_PAGE) = cột × hàng. Màn hẹp (≤ 620px) giữ lưới co theo bề ngang. */
const BAG_CELL = 58, BAG_GAP = 3;
function fitBagGrid(){
  const g = $('ivGrid'); if (!g) return;
  if (MODE !== 'panel' || innerWidth <= 620 || !g.clientHeight){ g.style.removeProperty('--cell'); g.style.removeProperty('--cols'); return; }
  const w = g.clientWidth - 4, h = g.clientHeight - 4;
  const cols = Math.max(6, Math.floor((w + BAG_GAP) / (BAG_CELL + BAG_GAP)));
  const cell = Math.max(32, Math.min(72, Math.floor((w - (cols - 1) * BAG_GAP) / cols)));
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
  if (invSel?.src === 'bag' && !(S.inventory[invSel.k] > 0)) invSel = null;
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
    if (k) return `<button class="iv-slot has r-${ITEM[k].rarity} ${cls}" data-slot="${id}" data-k="${k}" data-card="slot" draggable="true" aria-label="${sd.name}: ${ITEM[k].name}" data-tip="${itemTip(k, `${MS_R}/${MS_L2} tháo · kéo vào hành trang`)}">${itemImg(k)}<span class="lv">${itemLv(ITEM[k])}</span></button>`;
    return `<button class="iv-slot empty ${cls}" data-slot="${id}" aria-label="${sd.name} (trống)" data-tip="${sd.name}&#10;Trống · kéo trang bị vào đây"><span class="nm">${sd.name}</span></button>`;
  };

  // ---- chỉ số ----
  const statRows = [
    ['Tấn công', fmt(st.atk), 0], ['Phòng thủ', fmt(st.def), 0], ['Máu', fmt(st.hp), 0],
    ['Bạo kích', `${Math.round(st.crit)}%`, 0],
    ['Thân pháp', `${Math.round(st.speed)}%`, 1],
  ].map(([n, v, need]) => S.realm >= need
      ? `<div class="st"><span>${n}</span><b>${v}</b></div>`
      : `<div class="st off" data-tip="${n}&#10;Mở ở cảnh giới ${DATA.realms[need].name}"><span>${n}</span><b>${DATA.realms[need].name}</b></div>`).join('');

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
    return `<button class="iv-cell r-${it.rarity} ${it.slot ? 'eq' : ''} ${isSel ? 'sel' : ''} ${multiSel.has(i) ? 'msel' : ''} ${msMode && gearSell(c.k) ? 'mspick' : ''} ${it.slot && !canWear(it) ? 'req' : ''}" data-idx="${i}" data-k="${c.k}" data-card="bag" ${it.slot ? 'draggable="true"' : ''} aria-label="${it.name}${c.n > 1 ? ' × ' + c.n : ''}" data-tip="${itemTip(c.k, rcHint(it))}">${itemImg(c.k)}${c.n > 1 ? `<span class="qty">${short(c.n)}</span>` : it.slot ? `<span class="lv">${itemLv(it)}</span>` : ''}${isSel && Date.now() - sellArm < 3000 ? '<span class="arm">B lần nữa để bán</span>' : isSel && Date.now() - dropArm < 3000 ? '<span class="arm">Del lần nữa để bỏ</span>' : ''}</button>`;
  }).join('');
  const counts = Object.fromEntries(CATS.map(([id]) => [id, 0]));
  for (const k of DATA.items.map(i => i.id)) if ((S.inventory[k]||0) > 0){ counts.all++; counts[catOf(ITEM[k])]++; }


  const nextBag = DATA.realms[S.realm+1]?.bagSlots, nextNote = S.atTop ? '' : `Lên ${S.nextRealmName}: ${nextBag ? `hành trang ${nextBag} ô, ` : ''}${(DATA.realms[S.realm+1]?.equipSlots || []).some(x => !S.equipSlots.includes(x)) ? 'thêm ô trang bị và ' : ''}chỉ số mới`;
  $('pane-inv').innerHTML = `<div class="iv" style="--aura:${r.aura}">
    <section class="iv-char" aria-label="Nhân vật">${wardrobeHtml()}<div class="iv-char-in">
      <header class="iv-name"><b>${S.name}</b><span>Cấp ${S.level} · ${S.realmName} ${S.stageName}</span></header>
      <div class="iv-doll">
        <div class="iv-col">${SLOT_LEFT.map(slotHtml).join('')}</div>
        <div class="iv-fig"><div class="iv-stage"><canvas id="ivAura" class="iv-aura" width="320" height="320" aria-hidden="true"></canvas><img class="hero ref" src="${skinArt('heroStand') || 'img/hero_ref_stand.png'}" alt="${S.name}"><canvas id="ivAvatar" class="iv-avatar" width="320" height="320" role="img" aria-label="Tạo hình nhân vật" style="visibility:hidden"></canvas></div>
          <p class="iv-pow" data-tip="Lực chiến&#10;Tổng hợp tấn công, phòng thủ, máu và chỉ số phụ">Lực chiến <b>${fmt(power(st))}</b></p>
          <div class="iv-skills" data-goskill>${skSlotsHtml('mini')}</div></div>
        <div class="iv-col">${SLOT_RIGHT.map(slotHtml).join('')}</div>
      </div>
      <div class="iv-bars">
        <div class="iv-bar qi" data-tip="Tu vi&#10;Đủ để đột phá tầng tiếp theo"><span>Tu vi</span><div><i id="ivQiFill"></i><em id="ivQiTxt"></em></div></div>
        <div class="iv-bar hp" data-tip="Máu&#10;Hồi đầy sau mỗi chuyến thám hiểm"><span>Máu</span><div><i style="width:100%"></i><em title="${fmtFull(st.hp)} / ${fmtFull(st.hp)}">${fmt(st.hp)}/${fmt(st.hp)}</em></div></div>
        <div class="iv-bar ql" data-tip="Linh khí&#10;Tốc độ hấp thu linh khí mỗi giây"><span>Linh khí</span><div><i style="width:${Math.min(100, 20 + S.rate)}%"></i><em>${fmtDecimal(Math.round(S.rate*10)/10)}/giây</em></div></div>
      </div>
      <div class="iv-stats">${statRows}</div>
      ${gearFxHtml()}
      ${nextNote ? `<p class="iv-next">${nextNote}</p>` : ''}
    </div></section>
    <section class="iv-bag" aria-label="Hành trang">
      <nav class="iv-cats" role="tablist">${CATS.map(([id, n], i) => `<button role="tab" data-cat="${id}" aria-selected="${invCat === id}" data-tip="${n}&#10;Phím ${i+1}">${n}<small>${counts[id]}</small></button>`).join('')}</nav>
      <div class="iv-head"><p class="eyebrow ${used >= cap ? 'full' : ''}">Hành trang ${used}/${cap} ô</p>
        <span class="iv-stone" data-tip="Linh thạch&#10;Bán trang bị để nhận thêm">${itemImg('linh_thach')}${fmt(S.inventory.linh_thach || 0)}</span>${hpStoneHtml('iv-stone hp')}
        <button class="sell-btn" id="quickEquipBtn" aria-label="Trang bị nhanh" data-tip="Trang bị nhanh · phím Q&#10;Mỗi ô mặc món có Chiến lực cao nhất trong hành trang&#10;(tính cả thưởng cùng bộ, chỉ món đủ cảnh giới)"><svg viewBox="0 0 24 24"><path d="M12 3 5 6v5c0 4.5 3 8.3 7 10 4-1.7 7-5.5 7-10V6z"/><path d="m13 7-3 5h4l-3 5"/></svg><span>Mặc nhanh</span></button>
        <div class="sort">
          <button class="sell-btn" id="sellBtn" aria-label="Bán" data-tip="Bán&#10;Mở Luyện chế · Bán: chọn món bỏ vào quầy rồi bán"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8"/><path d="M12 7v10M9.5 9.5h3.8a1.7 1.7 0 0 1 0 3.4h-2.6a1.7 1.7 0 0 0 0 3.4H14.5"/></svg><span>Bán</span></button>
        </div>
        <button class="ib sm" id="arrangeBtn" aria-label="Tự xếp" data-tip="Tự xếp&#10;Về danh mục Tất cả, xếp theo loại · phím A"><svg viewBox="0 0 24 24"><rect x="4" y="4" width="6" height="6"/><rect x="14" y="4" width="6" height="6"/><rect x="4" y="14" width="6" height="6"/><path d="M14 17h6M17 14v6"/></svg></button>
        <div class="sort">
          <button class="ib sm" id="sortBtn" aria-haspopup="listbox" aria-expanded="${sortOpen}" aria-label="Sắp xếp: ${GEAR_SORTS[gearSort].name}" data-tip="Sắp xếp&#10;Đang chọn: ${GEAR_SORTS[gearSort].name}"><svg viewBox="0 0 24 24"><path d="M7 4v16M3.5 16.5 7 20l3.5-3.5"/><path d="M14 6h7M14 11h5M14 16h3"/></svg></button>
          <ul class="sort-menu" role="listbox" aria-label="Sắp xếp hành trang" ${sortOpen ? '' : 'hidden'}>${Object.entries(GEAR_SORTS).map(([k, v]) => `<li role="option" data-sort="${k}" tabindex="0" aria-selected="${k === gearSort}">${v.name}</li>`).join('')}</ul>
        </div></div>
      ${msBarHtml()}
      <div class="iv-grid" id="ivGrid">${cells}</div>
      <nav class="pager iv-pager" aria-label="Trang hành trang">${pagerHtml(bagPage, pages, 'data-bagpage')}<span class="pg-info">Trang ${bagPage + 1}/${pages}</span></nav>
    </section>
  </div>`;
  liveInv();
}
/** Các món nằm trong phạm vi bán hàng loạt: {id: số lượng} (cả hành trang hoặc các ô ở trang đang xem). */
function bulkPool(){
  const pool = {};
  if (bulkScope === 'page'){
    const first = bagPage * BAG_PER_PAGE;
    for (const c of bagList().slice(first, first + BAG_PER_PAGE)) pool[c.k] = (pool[c.k] || 0) + c.n;
  } else for (const [k, c] of Object.entries(S.inventory)) if (c > 0) pool[k] = c;
  for (const k of Object.keys(pool)){ const it = ITEM[k]; if (!it?.sell || it.tier || it.type === 'skin' || (bulkGear && !it.slot)) delete pool[k]; }
  return pool;
}
function bulkSellRows(){
  const pool = bulkPool(), rows = [];
  const tg = (attr, val, on, label) => `<button type="button" ${attr}="${val}" aria-pressed="${on}" class="${on ? 'on' : ''}">${label}</button>`;
  rows.push(`<li role="menuitem" tabindex="0" data-msmode class="bs-row bs-pick ${msMode ? 'armed' : ''}">
      <span class="bs-dots"><svg viewBox="0 0 24 24"><rect x="3" y="3" width="8" height="8"/><rect x="13" y="13" width="8" height="8"/><path d="M5 7l2 2 3-4M15 17l2 2 3-4"/></svg></span>
      <span class="bs-n">${msMode ? 'Tắt chọn từng món' : 'Chọn từng món để bán'}</span><small>${msMode ? `đã chọn ${multiSel.size}` : 'nhấp vào món'}</small></li>`);
  rows.push(`<li class="bs-head" role="presentation"><b>Bán theo độ hiếm</b>
    <div class="bs-tg" role="group" aria-label="Phạm vi">${tg('data-bulkscope', 'all', bulkScope === 'all', 'Cả hành trang')}${tg('data-bulkscope', 'page', bulkScope === 'page', `Trang ${bagPage + 1}`)}</div>
    <label class="bs-ck"><input type="checkbox" data-bulkgear ${bulkGear ? 'checked' : ''}> Chỉ trang bị</label></li>`);
  for (let m = 0; m < 4; m++){
    let n = 0, gain = 0;
    for (const [k, c] of Object.entries(pool)){ if (RAR_KEYS.indexOf(ITEM[k].rarity) > m) continue; n += c; gain += ITEM[k].sell * c; }
    const armed = bulkArm && bulkArm.m === m && Date.now() - bulkArm.t < 3000;
    const dots = RAR_KEYS.slice(0, m + 1).map(r => `<i style="background:${RARITY[r].c}"></i>`).join('');
    const label = m ? `${RARITY[RAR_KEYS[m]].n} trở xuống` : `Chỉ ${RARITY.common.n}`;
    rows.push(`<li role="menuitem" tabindex="0" data-sellmax="${m}" class="bs-row ${n ? '' : 'off'} ${armed ? 'armed' : ''}">
      <span class="bs-dots">${dots}</span><span class="bs-n" style="color:${n && !armed ? RARITY[RAR_KEYS[m]].c : ''}">${armed ? 'Bấm lần nữa để bán' : label}</span>
      <small>${n ? `${n} món · <b>+${fmt(gain)}</b>` : 'không có'}</small></li>`);
  }
  rows.push(`<li class="bs-foot" role="presentation">Không bán đồ đang mặc · từ ${RARITY.rare.n} trở lên bấm 2 lần để xác nhận</li>`);
  return rows.join('');
}
/** Thẻ chi tiết vật phẩm (giấy da) hiện trong tooltip khi rê chuột: chỉ số, yêu cầu, giá bán, so sánh với đồ đang mặc. */
// ---- hiệu ứng riêng của trang bị đang mặc (giống GameService.GearEffects: cộng dồn, áp trần) ----
function gearFxHtml(){
  const snapshot = S?.gearEffects;
  if (!snapshot) return '';
  const effects = (snapshot.effects || []).map(e => {
    const capped = e.raw > e.effective;
    return `<span>${e.name}</span> <b>+${fmtDecimal(e.effective)}${e.unit}</b>${capped ? ` <small>(tổng ${fmtDecimal(e.raw)}, trần ${e.cap})</small>` : ''}`;
  });
  const sets = (snapshot.sets || []).filter(s => s.count).map(s =>
    `<span>${s.name} ${s.count}/10</span> <b>${s.active.length ? `mốc ${s.active.join(' · ')}` : 'chưa kích hoạt'}</b>${s.next ? ` <small>mốc kế: ${s.next}</small>` : ''}`);
  const sourceFields = [['atk','công',''],['def','thủ',''],['hp','máu',''],['atkPct','công','%'],['defPct','thủ','%'],['hpPct','máu','%'],['crit','bạo kích',' điểm %'],['speed','thân pháp',' điểm %'],['qi','tu luyện','%']];
  const sources = (S.statSources || []).map(s => {
    const values = sourceFields.filter(([key]) => s[key]).map(([key, name, unit]) => `+${fmtDecimal(s[key])}${unit} ${name}`);
    return values.length ? `<p><b>${s.name}:</b> ${values.join(' · ')}</p>` : '';
  }).join('');
  const limits = (S.combatLimits || []).map(e => `<p>${e.name}: <b>${fmtDecimal(e.effective)}${e.unit}</b> <small>(tổng ${fmtDecimal(e.raw)}, trần ${e.cap})</small></p>`).join('');
  const divine=snapshot.divine;
  const divineHtml=divine ? `<details class="iv-fx iv-divine" ${divine.tier ? 'open' : ''}><summary>◇ Thần thông bộ · ${divine.tier ? divine.name : 'Chưa kích hoạt'}</summary><p>${divine.setName ? `Bộ ${divine.setName} · ` : ''}Cần 10 món cùng bộ; chỉ kích hoạt mốc phẩm cao nhất.</p>${(divine.progress||[]).map(p=>`<p style="opacity:${p.active?1:.65}">${p.active?'◆':'◇'} <b>${p.quality} · ${p.name} (${p.count}/10)</b><br>${p.description}</p>`).join('')}</details>` : '';
  return `<div class="iv-fx" data-tip="Hiệu ứng trang bị và bộ do server tính; trần tổng với công pháp/kỹ năng áp dụng khi chiến đấu.">${sets.concat(effects).join(' · ')}</div>${divineHtml}<details class="iv-fx"><summary>Nguồn chỉ số và giới hạn</summary>${sources}${limits}<small>Giá trị nguồn trước trần và suy yếu; chỉ số cuối ở bảng nhân vật. Thân pháp tăng né tránh; cộng Vân bộ và công pháp, tổng né tối đa 50%. Mỗi 1% Thân pháp giảm 1% thời gian di chuyển và giao chiến (tối đa 60%); mỗi chuyến tối thiểu 3 giây.</small></details>`;
}
function itemSetHtml(it){
  const def = (DATA.equipmentSets || []).find(s => s.id === it.setId);
  if (!def) return it.set ? `<p class="set">◆ Bộ ${it.set}</p>` : '';
  const state = S?.gearEffects?.sets?.find(s => s.id === def.id);
  return `<div class="set" style="--sc:${SET_COLOR[it.set] || '#2e8b6a'}"><p>◆ ${def.name} (${state?.count || 0}/10)</p>${def.milestones.map(m => {
    const active = state?.active?.includes(m.count);
    return `<p style="opacity:${active ? 1 : .5}">${active ? '✓' : '○'} ${m.count} món — cộng thêm: ${m.bonuses.map(b => b.text).join(' · ')}</p>`;
  }).join('')}</div>`;
}
function itemCardHtml(k, src){
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
  const del = `${KBD('Del')} bỏ`, act2 = `${MS_R}<i>/</i>${MS_L2}`;
  const hint = src === 'slot' ? `${act2} tháo · kéo vào hành trang · ${KBD('U')} tháo`
    : it.slot ? `${act2} mặc · kéo vào ô trang bị · ${KBD('B')} bán · ${del}` : it.type === 'chest' ? `${act2} mở · ${KBD('Ctrl')}+${MS_R} mở hết · ${del}` : it.type === 'skin' ? `${act2} mở · ${KBD('B')} bán · ${del}` : canUse(it) ? `${act2} dùng · ${del}`
    : rcCraft(it) ? `${MS_R} mở Luyện chế · Đan dược · ${del}` : del;
  return `<div class="iv-card">
    <p class="nm rc-${it.rarity}">${it.name}</p>
    <p class="sub">${it.itemLevel ? `Cấp ${it.itemLevel} · ` : ''}${qWord(it.rarity || 'common', true)} · ${it.kind || ''}${S.inventory[k] > 1 && !it.slot ? ` · có ${fmt(S.inventory[k])}` : ''}</p>
    ${it.slot ? `<p class="req ${okRealm ? '' : 'bad'}">Yêu cầu cảnh giới: ${DATA.realms[it.minRealm||0].name}</p>` : ''}
    ${it.slot ? [
      ['Thuộc tính cơ bản', STAT_KEYS.slice(0, 4)],   // kể cả Bạo kích (vũ khí): không còn dòng riêng
      ['Thuộc tính hỗ trợ', STAT_KEYS.slice(4)]
    ].map(([title, keys]) => {
      const lines = keys.filter(([key]) => it[key]).map(([key, name, unit]) => `${name}: +${it[key]}${unit}`);
      return lines.length ? `<p class="sts"><b>${title}</b><br>${lines.join('<br>')}</p>` : '';
    }).join('') : itemStats(it).length ? `<p class="sts">${itemStats(it).join('<br>')}</p>` : ''}
    ${it.special ? `<p class="spc">${fmtNumberText(it.special)}</p>` : ''}
    ${it.use ? `<p class="spc">${fmtNumberText(it.use)}</p>` : ''}
    ${it.effects?.length ? `<p class="eff">${it.effects.map(e => `<span class="qc-${it.rarity}">◇ ${e.name}: ${fmtNumberText(e.text)}</span>`).join('<br>')}</p>` : ''}
    ${itemSetHtml(it)}
    <p class="ds">“${fmtNumberText(it.desc)}”</p>
    <p class="sv">${it.sell ? `Giá bán: ${fmt(it.sell)} linh thạch` : 'Không bán được'}</p>
    ${adminOn && it.type === 'chest' ? chestOddsHtml(k) : ''}${cmp}${hint ? `<p class="hn">${hint}</p>` : ''}</div>`;
}
function liveInv(){
  const f = $('ivQiFill'); if (!f || !S) return;
  const q = qiNow(); f.style.width = Math.min(100, q / S.qiReq * 100) + '%'; setQiDisplay($('ivQiTxt'), q, S.qiReq);
}

// ---- tương tác: chọn, nhấp đúp, kéo thả, phím tắt ----
function invDo(action){
  const sel = invSel ? ITEM[invSel.k] : null;
  if (action === 'equip' && invSel?.src === 'bag' && sel?.slot) act('equip/' + invSel.k);
  else if (action === 'unequip' && invSel?.src === 'slot') act('unequip/' + invSel.slot);
  else if (action === 'use' && invSel?.src === 'bag' && canUse(sel)) act('use/' + invSel.k);
  else if (action === 'drop' && invSel?.src === 'bag'){
    if (Date.now() - dropArm < 3000){ dropArm = 0; act('drop/' + invSel.k); }
    else { dropArm = Date.now(); renderInv(); setTimeout(() => { if (dropArm && Date.now() - dropArm >= 3000){ dropArm = 0; renderInv(); } }, 3100); }
  }
  else if (action === 'sell' && invSel?.src === 'bag' && sel?.sell){
    // đồ từ Huyền cấp trở lên phải bấm hai lần để tránh bán nhầm
    if (RAR_KEYS.indexOf(sel.rarity) < 2 || Date.now() - sellArm < 3000){ sellArm = 0; act('sell/' + invSel.k); }
    else { sellArm = Date.now(); renderInv(); setTimeout(() => { if (sellArm && Date.now() - sellArm >= 3000){ sellArm = 0; renderInv(); } }, 3100); }
  }
  else if (action === 'compare'){ compareOn = !compareOn; renderInv(); }
  else if (action === 'sort'){ sortOpen = !sortOpen; hideTip(); renderInv(); }
  else if (action === 'arrange'){ setCat('all'); gearSort = 'slot'; try { localStorage.setItem('van-dao-gear-sort', 'slot'); } catch(e){} renderInv(); $('ivGrid') && ($('ivGrid').scrollTop = 0); }
}
function setCat(c){ invCat = c; bagPage = 0; invSel = invSel?.src === 'slot' ? invSel : null; try { localStorage.setItem('van-dao-inv-cat', c); } catch(e){} renderInv(); }
function initInventory(){
  const pane = $('pane-inv');
  let lastTap = {key:'', t:0};
  const pickSort = k => { gearSort = k; sortOpen = false; try { localStorage.setItem('van-dao-gear-sort', gearSort); } catch(err){} hideTip(); renderInv(); };
  document.addEventListener('click', e => {
    if (sortOpen && !e.target.closest('.sort') && !e.target.closest('[data-act="sort"]')){ sortOpen = false; renderInv(); }
    if (sellOpen && !e.target.closest('.sort')){ sellOpen = false; renderInv(); }
  });
  pane.addEventListener('click', e => {
    if (e.target.closest('#wardBtn')){ wardOpen = !wardOpen; hideTip(); renderInv(); return; }
    if (e.target.closest('[data-wardclose]')){ wardOpen = false; renderInv(); return; }
    const gender = e.target.closest('[data-gender]');
    if (gender){ if (!gender.disabled) changeWardrobeGender(gender.dataset.gender); return; }
    const sk = e.target.closest('[data-skin]');
    if (sk){ if (!sk.disabled && sk.dataset.skin !== (S.skin || '')) act('skin?skin=' + encodeURIComponent(sk.dataset.skin)); return; }
    if (wardOpen && !e.target.closest('.iv-ward')){ wardOpen = false; renderInv(); }
    if (e.target.closest('#sortBtn')){ sortOpen = !sortOpen; sellOpen = false; hideTip(); renderInv(); return; }
    if (e.target.closest('[data-goskill]')){ const sl = e.target.closest('[data-slot]'); if (sl && S.skillSlots[+sl.dataset.slot]) skSel = S.skillSlots[+sl.dataset.slot]; skEl = 'hoa'; openTab('skill'); return; }
    if (e.target.closest('#arrangeBtn')){ hideTip(); invDo('arrange'); return; }
    if (e.target.closest('[data-msmode]')){ msMode = !msMode; if (!msMode) msClear(); sellOpen = false; hideTip(); renderInv(); return; }
    const msb = e.target.closest('[data-ms]');
    if (msb){ const a = msb.dataset.ms; hideTip(); if (a === 'sell') msSell(); else { if (a === 'clear') msClear(); else if (a === 'page') msPage(); else if (a === 'done'){ msMode = false; msClear(); } renderInv(); } return; }
    if (e.target.closest('#quickEquipBtn')){ sellOpen = false; sortOpen = false; hideTip(); act('equip-best'); return; }
    if (e.target.closest('#sellBtn')){ sellOpen = false; sortOpen = false; hideTip(); openTab('sell', true); return; }
    const bsc = e.target.closest('[data-bulkscope]');
    if (bsc){ bulkScope = bsc.dataset.bulkscope; bulkArm = null; try { localStorage.setItem('van-dao-bulk-scope', bulkScope); } catch(err){} renderInv(); return; }
    const bge = e.target.closest('[data-bulkgear]');
    if (bge){ bulkGear = bge.checked; bulkArm = null; try { localStorage.setItem('van-dao-bulk-gear', bulkGear ? '1' : '0'); } catch(err){} renderInv(); return; }
    if (e.target.closest('.bs-head,.bs-foot')) return;
    const sm = e.target.closest('[data-sellmax]');
    if (sm){
      const m = +sm.dataset.sellmax;
      if (sm.classList.contains('off')) return;
      // gồm cả đồ Huyền cấp trở lên: bấm hai lần để tránh bán nhầm
      if (m >= 2 && !(bulkArm && bulkArm.m === m && Date.now() - bulkArm.t < 3000)){ bulkArm = {m, t: Date.now()}; renderInv(); return; }
      bulkArm = null; sellOpen = false;
      const q = `sell-bulk?max=${m}&gear=${bulkGear}` + (bulkScope === 'page' ? '&items=' + encodeURIComponent(Object.entries(bulkPool()).map(([k, c]) => k + ':' + c).join(',')) : '');
      act(q); return;
    }
    const so = e.target.closest('[data-sort]'); if (so){ pickSort(so.dataset.sort); return; }
    const ca = e.target.closest('[data-cat]'); if (ca){ setCat(ca.dataset.cat); return; }
    const pg = e.target.closest('[data-bagpage]'); if (pg){ if (!pg.disabled){ bagPage = +pg.dataset.bagpage; renderInv(); } return; }
    const ac = e.target.closest('[data-act]'); if (ac){ if (!ac.disabled) invDo(ac.dataset.act); return; }
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
      invSel = {src:'bag', k: cell.dataset.k, idx: +cell.dataset.idx}; dropArm = 0; sellArm = 0;
      const it = ITEM[cell.dataset.k];
      if (dbl && it.slot) invDo('equip'); else if (dbl && canUse(it)) invDo('use'); else renderInv();
      return;
    }
    if (slot){ invSel = {src:'slot', k: slot.dataset.k, slot: slot.dataset.slot}; if (dbl) invDo('unequip'); else renderInv(); return; }
  });
  // chuột phải: mặc / dùng đồ trong túi, tháo đồ đang mặc, tháo kỹ năng trong ô
  pane.addEventListener('contextmenu', e => {
    const cell = e.target.closest('.iv-cell[data-k]'), slot = e.target.closest('.iv-slot.has'), ss = e.target.closest('.sk-slot[data-slotsk]');
    if (!cell && !slot && !ss) return;
    e.preventDefault(); hideTip(); lastTap = {key:'', t:0};
    if (ss){ act(`skillslot/${ss.dataset.slot}`); return; }
    if (cell){
      invSel = {src:'bag', k: cell.dataset.k, idx: +cell.dataset.idx}; dropArm = 0; sellArm = 0;
      const it = ITEM[cell.dataset.k];
      if (it?.type === 'chest' && e.ctrlKey) act(`use/${cell.dataset.k}?n=0`);     // Ctrl + chuột phải: mở hết rương loại này
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
    drag = cell ? {src:'bag', k: cell.dataset.k} : slot ? {src:'slot', k: slot.dataset.k, slot: slot.dataset.slot} : null;
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
    if (drag.src === 'bag' && slot) act(`equip/${drag.k}?slot=${slot.dataset.slot}`);
    else if (drag.src === 'slot' && grid) act('unequip/' + drag.slot);
    drag = null;
  });
  // phím tắt khi đang xem Hành trang
  document.addEventListener('keydown', e => {
    if ($('pane-inv').hidden || e.target.closest('input,select,textarea') || e.ctrlKey || e.metaKey || e.altKey) return;
    const k = e.key.toLowerCase();
    if (sortOpen){ const li = e.target.closest?.('[data-sort]'); if (li && (k === 'enter' || k === ' ')){ e.preventDefault(); pickSort(li.dataset.sort); } if (k === 'escape'){ sortOpen = false; renderInv(); } return; }
    if (sellOpen){ const li = e.target.closest?.('[data-sellmax],[data-msmode]'); if (li && (k === 'enter' || k === ' ')){ e.preventDefault(); li.click(); return; } if (k === 'escape'){ sellOpen = false; renderInv(); return; } }
    if (k === 'escape' && (multiSel.size || msMode)){ msClear(); msMode = false; renderInv(); return; }
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
      invSel = {src:'bag', k: list[i].k, idx: i, _follow: true}; renderInv();
      pane.querySelector(`.iv-cell[data-idx="${i}"]`)?.scrollIntoView({block:'nearest'});
    }
    if (k === 'pageup' || k === 'pagedown'){ e.preventDefault(); bagPage += k === 'pagedown' ? 1 : -1; renderInv(); }
  });
}

const short = n => Math.abs(n) >= 1e6 ? fmtCompact(n) : n >= 1e4 ? Math.floor(n/1e3) + 'K' : String(n);
