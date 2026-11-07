/* Thiên lôi (hiệu ứng trang bị/tinh thạch): một tia sét "Nhất Lôi" giáng xuống quái khi đòn thường kèm sét.
   Sheet 6×5 ô 320×240, 30 khung / 1 giây, chạm đất ở khung 10 (0,333 s), điểm neo chân sét (160,200). */
const LIGHTNING_STRIKE_FX = {
  src: 'img/fx/loi/nhat-loi-v1/sheet.png',
  frameWidth: 320, frameHeight: 240, columns: 6, frameCount: 30, fps: 30,
  anchorX: 160, anchorY: 200, impactFrame: 10, reducedFrame: 10,
};
let lightningStrikeImg = null;
function lightningStrikeReady(){
  if (!lightningStrikeImg){ lightningStrikeImg = new Image(); lightningStrikeImg.src = LIGHTNING_STRIKE_FX.src; }
  return lightningStrikeImg.complete && lightningStrikeImg.naturalWidth > 0;
}
/** Vẽ tia sét theo tiến độ lượt s (0→1); hitAt = thời điểm sét chạm quái (trùng lúc hiện số sát thương).
 *  turnSeconds: độ dài lượt thật (giây) để giữ đúng 30 FPS. Trả false nếu ngoài khoảng hiển thị hoặc chưa tải ảnh. */
function drawLightningStrikeFx(ctx, s, {x, groundY, hitAt = .38, turnSeconds = 1, reducedMotion = false} = {}){
  const F = LIGHTNING_STRIKE_FX;
  if (!lightningStrikeReady()) return false;
  const sec = (s - hitAt) * turnSeconds;
  let frame = Math.floor(sec * F.fps) + F.impactFrame;
  if (reducedMotion){ if (sec < 0 || sec > .15) return false; frame = F.reducedFrame; }
  if (frame < 0 || frame >= F.frameCount) return false;
  const k = Math.max(.5, groundY / F.anchorY);                                       // đỉnh sheet chạm mép trên khung trận
  const sx = (frame % F.columns) * F.frameWidth, sy = Math.floor(frame / F.columns) * F.frameHeight;
  ctx.save();
  ctx.imageSmoothingEnabled = false;                                                 // pixel art: giữ cạnh sắc
  ctx.drawImage(lightningStrikeImg, sx, sy, F.frameWidth, F.frameHeight,
    Math.round(x - F.anchorX * k), Math.round(groundY - F.anchorY * k), F.frameWidth * k, F.frameHeight * k);
  ctx.restore();
  return true;
}
if (typeof Image !== 'undefined') lightningStrikeReady();                         // tải sẵn để tia sét đầu tiên đã có sheet
