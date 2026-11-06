/* Shared manual VFX timeline for preview and combat; sheets contain effects only. */
const fireFxCache = new Map();
// Hóa Thần / Luyện Hư: 36 authored frames, two seconds in the skill preview.
// Combat maps the full sequence to its existing hero-turn timeline.
const HOA_THAN_FX = {
  huyen_bang: ['bang', 'phoenix-ice-spritesheet-v2.png', 'projectile', [0,226,447,674,888,1090,1254]],
  cuu_tieu: ['loi', 'lightning-burst-spritesheet-v1.png', 'target'],
  thien_ha: ['thuy', 'thuy-long-quyen.png', 'projectile', [0,216,440,682,924,1120,1254]],
  thanh_de: ['moc', 'thanh-lien-phuoc-sat.png', 'target'],
  hau_tho: ['tho', 'thien-son-tran.png', 'target'],
  cuu_thien_phong: ['gio', 'phong-nhan-loan-vu.png', 'projectile'],
};
const LUYEN_HU_FX = {
  hu_hai: ['thuy', 'thien-ha-quy-hu-v2.png', 'target', [0,234,459,680,887,1090,1254]],
  hu_khong_kien_moc: ['moc', 'hu-khong-kien-moc.png', 'target'],
  hu_khong_tran_nhac: ['tho', 'hu-khong-tran-nhac.png', 'target'],
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
};
function fireSetFxSpec(skillId){
  const fx = FIRE_SET_FX[skillId]; if (!fx) return null;
  const [dir, frameWidth, frameHeight, anchorX, anchorY, impactMs, span, stageAnchor] = fx;
  const hit = Math.floor(impactMs*30/1000);
  return {skillId, element:'hoa', src:`img/fx/fire/${dir}/sheet.webp`, frameWidth, frameHeight, columns:10, frameCount:90,
    fps:30, durationMs:3000, sequence:true, stage:true, motion:'ground', authoredImpactMs:impactMs, anchorX, anchorY,
    span, stageAnchor, reducedFrame:Math.min(89, hit+4), filter:'none', visibleBounds:[0,0,frameWidth,frameHeight],
    phases:[[0,.45,0,hit-1],[.45,.75,hit,89]]};
}
/** Chiêu có sheet riêng diễn trọn lượt: không vẽ chồng hiệu ứng cũ. */
function fireFxOwnsStage(skillId, realmIndex = 0){
  return !!FIRE_SET_FX[skillId] || (skillId === 'hu_khong_bang_nguc' && realmIndex >= 5);
}
const FX_ALIAS = {
  thai_so_hoa:    ['hu_khong_hoa', 'hue-rotate(-20deg) saturate(1.6) brightness(1.15)'],   // Đại Thừa: tạm dùng hiệu ứng Hư Không Diễm
  thai_so_thuy:   ['hon_nguyen_thuy', 'hue-rotate(-25deg) saturate(1.4) brightness(1.15)'],
  thai_so_moc:    ['hon_nguyen_moc', 'hue-rotate(30deg) saturate(1.3) brightness(1.15)'],
  thai_so_tho:    ['hon_nguyen_tho', 'hue-rotate(-15deg) saturate(1.4) brightness(1.15)'],
  thai_so_loi:    ['hon_nguyen_loi', 'hue-rotate(25deg) saturate(1.3) brightness(1.2)'],
  thai_so_bang:   ['hon_nguyen_bang', 'hue-rotate(-20deg) saturate(1.3) brightness(1.15)'],
  thai_so_phong:  ['hon_nguyen_phong', 'hue-rotate(25deg) saturate(1.3) brightness(1.15)'],
};
for (const [element, source] of Object.entries({hoa:'hu_khong_hoa',thuy:'hon_nguyen_thuy',moc:'hon_nguyen_moc',tho:'hon_nguyen_tho',loi:'hon_nguyen_loi',bang:'hon_nguyen_bang',gio:'hon_nguyen_phong'}))
  for (const realm of ['do_kiep','chan_tien'])
    FX_ALIAS[`${element}_${realm}_active`] = [source, realm === 'do_kiep' ? 'hue-rotate(12deg) saturate(1.3)' : 'brightness(1.2) saturate(.9)'];
function fireFxSpec(skillId, realmIndex = 0){
  if (skillId === 'hu_khong_bang_nguc' && realmIndex >= 5) return {
    skillId,element:'bang',src:'img/fx/ice/thien-son-bang-kiem-v4/sheet-runtime-v1.png',
    frameWidth:240,frameHeight:165,columns:10,frameCount:90,fps:30,durationMs:3000,
    sequence:true,scene:true,motion:'ground',authoredImpactMs:1600,
    anchorX:170,anchorY:135,reducedFrame:60,filter:'none',
    visibleBounds:[108,27.5,232.5,161.5],phases:[[0,.45,0,47],[.45,.75,48,89]]
  };
  const fireSet = fireSetFxSpec(skillId); if (fireSet) return fireSet;
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
  // Hợp Thể / Đại Thừa kế thừa timeline chiêu bậc dưới, chỉ đổi màu (FX_ALIAS: chiêu → [chiêu nguồn, bộ lọc]).
  const [sourceId, filter] = FX_ALIAS[skillId] || [skillId, 'none'];
  const spec = data.find(s => s.skillId === sourceId && (s.variant || 'default') === variant);
  return spec ? {...spec, filter} : null;
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
const ULTIMATE_IMPACT_SECONDS={hu_khong_bang_nguc:1.6*ULTIMATE_CAST_SECONDS/ULTIMATE_AUTHORED_SECONDS};
for (const [id, fx] of Object.entries(FIRE_SET_FX)) ULTIMATE_IMPACT_SECONDS[id] = fx[5]/1000*ULTIMATE_CAST_SECONDS/ULTIMATE_AUTHORED_SECONDS;
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
function fireFxImage(src, width = 768, height = 768){
  let entry = fireFxCache.get(src);
  if (entry){ fireFxCache.delete(src); fireFxCache.set(src, entry); return entry; }
  if (fireFxCache.size >= 2){
    const oldest = fireFxCache.keys().next().value, old = fireFxCache.get(oldest);
    old.image.onload = old.image.onerror = null; old.image.src = ''; old.bitmap?.close?.(); fireFxCache.delete(oldest);
  }
  const image = new Image(); entry = {image, state:'loading'}; fireFxCache.set(src, entry);
  // Giải mã sẵn thành ImageBitmap (ngoài luồng vẽ) và đẩy lên GPU một lần bằng nét vẽ 1×1 trước khi báo sẵn sàng:
  // sheet tuyệt kỹ lớn không làm khựng (~200 ms) khung đầu tiên của chiêu.
  image.onload = () => {
    const ok = image.naturalWidth === width && image.naturalHeight === height;
    const done = () => { if (entry.state === 'loading') entry.state = ok ? 'ready' : 'error'; };
    if (!ok || typeof createImageBitmap !== 'function') { done(); return; }
    createImageBitmap(image).then(bitmap => {
      if (fireFxCache.get(src) !== entry){ bitmap.close?.(); return; }
      entry.bitmap = bitmap; fireFxWarm(bitmap); done();
    }, done);
  };
  image.onerror = () => { entry.state = 'error'; };
  image.src = src;
  return entry;
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
  return !!spec && fireFxImage(spec.src, spec.columns*spec.frameWidth, Math.ceil(spec.frameCount/spec.columns)*spec.frameHeight).state === 'ready';
}
function fireFxCacheInfo(){ return {size:fireFxCache.size, sources:[...fireFxCache.keys()]}; }
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
  const entry = fireFxImage(plan.src, plan.columns*plan.frameWidth, Math.ceil(plan.frameCount/plan.columns)*plan.frameHeight); if (entry.state !== 'ready') return false;
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
  if (plan.stage){
    // Neo sheet vào quái; tỉ lệ để quãng bay trên sheet khớp khoảng tay → quái, không cho tràn mép trên.
    x=targetX; y=plan.stageAnchor==='ground' ? groundY : targetY;
    const dist=Math.max(heroHeight*.8, targetX-handX);
    const k=Math.min(dist/plan.span, (y-2)/plan.anchorY, heroHeight*6/plan.frameHeight);
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
    ctx.drawImage(entry.bitmap || entry.image, frame%plan.columns*plan.frameWidth, sourceY, plan.frameWidth,sourceHeight,(rotation?0:x)-ax*size,(rotation?0:y)-ay*drawHeight,size,drawHeight);
  } finally { ctx.restore(); }
  return true;
}
/** true kể cả pha nghỉ: chặn hiệu ứng cũ bật lại ở cuối lượt. */
function drawFireSkillFx(ctx, skillId, s01, realmIndex, layout){
  if (!Number.isFinite(s01) || s01 < 0 || s01 > 1 || !fireFxAvailable(skillId, realmIndex)) return false;
  const plan = fireFxPlan(skillId, s01, realmIndex);
  return plan ? drawFireFx(ctx, plan, layout) : true;
}
