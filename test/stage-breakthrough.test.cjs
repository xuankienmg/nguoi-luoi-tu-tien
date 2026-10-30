const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('wwwroot/js/breakthrough.js', 'utf8');
const requirements = source.slice(source.indexOf('function btReqs()'), source.indexOf('/** Ô biểu tượng'));

function setup() {
  const context = vm.createContext({
    S: { realm: 5, isPeak: false, hasNext: true, qiReq: 100, targetCost: [], inventory: {},
      chance: .65, pillBonus: 0, break: { debuffMs: 0, cooldownMs: 0 } },
    ITEM: { pha_chuong_dan_5: { name: 'Phá Chướng Đan' } },
    qiNow: () => 100, fmt: String, fmtFull: String, fmtCompact: String,
    btLeft: x => x, secTxt: String, btLock: false, btProc: null,
  });
  vm.runInContext(requirements, context);
  return context;
}

test('missing support pill is visible and never blocks required conditions', () => {
  const c = setup();
  const optional = vm.runInContext('btReqs().find(r => r.optional)', c);
  assert.equal(optional.ic, 'pha_chuong_dan_5');
  assert.match(optional.val, /Không bắt buộc/);
  assert.equal(optional.ok, false);
  assert.equal(optional.use, false);
  assert.equal(vm.runInContext('btReady()', c), true);
  c.S.break.cooldownMs = 1000;
  assert.equal(vm.runInContext('btReady()', c), false);
});

test('support action respects availability, chance ceiling and animation lock', () => {
  const c = setup(); c.S.inventory.pha_chuong_dan_5 = 2;
  assert.equal(vm.runInContext('btReqs().find(r => r.optional).use', c), true);
  c.S.chance = .95;
  assert.equal(vm.runInContext('btReqs().find(r => r.optional).use', c), false);
  c.S.chance = .65; c.btLock = true;
  assert.equal(vm.runInContext('btReqs().find(r => r.optional).use', c), false);
});

test('small breakthrough displays actual server chance and failure outcome', () => {
  const elements = {};
  const c = vm.createContext({ peak: false, top: false, nextName: 'Trung kỳ',
    S: { chance: .65, bonus: .1, pillBonus: .1, break: { baseChance: .45 } },
    $: id => elements[id] ||= {},
  });
  const rate = source.slice(source.indexOf('  // tỉ lệ thành công'), source.indexOf('  // sau khi đột phá'));
  vm.runInContext(rate, c);
  assert.match(elements.btRate.innerHTML, /65%/);
  assert.match(elements.btRate.innerHTML, /35%/);
  assert.match(elements.btRate.innerHTML, /Thất bại/);
  assert.doesNotMatch(elements.btRate.innerHTML, /Đột phá chắc chắn|Tẩu hỏa nhập ma|Đại thành công/);
});
