const fs=require('fs'),vm=require('vm'),assert=require('assert');
const code=fs.readFileSync('wwwroot/js/battle.js','utf8');
const fn=code.slice(code.indexOf('function battlePhase(){'),code.indexOf('/** Đợt quái đang diễn'));
for(const extras of [false,true]){
 const hits=6,ws=[{start:0},{start:3}],extra=Array(hits).fill(extras),pet=extras?{casts:[{turn:1},{turn:4}]}:null;
 const phases=[];
 for(const speed of [0,20,60]){
  const travel=2.5*(1-speed/100),combat=hits+.8+1,cd=travel+combat;
  const ctx={S:{battle:{hits,waves:ws,extra,pet,win:true},stats:{speed},exploreCooldownMs:cd*1000,exploreReadyInMs:0},recvAt:0,EX_REP:.34,PET_CAST_SECONDS:.8,elapsed:0};
  ctx.exploreLeft=()=>cd*1000-ctx.elapsed*1000;
  vm.createContext(ctx);vm.runInContext(fn,ctx);
  ctx.elapsed=travel*.4;assert.equal(ctx.battlePhase().name,'walk');assert(Math.abs(ctx.battlePhase().k-.5)<1e-8);
  ctx.elapsed=travel+1.2;phases.push(JSON.stringify(ctx.battlePhase()));
  ctx.elapsed=cd;assert.equal(ctx.battlePhase().name,'win');
 }
 assert.equal(phases[0],phases[1]);assert.equal(phases[0],phases[2]);
}
console.log('PASS: travel scales with agility; combat timing unchanged at 0/20/60%, including pet casts and extra attacks.');
