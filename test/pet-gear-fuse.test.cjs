const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/fuse.js','utf8');
const code = source.slice(source.indexOf('let petFuseSel ='), source.indexOf('let petQF ='));
function context(inventory) {
  const gear = Array.from({length:10},(_,tier)=>['a','b','c'].map(slot=>({id:slot+tier,slot,tier}))).flat();
  const ctx = vm.createContext({S:{inventory,pet:{training:{gear,tierNames:Array(10).fill('tier'),fuseCount:5,fuseChances:[1,.95,.85,.75,.65,.55,.45,.25,.15],fuseStones:Array(10).fill(5000)}}},stones:()=>1000000});
  vm.runInContext(code,ctx); return ctx;
}
test('auto fill takes five same-slot items or five mixed items',()=>{
  const same = context({a0:5,b0:2});
  assert.deepEqual(Array.from(vm.runInContext('petFuseAuto()',same)),Array(5).fill('a0'));
  const mixed = context({a0:2,b0:2,c0:1});
  const picked = Array.from(vm.runInContext('petFuseAuto()',mixed));
  assert.equal(picked.length,5); assert.equal(new Set(picked).size,3);
  assert.equal(vm.runInContext('petFuseQuickCount()',mixed),1);
});
test('quick count excludes four leftovers, max tier and newly crafted items',()=>{
  assert.equal(vm.runInContext('petFuseQuickCount()',context({a0:4})),0);
  assert.equal(vm.runInContext('petFuseQuickCount()',context({a9:50})),0);
  assert.equal(vm.runInContext('petFuseQuickCount()',context({a0:11,a1:4,b1:5})),3);
  assert.equal(vm.runInContext('petFuseQuickCount()',context({a8:5})),1);
});

test('render shows server chance and failed-fusion refund, not guaranteed success',()=>{
  const ctx = context({a8:5});
  const pane = {innerHTML:'',querySelector:()=>({scrollTop:0})};
  Object.assign(ctx, {$:()=>pane, ITEM:Object.fromEntries(ctx.S.pet.training.gear.map(g=>[g.id,{name:g.id,rarity:'rare'}])),
    stones:()=>1000000, fmt:String, short:String, itemTip:()=>'', itemImg:()=>'', lcNav:()=>'', fuseTypePicker:()=>'',
    FUSE_GO:'', FUSE_GRID:'', busy:false,fitLcBag:()=>{}});
  vm.runInContext(source.slice(source.indexOf('let petQF ='),source.indexOf("$('pane-petfuse').addEventListener('click'",source.indexOf('let petQF ='))),ctx);
  vm.runInContext("petFuseSel = Array(5).fill('a8'); renderPetFuse()",ctx);
  assert.match(pane.innerHTML, /<b>15%<\/b>/);
  assert.match(pane.innerHTML, /Thất bại 85%: hoàn 1 món nguyên liệu cùng phẩm, mất 4 món và phí linh thạch/);
  assert.doesNotMatch(pane.innerHTML,/Chắc chắn thành công/);
});
