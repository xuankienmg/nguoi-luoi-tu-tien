const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){const s=vm.createContext({Image:class{}});vm.runInContext(fs.readFileSync('wwwroot/js/fire-fx.js','utf8'),s);return s;}
test('Xich Diem replaces only its own effect and reaches all 60 frames',()=>{
 const s=load(),spec=s.fireFxSpec('xich_diem_thien',0);
 assert.equal(spec.frameCount,60);assert.equal(spec.durationMs,60/30*1000);
 const seen=new Set();for(let i=0;i<10000;i++)seen.add(s.fireFxPlan('xich_diem_thien',i/10000*.75).frame);
 assert.equal(seen.size,60);
 assert.equal(s.fireFxPlan('xich_diem_thien',.45).frame,42);
 assert.equal(s.fireFxPlan('xich_diem_thien',.749999).frame,59);
 assert.equal(s.fireFxPlan('xich_diem_thien',.75),null);
 assert.equal(s.fireFxSpec('not-a-skill'),null);
});
test('long cast keeps 30 FPS timing before pets, combo and monster counter',()=>{
 const s=load(),b={casts:[{turn:0,skill:'xich_diem_thien',dmg:10}],extra:[true]};
 const timing=s.battleSkillTiming(b,0),end=3;   // mọi tuyệt kỹ 3s; sheet 60 khung kéo giãn đều, trúng ở 2.1s
 assert(Math.abs(timing.length-(end+.25+.34))<1e-8);
 assert(Math.abs(timing.progress(2.1).s-.45)<1e-8);
 assert(Math.abs(timing.progress(end-.001).s-(.75-.001/.9*.3))<1e-8);
 assert.equal(timing.progress(end+.1).rep,true);
 assert(timing.heroEnd>=end+.34);
 assert.equal(s.battleSkillTiming(b,1).length,1);
});
test('missed ultimate still plays its animation and reserves full duration',()=>{
 const s=load();
 const missed=s.battleSkillTiming({casts:[{turn:0,skill:'xich_diem_thien',dmg:0}]},0);
 assert(Math.abs(missed.length-(3+.25))<1e-8);
});
test('exploration waits for the last explosion frame before the pet turn',()=>{
 const s=load(),source=fs.readFileSync('wwwroot/js/battle.js','utf8');
 const fn=source.slice(source.indexOf('/** Nhịp chung'),source.indexOf('/** Đợt quái'));
 const duration=3,cd=2.5+duration+.25+.8+1;
 Object.assign(s,{S:{battle:{hits:1,casts:[{turn:0,skill:'xich_diem_thien',dmg:10}],pet:{casts:[{turn:0,at:.45}]}},stats:{},exploreCooldownMs:cd*1000,exploreReadyInMs:cd*1000},recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,elapsed:0});
 vm.runInContext('function exploreLeft(){return S.exploreReadyInMs-elapsed*1000;}'+fn,s);
 s.elapsed=2.5+2.1;assert(Math.abs(s.battlePhase().s-.45)<1e-8);
 s.elapsed=2.5+duration-.001;assert.equal(s.battlePhase().name,'fight');assert(s.battlePhase().s<.75);
 s.elapsed=2.5+duration+.01;assert.equal(s.battlePhase().name,'pet');
 s.elapsed=2.5+duration+.8+.1;assert.equal(s.battlePhase().name,'fight');assert(s.battlePhase().s>.75);
});
