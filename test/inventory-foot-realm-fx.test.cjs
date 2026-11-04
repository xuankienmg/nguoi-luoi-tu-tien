const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const inventory=fs.readFileSync('wwwroot/js/inventory.js','utf8');
test('inventory draws the foot effect after its backdrop with current realm and reduced motion',()=>{
  const code=inventory.slice(inventory.indexOf('function drawInventoryAura('),inventory.indexOf('function drawInventoryAvatar('));
  const calls=[],g={};
  const scope=vm.createContext({S:{realm:8},reduce:true,$:()=>({offsetParent:{},getContext:()=>g}),
    drawInventoryFormation:(...args)=>calls.push(['back',...args]),
    drawInventoryFootRealmFx:(...args)=>calls.push(['feet',...args])});
  vm.runInContext(code+';drawInventoryAura(15)',scope);
  assert.deepEqual(calls,[['back',g,8,15,true],['feet',g,8,15,true]]);
  assert(fs.readFileSync('wwwroot/index.html','utf8').includes('src="js/inventory-foot-realm-fx.js"'));
});
const scope=vm.createContext({});
vm.runInContext(fs.readFileSync('wwwroot/js/inventory-foot-realm-fx.js','utf8'),scope);
function render(realm,time,reduced){
  const calls=[];let depth=0;
  const g=new Proxy({}, {get:(_,key)=>{
    if(key==='save')return ()=>{depth++;calls.push(['save']);};
    if(key==='restore')return ()=>{depth--;calls.push(['restore']);};
    return (...args)=>{for(const v of args)if(typeof v==='number')assert(Number.isFinite(v));calls.push([key,...args]);};
  },set:(_,key,value)=>{calls.push(['set',key,value]);return true;}});
  assert(scope.drawInventoryFootRealmFx(g,realm,time,reduced));
  assert.equal(depth,0);return calls;
}
test('all ten realms have distinct geometry, clockwise sixty-second rotation and fixed ground anchor',()=>{
  const designs=new Set();
  for(let realm=0;realm<10;realm++){
    const calls=render(realm,15,false);
    assert.deepEqual(calls.find(c=>c[0]==='translate'),['translate',160,290]);
    assert.deepEqual(calls.find(c=>c[0]==='scale'),['scale',1,.25]);
    assert(Math.abs(calls.find(c=>c[0]==='rotate')[1]-Math.PI/2)<1e-10);
    designs.add(JSON.stringify(calls.filter(c=>!['set','save','restore','translate','scale','rotate'].includes(c[0]))));
  }
  assert.equal(designs.size,10);
});
test('reduced motion freezes both rotation and animated qi or lightning',()=>{
  for(let realm=0;realm<10;realm++)assert.deepEqual(render(realm,0,true),render(realm,98,true));
});
