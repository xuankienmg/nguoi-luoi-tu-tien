const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const data=JSON.parse(fs.readFileSync('site/data/wiki.json','utf8')),ITEM=Object.fromEntries(data.items.map(i=>[i.id,i]));
const gear=data.items.find(i=>i.slot==='weapon');
const c={S:{inventory:{[gear.id]:2,linh_thach:100},gemState:{vault:{stone_viem_so:9,gem_ruby_xin:3}}},DATA:data,ITEM,invCat:'all',gearSort:'name',GEAR_SORTS:{name:{key:i=>i.name}},catOf:i=>i.type==='gem'?'material':'other'};
const source=fs.readFileSync('wwwroot/js/inventory.js','utf8');vm.runInNewContext(source.slice(source.indexOf('const bagCount ='),source.indexOf('function power(st)')),c);
const list=c.bagList();assert.deepEqual(Array.from(list.filter(x=>x.k===gear.id),x=>x.copy),[0,1]);assert.equal(list.filter(x=>x.k===gear.id).length,2);assert.equal(list.find(x=>x.k==='stone_viem_so').n,9);assert.equal(list.find(x=>x.k==='gem_ruby_xin').n,3);
c.invCat='material';assert.ok(c.bagList().every(x=>ITEM[x.k].type==='gem'));
c.S.gemState.vault.stone_viem_so=0;assert.ok(!c.bagList().some(x=>x.k==='stone_viem_so'));
console.log('PASS: gems visible in inventory with correct stacks, material filter and live counts; equipment remains one cell per item');
