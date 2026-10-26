const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict');
const scope=vm.createContext({HEAL_COL:'#99d4a3',Image:class{set src(x){this.complete=false;}}});
vm.runInContext(fs.readFileSync('wwwroot/js/pet-battle.js','utf8'),scope);
let depth=0;const calls=[];
const g=new Proxy({canvas:{width:420,height:300},globalAlpha:1,save(){depth++},restore(){depth--}},{get(o,k){return k in o?o[k]:(...args)=>calls.push([k,...args])}});
const layout={sourceX:40,sourceY:150,heroX:100,heroY:150,targetX:310,targetY:170,height:65};
for(const kind of ['fox','frost','deer','turtle','eagle','leech','crane','cat','phoenix','xuanwu','dragon','tiger','butterfly','ancient_thunder','butterfly_rebirth','ancient_thunder_miracle'])for(const reduced of [false,true]){
 const miracle=kind==='butterfly_rebirth'||kind==='ancient_thunder_miracle';
 const cast={turn:0,kind,isMiracle:miracle,at:miracle?.9:.45,dmg:kind==='ancient_thunder_miracle'?250:0,heal:kind==='butterfly_rebirth'?250:0};
 for(const s of miracle?[.75,.9,.98]:[.25,.49,.7])scope.drawPetAttackEffects(g,{casts:[cast]},{name:'fight',i:0,s},{...layout,reduced});
 assert.equal(depth,0,kind);
}
assert.ok(!calls.some(c=>c.slice(1).some(x=>typeof x==='number'&&!Number.isFinite(x))));
const cast={turn:0,kind:'butterfly_rebirth',skill:'Thất Thải Niết Bàn',heal:250,isMiracle:true,at:.9};const seen={},events=[];
scope.petBattleFeedback({pet:{casts:[cast]}},{name:'fight',i:0,s:.86,trip:1},seen,e=>events.push(e),100,100,310,170);
assert.equal(events.length,0,'No resurrection label before enemy impact');
for(let i=0;i<4;i++)scope.petBattleFeedback({pet:{casts:[cast]}},{name:'fight',i:0,s:.95,trip:1},seen,e=>events.push(e),100,100,310,170);
assert.equal(events.filter(e=>e.text==='+250').length,1);assert.equal(events.filter(e=>e.text==='Thần thông · Thất Thải Niết Bàn').length,1);
const hits=scope.petCastHitSchedule({dmg:250,hits:[250],isMiracle:true,at:.9});assert.equal(hits[0].at,.9);
const timelineBattle={pet:{casts:[{turn:0,kind:'dragon',dmg:10},{turn:0,kind:'ancient_thunder_miracle',isMiracle:true,at:.45,dmg:100},cast]}};
let scene=scope.petSceneForPhase(timelineBattle,{name:'pet',i:0,s:.95,trip:2});
assert.equal(scene.battle.pet.casts.length,2);assert.ok(scene.battle.pet.casts.every(c=>c!==cast),'Post-hit miracle absent from pre-counter pet phase');
const timelineLabels=[];scope.petBattleFeedback(scene.battle,scene.phase,{},e=>timelineLabels.push(e),100,100,310,170);
assert.ok(!timelineLabels.some(e=>e.text.includes('Niết Bàn')));
scene=scope.petSceneForPhase(timelineBattle,{name:'fight',i:0,s:.86,trip:2});assert.equal(scene.battle.pet.casts.length,1);
const lateLabels=[];scope.petBattleFeedback(scene.battle,scene.phase,{},e=>lateLabels.push(e),100,100,310,170);assert.equal(lateLabels.length,0);
scene=scope.petSceneForPhase(timelineBattle,{name:'fight',i:0,s:.95,trip:2});scope.petBattleFeedback(scene.battle,scene.phase,{},e=>lateLabels.push(e),100,100,310,170);
assert.equal(lateLabels.filter(e=>e.text.includes('Niết Bàn')).length,1,'Revival appears only after enemy impact');
console.log('PASS 14 normal pet skills +2 miracles, safe canvas/reduced motion, post-impact timing, exact heal and no repeated labels.');
