const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){
  class Image { set src(src){if(!src)return;const b=fs.readFileSync('wwwroot/'+src);
    if(b.toString('ascii',12,16)==='VP8X'){this.naturalWidth=b.readUIntLE(24,3)+1;this.naturalHeight=b.readUIntLE(27,3)+1;}   // WebP có alpha
    else{this.naturalWidth=b.readUInt32BE(16);this.naturalHeight=b.readUInt32BE(20);}
    this.onload?.();} }
  const scope=vm.createContext({Image});
  for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),scope);
  return scope;
}
const D=vm.runInContext('ULTIMATE_CAST_SECONDS',load()),K=D/3;   // sheet dựng 3s phát nhanh 3/D lần
// Bộ Hỏa chốt 06/11/2026 (outputs/skill-hoa): mỗi chiêu → sheet riêng, điểm trúng (ms).
const SET={liet_diem:950,phan_thien:950,xich_diem_thien:1600,hoa_long:1500,than_hoa:1650,hu_khong_hoa:1450,hon_nguyen_hoa:1400};
test('every fire active skill uses its own 90-frame sheet in every realm',()=>{
  const s=load();
  for(const [id,impactMs] of Object.entries(SET)){
    const meta=JSON.parse(fs.readFileSync('wwwroot/'+s.fireFxSpec(id,0).src.replace(/sheet\.\w+$/,'sheet.json'),'utf8').replace(/^﻿/,''));
    assert.equal(meta.skillId,id);
    for(const realm of [0,3,4,5,6,9]){
      const spec=s.fireFxSpec(id,realm);
      assert.equal(spec.frameCount,90);assert.equal(spec.fps,30);assert.equal(spec.durationMs,3000);
      assert.equal(spec.frameWidth,meta.frameWidth);assert.equal(spec.frameHeight,meta.frameHeight);
      assert.deepEqual([spec.anchorX,spec.anchorY],meta.anchor);assert.equal(spec.authoredImpactMs,impactMs);
    }
    assert(s.fireFxAvailable(id,0),'real WebP dimensions accepted');
    assert(fs.statSync('wwwroot/'+s.fireFxSpec(id,0).src).size<2.5e6,'runtime sheet stays small');
    assert(s.fireFxOwnsStage(id,0));
    const timing=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0),seen=new Set();
    assert(Math.abs(timing.progress(impactMs/1000*K).s-.45)<1e-9,'damage lands on authored impact');
    for(let i=0;i<90;i++)seen.add(s.fireFxPlan(id,timing.progress((i+.5)/30*K).s,0).frame);
    assert.equal(seen.size,90);
  }
});
test('fire set draws anchored on the target and stays inside the top edge',()=>{
  const s=load();
  for(const id of Object.keys(SET))for(const [w,h,layout] of [
    [420,300,{handX:80,handY:150,targetX:300,targetY:170,groundY:250,heroHeight:90,targetWidth:90,targetHeight:110}],
    [120,60,{handX:38,handY:28,targetX:94,targetY:30,groundY:52,heroHeight:46,targetWidth:48,targetHeight:48,reducedMotion:true}]]){
    for(const t of [0,.3,.45,.6,.74]){
      const draws=[],ctx={canvas:{width:w,height:h},globalAlpha:1,save(){},restore(){},drawImage(...a){draws.push(a);}};
      assert(s.drawFireSkillFx(ctx,id,t,0,layout));
      const [,sx,sy,sw,sh,dx,dy,dw,dh]=draws[0],spec=s.fireFxSpec(id,0);
      assert(sx>=0&&sy>=0&&sx+sw<=spec.frameWidth*10&&sy+sh<=spec.frameHeight*9);
      assert(dy>=0,'art not clipped at the top');
      const y=spec.stageAnchor==='ground'?layout.groundY:layout.targetY;
      assert(Math.abs(dx+spec.anchorX/spec.frameWidth*dw-layout.targetX)<1e-6);
      assert(Math.abs(dy+spec.anchorY/spec.frameHeight*dh-y)<1e-6);
    }
  }
});
test('long ultimate turns keep hero and monster motion at normal speed',()=>{
  const s=load();
  for(const [id,impactMs] of Object.entries(SET)){
    const t=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0),hit=impactMs/1000*K;
    assert(Math.abs(t.progress(.3).a-.3)<1e-9,'hero cast pose runs in real seconds');
    assert(t.progress(hit-.01).a<.45,'no hit reaction before impact');
    assert(Math.abs(t.progress(hit+.1).a-.55)<1e-9,'monster flinch runs in real seconds after impact');
    assert(t.progress(D-.01).a<.75 && t.progress(D-.01).s<.75,'counter still waits for the last frame');
  }
  const normal=s.battleSkillTiming({casts:[]},0).progress(.5);
  assert.equal(normal.a,normal.s);
});
