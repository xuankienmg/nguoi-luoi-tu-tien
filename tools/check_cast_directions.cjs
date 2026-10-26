const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const scope=vm.createContext({Map,Image:class{set src(value){if(value){this.naturalWidth=this.naturalHeight=768;this.onload?.()}}}});
for(const name of ['fire-fx-data','water-wood-fx-data','four-elements-fx-data','fire-fx'])vm.runInContext(fs.readFileSync(`wwwroot/js/${name}.js`,'utf8'),scope);
const specs=vm.runInContext('FOUR_ELEMENTS_FX_DATA',scope),directions=new Set(specs.map(s=>s.castDirection));
assert.deepEqual(directions,new Set(['left','above','below','center']),'four distinct cast directions must be configured');
const view={handX:140,handY:235,targetX:620,targetY:235,groundY:340,heroHeight:150,targetWidth:150,targetHeight:150};
function context(width=840,height=420){
  let saved;const calls=[];
  return {canvas:{width,height},globalAlpha:1,filter:'none',imageSmoothingEnabled:false,tx:0,ty:0,angle:0,calls,
    save(){saved=[this.globalAlpha,this.filter,this.imageSmoothingEnabled,this.tx,this.ty,this.angle]},
    restore(){[this.globalAlpha,this.filter,this.imageSmoothingEnabled,this.tx,this.ty,this.angle]=saved},
    translate(x,y){this.tx+=x;this.ty+=y},rotate(a){this.angle+=a},
    drawImage(...args){const [,,,,,x,y,w,h]=args,c=Math.cos(this.angle),s=Math.sin(this.angle),corners=[[x,y],[x+w,y],[x,y+h],[x+w,y+h]].map(([px,py])=>[this.tx+c*px-s*py,this.ty+s*px+c*py]);calls.push({args,corners,angle:this.angle})}};
}
function position(skill,t,layout=view,reducedMotion=false,w=840,h=420){
  const ctx=context(w,h),plan=scope.fireFxPlan(skill,t,6);scope.fireFxAvailable(skill,6);
  const placed=scope.fireFxPlacement(plan,{...layout,reducedMotion},w,h);
  assert(placed);assert(scope.drawFireFx(ctx,plan,{...layout,reducedMotion}));
  assert.equal(ctx.tx,0);assert.equal(ctx.ty,0);assert.equal(ctx.angle,0);assert.equal(ctx.globalAlpha,1);
  return {placed,call:ctx.calls.at(-1),plan};
}
for(const [direction,skill] of [['left','thach_dan'],['above','son_nhac'],['below','dong_dia'],['center','tho_giap']]){
  const early=position(skill,.25).placed,mid=position(skill,.35).placed,hit=position(skill,.45).placed;
  assert.equal(scope.fireFxPlan(skill,.25).castDirection,direction);
  if(direction==='left')assert(early.x<mid.x&&mid.x<hit.x,'horizontal cast must move right');
  if(direction==='above')assert(early.y<mid.y&&mid.y<hit.y,'descending cast must move down');
  if(direction==='below')assert(early.y>mid.y&&mid.y>hit.y,'rising cast must move up');
  if(direction==='center'){assert.equal(early.x,hit.x);assert.equal(early.y,hit.y);assert(early.size<hit.size,'central burst must expand');}
  assert.equal(hit.x,view.targetX);assert.equal(hit.y,view.targetY);
  const reduced=position(skill,.25,view,true).placed;assert.equal(reduced.x,hit.x);assert.equal(reduced.y,hit.y);
}
let checks=0;
for(const spec of specs)for(const v of [{w:120,h:60,layout:{handX:32,handY:28,targetX:94,targetY:28,groundY:52,heroHeight:46,targetWidth:46,targetHeight:46}},{w:420,h:300,layout:{handX:110,handY:170,targetX:330,targetY:190,groundY:250,heroHeight:90,targetWidth:90,targetHeight:120}},{w:840,h:420,layout:view}]){
  for(const t of [0,.2499,.25,.30,.35,.40,.44999,.45,.60,.7499])for(const reduce of [false,true]){
    const {placed,plan,call}=position(spec.skillId,t,v.layout,reduce,v.w,v.h);
    // Actual transformed visible-bounds corners must stay in the canvas.
    const [left,top,right,bottom]=spec.visibleBounds,c=Math.cos(call.angle),s=Math.sin(call.angle);
    const [,,,,,dx,dy,dw,dh]=call.args;
    const world=([px,py])=>{let x=dx+px/192*dw,y=dy+py/192*dh;return call.angle?[placed.x+c*x-s*y,placed.y+s*x+c*y]:[x,y]};
    for(const point of [[left,top],[right,top],[left,bottom],[right,bottom]]){const [x,y]=world(point);assert(x>=1.9&&x<=v.w-1.9,spec.skillId+' horizontal clipping');assert(y>=1.9&&y<=v.h-1.9,spec.skillId+' vertical clipping');}
    if(plan.phase>=2)assert.equal(call.angle,0,'impact art must remain upright');checks++;
  }
}
assert.equal(position('loi_kich',.35).call.angle,Math.PI/2,'thunder bolt must face downward during descent');
assert.equal(position('son_nhac',.35).call.angle,0,'falling mountain must remain upright');
assert.equal(scope.fireFxSpec('hoa_cau',0).castDirection,undefined,'legacy spells must retain their path');
console.log(`PASS four cast paths, shared impact time, central expansion, vertical orientation, reduced motion and ${checks} transformed placements.`);
