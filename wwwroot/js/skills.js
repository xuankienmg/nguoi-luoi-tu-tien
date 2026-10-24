/* Vấn Đạo · js/skills.js: tab Công pháp + xem trước chiêu Hỏa/Thủy. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// CÔNG PHÁP: cây kỹ năng theo hệ. Khung dùng chung cho mọi hệ — cây vẽ hoàn toàn từ dữ liệu
// (DATA.skillTree: hàng/cột trên lưới 7×5 + kỹ năng tiền đề), thêm hệ mới chỉ cần thêm dữ liệu ở server.
// =====================================================================
// ---- Hỏa Cầu: animation 8 pha theo bản thiết kế, sprite sheet img/fx_hoa_cau_anim.png (18 khung 64px × 3 hàng cấp) ----
// tư thế chuẩn bị → tụ hỏa → hình thành → phóng ra → bay → va chạm → tan biến → kết thúc. Sinh lại sheet: tools/gen_fireball.py
const HC = {sheet: 'img/fx_hoa_cau_anim.png', N: 64, rest: .8,
  phases: [['prep', .2], ['ignite', .2], ['form', .2], ['launch', .2], ['fly', .6], ['hit', .3], ['fade', .3], ['end', .2]]};
const HC_TIER_NAME = ['Hỏa Cầu Nhỏ', 'Hỏa Cầu Trung', 'Hỏa Cầu Lớn'];
const hcTier = lv => lv >= 5 ? 2 : lv >= 3 ? 1 : 0;           // cấp 1–2 Nhỏ · 3–4 Trung · 5 Lớn
const HC_BALL_DX = {launch: 2, fly: 8};                          // tâm cầu lệch so với tâm khung (px sheet)
function hcAt(e){                                               // e: giây tính từ lúc bắt đầu thi triển
  let acc = 0;
  for (const [name, d] of HC.phases){ if (e < acc + d) return {name, k: (e - acc) / d}; acc += d; }
  return null;
}
function hcFrame(ph, t){
  const k = ph.k, q = n => Math.min(n - 1, Math.floor(k * n));
  return {ignite: q(3), form: 3 + q(3), launch: 6, fly: 7 + Math.floor(t * 14) % 4, hit: 11 + q(3), fade: 14 + q(2), end: 16 + q(2)}[ph.name] ?? -1;
}
function hcDraw(g, sheet, tier, f, cx, cy, sc = 1){
  if (!sheet || f < 0) return;
  const N = HC.N, d = Math.round(N * sc);
  g.globalAlpha = 1;
  g.drawImage(sheet, f * N, tier * N, N, N, Math.round(cx - d / 2), Math.round(cy - d / 2), d, d);
}
const hcFlashCache = new WeakMap();
function hcFlash(img){                                          // bóng sáng của quái lúc trúng hỏa cầu
  let c = hcFlashCache.get(img); if (c) return c;
  c = document.createElement('canvas'); c.width = img.naturalWidth; c.height = img.naturalHeight;
  const x = c.getContext('2d'); x.drawImage(img, 0, 0); x.globalCompositeOperation = 'source-atop'; x.fillStyle = '#fff4d0'; x.fillRect(0, 0, c.width, c.height);
  hcFlashCache.set(img, c); return c;
}
let skEl = 'hoa', skSel = 'liet_diem', skFade = 0, skPick = null, skAnimT0 = 0;
let skScrollEl = '';   // hệ của lần vẽ cây trước: cùng hệ thì giữ vị trí cuộn
// Chiến đấu: ô 0 = ĐÒN THƯỜNG (Hỏa Cầu / Thủy Cầu, luôn sẵn có, không dùng điểm) · ô 1 = KỸ NĂNG chủ động (tung khi đầy nộ)
// Nộ: mỗi đòn đánh thường +RAGE_HIT, đủ RAGE_MAX thì tung kỹ năng ở ô 1 (khớp SkillData.RagePerHit / RageMax ở server)
const RAGE_HIT = 25, RAGE_MAX = 100, RAGE_HITS = Math.ceil(RAGE_MAX / RAGE_HIT);
const SLOT_NAMES = ['Đòn thường', 'Kỹ năng'], SLOT_IDX = [0, 1];
const fitsSlot = (s, i) => !!s && (i === 0 ? s.category === 'Đòn thường' : ['Công','Tuyệt kỹ','Thủ','Khống','Thân pháp'].includes(s.category));
const skTree = s => s.category === 'Đòn thường' ? 'basic' : (s.category === 'Căn cơ' || s.category === 'Bị động') ? 'passive' : 'active';
const skBasicId = () => (S.skillSlots && S.skillSlots[0]) || 'hoa_cau';
// Công pháp kích hoạt: chỉ một hệ, quyết định đòn thường; bị động hệ khác chỉ phát huy SK_INACTIVE (khớp CultivationManuals.InactiveRate)
const SK_INACTIVE = .5;
const skActive = () => S?.activeManual || skById(skBasicId())?.element || 'hoa';
const skRate = el => el === skActive() ? 1 : SK_INACTIVE;
// Chiêu chủ động hệ Hỏa: mỗi lần tung có 10% × cấp cơ hội thiêu đốt (khớp SkillData.ActiveBurnPerLv)
const SK_BURN_PER_LV = 10;
const skBurnChance = (s, lv) => s.element === skActive() && s.element === 'hoa' && skTree(s) === 'active' && s.dmgBase > 0 ? SK_BURN_PER_LV * Math.max(0, lv) : 0;
function skSecondaryChance(skill, level, activeManual = skActive()){
  if (skill.secondaryEffect?.target === 'monster' && skill.element !== activeManual) return 0;
  return skill.secondaryEffect ? skill.secondaryEffect.chancePerLv * Math.max(0,Math.min(skill.maxLv,level)) : 0;
}
/** Ô chiến đấu (dùng chung cho Công pháp và Hành trang). */
function skSlotsHtml(cls = ''){
  return SLOT_IDX.map(i => {
    const id = i === 0 ? skBasicId() : (S.skillSlots || [])[1], s = id && skById(id), pick = skPick && fitsSlot(skById(skPick), i);
    const tip = s ? `${SLOT_NAMES[i]}: ${s.name}${i ? ` · Lv. ${skLv(id)}` : ` · mỗi lượt`}&#10;${s.effect}` : `${SLOT_NAMES[i]}: trống&#10;Chọn một kỹ năng chủ động đã lĩnh ngộ`;
    return `<button class="sk-slot ${i === 1 ? 'ult' : ''} ${s ? 'has' : ''} ${pick ? 'pick' : ''} ${cls}" data-slot="${i}" data-tip="${tip}" ${s && i ? `draggable="true" data-slotsk="${id}"` : ''}>
      ${s ? `<img style="--sk-icon-filter:${skIconStyle(s)}" src="${skIcon(s)}" alt="">` : `<span class="emp">★</span>`}<small>${SLOT_NAMES[i]}</small></button>`;
  }).join('');
}
try { skEl = localStorage.getItem('van-dao-sk-el') || 'hoa'; skSel = localStorage.getItem('van-dao-sk-sel') || 'liet_diem'; } catch(e){}
const SK_CAT = {'Căn cơ':'#c9a86a', 'Bị động':'#5fbf9a', 'Đòn thường':'#f8b04a', 'Công':'#f08a2a', 'Thủ':'#d8b04c', 'Khống':'#e0521e', 'Thân pháp':'#f8d25a', 'Tuyệt kỹ':'#ff5a3a'};
const SK_TYPE = {basic: 'Đánh thường', active: 'Chủ động', passive: 'Bị động'};
const skById = id => DATA.skillTree.find(s => s.id === id);
const skOfEl = el => DATA.skillTree.filter(s => s.element === el);
const skLv = id => S.skills[id] || 0;
const skCost = (s, lv) => lv === 0 ? s.cost : 1 + Math.floor(lv / 2);
const elOf = id => { const e=DATA.elements.find(e=>e.id===id); return e ? {...e,open:!!S?.unlockedElements?.includes(id)} : undefined; };
// Tranh cùng hệ được phân biệt bằng bảng màu riêng ở mọi ô và phần xem trước.
const SK_ART = {than_hoa:'phan_thien', hoa_than_phap:'phuong_hoang', hu_khong_hoa:'xich_diem_thien', thai_hu_hoa:'hoa_linh_chan_y', hon_nguyen_hoa:'hoa_long', hoa_dao_hop_nhat:'hoa_linh_can', thien_ha:'thuong_hai', thuy_than_phap:'huyen_minh', hu_hai:'thuy_lao', thai_hu_thuy:'thuy_linh_chan_y', hon_nguyen_thuy:'thuy_lao', thuy_dao_hop_nhat:'thuy_linh_can',
  // Đại Thừa: tạm dùng icon chiêu bậc dưới, đổi màu ở SK_ICON_FILTER
  thai_so_hoa:'xich_diem_thien', hoa_dao_dai_thua:'hoa_linh_chan_y', thai_so_thuy:'thuong_hai', thuy_dao_dai_thua:'thuy_linh_chan_y', thai_so_moc:'hon_nguyen_moc', moc_dao_dai_thua:'moc_dao_hop_nhat',
  thai_so_tho:'hon_nguyen_tho', tho_dao_dai_thua:'tho_dao_hop_nhat', thai_so_loi:'hon_nguyen_loi', loi_dao_dai_thua:'loi_dao_hop_nhat',
  thai_so_bang:'hon_nguyen_bang', bang_dao_dai_thua:'bang_dao_hop_nhat', thai_so_phong:'hon_nguyen_phong', phong_dao_dai_thua:'phong_dao_hop_nhat'};
const SK_ICON_FILTER = {
  than_hoa:'hue-rotate(-30deg) saturate(1.5)',
  hoa_than_phap:'hue-rotate(35deg) saturate(1.3)',
  hu_khong_hoa:'hue-rotate(255deg) saturate(1.3)',
  thai_hu_hoa:'hue-rotate(285deg) saturate(1.2)',
  hon_nguyen_hoa:'hue-rotate(25deg) saturate(.75) brightness(1.25)',
  hoa_dao_hop_nhat:'hue-rotate(40deg) saturate(.7) brightness(1.3)',
  hon_nguyen_thuy:'hue-rotate(35deg) saturate(1.3) brightness(1.15)',
  thuy_dao_hop_nhat:'hue-rotate(25deg) brightness(1.2)',
  thai_so_hoa:'hue-rotate(20deg) saturate(1.4) brightness(1.2)', hoa_dao_dai_thua:'hue-rotate(30deg) saturate(.8) brightness(1.3)',
  thai_so_thuy:'hue-rotate(-30deg) saturate(1.4) brightness(1.15)', thuy_dao_dai_thua:'hue-rotate(-20deg) brightness(1.25)',
  thai_so_moc:'hue-rotate(30deg) saturate(1.3) brightness(1.15)', moc_dao_dai_thua:'hue-rotate(35deg) brightness(1.2)',
  thai_so_tho:'hue-rotate(-15deg) saturate(1.4) brightness(1.15)', tho_dao_dai_thua:'hue-rotate(-15deg) brightness(1.2)', thai_so_loi:'hue-rotate(25deg) saturate(1.3) brightness(1.2)', loi_dao_dai_thua:'hue-rotate(25deg) brightness(1.2)',
  thai_so_bang:'hue-rotate(-20deg) saturate(1.3) brightness(1.15)', bang_dao_dai_thua:'hue-rotate(-20deg) brightness(1.2)', thai_so_phong:'hue-rotate(25deg) saturate(1.3) brightness(1.15)', phong_dao_dai_thua:'hue-rotate(25deg) brightness(1.2)'
};
const skIconStyle = s => SK_ICON_FILTER[s.id] || 'none';
const skIcon = s => `img/sk_${SK_ART[s.id] || s.id}.png`;
const skFactor = (s, lv) => lv > 0 ? lv + (s.lead || 0) : 0;
const realmName = i => DATA.realms[i]?.name || DATA.lockedRealms?.[i - DATA.realms.length] || '?';

/** Trạng thái một kỹ năng: locked · avail (lĩnh ngộ / nâng được) · learned · max, kèm danh sách điều kiện. */
function skState(s){
  const el = elOf(s.element), lv = skLv(s.id), max = lv >= s.maxLv, cost = skCost(s, lv);
  const reqs = [{ok: S.realm >= s.minRealm, t: `${realmName(s.minRealm)} cảnh`}]
    .concat(s.req.map(r => { const [rid, rl] = r.split(':'); return {ok: skLv(rid) >= +rl, t: `${skById(rid).name} Lv. ${rl}`}; }));
  if (!max) reqs.push({ok: S.skillPoints >= cost, t: `${cost} điểm kỹ năng`});
  const why = (el.open ? [] : ['Cần học bí kíp công pháp hệ này']).concat(reqs.filter(r => !r.ok).map(r => 'Cần ' + r.t));
  const reqOk = el.open && reqs.slice(0, reqs.length - (max ? 0 : 1)).every(r => r.ok);
  const st = max ? 'max' : why.length ? (lv > 0 ? 'learned' : 'locked') : 'avail';
  return {lv, st, why, reqs, cost, max, reqOk, learned: lv > 0};
}
const pct1 = v => (Math.round(v * 10) / 10) + '%';
/** Bảng màu nước (tối → sáng) cho hoạt ảnh hệ Thủy. */
const WP = ['#0a2350', '#1a52a8', '#3189e6', '#79c6ff', '#e4f6ff'];
// ---- Thương Hải Quyết: thủy long theo bản thiết kế (img/fx_thuy_long.png, 20 khung 285×157 xếp ngang) ----
// 0–5 xoáy nước tụ · 6–11 rồng trồi lên · 12–14 rồng lao tới · 15–17 phun thủy triều · 18–19 tan biến
const TL = {src: 'img/fx_thuy_long.png', fw: 285, fh: 157};
const tlImg = new Image(); tlImg.src = TL.src;
/** Khung thủy long ở tiến độ s của lượt (0–1): f = khung, at = 'hero' (trước mặt tu sĩ) · 'fly' (k: 0→1 lao tới) · 'target' (trước quái). */
function tlFrame(s){
  if (s < 0) return null;
  if (s < .2) return {f: Math.min(5, Math.floor(s / .2 * 6)), at: 'hero'};
  if (s < .38) return {f: 6 + Math.min(5, Math.floor((s - .2) / .18 * 6)), at: 'hero'};
  if (s < .5) return {f: 12 + Math.min(2, Math.floor((s - .38) / .12 * 3)), at: 'fly', k: (s - .38) / .12};
  if (s < .75) return {f: 15 + Math.min(2, Math.floor((s - .5) / .25 * 3)), at: 'target'};
  if (s < .9) return {f: 18 + Math.min(1, Math.floor((s - .75) / .15 * 2)), at: 'target'};
  return null;
}
/** Vẽ thủy long: hx = mép trước tu sĩ, tx = tâm quái, cy = ngang ngực (cùng hệ toạ độ của g2), h = chiều cao nhân vật. */
function tlDraw(g2, s, hx, tx, cy, h){
  const fr = tlFrame(s); if (!fr || !tlImg.complete || !tlImg.naturalWidth) return false;
  const k = h * 1.45 / TL.fh, w = TL.fw * k, hh = TL.fh * k;
  const tgtX = tx - w * .3;                                       // khung phun nước: đầu rồng bên trái, thủy triều bên phải đập vào quái
  const x = fr.at === 'hero' ? hx + w * .25 : fr.at === 'fly' ? hx + w * .25 + (tgtX - hx - w * .25) * fr.k : tgtX;
  g2.save(); g2.imageSmoothingEnabled = true; g2.imageSmoothingQuality = 'high';
  g2.drawImage(tlImg, fr.f * TL.fw, 0, TL.fw, TL.fh, x - w / 2, cy - hh / 2, w, hh);
  g2.restore();
  return true;
}
// ---- Thủy Lao Bạo: lao cầu phù văn theo bản thiết kế (img/fx_thuy_lao.png, 25 khung 160×160 xếp ngang) ----
// 0–4 xoáy nước tụ · 5–6 lưỡi nước khép lại · 7–10 lao cầu phù văn giam quái · 11–12 dồn nén · 13–21 nổ băng · 22–24 tan biến
const TLAO = {src: 'img/fx_thuy_lao.png', fw: 160, fh: 160};
const tlaoImg = new Image(); tlaoImg.src = TLAO.src;
const TLAO_SEG = [[0, .12, 0, 4], [.12, .22, 5, 6], [.22, .5, 7, 10], [.5, .6, 11, 12], [.6, .82, 13, 21], [.82, .95, 22, 24]];
/** Khung Thủy Lao ở tiến độ s của lượt (0–1), −1 = không vẽ. */
function tlaoFrame(s){
  const g = TLAO_SEG.find(([a, b]) => s >= a && s < b); if (!g) return -1;
  const [a, b, f0, f1] = g, n = f1 - f0 + 1;
  return f0 + Math.min(n - 1, Math.floor((s - a) / (b - a) * n));
}
/** Vẽ Thủy Lao bao lấy quái: tx, cy = tâm quái, h = chiều cao nhân vật (cùng hệ toạ độ của g2). */
function tlaoDraw(g2, s, tx, cy, h){
  const f = tlaoFrame(s); if (f < 0 || !tlaoImg.complete || !tlaoImg.naturalWidth) return false;
  const d = h * 1.7;
  g2.save(); g2.imageSmoothingEnabled = true; g2.imageSmoothingQuality = 'high';
  g2.drawImage(tlaoImg, f * TLAO.fw, 0, TLAO.fw, TLAO.fh, tx - d / 2, cy - d / 2, d, d);
  g2.restore();
  return true;
}
// ---- Vạn Thủy Tiễn: mưa tên nước theo bản thiết kế ----
// img/fx_thuy_tien.png (8 khung 160×160): 0–5 xoáy nước tụ · 6–7 năm tên nước hiện hình quạt
// img/fx_thuy_tien_x.png: tách riêng từng mũi tên (5 hàng = 5 mũi, từ trên xuống; đã xoay nằm ngang quanh tâm quạt px, py)
//   cột 0–1 quạt quay ngang · 2–5 tên lấp lánh chờ bắn · 6–9 tên kéo sóng nước khi bay · 10–12 đâm trúng · 13–16 tan biến
const TTIEN = {src: 'img/fx_thuy_tien.png', fw: 160, fh: 160};
const ttienImg = new Image(); ttienImg.src = TTIEN.src;
const TTX = {src: 'img/fx_thuy_tien_x.png', fw: 160, fh: 160, px: 27.45, py: 79.2, th: [-27, -12, 0, 12, 27], tip: [122, 131, 148, 131, 122]};
const ttxImg = new Image(); ttxImg.src = TTX.src;
const TTX_ORDER = [2, 1, 3, 0, 4];                                        // thứ tự bắn: mũi giữa trước, rồi xen kẽ hai bên
/** Vẽ Vạn Thủy Tiễn: hx = mép trước tu sĩ, tx = tâm quái, cy = ngang ngực, h = chiều cao nhân vật (cùng hệ toạ độ của g2). */
function ttienDraw(g2, s, hx, tx, cy, h){
  if (s < 0 || s >= .86 || !ttienImg.complete || !ttienImg.naturalWidth) return false;
  const d = h * 1.3, heroX = hx + d * .45, sc = d / TTX.fw;
  g2.save(); g2.imageSmoothingEnabled = true; g2.imageSmoothingQuality = 'high';
  if (s < .25){                                                           // xoáy nước tụ → năm tên hiện hình quạt
    const f = s < .15 ? Math.min(5, Math.floor(s / .15 * 6)) : 6 + Math.min(1, Math.floor((s - .15) / .1 * 2));
    g2.drawImage(ttienImg, f * TTIEN.fw, 0, TTIEN.fw, TTIEN.fh, heroX - d / 2, cy - d / 2, d, d);
  } else if (ttxImg.complete && ttxImg.naturalWidth){
    const pvX = heroX - d / 2 + TTX.px * sc, pvY = cy - d / 2 + TTX.py * sc;      // tâm quạt trước mặt tu sĩ
    const lay = (col, k, x, y, ang, a = 1) => {
      g2.save(); g2.globalAlpha = a; g2.translate(x, y); g2.rotate(ang);
      g2.drawImage(ttxImg, col * TTX.fw, k * TTX.fh, TTX.fw, TTX.fh, -TTX.px * sc, -TTX.py * sc, d, d);
      g2.restore();
    };
    TTX_ORDER.forEach((k, n) => {
      const L = .3 + n * .05, FLY = .13, HIT = .18, th = TTX.th[k] * Math.PI / 180, tipLen = (TTX.tip[k] - TTX.px) * sc;
      const ay = cy + h * .1 + (k - 2) * h * .05, aim = Math.atan2(ay - pvY, tx - pvX);     // mỗi mũi nhắm một điểm trên thân quái
      const ex = tx - Math.cos(aim) * tipLen, ey = ay - Math.sin(aim) * tipLen;             // vị trí tâm quạt khi mũi tên cắm vào quái
      if (s < L){                                                         // chờ trong đội hình quạt
        const col = s < .275 ? 0 : s < .3 ? 1 : 2 + (Math.floor(s * 40) + k) % 4;
        lay(col, k, pvX, pvY, th);
      } else if (s < L + FLY){                                            // lần lượt từng mũi lao đi, quay mũi về phía quái, kéo sóng nước
        const q = (s - L) / FLY, e = q * q * (3 - 2 * q), ang = th + (aim - th) * Math.min(1, q * 3);
        lay(6 + Math.min(3, Math.floor(q * 4)), k, pvX + (ex - pvX) * e, pvY + (ey - pvY) * e, ang);
      } else if (s < L + FLY + HIT){                                      // cắm vào quái: sóng nước vỡ rồi tan
        const q = (s - L - FLY) / HIT;
        lay(10 + Math.min(6, Math.floor(q * 7)), k, ex, ey, aim, 1 - q * .4);
      }
    });
  }
  g2.restore();
  return true;
}
// ---- Thủy Nguyệt Trảm: trăng nước lưỡi liềm (img/fx_thuy_nguyet.png, 22 khung 160×160 xếp ngang) ----
// 0–5 xoáy nước tụ · 6–9 trăng lưỡi liềm ngưng hình · 10–13 trăng bay (lặp, kéo bóng mờ) · 14–21 nổ băng nước trên quái
const TNG = {src: 'img/fx_thuy_nguyet.png', fw: 160, fh: 160};
const tngImg = new Image(); tngImg.src = TNG.src;
const TNG_SEG = [[0, .15, 0, 5, 'hero'], [.15, .27, 6, 9, 'hero'], [.27, .47, 10, 13, 'fly'], [.47, .85, 14, 21, 'target']];
/** Vẽ Thủy Nguyệt Trảm: hx = mép trước tu sĩ, tx = tâm quái, cy = ngang ngực, h = chiều cao nhân vật (cùng hệ toạ độ của g2). */
function tngDraw(g2, s, hx, tx, cy, h){
  const seg = TNG_SEG.find(([a, b]) => s >= a && s < b); if (!seg || !tngImg.complete || !tngImg.naturalWidth) return false;
  const [a, b, f0, f1, at] = seg, n = f1 - f0 + 1, k = (s - a) / (b - a);
  const d = h * (at === 'target' ? 1.4 : 1.1), heroX = hx + d * .4, tgtX = tx - d * .1;
  const blit = (f, x, y, alpha = 1) => { g2.globalAlpha = alpha; g2.drawImage(tngImg, f * TNG.fw, 0, TNG.fw, TNG.fh, x - d / 2, y - d / 2, d, d); };
  g2.save(); g2.imageSmoothingEnabled = true; g2.imageSmoothingQuality = 'high';
  if (at === 'fly'){                                                // trăng lao tới, xoay vân nước, kéo 3 bóng mờ phía sau
    const e = k * k * (3 - 2 * k), f = f0 + Math.floor(k * n * 2) % n, pos = q => heroX + (tgtX - heroX) * q;
    g2.globalCompositeOperation = 'lighter';
    for (let i = 3; i >= 1; i--) if (e - i * .07 > 0) blit(f0 + (f - f0 + i) % n, pos(e - i * .07), cy, .22 / i);
    g2.globalCompositeOperation = 'source-over';
    blit(f, pos(e), cy);
  } else blit(f0 + Math.min(n - 1, Math.floor(k * n)), at === 'hero' ? heroX : tx, cy);
  g2.restore();
  return true;
}
// ---- Đòn thường hệ Thủy theo bản thiết kế (img/fx_thuy_thuong.png, 14 khung 128×128 xếp ngang) ----
// Thủy Cầu: 0–3 xoáy tụ cầu · 4–7 cầu nước xoay khi bay · 8–13 vỡ băng nước
// nhịp đòn thường: tụ .00–.30 · bay .30–.45 · trúng .45
const TBASIC = {src: 'img/fx_thuy_thuong.png', fw: 128, fh: 128};
const tbImg = new Image(); tbImg.src = TBASIC.src;
/** Vẽ đòn thường hệ Thủy: kind = water_ball · (hx, hy) tay tu sĩ · (tx, ty) tâm quái · h chiều cao nhân vật · t giây (cùng hệ toạ độ của g2). */
function tbDraw(g2, kind, s, hx, hy, tx, ty, h, t){
  if (s < 0 || !tbImg.complete || !tbImg.naturalWidth) return false;
  const blit = (f, x, y, d, a = 1, dx = 0) => { g2.globalAlpha = a; g2.drawImage(tbImg, f * TBASIC.fw, 0, TBASIC.fw, TBASIC.fh, x - d / 2 - dx, y - d / 2, d, d); };
  const lerp = (a, b, k) => a + (b - a) * k;
  g2.save(); g2.imageSmoothingEnabled = true; g2.imageSmoothingQuality = 'high';
  if (kind === 'water_ball'){                                              // Thủy Cầu: xoáy tụ cầu → cầu nước xoay bay thẳng → vỡ băng nước
    const d = h * 1.3, sx = hx + d * .2;
    if (s < .3) blit(Math.min(3, Math.floor(s / .3 * 4)), sx, hy, d);
    else if (s < .45){
      const k = (s - .3) / .15, f = 4 + Math.floor(t * 16) % 4, x = lerp(sx, tx, k), y = lerp(hy, ty, k);
      g2.globalCompositeOperation = 'lighter';
      for (let i = 3; i >= 1; i--) if (k - i * .12 > 0) blit(f, lerp(sx, tx, k - i * .12), lerp(hy, ty, k - i * .12), d * (1 - i * .12), .25 / i);
      g2.globalCompositeOperation = 'source-over';
      blit(f, x, y, d);
    } else if (s < .72) blit(8 + Math.min(5, Math.floor((s - .45) / .27 * 6)), tx, ty, h * 1.15);
  }
  g2.restore();
  return true;
}
/** Bộ lọc màu đổi hiệu ứng lửa (fx_hoa_*) sang lam cho chiêu hệ Thủy. */
const WET_FILTER = 'hue-rotate(185deg) saturate(1.15)';
function skPassive(s, lv){
  const fire = s.element === 'hoa';
  const P = [['atkPct','Tấn công'],['defPct', fire ? 'Phòng thủ (kháng Hỏa)' : 'Phòng thủ'],['hpPct','Máu'],['crit', fire ? 'Chí mạng Hỏa' : 'Chí mạng'],['speed','Thân pháp'],['qiPct','Tốc độ tu luyện'],['monAtkDown','Công của địch −'],['skillPct', fire ? 'Sát thương Hỏa' : 'Sát thương chiêu thức'],['pen', fire ? 'Xuyên thấu Hỏa' : 'Xuyên thấu']];
  const r = skRate(s.element);                                    // hệ không kích hoạt: bị động chỉ còn 50%
  return P.filter(([k]) => s[k]).map(([k, n]) => [n, s[k] * skFactor(s, lv) * (k === 'monAtkDown' && s.element !== skActive() ? 0 : r), s[k] * skFactor(s, Math.min(s.maxLv, lv + 1)) * (k === 'monAtkDown' && s.element !== skActive() ? 0 : r)]);
}

function renderSkill(){
  const pane = $('pane-skill'); if (!pane || !S) return;
  // Hai ô chiến đấu là giới hạn cache: nạp sớm để lần tung chiêu đầu không giật.
  if (typeof fireFxAvailable === 'function'){
    fireFxAvailable(skBasicId(),S.realm);
    if (S.skillSlots?.[1]) fireFxAvailable(S.skillSlots[1],S.realm);
  }
  const el = elOf(skEl) || elOf(DATA.elements[0].id), list = skOfEl(el.id);
  // giữ vị trí cuộn của cây khi vẽ lại (đồng bộ, nâng cấp…); đổi sang hệ khác thì về đầu cây
  const SCROLLERS = '.sk-path-scroll', host = pane.closest('aside.book'), hostTop = host?.scrollTop;
  const keep = skScrollEl === el.id ? [pane.scrollTop, [...pane.querySelectorAll(SCROLLERS)].map(x => [x.scrollTop, x.scrollLeft])] : null;
  skScrollEl = el.id;
  if (!skById(skSel) || skById(skSel).element !== el.id) skSel = (list.find(s => skTree(s) === 'active') || list[0]).id;
  // đòn thường dùng chung mọi hệ (Hỏa Cầu): hệ chưa có đòn thường riêng thì hiện đòn thường chung
  const act = S.skillSlots?.[1] && skById(S.skillSlots[1]);
  const runEl = elOf(skActive()), runBasic = skById(skBasicId()), isRun = el.id === runEl?.id;
  // ô đòn thường trên thanh: hiện đòn thường của hệ đang xem (hệ chưa có đòn thường riêng thì hiện đòn đang dùng)
  const tabBasic = list.find(s => skTree(s) === 'basic') || runBasic;
  const WX = {hoa: '&#10;Ngũ hành: Hỏa khắc Mộc +15% sát thương · Hỏa bị Thủy khắc −15%'};
  pane.innerHTML = `<div class="sk2" style="--ec:${el.color};--ec2:${el.color2}">
    <nav class="sk-els" role="tablist" aria-label="Hệ tu luyện">${DATA.elements.map(e => elOf(e.id)).map(e => `<button role="tab" class="${e.id === el.id ? 'on' : ''} ${e.open ? '' : 'lock'} ${e.id === runEl?.id ? 'run' : ''}" aria-selected="${e.id === el.id}" data-el="${e.id}" style="--c:${e.color}" data-tip="${e.name} hệ&#10;${e.desc}${e.open ? (WX[e.id] || '') : '&#10;Chưa mở khóa'}">
      <img class="px" src="img/el_${e.id}.png" alt=""><span>${e.name.toUpperCase()}</span>${e.open ? (e.id === runEl?.id ? '<small class="run">Kích hoạt</small>' : '') : '<small>Chưa mở khóa</small>'}</button>`).join('')}</nav>
    <section class="sk2-bar">
      <div class="sk2-pts" data-tip="Điểm kỹ năng&#10;Chỉ dùng cho Chủ động và Bị động (đánh thường không tốn điểm)&#10;Đã nhận ${S.skillPointsTotal} · đã dùng ${S.skillPointsTotal - S.skillPoints}"><span>Điểm</span><b>${S.skillPoints}</b></div>
      <div class="sk2-grp sk2-run"><span>Kích hoạt</span>${tabBasic ? `<button class="sk2-ico ${tabBasic.id === runBasic?.id ? 'on' : ''}" data-skcard="${tabBasic.id}" data-tip="${tabBasic.name}"><img style="--sk-icon-filter:${skIconStyle(tabBasic)}" src="${skIcon(tabBasic)}" alt="${tabBasic.name}"></button>` : ''}
        ${el.open && !isRun ? `<button class="sk2-activate" data-activate="${el.id}" style="--c:${el.color}" data-tip="Kích hoạt công pháp hệ ${el.name}&#10;Đòn thường đổi sang hệ ${el.name}, bị động hệ ${el.name} phát huy 100%&#10;Bị động các hệ khác (kể cả ${runEl?.name || ''}) chỉ còn ${SK_INACTIVE * 100}%&#10;Đổi tự do, không tốn gì">Kích hoạt ${el.name}</button>` : `<b class="sk2-runname" style="color:${runEl?.color || '#f8b04a'}" data-tip="Công pháp đang kích hoạt&#10;Đòn thường: ${runBasic?.name || ''}&#10;Bị động hệ ${runEl?.name || ''} phát huy 100% · hệ khác ${SK_INACTIVE * 100}%">${runEl?.name || ''} hệ</b>`}</div>
      <div class="sk2-grp"><span>Kỹ năng</span><button class="sk2-ico slot ${act ? 'on' : ''}" data-slot="1" ${act ? 'draggable="true" data-slotsk="' + act.id + '"' : ''} ${act ? `data-sk="${act.id}" data-skcard="${act.id}" data-tip="${act.name}"` : 'data-tip="Ô kỹ năng trống&#10;Nhấp đúp hoặc kéo một kỹ năng chủ động đã lĩnh ngộ vào đây"'}>${act ? `<img style="--sk-icon-filter:${skIconStyle(act)}" src="${skIcon(act)}" alt="">` : '<span class="emp">+</span>'}</button></div>
    </section>
  </div>`;
  // Một cây chung: các nhánh giữ liên kết khi đi qua ranh giới cảnh giới.
  const root = pane.querySelector('.sk2');
  root.classList.add('sk-realms');
  const learnable = list.filter(s => skTree(s) !== 'basic' && skState(s).st === 'avail').length;
  root.insertAdjacentHTML('beforeend', `<section class="sk-path-section"><header class="sk-path-heading"><h3>Đạo lộ ${el.name}</h3><span>${el.open ? `${isRun ? 'Đang kích hoạt' : `Hệ phụ · bị động còn ${SK_INACTIVE * 100}%`} · ${learnable} có thể nâng · Rê chuột xem chi tiết · Nhấp đúp để trang bị` : 'Dùng bí kíp trong hành trang để mở cây kỹ năng'}</span></header><div class="sk-path-scroll" aria-label="Cây công pháp theo cảnh giới">${skRealmTree(el)}</div></section>`);
  if (host) host.scrollTop = hostTop;
  if (keep){ pane.scrollTop = keep[0]; pane.querySelectorAll(SCROLLERS).forEach((x, i) => { const k = keep[1][i]; if (k){ x.scrollTop = k[0]; x.scrollLeft = k[1]; } }); }
  skFade = 0;
  liveSkill();
}

/** Bố trí bằng MinRealm; không phụ thuộc số hàng cố định hay tọa độ cây cũ. */
function skRealmTree(el){
  const list = el.open ? skOfEl(el.id).filter(s => skTree(s) !== 'basic') : [];
  const pos = new Map(), height = 100, rowHeight = 92 / DATA.realms.length;
  const bands = DATA.realms.map((realm, i) => {
    const top = 6 + i * rowHeight, tier = list.filter(s => s.minRealm === i);
    const active = tier.filter(s => skTree(s) === 'active'), passive = tier.filter(s => skTree(s) === 'passive');
    const h = rowHeight;
    for (const [nodes, x, width] of [[active, 32, 28], [passive, 75, 47]]) nodes.forEach((s, n) => {
      pos.set(s.id, {x: x + (n - (nodes.length - 1) / 2) * Math.min(16, width / nodes.length), y: top + h / 2});
    });
    return `<div class="sk-realm-band ${i > S.realm ? 'future' : ''} ${i === S.realm ? 'current' : ''}" data-realm-band="${i}" style="top:${top}%;height:${h}%"><div class="sk-realm-label"><b>${realm.name}</b><span>${tier.length ? `${tier.filter(s => skLv(s.id) > 0).length}/${tier.length} đã học` : 'Chưa có bí kíp'}</span></div></div>`;
  }).join('');
  let lines = '';
  for (const s of list) for (const req of s.req){
    const p = pos.get(req.split(':')[0]), c = pos.get(s.id); if (!p || !c) continue;
    const state = skState(s), y = c.y - rowHeight / 2 + 1;
    lines += `<path class="${state.learned ? 'on' : state.reqOk ? 'next' : 'off'}" d="M${p.x} ${p.y + 3} V${y} H${c.x} V${c.y - 3}"/>`;
  }
  const nodes = list.map(s => {
    const st = skState(s), p = pos.get(s.id), tree = skTree(s);
    return `<div class="sk-node-wrap" style="left:${p.x}%;top:${p.y}%"><button class="sk-n ${tree === 'active' ? 'act' : 'pas'} ${st.st} ${s.id === skSel ? 'sel' : ''} ${S.skillSlots?.[1] === s.id ? 'eq' : ''}" data-sk="${s.id}" data-skcard="${s.id}" data-tip="${s.name}" aria-label="${s.name}, cấp ${st.lv}/${s.maxLv}, ${realmName(s.minRealm)}${st.why.length ? ', ' + st.why.join(', ') : ''}" aria-pressed="${s.id === skSel}" ${tree === 'active' && st.lv > 0 ? 'draggable="true"' : ''}><span class="sk-ic"><img style="--sk-icon-filter:${skIconStyle(s)}" src="${skIcon(s)}" alt="">${st.max ? '<em>MAX</em>' : st.st === 'locked' ? '<i class="lk">▣</i>' : ''}</span><span class="sk-nm">${s.name}</span>${S.skillSlots?.[1] === s.id ? '<span class="sk-lvt">Đang dùng</span>' : ''}</button>${st.max ? '' : `<button class="sk-node-up" data-up="${s.id}" ${st.why.length ? 'disabled' : ''} aria-label="${st.lv ? 'Nâng cấp' : 'Lĩnh ngộ'} ${s.name}, ${st.cost} điểm" data-tip="${st.why.length ? st.why.join('&#10;') : `${st.lv ? 'Nâng cấp' : 'Lĩnh ngộ'} ${s.name} · ${st.cost} điểm`}">+</button>`}</div>`;
  }).join('');
  return `<div class="sk-realm-board" style="--sk-rows:${DATA.realms.length}"><div class="sk-lane-head"><span>Cảnh giới</span><b>Chủ động</b><b>Bị động</b></div>${bands}<svg class="sk-lines" viewBox="0 0 100 ${height}" preserveAspectRatio="none" aria-hidden="true">${lines}</svg>${nodes}</div>`;
}

/** Vẽ một cây (chủ động / bị động) trên lưới rows × cols từ Row/Col của dữ liệu; chỉ nối các tiền đề cùng cây. */
function skTreeHtml(el, tree, rows, cols){
  const list = el.open ? skOfEl(el.id).filter(s => skTree(s) === tree) : [];
  const X = c => (c + .5) / cols * 100, Y = r => (r + .5) / rows * 100;
  let lines = '';
  for (const s of list) for (const r of s.req){
    const p = skById(r.split(':')[0]); if (!p || skTree(p) !== tree) continue;
    const ps = skState(p), cs = skState(s);
    const cls = cs.learned ? 'on' : ps.learned && cs.reqOk ? 'next' : 'off';
    const x1 = X(p.col), y1 = Y(p.row), x2 = X(s.col), y2 = Y(s.row), ym = (y1 + y2) / 2;
    lines += `<path class="${cls}" d="M${x1} ${y1} V${ym} H${x2} V${y2}" />`;
  }
  const nodes = list.map(s => {
    const st = skState(s), sel = s.id === skSel, cross = s.req.filter(r => skTree(skById(r.split(':')[0])) !== tree);
    const tip = `${s.name} · ${SK_TYPE[tree]}&#10;Lv. ${st.lv} / ${s.maxLv}&#10;${st.reqs.map(r => (r.ok ? '✓ ' : '✗ ') + r.t).join('&#10;')}`;
    return `<button class="sk-n ${tree === 'active' ? 'act' : 'pas'} ${st.st} ${sel ? 'sel' : ''} ${S.skillSlots?.[1] === s.id ? 'eq' : ''}" data-sk="${s.id}" data-skcard="${s.id}" ${tree === 'active' && st.lv > 0 ? 'draggable="true"' : ''} style="left:${X(s.col)}%;top:${Y(s.row)}%" data-tip="${tip}">
      <span class="sk-ic"><img style="--sk-icon-filter:${skIconStyle(s)}" src="${skIcon(s)}" alt="">${st.st === 'locked' ? '<i class="lk">🔒</i>' : ''}${st.max ? '<em>MAX</em>' : st.st === 'avail' ? '<em class="pl">+</em>' : ''}</span>
      <span class="sk-nm">${s.name}</span>${cross.length ? `<span class="sk-x" title="Cần thêm ở cây kia">⇄</span>` : ''}</button>`;
  }).join('');
  return `<div class="sk-grid ${tree}"><svg class="sk-lines" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">${lines}</svg>${nodes}</div>`;
}

/** Thẻ tooltip của kỹ năng: toàn bộ thông tin (không có animation xem trước, không có nút). */
function skTipHtml(s){
  if (!s) return '';
  const el = elOf(s.element), tree = skTree(s), st = skState(s), lv = st.lv, next = Math.min(s.maxLv, lv + 1);
  const dmg = l => s.dmgBase ? s.dmgBase + s.dmgPerLv * Math.max(0, l - 1) : 0;
  const pass = skPassive(s, lv);
  const isBasic = tree === 'basic';
  return `<div class="sk-card sk-tipcard">
      <div class="sk-hd"><img style="--sk-icon-filter:${el.open ? skIconStyle(s) : 'none'}" src="${el.open ? skIcon(s) : `img/el_${el.id}.png`}" alt=""><div><p class="nm">${s.name}</p><p class="sub"><span style="color:${SK_CAT[s.category]}">${SK_TYPE[tree]}${s.category === 'Tuyệt kỹ' ? ' · Tuyệt kỹ' : ''}</span> · ${el.name} hệ</p></div></div>
      <dl class="sk-dl">
        <dt>Loại</dt><dd>${SK_TYPE[tree]}${s.category === 'Tuyệt kỹ' ? ' (tối thượng)' : ''}</dd>
        <dt>Hệ</dt><dd>${el.name}</dd>
        ${isBasic ? '' : `<dt>Cấp</dt><dd>${lv} / ${s.maxLv}</dd><dt>Cảnh giới</dt><dd class="${S.realm < s.minRealm ? 'bad' : ''}">${realmName(s.minRealm)}</dd>`}
        ${isBasic ? `<dt>Thi triển</dt><dd>Mỗi lượt</dd>` : ''}
        ${s.dmgBase ? `<dt>Sát thương</dt><dd>${isBasic ? s.dmgBase : dmg(Math.max(1, lv))}% tấn công</dd>` : ''}
        ${isBasic && s.crit ? `<dt>Chí mạng</dt><dd>+${s.crit}%</dd>` : ''}
      </dl>
      <p class="sk-eff">“${s.effect}”</p>
      ${s.element !== skActive() && (s.burnPct > 0 || s.monAtkDown > 0) ? '<p class="sk-act">Hiệu ứng xấu bị động đã vô hiệu: cần kích hoạt công pháp cùng hệ.</p>' : ''}
      ${s.id === 'hoa_cau' ? `<p class="sk-note">Biến thể theo cảnh giới: <b>${HC_TIER_NAME[Math.min(2, S.realm)]}</b></p>` : ''}
      ${pass.length ? `<p class="sk-sec">Hiệu quả bị động${skRate(s.element) < 1 ? ` <i class="sk-half">(×${SK_INACTIVE * 100}% · hệ không kích hoạt)</i>` : ''}</p><ul class="sk-pass">${pass.map(([n, v, nx]) => `<li>${n} <b>+${pct1(v)}</b>${!st.max ? `<i>→ +${pct1(nx)}</i>` : ''}</li>`).join('')}</ul>` : ''}
      ${!isBasic && lv > 0 && tree === 'active' ? `<p class="sk-act">Trong trận: mỗi lần thi triển ${dmg(lv)}% tấn công${!st.max && dmg(next) !== dmg(lv) ? ` (Lv. ${next}: ${dmg(next)}%)` : ''}.</p>` : ''}
      ${s.secondaryEffect ? `<p class="sk-act">Hiệu ứng phụ: <b style="color:${el.color}">${skSecondaryChance(s,Math.max(1,lv))}% ${s.secondaryEffect.name}</b>${!st.max && lv>0 ? ` → ${skSecondaryChance(s,next)}% ở Lv. ${next}` : ''}. ${s.secondaryEffect.description}${s.secondaryEffect.target === 'monster' && s.element !== skActive() ? ' <b>Đã vô hiệu: cần kích hoạt công pháp cùng hệ.</b>' : ''}</p>` : ''}
      ${!isBasic && !st.max ? `<p class="sk-sec">${lv ? `Nâng cấp Lv. ${lv} → ${next}` : 'Yêu cầu lĩnh ngộ'}</p>
        <ul class="sk-reqs">${st.reqs.map(r => `<li class="${r.ok ? 'ok' : 'no'}">${r.ok ? '✓' : '✗'} ${r.t}</li>`).join('')}</ul>` : ''}
      ${isBasic ? `<p class="sk-note">${skBasicId() === s.id ? 'Đang dùng làm đòn đánh thường.' : `Kích hoạt công pháp hệ ${el.name} để dùng làm đòn đánh thường.`}</p>` : ''}
      ${tree === 'active' && lv > 0 ? `<p class="sk-note">${S.skillSlots?.[1] === s.id ? 'Đang dùng trong trận · nhấp đúp, chuột phải hoặc kéo ra ngoài để tháo.' : 'Nhấp đúp, chuột phải hoặc kéo vào ô Kỹ năng để dùng trong trận.'}</p>` : ''}
    </div>`;
}
function liveSkill(){
  const q = document.querySelector('#pane-skill .skq'); if (q && S) setQiDisplay(q, qiNow(), S.qiReq);
}

// ---- Hỏa hệ: animation 8 khung cắt từ bảng thiết kế (img/fx_hoa_skills.png, mỗi khung 124×64; sinh lại: tools/cut_skills.py) ----
const SK_FX = {src: 'img/fx_hoa_skills.png', fw: 124, fh: 64, bg: '#0c1019', dur: [.3, .25, .25, .25, .35, .3, .3, .3], rest: .6};
const SK_FX_ROW = {liet_diem: 2, hoa_long: 3, phan_thien: 4, xich_diem_thien: 5, than_hoa: 4, hu_khong_hoa: 5, hoa_thuan: 6, hoa_giap: 7};
/** Phát 8 khung của chiêu theo nhịp thiết kế, chuyển khung mờ dần cho mượt. */
function skFlip(g, now, s){
  const img = skImg(SK_FX.src), row = SK_FX_ROW[s.id], D = SK_FX.dur, total = D.reduce((a, b) => a + b, 0) + SK_FX.rest;
  g.fillStyle = SK_FX.bg; g.fillRect(0, 0, 120, 60);
  if (!img) return;
  let e = (now / 1000) % total, i = 0;
  while (i < 7 && e >= D[i]){ e -= D[i]; i++; }
  const rest = i === 7 && e >= D[7], k = rest ? 1 : e / D[i];
  const w = 60 * SK_FX.fw / SK_FX.fh, x = (120 - w) / 2;
  const draw = (f, a) => { g.globalAlpha = a; g.drawImage(img, f * SK_FX.fw, row * SK_FX.fh, SK_FX.fw, SK_FX.fh, x, 0, w, 60); };
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  if (rest){ draw(7, Math.max(0, 1 - (e - D[7]) / SK_FX.rest)); }
  else { draw(i, 1); if (i < 7 && k > .7) draw(i + 1, (k - .7) / .3); }
  g.globalAlpha = 1; g.imageSmoothingEnabled = false;
  drawSkinActor(g,skinActorAction(s),rest ? 0 : Math.min(1,(i+k)/7),26,55,46);
}
/** Kỹ năng bị động: không có animation chiêu, chỉ icon phát sáng và tàn lửa bay lên. */
function skPassiveFx(g, now, s){
  const t = now / 1000, img = skImg(skIcon(s));
  g.fillStyle = SK_FX.bg; g.fillRect(0, 0, 120, 60);
  const pulse = .5 + .5 * Math.sin(t * 2.4);
  const gr = g.createRadialGradient(60, 30, 4, 60, 30, 30);
  const wet = s.element === 'thuy';
  gr.addColorStop(0, wet ? `rgba(70,160,255,${.35 + .25 * pulse})` : `rgba(255,140,40,${.35 + .25 * pulse})`); gr.addColorStop(1, wet ? 'rgba(40,110,230,0)' : 'rgba(255,90,30,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 120, 60);
  for (let i = 0; i < 14; i++){
    const h = Math.sin(i * 91.7) * 1e4 % 1, p = (t * .35 + Math.abs(h)) % 1;
    g.globalAlpha = 1 - p; g.fillStyle = i % 3 ? '#f08a2a' : '#f8d25a';
    g.fillRect(60 + Math.sin(i * 2.3) * 26 + Math.sin(t * 2 + i) * 2, 56 - p * 50, .75, .75);
  }
  g.globalAlpha = 1;
  if (img){ g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high'; const d = 40 + 2 * pulse; g.save(); g.filter = skIconStyle(s); g.drawImage(img, 60 - d / 2, 30 - d / 2, d, d); g.restore(); g.imageSmoothingEnabled = false; }
}
/** Xem trước Hỏa Cầu: nhân vật + hiệu ứng cắt nguyên từ bản thiết kế (img/hoa_cau_ref.png), đúng nhịp 8 pha + nghỉ.
 *  Canvas xem trước chạy ở độ phân giải ×HC_RES để giữ nét ảnh gốc; toạ độ vẫn theo lưới logic 120×60. */
const HC_RES = 4;
// khung (168×158): 0 chuẩn bị · 1 tụ hỏa · 2 hình thành · 3 phóng · 4 cầu bay · 5 nổ · 6 tàn lửa · 7 tư thế phóng không có cầu
// feet: điểm giữa bàn chân · fire: tâm lửa/cầu/nổ (px trong khung). Sinh lại: python tools/cut_ref.py tools/hoa_cau_design.png wwwroot/img
const HC_REF = {src: 'img/hoa_cau_ref.png', fw: 168, fh: 158,
  feet: [[76, 153], [70, 150], [38, 148], [33, 146], null, null, null, [33, 146]],
  fire: [null, [111, 80], [126, 82], [144, 76], [143, 75], [81, 94], [83, 83], null],
  scale: [1, 1.107, 1.069, 1.204, 1, 1, 1, 1.204]};             // các thẻ trong bản thiết kế vẽ nhân vật to nhỏ khác nhau → chuẩn hoá chiều cao
const HC_FX_SCALE = [.8, 1, 1.25];                               // cỡ cầu / vụ nổ theo biến thể
function hcRef(g, img, i, a, px, py, k, alpha = 1){              // vẽ khung i sao cho mốc a của khung nằm tại (px, py)
  const {fw, fh} = HC_REF;
  g.globalAlpha = Math.max(0, Math.min(1, alpha));
  g.drawImage(img, i * fw, 0, fw, fh, px - a[0] * k, py - a[1] * k, fw * k, fh * k);
  g.globalAlpha = 1;
}
function hcPreview(g, now, hero, mon, P){
  const ref = skImg(skinArt('heroCast') || HC_REF.src), wolf = skImg('img/quai_yeu_lang.png') || mon, tier = Math.min(2, S.realm), t = now / 1000;   // đòn thường: biến thể theo cảnh giới
  
  const total = HC.phases.reduce((a, p) => a + p[1], 0) + HC.rest, e = t % total, ph = hcAt(e);
  const name = ph ? ph.name : 'rest', k = ph ? ph.k : 0, ease = x => x * x * (3 - 2 * x);
  const K = .38, fxK = K * HC_FX_SCALE[tier], footX = 22, ground = 52.5;
  const W = wolf === mon ? [48, 48, 72, 6] : [50, 50 * wolf.naturalHeight / wolf.naturalWidth];   // [rộng, cao, x, y] của quái
  if (W.length === 2) W.push(68, ground - W[1]);
  const tx = W[2] + W[0] * .4, ty = W[3] + W[1] * .5;
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  const shake = name === 'hit' && k < .6 ? (tier + 1) * .35 * (Math.floor(t * 30) % 2 ? 1 : -1) : 0;
  g.save(); g.translate(shake, 0);

  // quái: chớp sáng + bị đẩy lùi khi trúng
  const mdx = name === 'hit' ? 2.5 * Math.min(1, k * 3) : name === 'fade' ? 2.5 * (1 - k) : 0;
  if (wolf){
    g.drawImage(wolf, W[2] + mdx, W[3], W[0], W[1]);
    if (name === 'hit' && k < .5){ g.globalAlpha = .7 - k; g.drawImage(hcFlash(wolf), W[2] + mdx, W[3], W[0], W[1]); g.globalAlpha = 1; }
  }
  if (!ref){ g.restore(); return; }
  const fireAt = i => { const q = K * HC_REF.scale[i]; return [footX + (HC_REF.fire[i][0] - HC_REF.feet[i][0]) * q, ground + (HC_REF.fire[i][1] - HC_REF.feet[i][1]) * q]; };

  // tu sĩ (lửa trên tay nằm sẵn trong khung tư thế)
  const pose = {prep: 0, ignite: 1, form: 2, launch: 3, fly: k < .6 ? 7 : 0}[name] ?? 0;
  if (!drawSkinActor(g,'cast',({prep:0,ignite:.34,form:.5,launch:.8,fly:.99}[name] ?? 0),footX,ground,57))
    hcRef(g, ref, pose, HC_REF.feet[pose], footX, ground, K * HC_REF.scale[pose]);
  if (name === 'prep'){                                          // linh khí tụ về lòng bàn tay
    const hx = footX + 14, hy = ground - 21;
    for (let i = 0; i < 8; i++){ const a = i / 8 * Math.PI * 2 + t * 4, r = 14 * (1 - k) + 2; P(hx + Math.cos(a) * r, hy + Math.sin(a) * r * .8, i % 2 ? '#6fe0e0' : '#f8d25a', .35 + .5 * k); }
  }
  if (name === 'fly'){                                           // quả cầu rời tay bay tới quái
    const [x0, y0] = fireAt(3), kk = ease(k) * .3 + k * .7;
    const x = x0 + (tx - 4 - x0) * kk, y = y0 + (ty - y0) * kk + Math.sin(t * 25) * .3;
    hcRef(g, ref, 4, HC_REF.fire[4], x, y, fxK);
  } else if (name === 'hit'){                                    // va chạm: vụ nổ bung ra
    hcRef(g, ref, 5, HC_REF.fire[5], tx, ty, fxK * (.45 + .55 * ease(Math.min(1, k * 1.4))) * .85);
  } else if (name === 'fade'){                                   // tan biến: nổ mờ dần, tàn lửa hiện ra
    hcRef(g, ref, 5, HC_REF.fire[5], tx, ty, fxK * .85 * (1 + .08 * k), 1 - k);
    hcRef(g, ref, 6, HC_REF.fire[6], tx, ty, fxK, k);
  } else if (name === 'end'){                                    // kết thúc: tàn lửa rơi và tắt
    hcRef(g, ref, 6, HC_REF.fire[6], tx, ty + 3 * k, fxK, 1 - k);
  }
  g.restore(); g.globalAlpha = 1; g.imageSmoothingEnabled = false;
}
