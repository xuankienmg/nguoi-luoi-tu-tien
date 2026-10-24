/* Vấn Đạo · js/skill-preview.js: xem trước chiêu thức pixel. Tách từ index.html (tools/split_index.py). */
// ---- xem trước chiêu thức: tu sĩ thi triển, 8 khung hình pixel ----
const skImgs = {};
// Character-only atlases: effects are rendered by the existing skill layers.
function skinActorAction(skill){
  return /kiem|sword|slash|tram|water_moon/.test(skill?.anim || '') || /kiem|tram/.test(skill?.id || '') ? 'slash' : 'cast';
}
const SKIN_ACTOR_SHEETS = {
  'img/hero_trucco.png': {id:'trucco', bodyHeight:336, flight:'sword'},
  'img/hero_kimdan.png': {id:'kimdan', bodyHeight:305, flight:'sword'},
  'img/hero_luyenhu.png': {id:'luyenhu', bodyHeight:360, flight:'air'},
  'img/hero_hopthe.png': {id:'hopthe', bodyHeight:259, flight:'air', airSource:'flight'},
  'img/hero_hopthe_nu.png': {id:'hopthe_nu', bodyHeight:290, flight:'air', airSource:'flight', inventorySrc:'img/skin/hopthe_nu_idle.png'}
};
function skinActorFlight(){ return SKIN_ACTOR_SHEETS[skinArt('hero')]?.flight || null; }
// Static poses for the six female realm costumes; skill effects keep their existing timing.
for (const id of ['luyenkhi','trucco','kimdan','nguyenanh','hoathan','luyenhu'])
  SKIN_ACTOR_SHEETS[`img/hero_${id}_nu.png`] = {id:`${id}_nu`, static:true, flight:['hoathan','luyenhu'].includes(id) ? 'air' : null};
function skinActorLoop(action, t){
  const count = SKIN_ACTOR_SHEETS[skinArt('hero')] ? 4 : action === 'run' ? 7 : 8;
  const rate = action === 'run' ? (skinActorFlight() ? 4 : 10) : 2.5;
  return (t * rate % count) / count;
}
function drawSkinActor(g, action, progress, x, ground, height, alpha = 1){
  const hero = skinArt('hero'), profile = SKIN_ACTOR_SHEETS[hero];
  if (profile?.static){
    const casting = (action === 'cast' || action === 'slash') && progress > .15;
    const source = casting ? `img/skin/${profile.id}_cast.png` : skinArt('heroStand');
    const im = skImg(source);
    if (!im) return false;
    const width = height * im.naturalWidth / im.naturalHeight;
    g.save(); g.globalAlpha *= alpha; g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
    g.drawImage(im,x-width/2,ground-height,width,height); g.restore();
    return true;
  }
  if (!profile && hero !== 'img/hero_luyenkhi.png') return false;
  const rows = {idle:0, walk:0, run:1, groundRun:1, slash:2, cast:3};
  const counts = {idle:8, walk:8, run:7, slash:5, cast:3};
  const count = profile ? (rows[action] === undefined ? 0 : 4) : counts[action];
  const flying = profile?.flight && action === 'run';
  const stationaryHover = flying && profile.flight === 'air';
  const flightSheet = flying && (!stationaryHover || profile.airSource === 'flight');
  const image = skImg(profile ? `img/skin/${profile.id}_${flightSheet ? 'flight' : 'actions'}.png` : `img/skin/luyenkhi_${action === 'idle' ? 'walk' : action}_v2.png`);
  if (!image || !count) return false;
  const frame = stationaryHover ? 0 : Math.min(count - 1, Math.max(0, Math.floor(progress * count)));
  const scale = height / (profile?.bodyHeight || 210);
  const fw = profile ? 480 : 384, fh = profile ? 440 : 320;
  const ax = profile ? 240 : 192, ay = flying && profile.flight === 'sword' ? 380 : profile ? 420 : 308;
  g.save(); g.globalAlpha *= alpha;
  g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high';
  g.drawImage(image, frame * fw, profile && !flying ? rows[action] * fh : 0, fw, fh, x - ax * scale, ground - ay * scale, fw * scale, fh * scale);
  g.restore();
  return true;
}
function skImg(src){ if (!skImgs[src]){ const im = new Image(); im.src = src; skImgs[src] = im; } const im = skImgs[src]; return im.complete && im.naturalWidth ? im : null; }
const FIRE = ['#5a1410','#b02a1a','#e0521e','#f08a2a','#f8d25a','#fff4d0'];
function skFrame(now){
  requestAnimationFrame(skFrame);
  if (paneOff('skill')) return;
  const c = $('skCanvas'); if (!c || !c.offsetParent || !S) return;
  if (!c._sized){ const w = c.parentElement.getBoundingClientRect().width; if (w){ const sc = Math.max(1, Math.floor(w / 120)); c.style.width = 120 * sc + 'px'; c.style.height = 60 * sc + 'px'; c._sized = true; } }
  const g = c.getContext('2d'); g.imageSmoothingEnabled = false;
  const s = skById(skSel), el = elOf(s.element), open = el.open;
  const res = open && (['hoa','moc','tho','loi','bang','gio'].includes(s.element) || s.anim === 'water_dragon' || s.anim === 'water_prison' || s.anim === 'water_arrows' || s.anim === 'water_moon' || s.anim === 'water_ball') ? HC_RES : 1;
  if (c.width !== 120 * res){ c.width = 120 * res; c.height = 60 * res; }
  g.setTransform(res, 0, 0, res, 0, 0); g.imageSmoothingEnabled = false;
  const fps = 8, cycle = 12, t = now / 1000, f = Math.floor(t * fps) % cycle;    // 8 khung + 4 khung nghỉ
  const P = (x, y, col, a = 1) => { g.globalAlpha = a; g.fillStyle = col; g.fillRect(Math.round(x), Math.round(y), 1, 1); };
  g.clearRect(0, 0, 120, 60);
  g.fillStyle = '#0d0a08'; g.fillRect(0, 0, 120, 60);
  for (let x = 0; x < 120; x++) P(x, 52, '#2a221a'), P(x, 53, '#1a140f');
  const hero = skImg(skinArt('hero').replace('.png', '_48.png')), mon = skImg('img/quai_yeu_lang_48.png'), sheet = skImg('img/fx_hoa_cau.png');
  if (open && typeof fireFxAvailable === 'function' && fireFxAvailable(s.id, S.realm)){
    if (!drawSkinActor(g,skinActorAction(s),Math.min(1,((now-skAnimT0)%2600)/1200),26,52,46) && hero) g.drawImage(hero,2,6);
    if (mon) g.drawImage(mon,72,6);
    const progress=((now-skAnimT0)%2600)/2200;
    drawFireSkillFx(g,s.id,Math.min(1,progress),S.realm,{handX:38,handY:28,targetX:94,targetY:30,groundY:52,heroHeight:46,targetWidth:48,targetHeight:48,reducedMotion:matchMedia('(prefers-reduced-motion: reduce)').matches});
    return;
  }
  if (open && s.id === 'hoa_cau'){ hcPreview(g, now - skAnimT0, hero, mon, P); return; }
  if (open && SK_FX_ROW[s.id] !== undefined){ skFlip(g, now - skAnimT0, s); return; }
  if (open && s.category === 'Bị động'){ skPassiveFx(g, now, s); return; }
  const cast = open && f >= 1 && f <= 3 ? 1 : 0;
  if (!drawSkinActor(g,skinActorAction(s),open ? Math.min(1,f/7) : 0,26,52,46) && hero) g.drawImage(hero, 2 + cast, 6);
  if (mon) g.drawImage(mon, 72, 6);
  if (!open){ g.globalAlpha = .6; g.fillStyle = '#000'; g.fillRect(0, 0, 120, 60); g.globalAlpha = 1; return; }
  const wet = s.element === 'thuy';
  const FX = (i, cx, cy) => { if (!sheet) return; if (wet) g.filter = WET_FILTER; g.drawImage(sheet, (i % 4) * 48, Math.floor(i / 4) * 48, 48, 48, Math.round(cx - 24), Math.round(cy - 24), 48, 48); g.filter = 'none'; };
  const handX = 36, handY = 28, tx = 94, ty = 34;
  const k = s.anim;
  if (wet && k === 'water_dragon'){                                              // Thương Hải Quyết: thủy long theo bản thiết kế
    const sp = (t * fps % cycle) / 8;
    if (sp < 1) tlDraw(g, sp, 50, 94, 30, 46);
    return;
  }
  if (wet && k === 'water_moon'){                                               // Thủy Nguyệt Trảm: trăng nước theo bản thiết kế
    const sp = (t * fps % cycle) / 8;
    if (sp < 1) tngDraw(g, sp, 44, 96, 30, 46);
    return;
  }
  if (wet && k === 'water_arrows'){                                             // Vạn Thủy Tiễn: mưa tên nước theo bản thiết kế
    const sp = (t * fps % cycle) / 8;
    if (sp < 1) ttienDraw(g, sp, 44, 96, 30, 46);
    return;
  }
  if (wet && k === 'water_prison'){                                             // Thủy Lao Bạo: lao cầu theo bản thiết kế
    const sp = (t * fps % cycle) / 8;
    if (sp < 1) tlaoDraw(g, sp, 96, 30, 46);
    return;
  }
  if (wet && k === 'water_ball'){                  // đòn thường hệ Thủy theo bản thiết kế
    const sp = (t * fps % cycle) / 8;
    if (sp < 1) tbDraw(g, k, sp, 40, 28, 94, 32, 46, t);
    return;
  }
  if (f < 8){
    if (['bolt','dragon'].includes(k)){
      if (f <= 2) FX(f, handX, handY);
      else if (f <= 4){ const x = handX + (f - 2) * 18; FX(f, x, handY + 2); if (k === 'dragon') for (let i = 0; i < 14; i++) P(x - 10 - i, handY + 2 + Math.round(Math.sin(i * .8 + t * 10) * 3), FIRE[2 + (i % 3)]); }
      else FX(f, tx, ty);
    } else if (['burst','pillar','ultimate'].includes(k)){
      if (f <= 2) FX(f, handX, handY);
      else {
        if (k === 'ultimate'){ g.globalAlpha = .18 + .06 * (f % 2); g.fillStyle = '#1a4ab0'; g.fillRect(0, 0, 120, 52); g.globalAlpha = 1; for (let i = 0; i < 10; i++) P((i * 13 + f * 7) % 120, (f - 3) * 9 % 50, FIRE[3 + (i % 3)]); }
        if (k === 'pillar') for (let y = 52; y > 52 - (f - 2) * 9; y--) for (let dx = -3; dx <= 3; dx++) P(tx + dx, y, FIRE[Math.max(1, 5 - Math.abs(dx) - (y % 3 ? 0 : 1))]);
        FX(Math.min(7, f + 3), tx, ty);
      }
    } else if (['shield','aura','root'].includes(k)){
      const r = Math.min(1, f / 4), n = 28;
      for (let i = 0; i < n; i++){
        const a = i / n * Math.PI * 2 + t * 3;
        if (k === 'shield' && Math.cos(a) < .2) continue;
        const rx = (k === 'root' ? 10 : 15) * r, ry = (k === 'root' ? 5 : 20) * r;
        const x = 26 + Math.cos(a) * rx, y = (k === 'root' ? 50 : 30) + Math.sin(a) * ry;
        P(x, y, FIRE[2 + (i + f) % 4], f >= 6 ? 1 - (f - 5) / 3 : 1);
        if (k === 'root') P(x, y - 1, '#6fe0e0', .8);
      }
      if (k === 'root') FX(Math.min(3, f), 26, 26);
    } else if (['bind','field'].includes(k)){
      if (f <= 2) FX(f, handX, handY);
      const r = Math.max(0, Math.min(1, (f - 2) / 3));
      for (let i = 0; i < 30; i++){
        const a = i / 30 * Math.PI * 2 + t * (k === 'bind' ? 4 : 1);
        P(tx + Math.cos(a) * 18 * r, (k === 'bind' ? 34 : 50) + Math.sin(a) * (k === 'bind' ? 12 : 4) * r, i % 3 ? FIRE[3] : FIRE[4], f >= 7 ? .5 : 1);
      }
      if (k === 'field' && f >= 4) for (let i = 0; i < 5; i++) FX(1, tx - 16 + i * 8, 46);
    } else if (k === 'dash'){
      const x = Math.min(40, f * 7);
      for (let i = 0; i < x; i++) for (let d = -1; d <= 1; d++) P(6 + i, 40 + d * 3 + Math.round(Math.sin(i + t * 20)), FIRE[1 + ((i + f) % 4)], i / x);
      if (hero && f > 0){ g.globalAlpha = .55; g.drawImage(hero, 2 + x, 6); g.globalAlpha = 1; }
    }
  }
  g.globalAlpha = 1;
}
requestAnimationFrame(skFrame);

function initSkill(){
  $('pane-skill').addEventListener('click', e => {
    const b = e.target.closest('[data-el]');
    if (b){ if (b.dataset.el !== skEl){ skEl = b.dataset.el; skFade = 1; try { localStorage.setItem('van-dao-sk-el', skEl); } catch(err){} hideTip(); renderSkill(); } return; }
    const u = e.target.closest('[data-up]');
    if (u){ if (!u.disabled){ hideTip(); act('skill/' + u.dataset.up); } return; }
    const n = e.target.closest('[data-sk]');
    if (n){
      skAnimT0 = performance.now(); skSel = n.dataset.sk;
      try { localStorage.setItem('van-dao-sk-sel', skSel); } catch(err){}
      // Giữ nguyên node để cú nhấp thứ hai vẫn phát dblclick / trang bị.
      $('pane-skill').querySelectorAll('.sk-n[data-sk]').forEach(node => {
        const selected = node.dataset.sk === skSel;
        node.classList.toggle('sel', selected); node.setAttribute('aria-pressed', String(selected));
      });
      return;
    }
    const ac = e.target.closest('[data-activate]'); if (ac){ hideTip(); act(`manual/${ac.dataset.activate}`); return; }   // kích hoạt công pháp hệ đang xem
    const bs = e.target.closest('[data-basic]'); if (bs){ act(`skillslot/0?skill=${bs.dataset.basic}`); return; }
    if (e.target.closest('[data-anim]')){ skAnimT0 = performance.now(); return; }          // phát lại animation từ đầu
    const eq = e.target.closest('[data-equip-sk]');
    if (eq){
      const sk = skById(eq.dataset.equipSk), slots = S.skillSlots;
      const i = SLOT_IDX.find(i => fitsSlot(sk, i));                    // mỗi loại chỉ có 1 ô: đặt thẳng (thay chiêu cũ)
      if (i !== undefined){ skPick = null; act(`skillslot/${i}?skill=${sk.id}`); }
      else { skPick = skPick === sk.id ? null : sk.id; renderSkill(); }        // hết ô trống: chọn ô để thay
      return;
    }
    const un = e.target.closest('[data-unslot]'); if (un){ act(`skillslot/${un.dataset.unslot}`); return; }
    const sl = e.target.closest('[data-slot]');
    if (sl){
      const i = +sl.dataset.slot;
      if (skPick && fitsSlot(skById(skPick), i)){ const id = skPick; skPick = null; act(`skillslot/${i}?skill=${id}`); return; }
      const cur = S.skillSlots[i];
      if (cur){ skSel = cur; renderSkill(); }
    }
  });
  // nhấp đúp kỹ năng chủ động đã lĩnh ngộ: dùng trong trận (đang dùng thì tháo)
  $('pane-skill').addEventListener('dblclick', e => {
    const n = e.target.closest('.sk-n[data-sk], .sk2-ico.slot[data-sk]'); if (!n) return;
    const sk = skById(n.dataset.sk); if (!sk || skTree(sk) !== 'active' || skLv(sk.id) < 1) return;
    act(S.skillSlots?.[1] === sk.id ? 'skillslot/1' : `skillslot/1?skill=${sk.id}`);
  });
  // chuột phải: chọn đòn thường · kỹ năng chủ động đã lĩnh ngộ vào ô (đang dùng thì tháo) · chuột phải ô kỹ năng để tháo
  $('pane-skill').addEventListener('contextmenu', e => {
    const bs = e.target.closest('[data-basic]');
    if (bs){ e.preventDefault(); hideTip(); if (!bs.disabled && skBasicId() !== bs.dataset.basic) act(`skillslot/0?skill=${bs.dataset.basic}`); return; }
    const sl = e.target.closest('.sk-slot[data-slotsk], .sk2-ico.slot[data-slotsk]');
    if (sl){ e.preventDefault(); hideTip(); act(`skillslot/${sl.dataset.slot}`); return; }
    const n = e.target.closest('.sk-n[data-sk]'); if (!n) return;
    e.preventDefault();
    const sk = skById(n.dataset.sk); if (!sk || skTree(sk) !== 'active' || skLv(sk.id) < 1) return;
    hideTip(); skPick = null;
    act(S.skillSlots?.[1] === sk.id ? 'skillslot/1' : `skillslot/1?skill=${sk.id}`);
  });
  // kéo nút kỹ năng đã học vào ô, kéo kỹ năng trong ô ra ngoài để tháo
  let dragSk = null;
  const pane = $('pane-skill');
  pane.addEventListener('dragstart', e => {
    const n = e.target.closest('[data-sk]'), sl = e.target.closest('[data-slotsk]');
    dragSk = sl ? {id: sl.dataset.slotsk, from: +sl.dataset.slot} : n && skLv(n.dataset.sk) > 0 ? {id: n.dataset.sk, from: -1} : null;
    if (!dragSk){ e.preventDefault(); return; }
    e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', dragSk.id); hideTip();
  });
  pane.addEventListener('dragover', e => { const sl = e.target.closest('[data-slot]'); if (dragSk && sl && fitsSlot(skById(dragSk.id), +sl.dataset.slot)) e.preventDefault(); });
  pane.addEventListener('drop', e => {
    const sl = e.target.closest('[data-slot]'); if (!dragSk || !sl) return;
    e.preventDefault(); act(`skillslot/${sl.dataset.slot}?skill=${dragSk.id}`); dragSk = null;
  });
  pane.addEventListener('dragend', e => { if (dragSk && dragSk.from >= 0 && e.dataTransfer.dropEffect === 'none') act(`skillslot/${dragSk.from}`); dragSk = null; });
}

function renderLog(){
  const t = iso => { const d = new Date(iso); return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`; };
  $('log').innerHTML = '<h3>Nhật ký tu hành</h3>' + S.logs.map(l => `<p class="${l.kind}"><time>${t(l.at)}</time>${qColorize(fmtNumberText(window.tr(l.text)))}</p>`).join('');
}
function advState(){
  if (S.isPeak && !S.hasNext) return {ok:false, label:'Đỉnh phong hiện tại'};
  if (qiNow() < S.qiReq) return {ok:false, label:'Tu vi chưa đủ'};
  if (!hasAll(S.targetCost)) return {ok:false, label:'Thiếu vật liệu'};
  return {ok:true, label: 'Sẵn sàng'};
}
function live(){
  if (!S) return;
  const q = qiNow(), rq = S.qiReq, full = q >= rq;
  $('qiBar').firstElementChild.style.width = Math.min(100, q/rq*100) + '%';
  $('qiBar').classList.toggle('full', full);
  setQiDisplay($('qiText'), q, rq);
  if (MODE === 'strip'){ $('sStage').textContent = S.stageName; setQiDisplay($('qiBarTxt'), q, rq); $('qiBar').dataset.tip = `Tu vi${full ? ' · bình cảnh' : ''}&#10;${fmtFull(q)} / ${fmtFull(rq)} · ${fmtDecimal(Math.round(S.rate*10)/10)}/giây`.replace('&#10;', '\n'); }
  $('qiLabel').textContent = full ? 'Tu vi · bình cảnh' : 'Tu vi';
  const rq2 = $('reqQi'); if (rq2){ setQiDisplay(rq2, q, rq); rq2.classList.toggle('ok', full); }
  const a = advState(), b = $('btnAdvance');
  const b2 = $('btnAdvance2');
  if (b2){ b2.disabled = !a.ok || busy; b2.classList.toggle('ready', a.ok); $('advHint2').textContent = a.label; }
  b.disabled = false; b.classList.toggle('ready', a.ok);            // luôn bấm được: chỉ mở tab Đột phá, sáng lên khi đủ điều kiện
  b.dataset.tip = `${S.isPeak && S.hasNext ? 'Đột phá cảnh giới' : 'Đột phá'}: ${S.targetTitle}\n${a.label}`;
  const st = S.stats, bt = S.battle;
  $('btnBag').dataset.tip = `Hành trang\nTấn công ${fmt(st.atk)} · Máu ${fmt(st.hp)} · Phòng thủ ${fmt(st.def)}`;
  // thanh thám hiểm: luôn tự động, rê chuột để xem chi tiết chuyến
  const trip = $('trip'), left = exploreLeft();
  trip.dataset.tip = `${S.map.area > DATA.areas ? 'Khiêu chiến Boss · ' + S.map.bossLair : 'Ải ' + (S.map.diff ? MAP_DIFFS[S.map.diff] + ' ' : '') + (S.map.realm + 1) + '-' + S.map.area + ' · ' + S.map.areas[S.map.area - 1].place}\nMỗi ${(S.exploreCooldownMs/1000).toFixed(1).replace('.0','')}s một chuyến · ${bt.win ? `hạ ${(bt.waves || []).map(w => w.name + (w.tier === 'elite' ? ' (tinh anh)' : '')).join(', ') || bt.monster}` : `thua ${(bt.waves || []).at(-1)?.name || bt.monster}, cần mạnh hơn`}\nThắng: +${fmt(S.exploreQiGain)} tu vi và vật phẩm rơi theo tỉ lệ`;
  { const f = mapFrontier(); if (f.knocked) trip.dataset.tip += `\n⚑ Ải cao nhất ${S.map.realm + 1}-${f.top}: thua nên lùi về ${S.map.realm + 1}-${S.map.area}. Mở Bản đồ để thử lại.`; }
  { const nb = $('btnNext'), f = mapFrontier(), R = S.map.realm + 1, a = S.map.area, boss = a > DATA.areas, can = !boss && a < f.top;
    nb.disabled = !can || busy; nb.classList.toggle('knock', can && f.knocked);
    nb.dataset.tip = boss ? 'Ải tiếp theo\nĐang khiêu chiến Boss' : can ? `Sang ải ${R}-${a + 1}${f.knocked ? ' (thử lại)' : ''}\nẢi cao nhất ${R}-${f.top}` : `Ải tiếp theo\nĐang ở ải cao nhất ${R}-${f.top}, thắng ải này để mở ải kế`; }
  { const bb = $('btnBoss'), m = S.map, N = DATA.areas, R = m.realm + 1, open = m.areaMax >= N, inBoss = m.area > N,
      keyN = S.inventory[m.keyItem] || 0, key = ITEM[m.keyItem]?.name || 'lệnh Boss', ok = keyN > 0 || adminBoss();
    bb.style.display = open ? '' : 'none';                                           // chỉ hiện khi ải cao nhất đã là x-15
    bb.disabled = !open || inBoss || !ok || busy;
    bb.dataset.tip = inBoss ? `Khiêu chiến Boss\nĐang đánh Boss ${m.boss}` : !ok ? `Khiêu chiến Boss ${m.boss}\nCần ${key} (đang có 0)`
      : `Khiêu chiến Boss ${m.boss}\nVào thẳng ải Boss chương ${R}, không cần mở Bản đồ\n${adminBoss() ? 'Admin: không tốn lệnh' : `Mỗi trận tốn 1 ${key} · còn ${fmt(keyN)}`}`; }
  trip.querySelector('i').style.width = Math.min(100, 100 - left / S.exploreCooldownMs * 100) + '%';
  $('tripText').textContent = left > 0 ? `${S.map.area > DATA.areas ? 'Boss' : 'Ải ' + (S.map.diff ? MAP_DIFFS[S.map.diff] + ' ' : '') + (S.map.realm + 1) + '-' + S.map.area + (mapFrontier().knocked ? ' ↓' : '')} · ${Math.ceil(left/1000)}s` : 'Đang về…';
  if (left <= 0 && performance.now() - lastPull > 1500){ lastPull = performance.now(); sync(); }
  liveInv();
  liveDan(); liveBreak(); liveSkill();
  if (tipEl && !tipEl.hidden) showTip(tipEl._for);
}
// Tab ẩn không vẽ lại mỗi lần đồng bộ (trước đây vẽ hết mọi tab: 50–85 ms mỗi 4 giây khi túi đầy → khựng hình).
// Tab ẩn được đánh dấu "bẩn" và vẽ lại ngay khi mở (openTab gọi renderPane).
const PANE_RENDER = {req: () => renderReq(), inv: () => renderInv(), craft: () => renderCraft(), fuse: () => renderFuse(), sell: () => renderSell(),
  spin: () => renderSpin(), map: () => renderMap(), skill: () => renderSkill(), admin: () => renderAdmin(), pet: () => renderPet()};
const paneDirty = new Set();
function renderPane(id){ if (S && paneDirty.delete(id)) PANE_RENDER[id]?.(); }
function renderAll(){
  if (typeof renderConceptNavigation === 'function') renderConceptNavigation();
  $('hudRealm').textContent = S.realmName;
  $('sRealm').textContent = S.realmName;
  $('sStage').textContent = `${S.stageName} · ${fmtDecimal(Math.round(S.rate*10)/10)}/giây`;
  $('hudStage').textContent = S.stageName;
  $('hudRate').textContent = `${fmtDecimal(Math.round(S.rate*10)/10)} tu vi/giây`;
  const tipKey = tipEl && !tipEl.hidden && tipEl._for ? (tipEl._for.id || tipEl._for.dataset.equip || tipEl._for.dataset.unequip || '') : '';
  for (const [id, fn] of Object.entries(PANE_RENDER)){ if (paneOff(id)) paneDirty.add(id); else { paneDirty.delete(id); fn(); } }
  renderLog(); live();
  if (tipKey){ const el = document.getElementById(tipKey) || document.querySelector(`[data-equip="${tipKey}"],[data-unequip="${tipKey}"]`); if (el && el.matches(':hover')) showTip(el); }
}
