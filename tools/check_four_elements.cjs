const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const catalog=JSON.parse(fs.readFileSync('tools/four_elements/catalog.json','utf8'));
assert(fs.existsSync('wwwroot/js/four-elements-fx-data.js'),'four-element manifest must exist');
class TestImage {
  set src(src){this.path=src;if(!src)return;this.naturalWidth=this.naturalHeight=768;this.onload?.();}
}
const sandbox=vm.createContext({Image:TestImage,Map});
for(const file of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),sandbox);
const specs=vm.runInContext('FOUR_ELEMENTS_FX_DATA',sandbox);
assert.equal(specs.length,32);
const skills=JSON.parse(fs.readFileSync('docs/four-elements-skills.json','utf8'));
assert.equal(skills.length,76);
for(const skill of skills)assert(fs.existsSync(`wwwroot/img/sk_${skill.Id}.png`),'missing icon '+skill.Id);
assert.deepEqual(new Set(specs.map(s=>s.skillId)),new Set(skills.filter(s=>s.Tree!=='passive').map(s=>s.Id)));
let checks=0;
for(const a of catalog){
  const spec=sandbox.fireFxSpec(a.skillId,6);
  assert(spec,`renderer cannot resolve ${a.skillId}`);
  assert.equal(spec.skillId,a.skillId);assert.equal(spec.motion,a.motion);
  assert(fs.existsSync('wwwroot/'+spec.src));
  assert(fs.existsSync(`wwwroot/img/sk_${a.skillId}.png`));
  const frames=new Set();
  for(let ms=0;ms<750;ms++){const p=sandbox.fireFxPlan(a.skillId,ms/1000,6);assert(p);assert(p.frame>=0&&p.frame<16);frames.add(p.frame);}
  assert.equal(frames.size,16);
  for(const [t,f] of [[0,0],[.24999,3],[.25,4],[.44999,7],[.45,8],[.59999,11],[.60,12],[.74999,15]])assert.equal(sandbox.fireFxPlan(a.skillId,t).frame,f);
  for(const t of [-.1,.75,1,Infinity,NaN])assert.equal(sandbox.fireFxPlan(a.skillId,t),null);
  assert(sandbox.fireFxAvailable(a.skillId,6));
  for(const v of [{w:120,h:60,g:52,x:94,hero:46},{w:420,h:300,g:250,x:330,hero:90},{w:960,h:440,g:370,x:690,hero:120}]){
    for(const reducedMotion of [false,true])for(const t of [0,.25,.44999,.45,.60,.74999]){
      let draw,saved,transform;
      const ctx={canvas:{width:v.w,height:v.h},globalAlpha:.7,filter:'none',imageSmoothingEnabled:false,tx:0,ty:0,angle:0,getTransform:()=>({a:1,d:1}),
        save(){saved=[this.globalAlpha,this.filter,this.imageSmoothingEnabled,this.tx,this.ty,this.angle]},restore(){[this.globalAlpha,this.filter,this.imageSmoothingEnabled,this.tx,this.ty,this.angle]=saved},
        translate(x,y){this.tx+=x;this.ty+=y},rotate(a){this.angle+=a},drawImage(...args){draw=args;transform={x:this.tx,y:this.ty,angle:this.angle}}};
      const plan=sandbox.fireFxPlan(a.skillId,t,6);
      assert(sandbox.drawFireFx(ctx,plan,{handX:v.w*.27,handY:v.g-v.hero*.55,targetX:v.x,targetY:v.g-v.hero/2,groundY:v.g,heroHeight:v.hero,targetWidth:v.hero,targetHeight:v.hero,reducedMotion}));
      assert.equal(ctx.globalAlpha,.7);assert.equal(ctx.filter,'none');assert.equal(ctx.imageSmoothingEnabled,false);
      const [,,,,,x,y,w,h]=draw,[l,u,r,b]=spec.visibleBounds;
      assert(w>0&&h>0);
      const c=Math.cos(transform.angle),sn=Math.sin(transform.angle);
      for(const [px,py] of [[l,u],[r,u],[l,b],[r,b]]){
        const dx=x+px/192*w,dy=y+py/192*h,wx=transform.x+c*dx-sn*dy,wy=transform.y+sn*dx+c*dy;
        assert(wx>=1.8&&wx<=v.w-1.8,a.skillId+' horizontal clip');assert(wy>=1.8&&wy<=v.h-1.8,a.skillId+' vertical clip');
      }
      checks++;
    }
  }
}
assert(sandbox.fireFxCacheInfo().size<=2);
// Older Fire manifests have no element field. Keep their pixel edges unchanged.
sandbox.fireFxAvailable('hoa_cau',0);
let fireSmoothing;
const fireContext={canvas:{width:420,height:300},globalAlpha:1,save(){},restore(){},drawImage(){fireSmoothing=this.imageSmoothingEnabled}};
assert(sandbox.drawFireFx(fireContext,sandbox.fireFxPlan('hoa_cau',.45,0),{handX:100,handY:150,targetX:300,targetY:150,groundY:250,heroHeight:90}));
assert.equal(fireSmoothing,false,'legacy fire pixel smoothing changed');
const failure=vm.createContext({Map,Image:class{set src(src){if(src)this.onerror?.()}}});
for(const file of ['four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`,'utf8'),failure);
for(const a of catalog){assert.equal(failure.fireFxAvailable(a.skillId),false);assert.equal(failure.drawFireFx({},failure.fireFxPlan(a.skillId,.45),{}),false)}
const html=fs.readFileSync('wwwroot/index.html','utf8');assert(html.indexOf('four-elements-fx-data.js')<html.indexOf('js/fire-fx.js'));
console.log(`PASS 32 spell mappings, 512 frames, timeline boundaries, ${checks} placements, reduced motion, context restoration, failed loads and cache limit.`);
