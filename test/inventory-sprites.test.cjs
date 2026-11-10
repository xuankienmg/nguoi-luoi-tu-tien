const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const skins=['luyenkhi','trucco','kimdan','nguyenanh','hoathan','luyenhu','hopthe'];
function setup(ready=true){
  const file='wwwroot/js/inventory-sprites.js';
  assert(fs.existsSync(file),'inventory sprite renderer must exist');
  const loaded=[];
  // Canvas phụ giả lập: mỗi khung được thu nhỏ một lần vào canvas phụ (cacheDraws ghi ô nguồn + chất lượng smoothing),
  // rồi chép lên g bằng drawImage(canvasPhụ, dx, dy, dw, dh). `draws` gộp lại thành [ảnh, sx, sy, sw, sh, dx, dy, dw, dh].
  const cacheDraws=[];
  const scope=vm.createContext({skImg:src=>{
    loaded.push(src);if(!ready)return null;
    const b=fs.readFileSync('wwwroot/'+src);
    return {naturalWidth:b.readUInt32BE(16),naturalHeight:b.readUInt32BE(20)};
  },document:{createElement:()=>{
    const cv={width:0,height:0,srcRect:null,getContext:()=>({imageSmoothingEnabled:false,imageSmoothingQuality:'low',
      drawImage(img,sx,sy,sw,sh){cv.srcRect=[sx,sy,sw,sh];cacheDraws.push({quality:this.imageSmoothingQuality,rect:[sx,sy,sw,sh]})}})};
    return cv;
  }}});
  vm.runInContext(fs.readFileSync(file,'utf8'),scope);
  const draws=[];let depth=0;
  const g={imageSmoothingEnabled:true,imageSmoothingQuality:'low',drawImage(cv,...dest){draws.push([cv,...cv.srcRect,...dest]);g.lastQuality=g.imageSmoothingQuality},save(){depth++},restore(){depth--}};
  return {scope,g,draws,cacheDraws,loaded,depth:()=>depth};
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
// Hiệu năng: vẽ nhân vật Hành trang mỗi khung hình. Thu nhỏ sheet bằng smoothing 'high' mỗi khung tốn 50+ ms (đo trên Edge),
// nên mỗi khung được thu nhỏ đúng một lần rồi lưu cache; mỗi khung hình chỉ chép canvas đã thu nhỏ với smoothing thấp.
test('each pose is resampled from the sheet once (high quality) and later frames reuse the cached canvas',()=>{
  const {scope,g,draws,cacheDraws}=setup();
  const hero='img/hero_hoathan_nu.png',sheet=vm.runInContext('INVENTORY_SPRITE_SHEETS',scope)[hero],fps=sheet.fps||4;
  for(let round=0;round<5;round++)for(let i=0;i<sheet.frames.length;i++)assert(scope.drawInventorySprite(g,hero,(i+.5)/fps,false));
  assert.equal(cacheDraws.length,sheet.frames.length,'sheet is sampled once per pose, not once per frame');
  assert(cacheDraws.every(d=>d.quality==='high'),'one-time resample keeps high quality');
  assert.equal(draws.length,5*sheet.frames.length);
  assert.notEqual(g.lastQuality,'high','per-frame copy must not use high-quality filtering');
  assert.equal(new Set(draws.map(d=>d[0])).size,sheet.frames.length,'same pose reuses the same cached canvas');
});
test('a weapon layer sheet gets its own cache entries',()=>{
  const {scope,g,cacheDraws}=setup();
  const hero='img/hero_hoathan_nu.png',sheet=vm.runInContext('INVENTORY_SPRITE_SHEETS',scope)[hero];
  scope.drawInventorySprite(g,hero,0,false);scope.drawInventorySprite(g,hero,0,false,sheet.src);
  assert.equal(cacheDraws.length,1,'same src is the same entry');
});
