const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('wwwroot/js/breakthrough.js', 'utf8');
const code = source.slice(source.indexOf('const ITEM_GROUPS'), source.indexOf('let gearSort'));

// Danh mục giả lập thứ tự nối của server: nhóm thêm sau nằm cuối DATA.items.
const items = [
  { id: 'linh_thach', type: 'stone', rarity: 'common' },
  { id: 'tu_khi_dan', type: 'pill', tier: 1, rarity: 'common' },
  { id: 'dai_thua_dan', type: 'pill', tier: 8, rarity: 'immortal' },
  { id: 'tu_linh_thao', type: 'herb', rarity: 'common' },
  { id: 'cuu_diep_hon_lien', type: 'herb', rarity: 'legendary' },
  { id: 'pha_chuong_dan_0', type: 'elixir', rarity: 'rare' },
  { id: 'do_kiep_dan', type: 'pill', tier: 9, rarity: 'dao' },
  { id: 'pha_chuong_dan_7', type: 'elixir', rarity: 'immortal' },
  { id: 'nguyet_lo_hoa', type: 'herb', rarity: 'uncommon' },
  { id: 'tu_khi_dan_trung', type: 'pill', tier: 1, rarity: 'common' },
  { id: 'tu_khi_dan_thuong', type: 'pill', tier: 1, rarity: 'common' },
  { id: 'tu_linh_dan', type: 'elixir', tier: 2, rarity: 'uncommon' },
];
const ctx = vm.createContext({ DATA: { items, slots: [] }, RAR_KEYS: ['common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic', 'divine', 'immortal', 'dao'] });
vm.runInContext(code + ';this.GS = GEAR_SORTS;', ctx);
const order = () => [...items].sort((a, b) => ctx.GS.slot.key(a) - ctx.GS.slot.key(b)).map(i => i.id);

test('default sort groups pills by tier and quality, not catalog append order', () => {
  assert.deepEqual(order(), [
    'linh_thach',
    'tu_khi_dan', 'tu_khi_dan_trung', 'tu_khi_dan_thuong', 'dai_thua_dan', 'do_kiep_dan',
    'pha_chuong_dan_0', 'pha_chuong_dan_7',
    'tu_linh_dan',
    'tu_linh_thao', 'nguyet_lo_hoa', 'cuu_diep_hon_lien',
  ]);
});

test('items added at the end of the catalog still land inside their group', () => {
  items.push({ id: 'ket_anh_dan_thuong', type: 'pill', tier: 4, rarity: 'epic' });
  ctx.DATA.items = [...items];   // DATA tải lại → bộ nhớ đệm thứ tự tính lại
  const o = order();
  assert.ok(o.indexOf('ket_anh_dan_thuong') > o.indexOf('tu_khi_dan_thuong'));
  assert.ok(o.indexOf('ket_anh_dan_thuong') < o.indexOf('dai_thua_dan'));
});

test('bag keeps its order; new items append at the end until the player sorts', () => {
  const inv = fs.readFileSync('wwwroot/js/inventory.js', 'utf8');
  const bag = inv.slice(inv.indexOf('const bagCount'), inv.indexOf('function bagGemPips'));
  const store = {};
  const items2 = [
    { id: 'tu_khi_dan', type: 'pill', tier: 1, name: 'Tụ Khí Đan' },
    { id: 'truc_co_dan', type: 'pill', tier: 2, name: 'Trúc Cơ Đan' },
    { id: 'linh_thach', type: 'stone', name: 'Linh thạch' },
    { id: 'tu_linh_thao', type: 'herb', rarity: 'common', name: 'Tụ Linh Thảo' },
  ];
  const c = vm.createContext({
    DATA: { items: items2, slots: [] }, ITEM: Object.fromEntries(items2.map(i => [i.id, i])),
    RAR_KEYS: ['common', 'uncommon'], S: { id: 7, inventory: { tu_linh_thao: 1, linh_thach: 5 } },
    invCat: 'all', gearSort: 'slot', catOf: () => 'material',
    localStorage: { getItem: k => store[k] ?? null, setItem: (k, v) => { store[k] = v; }, removeItem: k => { delete store[k]; } },
  });
  vm.runInContext(code + bag + ';this.ids = () => bagList().map(x => x.k);', c);
  assert.deepEqual(c.ids(), ['linh_thach', 'tu_linh_thao']);
  c.S.inventory.truc_co_dan = 1; c.S.inventory.tu_khi_dan = 2;   // nhận đan mới
  assert.deepEqual(c.ids(), ['linh_thach', 'tu_linh_thao', 'tu_khi_dan', 'truc_co_dan']);
  vm.runInContext('resetBagOrder()', c);                         // chọn kiểu sắp xếp / bấm Sắp xếp
  assert.deepEqual(c.ids(), ['linh_thach', 'tu_khi_dan', 'truc_co_dan', 'tu_linh_thao']);
});
