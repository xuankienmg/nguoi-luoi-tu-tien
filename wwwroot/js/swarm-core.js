/* Vấn Đạo · js/swarm-core.js: logic thuần của mini game "Yêu Triều" (độ khó theo đợt, nâng cấp, lưới va chạm, điểm sinh quái). Không đụng DOM. */
(function (root) {
  const WAVE_SECONDS = 30;
  const MAX_ALIVE = 240;
  const MAX_SECONDS = 600;   // mỗi ván tối đa 10 phút (khớp SwarmData.MaxRunSeconds ở server)

  const waveOf = t => 1 + Math.floor(Math.max(0, t) / WAVE_SECONDS);
  const isBossWave = w => w === 3 || (w >= 6 && w % 2 === 0);
  /** Thứ tự đợt Boss tính đến đợt w (đợt 3 → 1, 6 → 2, 8 → 3…), dùng để luân phiên loại Boss. */
  const bossOrdinal = w => (w < 3 ? 0 : w < 6 ? 1 : Math.floor(w / 2) - 1);
  /** Quái sinh mỗi giây (mạnh dần, có trần). */
  const lateWaveScale = w => w >= 7 ? 1.2 : 1;
  const spawnRate = w => Math.min(1.2 + w * 0.9, 16) * lateWaveScale(w);
  const monsterStats = w => ({
    hp: Math.round(10 * (w >= 3 ? 1.2 : 1) * (1 + 0.45 * (w - 1)) * (1 + 0.02 * (w - 1) * (w - 1)) * (w > 9 ? 1 + 0.04 * (w - 9) ** 1.5 : 1) * lateWaveScale(w)),
    speed: Math.min(50 + w * 3.5, 130),
    dmg: Math.round(5 + w * 1.5),
    xp: 1,
  });
  const bossStats = w => { const m = monsterStats(w); return { hp: m.hp * 16, speed: m.speed * 0.7, dmg: Math.round(m.dmg * 1.8), xp: 12 }; };
  // Loại quái thường: hệ số so với monsterStats(wave); ai quyết định hành vi ở swarm.js. minWave = đợt bắt đầu xuất hiện, weight = tỉ trọng chọn.
  const MONSTER_TYPES = [
    { id: 'normal',  name: 'Yêu thường',  ai: 'chase',   minWave: 1, weight: 10, hp: 1,   speed: 1,   dmg: 1,   r: 20, size: 56, xp: 1, color: '' },
    { id: 'swift',   name: 'Tật phong yêu', ai: 'chase', minWave: 2, weight: 4,  hp: 0.5, speed: 1.7, dmg: 0.8, r: 15, size: 44, xp: 1, color: '#6fe6d6' },
    { id: 'armored', name: 'Thiết giáp yêu', ai: 'chase', minWave: 3, weight: 3, hp: 3,   speed: 0.6, dmg: 1.3, r: 26, size: 74, xp: 3, color: '#d9a441' },
    { id: 'archer',  name: 'Xạ yêu',      ai: 'archer',  minWave: 4, weight: 3,  hp: 0.8, speed: 0.9, dmg: 0.8, r: 18, size: 52, xp: 2, color: '#b58cff' },
    { id: 'charger', name: 'Xung yêu',    ai: 'charger', minWave: 5, weight: 3,  hp: 1.3, speed: 0.8, dmg: 1.4, r: 22, size: 60, xp: 2, color: '#ff6b5a' },
    { id: 'bomber',  name: 'Bạo yêu',     ai: 'bomber',  minWave: 6, weight: 2,  hp: 0.7, speed: 1.2, dmg: 2.5, r: 18, size: 50, xp: 2, color: '#ff9a3a' },
    // quái theo đợt sự kiện (weight 0: không sinh ngẫu nhiên, chỉ sinh theo waveEvent)
    { id: 'tree',    name: 'Thụ yêu',     ai: 'turret',  minWave: 4, weight: 0,  hp: 4,   speed: 0,   dmg: 1,   r: 26, size: 84, xp: 3, color: '#7bd36a' },
    { id: 'bat',     name: 'Dơi yêu',     ai: 'flock',   minWave: 5, weight: 0,  hp: 0.35, speed: 2.4, dmg: 0.6, r: 13, size: 44, xp: 1, color: '#c46bd8' },
  ];
  // Boss: lần lượt theo thứ tự xuất hiện (đợt 3, 6, 8, 10…), hết vòng thì lặp lại và mạnh hơn nhờ đợt.
  const BOSS_TYPES = [
    { id: 'colossus', name: 'Cự Yêu Vương',      ai: 'chase',    hp: 1,   speed: 1,   dmg: 1,   r: 46, size: 150, xp: 12, color: '#ff5a4a' },
    { id: 'charger',  name: 'Xung Thiên Yêu Vương', ai: 'charger', hp: 0.9, speed: 0.9, dmg: 1.2, r: 44, size: 140, xp: 14, color: '#ff7a3a' },
    { id: 'summoner', name: 'Triệu Hồn Yêu Vương', ai: 'summoner', hp: 1.1, speed: 0.7, dmg: 1,   r: 44, size: 140, xp: 16, color: '#b58cff' },
    { id: 'shooter',  name: 'Vạn Tiễn Yêu Vương', ai: 'shooter',  hp: 1,   speed: 0.6, dmg: 1,   r: 44, size: 140, xp: 16, color: '#6fb6ff' },
  ];
  // k = thứ tự Boss trong cùng một đợt (0, 1, 2) để các Boss cùng đợt khác loại nhau.
  const bossTypeForWave = (w, k = 0) => BOSS_TYPES[(Math.max(1, bossOrdinal(w)) - 1 + k) % BOSS_TYPES.length];
  /** Số Boss mỗi đợt Boss: 1 (đợt 3–12), 2 (đợt 15–21), 3 (từ đợt 24). */
  const bossCountForWave = w => (w >= 18 ? 3 : w >= 14 ? 2 : 1);   // đợt 3–12: 1 Boss · đợt 14–16: 2 · từ đợt 18: 3
  /** Hệ số máu mỗi Boss khi có nhiều Boss cùng lúc (tổng máu vẫn tăng nhưng không gấp 2–3 lần). */
  const bossHpShare = n => (n >= 3 ? 0.6 : n === 2 ? 0.7 : 1);
  /** Chọn loại quái thường theo trọng số, chỉ trong số loại đã mở ở đợt w. */
  function pickMonsterType(w, rng) {
    const pool = MONSTER_TYPES.filter(t => t.minWave <= w && t.weight > 0), total = pool.reduce((a, t) => a + t.weight, 0);
    let x = rng() * total;
    for (const t of pool) { x -= t.weight; if (x < 0) return t; }
    return pool[pool.length - 1];
  }

  // Bình máu rơi ngẫu nhiên trên bản đồ: cứ 14–24 giây rơi 1 bình quanh người (160–420px), tối đa 3 bình, tồn tại 30 giây.
  // Chỉ còn bình xanh: hồi 50% máu tối đa (bỏ bình vàng hồi đầy 11/2026).
  const HEAL = { minGap: 14, maxGap: 24, life: 30, max: 3, pickR: 34, near: 160, far: 420 };
  const healDelay = rng => HEAL.minGap + rng() * (HEAL.maxGap - HEAL.minGap);
  const HEAL_KINDS = [
    { id: 'green', pct: 0.5, weight: 80, color: '#4fd27a', glow: '79,210,122' },
  ];
  function pickHealKind(rng) { let x = rng() * HEAL_KINDS.reduce((a, k) => a + k.weight, 0); for (const k of HEAL_KINDS) { x -= k.weight; if (x < 0) return k; } return HEAL_KINDS[0]; }
  const healAmount = (maxHp, kind = HEAL_KINDS[0]) => Math.round(maxHp * kind.pct);
  function healSpot(px, py, rng) {
    const a = rng() * Math.PI * 2, r = HEAL.near + rng() * (HEAL.far - HEAL.near);
    return { x: px + Math.cos(a) * r, y: py + Math.sin(a) * r };
  }

  /** Đợt sự kiện: 'trees' = Thụ yêu mọc quanh người rồi bắn cầu (đợt 4, 9, 14, 19); 'bats' = bầy Dơi yêu bay theo đàn (đợt 5, 10, 15, 20). */
  const waveEvent = w => (w >= 4 && w % 5 === 4 ? 'trees' : w >= 5 && w % 5 === 0 ? 'bats' : null);
  const treeCount = w => Math.round((3 + Math.floor(w / 6)) * lateWaveScale(w));
  const flockSize = w => Math.round((8 + Math.floor(w / 2)) * lateWaveScale(w));
  const FLOCKS_PER_WAVE = 3, FLOCK_GAP = 10;      // mỗi đợt dơi: 3 bầy, cách nhau 10 giây

  /** Kinh nghiệm cần để lên cấp kế tiếp. */
  const xpNeed = level => {
    const base = 5 + level * 4;
    return level >= 50 ? Math.ceil(base * 13 / 10) : base;
  };

  const BASE_PLAYER = () => ({ maxHp: 100, speed: 150, swords: 1, cooldown: 0.65, damage: 10, range: 300, magnet: 80,
    pierce: 0, crit: 0.05, armor: 0, regen: 0, orbit: 0, bolt: 0, nova: 0,
    orbitDmg: 0.7, orbitR: 72, boltSplash: 50, boltEvery: 2, novaDmg: 1.3, novaSlow: 0, novaKb: 60, novaRange: 1, swordDmg: 1 });

  const UPGRADES = [
    // chiêu gốc luôn có sẵn từ đầu (levels.sword = 1); các nâng cấp kiếm là nhánh của nó
    { id: 'sword',    name: 'Phi kiếm',         desc: 'Phi kiếm tự bắn vào quái gần nhất',      max: 1, apply: () => {} },
    { id: 'swords',   req: 'sword', name: 'Thêm phi kiếm',   desc: '+1 thanh phi kiếm mỗi lượt',   max: 6, apply: p => { p.swords += 1; } },
    { id: 'speedAtk', req: 'sword', name: 'Kiếm tốc',         desc: 'Đánh nhanh hơn 15%',            max: 6, apply: p => { p.cooldown = Math.max(0.2, p.cooldown * 0.85); } },
    { id: 'damage',   name: 'Kiếm khí',         desc: '+30% sát thương',               max: 8, apply: p => { p.damage = Math.round(p.damage * 1.3); } },
    { id: 'range',    req: 'sword', name: 'Tầm kiếm',         desc: '+20% tầm đánh',                 max: 4, apply: p => { p.range = Math.round(p.range * 1.2); } },
    { id: 'hp',       name: 'Hộ thể',           desc: '+40 máu tối đa',   max: 6, apply: p => { p.maxHp += 40; } },
    { id: 'magnet',   name: 'Linh hút',         desc: '+50% tầm hút tinh thể',         max: 4, apply: p => { p.magnet = Math.round(p.magnet * 1.5); } },
    { id: 'speed',    name: 'Thân pháp',        desc: '+12% tốc độ chạy',              max: 4, apply: p => { p.speed = Math.round(p.speed * 1.12); } },
    { id: 'pierce',   req: 'sword', name: 'Xuyên kiếm',       desc: 'Phi kiếm xuyên thêm 1 quái',    max: 4, apply: p => { p.pierce += 1; } },
    { id: 'crit',     name: 'Bạo kích',         desc: '+10% bạo kích (gấp đôi sát thương)', max: 5, apply: p => { p.crit += 0.1; } },
    { id: 'armor',    name: 'Kim cương giáp',   desc: 'Giảm 10% sát thương nhận vào',  max: 5, apply: p => { p.armor = Math.min(0.5, p.armor + 0.1); } },
    { id: 'regen',    name: 'Hồi xuân',         desc: 'Hồi 2 máu mỗi giây',            max: 5, apply: p => { p.regen += 2; } },
    { id: 'orbit',    name: 'Hỏa cầu xoay',     desc: 'Hỏa cầu xoay quanh người, thiêu quái chạm vào (lần đầu có 2 quả, mỗi cấp +1)', max: 6, apply: p => { p.orbit += p.orbit ? 1 : 2; } },
    { id: 'bolt',     name: 'Lôi kích',         desc: 'Sét đánh +1 quái ngẫu nhiên mỗi 2 giây', max: 5, apply: p => { p.bolt += 1; } },
    { id: 'nova',     name: 'Hàn băng chấn',    desc: 'Sóng băng đẩy lùi và gây sát thương mỗi 4 giây', max: 5, apply: p => { p.nova += 1; } },
    // nhánh nâng cấp: chỉ xuất hiện sau khi đã chọn chiêu gốc (req)
    { id: 'swordDmg', req: 'sword', name: 'Kiếm phách',     desc: 'Phi kiếm +40% sát thương (không tăng chiêu khác)', max: 5, apply: p => { p.swordDmg *= 1.4; } },
    { id: 'orbitNum', req: 'orbit', name: 'Phân hỏa',       desc: '+2 hỏa cầu xoay quanh người',             max: 3, apply: p => { p.orbit += 2; } },
    { id: 'orbitDmg', req: 'orbit', name: 'Liệt hỏa hoàn',  desc: 'Hỏa cầu xoay +40% sát thương',            max: 4, apply: p => { p.orbitDmg *= 1.4; } },
    { id: 'orbitR',   req: 'orbit', name: 'Hỏa vân khuếch', desc: 'Hỏa cầu bay xa người hơn, quét rộng hơn', max: 3, apply: p => { p.orbitR += 26; } },
    { id: 'boltNum',  req: 'bolt', name: 'Vạn lôi',        desc: '+2 tia sét mỗi lần giáng',                 max: 3, apply: p => { p.bolt += 2; } },
    { id: 'boltSplash', req: 'bolt', name: 'Lôi bạo',       desc: 'Sét nổ lan rộng hơn 40%',                 max: 3, apply: p => { p.boltSplash = Math.round(p.boltSplash * 1.4); } },
    { id: 'boltEvery',  req: 'bolt', name: 'Lôi tốc',       desc: 'Sét giáng nhanh hơn 20%',                 max: 4, apply: p => { p.boltEvery = Math.max(0.6, p.boltEvery * 0.8); } },
    { id: 'novaRange', req: 'nova', name: 'Hàn vực',       desc: 'Sóng băng mở rộng tầm thêm 30%',           max: 4, apply: p => { p.novaRange += 0.3; } },
    { id: 'novaDmg',  req: 'nova', name: 'Huyền băng cực', desc: 'Sóng băng +40% sát thương',               max: 4, apply: p => { p.novaDmg *= 1.4; } },
    { id: 'novaSlow', req: 'nova', name: 'Đông kết',       desc: 'Quái trúng sóng băng bị làm chậm 50% trong 2 giây', max: 1, apply: p => { p.novaSlow = 2; } },
  ];

  /** Chọn tối đa n nâng cấp khác nhau, bỏ loại đã đạt giới hạn. levels: {id: số lần đã lấy}. */
  function pickUpgrades(rng, levels, n = 3) {
    const pool = UPGRADES.filter(u => (levels[u.id] || 0) < u.max && (!u.req || levels[u.req] > 0));
    const out = [];
    // ưu tiên 70%: một nâng cấp nhánh của chiêu đã có
    const branch = pool.filter(u => u.req);
    if (branch.length && n > 0 && rng() < 0.7) { const b = branch[Math.floor(rng() * branch.length)]; out.push(pool.splice(pool.indexOf(b), 1)[0]); }
    while (out.length < n && pool.length) out.push(pool.splice(Math.floor(rng() * pool.length), 1)[0]);
    return out;
  }

  /** Điểm sinh quái: vòng tròn ngoài màn hình, góc ngẫu nhiên 360°. */
  function spawnPoint(cx, cy, w, h, rng) {
    const a = rng() * Math.PI * 2, r = Math.hypot(w, h) / 2 + 60;
    return { x: cx + Math.cos(a) * r, y: cy + Math.sin(a) * r };
  }

  /** Lưới băm không gian để tìm quái gần điểm mà không duyệt hết. */
  class Grid {
    constructor(cell = 64) { this.cell = cell; this.map = new Map(); }
    clear() { this.map.clear(); }
    key(cx, cy) { return cx * 73856093 ^ cy * 19349663; }
    insert(o) { const k = this.key(Math.floor(o.x / this.cell), Math.floor(o.y / this.cell)); let b = this.map.get(k); if (!b) this.map.set(k, b = []); b.push(o); }
    query(x, y, r) {
      const c = this.cell, out = [], x0 = Math.floor((x - r) / c), x1 = Math.floor((x + r) / c), y0 = Math.floor((y - r) / c), y1 = Math.floor((y + r) / c);
      for (let i = x0; i <= x1; i++) for (let j = y0; j <= y1; j++) { const b = this.map.get(this.key(i, j)); if (b) for (const o of b) if (Math.hypot(o.x - x, o.y - y) <= r + (o.r || 0)) out.push(o); }
      return out;
    }
  }

  const api = { MAX_SECONDS, WAVE_SECONDS, MAX_ALIVE, waveOf, isBossWave, bossOrdinal, spawnRate, monsterStats, bossStats, MONSTER_TYPES, BOSS_TYPES, bossTypeForWave, bossCountForWave, bossHpShare, pickMonsterType, waveEvent, treeCount, flockSize, FLOCKS_PER_WAVE, FLOCK_GAP, HEAL, HEAL_KINDS, pickHealKind, healDelay, healAmount, healSpot, xpNeed, BASE_PLAYER, UPGRADES, pickUpgrades, spawnPoint, Grid };
  if (typeof module !== 'undefined' && module.exports) module.exports = api; else root.SwarmCore = api;
})(typeof window !== 'undefined' ? window : globalThis);
