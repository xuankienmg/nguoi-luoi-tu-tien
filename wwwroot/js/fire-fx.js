/* Shared manual VFX timeline for preview and combat; sheets contain effects only. */
const fireFxCache = new Map();
const woodRootCache = new Map();
const WOOD_ROOT_CAGE = new Set(['lam_khoa','hu_khong_kien_moc','thai_so_moc',
  'moc_do_kiep_active','moc_chan_tien_active','moc_kim_tien_active']);
// Hóa Thần / Luyện Hư: 36 authored frames, two seconds in the skill preview.
// Combat maps the full sequence to its existing hero-turn timeline.
const HOA_THAN_FX = {
  huyen_bang: ['bang', 'phoenix-ice-spritesheet-v2.png', 'projectile', [0,226,447,674,888,1090,1254]],
  cuu_tieu: ['loi', 'lightning-burst-spritesheet-v1.png', 'target'],
  thanh_de: ['moc', 'thanh-lien-phuoc-sat.png', 'target'],
  cuu_thien_phong: ['gio', 'phong-nhan-loan-vu.png', 'projectile'],
};
const LUYEN_HU_FX = {
  hu_khong_kien_moc: ['moc', 'hu-khong-kien-moc.png', 'target'],
  hu_khong_kiep_loi: ['loi', 'hu-khong-kiep-loi.png', 'target'],
  hu_khong_bang_nguc: ['bang', 'hu-khong-bang-nguc.png', 'target'],
  hu_khong_phong_nhan: ['gio', 'hu-khong-phong-nhan.png', 'target'],
};
// Bộ chiêu Hỏa (chốt 06/11/2026, nguồn outputs/skill-hoa): 90 khung · 30 FPS · 3 giây, dùng ở mọi cảnh giới.
// Sheet runtime đã cắt vùng trống + thu nhỏ (sheet.json cùng thư mục). [thư mục, khung W, H, neo X, Y, trúng ms,
// span = số px trên sheet ứng với khoảng tay tu sĩ → mục tiêu, neo vào 'target' (giữa quái) hay 'ground' (chân quái)]
const FIRE_SET_FX = {
  liet_diem:       ['xich-diem-tram-v1',         347, 94, 282.8, 51.8,  950, 247.5, 'target'],
  phan_thien:      ['song-diem-tram-v3',         362,107, 304.5, 51,    950, 247.5, 'target'],
  xich_diem_thien: ['thien-hoa-giang-thach-v10', 355,197, 192,   168,  1600, 198,   'ground'],
  hoa_long:        ['viem-toan-v2',              400,194, 286,   187,  1500, 214.5, 'ground'],
  than_hoa:        ['viem-kiem-giang-thien-v4',  349,254, 236.4, 191.2,1650, 174.8, 'ground'],
  hu_khong_hoa:    ['hoa-phuong-xung-v2',        394,179, 292.5, 95.9, 1450, 157.5, 'target'],
  hon_nguyen_hoa:  ['cu-kiem-thien-giang-v2',    364,336, 186.9, 273,  1400, 189,   'ground'],
  // Đại Thừa: Thiên Chỉ hệ Hỏa (output/thien-chi-hoa-demo-v1, đổi màu từ v5) — bàn tay đỏ cam nhất chỉ lao chéo rồi nổ vòm; 60 khung · 2 giây, trúng khung 32; phần tử thứ 9 = số khung (mặc định 90),
  // thứ 10 = fitTop: số px đầu sheet được phép tràn khỏi mép trên. 11/2026: bộ Hỏa cao cấp để 0 — vòng ấn/trận pháp nằm sát mép trên sheet, tràn ra là mất cả đoạn hiệu ứng.
  thai_so_hoa:     ['thien-chi-hoa-v1',          303,216, 173.52, 184.32, 1034, 108,   'ground', 60, 0],
  // Độ Kiếp (Dịch Linh Ấn): Dịch Linh Ấn Hỏa + nổ vòm (output/dich-linh-an-hoa-vom-no-demo-v1), trúng khung 30;
  // anchorY thấp hơn Thiên Chỉ → vẫn hiển thị to hơn Đại Thừa (phạm vi to dần theo cấp chiêu) mà không cắt vòng ấn.
  hoa_do_kiep_active: ['dich-linh-an-hoa-v2',    384,346, 192,   294,    967, 120,   'ground', 60, 0],
  // Chân Tiên (Ngũ Chưởng Dịch Linh Ấn): 5 bàn tay lửa chưởng xuống khung 24/28/32/36/40 (output/dich-linh-an-ngu-chuong-hoa-demo-v1);
  // sát thương hiện ở chưởng đầu; vùng rộng nhất bộ Hỏa (phạm vi to dần theo cấp chiêu).
  hoa_chan_tien_active: ['ngu-chuong-hoa-v1',    341,284, 170.5, 246.5,  767, 110,   'ground', 60, 0],
};
// Kim Tiên (11/2026): Thần Binh Trảm theo hệ (output/kim-tien-than-binh-tram-v1, gốc thien-than-hu-anh-demo-v6) —
// hư ảnh vàng kim nâng cự kiếm chém xuống quái, mỗi hệ đổi màu, trận pháp và va chạm riêng. 90 khung · 3 giây, trúng khung 57.
// Sheet 240×160 phủ cả tu sĩ lẫn quái. span 96 (< 116,25 khoảng tay → quái trên sheet): phóng to hư ảnh để vùng hiển thị
// lớn hơn Chân Tiên (phạm vi to dần theo cấp chiêu); hư ảnh đứng lùi sau tu sĩ một chút, mũi kiếm vẫn neo đúng quái.
// Hai lớp: back (trận pháp + hư ảnh, vẽ SAU tu sĩ) · front (vệt chém + va chạm). Mỗi lớp cắt theo vùng có hình:
// [back x, y, w, h, front x, y, w, h] trong ô 240×160 (in ra từ build.py --pack).
const KIM_TIEN_LAYERS = {
  hoa:[0,0,212,160, 102,8,138,152],
  moc:[0,9,211,151, 103,8,137,152],
  tho:[0,9,211,151, 99,8,141,152],
};
const KIM_TIEN_SET_FX = {};
for (const el of Object.keys(KIM_TIEN_LAYERS))
  KIM_TIEN_SET_FX[`${el}_kim_tien_active`] = [el, 240,160, 179.1667,133.3333, 1900, 96, 'ground', 90, 0, 60];
// Mộc v5 (11/2026, output/moc-skill-set-demo-v5, đóng gói bằng tools/moc_fx/pack_v5.py): [khung trúng, biên ảnh nhìn thấy trong ô 240 × 160].
// Cả 10 chiêu dùng sheet Mộc riêng (kể cả Hợp Thể → Chân Tiên), vụ nổ theo bậc cảnh giới đã ghép sẵn trong sheet.
const MOC_SET_FX = {
  dang_man:             [19, [98,41,191,151]],
  bach_diep:            [19, [43,78,193,140]],
  lam_khoa:             [32, [82,47,206,158]],
  co_moc:               [30, [21,30,205,160]],
  thanh_de:             [33, [2,22,202,155]],
  hu_khong_kien_moc:    [29, [55,8,233,146]],
  hon_nguyen_moc:       [28, [66,11,223,160]],
  thai_so_moc:          [31, [23,0,240,160]],
  moc_do_kiep_active:   [29, [52,0,236,159]],
  moc_chan_tien_active: [23, [0,0,237,159]],
};
const GROUND_TARGET_VERSION = {moc:'v5', tho:'v6', thuy:'v2', loi:'v1', bang:'v1', gio:'v1'};
const GROUND_TARGET_DIR = {moc:'moc', tho:'tho', thuy:'water', loi:'loi', bang:'bang', gio:'gio'};
// Thổ v6 (11/2026, output/tho-skill-set-demo-v6, đóng gói bằng tools/tho_fx/pack_v6.py): hình ảnh các chiêu Thổ cũ hạ một bậc cảnh giới,
// ID/mốc trúng giữ theo bậc. Phần tử thứ 3 (nếu có) = ô riêng [rộng, cao, neo X, neo Y, unitScale]: Độ Kiếp và Chân Tiên là model Hỏa
// (Dịch Linh Ấn / Ngũ Chưởng) đổi màu, bàn tay đá, trận pháp Thổ, thu 0,5 nên ô khác 240 × 160; unitScale = cỡ hiển thị so với ô chuẩn.
const THO_SET_FX = {
  thach_dan:            [19, [31,76,185,146]],
  tho_tuong:            [19, [42,71,196,146]],
  tho_giap:             [32, [76,0,213,149]],
  son_nhac:             [30, [63,0,225,160]],
  hau_tho:              [33, [51,0,235,148]],
  hu_khong_tran_nhac:   [29, [46,23,240,160]],
  hon_nguyen_tho:       [28, [48,2,240,160]],
  thai_so_tho:          [31, [46,0,240,158]],
  tho_do_kiep_active:   [29, [1,0,191,173], [192,173,96,147,.864]],
  tho_chan_tien_active: [23, [75,27,285,167], [360,167,180,148.25,.6209]],
};
// Thủy v5 (11/2026, output/thuy-skill-set-demo-v5, đóng gói bằng tools/thuy_fx/pack_v5.py → img/fx/water/v2): cả 11 chiêu Luyện Khí → Kim Tiên
// dùng sheet Thủy riêng, cùng khuôn với Thổ v6. Độ Kiếp, Chân Tiên, Kim Tiên có ô riêng [rộng, cao, neo X, neo Y, unitScale] (thu 0,5).
// Kim Tiên thủy (Thiên Hà Giáng Thế) thay Thần Binh Trảm hệ Thủy; sát thương/ID giữ nguyên.
const WATER_SET_FX = {
  thuy_nguyet:           [19, [34,79,172,145]],
  van_thuy_tien:         [19, [33,77,207,143]],
  thuy_lao:              [32, [63,21,225,146]],
  thuong_hai:            [30, [58,1,228,146]],
  thien_ha:              [33, [24,16,229,146]],
  hu_hai:                [29, [44,2,239,152]],
  hon_nguyen_thuy:       [28, [20,5,223,159]],
  thai_so_thuy:          [31, [1,8,239,154]],
  thuy_do_kiep_active:   [29, [0,3,192,173], [192,173,96,147,.864]],
  thuy_chan_tien_active: [23, [15,0,346,167], [360,167,180,148,.6209]],
  thuy_kim_tien_active:  [34, [0,0,372,200], [372,200,186,183,.64]],
};
// Lôi v1 (11/2026, output/loi-skill-set-demo-v1, đóng gói bằng tools/loi_fx/pack_v1.py → img/fx/loi/v1): cả 11 chiêu Luyện Khí → Kim Tiên dùng sheet Lôi riêng
// (không mây; sét mọc từ thiên ấn / địa ấn). Ô riêng [rộng, cao, neo X, neo Y, unitScale, fullWidth]. fullWidth (Chân Tiên, Kim Tiên): ô rộng hơn màn hình,
// cho phần ngoài mép bị cắt thay vì thu nhỏ. Kim Tiên Lôi (Cửu Tiêu Thần Tướng Trảm) thay Thần Binh Trảm hệ Lôi; sát thương/ID giữ nguyên.
const LOI_SET_FX = {
  loi_kich:             [19, [97,16,191,146]],
  loi_bo:               [19, [27,68,193,147]],
  lien_loi:             [32, [57,13,223,147]],
  thien_loi:            [30, [70,13,218,147]],
  cuu_tieu:             [33, [54,13,224,147]],
  hu_khong_kiep_loi:    [29, [18,13,219,147]],
  hon_nguyen_loi:       [28, [22,13,227,147]],
  thai_so_loi:          [31, [15,13,225,157], [240,170,120,145,1]],
  loi_do_kiep_active:   [29, [13,15,267,267], [280,280,140,235,.7]],
  loi_chan_tien_active: [23, [13,13,294,227], [307,240,153.5,204.67,.772,true]],
  loi_kim_tien_active:  [34, [13,14,320,232], [333,245,166.5,214.7,.7988,true]],
};
// Pháp tướng (lớp sau lưng tu sĩ, neo chân tu sĩ, cùng nhịp 60 khung với sheet chính): [rộng, cao, neo X, neo Y, unitScale] của ô general.webp.
// Băng v1 (11/2026, output/bang-skill-set-demo-v1, đóng gói bằng tools/loi_fx/pack_v1.py → img/fx/bang/v1): cả 11 chiêu dùng sheet Băng riêng (hàn trận tuyết sáu cánh,
// tinh thể băng). Chân Tiên và Kim Tiên có pháp tướng Băng (GENERAL_FX) và trải rộng cả màn hình; Kim Tiên Băng thay Thần Binh Trảm chung.
const BANG_SET_FX = {
  bang_tien:             [19, [132,19,271,208], [336,224,201.6,177.8,.7143]],
  bang_thuan:            [22, [50,74,272,210],  [336,224,201.6,177.8,.7143]],
  dong_dia:              [32, [86,69,314,211],  [336,224,201.6,177.8,.7143]],
  tuyet_doi:             [30, [79,21,317,211],  [336,224,201.6,177.8,.7143]],
  huyen_bang:            [33, [100,13,303,210], [336,224,201.6,177.8,.7143]],
  hu_khong_bang_nguc:    [29, [71,13,320,211],  [336,224,201.6,177.8,.7143]],
  hon_nguyen_bang:       [28, [51,61,323,211],  [336,224,201.6,177.8,.7143]],
  thai_so_bang:          [31, [14,13,323,225],  [336,238,168,203,.7143]],
  bang_do_kiep_active:   [29, [13,21,267,267],  [280,280,140,235,.7]],
  bang_chan_tien_active: [24, [13,15,307,237],  [320,250,160,213.33,.7406,true]],
  bang_kim_tien_active:  [34, [13,13,320,232],  [333,245,166.5,214.7,.7988,true]],
};
// Phong v1 (11/2026, output/phong-skill-set-demo-v1 → img/fx/gio/v1): cả 11 chiêu dùng sheet Phong riêng (dải khí ngọc lục xoắn); Chân Tiên và Kim Tiên có pháp tướng Phong.
const GIO_SET_FX = {
  phong_nhan:           [19, [104,26,253,196], [320,214,192,169.33,.75]],
  phong_bo:             [19, [30,104,256,200], [320,214,192,169.33,.75]],
  cuong_phong:          [32, [104,29,280,201], [320,214,192,169.33,.75]],
  thien_phong:          [30, [88,13,296,199],  [320,214,192,169.33,.75]],
  cuu_thien_phong:      [33, [81,25,302,201],  [320,214,192,169.33,.75]],
  hu_khong_phong_nhan:  [29, [59,29,304,201],  [320,214,192,169.33,.75]],
  hon_nguyen_phong:     [28, [43,54,307,201],  [320,214,192,169.33,.75]],
  thai_so_phong:        [31, [17,13,313,221],  [330,234,165,199.38,.7272]],
  gio_do_kiep_active:   [29, [13,53,271,271],  [284,284,142,238.36,.6902]],
  gio_chan_tien_active: [23, [13,13,307,237],  [320,250,160,213.33,.7406,true]],
  gio_kim_tien_active:  [34, [13,13,317,230],  [330,243,165,212.76,.8061,true]],
};
const GENERAL_FX = {
  loi_chan_tien_active: [260,300,130,276,.5],
  loi_kim_tien_active:  [272,280,136,249.6,.625],
  bang_chan_tien_active:[260,300,130,276,.5],
  bang_kim_tien_active: [272,280,120,249.6,.625],
  gio_chan_tien_active: [260,300,130,276,.5],
  gio_kim_tien_active:  [272,280,136,249.6,.625],
};
const GROUND_TARGET_FX = {...MOC_SET_FX, ...THO_SET_FX, ...WATER_SET_FX, ...LOI_SET_FX, ...BANG_SET_FX, ...GIO_SET_FX};
const STAGE_FX = {...FIRE_SET_FX, ...KIM_TIEN_SET_FX};
// Hợp Thể → Chân Tiên: giữ nhịp, hình học và diễn xuất của chiêu Hỏa cùng bậc.
// Chất liệu đổi theo hệ, còn va chạm lấy từ sheet nguyên hệ có sẵn.
const ELEMENTAL_FIRE_FX = {};
// Mọi hệ (Mộc, Thổ, Thủy, Lôi, Băng, Phong) đã có sheet riêng từ Hợp Thể → Chân Tiên nên không còn mượn sheet Hỏa đổi màu.
function groundTargetFxSpec(skillId){
  const [hit, visibleBounds, cell] = GROUND_TARGET_FX[skillId];
  const element = Object.hasOwn(THO_SET_FX,skillId) ? 'tho' : Object.hasOwn(WATER_SET_FX,skillId) ? 'thuy' : Object.hasOwn(LOI_SET_FX,skillId) ? 'loi' : Object.hasOwn(BANG_SET_FX,skillId) ? 'bang' : Object.hasOwn(GIO_SET_FX,skillId) ? 'gio' : 'moc';
  const [frameWidth, frameHeight, anchorX, anchorY, unitScale, fullWidth] = cell ?? [240, 160, 144, 127, 1];
  const general = Object.hasOwn(GENERAL_FX,skillId) ? GENERAL_FX[skillId] : null;
  return {skillId, element, fullWidth:!!fullWidth,
    general:general && {src:`img/fx/${GROUND_TARGET_DIR[element]}/${GROUND_TARGET_VERSION[element]}/${skillId}/general.webp`,
      frameWidth:general[0], frameHeight:general[1], anchorX:general[2], anchorY:general[3], unitScale:general[4]},
    src:`img/fx/${GROUND_TARGET_DIR[element]}/${GROUND_TARGET_VERSION[element]}/${skillId}/sheet.webp`,
    frameWidth, frameHeight, columns:10, frameCount:60, fps:30, durationMs:2000, unitScale,
    sequence:true, stage:true, motion:'ground', stageAnchor:'ground', anchorX, anchorY,
    authoredImpactMs:hit*1000/30, reducedFrame:hit+4, filter:'none', visibleBounds,
    coreRight:element === 'moc' ? 144+60 : undefined,
    phases:[[0,.45,0,hit-1],[.45,.75,hit,59]]};
}
function stageFxSpec(skillId){
  const variant = Object.hasOwn(ELEMENTAL_FIRE_FX,skillId) ? ELEMENTAL_FIRE_FX[skillId] : null;
  if (!variant && Object.hasOwn(GROUND_TARGET_FX,skillId)) return groundTargetFxSpec(skillId);
  const sourceId = variant?.sourceId ?? skillId;
  if (!Object.hasOwn(STAGE_FX,sourceId)) return null;
  const fx = STAGE_FX[sourceId];
  const kim = Object.hasOwn(KIM_TIEN_SET_FX,skillId);
  const [dir, frameWidth, frameHeight, anchorX, anchorY, impactMs, span, stageAnchor, frameCount = 90, fitTop = 0, reducedFrame] = fx;
  const hit = Math.floor(impactMs*30/1000+1e-8);
  const L = kim ? KIM_TIEN_LAYERS[dir] : null;
  const layers = L && ['back','front'].map((name,i) => ({name, src:`img/fx/kim-tien/${dir}/${name}.webp`, x:L[i*4], y:L[i*4+1], w:L[i*4+2], h:L[i*4+3]}));
  // Kim Tiên: element riêng để không rơi vào nhánh căn sóng của Thủy; src = lớp front (dùng cho sheet.json/kiểm tra).
  let impactFx = null;
  if (variant){
    impactFx = variant.element === 'moc' ? null
      : Object.hasOwn(GROUND_TARGET_FX,variant.impactId) ? groundTargetFxSpec(variant.impactId)
      : (typeof FOUR_ELEMENTS_FX_DATA !== 'undefined' && FOUR_ELEMENTS_FX_DATA.find(s => s.skillId === variant.impactId)) || null;
  }
  return {skillId, element:variant?.element ?? (kim?'kim_tien':'hoa'), layers, src:kim?layers[1].src:`img/fx/fire/${dir}/sheet.webp`, frameWidth, frameHeight, columns:10, frameCount,
    fps:30, durationMs:frameCount*1000/30, sequence:true, stage:true, motion:'ground', authoredImpactMs:impactMs, anchorX, anchorY,
    span, stageAnchor, fitTop, reducedFrame:reducedFrame ?? Math.min(frameCount-1, hit+4), filter:variant?.filter ?? 'none', impactFx, visibleBounds:[0,0,frameWidth,frameHeight],
    phases:[[0,.45,0,hit-1],[.45,.75,hit,frameCount-1]]};
}
/** Chiêu có sheet riêng diễn trọn lượt: không vẽ chồng hiệu ứng cũ. */
function fireFxOwnsStage(skillId, realmIndex = 0){
  return Object.hasOwn(GROUND_TARGET_FX,skillId) || Object.hasOwn(STAGE_FX,skillId) || Object.hasOwn(ELEMENTAL_FIRE_FX,skillId);
}
function fireFxSpec(skillId, realmIndex = 0){
  const stageFx = stageFxSpec(skillId); if (stageFx) return stageFx;
  const realmFx = realmIndex === 4 ? HOA_THAN_FX[skillId] : realmIndex === 5 ? LUYEN_HU_FX[skillId] : null;
  if (realmFx){
    const [element, file, motion, sourceRows] = realmFx;
    const folder = realmIndex === 4 ? 'hoa-than' : 'luyen-hu';
    return {skillId, element, src:`img/fx/${folder}/${file}`, frameWidth:209, frameHeight:209,
      columns:6, frameCount:36, durationMs:2000, continuous:true, motion, sourceRows,
      anchorX:104.5, anchorY:104.5, scale:1.6, visibleBounds:[0,0,209,209], targetScale:1.35,
      phases:[[0,.25,0,11],[.25,.45,12,20],[.45,.60,21,28],[.60,.75,29,35]], filter:'none'};
  }
  const variant = skillId === 'hoa_cau' ? (realmIndex <= 0 ? 'small' : realmIndex === 1 ? 'medium' : 'large') : 'default';
  const fire = typeof FIRE_FX_DATA === 'undefined' || !Array.isArray(FIRE_FX_DATA) ? [] : FIRE_FX_DATA;
  const extra = typeof WATER_WOOD_FX_DATA === 'undefined' || !Array.isArray(WATER_WOOD_FX_DATA) ? [] : WATER_WOOD_FX_DATA;
  const elements = typeof FOUR_ELEMENTS_FX_DATA === 'undefined' || !Array.isArray(FOUR_ELEMENTS_FX_DATA) ? [] : FOUR_ELEMENTS_FX_DATA;
  const data = [...fire, ...extra, ...elements];
  const spec = data.find(s => s.skillId === skillId && (s.variant || 'default') === variant);
  return spec ? {...spec, filter:'none'} : null;
}
function fireFxPlan(skillId, s01, realmIndex = 0){
  const spec = fireFxSpec(skillId, realmIndex);
  if (!spec || !Number.isFinite(s01) || s01 < 0 || s01 >= .75) return null;
  const phase = spec.phases.findIndex(p => s01 >= p[0] && s01 < p[1]);
  if (phase < 0) return null;
  const [start, end, first, last] = spec.phases[phase];
  const authoredSeconds=spec.authoredImpactMs ? (s01<.45 ? s01/.45*spec.authoredImpactMs/1000
    : spec.authoredImpactMs/1000+(s01-.45)/.3*(spec.durationMs-spec.authoredImpactMs)/1000) : null;
  const frame = authoredSeconds!==null ? Math.min(spec.frameCount-1,Math.floor(authoredSeconds*spec.fps+1e-8))
    : spec.continuous ? Math.min(spec.frameCount-1, Math.floor(s01/.75*spec.frameCount))
    : Math.min(last, first + Math.floor((s01 - start) / (end - start) * (last - first + 1)));
  const placementProgress=spec.sequence && phase===1 ? .25+(s01-start)/(end-start)*.2 : s01;
  return {...spec, frame, phase, s01:placementProgress, anchorKind: spec.motion === 'projectile' ? (phase === 0 ? 'hand' : phase === 1 ? 'projectile' : 'target') : spec.motion === 'target' ? 'target' : 'ground', alpha: spec.sequence ? 1 : phase === 3 ? Math.max(0, 1 - (s01 - .60) / .15) : 1};
}
// Mọi lượt tuyệt kỹ (đầy nộ) diễn 2 giây, khớp BattleTiming.UltimateTurnSeconds phía server (2 + .25).
// cân bằng 11/2026: 3s → 2s cho mượt; sheet dựng 3s (90 khung) phát nhanh 1,5× chứ không cắt khung.
const ULTIMATE_CAST_SECONDS=2, ULTIMATE_AUTHORED_SECONDS=3;
// Điểm trúng đòn (giây trong trận) của chiêu có sheet riêng = mốc trúng trên sheet × tỉ lệ phát nhanh;
// chiêu khác kéo giãn đều hiệu ứng cũ (trúng ở .45/.75 thời lượng).
const ULTIMATE_IMPACT_SECONDS={};
for (const [id, fx] of Object.entries(STAGE_FX)) ULTIMATE_IMPACT_SECONDS[id] = fx[5]/1000*ULTIMATE_CAST_SECONDS/((fx[8] ?? 90)/30);
for (const [id, [hit]] of Object.entries(GROUND_TARGET_FX)) ULTIMATE_IMPACT_SECONDS[id] = hit/30*ULTIMATE_CAST_SECONDS/2;
for (const [id, {sourceId}] of Object.entries(ELEMENTAL_FIRE_FX)) ULTIMATE_IMPACT_SECONDS[id] = ULTIMATE_IMPACT_SECONDS[sourceId];
/** Tuyệt kỹ: server gắn cờ `ultimate`; id có sheet riêng vẫn nhận ra khi dữ liệu cũ thiếu cờ. */
function battleCastIsUltimate(c){ return !!c?.ultimate || ULTIMATE_IMPACT_SECONDS[c?.skill]!==undefined; }
/** Wall-clock timing for ultimate turns. Logical impact remains at s=.45. */
function battleSkillTiming(battle, turn){
  const turnCasts=(battle.casts || []).filter(c=>c.turn===turn);
  const ult=turnCasts.filter(battleCastIsUltimate).sort((a,b)=>b.dmg-a.dmg)[0];
  const long=!!ult && !battle.pvp?.idle?.[turn];
  const extra=battle.extra?.[turn] ? .34 : 0;
  const duration=long ? ULTIMATE_CAST_SECONDS : .75;
  const impact=long ? (ULTIMATE_IMPACT_SECONDS[ult.skill] ?? duration*.6) : .45, extraAt=long ? duration : .62;
  return {length:duration+.25+extra,heroEnd:(long?duration:.7)+extra,authoredDuration:long?duration:0,
    progress(seconds){
      const rep=!!extra && seconds>=extraAt && seconds<extraAt+extra;
      if(rep)return {s:.28+seconds-extraAt,rep:true};
      const x=seconds-(extra && seconds>=extraAt+extra?extra:0);
      const s=!long ? x : x<impact ? x/impact*.45 : x<duration ? .45+(x-impact)/(duration-impact)*.3 : .75+x-duration;
      // a: nhịp chuyển động của tu sĩ/quái theo giây thật (như lượt thường), không bị kéo giãn theo hiệu ứng 3 giây:
      // ra chiêu ngay đầu lượt rồi giữ thế đến lúc trúng; quái giật/nháy sáng đúng nhịp sau điểm trúng.
      const a=!long || x>=duration ? s : x<impact ? Math.min(x,.449) : Math.min(.749,.45+x-impact);
      return {s,a,rep:false};
    }};
}
/** Authored casts own the stage until their last frame, including monster telegraphs. */
function battleMonsterActionReady(battle,phase){
  const timing=battleSkillTiming(battle,phase.i);
  return !timing.authoredDuration || (phase.name==='fight' && !phase.rep && phase.s>=.78);
}
function fireFxImage(src, width = 768, height = 768, cache = fireFxCache){
  let entry = cache.get(src);
  if (entry){ cache.delete(src); cache.set(src, entry); return entry; }
  if (cache.size >= 2){
    const oldest = cache.keys().next().value, old = cache.get(oldest);
    old.image.onload = old.image.onerror = null; old.image.src = ''; old.bitmap?.close?.(); cache.delete(oldest);
  }
  const image = new Image(); entry = {image, state:'loading'}; cache.set(src, entry);
  // Giải mã sẵn thành ImageBitmap (ngoài luồng vẽ) và đẩy lên GPU một lần bằng nét vẽ 1×1 trước khi báo sẵn sàng:
  // sheet tuyệt kỹ lớn không làm khựng (~200 ms) khung đầu tiên của chiêu.
  image.onload = () => {
    const ok = image.naturalWidth === width && image.naturalHeight === height;
    const done = () => { if (entry.state === 'loading') entry.state = ok ? 'ready' : 'error'; };
    if (!ok || typeof createImageBitmap !== 'function') { done(); return; }
    createImageBitmap(image).then(bitmap => {
      if (cache.get(src) !== entry){ bitmap.close?.(); return; }
      entry.bitmap = bitmap; fireFxWarm(bitmap); done();
    }, done);
  };
  image.onerror = () => { entry.state = 'error'; };
  image.src = src;
  return entry;
}
function woodRootFrame(skillId, progress, reduced){
  if ((!Object.hasOwn(MOC_SET_FX,skillId) && skillId !== 'moc_kim_tien_active') ||
      !Number.isFinite(progress) || progress < .10 || progress >= .70) return null;
  const cage = WOOD_ROOT_CAGE.has(skillId);
  const lock = cage ? 39 : 31;
  let frame = lock;
  if (!reduced && progress < .37)
    frame = Math.floor((cage ? 4 : 3) + (lock-(cage ? 4 : 3))*(progress-.10)/.27);
  else if (!reduced && progress >= .45)
    frame = Math.min(89,64+Math.floor(26*(progress-.45)/.25));
  return {variant:cage ? 'cage' : 'spiral',frame};
}
function woodRootImages(variant){
  return ['back','front'].map(side =>
    fireFxImage(`img/fx/moc/root-bind-v1/${variant}-${side}.webp`,1600,1080,woodRootCache));
}
function drawWoodRootFx(ctx, skillId, progress, layer, {x, footY, height, reduced, alpha=1}){
  const plan = woodRootFrame(skillId,progress,reduced);
  if (!plan || height <= 0 || ![x,footY,height,alpha].every(Number.isFinite)) return false;
  const images = woodRootImages(plan.variant);
  if (images.some(entry => entry.state !== 'ready')) return false;
  const entry = images[layer === 'back' ? 0 : 1];
  const scale = height/53;
  ctx.save();
  ctx.globalAlpha *= alpha;
  ctx.imageSmoothingEnabled = true;
  ctx.drawImage(entry.bitmap || entry.image,plan.frame%10*160,Math.floor(plan.frame/10)*120,160,120,
    x-80*scale,footY-95.5*scale,160*scale,120*scale);
  ctx.restore();
  return true;
}
let fireFxWarmCtx = null;
function fireFxWarm(bitmap){
  try {
    fireFxWarmCtx ??= Object.assign(document.createElement('canvas'), {width:1, height:1}).getContext('2d');
    fireFxWarmCtx.drawImage(bitmap, 0, 0, 1, 1);
  } catch { /* chỉ là làm nóng: lỗi thì để lần vẽ thật tự tải */ }
}
function fireFxAvailable(skillId, realmIndex = 0){
  const spec = fireFxSpec(skillId, realmIndex);
  const roots = woodRootFrame(skillId,.37,true);
  if (roots) woodRootImages(roots.variant);
  if (spec?.layers) return spec.layers.map(l => fireFxImage(l.src, spec.columns*l.w, Math.ceil(spec.frameCount/spec.columns)*l.h).state).every(s => s === 'ready');
  if (!spec) return false;
  const base = fireFxImage(spec.src, spec.columns*spec.frameWidth, Math.ceil(spec.frameCount/spec.columns)*spec.frameHeight);
  const impact = spec.impactFx;
  const overlay = impact && fireFxImage(impact.src, impact.columns*impact.frameWidth, Math.ceil(impact.frameCount/impact.columns)*impact.frameHeight);
  const general = spec.general && fireFxImage(spec.general.src, spec.columns*spec.general.frameWidth, Math.ceil(spec.frameCount/spec.columns)*spec.general.frameHeight);
  return base.state === 'ready' && (!overlay || overlay.state !== 'loading') && (!general || general.state === 'ready');
}
/** Geometry for authored cast directions. Legacy manifests keep their original path. */
function fireFxPlacement(plan, layout, width, height){
  if (!plan || !['left','above','below','center'].includes(plan.castDirection)) return null;
  const {handX,handY,targetX,targetY,groundY,heroHeight}=layout;
  if (![handX,handY,targetX,targetY,groundY,heroHeight,width,height].every(Number.isFinite) || heroHeight<=0 || width<=4 || height<=4) return null;
  const bounds=plan.visibleBounds || [0,0,plan.frameWidth,plan.frameHeight];
  const [left,top,right,bottom]=bounds, ax=(left+right)/2/plan.frameWidth, ay=(top+bottom)/2/plan.frameHeight;
  const bw=(right-left)/plan.frameWidth, bh=(bottom-top)/plan.frameHeight;
  if (bw<=0 || bh<=0) return null;
  const direction=plan.castDirection, t=Math.max(0,Math.min(1,(plan.s01-.25)/.20)), travel=t*t*(3-2*t);
  const rotation=plan.phase===1 && plan.rotateWithTravel && !layout.reducedMotion ? (direction==='above'?Math.PI/2:direction==='below'?-Math.PI/2:0) : 0;
  const c=Math.cos(rotation),s=Math.sin(rotation);
  const rx=Math.abs(c)*bw/2+Math.abs(s)*bh/2, ry=Math.abs(s)*bw/2+Math.abs(c)*bh/2;
  const tx=Math.max(2.01,Math.min(width-2.01,targetX)),ty=Math.max(2.01,Math.min(height-2.01,targetY));
  const targetSize=Math.max(layout.targetWidth || heroHeight,layout.targetHeight || heroHeight);
  const impactSize=targetSize*(plan.targetScale || 1)/Math.max(bw,bh),chargeSize=heroHeight*.55/Math.max(bw,bh);
  const growth=direction==='center'?Math.min(1,Math.max(0,plan.s01/.45)):(plan.phase===0?0:plan.phase===1?travel:1);
  // Fit around the impact point first. Travel then clamps its origin without shrinking to nothing at an edge.
  const size=Math.min(chargeSize+(impactSize-chargeSize)*growth,(width-4)/(2*rx),(height-4)/(2*ry),
    (tx-2)/rx,(width-2-tx)/rx,(ty-2)/ry,(height-2-ty)/ry);
  if (!Number.isFinite(size) || size<=0) return null;
  let fromX=tx,fromY=ty;
  if (direction==='left'){fromX=handX;fromY=handY;}
  if (direction==='above')fromY=ty-heroHeight*1.45;
  if (direction==='below')fromY=groundY+heroHeight*.35;
  const k=layout.reducedMotion || plan.phase>=2 || direction==='center'?1:plan.phase===0?0:travel;
  const x=Math.max(2+rx*size,Math.min(width-2-rx*size,fromX+(tx-fromX)*k));
  const y=Math.max(2+ry*size,Math.min(height-2-ry*size,fromY+(ty-fromY)*k));
  return {x,y,size,ax,ay,rotation};
}
function drawFireFx(ctx, plan, layout){
  if (!plan) return false;
  const rows = Math.ceil(plan.frameCount/plan.columns);
  // Sheet hai lớp (Kim Tiên): layout.layer 'back' / 'front' vẽ một lớp, bỏ trống vẽ cả hai.
  const layers = plan.layers ? plan.layers.filter(l => !layout.layer || l.name === layout.layer) : null;
  const entries = layers ? layers.map(l => fireFxImage(l.src, plan.columns*l.w, rows*l.h)) : null;
  if (entries && entries.some(e => e.state !== 'ready')) return false;
  const entry = entries ? entries[0] : fireFxImage(plan.src, plan.columns*plan.frameWidth, rows*plan.frameHeight); if (entry.state !== 'ready') return false;
  const {handX,handY,targetX,targetY,groundY,heroHeight} = layout;
  let x = targetX, y = plan.anchorKind === 'ground' ? groundY : targetY;
  if (plan.anchorKind === 'hand'){ x = handX; y = handY; }
  if (plan.anchorKind === 'projectile'){
    const t = Math.max(0,Math.min(1,(plan.s01 - .25) / .20)), k = t*t*(3-2*t);
    x = handX + (targetX - handX)*k; y = handY + (targetY - handY)*k;
  }
  if (![x,y,heroHeight].every(Number.isFinite) || heroHeight <= 0) return false;
  // Layout dùng tọa độ logic; canvas có thể có backing store DPR / preview ×4.
  const transform = ctx.getTransform?.(), scaleX = Math.abs(transform?.a || 1), scaleY = Math.abs(transform?.d || 1);
  const w = ctx.canvas.width/scaleX, h = ctx.canvas.height/scaleY;
  if (w <= 2 || h <= 2) return false;
  let ax = plan.anchorX / plan.frameWidth, ay = plan.anchorY / plan.frameHeight, rotation = 0;
  x = Math.max(1,Math.min(w-1,x)); y = Math.max(1,Math.min(h-1,y));
  let size;
  if (plan.stage && !plan.span){
    x=targetX; y=groundY;
    const [left,top,right,bottom]=plan.visibleBounds;
    // Mộc v3: chiêu cao cấp lan lá/rễ tới sát mép phải ô; chỉ bắt phần lõi (tới coreRight) nằm trong khung,
    // phần ngọn ngoài cùng được tràn — tránh thu nhỏ chiêu cao cấp hơn chiêu cấp thấp (phạm vi to dần theo cấp).
    const fitRight=Math.min(right,plan.coreRight ?? right);
    // fullWidth (Lôi Chân Tiên / Kim Tiên): chiêu rộng cả màn hình, không giới hạn theo chiều ngang; phần ngoài mép bị khung hình cắt.
    const units=Math.min((layout.targetHeight || heroHeight)/27.5*(plan.unitScale ?? 1),   // unitScale: ô khác 240 × 160 (Thổ Độ Kiếp/Chân Tiên)
      plan.fullWidth ? Infinity : (x-2)/(plan.anchorX-left),plan.fullWidth ? Infinity : (w-2-x)/(fitRight-plan.anchorX),
      (y-2)/(plan.anchorY-top),(h-2-y)/(bottom-plan.anchorY));
    size=units*plan.frameWidth;
  } else if (plan.stage){
    // Neo sheet vào quái; tỉ lệ để quãng bay trên sheet khớp khoảng tay → quái, không cho tràn mép trên.
    x=targetX; y=plan.stageAnchor==='ground' ? groundY : targetY;
    const dist=Math.max(heroHeight*.8, targetX-handX);
    let k=Math.min(dist/plan.span, (y-2)/(plan.anchorY-(plan.fitTop||0)), heroHeight*6/plan.frameHeight);
    if (plan.element === 'thuy'){
      // Center the authored wave on the target while keeping its left and right edges visible.
      k=Math.min(k,(x-2)/plan.anchorX,(w-2-x)/(plan.frameWidth-plan.anchorX));
    } else if (plan.element !== 'kim_tien'){   // Kim Tiên: không dịch neo, mũi kiếm phải chạm đúng quái (mép trái là áo choàng hư ảnh, cho tràn)
      // Hỏa: giữ nguyên cỡ, dịch neo vào trong để mép trái/phải sheet không bị khung hình cắt (neo lệch tâm sheet nên quái sát mép phải hay bị mất nửa phải).
      const lo=plan.anchorX*k+2, hi=w-2-(plan.frameWidth-plan.anchorX)*k;
      if (lo<=hi) x=Math.max(lo,Math.min(hi,x));
    }
    size=k*plan.frameWidth;
  } else if (plan.scene){
    x=targetX;y=groundY;
    const [left,top,right,bottom]=plan.visibleBounds;
    const units=Math.min(heroHeight*3.4/plan.frameHeight,
      (x-2)/(plan.anchorX-left),(w-2-x)/(right-plan.anchorX),
      (y-2)/(plan.anchorY-top),(h-2-y)/(bottom-plan.anchorY));
    size=units*plan.frameWidth;
  } else if (plan.sequence){
    // This atlas retains a wide particle envelope. Size its core, not its transparent margin.
    size=heroHeight*(plan.phase<2?4:8);
  } else if (plan.castDirection){
    const placement=fireFxPlacement(plan,layout,w,h); if (!placement) return false;
    ({x,y,size,ax,ay,rotation}=placement);
  } else if (plan.visibleBounds && plan.targetScale){
    const [left,top,right,bottom]=plan.visibleBounds;
    const bw=(right-left)/plan.frameWidth, bh=(bottom-top)/plan.frameHeight;
    const targetSize=Math.max(layout.targetWidth || heroHeight,layout.targetHeight || heroHeight);
    const impactSize=targetSize*plan.targetScale/Math.max(bw,bh);
    const chargeSize=heroHeight*.55/Math.max(bw,bh);
    const travel=plan.anchorKind==='hand'?0:plan.anchorKind==='projectile'?Math.max(0,Math.min(1,(plan.s01-.25)/.2)):1;
    const wanted=plan.motion==='projectile'?chargeSize+(impactSize-chargeSize)*travel:impactSize;
    const l=Math.max(0,ax-left/plan.frameWidth),r=Math.max(0,right/plan.frameWidth-ax);
    const u=Math.max(0,ay-top/plan.frameHeight),d=Math.max(0,bottom/plan.frameHeight-ay);
    // Fit visible art, not transparent padding. Ground effects end at the feet.
    size=Math.min(wanted,(w-4)/bw,(h-4)/bh,
      l>0?(x-2)/l:Infinity,r>0?(w-2-x)/r:Infinity,
      u>0?(y-2)/u:Infinity,d>0?(h-2-y)/d:Infinity);
  } else {
    size = Math.min(heroHeight*plan.scale, w*.9, h*.9, x/ax, (w-x)/(1-ax), y/ay, (h-y)/(1-ay));
  }
  if (!Number.isFinite(size) || size <= 0) return false;
  const frame = layout.reducedMotion ? (plan.reducedFrame ?? (plan.sequence ? [6,30,48,58] : plan.continuous ? [5,16,24,33] : [2,6,9,14])[plan.phase]) : plan.frame;
  ctx.save();
  try {
    ctx.filter = plan.filter || 'none';
    ctx.globalAlpha *= plan.alpha * (layout.reducedMotion ? .8 : 1);
    ctx.imageSmoothingEnabled = plan.continuous || plan.stage || ['thuy','moc','tho','loi','bang','gio'].includes(plan.element);
    if (rotation){ctx.translate(x,y);ctx.rotate(rotation);}
    // Some authored sheets have uneven row gutters; crop by their actual row
    // boundaries so a feather or wave from the previous row cannot leak in.
    const row=Math.floor(frame/plan.columns);
    const sourceY=plan.sourceRows ? plan.sourceRows[row] : row*plan.frameHeight;
    const sourceHeight=plan.sourceRows ? plan.sourceRows[row+1]-sourceY : plan.frameHeight;
    const drawHeight=plan.scene || plan.stage ? size*plan.frameHeight/plan.frameWidth : size;
    if (layers){
      const k=size/plan.frameWidth, left=x-ax*size, top=y-ay*drawHeight;
      layers.forEach((l,i) => ctx.drawImage(entries[i].bitmap || entries[i].image, frame%plan.columns*l.w, row*l.h, l.w, l.h, left+l.x*k, top+l.y*k, l.w*k, l.h*k));
      return true;
    }
    ctx.drawImage(entry.bitmap || entry.image, frame%plan.columns*plan.frameWidth, sourceY, plan.frameWidth,sourceHeight,(rotation?0:x)-ax*size,(rotation?0:y)-ay*drawHeight,size,drawHeight);
  } finally { ctx.restore(); }
  if (plan.impactFx && plan.phase === 1 && !layout.hideImpactFx){
    const impact = plan.impactFx;
    const first = impact.phases.find(([start]) => start === .45)[2];
    const baseFirst = plan.phases[1][2];
    const t = (plan.frame-baseFirst)/Math.max(1,plan.frameCount-1-baseFirst);
    drawFireFx(ctx,{...impact, phase:2, s01:.45+t*.3,
      frame:first+Math.floor(t*(impact.frameCount-1-first)),
      reducedFrame:impact.reducedFrame ?? first, anchorKind:'ground', filter:'none',
      alpha:Math.min(1, (1-t)*2)},layout);
  }
  return true;
}
/** Pháp tướng sau lưng tu sĩ (Lôi Chân Tiên / Kim Tiên): gọi TRƯỚC khi vẽ tu sĩ. layout: casterX, casterGroundY (chân), casterHeight (cao tu sĩ vẽ ra), reducedMotion. */
function drawFireGeneralFx(ctx, skillId, s01, realmIndex, layout){
  if (!Number.isFinite(s01) || s01 < 0 || s01 >= .75 || !Object.hasOwn(GENERAL_FX,skillId) || !fireFxAvailable(skillId, realmIndex)) return false;
  const plan = fireFxPlan(skillId, s01, realmIndex), g = plan?.general;
  const {casterX, casterGroundY, casterHeight} = layout;
  if (!g || ![casterX,casterGroundY,casterHeight].every(Number.isFinite) || casterHeight <= 0) return false;
  const entry = fireFxImage(g.src, plan.columns*g.frameWidth, Math.ceil(plan.frameCount/plan.columns)*g.frameHeight);
  if (entry.state !== 'ready') return false;
  const frame = layout.reducedMotion ? plan.reducedFrame : plan.frame, size = casterHeight/27.5*g.unitScale*g.frameWidth, k = size/g.frameWidth;
  ctx.save();
  try {
    ctx.globalAlpha *= layout.reducedMotion ? .8 : 1;
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(entry.bitmap || entry.image, frame%plan.columns*g.frameWidth, Math.floor(frame/plan.columns)*g.frameHeight, g.frameWidth, g.frameHeight,
      casterX-g.anchorX*k, casterGroundY-g.anchorY*k, g.frameWidth*k, g.frameHeight*k);
  } finally { ctx.restore(); }
  return true;
}
/** true kể cả pha nghỉ: chặn hiệu ứng cũ bật lại ở cuối lượt. */
function drawFireSkillFx(ctx, skillId, s01, realmIndex, layout){
  if (!Number.isFinite(s01) || s01 < 0 || s01 > 1 || !fireFxAvailable(skillId, realmIndex)) return false;
  const plan = fireFxPlan(skillId, s01, realmIndex);
  return plan ? drawFireFx(ctx, plan, layout) : true;
}
