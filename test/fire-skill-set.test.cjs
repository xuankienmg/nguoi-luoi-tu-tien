const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){
  class Image { set src(src){if(!src)return;const b=fs.readFileSync('wwwroot/'+src);
    if(b.toString('ascii',12,16)==='VP8X'){this.naturalWidth=b.readUIntLE(24,3)+1;this.naturalHeight=b.readUIntLE(27,3)+1;}   // WebP có alpha
    else if(b.toString('ascii',12,16)==='VP8L'){
      const dimensions=b.readUInt32LE(21);
      this.naturalWidth=(dimensions&0x3fff)+1;
      this.naturalHeight=((dimensions>>>14)&0x3fff)+1;
    }
    else{this.naturalWidth=b.readUInt32BE(16);this.naturalHeight=b.readUInt32BE(20);}
    this.onload?.();} }
  const scope=vm.createContext({Image});
  for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),scope);
  return scope;
}
const D=vm.runInContext('ULTIMATE_CAST_SECONDS',load()),K=D/3;   // sheet dựng 3s phát nhanh 3/D lần
const groundSets=[
  ['moc',{dang_man:19,bach_diep:19,lam_khoa:32,co_moc:30,thanh_de:33,hu_khong_kien_moc:29}],
  ['tho',{thach_dan:19,tho_tuong:19,tho_giap:32,son_nhac:30,hau_tho:33,hu_khong_tran_nhac:29}],
];
for(const [element,hits] of groundSets)test(`${element} atlases preserve impact timing and stay anchored inside the viewport`,()=>{
  const scope=load();
  for(const [id,hit] of Object.entries(hits)){
    const spec=scope.fireFxSpec(id,9);
    assert.equal(spec.element,element);
    assert.equal(spec.frameCount,60);
    assert.equal(spec.durationMs,2000);
    assert(scope.fireFxOwnsStage(id,9));
    assert(scope.fireFxAvailable(id,9));
    const metadata=JSON.parse(fs.readFileSync('wwwroot/'+spec.src.replace(/\.webp$/,'.json'),'utf8'));
    assert.deepEqual(Array.from(spec.visibleBounds),metadata.visibleBounds);
    assert.equal(metadata.impactFrame,hit);
    const battle={casts:[{turn:0,skill:id,dmg:5,ultimate:true}]};
    const timing=scope.battleSkillTiming(battle,0);
    assert.equal(timing.length,2.25);
    assert.equal(scope.fireFxPlan(id,timing.progress(hit/30).s,9).frame,hit);
    assert(!scope.battleMonsterActionReady(battle,{i:0,name:'fight',s:.749}));
    assert(scope.battleMonsterActionReady(battle,{i:0,name:'fight',s:.78}));
    for(let f=0;f<60;f++) assert.equal(scope.fireFxPlan(id,timing.progress((f+.5)/30).s,9).frame,f);
    for(const [width,height,targetX,groundY,targetHeight] of [[120,60,94,52,48],[420,300,375,250,110]]){
      const draws=[];
      const ctx={canvas:{width,height},globalAlpha:1,save(){},restore(){},drawImage(...args){draws.push(args);}};
      const layout={handX:38,handY:28,targetX,targetY:30,groundY,heroHeight:46,targetHeight,reducedMotion:true};
      assert(scope.drawFireSkillFx(ctx,id,.45,9,layout));
      const [,sx,sy,, ,dx,dy,dw,dh]=draws[0];
      assert.equal(sx/240+sy/160*10,spec.reducedFrame);
      assert(Math.abs(dx+dw*144/240-targetX)<1e-6);
      assert(Math.abs(dy+dh*127/160-groundY)<1e-6);
      const [left,top,right,bottom]=spec.visibleBounds;
      assert(dx+dw*left/240>=0 && dx+dw*right/240<=width);
      assert(dy+dh*top/160>=0 && dy+dh*bottom/160<=height);
    }
  }
});
test('high-realm elements share their Fire cast and draw their own impact at the hit',()=>{
  const scope=load();
  for (const [element,suffix] of [['moc','moc'],['tho','tho'],['loi','loi'],['bang','bang'],['gio','phong']]){
    const fireIds=['hon_nguyen_hoa','thai_so_hoa','hoa_do_kiep_active','hoa_chan_tien_active'];
    const ids=[`hon_nguyen_${suffix}`,`thai_so_${suffix}`,`${element}_do_kiep_active`,`${element}_chan_tien_active`];
    for (const [index,id] of ids.entries()){
      const spec=scope.fireFxSpec(id,10), fire=scope.fireFxSpec(fireIds[index],10);
      assert.equal(spec.skillId,id);
      assert.equal(spec.element,element);
      assert.equal(spec.src,fire.src);
      assert.equal(spec.frameCount,fire.frameCount);
      assert.equal(spec.authoredImpactMs,fire.authoredImpactMs);
      if (element === 'moc') assert.equal(spec.impactFx,null);
      else{
        assert.equal(spec.impactFx.element,element);
        assert.notEqual(spec.impactFx.src,fire.src);
      }
      assert(scope.fireFxOwnsStage(id,10));
      assert(scope.fireFxAvailable(id,10));
      const timing=scope.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0);
      const hit=spec.authoredImpactMs*D/spec.durationMs;
      assert(Math.abs(timing.progress(hit).s-.45)<1e-9);
      const layout={handX:145,handY:254,targetX:350,targetY:280,groundY:320,heroHeight:96,targetHeight:96};
      const draws=[];
      const ctx={canvas:{width:500,height:400},globalAlpha:1,save(){},restore(){},drawImage(){draws.push(this.filter);}};
      assert(scope.drawFireSkillFx(ctx,id,.44,10,layout));
      assert.deepEqual(draws,[spec.filter]);
      draws.length=0;
      assert(scope.drawFireSkillFx(ctx,id,.45,10,{...layout,reducedMotion:true}));
      assert.deepEqual(draws,element === 'moc' ? [spec.filter] : [spec.filter,'none']);
    }
  }
});
test('Wood roots are locked before damage and both layers load from runtime sheets',()=>{
  const scope=load(),draws=[];
  const ctx={globalAlpha:1,save(){},restore(){},drawImage(...args){draws.push(args);}};
  const layout={x:120,footY:80,height:40};
  for (const [id,variant,frame] of [['hon_nguyen_moc','spiral',31],['moc_kim_tien_active','cage',39]]){
    assert.equal(scope.woodRootFrame(id,.09,false),null);
    assert.equal(scope.woodRootFrame(id,.37,false).variant,variant);
    assert.equal(scope.woodRootFrame(id,.37,false).frame,frame);
    assert.equal(scope.woodRootFrame(id,.44,false).frame,frame);
    assert.equal(scope.woodRootFrame(id,.70,false),null);
    assert(scope.drawWoodRootFx(ctx,id,.37,'back',layout));
    assert(scope.drawWoodRootFx(ctx,id,.37,'front',layout));
    assert.deepEqual(draws.at(-1).slice(1),draws.at(-2).slice(1));
  }
  assert.equal(scope.woodRootFrame('thai_so_thuy',.37,false),null);
});
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
      const anchoredX=Math.abs(dx+spec.anchorX/spec.frameWidth*dw-layout.targetX)<1e-6;
      assert(anchoredX||(dx>=1.99&&dx+dw<=w-1.99),"neo lệch chỉ khi dịch vào để mép trái/phải không bị cắt");
      const y=spec.stageAnchor==='ground'?layout.groundY:layout.targetY;
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
test('Dai Thua fire ultimate (thai_so_hoa) plays its 60-frame Thien Chi sheet with impact on frame 32',()=>{
  const s=load(),spec=s.fireFxSpec('thai_so_hoa',8);
  const meta=JSON.parse(fs.readFileSync('wwwroot/'+spec.src.replace(/sheet\.\w+$/,'sheet.json'),'utf8'));
  assert.equal(meta.skillId,'thai_so_hoa');assert.equal(spec.filter,'none');
  assert.equal(spec.frameCount,60);assert.equal(spec.durationMs,2000);
  assert.deepEqual([spec.frameWidth,spec.frameHeight,spec.anchorX,spec.anchorY],[meta.frameWidth,meta.frameHeight,...meta.anchor]);
  assert(s.fireFxAvailable('thai_so_hoa',8),'real WebP dimensions accepted');
  assert(s.fireFxOwnsStage('thai_so_hoa',8));
  const timing=s.battleSkillTiming({casts:[{turn:0,skill:'thai_so_hoa',dmg:5}]},0),seen=new Set(),k=D/2;
  assert(Math.abs(timing.progress(1.034*k).s-.45)<1e-9,'damage lands on authored impact');
  assert.equal(s.fireFxPlan('thai_so_hoa',.45,8).frame,31);
  for(let i=0;i<60;i++)seen.add(s.fireFxPlan('thai_so_hoa',timing.progress((i+.5)/30*k).s,8).frame);
  assert.equal(seen.size,60);
});
test('Do Kiep fire ultimate (hoa_do_kiep_active) plays the Dich Linh An sheet and draws larger than Dai Thua',()=>{
  const s=load(),spec=s.fireFxSpec('hoa_do_kiep_active',8);
  const meta=JSON.parse(fs.readFileSync('wwwroot/'+spec.src.replace(/sheet\.\w+$/,'sheet.json'),'utf8'));
  assert.equal(meta.skillId,'hoa_do_kiep_active');assert.equal(spec.filter,'none');assert.equal(spec.frameCount,60);
  assert(s.fireFxAvailable('hoa_do_kiep_active',8));assert(s.fireFxOwnsStage('hoa_do_kiep_active',8));
  const timing=s.battleSkillTiming({casts:[{turn:0,skill:'hoa_do_kiep_active',dmg:5}]},0);
  assert(Math.abs(timing.progress(.967*D/2).s-.45)<1e-9,'damage lands on authored impact');
  assert.equal(s.fireFxPlan('hoa_do_kiep_active',.45,8).frame,29);
  // Phạm vi to dần theo cấp chiêu: so chiều cao vẽ (khung Thiên Chỉ rộng hơn nên không so bề rộng khung); Độ Kiếp cao hơn Đại Thừa.
  const layout={handX:145,handY:254,targetX:350,targetY:280,groundY:320,heroHeight:96,targetWidth:96,targetHeight:96};
  const width=id=>{let w=0;const ctx={canvas:{width:500,height:400},save(){},restore(){},drawImage(...a){w=a[8];},set filter(v){},set globalAlpha(v){},get globalAlpha(){return 1},set imageSmoothingEnabled(v){}};
    s.drawFireFx(ctx,s.fireFxPlan(id,.6,8),layout);return w;};
  assert(width('hoa_do_kiep_active')>width('thai_so_hoa'));
});
test('Chan Tien fire ultimate (hoa_chan_tien_active) plays the five-palm sheet, first palm lands the damage, widest of the fire set',()=>{
  const s=load(),id='hoa_chan_tien_active',spec=s.fireFxSpec(id,9);
  const meta=JSON.parse(fs.readFileSync('wwwroot/'+spec.src.replace(/sheet\.\w+$/,'sheet.json'),'utf8'));
  assert.equal(meta.skillId,id);assert.equal(spec.filter,'none');assert.equal(spec.frameCount,60);
  assert(s.fireFxAvailable(id,9));assert(s.fireFxOwnsStage(id,9));
  const timing=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0);
  assert(Math.abs(timing.progress(.767*D/2).s-.45)<1e-9,'damage lands on the first palm');
  assert.equal(s.fireFxPlan(id,.45,9).frame,23);
  const layout={handX:145,handY:254,targetX:350,targetY:280,groundY:320,heroHeight:96,targetWidth:96,targetHeight:96};
  const width=k=>{let w=0;const ctx={canvas:{width:500,height:400},save(){},restore(){},drawImage(...a){w=a[7];},set filter(v){},set globalAlpha(v){},get globalAlpha(){return 1},set imageSmoothingEnabled(v){}};
    s.drawFireFx(ctx,s.fireFxPlan(k,.6,9),layout);return w;};
  assert(width(id)>width('hoa_do_kiep_active'));
});
