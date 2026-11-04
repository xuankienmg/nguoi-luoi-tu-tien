const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('wwwroot/js/inventory.js','utf8');
function setup(ready=true){
  const start=source.indexOf('function drawInventoryFormation(');
  assert(start>=0,'formation renderer exists');
  const end=source.indexOf('function drawStandAura(',start);
  const loaded=[],calls=[];
  const scope=vm.createContext({skImg:path=>{loaded.push(path);return ready?{naturalWidth:1254,naturalHeight:1254}:null;}});
  vm.runInContext(source.slice(start,end),scope);
  let depth=0;
  const g={save(){depth++;},restore(){depth--;},setTransform(){},clearRect(...a){calls.push(['clear',...a]);},translate(...a){calls.push(['translate',...a]);},rotate(a){calls.push(['rotate',a]);},drawImage(...a){calls.push(['draw',...a]);}};
  return {scope,g,loaded,calls,depth:()=>depth};
}
test('all ten realms use their own formation behind the avatar',()=>{
  const {scope,g,loaded,calls,depth}=setup();
  for(let tier=0;tier<10;tier++){
    assert.equal(scope.drawInventoryFormation(g,tier,0,false),true);
    assert.equal(loaded.at(-1),`img/fx/inventory-formations-v1/tier-${String(tier+1).padStart(2,'0')}.png`);
    assert(fs.existsSync('wwwroot/'+loaded.at(-1)));
    assert.deepEqual(calls.filter(c=>c[0]==='translate').at(-1),['translate',160,154]);
    assert.deepEqual(calls.filter(c=>c[0]==='draw').at(-1).slice(2),[-143,-143,286,286]);
    assert.equal(depth(),0);
  }
});
test('clockwise rotation takes forty seconds and reduced motion holds zero',()=>{
  const {scope,g,calls}=setup();
  for(const [time,reduced,expected] of [[0,false,0],[10,false,Math.PI/2],[20,false,Math.PI],[40,false,0],[13,true,0]]){
    scope.drawInventoryFormation(g,4,time,reduced);
    assert(Math.abs(calls.filter(c=>c[0]==='rotate').at(-1)[1]-expected)<1e-10);
  }
});
test('unloaded assets clear the old effect and keep canvas state balanced',()=>{
  const {scope,g,calls,depth}=setup(false);
  assert.equal(scope.drawInventoryFormation(g,0,2,false),false);
  assert(calls.some(c=>c[0]==='clear'));
  assert(!calls.some(c=>c[0]==='draw'));
  assert.equal(depth(),0);
});
