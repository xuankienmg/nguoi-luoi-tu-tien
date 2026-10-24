/* Vấn Đạo · js/craft.js: Đan dược: luyện chế theo đan phương, lò đan. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// ĐAN DƯỢC: chọn thành phẩm, luyện trực tiếp từ linh thảo và linh thạch.
// =====================================================================
let ddSel = null, ddErr = null, ddAnim = null;
try { ddSel = localStorage.getItem('van-dao-dd-sel'); } catch(e){}
const brewOf = k => (DATA.brews || []).find(b=>b.out===k);
const brewMax = b => !b || S.realm < (ITEM[b.out]?.minRealm || 0) ? 0 : Math.min(...Object.entries(b.in).filter(([,c])=>c>0).map(([k,c])=>Math.floor((S.inventory[k]||0)/c)));
const stones = () => S.inventory.linh_thach || 0;
const ddRecipes = () => DATA.brews || [];
function ddCur(){
  if(ddSel && brewOf(ddSel))return ddSel;
  const ready=ddRecipes().find(b=>brewMax(b)>0);
  return ready?.out || DATA.pillChain[Math.min(S.realm+1,DATA.pillChain.length-1)] || ddRecipes()[0]?.out;
}
function ddPillCard(k){
  const it=ITEM[k],n=S.inventory[k]||0,b=brewOf(k),max=brewMax(b),sel=k===ddCur();
  const locked=S.realm<(it.minRealm||0),st=locked?'Chưa đủ cảnh giới':max?'Luyện được':'Thiếu nguyên liệu';
  return '<button class="dd-card '+(sel?'sel ':'')+(max?'ok':'miss')+'" data-pill="'+k+'" aria-pressed="'+sel+'" data-tip="'+itemTip(k,st)+'"><small class="dd-tier">'+(it.tier?'Bậc '+it.tier:'Đan phương')+'</small><span class="dd-fr r-'+it.rarity+'">'+itemImg(k)+'</span><span class="dd-cn">'+it.name+'</span><b class="dd-cq">×'+fmt(n)+'</b><span class="dd-cs">'+st+'</span></button>';
}

function ddBreak(){
  const r = realm();
  if (S.isPeak && !S.hasNext)
    return `<p class="dd-sec">Đột phá kế tiếp</p><p class="dd-now">${S.realmName} ${S.stageName}</p><p class="dd-note">Cảnh giới ${DATA.lockedRealms[0]} chưa khai mở. Tích lũy đan dược và yêu hạch chờ bản cập nhật sau.</p>`;
  const q = qiNow(), full = q >= S.qiReq;
  const cur = `${S.realmName} ${S.stageName}`, next = S.isPeak ? DATA.realms[S.realm + 1].name : S.targetTitle.replace(' · ', ' ');
  const row = (ok, ic, name, val, extra = '') => `<li class="${ok ? 'ok' : 'no'} ${extra}"><i>${ok ? '✓' : '✗'}</i>${ic}<span>${name}</span><b>${val}</b></li>`;
  let rows = row(full, '', 'Tu vi', `<span id="ddQi2" title="${fmtFull(q)} / ${fmtFull(S.qiReq)}">${fmtCompact(q)} / ${fmtCompact(S.qiReq)}</span>`);
  let bossBox = '';
  for (const c of S.targetCost){
    const it = ITEM[c.item], ok = c.have >= c.need;
    if (it.type === 'boss'){
      bossBox = `<div class="dd-boss ${ok ? 'ok' : ''}" data-tip="${itemTip(c.item)}">
        <span class="dd-fr boss">${itemImg(c.item)}</span>
        <div><p class="nm">${it.name}</p><p class="st">${fmt(c.have)} / ${c.need} · nguyên liệu Boss</p>
        <p class="hint">${ok ? 'Đã có, sẵn sàng đột phá.' : `Đánh bại Boss ${bossChapterFor(r.bossItem)?.boss || r.boss}${bossChapterTag(r.bossItem)} để nhận nguyên liệu.`}</p></div></div>`;
      rows += row(ok, '', it.name, `${fmt(c.have)} / ${c.need}`, 'bossrow');
    } else rows += row(ok, itemImg(c.item), it.name, `${fmt(c.have)} / ${fmt(c.need)}`);
  }
  const ready = full && hasAll(S.targetCost);
  const needBoss = S.isPeak && S.targetCost.some(c => ITEM[c.item].type === 'boss' && c.have < c.need);
  const btn = ready ? `<button class="dd-go gold" data-dd="advance">Đột phá</button>`
    : needBoss ? `<button class="dd-go" data-dd="boss">Đến khu vực Boss</button>` : '';
  const upcoming = !S.isPeak && ITEM[r.bossItem] ? `<p class="dd-note">Đại cảnh giới ${DATA.realms[S.realm + 1] ? DATA.realms[S.realm + 1].name : DATA.lockedRealms[0]} cần thêm ${itemImg(r.bossItem)} <b>${ITEM[r.bossItem].name}</b> (Boss ${bossChapterFor(r.bossItem)?.boss || r.boss}${bossChapterTag(r.bossItem)}).</p>` : '';
  return `<p class="dd-sec">${S.isPeak ? 'Đột phá kế tiếp' : 'Đột phá tầng kế tiếp'}</p>
    <div class="dd-path"><span>${cur}</span><i>→</i><b>${next}</b></div>
    <ul class="dd-req">${rows}</ul>${bossBox}${btn}${upcoming}`;
}

function renderCraft(){
  const pane = $('pane-craft');
  if (!$('ddCanvas')){
    pane.innerHTML = `<div class="dd">
      <header class="dd-top">${lcNav('craft')}<div class="dd-cult" id="ddCult"></div></header>
      <section class="dd-main">
        <div class="dd-synth" id="ddSynth">
          <div class="dd-io" id="ddIn"></div>
          <div class="dd-fur"><canvas id="ddCanvas" width="160" height="144" aria-label="Lò đan"></canvas></div>
          <div class="dd-io" id="ddOut"></div>
          <div class="dd-act" id="ddAct"></div>
        </div>
        <div class="dd-inv" id="ddInv"></div>
      </section>
      <aside class="dd-side"><div class="dd-use" id="ddUse"></div><div class="dd-break" id="ddBreak"></div></aside>
    </div>`;
  }
  const k=ddCur(),it=ITEM[k],b=brewOf(k),max=brewMax(b);
  if(!it||!b)return;
  const herbs=Object.entries(b.in).filter(([id])=>id!=='linh_thach');
  $('ddIn').innerHTML='<span class="dd-lab">Nguyên liệu</span><div class="dd-reagents">'+herbs.map(([id,c])=>{
    const have=S.inventory[id]||0;
    return '<div class="dd-reagent" data-tip="'+itemTip(id)+'"><span class="dd-fr r-'+ITEM[id].rarity+'">'+itemImg(id)+'</span><span class="dd-nm">'+ITEM[id].name+'</span><b class="dd-cnt '+(have>=c?'ok':'miss')+'">'+fmt(Math.min(have,c))+' / '+fmt(c)+'</b></div>';
  }).join('')+'</div>';
  $('ddOut').innerHTML='<span class="dd-lab">Thành phẩm</span><span class="dd-fr big r-'+it.rarity+'">'+itemImg(k)+'</span><span class="dd-nm">'+it.name+'</span><b class="dd-cnt out">1</b><small class="dd-have">Đang có '+fmt(S.inventory[k]||0)+'</small>';
  const missing=Object.entries(b.in).filter(([id,c])=>(S.inventory[id]||0)<c).map(([id,c])=>ITEM[id].name+' '+fmt(c-(S.inventory[id]||0)));
  const locked=S.realm<(it.minRealm||0),why=locked?'Cần đạt '+DATA.realms[it.minRealm].name:missing.length?'Thiếu '+missing.join(' · '):'';
  $('ddAct').innerHTML='<button class="dd-synth-btn" data-dd="brew" '+(max>0&&!busy?'':'disabled')+'>Luyện chế</button><button class="dd-all" data-dd="ten" '+(max>=10&&!busy?'':'disabled')+' data-tip="Luyện đúng 10 viên · cần đủ nguyên liệu cho 10 lần">Luyện ×10</button><button class="dd-all" data-dd="all" '+(max>1&&!busy?'':'disabled')+' data-tip="Luyện chế tối đa: '+max+' lần">Tối đa ×'+max+'</button><p class="dd-fee">'+(why?'<span class="miss">'+why+'</span> · ':'')+'Phí '+itemImg('linh_thach')+' '+fmt(b.in.linh_thach||0)+' · luôn thành công</p>';
  $('ddInv').innerHTML='<p class="dd-sec">Đan phương · chọn đan dược để luyện chế</p><div class="dd-cards">'+ddRecipes().map(b=>ddPillCard(b.out)).join('')+'</div>';
  $('ddUse').innerHTML='<p class="dd-sec">Công dụng</p><div class="dd-use-hd">'+itemImg(k)+'<b class="rc-'+it.rarity+'">'+it.name+'</b></div><p class="dd-use-tx">'+(it.use||it.desc)+'</p><p class="dd-note">Luyện chế trực tiếp từ linh thảo và linh thạch. Không tiêu hao đan bậc dưới.</p>';
  $('ddBreak').innerHTML = ddBreak();
  liveDan();
}

function liveDan(){
  if (!S || !$('ddCult')) return;
  const q = qiNow(), rq = S.qiReq;
  $('ddCult').innerHTML = `<span>Cảnh giới <b>${S.realmName}</b> · <b>${S.stageName}</b></span>
    <div class="dd-qi" title="${fmtFull(q)} / ${fmtFull(rq)}"><i style="width:${Math.min(100, q / rq * 100)}%"></i><em>${fmtCompact(q)} / ${fmtCompact(rq)} tu vi</em></div>`;
  setQiDisplay($('ddQi2'), q, rq);
}

// ---- lò đan pixel: nghỉ · đang luyện chế · thành đan ----
const ddImg = {};
// ảnh lò nét gấp đôi canvas logic (img/furnace2_*.png, cắt lại: tools/cut_furnace.py)
['furnace_body','furnace_lid','furnace_runes'].forEach(k => { const im = new Image(); im.src = `img/${k.replace('furnace_', 'furnace2_')}.png`; ddImg[k] = im; });
const ddDraw = (g, k, x, y) => { const im = ddImg[k]; if (im.complete && im.naturalWidth) g.drawImage(im, x, y, im.naturalWidth / 2, im.naturalHeight / 2); };
const ddFx = {smoke: [], sparks: [], t: 0, last: 0};
const DD_BREW_ANIM = {heat: .25, reveal: .75};
const FIRE_O = ['#fff1b8','#ffd27a','#ff9a3c','#e0562a','#8a2a1a'];
const FIRE_C = ['#eaffff','#9ff6ee','#4fd6d0','#2a8f9a','#1d4d5a'];
function ddSizeCanvas(c){
  const box = c.parentElement.getBoundingClientRect();
  if (!box.width) return false;
  const sc = Math.max(2, Math.min(MODE === 'panel' ? 2 : 3, Math.floor(Math.min(box.width / 80, Math.max(144, box.height) / 72))));   // cửa sổ Tu luyện: giữ lò ×2 cho gọn
  c.style.width = 80 * sc + 'px'; c.style.height = 72 * sc + 'px';
  return true;
}
function ddFrame(now){
  requestAnimationFrame(ddFrame);
  if (paneOff('craft')) return;
  const c = $('ddCanvas');
  if (!c || !S || !c.offsetParent) return;
  if (!c._sized) c._sized = ddSizeCanvas(c);
  const dt = Math.min(.1, (now - (ddFx.last || now)) / 1000); ddFx.last = now; ddFx.t += dt;
  const g = c.getContext('2d'); g.imageSmoothingEnabled = false; g.setTransform(2, 0, 0, 2, 0, 0);   // canvas 160×144, vẽ theo toạ độ logic 80×72
  const t = ddFx.t, ag = ddAnim ? (performance.now() - ddAnim.t0) / 1000 : 99;
  const fire = ag < DD_BREW_ANIM.heat, done = ag >= DD_BREW_ANIM.heat && ag < DD_BREW_ANIM.heat + DD_BREW_ANIM.reveal, k = done ? (ag - DD_BREW_ANIM.heat) / DD_BREW_ANIM.reveal : 0;
  const heat = fire ? 1 : done ? .9 - k * .6 : .3;
  g.clearRect(0, 0, 80, 72);
  const P = (x, y, col, a = 1) => { g.globalAlpha = a; g.fillStyle = col; g.fillRect(Math.round(x), Math.round(y), 1, 1); };
  const glow = fire ? '#12403e' : done ? '#3e3412' : '#16201e';
  for (let r = 30; r > 0; r -= 6){ g.globalAlpha = .18; g.fillStyle = glow; g.beginPath(); g.ellipse(40, 44, r * 1.25, r, 0, 0, 7); g.fill(); }
  g.globalAlpha = 1;
  [['#3a322a', 25, 64, 30, 1], ['#2a2420', 21, 65, 38, 2], ['#1e1a16', 18, 67, 44, 2], ['#14110e', 20, 69, 40, 2]].forEach(([col, x, y, w, h]) => { g.fillStyle = col; g.fillRect(x, y, w, h); });
  for (let x = 25; x <= 55; x++){
    const cyan = x % 6 < 2;
    const n = Math.sin(x * 1.7 + t * 9) + Math.sin(x * .6 - t * 5.3) * .8 + Math.sin(t * 13 + x);
    const h = Math.max(0, Math.round((fire ? 6 : 2.2) * heat + n * (fire ? 2.2 : 1) * heat + (x > 34 && x < 46 ? 2 * heat : 0)));
    const pal = cyan ? FIRE_C : FIRE_O;
    for (let i = 0; i < h; i++) P(x, 64 - i, pal[Math.min(4, Math.floor(i / Math.max(1, h) * 4.2))], heat < .35 ? .65 : 1);
  }
  if (fire && Math.random() < dt * 18) ddFx.sparks.push({x: 28 + Math.random() * 26, y: 60, vx: (Math.random() - .5) * 6, vy: -10 - Math.random() * 8, life: 1, c: Math.random() < .5 ? '#9ff6ee' : '#ffd27a'});
  const fx = 9, fy = 10;
  ddDraw(g, 'furnace_body', fx, fy);
  const rune = fire ? .6 + .4 * Math.abs(Math.sin(t * 6)) : done ? 1 - k * .7 : .14 + .08 * Math.sin(t * 1.3);
  g.globalAlpha = rune; ddDraw(g, 'furnace_runes', fx, fy);
  g.globalAlpha = rune * .35; ddDraw(g, 'furnace_runes', fx - .5, fy); ddDraw(g, 'furnace_runes', fx + .5, fy); ddDraw(g, 'furnace_runes', fx, fy - .5);
  g.globalAlpha = 1;
  let lidY = fy;
  if (fire) lidY -= (t % .3 < .08) ? 1 : 0;
  if (done) lidY -= Math.round(12 * Math.min(1, (ag - DD_BREW_ANIM.heat) / .12));
  ddDraw(g, 'furnace_lid', fx, lidY);
  if (done && ddAnim){
    const it = ITEM[ddAnim.item], col = it?.color || '#e6c46a', tier = it?.tier || 1;
    const py = Math.round(27 - Math.min(7, (ag - DD_BREW_ANIM.heat) * 24) + Math.sin(t * 3) * 1.2);
    const rays = 2 + tier * 2;
    for (let i = 0; i < rays; i++){
      const a = t * .8 + i / rays * Math.PI * 2, L = 6 + tier * 2 + Math.sin(t * 4 + i) * 1.5;
      for (let d = 4; d < L; d++) P(40 + Math.cos(a) * d, py + Math.sin(a) * d, col, .6 * (1 - d / L));
    }
    g.globalAlpha = .35; g.fillStyle = col; g.fillRect(36, py - 4, 9, 9);
    g.globalAlpha = 1; g.fillStyle = '#1a120c'; g.fillRect(38, py - 2, 5, 5);
    g.fillStyle = col; g.fillRect(38, py - 1, 5, 3); g.fillRect(39, py - 2, 3, 5);
    g.fillStyle = '#fff'; g.fillRect(39, py - 1, 1, 1);
  }
  const rate = fire ? 14 : done ? 4 : 1.5;
  if (Math.random() < dt * rate) ddFx.smoke.push({x: 36 + Math.random() * 8, y: lidY + 2, vx: (Math.random() - .5) * 3, vy: -(4 + Math.random() * 5), life: 1, c: fire && Math.random() < .5 ? '#7ff0e6' : '#9aa6a2'});
  for (const s of ddFx.smoke){ s.x += (s.vx + Math.sin(t * 2 + s.y * .3) * 1.5) * dt; s.y += s.vy * dt; s.life -= dt * .4; P(s.x, s.y, s.c, Math.max(0, s.life) * (s.c === '#7ff0e6' ? .7 : .45)); }
  ddFx.smoke = ddFx.smoke.filter(s => s.life > 0 && s.y > -2);
  for (const s of ddFx.sparks){ s.x += s.vx * dt; s.y += s.vy * dt; s.vy += 6 * dt; s.life -= dt * 1.1; P(s.x, s.y, s.c, Math.max(0, s.life)); }
  ddFx.sparks = ddFx.sparks.filter(s => s.life > 0);
  if (fire) for (let i = 0; i < 8; i++){ const a = t * 2.4 + i / 8 * Math.PI * 2; P(40 + Math.cos(a) * 34, 40 + Math.sin(a) * 9, i % 2 ? '#7ff0e6' : '#e6fffb', .5 + .3 * Math.sin(a)); }
  g.globalAlpha = 1;
}
requestAnimationFrame(ddFrame);
addEventListener('resize', () => { const c = $('ddCanvas'); if (c) c._sized = false; });

// Thông báo luyện chế thành công: dùng chung cho mọi công thức (server trả thành phẩm + nguyên liệu đã tiêu hao)
let craftTimer = 0;
