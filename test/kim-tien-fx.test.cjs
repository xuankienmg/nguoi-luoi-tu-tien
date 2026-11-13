const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){
  class Image { set src(src){if(!src)return;const b=fs.readFileSync('wwwroot/'+src);
    if(b.toString('ascii',12,16)==='VP8X'){this.naturalWidth=b.readUIntLE(24,3)+1;this.naturalHeight=b.readUIntLE(27,3)+1;}
    else if(b.toString('ascii',12,16)==='VP8L'){const v=b.readUInt32LE(21);this.naturalWidth=(v&0x3fff)+1;this.naturalHeight=((v>>>14)&0x3fff)+1;}   // WebP lossless đơn giản
    else{this.naturalWidth=b.readUInt32BE(16);this.naturalHeight=b.readUInt32BE(20);}
    this.onload?.();} }
  const scope=vm.createContext({Image});
  for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),scope);
  return scope;
}
const ELEMENTS=['hoa','thuy','moc','tho','loi','bang','gio'];
// Ghi lại các lệnh drawImage của một lần vẽ: [nguồn, x, y, w, h] trên canvas.
function record(s,id,realm,layout){
  const calls=[];const ctx={canvas:{width:500,height:400},save(){},restore(){},drawImage(img,...v){calls.push([img,v[4],v[5],v[6],v[7]]);},
    set filter(v){},set globalAlpha(v){},get globalAlpha(){return 1},set imageSmoothingEnabled(v){}};
  s.drawFireFx(ctx,s.fireFxPlan(id,.6,realm),layout);return calls;
}
const LAYOUT={handX:145,handY:254,targetX:350,targetY:280,groundY:320,heroHeight:96,targetWidth:96,targetHeight:96};

test('Kim Tien actives play their own per-element two-layer sheet, sword lands on frame 57',()=>{
  const s=load(),D=vm.runInContext('ULTIMATE_CAST_SECONDS',s);
  const srcs=new Set();
  for(const el of ELEMENTS){
    const id=`${el}_kim_tien_active`,spec=s.fireFxSpec(id,10);
    const meta=JSON.parse(fs.readFileSync(`wwwroot/img/fx/kim-tien/${el}/sheet.json`,'utf8'));
    assert.equal(meta.skillId,id);
    assert.equal(spec.frameCount,90);assert.equal(spec.durationMs,3000);assert.equal(spec.filter,'none');
    assert.equal(spec.frameWidth,meta.frameWidth);assert.equal(spec.frameHeight,meta.frameHeight);
    assert.deepEqual([spec.anchorX,spec.anchorY],meta.anchor);
    assert(spec.span<=meta.span,'zoomed at least to align the guardian with the caster');
    assert.deepEqual([...spec.layers.map(l=>l.name)],['back','front']);
    for(const l of spec.layers){
      const m=meta.layers[l.name];
      assert.equal(l.src,`img/fx/kim-tien/${el}/${m.sheet}`);srcs.add(l.src);
      assert.deepEqual([l.x,l.y,l.w,l.h],[...m.offset,m.frameWidth,m.frameHeight]);
      assert(l.x+l.w<=spec.frameWidth && l.y+l.h<=spec.frameHeight);
    }
    assert(meta.decodedRgbaBytes<24e6,'two cropped layers stay near one full sheet');
    assert(s.fireFxAvailable(id,10),'both layers decoded with matching dimensions');assert(s.fireFxOwnsStage(id,10));
    const timing=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0);
    assert(Math.abs(timing.progress(1.9*D/3).s-.45)<1e-9,'damage lands when the sword hits');
    assert.equal(s.fireFxPlan(id,.45,10).frame,57);
  }
  assert.equal(srcs.size,ELEMENTS.length*2,'every element has its own sheets');
});

test('layer option draws only the guardian behind the hero, or only the strike in front',()=>{
  const s=load(),id='loi_kim_tien_active';
  assert(s.fireFxAvailable(id,10));
  const both=record(s,id,10,LAYOUT),back=record(s,id,10,{...LAYOUT,layer:'back'}),front=record(s,id,10,{...LAYOUT,layer:'front'});
  assert.equal(both.length,2);assert.equal(back.length,1);assert.equal(front.length,1);
  assert.deepEqual([back[0].slice(1),front[0].slice(1)],both.map(c=>c.slice(1)),'layers keep the same placement as a full draw');
  assert(front[0][1]>back[0][1],'strike layer sits toward the target');
});

test('Kim Tien ultimate covers a larger area than Chan Tien and the sword tip stays on the target',()=>{
  const s=load();
  const area=(id,realm)=>{const c=record(s,id,realm,LAYOUT);
    const x0=Math.min(...c.map(v=>v[1])),y0=Math.min(...c.map(v=>v[2])),x1=Math.max(...c.map(v=>v[1]+v[3])),y1=Math.max(...c.map(v=>v[2]+v[4]));
    return (x1-x0)*(y1-y0);};
  for(const el of ['hoa','thuy']){
    assert(s.fireFxAvailable(`${el}_chan_tien_active`,9)&&s.fireFxAvailable(`${el}_kim_tien_active`,10));
    assert(area(`${el}_kim_tien_active`,10)>area(`${el}_chan_tien_active`,9),el);
  }
  const spec=s.fireFxSpec('hoa_kim_tien_active',10),c=record(s,'hoa_kim_tien_active',10,LAYOUT);
  const k=c[0][3]/spec.layers[0].w,left=c[0][1]-spec.layers[0].x*k;
  assert(Math.abs(left+spec.anchorX*k-LAYOUT.targetX)<.5,'anchor (sword impact) is not shifted off the monster');
});
