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
// Mỗi sheet tự khai báo số khung/FPS (sheet 64 khung 16 FPS = 4 giây; sheet cũ 16 khung, FPS mặc định 4 = 4 giây).
test('all seven female inventory skins play every declared frame in bounds, pin feet and loop seamlessly',()=>{
  const {scope,g,draws,depth}=setup();
  const sheets=vm.runInContext('INVENTORY_SPRITE_SHEETS',scope);
  for(const skin of skins){
    const hero=`img/hero_${skin}_nu.png`,sheet=sheets[hero],frames=new Set();
    assert(sheet,`${hero} has an inventory sheet`);
    const fps=sheet.fps||4,count=sheet.frames.length,loop=count/fps;
    assert([16,64].includes(count),`${hero} uses 16 or 64 frames`);
    for(let i=0;i<count;i++){
      assert(scope.drawInventorySprite(g,hero,(i+.5)/fps,false));
      const [,sx,sy,sw,sh,dx,dy,dw,dh]=draws.at(-1);
      frames.add([sx,sy,sw,sh].join(','));
      assert(sx>=0&&sy>=0&&sw>0&&sh>0&&sx+sw<=sheet.width&&sy+sh<=sheet.height,`${hero} frame ${i} inside sheet`);
      assert(dx>=10&&dx+dw<=310&&dy>=20&&dy+dh<=306.00001,'art fits inventory canvas');
      assert(Math.abs(dy+dh-306)<1e-8,'feet stay on ground');
      assert.equal(depth(),0);
    }
    assert.equal(frames.size,count,`${hero} frames are all distinct`);
    scope.drawInventorySprite(g,hero,0,false);const first=draws.at(-1).slice(1);
    scope.drawInventorySprite(g,hero,loop,false);assert.deepEqual(draws.at(-1).slice(1),first,`${hero} loops after ${loop}s`);
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
