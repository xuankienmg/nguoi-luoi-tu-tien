/* js/craft.js: Đan dược: luyện chế theo đan phương, lò đan. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// ĐAN DƯỢC: chọn thành phẩm, luyện trực tiếp từ linh thảo và linh thạch.
// =====================================================================
let ddSel = null, ddErr = null, ddAnim = null;
try { ddSel = localStorage.getItem('van-dao-dd-sel'); } catch(e){}
const brewOf = k => (DATA.brews || []).find(b=>b.out===k);
// cấp luyện đan (Nhất → Thánh Phẩm): đan bậc N cần cấp N
const ddRank = () => S.alchemy?.rank || 1;
const ddRankLock = k => (ITEM[k]?.tier || 0) > ddRank();
const ddOdds = k => (S.alchemy?.odds || []).find(o => o.pill === k);
const DD_RANK_NUM = ['Nhất','Nhị','Tam','Tứ','Ngũ','Lục','Thất','Bát','Cửu','Thiên','Thánh'];
const brewMax = b => !b || S.realm < (ITEM[b.out]?.minRealm || 0) || ddRankLock(b.out) ? 0 : Math.min(...Object.entries(b.in).filter(([,c])=>c>0).map(([k,c])=>Math.floor((S.inventory[k]||0)/c)));
const stones = () => S.inventory.linh_thach || 0;
const ddRecipes = () => DATA.brews || [];
function ddCur(){
  if(ddSel && brewOf(ddSel))return ddSel;
  const ready=ddRecipes().find(b=>brewMax(b)>0);
  return ready?.out || DATA.pillChain[Math.min(S.realm+1,DATA.pillChain.length-1)] || ddRecipes()[0]?.out;
}
// ---- danh sách đan phương: chia nhóm, lọc theo nhóm / luyện được, tìm theo tên ----
const DD_CATS = [['cg','Cảnh giới','Đan cảnh giới'],['pc','Phá chướng','Phá chướng đan'],['bn','Bổ nguyên','Bổ nguyên đan'],['ht','Hỗ trợ','Đan hỗ trợ'],['lt','Linh thú','Đan linh thú']];
const DD_PET = ['linh_thu_dan','hoa_hinh_dan','than_thu_huyet','thu_linh_dan'];
const ddCatOf = k => k.startsWith('pha_chuong_dan_') ? 'pc' : k.startsWith('bo_nguyen_') ? 'bn' : DD_PET.includes(k) ? 'lt' : DATA.pillChain.includes(k) ? 'cg' : 'ht';
let ddCat = 'all', ddQ = '', ddOnlyReady = false;
try { ddCat = localStorage.getItem('van-dao-dd-cat') || 'all'; ddOnlyReady = localStorage.getItem('van-dao-dd-ready') === '1'; } catch(e){}
const ddFold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();
// trạng thái: ok luyện được · miss thiếu nguyên liệu · rank cấp luyện đan thấp · lock chưa đủ cảnh giới
const ddState = k => { const it = ITEM[k]; return S.realm < (it.minRealm || 0) ? 'lock' : ddRankLock(k) ? 'rank' : brewMax(brewOf(k)) ? 'ok' : 'miss'; };
const DD_LOCK_SVG = '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6"><rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/></svg>';
function ddPillCard(k){
  const it=ITEM[k],max=brewMax(brewOf(k)),sel=k===ddCur(),st=ddState(k);
  const full=st==='lock'?'Chưa đủ cảnh giới':st==='rank'?'Cần '+DD_RANK_NUM[it.tier-1]+' Phẩm':st==='ok'?'Luyện được':'Thiếu nguyên liệu';
  // khóa: chỉ hiện biểu tượng (bậc đan đã ghi bên cạnh) để chừa chỗ cho tên
  const chip=st==='ok'?'×'+fmt(max):st==='miss'?'Thiếu':DD_LOCK_SVG+'<span class="dd-sr">'+(st==='rank'?'Cấp thấp':'Khóa')+'</span>';
  return '<button class="dd-row st-'+st+(sel?' sel':'')+'" data-pill="'+k+'" aria-pressed="'+sel+'" data-tip="'+itemTip(k,full)+'"><span class="dd-fr r-'+it.rarity+'">'+itemImg(k)+'</span><span class="dd-rn">'+it.name+'</span>'+(it.tier?'<small class="dd-rt">'+DD_RANK_NUM[it.tier-1]+' Phẩm</small>':'')+'<span class="dd-chip">'+chip+'</span></button>';
}
function ddRenderList(){
  if (!$('ddList')) return;
  const all = ddRecipes().map(b => b.out).filter(k => ITEM[k]);
  const ready = all.filter(k => ddState(k) === 'ok');
  if (ddCat !== 'all' && !DD_CATS.some(c => c[0] === ddCat)) ddCat = 'all';
  const q = ddFold(ddQ.trim());
  const hit = k => (ddCat === 'all' || ddCatOf(k) === ddCat) && (!ddOnlyReady || ddState(k) === 'ok')
    && (!q || ddFold(ITEM[k].name).includes(q) || ddFold(window.tr ? tr(ITEM[k].name) : '').includes(q));
  const shown = all.filter(hit);
  const cats = [['all','Tất cả']].concat(DD_CATS).map(([c, label]) => {
    const n = c === 'all' ? all.length : all.filter(k => ddCatOf(k) === c).length;
    return n ? `<button class="dd-cat${ddCat === c ? ' on' : ''}" data-ddcat="${c}" aria-pressed="${ddCat === c}">${label}<small>${n}</small></button>` : '';
  }).join('');
  if ($('ddCats')._h !== cats) { $('ddCats').innerHTML = cats; $('ddCats')._h = cats; }
  $('ddCount').textContent = shown.length + ' / ' + all.length;
  $('ddReadyN').textContent = '(' + ready.length + ')';
  $('ddReady').checked = ddOnlyReady;
  const groups = DD_CATS.map(([c, , label]) => {
    const ks = shown.filter(k => ddCatOf(k) === c);
    if (!ks.length) return '';
    const r = all.filter(k => ddCatOf(k) === c && ddState(k) === 'ok').length;
    return `<div class="dd-grp"><p class="dd-gh">${label}<small>${r} luyện được</small></p><div class="dd-rows">${ks.map(ddPillCard).join('')}</div></div>`;
  }).join('');
  $('ddList').innerHTML = groups || '<p class="dd-empty">Không có đan phương phù hợp.</p>';
}

function renderCraft(){
  const pane = $('pane-craft');
  if (!$('ddList')){
    // 3 cột: đan phương (trái) · lò đan (giữa) · công dụng (phải)
    pane.innerHTML = `<div class="dd dn">
      <header class="dd-top">${lcNav('craft')}<div class="dd-cult" id="ddCult"></div></header>
      <section class="dd-main dn-main" aria-label="Lò đan">
        <div class="dn-out" id="ddOut"></div>
        <div class="dn-in" id="ddIn"></div>
        <div class="dn-act" id="ddAct"></div>
      </section>
        <section class="dd-inv" id="ddInv" aria-label="Đan phương">
          <div class="dd-flt">
            <p class="dd-sec">Đan phương <small id="ddCount"></small></p>
            <label class="dd-q"><span class="dd-sr">Tìm đan dược</span><svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke-width="2.4" stroke-linecap="round"><circle cx="11" cy="11" r="7"/><path d="M20 20l-3.5-3.5"/></svg><input id="ddQ" type="search" placeholder="Tìm theo tên đan…" autocomplete="off"></label>
            <div class="dd-cats" id="ddCats" role="group" aria-label="Lọc theo nhóm"></div>
            <label class="dd-ready"><input type="checkbox" id="ddReady"> Chỉ hiện đan luyện được <b id="ddReadyN"></b></label>
          </div>
          <div id="ddList"></div>
        </section>
      <aside class="dd-side"><div class="dd-use" id="ddUse"></div></aside>
    </div>`;
  }
  const k=ddCur(),it=ITEM[k],b=brewOf(k),max=brewMax(b);
  if(!it||!b)return;
  const herbs=Object.entries(b.in).filter(([id])=>id!=='linh_thach');
  $('ddIn').innerHTML='<p class="dd-sec">Nguyên liệu mỗi lò</p>'+herbs.map(([id,c])=>{
    const have=S.inventory[id]||0,ok=have>=c;
    return '<div class="dn-rg '+(ok?'ok':'miss')+'" data-tip="'+itemTip(id)+'"><span class="dd-fr r-'+ITEM[id].rarity+'">'+itemImg(id)+'</span><span class="dn-rgm"><span class="dn-rgn">'+ITEM[id].name+'</span><span class="dn-bar"><i style="width:'+Math.min(100,Math.floor(have/c*100))+'%"></i></span></span><b>'+fmt(have)+' / '+fmt(c)+'</b></div>';
  }).join('');
  const st=ddState(k),stTx=st==='lock'?'Chưa đủ cảnh giới':st==='rank'?'Cần '+DD_RANK_NUM[it.tier-1]+' Phẩm':st==='ok'?'Luyện được ×'+fmt(max):'Thiếu nguyên liệu';
  $('ddOut').innerHTML='<div class="dn-tags">'+(it.tier?'<span class="dn-tier rc-'+it.rarity+'">'+DD_RANK_NUM[it.tier-1]+' Phẩm</span>':'')+'<span class="dn-cat">'+DD_CATS.find(c=>c[0]===ddCatOf(k))[2]+'</span></div>'
    +'<div class="dn-name"><span class="dd-fr big r-'+it.rarity+'">'+itemImg(k)+'</span><h3 class="rc-'+it.rarity+'">'+it.name+'</h3></div>'
    +'<p class="dn-have">Đang có <b>'+fmt(S.inventory[k]||0)+'</b> viên <span class="st-'+st+'"><span class="dd-chip">'+stTx+'</span></span></p>';
  const missing=Object.entries(b.in).filter(([id,c])=>(S.inventory[id]||0)<c).map(([id,c])=>ITEM[id].name+' '+fmt(c-(S.inventory[id]||0)));
  const locked=S.realm<(it.minRealm||0),why=locked?'Cần đạt '+DATA.realms[it.minRealm].name:ddRankLock(k)?'Cần cấp luyện đan '+DD_RANK_NUM[it.tier-1]+' Phẩm':missing.length?'Thiếu '+missing.join(' · '):'';
  const od=ddOdds(k),pct=x=>Math.round(x*100)+'%';
  const failP=od?od.fail:(S.alchemy?.fail||[])[Math.max(1,it.tier||1)-1];   // tỉ lệ thất bại theo cấp luyện đan
  // Tỉ lệ thật mỗi lò: thất bại + (thành công × phẩm). Thanh và ô cộng lại đủ 100%.
  // Đan không chia phẩm (Phá chướng, Bổ nguyên, Linh thú): cùng khung, chỉ Thất bại · Thành công.
  const failTip='Lò hỏng mất nguyên liệu, nhận 50% EXP. Cấp luyện đan càng vượt bậc đan, tỉ lệ thất bại càng thấp (tối thiểu 15%, đan Đại Thừa trở lên tối thiểu 80%).';
  const p1=x=>(Math.round(x*1000)/10)+'%';
  const odds=failP==null?'':(()=>{
    const f=failP||0,s=1-f;
    let cells,note;
    if(od){
      const sum=(od.ha+od.trung+od.thuong)||1,ab=q=>s*q/sum;
      cells=[['qf','Thất bại',f,failTip],['q0','Hạ phẩm',ab(od.ha)],['q1','Trung phẩm',ab(od.trung)],['q2','Thượng phẩm',ab(od.thuong)]];
      note='Khi thành công, phẩm chất chia: Hạ '+pct(od.ha/sum)+' · Trung '+pct(od.trung/sum)+' · Thượng '+pct(od.thuong/sum);
    }else{
      cells=[['qf','Thất bại',f,failTip],['qs','Thành công',s]];
      note='Đan này không chia phẩm chất.';
    }
    return '<div class="dn-odds"><p class="dn-odds-hd"><span>Kết quả mỗi lò</span><span class="dn-succ"><span>Thành công</span> <b>'+p1(s)+'</b></span></p>'
      +'<div class="dn-odds-bar">'+cells.map(([c,,v])=>'<i class="'+c+'" style="width:'+(v*100)+'%"></i>').join('')+'</div>'
      +'<div class="dn-odds-grid'+(od?'':' two')+'">'+cells.map(([c,n,v,tip])=>'<div class="dn-oc '+c+'"'+(tip?' data-tip="'+tip+'"':'')+'><span><i></i>'+n+'</span><b>'+p1(v)+'</b></div>').join('')+'</div>'
      +'<p class="dn-odds-note">'+note+'</p></div>';
  })();
  $('ddAct').innerHTML=odds+(why?'<p class="dn-why" role="status">'+why+'</p>':'')
    +'<div class="dn-btns"><button class="dd-synth-btn" data-dd="brew" '+(max>0&&!busy?'':'disabled')+'>Luyện chế</button><button class="dd-all" data-dd="ten" '+(max>=10&&!busy?'':'disabled')+' data-tip="Luyện đúng 10 viên · cần đủ nguyên liệu cho 10 lần">Luyện ×10</button><button class="dd-all" data-dd="all" '+(max>1&&!busy?'':'disabled')+' data-tip="Luyện chế tối đa: '+max+' lần">Tối đa ×'+fmt(max)+'</button></div>'
    +'<p class="dd-fee">Phí mỗi lò '+itemImg('linh_thach')+' <b>'+fmt(b.in.linh_thach||0)+'</b> linh thạch</p>';
  ddRenderList();
  $('ddUse').innerHTML='<p class="dd-sec">Công dụng</p><div class="dd-use-hd">'+itemImg(k)+'<b class="rc-'+it.rarity+'">'+it.name+'</b></div><p class="dd-use-tx">'+(it.use||it.desc)+'</p><p class="dd-note">Luyện chế trực tiếp từ linh thảo và linh thạch. Không tiêu hao đan bậc dưới.</p>';

  liveDan();
}

function liveDan(){
  if (!S || !$('ddCult')) return;
  // thanh trên cùng: cảnh giới luyện đan (Nhất → Thánh Phẩm) và EXP tới cấp kế tiếp
  const al = S.alchemy;
  if (al){
    const [grade, title] = al.name.split(' - '), top = al.rank >= 11, span = Math.max(1, al.nextExp - al.rankExp), cur = al.exp - al.rankExp;
    // màu theo phẩm như khung đan: Nhất Phẩm common → Thiên Phẩm supreme · Thánh Phẩm kimtien
    const rc = ['common','uncommon','rare','epic','legendary','mythic','divine','immortal','dao','supreme','kimtien'][al.rank - 1] || 'common';
    const html = `<div class="dd-rk r-${rc}" data-tip="Cảnh giới luyện đan&#10;Luyện đan cộng EXP theo phẩm cấp của đan. Từ Ngũ Phẩm, EXP cần mỗi cấp tăng dần ×1.1 → ×2.25. Cấp càng cao, đan càng dễ ra Trung / Thượng phẩm; đột phá dùng viên phẩm cao nhất trước (Trung +5%, Thượng +10% tỉ lệ).">
      <span class="dd-rk-badge">${grade}</span>
      <div class="dd-rk-main"><span class="dd-rk-lab">Cảnh giới luyện đan · <b>${title}</b></span>
        <div class="dd-rk-bar"><i style="width:${top ? 100 : Math.min(100, cur / span * 100)}%"></i><em>${top ? 'Viên mãn' : `${fmt(cur)} / ${fmt(span)} EXP`}</em></div></div>
    </div><div class="buff-row" hidden></div>`;
    if ($('ddCult')._h !== html) { $('ddCult').innerHTML = html; $('ddCult')._h = html; }   // liveDan chạy liên tục: chỉ vẽ lại khi EXP đổi
  }

}

// ---- lò đan pixel: nghỉ · đang luyện chế · thành đan ----
const ddImg = {};
// ảnh lò nét gấp đôi canvas logic (img/furnace2_*.png, cắt lại: tools/cut_furnace.py)
['furnace_body','furnace_lid','furnace_runes'].forEach(k => { const im = new Image(); im.src = `img/${k.replace('furnace_', 'furnace2_')}.png`; ddImg[k] = im; });
const ddDraw = (g, k, x, y) => { const im = ddImg[k]; if (im.complete && im.naturalWidth) g.drawImage(im, x, y, im.naturalWidth / 2, im.naturalHeight / 2); };
const ddFx = {smoke: [], sparks: [], t: 0, last: 0};
const DD_BREW_ANIM = {heat: .1, reveal: .25};
const FIRE_O = ['#fff1b8','#ffd27a','#ff9a3c','#e0562a','#8a2a1a'];
const FIRE_C = ['#eaffff','#9ff6ee','#4fd6d0','#2a8f9a','#1d4d5a'];
// hiệu hỏa theo phẩm đan vừa ra: Hạ = lửa thường · Trung = lam băng · Thượng = kim tử
const DD_FIRE = [
  [FIRE_O, FIRE_C],
  [['#f2f8ff','#a8d0ff','#5a96ff','#2f58c8','#1a2a6a'], ['#eaffff','#9ff6ee','#4fd6d0','#2a8f9a','#1d4d5a']],
  [['#fffbe6','#ffe38a','#ffbf3c','#e07a1a','#7a3a0a'], ['#fbeaff','#e2a8ff','#b45aff','#7a2ad0','#3a145a']],
];
const DD_QCOL = [null, '#7fb8ff', '#ffcf5a'];
const DD_LINGER = 1.4;   // Trung / Thượng phẩm: lửa giữ màu thêm một lúc sau khi ra đan
function ddSizeCanvas(c){
  const box = c.parentElement.getBoundingClientRect();
  if (!box.width) return false;
  const sc = Math.max(2, Math.min(MODE === 'panel' ? 2 : 3, Math.floor(Math.min(box.width / 80, Math.max(144, box.height) / 72))));   // cửa sổ Tu luyện: giữ lò ×2 cho gọn
  c.style.width = 80 * sc + 'px'; c.style.height = 72 * sc + 'px';
  return true;
}
function ddFrame(now){
  requestAnimationFrame(ddFrame);
  if (paneOff('craft') || !fpsGate('craft', now, FPS_PANE)) return;
  const c = $('ddCanvas');
  if (!c || !S || !c.offsetParent) return;
  if (!c._sized) c._sized = ddSizeCanvas(c);
  const dt = Math.min(.1, (now - (ddFx.last || now)) / 1000); ddFx.last = now; ddFx.t += dt;
  const g = c.getContext('2d'); g.imageSmoothingEnabled = false; g.setTransform(2, 0, 0, 2, 0, 0);   // canvas 160×144, vẽ theo toạ độ logic 80×72
  const t = ddFx.t, ag = ddAnim ? (performance.now() - ddAnim.t0) / 1000 : 99;
  const fire = ag < DD_BREW_ANIM.heat, done = ag >= DD_BREW_ANIM.heat && ag < DD_BREW_ANIM.heat + DD_BREW_ANIM.reveal, k = done ? (ag - DD_BREW_ANIM.heat) / DD_BREW_ANIM.reveal : 0;
  const q = ddAnim?.q || 0, linger = q > 0 && !fire && !done && ag < DD_BREW_ANIM.heat + DD_BREW_ANIM.reveal + DD_LINGER;
  const fq = fire || done || linger ? q : 0, firePal = DD_FIRE[fq];
  const heat = fire ? 1 : done ? .9 - k * .6 + q * .2 : linger ? .3 + .5 * (1 - (ag - DD_BREW_ANIM.heat - DD_BREW_ANIM.reveal) / DD_LINGER) : .3;
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
    const pal = firePal[cyan ? 1 : 0];
    for (let i = 0; i < h; i++) P(x, 64 - i, pal[Math.min(4, Math.floor(i / Math.max(1, h) * 4.2))], heat < .35 ? .65 : 1);
  }
  if ((fire || (fq && (done || linger))) && Math.random() < dt * (fire ? 18 : 10 * fq)) ddFx.sparks.push({x: 28 + Math.random() * 26, y: 60, vx: (Math.random() - .5) * 6, vy: -10 - Math.random() * 8, life: 1, c: firePal[Math.random() < .5 ? 1 : 0][1]});
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
    const it = ITEM[ddAnim.item], col = DD_QCOL[q] || it?.color || '#e6c46a', tier = (it?.tier || 1) + q;
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
