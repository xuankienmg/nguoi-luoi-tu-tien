const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
const src=fs.readFileSync('wwwroot/js/battle.js','utf8');
const fn=src.slice(src.indexOf('function battlePhase(){'),src.indexOf('/** Đợt quái đang diễn'));
let checks=0;
for(const hits of [1,6,20])for(const waves of [[],[{start:0},{start:2},{start:4}]])for(const special of [false,true]){
 if(waves.length&&hits<6)continue;
 const raw=2.5+hits+Math.max(0,waves.length-1)*.8+1;
 function phase(speed,t){const factor=1-Math.min(60,Math.max(0,speed))/100,cd=Math.max(3,raw*factor);const ctx={S:{stats:{speed},battle:{hits,waves,win:true,extra:special?Array(hits).fill(true):[],pet:{casts:special?[{turn:0,at:0}]:[]}},exploreCooldownMs:cd*1000,exploreReadyInMs:0},recvAt:0,exploreLeft:()=>cd*1000*(1-t),EX_REP:.34,PET_CAST_SECONDS:.8};vm.runInNewContext(fn,ctx);return ctx.battlePhase();}
 for(const t of [.05,.2,.35,.5,.7,.9,.999])for(const speed of [0,30,60,100]){const a=phase(0,t),b=phase(speed,t);assert.equal(b.name,a.name);assert.equal(b.i,a.i);assert.equal(b.wave,a.wave);if(a.s!==undefined)assert.ok(Math.abs(a.s-b.s)<1e-8);checks++;}
 assert.equal(phase(60,1).name,'win');
}
console.log(`PASS: client phases stay synchronized for agility, pet casts, extra hits, wave gaps and minimum duration (${checks} cases).`);
