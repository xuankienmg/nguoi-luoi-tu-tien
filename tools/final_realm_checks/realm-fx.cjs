const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const context=vm.createContext({S:{realm:2}});
vm.runInContext(fs.readFileSync('wwwroot/js/skin-fx.js','utf8'),context);
const themes=new Set();
for(let realm=2;realm<=9;realm++){
 context.S.realm=realm;
 const a=context.skinFxTheme('img/hero_luyenkhi.png'),b=context.skinFxTheme('img/final_realms/hero_chantien_nu.png');
 assert.ok(a,`Realm ${realm} must have effects regardless of outfit`);
 assert.equal(a,b,'Changing outfits must retain the realm effect');themes.add(a.kind);
}
assert.equal(themes.size,8,'Eight realms need eight distinct visual themes');
for(const realm of [0,1]){context.S.realm=realm;assert.equal(context.skinFxTheme('img/final_realms/hero_chantien_nam.png'),null);}
function render(realm,layer,action,reduced,time){
 context.S.realm=realm;const stack=[],trace=[];
 const gradient={addColorStop(){}};
 const g=new Proxy({globalAlpha:.7,save(){stack.push({globalAlpha:this.globalAlpha,lineWidth:this.lineWidth,strokeStyle:this.strokeStyle,fillStyle:this.fillStyle});},restore(){assert.ok(stack.length,'Unbalanced restore');Object.assign(this,stack.pop());},createRadialGradient(){return gradient;}},{get(target,key){return key in target?target[key]:(...args)=>{assert.ok(args.filter(x=>typeof x==='number').every(Number.isFinite),`${key}: nonfinite coordinate`);trace.push([key,...args]);};}});
 assert.equal(context.drawSkinFx(g,'custom-costume.png',layer,0,100,80,time,action,.5,reduced),true);
 assert.equal(stack.length,0,'Canvas save/restore must balance');assert.equal(g.globalAlpha,.7,'Aura must preserve caller opacity');return JSON.stringify(trace);
}
for(let realm=2;realm<=9;realm++)for(const layer of ['back','front'])for(const action of ['idle','run','cast','meditate']){
 render(realm,layer,action,false,2);
 assert.equal(render(realm,layer,action,true,2),render(realm,layer,action,true,200),'Reduced motion must remain static');
}
// Exercise the actual preview path for costumes without an actor atlas.
context.performance={now:()=>2000};context.matchMedia=()=>({matches:false});
const previewSource=fs.readFileSync('wwwroot/js/skill-preview.js','utf8');
vm.runInContext(previewSource.slice(0,previewSource.indexOf('const FIRE =')),context);
for(const costume of ['nguyenanh','hoathan']){
 const order=[];context.skinArt=()=>`img/hero_${costume}.png`;
 context.drawSkinFx=(_g,_hero,layer)=>order.push(layer);
 assert.equal(typeof context.drawSkinPreviewActor,'function','Preview fallback must retain realm effects');
 context.drawSkinPreviewActor({},'cast',.5,26,52,46,()=>order.push('sprite'));
 assert.deepEqual(order,['back','sprite','front']);
}
console.log('PASS: eight realm themes, outfit independence, action/layer renders, reduced motion and real preview fallbacks.');
