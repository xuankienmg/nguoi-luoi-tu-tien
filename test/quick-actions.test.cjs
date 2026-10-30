const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/sell.js', 'utf8');
const pools = source.slice(source.indexOf('function quickPools()'), source.indexOf('function quickRow()'));
test('quick sale separates gear rarity from sellable item types and uses gem vault counts', () => {
  const context = vm.createContext({FUSE_R: ['common', 'rare'], ITEM: {
    gear: {slot:'weapon', rarity:'common', sell:3}, herb: {type:'herb', rarity:'common', sell:2},
    pill: {type:'pill', rarity:'rare', sell:10}, gem: {type:'gem', rarity:'rare', sell:5},
    currency: {type:'stone', rarity:'common', sell:0}
  }, sellInventory: () => ({gear:2, herb:4, pill:3, gem:7, currency:100})});
  vm.runInContext(pools + ';globalThis.result = quickPools()', context);
  const result = JSON.parse(JSON.stringify(context.result));
  assert.equal(result.common.n, 2);
  assert.deepEqual(result['type:herb'].pool, {herb:4});
  assert.equal(result['type:pill'].gain, 30);
  assert.equal(result['type:gem'].n, 7);
  assert.equal(result['type:stone'].n, 0);
});
test('quick fusion counts each initial rarity separately and ignores maximum rarity', () => {
  const fuse = fs.readFileSync('wwwroot/js/fuse.js', 'utf8');
  const fn = fuse.slice(fuse.indexOf('function fuseQuickCount('), fuse.indexOf('function renderFuse()'));
  const context = vm.createContext({FUSE_R:['common','uncommon','supreme'],
    S:{inventory:{low:18,next:8,top:90,herb:90}}, ITEM:{low:{slot:'weapon',base:'a'},next:{slot:'head',base:'b'},top:{slot:'weapon',base:'a'},herb:{}},
    fuseQOf:id=>({low:0,next:1,top:2,herb:0}[id]), gemFuseVault:()=>({g:17,h:8}), gemFuseLevel:id=>id==='g'?0:1});
  vm.runInContext(fn + ';globalThis.result = [fuseQuickCount(false),fuseQuickCount(true)]', context);
  assert.deepEqual(Array.from(context.result), [2,1]);
});
test('selling an item group waits for confirmation and sends only that group', async () => {
  const handlers = {}, calls = [];
  const context = vm.createContext({FUSE_R:['common','uncommon','rare','epic','legendary','mythic'],
    ITEM:{herb:{type:'herb',sell:2,rarity:'common'},pill:{type:'pill',sell:5,rarity:'common'}},
    S:{inventory:{herb:4,pill:3}}, busy:false, hideTip:()=>{}, setTimeout:()=>{},
    act:async path=>{calls.push(path);return true;},
    $:()=>({addEventListener:(event,handler)=>{handlers[event]=handler;}})});
  vm.runInContext(source + ';renderSell = () => {}; quickRarity = "type:herb";', context);
  const event = {target:{closest:()=>({dataset:{sq:'type:herb'},disabled:false})}};
  await handlers.click(event);
  assert.equal(calls.length,0);
  await handlers.click(event);
  assert.equal(calls.length,1);
  assert.equal(decodeURIComponent(calls[0]),'sell-bulk?max=9&pick=true&items=herb:4');
});
test('brewing refreshes buttons immediately after the request releases busy', async () => {
  const notify = fs.readFileSync('wwwroot/js/notify.js','utf8');
  const fn = notify.slice(notify.indexOf('async function ddBrew('),notify.indexOf('/** Đến Boss'));
  const calls = [];
  const context = vm.createContext({busy:false,S:{inventory:{pill:0}},brewOf:()=>({}),brewMax:()=>10,
    performance:{now:()=>123}, renderCraft:()=>calls.push('render'),
    act:async()=>{context.S.inventory.pill++;context.busy=false;calls.push('response');}});
  await vm.runInContext(fn+';ddBrew("pill",1)',context);
  assert.deepEqual(calls,['response','render']);
  assert.equal(context.ddAnim.item,'pill');
});
test('bag socket indicators show only valid socketed gems', () => {
  const inventory=fs.readFileSync('wwwroot/js/inventory.js','utf8');
  const fn=inventory.slice(inventory.indexOf('function bagGemPips('),inventory.indexOf('function power('));
  const context=vm.createContext({S:{gemState:{bagSlots:{gear:[['',null],['','gem','']]}}},ITEM:{gem:{rarity:'rare'}}});
  vm.runInContext(fn,context);
  assert.equal(vm.runInContext('bagGemPips("gear",0)',context),'');
  const html=vm.runInContext('bagGemPips("gear",1)',context);
  assert.equal((html.match(/filled/g)||[]).length,1);
  assert.ok(!html.includes('empty'));
});
