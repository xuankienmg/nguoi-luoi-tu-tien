const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const src = fs.readFileSync('wwwroot/js/forge.js', 'utf8');
const pure = src.slice(src.indexOf('const FORGE_KINDS'), src.indexOf('function renderForge('));

test('odds rows follow server order and format percent', () => {
  const ctx = vm.createContext({});
  vm.runInContext(pure + ';globalThis.r = forgeOddsRows([0.08,0.473,0.15,0.12,0.177])', ctx);
  const rows = JSON.parse(JSON.stringify(ctx.r));
  assert.deepEqual(rows.map(r => r.kind), ['great','success','hold','damage','break']);
  assert.equal(rows[1].pct, '47,3%');
  assert.equal(rows[4].label, 'Vỡ nát');
});

test('log filter counts every kind and filters entries', () => {
  const ctx = vm.createContext({});
  const log = [{kind:'success'},{kind:'break'},{kind:'success'},{kind:'undo'}];
  vm.runInContext(pure + `;globalThis.r = [forgeLogCounts(${JSON.stringify(log)}), forgeLogFilter(${JSON.stringify(log)}, 'success').length]`, ctx);
  const [counts, n] = JSON.parse(JSON.stringify(ctx.r));
  assert.equal(counts.all, 4);
  assert.equal(counts.success, 2);
  assert.equal(counts.undo, 1);
  assert.equal(n, 2);
});

test('strike rings immediately and only a break adds a shatter sound', async () => {
  const events = [];
  let click;
  const ctx = vm.createContext({
    document: {addEventListener: (_, handler) => { click = handler; }},
    Audio: class {
      constructor(url) { this.url = url; }
      play() { events.push(this.url); return Promise.reject(new Error('Playback blocked')); }
    },
    S: {forge: {log: []}},
    setTimeout() {},
    clearTimeout() {},
    act: async action => { events.push(action); return Boolean(ctx.S.forge.log.length); },
  });
  vm.runInContext(src + '\nforgeSlot = "weapon"; renderForge = () => {};', ctx);

  const press = (action, disabled = false) => click({
    target: {closest: selector => selector === '[data-fg]' ? {dataset: {fg: action}, disabled} : null},
  });
  await press('strike', true);
  await press('strike');
  await press('undo');

  ctx.S.forge.log = [{kind: 'damage'}];
  await press('strike');
  ctx.S.forge.log = [{kind: 'break'}];
  await press('strike');

  assert.deepEqual(events, [
    'audio/forge-strike.wav', 'forge/strike/weapon', 'forge/undo',
    'audio/forge-strike.wav', 'forge/strike/weapon',
    'audio/forge-strike.wav', 'forge/strike/weapon', 'audio/forge-break.wav',
  ]);
});

test('salvage list keeps only bag gear with points per rarity', () => {
  const ctx = vm.createContext({});
  vm.runInContext(pure + `;globalThis.r = forgeSalvageRows({a:2,b:1,herb:9,c:0},
    {a:{name:'Kiếm',slot:'weapon',base:'k',rarity:'rare'},b:{name:'Giáp',slot:'body',base:'g',rarity:'common'},herb:{name:'Cỏ'},c:{name:'Nón',slot:'head',base:'n',rarity:'rare'}},
    ['common','uncommon','rare'], [1,2,4])`, ctx);
  const rows = JSON.parse(JSON.stringify(ctx.r));
  assert.deepEqual(rows.map(x => [x.id, x.n, x.each]), [['a',2,4],['b',1,1]]);
});
