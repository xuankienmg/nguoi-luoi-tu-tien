const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){
  class Image { set src(src){if(!src)return;const b=fs.readFileSync('wwwroot/'+src);this.naturalWidth=b.readUInt32BE(16);this.naturalHeight=b.readUInt32BE(20);this.onload?.();} }
  const scope=vm.createContext({Image});
  for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),scope);
  return scope;
}
const realms = [
  [4,['than_hoa','thien_ha','thanh_de','hau_tho','cuu_tieu','huyen_bang','cuu_thien_phong']],
  [5,['hu_khong_hoa','hu_hai','hu_khong_kien_moc','hu_khong_tran_nhac','hu_khong_kiep_loi','hu_khong_bang_nguc','hu_khong_phong_nhan']],
];
for(const [realm,ids] of realms)test(`Realm ${realm} effects load real 6x6 sheets and play all 36 frames in order`,()=>{
  const s=load();
  for(const id of ids){
    const spec=s.fireFxSpec(id,realm);
    assert.equal(spec.frameCount,36);
    assert.equal(spec.durationMs,2000);
    assert.equal(spec.columns,6);
    assert(s.fireFxAvailable(id,realm),'real PNG dimensions accepted');
    const frames=new Set();
    for(let i=0;i<36;i++){
      const plan=s.fireFxPlan(id,(i+.5)/36*.75,realm);
      assert.equal(plan.frame,i);frames.add(plan.frame);
      const draws=[];
      const ctx={canvas:{width:420,height:300},globalAlpha:1,save(){},restore(){},drawImage(...args){draws.push(args);}};
      assert(s.drawFireFx(ctx,plan,{handX:80,handY:150,targetX:300,targetY:150,groundY:250,heroHeight:90,targetWidth:90,targetHeight:110}));
      const [,x,y,w,h]=draws[0];
      assert(x>=0&&y>=0&&x+w<=1254&&y+h<=1254,'frame crop stays inside atlas');
    }
    assert.equal(frames.size,36);
    assert.equal(s.fireFxPlan(id,.75,realm),null);
    assert.equal(s.fireFxSpec(id,3).frameCount,16,'other realms retain their effects');
    assert.equal(s.fireFxSpec(id,6).frameCount,16,'higher realms retain their effects');
    for(const time of [0,.25,.45,.6,.74999]){
      const draws=[];
      const ctx={canvas:{width:120,height:60},globalAlpha:1,save(){},restore(){},drawImage(...args){draws.push(args);}};
      const layout={handX:38,handY:28,targetX:94,targetY:30,groundY:52,heroHeight:46,targetWidth:48,targetHeight:48,reducedMotion:true};
      assert(s.drawFireSkillFx(ctx,id,time,realm,layout));
      const [,sx,sy,sw,sh,dx,dy,dw,dh]=draws[0];
      assert(sx>=0&&sy>=0&&sx+sw<=1254&&sy+sh<=1254);
      assert(dx>=0&&dy>=0&&dx+dw<=120&&dy+dh<=60,'preview stays within canvas');
    }
    assert(s.drawFireSkillFx({},id,.9,realm,{}),'rest phase suppresses legacy effects');
  }
});
