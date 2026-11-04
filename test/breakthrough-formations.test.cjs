const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('wwwroot/js/breakthrough.js','utf8');
function setup(ready=true){
  const start=source.indexOf('function btDrawFormation(');
  assert(start>=0,'breakthrough uses the new formation renderer');
  const end=source.indexOf('function btBackdrop(',start);
  const calls=[],loaded=[],image={naturalWidth:1254};let depth=0;
  const scope=vm.createContext({skImg:path=>{loaded.push(path);return ready?image:null;}});
  vm.runInContext(source.slice(start,end),scope);
  const g={save(){depth++;},restore(){depth--;},translate(...a){calls.push(['translate',...a]);},rotate(a){calls.push(['rotate',a]);},drawImage(...a){calls.push(['draw',...a]);}};
  return {scope,g,calls,loaded,image,depth:()=>depth};
}
test('breakthrough follows all ten visual realms and draws the new seal behind the meditator',()=>{
  const {scope,g,calls,loaded,image,depth}=setup();
  for(let realm=0;realm<10;realm++){
    assert.equal(scope.btDrawFormation(g,realm,0,false,.8),image);
    assert.equal(loaded.at(-1),`img/fx/inventory-formations-v1/tier-${String(realm+1).padStart(2,'0')}.png`);
    assert(fs.existsSync('wwwroot/'+loaded.at(-1)));
    assert.deepEqual(calls.filter(c=>c[0]==='translate').at(-1),['translate',48,40]);
    assert.deepEqual(calls.filter(c=>c[0]==='draw').at(-1).slice(2),[-37,-37,74,74]);
    assert.equal(depth(),0);
  }
});
test('breakthrough formation turns clockwise at forty seconds per revolution and freezes with reduced motion',()=>{
  const {scope,g,calls}=setup();
  for(const [time,reduced,angle] of [[10,false,Math.PI/2],[20,false,Math.PI],[40,false,0],[20,true,0]]){
    scope.btDrawFormation(g,2,time,reduced,.8);
    assert(Math.abs(calls.filter(c=>c[0]==='rotate').at(-1)[1]-angle)<1e-10);
  }
});
test('unloaded seal waits safely without drawing the old halo',()=>{
  const {scope,g,calls,depth}=setup(false);
  assert.equal(scope.btDrawFormation(g,0,1,false,.8),null);
  assert.equal(calls.length,0);
  assert.equal(depth(),0);
});
