/* js/stage.js: canvas chính (nền trận đánh). Tách từ index.html (tools/split_index.py). */
/* ============================================================
   CANVAS — TU SĨ NGỒI XẾP BẰNG (2D)
   ============================================================ */
const cv = document.getElementById('stage'), ctx = cv.getContext('2d');
let W = 0, H = 0;
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
function resize(){
  const r = cv.getBoundingClientRect(), d = Math.min(2, devicePixelRatio || 1);
  W = r.width; H = r.height; cv.width = W*d; cv.height = H*d; ctx.setTransform(d,0,0,d,0,0);
}
const stars = Array.from({length:46}, () => ({x:Math.random(), y:Math.random()*.5, r:Math.random()*1.2+.3, p:Math.random()*6}));
let rings = [], floats = [];
function hexA(h,a){ const n=parseInt(h.slice(1),16); return `rgba(${n>>16&255},${n>>8&255},${n&255},${a})`; }

// ===== Cảnh: trận đánh pixel khi thám hiểm; lúc ảnh chưa tải xong chỉ vẽ nền trời đêm =====
function drawScene(t, dt){ drawSceneInner(t, dt); }

let scrollT = 0;
function drawSceneInner(t, dt){
  const r = realm();
  const visual=typeof battleVisualFrame==='function' ? battleVisualFrame(t*1000) : null;
  const visualBattle=visual?.battle||S.battle;
  const fight = visualBattle && sprite48(skinArt('hero')) && sprite48(visualBattle.image);
  const ph = fight ? (visual?.ph||battlePhase()) : null;
  if (ph && ph.name === 'walk' && !reduce) scrollT += dt;
  if (ph?.name === 'lose' && typeof powerTipsOnLose === 'function') { const lostMap=(visual?.state||S).map; powerTipsOnLose(lostMap?.area > mapAreaCount(lostMap) ? 'boss' : 'area', 'map' + ph.trip); }   // bản đồ của trận đang diễn
  if (fight && drawPixelBattle(t, dt, ph, r,visualBattle,visual?.state||S)) return;
  if(typeof drawConceptBackdrop==='function'&&drawConceptBackdrop(ctx,W,H,(visual?.state||S).map?.diff>=3))return;
  // ảnh trận đánh chưa tải xong: nền trời đêm tạm thời
  const g = ctx.createLinearGradient(0,0,0,H); g.addColorStop(0,'#0b1216'); g.addColorStop(1,'#1a2529');
  ctx.fillStyle = g; ctx.fillRect(0,0,W,H);
  for (const s of stars){ ctx.fillStyle = `rgba(235,228,211,${.25+.35*Math.abs(Math.sin(t*.6+s.p))})`; ctx.beginPath(); ctx.arc(s.x*W, s.y*H, s.r, 0, 7); ctx.fill(); }
}
