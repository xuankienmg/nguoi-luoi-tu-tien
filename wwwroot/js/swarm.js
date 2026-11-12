/* Vấn Đạo · js/swarm.js: mini game "Yêu Triều" — quái tràn từ mọi hướng. Chạy hoàn toàn ở client; logic thuần ở js/swarm-core.js. */
(function () {
  const C = window.SwarmCore;
  const BEST_KEY = 'van-dao-swarm-best';
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const loadBest = () => { try { return JSON.parse(localStorage.getItem(BEST_KEY)) || { wave: 0, kills: 0, time: 0 }; } catch (e) { return { wave: 0, kills: 0, time: 0 }; } };
  const saveBest = b => { try { localStorage.setItem(BEST_KEY, JSON.stringify(b)); } catch (e) {} };
  const imgCache = {};
  const getImg = src => { if (!src) return null; if (!imgCache[src]) { const i = new Image(); i.src = src; imgCache[src] = i; } const i = imgCache[src]; return i.complete && i.naturalWidth ? i : null; };
  const fmtTime = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

  let overlay, canvas, g, hud, levelBox, endBox, game = null, raf = 0, last = 0;
  const keys = new Set();
  const stick = { on: false, ox: 0, oy: 0, dx: 0, dy: 0, id: null };

  /** Ảnh quái: các ải của chương đang chơi; mạnh dần theo đợt, Boss dùng ảnh Boss chương. */
  function monsterPool() {
    const areas = (typeof S !== 'undefined' && S?.map?.areas || []).filter(a => a.image);
    return { areas, boss: (typeof S !== 'undefined' && S?.map?.bossImage) || '' };
  }
  function pickMonsterImage(pool, wave, rng) {
    if (!pool.areas.length) return '';
    const tier = Math.min(pool.areas.length - 1, Math.floor((wave - 1) / 2));
    const lo = Math.max(0, tier - 2);
    return pool.areas[lo + Math.floor(rng() * (tier - lo + 1))].image;
  }

  function newGame() {
    const p = Object.assign(C.BASE_PLAYER(), { x: 0, y: 0, hp: 0, inv: 0, face: 1, xp: 0, level: 1, cd: 0, boltCd: 1.2, novaCd: 2, orbA: 0 });
    p.hp = p.maxHp;
    return { t: 0, p, mons: [], shots: [], gems: [], pops: [], fx: [], chests: [], kills: 0, levels: { sword: 1 }, spawnAcc: 0, bossWave: 0, paused: false, over: false, pool: monsterPool(), grid: new C.Grid(64), w: 800, h: 600 };
  }

  function resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const r = canvas.getBoundingClientRect();
    canvas.width = Math.max(1, Math.round(r.width * dpr)); canvas.height = Math.max(1, Math.round(r.height * dpr));
    if (game) { game.w = r.width; game.h = r.height; }
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function spawnMonster(boss) {
    const m = boss ? C.bossStats(C.waveOf(game.t)) : C.monsterStats(C.waveOf(game.t));
    const pt = C.spawnPoint(game.p.x, game.p.y, game.w, game.h, Math.random);
    const wave = C.waveOf(game.t);
    game.mons.push({ x: pt.x, y: pt.y, hp: m.hp, maxHp: m.hp, speed: m.speed * (boss ? 1 : 0.85 + Math.random() * 0.3), dmg: m.dmg, xp: m.xp, boss, r: boss ? 46 : 20, face: 1, hit: 0,
      img: boss ? game.pool.boss : pickMonsterImage(game.pool, wave, Math.random), hue: Math.floor(Math.random() * 360) });
  }

  /** Gây sát thương (có bạo kích) và hiện số. */
  function hurt(m, base, col) {
    const crit = Math.random() < game.p.crit, d = Math.round(base * (crit ? 2 : 1));
    m.hp -= d; m.hit = 0.1;
    game.pops.push({ x: m.x, y: m.y - m.r, text: crit ? d + '!' : String(d), life: 0.5, col: crit ? '#ffb347' : col });
  }

  /** Kỹ năng chủ động: hỏa cầu xoay, lôi kích, hàn băng chấn, hồi xuân. */
  function skills(dt) {
    const s = game, p = s.p;
    if (p.regen) p.hp = Math.min(p.maxHp, p.hp + p.regen * dt);
    if (p.orbit) {
      p.orbA += dt * 3.4;
      for (let i = 0; i < p.orbit; i++) {
        const a = p.orbA + i * Math.PI * 2 / p.orbit, ox = p.x + Math.cos(a) * p.orbitR, oy = p.y + Math.sin(a) * p.orbitR;
        for (const m of s.grid.query(ox, oy, 14)) if (!(m.orbCd > 0)) { m.orbCd = 0.45; hurt(m, p.damage * p.orbitDmg, '#ff9a5a'); }
      }
      for (const m of s.mons) if (m.orbCd > 0) m.orbCd -= dt;
    }
    if (p.bolt) {
      p.boltCd -= dt;
      if (p.boltCd <= 0) {
        p.boltCd = p.boltEvery;
        const near = s.grid.query(p.x, p.y, Math.max(p.range, 320));
        for (let i = 0; i < p.bolt && near.length; i++) {
          const t = near.splice(Math.floor(Math.random() * near.length), 1)[0];
          s.fx.push({ type: 'bolt', x: t.x, y: t.y, life: 0.28 });
          for (const m of s.grid.query(t.x, t.y, p.boltSplash)) hurt(m, p.damage * 2.4, '#d6c8ff');
        }
      }
    }
    if (p.nova) {
      p.novaCd -= dt;
      if (p.novaCd <= 0) {
        p.novaCd = 4; const R = (120 + p.nova * 22) * p.novaRange;
        s.fx.push({ type: 'nova', x: p.x, y: p.y, r: R, life: 0.45 });
        for (const m of s.grid.query(p.x, p.y, R)) {
          hurt(m, p.damage * p.novaDmg, '#9fe8ff'); if (p.novaSlow) m.slow = p.novaSlow;
          const d = Math.hypot(m.x - p.x, m.y - p.y) || 1; m.x += (m.x - p.x) / d * (m.boss ? 20 : p.novaKb); m.y += (m.y - p.y) / d * (m.boss ? 20 : p.novaKb);
        }
      }
    }
    for (const f of s.fx) f.life -= dt; s.fx = s.fx.filter(f => f.life > 0);
  }

  function update(dt) {
    const s = game, p = s.p;
    s.t += dt;
    const wave = C.waveOf(s.t);
    // vào đợt Boss: sinh 1 Boss
    if (C.isBossWave(wave) && s.bossWave !== wave) { s.bossWave = wave; spawnMonster(true); }
    s.spawnAcc += C.spawnRate(wave) * dt;
    while (s.spawnAcc >= 1) { s.spawnAcc -= 1; if (s.mons.length < C.MAX_ALIVE) spawnMonster(false); }

    // di chuyển nhân vật
    let dx = (keys.has('d') || keys.has('arrowright') ? 1 : 0) - (keys.has('a') || keys.has('arrowleft') ? 1 : 0) + stick.dx;
    let dy = (keys.has('s') || keys.has('arrowdown') ? 1 : 0) - (keys.has('w') || keys.has('arrowup') ? 1 : 0) + stick.dy;
    const len = Math.hypot(dx, dy); if (len > 1) { dx /= len; dy /= len; }
    p.x += dx * p.speed * dt; p.y += dy * p.speed * dt; if (dx) p.face = dx > 0 ? 1 : -1;
    p.inv = Math.max(0, p.inv - dt);

    // quái lao về nhân vật + chạm
    s.grid.clear();
    for (const m of s.mons) {
      const ax = p.x - m.x, ay = p.y - m.y, d = Math.hypot(ax, ay) || 1;
      const sp = m.slow > 0 ? m.speed * 0.5 : m.speed; m.slow -= dt; m.x += ax / d * sp * dt; m.y += ay / d * sp * dt; m.face = ax > 0 ? 1 : -1; m.hit = Math.max(0, m.hit - dt);
      s.grid.insert(m);
      if (d < m.r + 11 && p.inv <= 0) { const dm = Math.max(1, Math.round(m.dmg * (1 - p.armor))); p.hp -= dm; p.inv = 0.6; s.pops.push({ x: p.x, y: p.y - 40, text: '-' + dm, life: 0.7, col: '#ff6b5a' }); }
    }
    if (p.hp <= 0) return endGame();

    // phi kiếm tự bắn vào quái gần nhất
    p.cd -= dt;
    if (p.cd <= 0) {
      const near = s.grid.query(p.x, p.y, p.range).sort((a, b) => Math.hypot(a.x - p.x, a.y - p.y) - Math.hypot(b.x - p.x, b.y - p.y));
      if (near.length) {
        p.cd = p.cooldown;
        for (let i = 0; i < p.swords; i++) {
          const t = near[Math.min(i, near.length - 1)], a = Math.atan2(t.y - p.y, t.x - p.x) + (near.length < p.swords ? (i - (p.swords - 1) / 2) * 0.18 : 0);
          s.shots.push({ x: p.x, y: p.y, vx: Math.cos(a) * 560, vy: Math.sin(a) * 560, a, life: p.range / 560 + 0.35, hit: new Set() });
        }
      }
    }
    for (const b of s.shots) {
      b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt;
      for (const m of s.grid.query(b.x, b.y, 10)) {
        if (b.hit.has(m) || m.hp <= 0) continue;
        b.hit.add(m); hurt(m, p.damage * p.swordDmg, '#ffe9a8');
        if (b.hit.size > p.pierce) { b.life = 0; break; }
      }
    }
    skills(dt);
    s.shots = s.shots.filter(b => b.life > 0);

    // quái chết → rơi tinh thể
    for (const m of s.mons) if (m.hp <= 0) { s.kills++; s.gems.push({ x: m.x, y: m.y, v: m.xp }); if (m.boss) { s.chests.push({ x: m.x, y: m.y }); s.pops.push({ x: m.x, y: m.y - 70, text: 'Boss rơi rương!', life: 1.4, col: '#f2d68a' }); } }
    s.mons = s.mons.filter(m => m.hp > 0);

    // nhặt tinh thể
    for (const gem of s.gems) {
      const d = Math.hypot(p.x - gem.x, p.y - gem.y);
      if (d < p.magnet) { gem.x += (p.x - gem.x) / d * 380 * dt; gem.y += (p.y - gem.y) / d * 380 * dt; }
      if (d < 16) { p.xp += gem.v; gem.v = 0; }
    }
    s.gems = s.gems.filter(x => x.v > 0);
    if (s.gems.length > 400) s.gems.splice(0, s.gems.length - 400);
    for (const o of s.pops) o.life -= dt; s.pops = s.pops.filter(o => o.life > 0);

    // nhặt rương nâng cấp do Boss rơi: mở bảng chọn thần thông
    for (let i = 0; i < s.chests.length && !s.paused; i++) if (Math.hypot(p.x - s.chests[i].x, p.y - s.chests[i].y) < 36) { s.chests.splice(i, 1); openLevelUp('Rương nâng cấp! Chọn một phần thưởng'); }
    if (!s.paused && p.xp >= C.xpNeed(p.level)) { p.xp -= C.xpNeed(p.level); p.level++; openLevelUp(); }
  }

  // ---- vẽ ----
  const heroBuf = document.createElement('canvas'); heroBuf.width = heroBuf.height = 320; const hg = heroBuf.getContext('2d');
  function drawHero(x, y, size, t, face, blink) {
    let ok = false;
    try {
      const key = typeof skinArt === 'function' ? skinArt('hero') : '';
      if (key && typeof drawInventorySprite === 'function') { hg.clearRect(0, 0, 320, 320); ok = drawInventorySprite(hg, key, t, false); }
      if (ok) { g.save(); g.translate(x, y); g.scale(face, 1); if (blink) g.globalAlpha = .45; g.drawImage(heroBuf, -size / 2, -size + 8, size, size); g.restore(); return; }
      const st = key && getImg(key); if (st) { g.save(); g.translate(x, y); g.scale(face, 1); if (blink) g.globalAlpha = .45; g.drawImage(st, -size / 2, -size + 8, size, size * st.naturalHeight / st.naturalWidth); g.restore(); return; }
    } catch (e) {}
    g.fillStyle = blink ? 'rgba(143,208,201,.45)' : '#8fd0c9'; g.beginPath(); g.arc(x, y - size / 3, size / 4, 0, 7); g.fill();
  }

  function render() {
    const s = game, p = s.p, W = s.w, H = s.h, cx = W / 2 - p.x, cy = H / 2 - p.y;
    g.fillStyle = '#0d1a1c'; g.fillRect(0, 0, W, H);
    // nền lưới lặp theo camera
    g.strokeStyle = 'rgba(143,208,201,.07)'; g.lineWidth = 1; g.beginPath();
    const step = 80, ox = ((cx % step) + step) % step, oy = ((cy % step) + step) % step;
    for (let x = ox; x < W; x += step) { g.moveTo(x, 0); g.lineTo(x, H); }
    for (let y = oy; y < H; y += step) { g.moveTo(0, y); g.lineTo(W, y); }
    g.stroke();
    g.save(); g.translate(cx, cy);
    for (const c of s.chests) {
      const pulse = 0.5 + 0.5 * Math.sin(s.t * 5), gr = g.createRadialGradient(c.x, c.y, 4, c.x, c.y, 46);
      gr.addColorStop(0, `rgba(255,214,102,${0.55 + pulse * 0.3})`); gr.addColorStop(1, 'rgba(255,214,102,0)');
      g.fillStyle = gr; g.beginPath(); g.arc(c.x, c.y, 46, 0, 7); g.fill();
      g.fillStyle = '#8a5a1e'; g.fillRect(c.x - 16, c.y - 14, 32, 22); g.fillStyle = '#c9a24a'; g.fillRect(c.x - 16, c.y - 14, 32, 8);
      g.fillStyle = '#f2d68a'; g.fillRect(c.x - 3, c.y - 8, 6, 8); g.strokeStyle = '#3a2208'; g.lineWidth = 1.5; g.strokeRect(c.x - 16, c.y - 14, 32, 22);
    }
    for (const gem of s.gems) { g.fillStyle = '#6fe6d6'; g.beginPath(); g.moveTo(gem.x, gem.y - 6); g.lineTo(gem.x + 4, gem.y); g.lineTo(gem.x, gem.y + 6); g.lineTo(gem.x - 4, gem.y); g.fill(); }
    // quái xa → gần (sắp theo y để chồng đúng)
    const vis = s.mons.filter(m => Math.abs(m.x - p.x) < W / 2 + 80 && Math.abs(m.y - p.y) < H / 2 + 80).sort((a, b) => a.y - b.y);
    for (const m of vis) {
      const sz = m.boss ? 150 : 56, im = getImg(m.img);
      g.save(); g.translate(m.x, m.y); g.scale(m.face, 1);
      if (m.hit > 0) g.filter = 'brightness(2)';
      if (im) { const h = sz * im.naturalHeight / im.naturalWidth; g.drawImage(im, -sz / 2, -h + 8, sz, h); }
      else { g.fillStyle = `hsl(${m.hue},55%,45%)`; g.beginPath(); g.arc(0, -m.r, m.r, 0, 7); g.fill(); }
      g.restore(); g.filter = 'none';
      if (m.boss || m.hp < m.maxHp) { const w = m.boss ? 90 : 34; g.fillStyle = 'rgba(0,0,0,.6)'; g.fillRect(m.x - w / 2, m.y + 10, w, 4); g.fillStyle = m.boss ? '#ff5a4a' : '#ff9a8a'; g.fillRect(m.x - w / 2, m.y + 10, w * Math.max(0, m.hp / m.maxHp), 4); }
    }
    for (const f of s.fx) {
      if (f.type === 'nova') { const k = 1 - f.life / 0.45; g.strokeStyle = `rgba(159,232,255,${1 - k})`; g.lineWidth = 6 * (1 - k) + 1; g.beginPath(); g.arc(f.x, f.y, f.r * (0.3 + 0.7 * k), 0, 7); g.stroke(); }
      else { g.strokeStyle = `rgba(214,200,255,${Math.min(1, f.life * 5)})`; g.lineWidth = 3; g.beginPath(); let x = f.x + 14, y = f.y - 320; g.moveTo(x, y); for (let i = 0; i < 8; i++) { y += 40; x = f.x + (i === 7 ? 0 : (Math.random() - .5) * 36); g.lineTo(x, y); } g.stroke(); }
    }
    drawHero(p.x, p.y, 64, s.t, p.face, p.inv > 0 && Math.floor(s.t * 20) % 2 === 0);
    for (let i = 0; i < p.orbit; i++) { const a = p.orbA + i * Math.PI * 2 / p.orbit, ox = p.x + Math.cos(a) * p.orbitR, oy = p.y - 20 + Math.sin(a) * p.orbitR; const gr = g.createRadialGradient(ox, oy, 2, ox, oy, 14); gr.addColorStop(0, '#fff3c4'); gr.addColorStop(.5, '#ff9a3a'); gr.addColorStop(1, 'rgba(255,80,20,0)'); g.fillStyle = gr; g.beginPath(); g.arc(ox, oy, 14, 0, 7); g.fill(); }
    g.fillStyle = '#bff3ff'; g.strokeStyle = '#3fb6d6'; g.lineWidth = 1.5;
    for (const b of s.shots) { g.save(); g.translate(b.x, b.y); g.rotate(b.a); g.beginPath(); g.moveTo(14, 0); g.lineTo(-8, -4); g.lineTo(-4, 0); g.lineTo(-8, 4); g.closePath(); g.fill(); g.stroke(); g.restore(); }
    g.font = '700 14px "Be Vietnam Pro",sans-serif'; g.textAlign = 'center';
    for (const o of s.pops) { g.globalAlpha = Math.min(1, o.life * 2.5); g.fillStyle = o.col; g.fillText(o.text, o.x, o.y - (0.7 - o.life) * 40); }
    g.globalAlpha = 1; g.restore();
    // joystick ảo
    if (stick.on) { g.strokeStyle = 'rgba(255,255,255,.3)'; g.lineWidth = 2; g.beginPath(); g.arc(stick.ox, stick.oy, 50, 0, 7); g.stroke(); g.fillStyle = 'rgba(255,255,255,.35)'; g.beginPath(); g.arc(stick.ox + stick.dx * 50, stick.oy + stick.dy * 50, 20, 0, 7); g.fill(); }
    // HUD
    const need = C.xpNeed(p.level);
    hud.innerHTML = `<div class="sw-bars"><div class="sw-bar hp"><i style="width:${Math.max(0, p.hp / p.maxHp * 100)}%"></i><span>${Math.max(0, Math.ceil(p.hp))}/${p.maxHp}</span></div><div class="sw-bar xp"><i style="width:${p.xp / need * 100}%"></i><span>Cấp ${p.level}</span></div></div>`
      + `<div class="sw-skills">${C.UPGRADES.filter(u => s.levels[u.id]).map(u => `<span>${u.name} ${s.levels[u.id]}</span>`).join('')}</div>`
      + `<div class="sw-info"><b>Đợt ${C.waveOf(s.t)}</b> · ${fmtTime(s.t)} · Hạ ${s.kills}</div>`;
  }

  function loop(ts) {
    raf = requestAnimationFrame(loop);
    const dt = Math.min(0.05, (ts - last) / 1000 || 0); last = ts;
    if (!game || game.over) return;
    if (!game.paused && !document.hidden) update(dt);
    if (game && !game.over) render();
  }

  // ---- nâng cấp / kết thúc ----
  function openLevelUp(title) {
    game.paused = true; stick.on = false; stick.dx = stick.dy = 0; keys.clear();
    const picks = C.pickUpgrades(Math.random, game.levels, 3);
    if (!picks.length) { game.paused = false; return; }
    levelBox.innerHTML = `<h3>${title || 'Đột phá! Chọn một thần thông'}</h3>`;
    picks.forEach((u, i) => {
      const b = el('button', 'sw-card', `<kbd>${i + 1}</kbd><b>${u.name}</b><small>${u.desc}</small><em>${u.req ? 'Nhánh · ' : ''}Cấp ${(game.levels[u.id] || 0) + 1}/${u.max}</em>`);
      b.onclick = () => choose(u); b.dataset.k = String(i + 1); levelBox.appendChild(b);
    });
    levelBox._picks = picks; levelBox.hidden = false;
  }
  function choose(u) { game.levels[u.id] = (game.levels[u.id] || 0) + 1; u.apply(game.p); levelBox.hidden = true; game.paused = false; }

  function endGame() {
    game.over = true;
    const b = loadBest(), isNew = C.waveOf(game.t) > b.wave || (C.waveOf(game.t) === b.wave && game.kills > b.kills);
    if (isNew) saveBest({ wave: C.waveOf(game.t), kills: game.kills, time: Math.floor(game.t) });
    const best = isNew ? { wave: C.waveOf(game.t), kills: game.kills, time: Math.floor(game.t) } : b;
    endBox.innerHTML = `<h3>Đạo tiêu thân vong</h3><p>Sống sót <b>${fmtTime(game.t)}</b> · Đợt <b>${C.waveOf(game.t)}</b> · Hạ <b>${game.kills}</b> yêu</p>`
      + `<p class="sw-best">${isNew ? 'Kỷ lục mới!' : 'Kỷ lục'}: Đợt ${best.wave} · ${fmtTime(best.time)} · ${best.kills} yêu</p>`;
    const again = el('button', 'sw-btn gold', 'Chơi lại'), close = el('button', 'sw-btn', 'Đóng');
    again.onclick = start; close.onclick = closeGame; endBox.append(again, close); endBox.hidden = false;
  }

  function start() { endBox.hidden = true; levelBox.hidden = true; keys.clear(); stick.on = false; stick.dx = stick.dy = 0; game = newGame(); resize(); }

  function openGame() {
    if (!overlay) build();
    overlay.hidden = false; document.body.classList.add('swarm-open'); start();
    last = performance.now(); cancelAnimationFrame(raf); raf = requestAnimationFrame(loop);
  }
  function closeGame() { if (!overlay) return; overlay.hidden = true; document.body.classList.remove('swarm-open'); cancelAnimationFrame(raf); game = null; keys.clear(); }

  function build() {
    overlay = el('div', 'swarm'); overlay.hidden = true; overlay.setAttribute('role', 'dialog'); overlay.setAttribute('aria-label', 'Yêu Triều');
    canvas = el('canvas', 'sw-canvas'); hud = el('div', 'sw-hud'); levelBox = el('div', 'sw-modal'); endBox = el('div', 'sw-modal');
    levelBox.hidden = endBox.hidden = true;
    const x = el('button', 'sw-x', '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>'); x.setAttribute('aria-label', 'Đóng'); x.onclick = closeGame;
    const help = el('div', 'sw-help', 'WASD / mũi tên / kéo chuột để di chuyển · phi kiếm tự đánh');
    overlay.append(canvas, hud, help, x, levelBox, endBox); document.body.appendChild(overlay);
    g = canvas.getContext('2d'); window.addEventListener('resize', () => { if (!overlay.hidden) resize(); });
    window.addEventListener('keydown', e => {
      if (overlay.hidden) return;
      const k = e.key.toLowerCase();
      if (k === 'escape') { closeGame(); e.preventDefault(); return; }
      if (!levelBox.hidden && '123'.includes(k) && levelBox._picks?.[+k - 1]) { choose(levelBox._picks[+k - 1]); return; }
      if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(k)) { keys.add(k); e.preventDefault(); }
    });
    window.addEventListener('keyup', e => keys.delete(e.key.toLowerCase()));
    canvas.addEventListener('pointerdown', e => { const r = canvas.getBoundingClientRect(); stick.on = true; stick.id = e.pointerId; stick.ox = e.clientX - r.left; stick.oy = e.clientY - r.top; stick.dx = stick.dy = 0; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => { if (!stick.on || e.pointerId !== stick.id) return; const r = canvas.getBoundingClientRect(), dx = e.clientX - r.left - stick.ox, dy = e.clientY - r.top - stick.oy, d = Math.hypot(dx, dy) || 1, k = Math.min(1, d / 50); stick.dx = dx / d * k; stick.dy = dy / d * k; });
    const up = e => { if (e.pointerId === stick.id) { stick.on = false; stick.dx = stick.dy = 0; } };
    canvas.addEventListener('pointerup', up); canvas.addEventListener('pointercancel', up);
  }

  function mountButton() {
    const actions = document.querySelector('.actions'); if (!actions || document.getElementById('btnSwarm')) return;
    const b = el('button', 'ib', '<span class="sw-ico" aria-hidden="true">⚔</span>'); b.id = 'btnSwarm'; b.setAttribute('aria-label', 'Yêu Triều'); b.dataset.tip = 'Yêu Triều\nMini game: quái tràn từ mọi hướng';
    b.onclick = openGame; actions.insertBefore(b, document.getElementById('btnLog')?.nextSibling || null);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mountButton); else mountButton();
  // __debug: chỉ để kiểm thử/chạy mô phỏng nhanh; không dùng trong game.
  window.SwarmGame = { open: openGame, close: closeGame, __debug: { step: dt => { if (game && !game.over && !game.paused) update(dt); if (game && !game.over) render(); }, state: () => game, choose: i => levelBox._picks && choose(levelBox._picks[i]) } };
})();
