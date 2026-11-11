const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/sect.js', 'utf8');
const pure = source.slice(source.indexOf('/* sect-pure:start */'), source.indexOf('/* sect-pure:end */'));
const ctx = vm.createContext({});
vm.runInContext(pure + ';globalThis.F={sectRoleName,sectIsOfficer,sectCanKick,sectFundProgress,sectShopState,sectWaitText,sectSeen}', ctx);
const F = ctx.F;

test('kick rules match server: leader kicks anyone but self-role, elder only disciples', () => {
  assert.equal(F.sectCanKick('leader', 'elder'), true);
  assert.equal(F.sectCanKick('leader', 'disciple'), true);
  assert.equal(F.sectCanKick('leader', 'leader'), false);
  assert.equal(F.sectCanKick('elder', 'disciple'), true);
  assert.equal(F.sectCanKick('elder', 'elder'), false);
  assert.equal(F.sectCanKick('disciple', 'disciple'), false);
  assert.equal(F.sectIsOfficer('elder'), true);
  assert.equal(F.sectIsOfficer('disciple'), false);
  assert.equal(F.sectRoleName('leader'), 'Tông chủ');
});

test('fund progress clamps and is null at max level', () => {
  assert.equal(F.sectFundProgress(250, 500), 0.5);
  assert.equal(F.sectFundProgress(900, 500), 1);
  assert.equal(F.sectFundProgress(10, null), null);
});

test('shop state: locked before sold out before poor', () => {
  const it = {minLevel: 2, limit: 3, bought: 0, price: 40};
  assert.equal(F.sectShopState(it, 1, 1000), 'locked');
  assert.equal(F.sectShopState({...it, bought: 3}, 2, 1000), 'soldout');
  assert.equal(F.sectShopState(it, 2, 39), 'poor');
  assert.equal(F.sectShopState(it, 2, 40), 'ok');
});

test('wait and last-seen text', () => {
  assert.equal(F.sectWaitText(3600 * 3 + 300), '3 giờ 5 phút');
  assert.equal(F.sectWaitText(3600), '1 giờ');
  assert.equal(F.sectWaitText(61), '2 phút');
  const now = Date.parse('2026-11-11T12:00:00Z');
  assert.equal(F.sectSeen('2026-11-11T11:58:00Z', now), 'vừa xong');
  assert.equal(F.sectSeen('2026-11-11T11:30:00Z', now), '30 phút trước');
  assert.equal(F.sectSeen('2026-11-11T09:00:00Z', now), '3 giờ trước');
  assert.equal(F.sectSeen('2026-11-09T12:00:00Z', now), '2 ngày trước');
});
