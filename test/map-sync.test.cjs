// Đồng bộ nhẹ: client giữ danh sách ải theo mã, lấp vào khi server bỏ trống (core.js fillMapAreas)
const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/core.js', 'utf8');
const pure = source.slice(source.indexOf('/* map-sync:start */'), source.indexOf('/* map-sync:end */'));
const load = () => { const ctx = vm.createContext({}); vm.runInContext(pure + ';globalThis.F={fillMapAreas,MAP_AREAS}', ctx); return ctx.F; };
const st = (key, areas, omitted = false) => ({map: {areasKey: key, areas, areasOmitted: omitted}});

test('full state is remembered, omitted state is filled from it', () => {
  const F = load();
  const areas = [{index: 1}, {index: 2}];
  assert.equal(F.fillMapAreas(st('K1', areas)), true);
  const lite = st('K1', [], true);
  assert.equal(F.fillMapAreas(lite), true);
  assert.deepEqual(lite.map.areas, areas);
  assert.equal(lite.map.areasOmitted, false);
});

test('omitted state with an unknown key asks for a full sync', () => {
  const F = load();
  F.fillMapAreas(st('K1', [{index: 1}]));
  assert.equal(F.fillMapAreas(st('K2', [], true)), false);
});

test('states without a key (old server) pass through untouched', () => {
  const F = load();
  const old = {map: {areas: [{index: 9}]}};
  assert.equal(F.fillMapAreas(old), true);
  assert.equal(F.MAP_AREAS.size, 0);
  assert.equal(F.fillMapAreas({}), true);
});

test('keeps only the 4 most recent maps', () => {
  const F = load();
  for (const k of ['A', 'B', 'C', 'D', 'E']) F.fillMapAreas(st(k, [{k}]));
  assert.deepEqual([...F.MAP_AREAS.keys()], ['B', 'C', 'D', 'E']);
  assert.equal(F.fillMapAreas(st('A', [], true)), false);
});
