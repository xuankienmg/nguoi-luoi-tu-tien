// VFX trạng thái bất lợi (atlas từ output/status-effects-demo-v1).
// Mỗi atlas 6×10 ô 168×168: hàng 0–4 lớp sau nhân vật, 5–9 lớp trước. Neo chân [84,140], nhân vật tham chiếu cao 90 px.
// 20 FPS: khung 0–4 vào, lặp 5–22 khi còn hiệu lực, 23–29 khi hết. Khung 10 cho chế độ giảm chuyển động.
const STATUS_VFX = {
  fps:20, frame:168, cols:6, frontRow:5, anchor:[84,140], refHeight:90,
  enter:5, loopEnd:23, count:30, still:10,
  kinds:['burn','freeze','poison','paralyze','stun'],
};
const statusVfxSheets = {};
const statusVfxTracks = {};

function statusVfxSheet(kind){
  if(!STATUS_VFX.kinds.includes(kind))return null;
  let im = statusVfxSheets[kind];
  if(!im){ im = statusVfxSheets[kind] = new Image(); im.decoding = 'async'; im.src = 'img/status-fx/' + kind + '.png'; }   // nạp khi cần: mỗi atlas ~6,5 MiB RGBA
  return im.complete && im.naturalWidth ? im : null;
}
/** Atlas đã sẵn sàng để vẽ (chưa có thì vẫn dùng hình vẽ tay cũ). */
function statusVfxReady(kind){ return !!statusVfxSheet(kind); }

/** Cập nhật trạng thái đang có của một mục tiêu, gọi một lần mỗi khung trước khi vẽ. `scene` đổi thì xóa trạng thái cũ. */
function statusVfxUpdate(target, scene, kinds, time){
  let tr = statusVfxTracks[target];
  if(!tr || tr.scene !== scene || time < tr.time){ tr = statusVfxTracks[target] = {scene, time, fx:{}}; }
  tr.time = time;
  const active = new Set(kinds.filter(k => STATUS_VFX.kinds.includes(k)));
  for(const kind of active){
    statusVfxSheet(kind);
    const fx = tr.fx[kind];
    if(!fx) tr.fx[kind] = {start:time, end:null};
    else if(fx.end !== null){ fx.start = time - STATUS_VFX.enter / STATUS_VFX.fps; fx.end = null; }   // bật lại khi đang tan: vào thẳng vòng lặp
  }
  for(const [kind, fx] of Object.entries(tr.fx)){
    if(active.has(kind))continue;
    if(fx.end === null) fx.end = time;
    if((time - fx.end) * STATUS_VFX.fps >= STATUS_VFX.count - STATUS_VFX.loopEnd) delete tr.fx[kind];
  }
}
function statusVfxFrame(fx, time, reduced){
  const S = STATUS_VFX;
  if(reduced) return fx.end === null ? S.still : -1;
  if(fx.end !== null){ const f = S.loopEnd + Math.floor((time - fx.end) * S.fps); return f < S.count ? f : -1; }
  const el = Math.max(0, Math.floor((time - fx.start) * S.fps));
  return el < S.enter ? el : S.enter + (el - S.enter) % (S.loopEnd - S.enter);
}
/** Vẽ một lớp ('back' trước sprite, 'front' sau sprite) của mọi trạng thái của mục tiêu. x, footY, height theo pixel canvas thật. */
function statusVfxDraw(ctx, target, layer, {x, footY, height, time, reduced=false, alpha=1}){
  const tr = statusVfxTracks[target];
  if(!tr || height <= 0) return;
  const S = STATUS_VFX, k = height / S.refHeight, size = S.frame * k;
  ctx.save();
  ctx.imageSmoothingEnabled = true; ctx.imageSmoothingQuality = 'high'; ctx.globalAlpha = alpha;
  for(const [kind, fx] of Object.entries(tr.fx)){
    const im = statusVfxSheet(kind), f = statusVfxFrame(fx, time, reduced);
    if(!im || f < 0) continue;
    const row = Math.floor(f / S.cols) + (layer === 'front' ? S.frontRow : 0), col = f % S.cols;
    ctx.drawImage(im, col * S.frame, row * S.frame, S.frame, S.frame, x - S.anchor[0] * k, footY - S.anchor[1] * k, size, size);
  }
  ctx.restore();
}
