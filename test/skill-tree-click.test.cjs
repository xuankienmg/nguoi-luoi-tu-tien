const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), vm = require('node:vm');
const src = fs.readFileSync('wwwroot/js/skill-preview.js', 'utf8');
const init = src.slice(src.indexOf('function initSkill('));

// Khung cây .sk3-tree mang data-el (vẽ đường nối) → trình xử lý không được coi click bên trong là bấm chọn hệ.
function setup(){
  let handler; const calls = [];
  const pane = {addEventListener:(t, h) => { if (t === 'click' && !handler) handler = h; }, querySelectorAll:() => []};
  const ctx = vm.createContext({$:() => pane, act:p => calls.push(p), hideTip:() => {}, renderSkill:() => calls.push('render'),
    skEl:'hoa', skSel:'', skFade:0, skAnimT0:0, performance:{now:() => 0}, localStorage:{setItem(){}}, document:{addEventListener(){}}, window:{}});
  vm.runInContext(init + ';initSkill()', ctx);
  return {click: target => handler({target}), calls, ctx};
}
// phần tử giả: closest() trả về theo bảng bộ chọn
const el = (map, dataset = {}, disabled = false) => ({dataset, disabled, closest:sel => map[sel] ?? null});

test('nút + trong cây công pháp (khung có data-el) gửi lệnh lĩnh ngộ', () => {
  const {click, calls} = setup();
  const tree = el({}, {el:'hoa'}), up = el({}, {up:'hoa_than_viem_tam'});
  click(el({'[data-el]':tree, '.sk-els [data-el]':null, '[data-up]':up}));
  assert.deepEqual(calls, ['skill/hoa_than_viem_tam']);
});
test('thanh chọn hệ vẫn đổi hệ', () => {
  const {click, calls, ctx} = setup();
  const tab = el({}, {el:'thuy'});
  click(el({'[data-el]':tab, '.sk-els [data-el]':tab}));
  assert.equal(ctx.skEl, 'thuy'); assert.deepEqual(calls, ['render']);
});
