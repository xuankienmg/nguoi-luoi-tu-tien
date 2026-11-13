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

  let overlay, canvas, g, hud, levelBox, endBox, pauseBox, game = null, raf = 0, last = 0;
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
    return { t: 0, p, mons: [], shots: [], gems: [], pops: [], fx: [], chests: [], heals: [], healCd: C.healDelay(Math.random), eb: [], kills: 0, killsBy: {}, levels: { sword: 1 }, spawnAcc: 0, bossWave: 0, paused: false, over: false, pool: monsterPool(), grid: new C.Grid(64), w: 800, h: 600 };
  }

  function resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const r = canvas.getBoundingClientRect();
    canvas.width = Math.max(1, Math.round(r.width * dpr)); canvas.height = Math.max(1, Math.round(r.height * dpr));
    if (game) { game.w = r.width; game.h = r.height; }
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  /** Sinh quái: loại theo đợt (C.pickMonsterType) hoặc Boss luân phiên (C.bossTypeForWave). at/forceId dùng cho quái do Boss triệu hồi. */
  function spawnMonster(boss, at, forceId, k = 0, share = 1) {
    const wave = C.waveOf(game.t);
    const type = boss ? C.bossTypeForWave(wave, k) : (forceId && C.MONSTER_TYPES.find(t => t.id === forceId)) || C.pickMonsterType(wave, Math.random);
    const base = boss ? C.bossStats(wave) : C.monsterStats(wave);
    const pt = at || C.spawnPoint(game.p.x, game.p.y, game.w, game.h, Math.random), hp = Math.max(1, Math.round(base.hp * type.hp * share));
    game.mons.push({ x: pt.x, y: pt.y, hp, maxHp: hp, speed: base.speed * type.speed * (boss ? 1 : 0.85 + Math.random() * 0.3), dmg: Math.max(1, Math.round(base.dmg * type.dmg)), xp: type.xp,
      boss, type: type.id, name: type.name, ai: type.ai, color: type.color, r: type.r, size: type.size, face: 1, hit: 0, cd: 1 + Math.random() * 2, st: 'walk', tm: 2 + Math.random() * 2, dir: 0, fuse: 0, rot: 0,
      img: boss ? game.pool.boss : pickMonsterImage(game.pool, wave, Math.random), hue: Math.floor(Math.random() * 360) });
  }
  /** Thụ yêu mọc thành vòng quanh nhân vật (trong tầm nhìn), cách 230–420px. */
  function spawnTrees(wave) {
    const p = game.p, n = C.treeCount(wave), a0 = Math.random() * Math.PI * 2;
    for (let i = 0; i < n && game.mons.length < C.MAX_ALIVE; i++) {
      const a = a0 + i * Math.PI * 2 / n + (Math.random() - 0.5) * 0.4, r = 230 + Math.random() * 190;
      spawnMonster(false, { x: p.x + Math.cos(a) * r, y: p.y + Math.sin(a) * r }, 'tree');
      game.mons[game.mons.length - 1].cd = 1.2 + Math.random();   // mọc xong một lúc mới bắn
    }
  }
  /** Một bầy Dơi yêu bay từ ngoài màn hình, cùng một hướng về phía nhân vật. */
  function spawnFlock(wave) {
    const p = game.p, n = C.flockSize(wave), from = C.spawnPoint(p.x, p.y, game.w, game.h, Math.random), dir = Math.atan2(p.y - from.y, p.x - from.x);
    for (let i = 0; i < n && game.mons.length < C.MAX_ALIVE; i++) {
      spawnMonster(false, { x: from.x + (Math.random() - 0.5) * 140, y: from.y + (Math.random() - 0.5) * 140 }, 'bat');
      Object.assign(game.mons[game.mons.length - 1], { dir: dir + (Math.random() - 0.5) * 0.2, life: 14 });
    }
  }

  /** Đạn của quái bay thẳng, trúng nhân vật thì gây sát thương. */
  function enemyShot(x, y, a, spd, dmg, col) { game.eb.push({ x, y, vx: Math.cos(a) * spd, vy: Math.sin(a) * spd, life: 3.2, dmg, col: col || '#c9a0ff' }); }
  function hitPlayer(dmg, text) {
    const p = game.p; if (p.inv > 0) return false;
    const dm = Math.max(1, Math.round(dmg * (1 - p.armor))); p.hp -= dm; p.inv = 0.6;
    game.pops.push({ x: p.x, y: p.y - 40, text: text || '-' + dm, life: 0.7, col: '#ff6b5a' }); return true;
  }

  /** Hành vi theo loại: đuổi · xạ thủ giữ khoảng cách · xung phong (báo trước rồi lao) · tự bạo · Boss triệu hồi / bắn vòng đạn. Trả về {mx, my, move, sp}. */
  function monsterAI(m, ax, ay, d, dt, sp) {
    const s = game, p = s.p; let mx = ax / d, my = ay / d, move = 1;
    switch (m.ai) {
      case 'archer': case 'shooter': {
        const keep = m.boss ? 320 : 260; if (d < keep - 40) move = -0.8; else if (d < keep + 40) move = 0.15;
        m.cd -= dt;
        if (m.cd <= 0 && d < 620) {
          if (m.ai === 'archer') { m.cd = 2.4; enemyShot(m.x, m.y, Math.atan2(ay, ax), 240, Math.round(m.dmg * 0.8)); }
          else { m.cd = 3; m.rot += 0.26; for (let i = 0; i < 12; i++) enemyShot(m.x, m.y, m.rot + i * Math.PI / 6, 200, Math.round(m.dmg * 0.6), '#9fd0ff'); }
        }
        break;
      }
      case 'charger': {
        m.tm -= dt;
        if (m.st === 'walk') { if (m.tm <= 0) { m.st = 'tele'; m.tm = 0.6; m.dir = Math.atan2(ay, ax); } }
        else if (m.st === 'tele') { move = 0; if (m.tm <= 0) { m.st = 'dash'; m.tm = 0.7; } }
        else { mx = Math.cos(m.dir); my = Math.sin(m.dir); sp = m.boss ? 540 : 430; if (m.tm <= 0) { m.st = 'walk'; m.tm = m.boss ? 3 : 3.5; } }
        break;
      }
      case 'bomber': {
        if (m.fuse > 0) {
          move = 0; m.fuse -= dt;
          if (m.fuse <= 0) {
            s.fx.push({ type: 'blast', x: m.x, y: m.y, r: 85, life: 0.35 });
            if (Math.hypot(p.x - m.x, p.y - m.y) < 85) hitPlayer(m.dmg);
            m.hp = 0; m.noDrop = true;
          }
        } else if (d < 95) m.fuse = 0.6;
        break;
      }
      case 'turret': {   // Thụ yêu: đứng yên, mỗi 2,8 giây bắn 3 quả cầu xoè về phía nhân vật
        move = 0; m.cd -= dt;
        if (m.cd <= 0 && d < 700) { m.cd = 2.8; const a0 = Math.atan2(ay, ax); for (let i = -1; i <= 1; i++) enemyShot(m.x, m.y - 30, a0 + i * 0.22, 170, Math.round(m.dmg * 0.7), '#9be36a'); }
        break;
      }
      case 'flock': {   // Dơi yêu: bay thẳng theo hướng cả bầy, chỉ bẻ lái chậm về phía nhân vật nên né được; bay quá lâu thì rời đi
        const want = Math.atan2(ay, ax), df = ((want - m.dir) % (Math.PI * 2) + Math.PI * 3) % (Math.PI * 2) - Math.PI;
        m.dir += Math.max(-0.9 * dt, Math.min(0.9 * dt, df)); m.wob = (m.wob || 0) + dt;
        const h = m.dir + Math.sin(m.wob * 6 + m.hue) * 0.25; mx = Math.cos(h); my = Math.sin(h);
        m.life -= dt; if (m.life <= 0) { m.hp = 0; m.noDrop = true; }
        break;
      }
      case 'summoner': {
        m.cd -= dt;
        if (m.cd <= 0) { m.cd = 6; s.pops.push({ x: m.x, y: m.y - 80, text: 'Triệu hồn!', life: 1, col: '#c9a0ff' }); for (let i = 0; i < 4 && s.mons.length < C.MAX_ALIVE; i++) { const a = i * Math.PI / 2 + Math.random(); spawnMonster(false, { x: m.x + Math.cos(a) * 80, y: m.y + Math.sin(a) * 80 }, 'normal'); } }
        break;
      }
    }
    return { mx, my, move, sp };
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
    if (C.isBossWave(wave) && s.bossWave !== wave) {
      s.bossWave = wave; const n = C.bossCountForWave(wave);
      for (let k = 0; k < n; k++) spawnMonster(true, undefined, undefined, k, C.bossHpShare(n));
      if (n > 1) s.pops.push({ x: p.x, y: p.y - 90, text: `${n} Boss giáng thế!`, life: 2, col: '#ff8a6a' });
    }
    // đợt sự kiện: Thụ yêu mọc quanh người / bầy Dơi yêu kéo tới
    const ev = C.waveEvent(wave);
    if (ev && s.evWave !== wave) {
      s.evWave = wave; s.evKind = ev; s.evLeft = ev === 'trees' ? 1 : C.FLOCKS_PER_WAVE; s.evNext = s.t;
      s.pops.push({ x: p.x, y: p.y - 110, text: ev === 'trees' ? 'Thụ yêu mọc lên!' : 'Bầy dơi kéo tới!', life: 2, col: ev === 'trees' ? '#9be36a' : '#d79bff' });
    }
    if (s.evLeft > 0 && s.t >= s.evNext) { s.evLeft--; s.evNext = s.t + C.FLOCK_GAP; if (s.evKind === 'trees') spawnTrees(wave); else spawnFlock(wave); }
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
      const a = monsterAI(m, ax, ay, d, dt, m.slow > 0 ? m.speed * 0.5 : m.speed); m.slow -= dt;
      m.x += a.mx * a.sp * a.move * dt; m.y += a.my * a.sp * a.move * dt; m.face = (m.st === 'dash' ? a.mx : ax) > 0 ? 1 : -1; m.hit = Math.max(0, m.hit - dt);
      s.grid.insert(m);
      if (d < m.r + 11) hitPlayer(m.dmg);
    }
    // đạn của quái
    for (const b of s.eb) { b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt; if (b.life > 0 && Math.hypot(p.x - b.x, p.y - 20 - b.y) < 16 && hitPlayer(b.dmg)) b.life = 0; }
    s.eb = s.eb.filter(b => b.life > 0);
    if (p.hp <= 0) return endGame();
    if (s.t >= C.MAX_SECONDS) { s.t = C.MAX_SECONDS; return endGame('Yêu Triều lui binh'); }

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
    for (const m of s.mons) if (m.hp <= 0 && !m.noDrop) { s.kills++; const kt = m.boss ? 'boss' : m.type; s.killsBy[kt] = (s.killsBy[kt] || 0) + 1; s.gems.push({ x: m.x, y: m.y, v: m.xp }); if (m.boss) { s.chests.push({ x: m.x, y: m.y }); s.pops.push({ x: m.x, y: m.y - 70, text: 'Boss rơi rương!', life: 1.4, col: '#f2d68a' }); } }
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
    // bình máu rơi ngẫu nhiên quanh người; đầy máu thì chưa nhặt (để dành)
    s.healCd -= dt;
    if (s.healCd <= 0) { s.healCd = C.healDelay(Math.random); if (s.heals.length < C.HEAL.max) { const q = C.healSpot(p.x, p.y, Math.random); s.heals.push({ x: q.x, y: q.y, life: C.HEAL.life, kind: C.pickHealKind(Math.random) }); } }
    for (const h of s.heals) {
      h.life -= dt;
      if (h.life > 0 && p.hp < p.maxHp && Math.hypot(p.x - h.x, p.y - h.y) < C.HEAL.pickR) {
        const v = Math.min(p.maxHp - p.hp, C.healAmount(p.maxHp, h.kind)); p.hp += v; h.life = 0;
        s.pops.push({ x: p.x, y: p.y - 60, text: `+${Math.round(v)} máu`, life: 1.2, col: h.kind.color });
      }
    }
    s.heals = s.heals.filter(h => h.life > 0);
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

  /** Nền đá khắc trận đồ: ô ảnh đặt tâm tại gốc thế giới, lặp theo camera và lật gương xen kẽ để không lộ đường nối. */
  const FLOOR_SRC = 'img/swarm/swarm-floor.webp';
  function drawFloor(im, cx, cy, W, H) {
    const tw = im.naturalWidth, th = im.naturalHeight;
    const i0 = Math.floor((-cx + tw / 2) / tw) , i1 = Math.floor((-cx + W + tw / 2) / tw);
    const j0 = Math.floor((-cy + th / 2) / th), j1 = Math.floor((-cy + H + th / 2) / th);
    for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) {
      const fx = (i & 1) !== 0, fy = (j & 1) !== 0, sx = i * tw - tw / 2 + cx, sy = j * th - th / 2 + cy;
      g.save(); g.translate(Math.round(sx) + (fx ? tw : 0), Math.round(sy) + (fy ? th : 0)); g.scale(fx ? -1 : 1, fy ? -1 : 1);
      g.drawImage(im, 0, 0, tw + 1, th + 1); g.restore();
    }
  }

  function render() {
    const s = game, p = s.p, W = s.w, H = s.h, cx = W / 2 - p.x, cy = H / 2 - p.y;
    g.fillStyle = '#0d1a1c'; g.fillRect(0, 0, W, H);
    const floor = getImg(FLOOR_SRC);
    if (floor) drawFloor(floor, cx, cy, W, H);
    else {   // chưa có ảnh nền: lưới lặp theo camera
      g.strokeStyle = 'rgba(143,208,201,.07)'; g.lineWidth = 1; g.beginPath();
      const step = 80, ox = ((cx % step) + step) % step, oy = ((cy % step) + step) % step;
      for (let x = ox; x < W; x += step) { g.moveTo(x, 0); g.lineTo(x, H); }
      for (let y = oy; y < H; y += step) { g.moveTo(0, y); g.lineTo(W, y); }
      g.stroke();
    }
    g.save(); g.translate(cx, cy);
    for (const c of s.chests) {
      const pulse = 0.5 + 0.5 * Math.sin(s.t * 5), gr = g.createRadialGradient(c.x, c.y, 4, c.x, c.y, 46);
      gr.addColorStop(0, `rgba(255,214,102,${0.55 + pulse * 0.3})`); gr.addColorStop(1, 'rgba(255,214,102,0)');
      g.fillStyle = gr; g.beginPath(); g.arc(c.x, c.y, 46, 0, 7); g.fill();
      g.fillStyle = '#8a5a1e'; g.fillRect(c.x - 16, c.y - 14, 32, 22); g.fillStyle = '#c9a24a'; g.fillRect(c.x - 16, c.y - 14, 32, 8);
      g.fillStyle = '#f2d68a'; g.fillRect(c.x - 3, c.y - 8, 6, 8); g.strokeStyle = '#3a2208'; g.lineWidth = 1.5; g.strokeRect(c.x - 16, c.y - 14, 32, 22);
    }
    for (const h of s.heals) {   // bình máu: tim xanh (hồi 50%) nhấp nhô, nhấp nháy 5 giây cuối trước khi tan
      if (h.life < 5 && Math.floor(h.life * 6) % 2) continue;
      const y = h.y + Math.sin(s.t * 3 + h.x) * 3, gr = g.createRadialGradient(h.x, y, 2, h.x, y, 30);
      gr.addColorStop(0, `rgba(${h.kind.glow},.55)`); gr.addColorStop(1, `rgba(${h.kind.glow},0)`); g.fillStyle = gr; g.beginPath(); g.arc(h.x, y, 30, 0, 7); g.fill();
      g.beginPath(); g.moveTo(h.x, y + 10); g.bezierCurveTo(h.x - 16, y - 2, h.x - 9, y - 16, h.x, y - 7); g.bezierCurveTo(h.x + 9, y - 16, h.x + 16, y - 2, h.x, y + 10);
      g.fillStyle = h.kind.color; g.fill(); g.strokeStyle = '#fffbea'; g.lineWidth = 1.5; g.stroke();
    }
    for (const gem of s.gems) { g.fillStyle = '#6fe6d6'; g.beginPath(); g.moveTo(gem.x, gem.y - 6); g.lineTo(gem.x + 4, gem.y); g.lineTo(gem.x, gem.y + 6); g.lineTo(gem.x - 4, gem.y); g.fill(); }
    // quái xa → gần (sắp theo y để chồng đúng)
    const vis = s.mons.filter(m => Math.abs(m.x - p.x) < W / 2 + 80 && Math.abs(m.y - p.y) < H / 2 + 80).sort((a, b) => a.y - b.y);
    for (const m of vis) {
      const sz = m.size, im = getImg(m.img);
      if (m.color) { g.fillStyle = m.color; g.globalAlpha = m.boss ? 0.4 : 0.3; g.beginPath(); g.ellipse(m.x, m.y + 4, sz * 0.42, sz * 0.13, 0, 0, 7); g.fill(); g.globalAlpha = 1; }
      if (m.ai === 'charger' && m.st === 'tele') { g.strokeStyle = 'rgba(255,90,70,.4)'; g.lineWidth = m.r * 1.4; g.lineCap = 'round'; g.beginPath(); g.moveTo(m.x, m.y); g.lineTo(m.x + Math.cos(m.dir) * 520, m.y + Math.sin(m.dir) * 520); g.stroke(); g.lineCap = 'butt'; }
      if (m.ai === 'bomber' && m.fuse > 0) { g.fillStyle = `rgba(255,120,40,${0.18 + 0.2 * Math.sin(s.t * 40)})`; g.beginPath(); g.arc(m.x, m.y, 85, 0, 7); g.fill(); }
      g.save(); g.translate(m.x, m.y); g.scale(m.face, 1);
      if (m.hit > 0) g.filter = 'brightness(2)';
      if (m.type === 'tree') drawTree(m, s.t);
      else if (m.type === 'bat') drawBat(m, s.t);
      else if (im) { const h = sz * im.naturalHeight / im.naturalWidth; g.drawImage(im, -sz / 2, -h + 8, sz, h); }
      else { g.fillStyle = `hsl(${m.hue},55%,45%)`; g.beginPath(); g.arc(0, -m.r, m.r, 0, 7); g.fill(); }
      g.restore(); g.filter = 'none';
      if (m.boss || m.hp < m.maxHp) { const w = m.boss ? 90 : 34; g.fillStyle = 'rgba(0,0,0,.6)'; g.fillRect(m.x - w / 2, m.y + 10, w, 4); g.fillStyle = m.boss ? '#ff5a4a' : '#ff9a8a'; g.fillRect(m.x - w / 2, m.y + 10, w * Math.max(0, m.hp / m.maxHp), 4); }
    }
    for (const b of s.eb) { const gr = g.createRadialGradient(b.x, b.y - 20, 1, b.x, b.y - 20, 10); gr.addColorStop(0, '#fff'); gr.addColorStop(.5, b.col); gr.addColorStop(1, 'rgba(180,120,255,0)'); g.fillStyle = gr; g.beginPath(); g.arc(b.x, b.y - 20, 10, 0, 7); g.fill(); }
    for (const f of s.fx) {
      if (f.type === 'blast') { const k = 1 - f.life / 0.35; g.fillStyle = `rgba(255,140,50,${0.5 * (1 - k)})`; g.beginPath(); g.arc(f.x, f.y, f.r * (0.4 + 0.6 * k), 0, 7); g.fill(); }
      else if (f.type === 'nova') { const k = 1 - f.life / 0.45; g.strokeStyle = `rgba(159,232,255,${1 - k})`; g.lineWidth = 6 * (1 - k) + 1; g.beginPath(); g.arc(f.x, f.y, f.r * (0.3 + 0.7 * k), 0, 7); g.stroke(); }
      else { g.strokeStyle = `rgba(214,200,255,${Math.min(1, f.life * 5)})`; g.lineWidth = 3; g.beginPath(); let x = f.x + 14, y = f.y - 320; g.moveTo(x, y); for (let i = 0; i < 8; i++) { y += 40; x = f.x + (i === 7 ? 0 : (Math.random() - .5) * 36); g.lineTo(x, y); } g.stroke(); }
    }
    drawHero(p.x, p.y, 64, s.t, p.face, p.inv > 0 && Math.floor(s.t * 20) % 2 === 0);
    for (let i = 0; i < p.orbit; i++) { const a = p.orbA + i * Math.PI * 2 / p.orbit, ox = p.x + Math.cos(a) * p.orbitR, oy = p.y - 20 + Math.sin(a) * p.orbitR; const gr = g.createRadialGradient(ox, oy, 2, ox, oy, 14); gr.addColorStop(0, '#fff3c4'); gr.addColorStop(.5, '#ff9a3a'); gr.addColorStop(1, 'rgba(255,80,20,0)'); g.fillStyle = gr; g.beginPath(); g.arc(ox, oy, 14, 0, 7); g.fill(); }
    for (const b of s.shots) drawSword(b.x, b.y, b.a);
    g.font = '700 14px "Be Vietnam Pro",sans-serif'; g.textAlign = 'center';
    for (const o of s.pops) { g.globalAlpha = Math.min(1, o.life * 2.5); g.fillStyle = o.col; g.fillText(o.text, o.x, o.y - (0.7 - o.life) * 40); }
    g.globalAlpha = 1; g.restore();
    // thanh máu Boss ở giữa phía trên
    s.mons.filter(m => m.boss).slice(0, 3).forEach((bs, i) => {   // mỗi Boss một thanh, xếp dọc
      const bw = Math.min(420, W - 24), bx = W / 2 - bw / 2, by = 68 + i * 40;   // ngay dưới bảng Đợt ở giữa trên
      g.fillStyle = 'rgba(0,0,0,.6)'; g.fillRect(bx, by, bw, 12); g.fillStyle = bs.color || '#ff5a4a'; g.fillRect(bx, by, bw * Math.max(0, bs.hp / bs.maxHp), 12);
      g.strokeStyle = 'rgba(255,255,255,.4)'; g.lineWidth = 1; g.strokeRect(bx, by, bw, 12);
      g.font = '700 13px "Be Vietnam Pro",sans-serif'; g.fillStyle = '#f2d68a'; g.textAlign = 'center'; g.fillText(bs.name, bx + bw / 2, by + 30);
    });
    // joystick ảo
    if (stick.on) { g.strokeStyle = 'rgba(255,255,255,.3)'; g.lineWidth = 2; g.beginPath(); g.arc(stick.ox, stick.oy, 50, 0, 7); g.stroke(); g.fillStyle = 'rgba(255,255,255,.35)'; g.beginPath(); g.arc(stick.ox + stick.dx * 50, stick.oy + stick.dy * 50, 20, 0, 7); g.fill(); }
    // HUD
    const need = C.xpNeed(p.level);
    // bố cục: thanh kinh nghiệm chạy suốt mép trên · bảng Đợt/thời gian ở giữa · máu + thần thông ở góc dưới trái (không đè lên khu chiến đấu)
    const hpPct = Math.max(0, p.hp / p.maxHp * 100), left = Math.max(0, C.MAX_SECONDS - s.t);
    hud.innerHTML = `<div class="sw-xp"><i style="width:${Math.min(100, p.xp / need * 100)}%"></i></div><div class="sw-lv">Cấp ${p.level}</div>`
      + `<div class="sw-plate"><b>Đợt ${C.waveOf(s.t)}</b><span class="${left <= 60 ? 'warn' : ''}">còn ${fmtTime(left)}</span><em>Hạ ${s.kills}</em></div>`
      + `<div class="sw-dock"><div class="sw-skills">${C.UPGRADES.filter(u => s.levels[u.id]).map(u => `<span>${u.name} <i>${s.levels[u.id]}</i></span>`).join('')}</div>`
      + `<div class="sw-hp${hpPct <= 30 ? ' low' : ''}"><i style="width:${hpPct}%"></i><span>${Math.max(0, Math.ceil(p.hp))} / ${p.maxHp}</span></div></div>`;
  }

  /** Thụ yêu (vẽ tay, gốc toạ độ ở chân): thân gỗ, tán lá đung đưa, lõi phát sáng rực lên ngay trước khi bắn. */
  function drawTree(m, t) {
    const sw = Math.sin(t * 1.6 + m.hue) * 3, glow = m.cd < 0.5 ? 1 : 0.45;
    g.fillStyle = '#5a3a1e'; g.beginPath(); g.moveTo(-9, 0); g.lineTo(-6, -34); g.lineTo(6, -34); g.lineTo(9, 0); g.closePath(); g.fill();
    g.strokeStyle = '#5a3a1e'; g.lineWidth = 4; g.lineCap = 'round'; g.beginPath(); g.moveTo(-8, -2); g.lineTo(-18, 4); g.moveTo(8, -2); g.lineTo(18, 4); g.stroke(); g.lineCap = 'butt';
    g.fillStyle = '#2f7a3a'; for (const [cx, cy, r] of [[-16, -46, 18], [16, -46, 18], [0, -62, 22]]) { g.beginPath(); g.arc(cx + sw, cy, r, 0, 7); g.fill(); }
    g.fillStyle = '#4fae4f'; g.beginPath(); g.arc(-4 + sw, -58, 12, 0, 7); g.fill();
    g.fillStyle = `rgba(190,255,140,${glow})`; g.beginPath(); g.arc(sw, -44, 6, 0, 7); g.fill();
    g.fillStyle = '#1a1206'; g.fillRect(-6, -26, 4, 3); g.fillRect(2, -26, 4, 3);
  }
  /** Dơi yêu: cánh đập liên tục, mắt đỏ; bay cao hơn mặt đất một chút. */
  function drawBat(m, t) {
    const f = Math.sin(t * 22 + m.hue), y = -24;
    g.fillStyle = '#3a2048'; g.beginPath(); g.moveTo(0, y); g.lineTo(-22, y - 10 * f); g.lineTo(-15, y + 4); g.lineTo(-9, y + 1); g.lineTo(0, y + 6); g.lineTo(9, y + 1); g.lineTo(15, y + 4); g.lineTo(22, y - 10 * f); g.closePath(); g.fill();
    g.fillStyle = '#5b2f70'; g.beginPath(); g.ellipse(0, y + 1, 6, 8, 0, 0, 7); g.fill();
    g.fillStyle = '#ff4a4a'; g.fillRect(-3.5, y - 2, 2, 2); g.fillRect(1.5, y - 2, 2, 2);
  }

  /** Phi kiếm Thanh Phong (theo bản thiết kế): lưỡi ngọc bích, chắn kiếm vàng, chuôi quấn kim tuyến, vệt gió xanh phía sau; mũi kiếm hướng theo góc a. */
  let swordGrad = null;
  function drawSword(x, y, a) {
    if (!swordGrad || swordGrad.ctx !== g) {   // gradient ở hệ toạ độ của kiếm: tạo một lần, dùng lại mọi khung hình
      const tr = g.createLinearGradient(-44, 0, -18, 0); tr.addColorStop(0, 'rgba(111,230,214,0)'); tr.addColorStop(1, 'rgba(111,230,214,.55)');
      const bl = g.createLinearGradient(0, -2.3, 0, 2.3); bl.addColorStop(0, '#f4fffc'); bl.addColorStop(0.5, '#b8f4ea'); bl.addColorStop(1, '#5cc9b9');
      swordGrad = { ctx: g, tr, bl };
    }
    g.save(); g.translate(x, y); g.rotate(a); g.scale(1.1, 1.1);
    g.fillStyle = swordGrad.tr; g.beginPath(); g.moveTo(-44, 0); g.lineTo(-18, -1.6); g.lineTo(-18, 1.6); g.closePath(); g.fill();
    g.fillStyle = swordGrad.bl; g.strokeStyle = '#2c6f66'; g.lineWidth = 0.6;
    g.beginPath(); g.moveTo(26, 0); g.lineTo(6, -2.3); g.lineTo(-6, -1.9); g.lineTo(-6, 1.9); g.lineTo(6, 2.3); g.closePath(); g.fill(); g.stroke();
    g.fillStyle = '#f2d68a'; g.fillRect(-8, -5.2, 2, 10.4);
    g.fillStyle = '#3a2a14'; g.fillRect(-17, -1.3, 9, 2.6);
    g.fillStyle = '#f2d68a'; g.beginPath(); g.arc(-18, 0, 1.7, 0, 7); g.fill();
    g.restore();
  }

  function loop(ts) {
    raf = requestAnimationFrame(loop);
    const dt = Math.min(0.05, (ts - last) / 1000 || 0); last = ts;
    if (!game || game.over) return;
    if (!game.paused && !game.menu && !document.hidden) update(dt);
    if (game && !game.over) render();
  }

  // ---- nâng cấp / kết thúc ----
  function openLevelUp(title) {
    game.paused = true; stick.on = false; stick.dx = stick.dy = 0; keys.clear();
    const picks = C.pickUpgrades(Math.random, game.levels, 3);
    if (!picks.length) { game.paused = false; return; }
    levelBox.innerHTML = `<h3>${title || 'Đột phá! Chọn một thần thông'}</h3>`;
    picks.forEach((u, i) => {
      const b = el('button', 'sw-card', `<kbd>${i + 1}</kbd><b>${u.name}</b><small>${u.desc}</small><em>${u.req ? 'Nhánh · ' : ''}${upLabel(u)}</em>`);
      b.onclick = () => choose(u); b.dataset.k = String(i + 1); levelBox.appendChild(b);
    });
    levelBox._picks = picks; levelBox.hidden = false;
  }
  /** Nhãn cấp trên thẻ chọn: cấp đang có → cấp sau khi chọn (thẻ chỉ hiện khi chưa đạt tối đa; tránh hiểu nhầm "4/4" là đã đầy). */
  function upLabel(u) { const cur = game.levels[u.id] || 0; return cur ? `Cấp ${cur} → ${cur + 1} (tối đa ${u.max})` : `Mới · tối đa ${u.max}`; }
  function choose(u) { if (levelBox.hidden || (game.levels[u.id] || 0) >= u.max) return; game.levels[u.id] = (game.levels[u.id] || 0) + 1; u.apply(game.p); levelBox.hidden = true; game.paused = false; }

  function endGame(title = 'Đạo tiêu thân vong') {
    if (!game || game.over) return;
    game.over = true;
    game.claiming = true;
    pauseBox.hidden = levelBox.hidden = true;
    const b = loadBest(), isNew = C.waveOf(game.t) > b.wave || (C.waveOf(game.t) === b.wave && game.kills > b.kills);
    if (isNew) saveBest({ wave: C.waveOf(game.t), kills: game.kills, time: Math.floor(game.t) });
    const best = isNew ? { wave: C.waveOf(game.t), kills: game.kills, time: Math.floor(game.t) } : b;
    endBox.innerHTML = `<h3>${title}</h3><p>Sống sót <b>${fmtTime(game.t)}</b> · Đợt <b>${C.waveOf(game.t)}</b> · Hạ <b>${game.kills}</b> yêu</p>`
      + `<p class="sw-best">${isNew ? 'Kỷ lục mới!' : 'Kỷ lục'}: Đợt ${best.wave} · ${fmtTime(best.time)} · ${best.kills} yêu</p>`;
    const rewardP = el('p', 'sw-best', 'Đang nhận Linh Thú Đan…'); endBox.append(rewardP);
    const again = el('button', 'sw-btn gold', 'Vào lại (tốn 1 lượt)'), close = el('button', 'sw-btn', 'Về Bí Cảnh'), msg = el('p', 'sw-note', '');
    const finished = game;
    again.disabled = close.disabled = true;
    claimReward(rewardP, finished).then(() => {
      finished.claiming = close.disabled = false;
      again.disabled = swarmLeft() <= 0;
      if (again.disabled) again.textContent = 'Hết lượt hôm nay';
    });
    again.onclick = () => enterThen(again, msg, start); close.onclick = () => closeGame(); endBox.append(again, close, msg); endBox.hidden = false;
  }

  /** Số lượt vào Yêu Triều còn lại hôm nay (theo trạng thái server). */
  function swarmLeft() { const sw = typeof S !== 'undefined' && S && S.swarm; return sw ? Math.max(0, sw.limit - sw.used) : 0; }
  /** Xin server cho vào bí cảnh (tốn 1 lượt); được thì chạy then(), không được thì hiện lý do. */
  async function enterThen(btn, msgEl, then) {
    if (typeof S === 'undefined' || !S || typeof api !== 'function') { msgEl.textContent = 'Cần đăng nhập để vào Bí Cảnh.'; return; }
    btn.disabled = true;
    try {
      const res = await api('POST', `/api/players/${S.id}/swarm/enter`);
      if (typeof setState === 'function') setState(res.state);
      then();
    } catch (e) { msgEl.textContent = e.message; btn.disabled = false; }
  }
  async function claimReward(box, g) {
    try {
      if (typeof S === 'undefined' || !S || typeof api !== 'function') { box.textContent = 'Cần đăng nhập để nhận Linh Thú Đan.'; return; }
      const res = await api('POST', `/api/players/${S.id}/swarm/claim`, { kills: g.killsBy, seconds: Math.floor(g.t) });
      if (typeof setState === 'function') setState(res.state);
      box.textContent = res.message || 'Đã nhận Linh Thú Đan.'; box.classList.add('sw-gain');
    } catch (e) { box.textContent = e.message; }
  }

  // ---- tạm dừng: nút ⏸ dưới nút đóng, phím Esc / P; tự dừng khi chuyển cửa sổ ----
  function setPause(on) {
    if (!game || game.over || (on && !levelBox.hidden)) return;
    game.menu = on; pauseBox.hidden = !on; keys.clear(); stick.on = false; stick.dx = stick.dy = 0;
    if (!on) { last = performance.now(); return; }
    pauseBox.innerHTML = `<h3>Tạm dừng</h3><p>Đợt ${C.waveOf(game.t)} · còn ${fmtTime(Math.max(0, C.MAX_SECONDS - game.t))} · Hạ ${game.kills} yêu</p>`;
    const go = el('button', 'sw-btn gold', 'Tiếp tục'), quit = el('button', 'sw-btn', 'Thoát ván');
    go.onclick = () => setPause(false); quit.onclick = () => closeGame();
    pauseBox.append(go, quit, el('p', 'sw-note', 'Thoát ván để nhận Linh Thú Đan của lượt này.')); go.focus({ preventScroll: true });
  }

  function start() { if (pauseBox) pauseBox.hidden = true; const hp = overlay && overlay.querySelector('.sw-help'); if (hp) { hp.style.animation = 'none'; void hp.offsetWidth; hp.style.animation = ''; } endBox.hidden = true; levelBox.hidden = true; keys.clear(); stick.on = false; stick.dx = stick.dy = 0; game = newGame(); resize(); }

  // ---- Bí Cảnh: màn chọn bí cảnh bên ngoài (hiện chỉ có Yêu Triều). Mỗi bí cảnh mở là một thẻ có ảnh nền, dấu ? (tooltip phần thưởng),
  //      chip lượt thưởng hôm nay và nút Tiến vào ngay trong thẻ; bí cảnh chưa mở là một dòng thấp có khóa. ----
  const REALMS = [
    { id: 'swarm', name: 'Yêu Triều', tag: 'Sinh tồn 10 phút', img: 'img/swarm/swarm-floor.webp', open: openGame,
      desc: 'Yêu quái tràn tới từ mọi hướng, Boss xuất hiện ở đợt 3, rồi đợt 6, 8, 10…. Phi kiếm tự đánh, đạo hữu lo né và chọn thần thông.',
      rewards: ['Nhận tổng cộng 5.000 Linh Thú Đan khi kết thúc cả 3 lượt trong ngày'] },
    { id: 'soon', locked: true },
  ];
  const LOCK_SVG = '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg>';
  let realmEl;
  function renderRealms() {
    const sw = typeof S !== 'undefined' && S && S.swarm, full = !!sw && sw.used >= sw.limit;
    realmEl.innerHTML = '';
    const head = el('header', 'sw-rh', '<h2>Bí Cảnh</h2>');
    const x = el('button', 'sw-rx', '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>'); x.setAttribute('aria-label', 'Đóng'); x.onclick = closeRealms; head.appendChild(x);
    const list = el('div', 'sw-rlist'), msg = el('p', 'sw-rmsg', '');
    REALMS.forEach(r => {
      if (r.locked) { list.appendChild(el('div', 'sw-rlock', `${LOCK_SVG}<span>Bí cảnh mới đang phong ấn</span><em>Sắp mở</em>`)); return; }
      const card = el('div', 'sw-rcard' + (full ? ' full' : ''));
      const bg = el('span', 'sw-rbg'); if (r.img) bg.style.setProperty('--img', `url('${r.img}')`);
      const body = el('div', 'sw-rbody'), title = el('div', 'sw-rtitle', `<span class="sw-rname">${r.name}</span>`);
      const q = el('button', 'sw-rq', '?'); q.setAttribute('aria-label', 'Phần thưởng ' + r.name); q.setAttribute('aria-expanded', 'false');
      const tip = el('div', 'sw-rtip', `<p>${r.desc}</p><ul>${r.rewards.map(t => `<li>${t}</li>`).join('')}</ul>`); tip.setAttribute('role', 'tooltip');
      q.onclick = () => { const on = !card.classList.contains('tip'); card.classList.toggle('tip', on); q.setAttribute('aria-expanded', String(on)); };   // chạm màn hình: bấm để hiện/ẩn
      title.append(q, tip);
      body.append(title, el('span', 'sw-rtag', r.tag));
      if (sw) body.append(el('span', 'sw-rchip', full ? `Hết lượt vào ${sw.used}/${sw.limit}` : `Lượt vào hôm nay ${sw.used}/${sw.limit}`));
      const go = el('button', 'sw-rgo', full ? 'Hết lượt' : 'Tiến vào'); go.disabled = full;
      go.onclick = () => enterThen(go, msg, () => { closeRealms(); r.open(); });
      if (r.id === 'swarm' && typeof adminAct === 'function' && devMode && adminOn) {
        const reset = el('button', 'mk-shop-admin', 'Reset lượt');
        reset.onclick = async () => {
          reset.disabled = true;
          go.disabled = true;
          await adminAct('swarmreset');
          if (!realmEl.hidden) renderRealms();
        };
        body.append(reset);
      }
      card.append(bg, body, go); list.appendChild(card);
    });
    realmEl.append(head, list, msg);
    list.querySelector('.sw-rgo:not(:disabled)')?.focus({ preventScroll: true });
  }
  function openRealms() {
    if (!realmEl) {
      realmEl = el('div', 'swarm sw-realm'); realmEl.hidden = true; realmEl.setAttribute('role', 'dialog'); realmEl.setAttribute('aria-label', 'Bí Cảnh'); document.body.appendChild(realmEl);
      window.addEventListener('keydown', e => {   // đang ở màn chọn: chặn phím tắt game chính
        if (realmEl.hidden) return;
        e.stopPropagation();
        if (e.key === 'Escape') { closeRealms(); e.preventDefault(); }
      }, true);
    }
    realmEl.hidden = false; document.body.classList.add('swarm-open'); renderRealms();
  }
  function closeRealms() { if (realmEl) realmEl.hidden = true; document.body.classList.remove('swarm-open'); }

  function openGame() {
    if (!overlay) build();
    overlay.hidden = false; document.body.classList.add('swarm-open'); start();
    last = performance.now(); cancelAnimationFrame(raf); raf = requestAnimationFrame(loop);
  }
  function closeGame(back = true) {
    if (!overlay || game?.claiming) return;
    if (game && !game.over) return endGame('Đã thoát ván');
    overlay.hidden = true; document.body.classList.remove('swarm-open'); cancelAnimationFrame(raf); game = null; keys.clear();
    if (back) openRealms();
  }

  function build() {
    overlay = el('div', 'swarm'); overlay.hidden = true; overlay.setAttribute('role', 'dialog'); overlay.setAttribute('aria-label', 'Yêu Triều');
    canvas = el('canvas', 'sw-canvas'); hud = el('div', 'sw-hud'); levelBox = el('div', 'sw-modal'); endBox = el('div', 'sw-modal');
    pauseBox = el('div', 'sw-modal sw-pause'); levelBox.hidden = endBox.hidden = pauseBox.hidden = true;
    const x = el('button', 'sw-x', '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>'); x.setAttribute('aria-label', 'Đóng'); x.onclick = () => closeGame();
    const pz = el('button', 'sw-pz', '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 6v12M15 6v12"/></svg>'); pz.setAttribute('aria-label', 'Tạm dừng'); pz.onclick = () => setPause(!game?.menu);
    document.addEventListener('visibilitychange', () => { if (document.hidden && !overlay.hidden) setPause(true); });
    const help = el('div', 'sw-help', 'WASD / mũi tên / kéo chuột để di chuyển · Esc / P tạm dừng');
    overlay.append(canvas, hud, help, x, pz, levelBox, endBox, pauseBox); document.body.appendChild(overlay);
    g = canvas.getContext('2d'); window.addEventListener('resize', () => { if (!overlay.hidden) resize(); });
    window.addEventListener('keydown', e => {
      if (overlay.hidden) return;
      e.stopPropagation();   // đang chơi: chặn phím tắt của game chính (pha capture, chạy trước các listener khác)
      const k = e.key.toLowerCase();
      if (k === 'escape' || k === 'p') { if (game && !game.over) setPause(!game.menu); else if (k === 'escape') closeGame(); e.preventDefault(); return; }
      if (game?.menu) return;   // đang tạm dừng: bỏ qua phím di chuyển
      if (!levelBox.hidden && '123'.includes(k) && levelBox._picks?.[+k - 1]) { choose(levelBox._picks[+k - 1]); return; }
      if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(k)) { keys.add(k); e.preventDefault(); }
    }, true);
    window.addEventListener('keyup', e => { keys.delete(e.key.toLowerCase()); if (!overlay.hidden) e.stopPropagation(); }, true);
    canvas.addEventListener('pointerdown', e => { const r = canvas.getBoundingClientRect(); stick.on = true; stick.id = e.pointerId; stick.ox = e.clientX - r.left; stick.oy = e.clientY - r.top; stick.dx = stick.dy = 0; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => { if (!stick.on || e.pointerId !== stick.id) return; const r = canvas.getBoundingClientRect(), dx = e.clientX - r.left - stick.ox, dy = e.clientY - r.top - stick.oy, d = Math.hypot(dx, dy) || 1, k = Math.min(1, d / 50); stick.dx = dx / d * k; stick.dy = dy / d * k; });
    const up = e => { if (e.pointerId === stick.id) { stick.on = false; stick.dx = stick.dy = 0; } };
    canvas.addEventListener('pointerup', up); canvas.addEventListener('pointercancel', up);
  }

  function mountButton() {
    const actions = document.querySelector('.actions'); if (!actions || document.getElementById('btnSwarm')) return;
    const b = el('button', 'ib', '<span class="sw-ico" aria-hidden="true">⚔</span>'); b.id = 'btnSwarm'; b.setAttribute('aria-label', 'Bí Cảnh'); b.dataset.tip = 'Bí Cảnh\nChọn bí cảnh để tiến vào (hiện có: Yêu Triều)';
    b.onclick = openRealms; actions.insertBefore(b, document.getElementById('btnLog')?.nextSibling || null);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mountButton); else mountButton();
  // __debug: chỉ để kiểm thử/chạy mô phỏng nhanh; không dùng trong game.
  window.SwarmGame = { open: openGame, close: () => closeGame(false), realms: openRealms, __debug: { step: dt => { if (game && !game.over && !game.paused) update(dt); if (game && !game.over) render(); }, state: () => game, choose: i => levelBox._picks && choose(levelBox._picks[i]) } };
})();
