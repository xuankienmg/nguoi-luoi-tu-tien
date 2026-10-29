const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){
  class Image { set src(src){if(!src)return;const b=fs.readFileSync('wwwroot/'+src);this.naturalWidth=b.readUInt32BE(16);this.naturalHeight=b.readUInt32BE(20);this.onload?.();} }
  const scope=vm.createContext({Image});
  for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),scope);
  return scope;
}
test('Hoa Than effects load real 6x6 sheets and play all 36 frames in order',()=>{
  const s=load();
  for(const id of ['than_hoa','huyen_bang','cuu_tieu']){
    const spec=s.fireFxSpec(id,4);
    assert.equal(spec.frameCount,36);
    assert.equal(spec.durationMs,2000);
    assert.equal(spec.columns,6);
    assert(s.fireFxAvailable(id,4),'real PNG dimensions accepted');
    const frames=new Set();
    for(let i=0;i<36;i++){
      const plan=s.fireFxPlan(id,(i+.5)/36*.75,4);
      assert.equal(plan.frame,i);frames.add(plan.frame);
      const draws=[];
      const ctx={canvas:{width:420,height:300},globalAlpha:1,save(){},restore(){},drawImage(...args){draws.push(args);}};
      assert(s.drawFireFx(ctx,plan,{handX:80,handY:150,targetX:300,targetY:150,groundY:250,heroHeight:90,targetWidth:90,targetHeight:110}));
      const [,x,y,w,h]=draws[0];
      assert(x>=0&&y>=0&&x+w<=1254&&y+h<=1254,'frame crop stays inside atlas');
    }
    assert.equal(frames.size,36);
    assert.equal(s.fireFxPlan(id,.75,4),null);
    assert.equal(s.fireFxSpec(id,3).frameCount,16,'other realms retain their effects');
  }
});
