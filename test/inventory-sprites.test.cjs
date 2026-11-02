const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const skins=['luyenkhi','trucco','kimdan','nguyenanh','hoathan','luyenhu','hopthe'];
function setup(ready=true){
  const file='wwwroot/js/inventory-sprites.js';
  assert(fs.existsSync(file),'inventory sprite renderer must exist');
  const loaded=[];
  const scope=vm.createContext({skImg:src=>{
    loaded.push(src);if(!ready)return null;
    const b=fs.readFileSync('wwwroot/'+src);
    return {naturalWidth:b.readUInt32BE(16),naturalHeight:b.readUInt32BE(20)};
  }});
  vm.runInContext(fs.readFileSync(file,'utf8'),scope);
  const draws=[];let depth=0;
  const g={drawImage(...a){draws.push(a)},save(){depth++},restore(){depth--}};
  return {scope,g,draws,loaded,depth:()=>depth};
}
test('all seven female inventory skins play sixteen bounded frames, pin feet and loop at two seconds',()=>{
  const {scope,g,draws,depth}=setup();
  for(const skin of skins){
    const hero=`img/hero_${skin}_nu.png`,frames=new Set();
    for(let i=0;i<16;i++){
      assert(scope.drawInventorySprite(g,hero,(i+.5)/8,false));
      const [,sx,sy,sw,sh,dx,dy,dw,dh]=draws.at(-1);
      frames.add([sx,sy,sw,sh].join(','));
      assert(sx>=0&&sy>=0&&sw>0&&sh>0&&sx+sw<=1254&&sy+sh<=1254);
      assert(dx>=10&&dx+dw<=310&&dy>=20&&dy+dh<=306.00001,'art fits inventory canvas');
      assert(Math.abs(dy+dh-306)<1e-8,'feet stay on ground');
      assert.equal(depth(),0);
    }
    assert.equal(frames.size,16);
    scope.drawInventorySprite(g,hero,0,false);const first=draws.at(-1).slice(1);
    scope.drawInventorySprite(g,hero,2,false);assert.deepEqual(draws.at(-1).slice(1),first);
    scope.drawInventorySprite(g,hero,1.2,true);assert.deepEqual(draws.at(-1).slice(1),first,'reduced motion freezes first pose');
  }
});
test('unloaded sheets and unsupported costumes leave static or legacy fallback available',()=>{
  const cold=setup(false);
  assert.equal(cold.scope.drawInventorySprite(cold.g,'img/hero_hoathan_nu.png',1,false),false);
  assert.equal(cold.draws.length,0);
  const ready=setup();
  assert.equal(ready.scope.drawInventorySprite(ready.g,'img/hero_luyenkhi.png',1,false),false);
  assert.equal(ready.scope.drawInventorySprite(ready.g,'img/custom.png',1,false),false);
  assert.equal(ready.loaded.length,0);
});
