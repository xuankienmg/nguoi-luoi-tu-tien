const { test } = require('node:test');
const assert = require('node:assert/strict');
const C = require('../wwwroot/js/swarm-core.js');

test('đợt tăng mỗi 30 giây và Boss ở đợt 3, 6, 9…', () => {
  assert.equal(C.waveOf(0), 1);
  assert.equal(C.waveOf(29.9), 1);
  assert.equal(C.waveOf(30), 2);
  assert.equal(C.isBossWave(3), true);
  assert.equal(C.isBossWave(6), true);
  assert.equal(C.isBossWave(4), false);
  assert.equal(C.isBossWave(2), false);
  assert.equal(C.isBossWave(0), false);
  assert.deepEqual([12, 13, 14, 15, 16, 18, 20].map(C.isBossWave), [true, false, true, false, true, true, true]);   // late: mỗi 2 đợt
  assert.ok(C.monsterStats(20).hp > C.monsterStats(19).hp * 1.15);   // late tăng máu mạnh
});

test('quái mạnh dần, tốc độ và tần suất có trần', () => {
  assert.ok(C.monsterStats(10).hp > C.monsterStats(1).hp);
  assert.ok(C.monsterStats(999).speed <= 130);
  assert.ok(C.spawnRate(999) <= 16);
  assert.ok(C.bossStats(5).hp > C.monsterStats(5).hp * 10);
});

test('pickUpgrades trả về 3 loại khác nhau và bỏ loại đã đạt giới hạn', () => {
  const picks = C.pickUpgrades(Math.random, {}, 3);
  assert.equal(new Set(picks.map(u => u.id)).size, 3);
  const maxed = Object.fromEntries(C.UPGRADES.map(u => [u.id, u.max]));
  assert.equal(C.pickUpgrades(Math.random, maxed, 3).length, 0);
  maxed.hp = 0;
  assert.deepEqual(C.pickUpgrades(Math.random, maxed, 3).map(u => u.id), ['hp']);
});

test('điểm sinh quái nằm ngoài màn hình ở mọi hướng', () => {
  for (const r of [0, 0.25, 0.5, 0.75, 0.99]) {
    const p = C.spawnPoint(100, 100, 800, 600, () => r);
    assert.ok(Math.hypot(p.x - 100, p.y - 100) >= 500);
  }
});

test('Grid chỉ trả về quái trong bán kính', () => {
  const g = new C.Grid(64);
  const near = { x: 10, y: 10, r: 5 }, far = { x: 900, y: 900, r: 5 };
  g.insert(near); g.insert(far);
  assert.deepEqual(g.query(0, 0, 50), [near]);
});

test('kỹ năng mới áp dụng đúng', () => {
  const p = C.BASE_PLAYER(), ap = id => C.UPGRADES.find(u => u.id === id).apply(p);
  ap('orbit'); ap('bolt'); ap('nova'); ap('pierce'); ap('regen');
  assert.deepEqual([p.orbit, p.bolt, p.nova, p.pierce, p.regen], [2, 1, 1, 1, 2]);
  ap('orbit'); ap('orbitNum');
  assert.equal(p.orbit, 5);
  for (let i = 0; i < 10; i++) ap('armor');
  assert.equal(p.armor, 0.5);
  assert.ok(C.UPGRADES.length >= 14);
});

test('nâng cấp áp dụng đúng', () => {
  const p = { ...C.BASE_PLAYER(), hp: 10 };
  C.UPGRADES.find(u => u.id === 'hp').apply(p);
  assert.equal(p.maxHp, 140);
  assert.equal(p.hp, 10);   // Hộ thể chỉ tăng máu tối đa, không hồi máu
  C.UPGRADES.find(u => u.id === 'swords').apply(p);
  assert.equal(p.swords, 2);
});

test('nâng cấp nhánh chỉ xuất hiện sau khi có chiêu gốc', () => {
  const branchIds = C.UPGRADES.filter(u => u.req).map(u => u.id);
  assert.ok(branchIds.length >= 6);
  for (let i = 0; i < 50; i++) assert.ok(!C.pickUpgrades(Math.random, {}, 3).some(u => u.req));
  let seen = false;
  for (let i = 0; i < 50; i++) for (const u of C.pickUpgrades(Math.random, { orbit: 1 }, 3)) if (u.req) { assert.equal(u.req, 'orbit'); seen = true; }
  assert.ok(seen);
  const p = C.BASE_PLAYER();
  C.UPGRADES.find(u => u.id === 'novaSlow').apply(p);
  C.UPGRADES.find(u => u.id === 'boltEvery').apply(p);
  C.UPGRADES.find(u => u.id === 'boltNum').apply(p);
  C.UPGRADES.find(u => u.id === 'novaRange').apply(p);
  assert.equal(p.bolt, 2);
  assert.ok(Math.abs(p.novaRange - 1.3) < 1e-9);
  assert.equal(p.novaSlow, 2);
  assert.ok(p.boltEvery < 2);
});

test('phi kiếm là chiêu gốc: nâng cấp kiếm chỉ hiện khi levels.sword > 0', () => {
  const swordIds = ['swords', 'speedAtk', 'range', 'pierce', 'swordDmg'];
  for (const id of swordIds) assert.equal(C.UPGRADES.find(u => u.id === id).req, 'sword');
  for (let i = 0; i < 50; i++) assert.ok(!C.pickUpgrades(Math.random, {}, 3).some(u => swordIds.includes(u.id)));
  assert.equal(C.pickUpgrades(Math.random, { sword: 1 }, 3).some(u => u.id === 'sword'), false);
  const p = C.BASE_PLAYER(); C.UPGRADES.find(u => u.id === 'swordDmg').apply(p);
  assert.ok(Math.abs(p.swordDmg - 1.4) < 1e-9);
});

test('loại quái mở dần theo đợt và Boss luân phiên', () => {
  const ids = w => new Set(Array.from({ length: 400 }, (_, i) => C.pickMonsterType(w, () => (i + 0.5) / 400).id));
  assert.deepEqual([...ids(1)], ['normal']);
  assert.ok(ids(2).has('swift') && !ids(2).has('armored'));
  assert.equal(ids(6).size, C.MONSTER_TYPES.filter(t => t.weight > 0).length);   // quái sự kiện (Thụ yêu, Dơi yêu) không sinh ngẫu nhiên
  assert.ok(C.MONSTER_TYPES.length >= 6);
  const bosses = [3, 6, 9, 12, 15].map(w => C.bossTypeForWave(w).id);
  assert.deepEqual(bosses.slice(0, 4), C.BOSS_TYPES.map(b => b.id));
  assert.equal(bosses[4], bosses[0]);
});

test('về late mỗi đợt Boss có 2–3 Boss khác loại', () => {
  assert.deepEqual([3, 12, 14, 16, 18, 20].map(C.bossCountForWave), [1, 1, 2, 2, 3, 3]);
  const ids = [0, 1, 2].map(k => C.bossTypeForWave(18, k).id);
  assert.equal(new Set(ids).size, 3);
  assert.ok(C.bossHpShare(2) < 1 && C.bossHpShare(3) < C.bossHpShare(2) && C.bossHpShare(1) === 1);
});

test('bình máu rơi ngẫu nhiên: khoảng cách, vị trí, lượng hồi', () => {
  assert.equal(C.healDelay(() => 0), C.HEAL.minGap);
  assert.ok(C.healDelay(() => 0.999) < C.HEAL.maxGap);
  const q = C.healSpot(100, 100, () => 0.5), d = Math.hypot(q.x - 100, q.y - 100);
  assert.ok(d >= C.HEAL.near && d <= C.HEAL.far);
  assert.equal(C.healAmount(200, C.HEAL_KINDS.find(k => k.id === 'green')), 100);   // xanh hồi 50%
  assert.equal(C.pickHealKind(() => 0).id, 'green');
  assert.equal(C.pickHealKind(() => 0.99).id, 'green');   // chỉ còn bình xanh
});

test('đợt sự kiện: Thụ yêu và bầy dơi không sinh ngẫu nhiên', () => {
  assert.deepEqual([3, 4, 5, 9, 10, 14, 20].map(C.waveEvent), [null, 'trees', 'bats', 'trees', 'bats', 'trees', 'bats']);
  for (let i = 0; i < 200; i++) assert.ok(!['tree', 'bat'].includes(C.pickMonsterType(20, Math.random).id));
  assert.equal(C.treeCount(4), 3);
  assert.equal(C.flockSize(5), 10);
});
