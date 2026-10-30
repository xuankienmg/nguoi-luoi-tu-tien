/* Shared manual VFX timeline for preview and combat; sheets contain effects only. */
const fireFxCache = new Map();
// Hóa Thần / Luyện Hư: 36 authored frames, two seconds in the skill preview.
// Combat maps the full sequence to its existing hero-turn timeline.
const HOA_THAN_FX = {
  than_hoa: ['hoa', 'phoenix-fire-spritesheet-v1.png', 'projectile'],
  huyen_bang: ['bang', 'phoenix-ice-spritesheet-v2.png', 'projectile', [0,226,447,674,888,1090,1254]],
  cuu_tieu: ['loi', 'lightning-burst-spritesheet-v1.png', 'target'],
  thien_ha: ['thuy', 'thuy-long-quyen.png', 'projectile', [0,216,440,682,924,1120,1254]],
  thanh_de: ['moc', 'thanh-lien-phuoc-sat.png', 'target'],
  hau_tho: ['tho', 'thien-son-tran.png', 'target'],
  cuu_thien_phong: ['gio', 'phong-nhan-loan-vu.png', 'projectile'],
};
const LUYEN_HU_FX = {
  hu_khong_hoa: ['hoa', 'hu-khong-diem.png', 'target'],
  hu_hai: ['thuy', 'thien-ha-quy-hu-v2.png', 'target', [0,234,459,680,887,1090,1254]],
  hu_khong_kien_moc: ['moc', 'hu-khong-kien-moc.png', 'target'],
  hu_khong_tran_nhac: ['tho', 'hu-khong-tran-nhac.png', 'target'],
  hu_khong_kiep_loi: ['loi', 'hu-khong-kiep-loi.png', 'target'],
  hu_khong_bang_nguc: ['bang', 'hu-khong-bang-nguc.png', 'target'],
  hu_khong_phong_nhan: ['gio', 'hu-khong-phong-nhan.png', 'target'],
};
const FX_ALIAS = {
  hon_nguyen_hoa: ['hu_khong_hoa', 'hue-rotate(35deg) saturate(.75) brightness(1.2)'],
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
  const frame = spec.continuous ? Math.min(spec.frameCount-1, Math.floor(s01/.75*spec.frameCount))
    : Math.min(last, first + Math.floor((s01 - start) / (end - start) * (last - first + 1)));
  return {...spec, frame, phase, s01, anchorKind: spec.motion === 'projectile' ? (phase === 0 ? 'hand' : phase === 1 ? 'projectile' : 'target') : spec.motion === 'target' ? 'target' : 'ground', alpha: phase === 3 ? Math.max(0, 1 - (s01 - .60) / .15) : 1};
}
function fireFxImage(src, width = 768, height = 768){
  let entry = fireFxCache.get(src);
  if (entry){ fireFxCache.delete(src); fireFxCache.set(src, entry); return entry; }
  if (fireFxCache.size >= 2){
    const oldest = fireFxCache.keys().next().value, old = fireFxCache.get(oldest);
    old.image.onload = old.image.onerror = null; old.image.src = ''; fireFxCache.delete(oldest);
  }
  const image = new Image(); entry = {image, state:'loading'}; fireFxCache.set(src, entry);
  image.onload = () => { entry.state = image.naturalWidth === width && image.naturalHeight === height ? 'ready' : 'error'; };
  image.onerror = () => { entry.state = 'error'; };
  image.src = src;
  return entry;
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
  if (plan.castDirection){
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
  const frame = layout.reducedMotion ? (plan.continuous ? [5,16,24,33] : [2,6,9,14])[plan.phase] : plan.frame;
  ctx.save();
  try {
    ctx.filter = plan.filter || 'none';
    ctx.globalAlpha *= plan.alpha * (layout.reducedMotion ? .8 : 1);
    ctx.imageSmoothingEnabled = plan.continuous || ['thuy','moc','tho','loi','bang','gio'].includes(plan.element);
    if (rotation){ctx.translate(x,y);ctx.rotate(rotation);}
    // Some authored sheets have uneven row gutters; crop by their actual row
    // boundaries so a feather or wave from the previous row cannot leak in.
    const row=Math.floor(frame/plan.columns);
    const sourceY=plan.sourceRows ? plan.sourceRows[row] : row*plan.frameHeight;
    const sourceHeight=plan.sourceRows ? plan.sourceRows[row+1]-sourceY : plan.frameHeight;
    ctx.drawImage(entry.image, frame%plan.columns*plan.frameWidth, sourceY, plan.frameWidth,sourceHeight,(rotation?0:x)-ax*size,(rotation?0:y)-ay*size,size,size);
  } finally { ctx.restore(); }
  return true;
}
/** true kể cả pha nghỉ: chặn hiệu ứng cũ bật lại ở cuối lượt. */
function drawFireSkillFx(ctx, skillId, s01, realmIndex, layout){
  if (!Number.isFinite(s01) || s01 < 0 || s01 > 1 || !fireFxAvailable(skillId, realmIndex)) return false;
  const plan = fireFxPlan(skillId, s01, realmIndex);
  return plan ? drawFireFx(ctx, plan, layout) : true;
}
