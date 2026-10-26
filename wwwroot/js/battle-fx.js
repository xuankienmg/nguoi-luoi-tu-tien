/* Vấn Đạo · js/battle-fx.js: trận pixel: lớp nền, đòn quái, chiêu, hiệu ứng nét cao. Tách từ index.html (tools/split_index.py). */
// ===== Trận đánh với yêu lang, đồng bộ với thời gian mỗi chuyến thám hiểm =====
const ease = k => 1 - Math.pow(1 - k, 3);
let battleDone = {};      // đánh dấu đòn đã tung để chỉ sinh hiệu ứng một lần
// =====================================================================
// Trận đánh PIXEL ART (theo concept): vẽ ở độ phân giải thấp rồi phóng to
// không làm mịn → cạnh cứng, sprite 48×48, bảng màu giới hạn.
// Tu sĩ bên trái, quái bên phải; vệt kiếm khí, số sát thương, thanh máu,
// vòng chọn mục tiêu dưới quái.
// =====================================================================
const PX = {sky0:'#091110', sky1:'#0d1916', far:'#11241e', mid:'#173026', near:'#1e3e2e', leaf:'#285038',
  trunk:'#241e1b', trunk2:'#342a24', ground:'#18241c', ground2:'#223224', path:'#303c2c', path2:'#3e4a36',
  mist:'#5c7c70', cyan:'#5cd6c6', cyan2:'#c4fff4', white:'#ffffff', red:'#d44c40', red2:'#782420',
  gold:'#e8c468', hp:'#60c46c', hp2:'#306e3a', out:'#0a0c0e', shadow:'#080c0a', dmg:'#ffd678', hurt:'#f0a060'};
const pix = document.createElement('canvas'), pctx = pix.getContext('2d');
const P = (x, y, c) => { pctx.fillStyle = c; pctx.fillRect(x|0, y|0, 1, 1); };
const R = (x, y, w, h, c) => { pctx.fillStyle = c; pctx.fillRect(x|0, y|0, w|0, h|0); };
let pixLayers = null, pixKey = '', pfx = [], pixMotes = [], ghostHp = 1, lastMHp = 1;
const img48 = {};
// kích thước sprite: dải taskbar 24px, bản lớn 48px (trên lưới gấp đôi) → nhân vật chiếm một nửa so với trước
const SPR = () => MODE === 'strip' ? 24 : 48;
function sprite48(src){                       // img/hero_luyenkhi.png → img/hero_luyenkhi_24.png / _48.png
  const z = SPR(), s = src.replace(/\.png$/, `_${z}.png`);
  if (!img48[s]){ const im = new Image(); im.src = s; img48[s] = {im, flash:null, burn:null}; }
  const o = img48[s];
  if (o.im.complete && o.im.naturalWidth && !o.flash){
    // bản nháy sáng khi trúng đòn: sáng lên nhưng giữ viền tối để không mất hình
    const c = document.createElement('canvas'); c.width = c.height = z;
    const x = c.getContext('2d'); x.drawImage(o.im, 0, 0);
    const d = x.getImageData(0, 0, z, z), a = d.data;
    for (let i = 0; i < a.length; i += 4) if (a[i+3] && a[i] + a[i+1] + a[i+2] > 60){ a[i] += (255-a[i])*.55; a[i+1] += (255-a[i+1])*.55; a[i+2] += (255-a[i+2])*.55; }
    x.putImageData(d, 0, 0); o.flash = c;
  }
  if (o.im.complete && o.im.naturalWidth && !o.burn){
    // bản bị thiêu đốt: ám đỏ cam
    const c = document.createElement('canvas'); c.width = c.height = z;
    const x = c.getContext('2d'); x.drawImage(o.im, 0, 0);
    const d = x.getImageData(0, 0, z, z), a = d.data;
    for (let i = 0; i < a.length; i += 4) if (a[i+3]){ a[i] += (255-a[i])*.28; a[i+1] += (90-a[i+1])*.28; a[i+2] += (30-a[i+2])*.28; }
    x.putImageData(d, 0, 0); o.burn = c;
  }
  return o.im.complete && o.im.naturalWidth ? o : null;
}
// quái bản nét (img/x.png → img/x_hd.png, 240×240 cùng bố cục với _48): vẽ ở độ phân giải thật như tu sĩ,
// không phóng to từ lưới pixel. Chưa có / lỗi tải → dùng sprite pixel cũ.
const imgHD = {};
function monHD(src){
  const s = src.replace(/\.png$/, '_hd.png');
  if (!imgHD[s]){ const im = new Image(); const o = imgHD[s] = {im, flash:null, burn:null, bad:false}; im.onerror = () => { o.bad = true; }; im.src = s; }
  const o = imgHD[s];
  if (o.bad || !o.im.complete || !o.im.naturalWidth) return null;
  if (!o.flash){
    const tint = f => { const c = document.createElement('canvas'); c.width = o.im.naturalWidth; c.height = o.im.naturalHeight;
      const x = c.getContext('2d'); x.drawImage(o.im, 0, 0); const d = x.getImageData(0, 0, c.width, c.height), a = d.data;
      for (let i = 0; i < a.length; i += 4) if (a[i+3]) f(a, i); x.putImageData(d, 0, 0); return c; };
    o.flash = tint((a, i) => { if (a[i] + a[i+1] + a[i+2] > 60){ a[i] += (255-a[i])*.55; a[i+1] += (255-a[i+1])*.55; a[i+2] += (255-a[i+2])*.55; } });
    o.burn = tint((a, i) => { a[i] += (255-a[i])*.28; a[i+1] += (90-a[i+1])*.28; a[i+2] += (30-a[i+2])*.28; });
  }
  return o;
}
// lưới pixel: chiều cao cố định, chiều ngang theo tỉ lệ khung
function pixGrid(){
  const h = MODE === 'strip' ? 64 : 320;
  return {w: Math.max(80, Math.round(h * cv.width / cv.height)), h};
}
// rặng thông vẽ theo từng hàng pixel (không khử răng cưa)
function pine(c, cx, base, hgt, ww, col){
  c.fillStyle = col;
  for (let tier = 0; tier < 4; tier++){
    const top = base - hgt + Math.round(tier*hgt/4), th = Math.max(2, Math.round(hgt/3)), half = ww*(tier+2)/5;
    for (let y = 0; y < th; y++){ const hw = Math.round(half*y/th); c.fillRect(cx-hw, top+y, hw*2+1, 1); }
  }
}
const hash = n => { const x = Math.sin(n*127.1)*43758.5; return x - Math.floor(x); };
/** Tiên sơn ban đêm: các lớp được cache theo kích thước, không sinh lại mỗi frame. */
function buildPixLayers(w, h){
  const g=Math.round(h*.80);
  const mk=(cw,ch)=>{const c=document.createElement('canvas');c.width=cw;c.height=ch;return[c,c.getContext('2d')]};
  const [bg,b]=mk(w,h);
  b.fillStyle='#0a1423';b.fillRect(0,0,w,h);
  b.fillStyle='#101f30';b.fillRect(0,Math.round(h*.2),w,g);
  b.fillStyle='#182d3b';b.fillRect(0,Math.round(h*.43),w,g);
  // Sao nhỏ, vị trí cố định; không nhấp nháy gây nhiễu thanh máu.
  for(let i=0;i<Math.min(45,Math.round(w*h/800));i++){
    const x=Math.floor(hash(i*17+3)*w),y=Math.floor(hash(i*23+8)*h*.4);
    b.fillStyle=i%4?'#536c82':'#92a4af';b.fillRect(x,y,1,1);
  }
  const mx=Math.round(w*.79),my=Math.round(h*.19),mr=Math.max(3,Math.round(h*.065));
  // Trăng kẻ từng hàng để giữ cạnh pixel, không khử răng cưa.
  for(let y=-mr;y<=mr;y++){
    const half=Math.floor(Math.sqrt(mr*mr-y*y));
    b.fillStyle='#a8b9bb';b.fillRect(mx-half,my+y,half*2+1,1);
  }
  b.fillStyle='#8c9fa6';b.fillRect(mx-Math.max(1,mr>>2),my-1,Math.max(1,mr>>1),2);
  const mountain=(spacing,n,base,height,color,edge)=>{
    const tile=spacing*n,[c,x]=mk(w+tile,h);
    for(let px=0;px<w+tile;px++){
      const j=Math.floor(px/spacing)%n,k=(px%spacing)/spacing;
      const a=hash(j*13+base),next=hash(((j+1)%n)*13+base);
      const ridge=(a*(1-k)+next*k)*.5+Math.sin(k*Math.PI)*(.35+hash(j+31)*.3);
      const top=Math.round(base-height*ridge);
      x.fillStyle=color;x.fillRect(px,top,1,g-top);
      if(edge&&px%3!==0){x.fillStyle=edge;x.fillRect(px,top,1,1)}
    }
    return{c,tile};
  };
  const far=mountain(Math.max(18,Math.round(h*.24)),7,Math.round(h*.61),Math.round(h*.49),'#20394b','#304b5b');
  const mid=mountain(Math.max(24,Math.round(h*.32)),6,Math.round(h*.79),Math.round(h*.42),'#152d39',null);
  // Bóng đình cổ ở rìa cảnh; phần giao chiến luôn thoáng.
  const temple=(x,cx,base,size)=>{
    x.fillStyle='#10212c';
    x.fillRect(cx-size*.32,base-size*.65,Math.max(2,size*.64),size*.65);
    for(let tier=0;tier<2;tier++){
      const top=base-size*(.65+tier*.3),half=size*(tier?.38:.55);
      for(let row=0;row<Math.max(2,size*.17);row++){
        const k=row/Math.max(2,size*.17),width=Math.max(1,half*(.25+k*.75));
        x.fillRect(Math.round(cx-width),Math.round(top+row),Math.round(width*2),1);
      }
    }
    x.fillStyle='#9a986f';x.fillRect(Math.round(cx-1),Math.round(base-size*.48),2,Math.max(1,size*.09));
  };
  const midCtx=mid.c.getContext('2d');
  for(let x=Math.round(h*.13);x<w+mid.tile;x+=mid.tile)temple(midCtx,x,Math.round(h*.67),Math.max(8,Math.round(h*.17)));
  // Sân đá tiền cảnh có khe lát phối cảnh; không trôi dưới chân khi đang đánh.
  b.fillStyle='#26363d';b.fillRect(0,g,w,h-g);
  b.fillStyle='#5b6a69';b.fillRect(0,g,w,1);
  b.fillStyle='#162832';b.fillRect(0,g+1,w,Math.max(1,Math.round(h*.012)));
  const step=Math.max(4,Math.round(h*.065));
  for(let row=0,y=g+2;y<h;y+=step,row++){
    b.fillStyle=row%2?'#2c3e44':'#304249';b.fillRect(0,y,w,step-1);
    const slab=Math.max(12,Math.round(h*.24)),offset=(row%2)*slab/2;
    for(let x=-slab+offset;x<w;x+=slab){
      b.fillStyle='#192d35';b.fillRect(Math.round(x),y,1,step);
      b.fillStyle='#405158';b.fillRect(Math.round(x+2),y,Math.max(1,slab-4),1);
    }
  }
  // Vết nứt và rêu mỏng trên sân cổ.
  for(let i=0;i<Math.round(w/10);i++){
    const x=Math.floor(hash(i*11+41)*w),y=g+2+Math.floor(hash(i*7+91)*(h-g-3));
    b.fillStyle=i%3?'#23353b':'#40554c';b.fillRect(x,y,Math.max(1,Math.round(h*.01)),1);
  }
  const [mist,m]=mk(w,h);
  // Mây ngang mỏng, giữ vùng nhân vật và số sát thương dễ đọc.
  for(const [center,alpha] of [[.49,.13],[.67,.12],[.76,.08]]){
    m.fillStyle='rgba(148,181,186,'+alpha+')';
    const cy=Math.round(h*center),band=Math.max(1,Math.round(h*.012));
    for(let x=0;x<w;x++){
      const y=cy+Math.round(Math.sin(x/Math.max(10,h*.2))*band);
      m.fillRect(x,y,1,band);
    }
  }
  pixMotes=Array.from({length:Math.min(16,Math.max(3,Math.round(w*h/4500)))},(_,i)=>({x:hash(i*19+2)*w,y:h*.2+hash(i*5+7)*h*.45,ph:hash(i*11)*6.28}));
  return{bg,far,mid,mist,g};
}
// phông chữ pixel 3×5 cho số sát thương
const FONT = {'0':'111101101101111','1':'010110010010111','2':'111001111100111','3':'111001111001111','4':'101101111001001',
  '5':'111100111001111','6':'111100111101111','7':'111001010010010','8':'111101111101111','9':'111101111001111','-':'000000111000000','+':'000010111010000','%':'101001010100101'};
function ptext(x, y, s, col, sc = 1){
  const draw = (ox, oy, c) => { let cx = 0; for (const ch of s){ const g = FONT[ch]; if (g) for (let i = 0; i < 15; i++) if (g[i] === '1') R(x + (cx + i%3)*sc + ox, y + Math.floor(i/3)*sc + oy, sc, sc, c); cx += 4; } };
  for (const [ox, oy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1]]) draw(ox, oy, PX.out);
  draw(0, 0, col);
  return s.length*4*sc;
}
// lửa thiêu đốt bám chân quái + tàn lửa bay lên
function pburn(cx, g, half, t, u){
  for (let k = -half; k <= half; k += 2){
    const edge = 1 - Math.abs(k) / (half + 1), hgt = Math.round((2 + (Math.sin(t*13 + k*1.7) + 1) * 2.5) * edge * Math.max(1, u));
    for (let y = 0; y < hgt; y++){ const f = y / Math.max(1, hgt); P(cx + k, g - 1 - y, f < .35 ? FIRE[4] : f < .7 ? FIRE[3] : FIRE[2]); }
  }
  if (Math.random() < .25) pfx.push({type:'spark', x: cx + (Math.random() - .5) * half * 2, y: g - 2 - Math.random() * half, vx: (Math.random() - .5) * 6 * u, vy: (-16 - Math.random() * 12) * u, life: 0, max: .55, col: Math.random() < .5 ? FIRE[3] : FIRE[4]});
}
// thanh nộ mảnh 1px, ghép sát dưới thanh máu (dùng chung viền)
function pbarThin(cx, y, w, v, col){
  const x = Math.round(cx - w/2);
  R(x-1, y-1, w+2, 3, PX.out); R(x, y, w, 1, '#16202a');
  if (v > 0) R(x, y, Math.max(1, Math.round(w*v)), 1, col);
}
function pbar(cx, y, w, v, ghost, col, col2){
  const x = Math.round(cx - w/2);
  R(x-1, y-1, w+2, 4, PX.out); R(x, y, w, 2, '#16202a');
  if (ghost > v) R(x, y, Math.round(w*ghost), 2, PX.white);
  if (v > 0){ R(x, y, Math.max(1, Math.round(w*v)), 2, col); R(x, y+1, Math.max(1, Math.round(w*v)), 1, col2); }
}
// vệt kiếm khí lưỡi liềm: lõi trắng, viền xanh linh khí, hai đầu thon
function pslash(cx, cy, r, a0, a1, prog, fade){
  const end = a0 + (a1 - a0)*prog, steps = Math.max(30, r*5);
  for (let i = 0; i <= steps; i++){
    const k = i/steps, a = a0 + (end - a0)*k, th = Math.max(1, Math.round(3*Math.sin(Math.PI*k)*fade));
    for (let t = -1; t <= th; t++){
      const X = Math.round(cx + Math.cos(a)*(r+t)), Y = Math.round(cy + Math.sin(a)*(r+t));
      P(X, Y, t === -1 || t === th ? PX.cyan : (t < th-1 ? PX.white : PX.cyan2));
    }
  }
}
// ---- đòn phản công của quái: mỗi loài một kiểu ----
// k: 0→1 trong pha phản đòn (trúng đích ở k≈.32, ứng với s=.85). Trả về vị trí quái + màu hiệu ứng khi trúng;
// hiệu ứng phụ (đạn, vệt, khói) đẩy vào o.fxAfter để vẽ sau sprite.
const MON_STYLE = {quai_yeu_lang:'pounce', quai_doc_xa:'spit', quai_thach_tru:'charge', quai_bang_ho:'frost', quai_pha_son_vien:'slam',
  quai_tinh_thach_tich:'frost', quai_huyen_o:'charge', quai_ma_dang:'spit', quai_cot_khuyen:'blink', quai_loi_ung:'charge',
  tinh_thiet_giap_ho:'pounce', tinh_cot_tuong:'charge', tinh_hac_mang:'spit',
  boss_tu_ty_ma_vien:'slam', boss_hac_giao:'foxfire', boss_bang_phuong:'frost',
  quai_reu_thach_nhan:'slam', quai_doc_chu_nu:'spit', quai_cuu_vi_ho:'foxfire', quai_thiet_giap_binh:'charge',
  tinh_huyet_y_kiem_khach:'pounce', tinh_cot_long:'breath', tinh_loi_ky_lan:'charge', tinh_kiem_hon:'blink',
  boss_dao_ngot:'charge', boss_cung_ky:'pounce', boss_hon_don:'slam',
  boss_am_ma:'breath', boss_kiem_de:'blink', boss_thao_thiet:'breath', boss_cung_ky_vuong:'pounce'};
// bản đổi màu (_v1 Huyết · _v2 U Minh · _v3 Kim · _v4 Ám) đánh như loài gốc
const monStyleOf = src => MON_STYLE[(src || '').replace(/^.*\/|\.png$/g, '').replace(/_v\d$/, '')] || 'pounce';
const HIT = .32, lerp = (a, b, k) => a + (b - a) * k, clamp01 = k => Math.max(0, Math.min(1, k));
function pline(x0, y0, x1, y1, col, w = 1){                       // đoạn thẳng pixel
  const n = Math.max(1, Math.ceil(Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0))));
  for (let j = 0; j <= n; j++){ const x = lerp(x0, x1, j / n), y = lerp(y0, y1, j / n); for (let d = 0; d < w; d++) P(x, y + d, col); }
}
function pblob(x, y, r, c1, c2){                                  // cục tròn 2 màu (đạn, cầu lửa)
  for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++){ const d = dx*dx + dy*dy; if (d <= r*r) P(x + dx, y + dy, d <= (r-1)*(r-1) ? c2 : c1); }
}
function monAttack(style, k, o){
  const {g, sz, U, t} = o, fx = fn => o.fxAfter.push(fn);
  const big = Math.max(0, Math.round((sz - 48 * o.u) / 2));                      // Boss to gấp đôi: dừng xa hơn, miệng cao hơn
  const heroX = o.heroX + big, wolfX = o.wolfX;               // heroX: mốc dừng khi áp sát (lùi ra theo cỡ quái)
  const chest = [o.heroX + U(6), g - U(24)], mouth = [wolfX - U(16) - big, g - U(22) - big];
  switch (style){
    case 'spit': {                                                 // rắn độc: ngóc lên, phun nọc xanh
      const back = clamp01(k / .2);
      const r = {wx: Math.round(wolfX + U(4) * Math.sin(back * Math.PI / 2) * (k < HIT ? 1 : 1 - clamp01((k - HIT) / .3))), wy: -Math.round(U(3) * Math.sin(clamp01(k / HIT) * Math.PI)),
        numCol: '#a6e868', sparks: ['#7ad04a', '#c8f080', '#3e8a2a'], nSparks: 12, label: 'Trúng độc'};
      if (k >= .18 && k < HIT){ const q = (k - .18) / (HIT - .18);
        fx(() => { const x = lerp(mouth[0], chest[0], q), y = lerp(mouth[1], chest[1], q) - Math.sin(q * Math.PI) * U(6);
          pblob(x, y, Math.max(1, U(2)), '#3e8a2a', '#9be05a'); P(x + U(3), y + 1, '#7ad04a'); P(x + U(5), y, '#3e8a2a'); }); }
      if (k >= HIT && k < .7) fx(() => { const q = (k - HIT) / .38;                                   // nọc nhỏ giọt trên người
        for (let n = 0; n < 4; n++) P(chest[0] - U(4) + n * U(3), chest[1] + U(2) + q * U(10) * ((n % 2) + 1), n % 2 ? '#7ad04a' : '#3e8a2a'); });
      return r;
    }
    case 'slam': {                                                 // rùa đá: nhảy lên dậm đất, đá vỡ chạy tới
      const up = k < .2 ? Math.sin(k / .2 * Math.PI / 2) : k < .27 ? 1 - (k - .2) / .07 : 0;
      const r = {wy: -Math.round(U(12) * up), wx: k >= .27 && k < .35 ? wolfX + (Math.floor(t * 40) % 2 ? 1 : -1) : wolfX,
        knock: 3, numCol: '#e0b878', sparks: ['#8a6a48', '#b89060', '#5a4632'], nSparks: 12, spread: 70};
      if (k >= .27 && k < .6) fx(() => { const q = clamp01((k - .27) / (HIT - .27 + .02));
        const x1 = lerp(wolfX - U(12), heroX + U(4), q);
        for (let x = wolfX - U(12); x >= x1; x -= U(3)){ const h = Math.round(U(2) + (Math.sin(x * 1.7) + 1) * U(2)) * (1 - clamp01((k - HIT) / .28));
          for (let y = 0; y < h; y++) P(x, g - y, y === h - 1 ? '#b89060' : '#6a5038'); } });
      return r;
    }
    case 'foxfire': {                                              // hồ lửa: bật nhẹ, phóng 3 quả hồ hỏa vòng cung
      const r = {wy: -Math.round(U(4) * Math.sin(clamp01(k / .25) * Math.PI)), numCol: '#ffb060', sparks: FIRE.slice(2), nSparks: 14, label: 'Hồ hỏa'};
      for (let n = 0; n < 3; n++){ const k0 = .04 + n * .07, q = (k - k0) / (HIT - k0 + n * .04);
        if (q >= 0 && q < 1) fx(() => { const x = lerp(wolfX - U(10), chest[0], q), y = lerp(g - U(30), chest[1], q) - Math.sin(q * Math.PI) * U(14 + n * 4);
          pblob(x, y, Math.max(1, U(2)), FIRE[2], FIRE[4]); P(x + U(3), y + 1, FIRE[3]); P(x + U(5), y + 2, FIRE[1]); }); }
      return r;
    }
    case 'frost': {                                                // sói băng: lao thẳng như tên, để lại vệt băng
      const go = k < HIT ? ease(k / HIT) : 1 - ease(clamp01((k - HIT) / .5));
      const x = Math.round(lerp(wolfX, heroX + U(24), go));
      const r = {wx: x, wy: 0, numCol: '#bfe8ff', sparks: ['#ffffff', '#bfe8ff', '#6cc0e8'], nSparks: 16, spread: 60, label: 'Băng hàn'};
      if (k < .75) fx(() => { const fade = 1 - clamp01((k - HIT) / .4);
        for (let xx = x + U(10); xx < wolfX + U(8); xx += U(4)) if (Math.random() < .6 * fade + .2) P(xx, g - 1 - (xx % 3), xx % 2 ? '#bfe8ff' : '#6cc0e8'); });
      if (k >= HIT && k < .55) fx(() => { const q = (k - HIT) / .23;                                  // gai băng bung ra
        for (let n = 0; n < 6; n++){ const a = -Math.PI / 2 + (n - 2.5) * .45, L = U(4) + q * U(8);
          pline(chest[0], chest[1], chest[0] + Math.cos(a) * L, chest[1] + Math.sin(a) * L, n % 2 ? '#ffffff' : '#9fd8f8'); } });
      return r;
    }
    case 'charge': {                                               // bọ sắt: lùi lấy đà, húc thẳng sát đất
      const go = k < .12 ? -clamp01(k / .12) * .15 : k < HIT ? ease((k - .12) / (HIT - .12)) : 1 - ease(clamp01((k - HIT) / .5));
      const shakeX = k < .12 && Math.floor(t * 40) % 2 ? 1 : 0;
      const r = {wx: Math.round(lerp(wolfX, heroX + U(20), go)) + shakeX, wy: 0, knock: 5, numCol: '#ffe080', sparks: ['#ffffff', '#ffe080', '#c0c0c0', '#e8a040'], nSparks: 18, spread: 90};
      if (k >= .12 && k < HIT + .05 && Math.random() < .7)
        pfx.push({type:'dust', x: r.wx + U(10), y: g - 1, vx: 20 + Math.random() * 20, vy: -6, life: 0, max: .45});
      return r;
    }
    case 'blink': {                                                // chó xương: tan vào khói, hiện ra sát bên cắn
      let wx = wolfX, wAlpha = 1;
      if (k < .12) wAlpha = 1 - k / .12;
      else if (k < HIT){ wx = heroX + U(22); wAlpha = (k - .12) / (HIT - .12); }
      else if (k < .6){ wx = heroX + U(22) - (k < .42 ? U(3) : 0); }
      else if (k < .78){ wx = heroX + U(22); wAlpha = 1 - (k - .6) / .18; }
      else wAlpha = (k - .78) / .22;
      const smoke = (cx) => fx(() => { for (let n = 0; n < 14; n++){ const a = n * 2.4 + t * 3, rr = U(6) + (n % 4) * U(3);
        P(cx + Math.cos(a) * rr, g - U(14) + Math.sin(a) * rr * .7 - (t * 20 + n * 3) % U(10), n % 3 ? '#3a2d4a' : '#7a6a96'); } });
      if (k < .2 || k > .7) smoke(wolfX);
      if (k > .05 && k < .4) smoke(heroX + U(22));
      return {wx: Math.round(wx), wy: 0, wAlpha: Math.max(.05, Math.min(1, wAlpha)), numCol: '#c8b8f0', sparks: ['#7a6a96', '#d8d0c0', '#3a2d4a'], nSparks: 10, label: 'Cắn xé'};
    }
    case 'breath': {                                               // xà vương: ngóc đầu, phun luồng hắc khí
      const r = {wy: -Math.round(U(4) * Math.sin(clamp01(k / .5) * Math.PI)), wx: wolfX + Math.round(U(2) * clamp01(k / .15)),
        knock: 4, numCol: '#d8a0ff', sparks: ['#8040c0', '#d8a0ff', '#301848'], nSparks: 16, spread: 60, label: 'Hắc khí'};
      if (k >= .14 && k < .62) fx(() => { const q = clamp01((k - .14) / (HIT - .14)), fade = 1 - clamp01((k - HIT) / .3);
        const x0 = mouth[0], y0 = g - sz + U(16), x1 = lerp(x0, chest[0], q);
        for (let x = x0; x >= x1; x -= 1){ const p = (x0 - x) / Math.max(1, x0 - chest[0]), y = lerp(y0, chest[1], p), wv = Math.sin(x * .6 + t * 30);
          const w = Math.max(1, Math.round((U(2) + p * U(3)) * fade));
          for (let d = -w; d <= w; d++) if (Math.random() < .85) P(x, y + d + wv, Math.abs(d) < w * .4 ? '#d8a0ff' : d % 2 ? '#8040c0' : '#301848'); } });
      return r;
    }
    default: {                                                     // vồ: nhảy vòng cung, cào 3 vệt
      const r = {wx: Math.round(wolfX - (wolfX - heroX - U(26)) * Math.sin(k * Math.PI)), wy: -Math.round(U(5) * Math.sin(k * Math.PI)),
        sparks: [PX.red, '#ff8070'], nSparks: 10};
      if (k >= HIT && k < .6) fx(() => { const q = clamp01((k - HIT) / .12), fade = k < .5;
        for (let n = 0; n < 3; n++){ const x0 = chest[0] - U(5) + n * U(4), y0 = chest[1] - U(8);
          pline(x0 + U(4), y0, lerp(x0 + U(4), x0 - U(3), q), lerp(y0, y0 + U(14), q), fade ? '#ff6050' : PX.red2); } });
      return r;
    }
  }
}
function pring(cx, cy, rx, ry, col, dotted){
  for (let a = 0; a < 360; a += 3){
    if (dotted && Math.floor(a/15) % 2) continue;
    P(Math.round(cx + Math.cos(a*Math.PI/180)*rx), Math.round(cy + Math.sin(a*Math.PI/180)*ry), col);
  }
}

// ---- Hỏa hệ trong trận: khung animation đã tách nền (img/fx_hoa_skills_a.png; sinh lại: tools/cut_skills_alpha.py) ----
// feet: x chân nhân vật trong từng khung (null = khung chỉ có hiệu ứng). Khung có nhân vật được ghim vào chỗ tu sĩ đứng.
// khung chiêu: bản đã bỏ nhân vật cũ vẽ sẵn (tools/clean_skill_hero.py); tu sĩ theo cảnh giới vẽ riêng
const SKB = {src: 'img/fx_hoa_skills_fx.png', fw: 124, fh: 64, charH: 52, feetY: 62,
  feet: [[44,31,28,20,null,45,46,null], [46,25,25,null,null,33,40,null], [48,null,null,null,null,null,null,null], [49,22,25,null,null,36,39,null],
         [49,36,34,null,null,null,null,null], [48,null,null,null,null,null,null,null], [48,65,73,62,63,66,61,null], [50,62,71,66,62,63,63,null]]};
const skbImg = new Image(); skbImg.src = SKB.src;
const SKB_HEAD = [80, 91, 106, 106, 113, 109, 74, 87];             // Hỏa Long Thuật: x đầu rồng / tâm nổ trong từng khung (hàng 3)
/** Chọn chiêu diễn ở thời điểm s của lượt: chiêu công gây nhiều sát thương nhất (s .00–.75), hoặc chiêu thủ ở đầu lượt 0 (s .00–.30). */
function skBattlePlan(casts, s, basic){
  if (s < 0 || !skbImg.complete || !skbImg.naturalWidth) return null;
  const main = casts.filter(c => c.dmg > 0 && SK_FX_ROW[c.skill] !== undefined).sort((a, b) => b.dmg - a.dmg)[0];
  const def = casts.find(c => c.dmg === 0 && SK_FX_ROW[c.skill] !== undefined);
  let row, f, isMain = false;
  if (main && s < .75){ row = SK_FX_ROW[main.skill]; f = Math.min(7, Math.floor(s / .75 * 8)); isMain = true; }
  else if (def && s < .3){ row = SK_FX_ROW[def.skill]; f = Math.min(7, Math.floor(s / .3 * 8)); }
  else return null;
  const onTarget = f > 0 && (row === 2 || row === 5 || (row === 4 && f >= 3));       // Liệt Diễm, Xích Diễm Thiên, Phần Thiên giáng xuống
  let ax = null; for (let i = f; i >= 0 && ax === null; i--) ax = SKB.feet[row][i];
  return {row, f, s, main: isMain, onTarget, ax: ax ?? 46, hideHero: false};          // khung hiệu ứng không còn người: luôn vẽ tu sĩ theo cảnh giới
}
function skBattleDraw(p, o){
  const k = o.sz * o.sy / SKB.charH, {fw, fh} = SKB;
  ctx.globalAlpha = 1;
  if (p.onTarget){
    const k2 = k * (p.row === 5 ? 1.7 : 1.25), tx = (o.wolfX - o.sz * .05) * o.sx, ty = (o.g + 2) * o.sy;
    ctx.drawImage(skbImg, p.f * fw + .5, p.row * fh + .5, fw - 1, fh - 1, tx - 62 * k2, ty - 63 * k2, fw * k2, fh * k2);
  } else {
    ctx.globalAlpha = p.hideHero ? o.hAlpha : 1;
    let dx = 0;
    if (p.row === 3 && p.main && p.f >= 2){                                         // Hỏa Long Thuật: rồng bay tới tận quái
      const head = SKB_HEAD[p.f], target = (o.wolfX - o.sz * .05) * o.sx, x0 = o.fx - p.ax * k;
      const pr = p.f >= 5 ? 1 : Math.max(0, Math.min(1, (p.s - .19) / .28)), e = pr * pr * (3 - 2 * pr);
      dx = Math.max(0, target - (x0 + head * k)) * e;
    }
    ctx.drawImage(skbImg, p.f * fw + .5, p.row * fh + .5, fw - 1, fh - 1, o.fx - p.ax * k + dx, o.fy - SKB.feetY * k, fw * k, fh * k);
  }
  ctx.globalAlpha = 1;
}
/** Hiệu ứng nét cao cho đòn phản công của quái. k: 0→1 trong pha phản đòn, trúng tu sĩ ở k = HIT (.32).
 *  o: mx,my tâm quái · mouthX/Y miệng · cx,cy ngực tu sĩ · gy mặt đất · H chiều cao tu sĩ (CSS px). */
function monFx(style, k, o){
  const {mx, my, mouthX, mouthY, cx, cy, gy, H, t} = o, cl = clamp01, e = x => x * x * (3 - 2 * x);
  const rnd = n => { const v = Math.sin((n + o.seed * 13.37) * 127.1 + 311.7) * 43758.5453; return v - Math.floor(v); };
  const after = cl((k - HIT) / (1 - HIT)), hit = k >= HIT;
  const glow = (x, y, r, c0, c1, a = 1) => { const g = ctx.createRadialGradient(x, y, 0, x, y, r); g.addColorStop(0, c0); g.addColorStop(1, c1);
    ctx.globalAlpha = a; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); };
  const burst = (x, y, n, R, cols, q, sz = 2) => { for (let i = 0; i < n; i++){ const a = rnd(i) * Math.PI * 2, d = R * (.3 + .7 * rnd(i + 50)) * e(q);
    ctx.globalAlpha = 1 - q; ctx.fillStyle = cols[i % cols.length]; ctx.fillRect(x + Math.cos(a) * d - sz / 2, y + Math.sin(a) * d * .8 - sz / 2, sz, sz); } };
  const stroke = (pts, w, col, a) => { ctx.globalAlpha = a; ctx.strokeStyle = col; ctx.lineWidth = w; ctx.beginPath(); pts.forEach(([x, y], i) => i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)); ctx.stroke(); };
  const arcPt = (q, lift) => [mouthX + (cx - mouthX) * q, mouthY + (cy - mouthY) * q - Math.sin(q * Math.PI) * lift];
  const px = Math.max(1.5, H / 40);
  ctx.save(); ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  switch (style){
    case 'spit': {                                                   // rắn độc: cục nọc bay vòng cung, vỡ tung, vũng độc dưới chân
      ctx.globalCompositeOperation = 'lighter';
      if (k > .08 && !hit){ const q = e(cl((k - .08) / (HIT - .08))), [x, y] = arcPt(q, H * .45);
        for (let j = 1; j <= 6; j++){ const [tx, ty] = arcPt(Math.max(0, q - j * .05), H * .45); glow(tx, ty, px * (3 - j * .35), 'rgba(170,255,120,.8)', 'rgba(60,160,40,0)', .6 - j * .08); }
        glow(x, y, px * 6, 'rgba(220,255,170,1)', 'rgba(70,200,60,0)'); }
      if (hit && after < .7){ const q = after / .7; burst(cx, cy, 18, H * .7, ['#b8ff7a', '#5fd04a', '#e8ffd0'], q, px * 1.6); glow(cx, cy, H * .5 * (1 - q * .5), 'rgba(140,255,90,.55)', 'rgba(40,140,30,0)', 1 - q); }
      ctx.globalCompositeOperation = 'source-over';
      if (hit){ const a = 1 - after; ctx.globalAlpha = .55 * a; ctx.fillStyle = '#3f8f2a'; ctx.beginPath(); ctx.ellipse(cx, gy, H * (.25 + .25 * e(cl(after * 3))), H * .07, 0, 0, Math.PI * 2); ctx.fill();
        for (let i = 0; i < 4; i++){ const p = (after * 2 + rnd(i + 20)) % 1; ctx.globalAlpha = a * (1 - p); ctx.fillStyle = '#9cf06a'; ctx.beginPath(); ctx.arc(cx + (rnd(i + 30) - .5) * H * .4, gy - p * H * .3, px * (1 + p), 0, Math.PI * 2); ctx.fill(); } }
      break;
    }
    case 'slam': {                                                   // dậm đất: chấn động, đường nứt chạy tới, đá văng
      if (k > .22){ const q = cl((k - .22) / .5), fx = mx;
        ctx.globalAlpha = (1 - q) * .9; ctx.strokeStyle = '#e8d2a0'; ctx.lineWidth = px * 1.5 * (1 - q);
        ctx.beginPath(); ctx.ellipse(fx, gy, H * (.3 + 1.2 * e(q)), H * (.08 + .25 * e(q)), 0, 0, Math.PI * 2); ctx.stroke();
        const len = e(cl((k - .22) / .2)), pts = [[fx - o.mw * .3, gy]];                // đường nứt
        for (let i = 1; i <= 10; i++){ const qq = i / 10 * len; pts.push([fx - o.mw * .3 + (cx - fx + o.mw * .3) * qq, gy + (rnd(i) - .5) * px * 3]); }
        stroke(pts, px * 2.2, '#2a1c10', 1 - after * .8); stroke(pts, px, '#ffb860', (1 - after) * .9);
        for (let i = 0; i < 9; i++){ const sp = cl((k - .22 - rnd(i) * .1) / .45); if (sp <= 0 || sp >= 1) continue;   // đá văng
          const x = fx + (cx - fx) * sp * (.6 + .5 * rnd(i + 5)), y = gy - Math.sin(sp * Math.PI) * H * (.3 + .4 * rnd(i + 9)), z = px * (2 + 2 * rnd(i + 3));
          ctx.globalAlpha = 1 - sp * .4; ctx.fillStyle = i % 2 ? '#7a6248' : '#a8906a'; ctx.save(); ctx.translate(x, y); ctx.rotate(sp * 6 + i); ctx.fillRect(-z / 2, -z / 2, z, z); ctx.restore(); }
        for (let i = 0; i < 6; i++){ const p = cl(q * 1.3 - rnd(i) * .2); ctx.globalAlpha = (1 - p) * .45; ctx.fillStyle = '#c8b08a';   // bụi
          ctx.beginPath(); ctx.arc(fx + (rnd(i + 40) - .5) * o.mw * 1.4, gy - p * H * .25, H * .08 * (1 + p * 2), 0, Math.PI * 2); ctx.fill(); }
      }
      break;
    }
    case 'foxfire': {                                                // ma hỏa: 3 quả cầu lửa tím lượn tới, nổ tung
      ctx.globalCompositeOperation = 'lighter';
      for (let j = 0; j < 3; j++){
        const st = .04 + j * .07, q = cl((k - st) / (HIT + .04 - st));
        if (q > 0 && q < 1){ const qq = e(q), x = mouthX + (cx - mouthX) * qq, y = mouthY + (cy - mouthY) * qq - Math.sin(qq * Math.PI) * H * (.35 + j * .2) * (j % 2 ? -1 : 1);
          for (let tr = 1; tr <= 5; tr++){ const q2 = e(Math.max(0, q - tr * .04)), x2 = mouthX + (cx - mouthX) * q2, y2 = mouthY + (cy - mouthY) * q2 - Math.sin(q2 * Math.PI) * H * (.35 + j * .2) * (j % 2 ? -1 : 1);
            glow(x2, y2, px * (4 - tr * .5), 'rgba(210,140,255,.7)', 'rgba(120,40,200,0)', .5 - tr * .08); }
          glow(x, y, px * 6 * (1 + .15 * Math.sin(t * 30 + j)), 'rgba(255,230,255,1)', 'rgba(150,60,255,0)'); }
        const hq = cl((k - (HIT + .04 + j * .03)) / .3);
        if (hq > 0 && hq < 1){ glow(cx + (j - 1) * H * .12, cy + (j - 1) * H * .1, H * .35 * e(hq), 'rgba(240,200,255,.9)', 'rgba(130,50,230,0)', 1 - hq);
          burst(cx, cy, 10, H * .5, ['#f0c8ff', '#b070ff'], hq, px * 1.4); }
      }
      ctx.globalCompositeOperation = 'source-over';
      break;
    }
    case 'frost': {                                                  // lao băng: vệt băng dưới đất, băng nổ, vòng sương giá
      if (k > .05){ const q = cl((k - .05) / (HIT - .05));
        for (let i = 0; i < 9; i++){ const pos = i / 8; if (pos > q) break; const x = mx + (cx - mx) * pos * .9, h = px * (3 + 4 * rnd(i)) * (1 - after * .8);   // gai băng mọc theo đường lao
          ctx.globalAlpha = .9 * (1 - after); ctx.fillStyle = i % 2 ? '#bfefff' : '#7fd0f0';
          ctx.beginPath(); ctx.moveTo(x - px * 1.5, gy); ctx.lineTo(x, gy - h); ctx.lineTo(x + px * 1.5, gy); ctx.fill(); } }
      if (hit && after < .8){ const q = after / .8;
        ctx.globalCompositeOperation = 'lighter';
        glow(cx, cy, H * .55 * (1 - q * .3), 'rgba(220,250,255,.85)', 'rgba(80,180,255,0)', 1 - q);
        for (let i = 0; i < 8; i++){ const a = i / 8 * Math.PI * 2 + rnd(i), d = H * (.15 + .45 * e(q)), L = px * 5 * (1 - q * .5);   // mảnh băng bắn ra
          const x = cx + Math.cos(a) * d, y = cy + Math.sin(a) * d * .8;
          stroke([[x - Math.cos(a) * L, y - Math.sin(a) * L], [x, y]], px * 1.5, '#e8fbff', 1 - q); }
        ctx.globalCompositeOperation = 'source-over';
        ctx.globalAlpha = .7 * (1 - q); ctx.strokeStyle = '#bfefff'; ctx.lineWidth = px;
        ctx.beginPath(); ctx.ellipse(cx, gy, H * (.2 + .5 * e(q)), H * (.05 + .1 * e(q)), 0, 0, Math.PI * 2); ctx.stroke(); }
      break;
    }
    case 'charge': {                                                 // húc: bụi tung sau lưng, vệt tốc độ, chấn động khi trúng
      if (k > .1 && k < HIT + .1){ const q = cl((k - .1) / (HIT - .1));
        for (let i = 0; i < 5; i++){ const y = my + (rnd(i) - .5) * H * .6, L = H * (.4 + .5 * rnd(i + 7));       // vệt tốc độ
          stroke([[mx + o.mw * .3, y], [mx + o.mw * .3 + L, y]], px * .8, '#fff', .5 * (1 - q * .3)); }
        for (let i = 0; i < 6; i++){ const p = cl(q * 1.4 - rnd(i) * .4); ctx.globalAlpha = (1 - p) * .5; ctx.fillStyle = '#b8a888';
          ctx.beginPath(); ctx.arc(mx + o.mw * .4 + p * H * .5, gy - px * 2 - rnd(i + 3) * H * .15, H * .07 * (1 + p), 0, Math.PI * 2); ctx.fill(); } }
      if (hit && after < .55){ const q = after / .55;
        ctx.globalCompositeOperation = 'lighter';
        glow(cx + H * .1, cy, H * .45 * e(cl(q * 2)), 'rgba(255,250,220,.95)', 'rgba(255,190,80,0)', 1 - q);
        for (let i = 0; i < 10; i++){ const a = i / 10 * Math.PI * 2, r0 = H * (.12 + .3 * q), r1 = r0 + H * .18 * (1 - q);   // tia chấn động
          stroke([[cx + H * .1 + Math.cos(a) * r0, cy + Math.sin(a) * r0], [cx + H * .1 + Math.cos(a) * r1, cy + Math.sin(a) * r1]], px * 1.4, '#fff4c8', 1 - q); }
        ctx.globalCompositeOperation = 'source-over'; }
      break;
    }
    case 'blink': {                                                  // hóa khói: khói đen chỗ cũ, hiện ra sát bên cắn (hàm răng khép lại)
      const puff = (x, y, q) => { for (let i = 0; i < 7; i++){ const a = rnd(i + 70) * Math.PI * 2, d = H * .25 * e(q);
        ctx.globalAlpha = (1 - q) * .6; ctx.fillStyle = i % 2 ? '#3a3448' : '#5a5070'; ctx.beginPath(); ctx.arc(x + Math.cos(a) * d, y + Math.sin(a) * d * .7, H * .12 * (1 + q), 0, Math.PI * 2); ctx.fill(); } };
      if (k > .05 && k < .4) puff(o.mx, my, cl((k - .05) / .35));
      if (k > .18 && k < .55) puff(cx + H * .3, cy, cl((k - .18) / .37));
      if (k > HIT - .08 && k < HIT + .3){ const q = cl((k - HIT + .08) / .14), f = cl((k - HIT) / .3), gap = H * .32 * (1 - e(q));   // hàm răng
        ctx.globalAlpha = 1 - f;
        for (const sgn of [-1, 1]){ const y0 = cy + sgn * gap;
          ctx.strokeStyle = '#d8d0c0'; ctx.lineWidth = px * 1.4; ctx.beginPath(); ctx.arc(cx, y0 - sgn * H * .1, H * .22, sgn < 0 ? .15 * Math.PI : 1.15 * Math.PI, sgn < 0 ? .85 * Math.PI : 1.85 * Math.PI); ctx.stroke();
          ctx.fillStyle = '#fff';
          for (let i = 0; i < 5; i++){ const x = cx - H * .16 + i * H * .08; ctx.beginPath(); ctx.moveTo(x - px * 1.5, y0); ctx.lineTo(x, y0 - sgn * px * 4.5); ctx.lineTo(x + px * 1.5, y0); ctx.fill(); } }
        if (hit) burst(cx, cy, 10, H * .45, ['#ff5a50', '#ffb0a0'], f, px * 1.5); }
      break;
    }
    case 'breath': {                                                 // phun hắc khí: luồng khói tím đen hình nón, xoáy quanh tu sĩ
      if (k > .1 && k < .8){ const q = cl((k - .1) / (HIT - .05)), out = cl((k - .55) / .25);
        for (let i = 0; i < 26; i++){ const p = ((t * 1.6 + rnd(i)) % 1) * q, x = mouthX + (cx - mouthX) * p, y = mouthY + (cy - mouthY) * p + (rnd(i + 9) - .5) * H * .55 * p + Math.sin(t * 8 + i) * px * 2;
          ctx.globalAlpha = (1 - out) * (.25 + .4 * (1 - p)); ctx.fillStyle = i % 3 ? '#2a1838' : '#7a4aa8';
          ctx.beginPath(); ctx.arc(x, y, H * (.04 + .1 * p), 0, Math.PI * 2); ctx.fill(); }
        if (hit){ ctx.globalCompositeOperation = 'lighter'; glow(cx, cy, H * .5, 'rgba(170,90,230,.5)', 'rgba(60,20,90,0)', 1 - after); ctx.globalCompositeOperation = 'source-over'; } }
      break;
    }
    default: {                                                       // vồ: 3 vệt cào sáng đỏ chém qua người
      if (k > HIT - .04 && after < .6){ const q = cl((k - HIT + .04) / .1), f = cl(after / .6);
        for (let n = 0; n < 3; n++){ const ox = (n - 1) * H * .12, pts = [];
          for (let j = 0; j <= 8; j++){ const qq = j / 8 * q; pts.push([cx + ox + H * .22 - qq * H * .44, cy - H * .32 + qq * H * .6 + Math.sin(qq * Math.PI) * H * .06]); }
          ctx.globalCompositeOperation = 'lighter'; stroke(pts, px * 4, '#ff3a30', .35 * (1 - f)); ctx.globalCompositeOperation = 'source-over';
          stroke(pts, px * 1.4, '#fff0e8', 1 - f); }
        burst(cx, cy, 12, H * .5, ['#ff6050', '#ffd0c0'], f, px * 1.5); }
    }
  }
  ctx.restore();
}
/** Tuyệt kỹ Xích Diễm Thiên (lửa lam): hiệu ứng vẽ thêm theo nhịp lượt s (−1 = không diễn: trả màn hình về chỗ). */
let ultShaking = false;
function ultFx(s, o){
  const cv = ctx.canvas;
  const shake = s >= .36 && s < .62 && !reduce;
  if (shake){ const a = 4 * (1 - (s - .36) / .26); cv.style.transform = `translate(${(Math.random() - .5) * 2 * a}px,${(Math.random() - .5) * 2 * a}px)`; ultShaking = true; }
  else if (ultShaking){ cv.style.transform = ''; ultShaking = false; }
  if (s < 0) return;
  const {fx, fy, sz, sx, sy, g, wolfX, t} = o, H = sz * sy, mx = wolfX * sx, my = (g - sz * .45) * sy, gy = (g + 1) * sy;
  const W = cv.clientWidth || cv.width, Hc = cv.clientHeight || cv.height;
  const cl = x => Math.max(0, Math.min(1, x)), rnd = n => { const v = Math.sin(n * 127.1 + 311.7) * 43758.5453; return v - Math.floor(v); };
  ctx.save();
  // trời tối sầm ánh lam
  const dim = s < .19 ? cl(s / .19) * .5 : s < .7 ? .5 : cl(1 - (s - .7) / .08) * .5;
  ctx.globalAlpha = dim; ctx.fillStyle = '#02081e'; ctx.fillRect(0, 0, W, Hc);
  // tụ khí: quầng lam quanh người + hạt sáng bay lên
  if (s < .32){
    const k = cl(s / .3), cx = fx, cy = fy - H * .5, R = H * (.55 + .25 * Math.sin(t * 12));
    const gr = ctx.createRadialGradient(cx, cy, 2, cx, cy, R);
    gr.addColorStop(0, `rgba(140,220,255,${.55 * k})`); gr.addColorStop(.5, `rgba(40,120,255,${.3 * k})`); gr.addColorStop(1, 'rgba(20,60,200,0)');
    ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = gr; ctx.fillRect(cx - R, cy - R, R * 2, R * 2);
    for (let i = 0; i < 18; i++){
      const p = (t * .9 + rnd(i)) % 1, x = cx + (rnd(i + 40) - .5) * H * .9, y = fy - p * H * 1.3;
      ctx.globalAlpha = (1 - p) * k; ctx.fillStyle = i % 3 ? '#7fd8ff' : '#e8fbff'; ctx.fillRect(x, y, 2 * sx * .5 + 1, 2 * sx * .5 + 1);
    }
    ctx.globalCompositeOperation = 'source-over';
  }
  // cột lửa lam phun lên quanh quái (thay cho sét)
  if (s >= .28 && s < .66){
    const k = cl((s - .28) / .3), out = cl((s - .56) / .1);
    ctx.globalCompositeOperation = 'lighter';
    for (let c = 0; c < 5; c++){
      const cx = mx + (c - 2) * H * .32 + (rnd(c + 7) - .5) * H * .1, delay = Math.abs(c - 2) * .12;
      const grow = cl((k - delay) / .5), hgt = H * (1.5 - Math.abs(c - 2) * .3) * (grow < 1 ? grow * (2 - grow) : 1) * (1 - out * .6);
      if (hgt <= 1) continue;
      for (let j = 0; j < 7; j++){                                                 // lưỡi lửa chồng lên nhau, rung theo thời gian
        const q = j / 6, fl = Math.sin(t * 22 + c * 3 + j * 1.7), y = gy - hgt * q, w = H * (.16 - q * .1) * (1 + .15 * fl);
        const gr = ctx.createRadialGradient(cx + fl * w * .3, y, 0, cx + fl * w * .3, y, w * 1.6);
        gr.addColorStop(0, q > .6 ? 'rgba(230,250,255,.9)' : 'rgba(170,235,255,.8)'); gr.addColorStop(.45, 'rgba(60,150,255,.55)'); gr.addColorStop(1, 'rgba(20,60,220,0)');
        ctx.globalAlpha = (1 - out) * (.55 + .45 * (1 - q)); ctx.fillStyle = gr;
        ctx.beginPath(); ctx.ellipse(cx + fl * w * .3, y, w * 1.6, w * 2.4, 0, 0, Math.PI * 2); ctx.fill();
      }
    }
    ctx.globalCompositeOperation = 'source-over';
  }
  // chớp sáng khi trúng
  if (s >= .36 && s < .44){ ctx.globalAlpha = .5 * (1 - (s - .36) / .08); ctx.fillStyle = '#bfe6ff'; ctx.fillRect(0, 0, W, Hc); }
  // vòng lửa lam loang ra dưới chân quái (lưỡi lửa nhấp nhô dọc vòng)
  if (s >= .38 && s < .74){
    ctx.globalCompositeOperation = 'lighter';
    for (const d of [0, .1]){
      const k = cl((s - .38 - d) / .28); if (k <= 0 || k >= 1) continue;
      const rx = H * (.35 + 1.5 * k), ry = rx * .24, n = 28;
      for (let i = 0; i < n; i++){
        const a = i / n * Math.PI * 2, x = mx + Math.cos(a) * rx, y = gy + Math.sin(a) * ry;
        const hh = H * (.12 + .1 * Math.abs(Math.sin(t * 18 + i * 2.1))) * (1 - k), w = H * .05;
        const gr = ctx.createLinearGradient(x, y, x, y - hh);
        gr.addColorStop(0, 'rgba(120,210,255,.9)'); gr.addColorStop(1, 'rgba(30,90,255,0)');
        ctx.globalAlpha = (1 - k) * (d ? .6 : .9); ctx.fillStyle = gr;
        ctx.beginPath(); ctx.moveTo(x - w, y); ctx.quadraticCurveTo(x, y - hh * 1.2, x + w, y); ctx.fill();
      }
    }
    ctx.globalCompositeOperation = 'source-over';
  }
  // tàn lửa lam bay lên quanh quái
  if (s >= .4 && s < .8){
    const k = cl((s - .4) / .4);
    ctx.globalCompositeOperation = 'lighter';
    for (let i = 0; i < 26; i++){
      const p = (k * 1.3 + rnd(i * 5)) % 1, x = mx + (rnd(i + 9) - .5) * H * 2 + Math.sin(t * 6 + i) * H * .05, y = gy - p * H * 1.6;
      const z = (1 + rnd(i + 3) * 1.6) * Math.max(1, sx * .5);
      ctx.globalAlpha = (1 - p) * (1 - k * .4); ctx.fillStyle = i % 4 ? '#6cc8ff' : '#e8fbff';
      ctx.beginPath(); ctx.arc(x, y, z, 0, Math.PI * 2); ctx.fill();
    }
    ctx.globalCompositeOperation = 'source-over';
  }
  ctx.restore();
}
/** Trận đánh: tu sĩ theo bản thiết kế + đòn Hỏa Cầu mặc định, vẽ ở độ phân giải thật (toạ độ CSS px).
 *  Nhịp lượt đánh s: tư thế .00–.30 · phóng .30 · bay .30–.45 · nổ .45–.62 · tan .62–.72 · tàn lửa .72–.78 */
function drawAirFlightFx(g, x, feetY, height, t, reduced = false){
  const phase = reduced ? 0 : t * 1.8;
  const pulse = reduced ? 1 : 1 + Math.sin(t * 2.8) * .06;
  const radius = height * .32 * pulse;
  g.save();
  g.translate(x, feetY + height * .035);
  g.scale(1, .3);
  g.globalCompositeOperation = 'lighter';
  const glow = g.createRadialGradient(0,0,0,0,0,radius*1.35);
  glow.addColorStop(0,'rgba(180,235,255,.24)');
  glow.addColorStop(.6,'rgba(70,170,255,.15)');
  glow.addColorStop(1,'rgba(70,170,255,0)');
  g.fillStyle = glow;
  g.beginPath(); g.arc(0,0,radius*1.35,0,Math.PI*2); g.fill();
  g.lineWidth = Math.max(1,height*.009);
  g.strokeStyle = 'rgba(120,220,255,.75)';
  g.beginPath(); g.arc(0,0,radius,0,Math.PI*2); g.stroke();
  g.strokeStyle = 'rgba(245,208,125,.8)';
  for(let i=0;i<3;i++){
    const a=phase+i*Math.PI*2/3;
    g.beginPath(); g.arc(0,0,radius*.72,a,a+.8); g.stroke();
  }
  g.fillStyle = 'rgba(210,245,255,.85)';
  for(let i=0;i<6;i++){
    const a=-phase+i*Math.PI/3, r=radius*.9;
    g.beginPath(); g.arc(Math.cos(a)*r,Math.sin(a)*r,Math.max(.8,height*.007),0,Math.PI*2); g.fill();
  }
  g.restore();
}
function hcBattleDraw(ref, o){
  const {fw, fh} = HC_REF, {ph, sx, sy, g, sz, t} = o, s = ph.name === 'fight' ? ph.s : -1;
  const kH = sz * sy * 1.05 / 150;                                 // 150px = chiều cao nhân vật trong ảnh gốc
  // lượt hệ Thủy (đòn thường Thủy hoặc chiêu Thủy): không dùng tư thế tụ lửa trong tay (khung 1–3) của bản thiết kế Hỏa Cầu
  const dmgCasts = (o.casts || []).filter(c => c.dmg > 0);
  const wetTurn = dmgCasts.length ? dmgCasts.some(c => skById(c.skill)?.element === 'thuy') : skById(o.basic)?.element === 'thuy';
  const fireSkill = dmgCasts.length ? [...dmgCasts].sort((a,b) => b.dmg-a.dmg)[0].skill : o.basic;
  const newFireReady = s >= 0 && typeof fireFxAvailable === 'function' && fireFxAvailable(fireSkill, S.realm);
  const pose = s < 0 || s >= .5 ? 0 : s < .1 ? 0 : wetTurn || newFireReady ? (s < .3 ? 0 : 7) : s < .2 ? 1 : s < .3 ? 2 : s < .36 ? 3 : 7;
  const q = kH * HC_REF.scale[pose], fx = o.hx * sx, fy = (g + 1 + o.hy) * sy, ft = HC_REF.feet[pose];
  ctx.save(); ctx.imageSmoothingEnabled = true; ctx.imageSmoothingQuality = 'high';
  // chiêu trong ô thi triển lượt này: chiêu công mạnh nhất thay cho hỏa cầu thường, chiêu thủ diễn đầu lượt 0
  const plan = skBattlePlan(o.casts || [], s, o.basic);
  if (!plan || !plan.hideHero){
    ctx.globalAlpha = o.hAlpha;
    ctx.save(); ctx.translate(fx, fy);                                             // dáng: co giãn + nghiêng quanh điểm giữa bàn chân
    if (o.rot) ctx.rotate(o.rot);
    ctx.scale(o.sqx || 1, o.sqy || 1);
    const acting = s >= 0 && s < .78;
    const skill = skById(fireSkill);
    const action = ph.name === 'walk' ? 'run' : acting ? skinActorAction(skill) : 'idle';
    const progress = reduce ? 0 : ph.name === 'walk' ? skinActorLoop('run', t)
      : acting ? Math.min(.99, s / .6)
      : SKIN_ACTOR_SHEETS[skinArt('hero')] ? skinActorLoop('idle', t) : 0;
    if (o.hFlash) ctx.filter = 'brightness(1.8)';
    if (ph.name === 'walk' && skinActorFlight() === 'air')
      drawAirFlightFx(ctx, 0, 0, sz * sy * 1.05, t, reduce);
    const newActor = drawSkinActor(ctx, action, progress, 0, 0, sz * sy * 1.05);
    ctx.filter = 'none';
    if (!newActor){
      if(typeof drawSkinFx==='function')drawSkinFx(ctx,skinArt('hero'),'back',0,0,sz*sy*1.05,t,action,progress,reduce);
      ctx.drawImage(ref, pose * fw, 0, fw, fh, -ft[0] * q, -ft[1] * q, fw * q, fh * q);
      if (o.hFlash){ ctx.globalAlpha = .55 * o.hAlpha; ctx.drawImage(hcFlash(ref), pose * fw, 0, fw, fh, -ft[0] * q, -ft[1] * q, fw * q, fh * q); }
      // The hit overlay must not change the opacity of the front aura.
      ctx.globalAlpha=o.hAlpha;
      if(typeof drawSkinFx==='function')drawSkinFx(ctx,skinArt('hero'),'front',0,0,sz*sy*1.05,t,action,progress,reduce);
    }
    ctx.restore();
  }
  ctx.globalAlpha = 1;
  const newFireDrawn = newFireReady && drawFireSkillFx(ctx, fireSkill, s, S.realm, {
    handX:fx+sz*sx*.25, handY:fy-sz*sy*.55, targetX:o.wolfX*sx, targetY:(g+1-(o.msz||sz)*.5)*sy,
    groundY:(g+1)*sy, heroHeight:sz*sy, targetWidth:(o.msz||sz)*sx, targetHeight:(o.msz||sz)*sy, reducedMotion:reduce
  });
  if (!newFireDrawn && plan) skBattleDraw(plan, {fx, fy, sz, sx, sy, g, wolfX: o.wolfX, hAlpha: o.hAlpha});
  ultFx(!newFireDrawn && plan && plan.main && plan.row === 5 ? s : -1, {fx, fy, sz, sx, sy, g, wolfX: o.wolfX, t});
  if (!newFireDrawn && s >= .3 && s < .78 && !(plan && plan.main) && !wetTurn){                 // lượt hệ Thủy: không vẽ cầu lửa (nước vẽ bằng tbDraw / tngDraw / tlaoDraw / tlDraw)
    const tier = o.cast ? Math.min(2, S.realm) : 0;                                  // đòn thường Hỏa Cầu: biến thể theo cảnh giới
    const fk = kH * HC_FX_SCALE[tier];
    const L = HC_REF.feet[3], B = HC_REF.fire[3], q3 = kH * HC_REF.scale[3];
    const x0 = fx + (B[0] - L[0]) * q3, y0 = fy + (B[1] - L[1]) * q3;
    const tx = (o.wolfX - sz * .12) * sx, ty = (g - sz * .42) * sy, e = x => x * x * (3 - 2 * x);
    const draw = (i, a, x, y, k, al = 1) => { ctx.globalAlpha = Math.max(0, Math.min(1, al)); ctx.drawImage(ref, i * fw, 0, fw, fh, x - a[0] * k, y - a[1] * k, fw * k, fh * k); };
    if (s < .45){ const k = (s - .3) / .15, kk = e(k) * .3 + k * .7; draw(4, HC_REF.fire[4], x0 + (tx - x0) * kk, y0 + (ty - y0) * kk, fk); }
    else if (s < .62){ const k = (s - .45) / .17; draw(5, HC_REF.fire[5], tx, ty, fk * .85 * (.45 + .55 * e(Math.min(1, k * 1.4)))); }
    else if (s < .72){ const k = (s - .62) / .1; draw(5, HC_REF.fire[5], tx, ty, fk * .85 * (1 + .08 * k), 1 - k); draw(6, HC_REF.fire[6], tx, ty, fk, k); }
    else { const k = (s - .72) / .06; draw(6, HC_REF.fire[6], tx, ty + 3 * sy * k, fk, 1 - k); }
    ctx.globalAlpha = 1;
  }
  ctx.restore();
}
const fxSheet = new Image(); fxSheet.src = 'img/fx_hoa_cau.png';
const hcSheet = new Image(); hcSheet.src = HC.sheet;
