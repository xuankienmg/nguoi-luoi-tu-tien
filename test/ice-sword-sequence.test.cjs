const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
function load(){const s=vm.createContext({Image:class{}});vm.runInContext(fs.readFileSync('wwwroot/js/fire-fx.js','utf8'),s);return s;}
// Lượt tuyệt kỹ D giây; sheet dựng 3s phát nhanh 3/D lần (K = D/3 đổi giây trên sheet → giây trong trận).
const D=vm.runInContext('ULTIMATE_CAST_SECONDS',load()),K=D/3,IMPACT=1.6*K;
test('ice ultimate plays all 90 frames, impact at the authored 1.6s mark, without changing other ice skills',()=>{
 const s=load(),id='hu_khong_bang_nguc',spec=s.fireFxSpec(id,5);
 assert.equal(spec.frameCount,90);assert.equal(spec.durationMs,3000);
 const timing=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}]},0),seen=new Set();
 for(let i=0;i<90;i++)seen.add(s.fireFxPlan(id,timing.progress((i+.5)/30*K).s,5).frame);
 assert.equal(seen.size,90);assert(Math.abs(timing.progress(IMPACT).s-.45)<1e-9);
 assert.equal(s.fireFxPlan(id,.45,5).frame,48);
 assert.equal(s.fireFxPlan(id,timing.progress(D-.001).s,5).frame,89);
 assert.equal(s.fireFxSpec('huyen_bang',4).frameCount,36);
 assert.equal(timing.length,D+.25);
 for(const realm of [6,7,8,9]){
   const retained=s.fireFxSpec(id,realm);
   assert.equal(retained.src,spec.src,'learned ice ultimate retains its new atlas in higher realms');
   assert.equal(retained.durationMs,3000);
   assert.equal(s.fireFxPlan(id,timing.progress(IMPACT).s,realm).frame,48);
 }
});
test('miss reserves full ice animation; combo and counter follow its last frame',()=>{
 const s=load(),id='hu_khong_bang_nguc';
 const timing=s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:0}],extra:[true]},0);
 assert(Math.abs(timing.length-(D+.25+.34))<1e-9);assert.equal(timing.progress(D+.1).rep,true);
 assert(timing.progress(D-.001).s<.75);assert.equal(s.battleSkillTiming({casts:[]},0).length,1);
 assert.equal(s.battleSkillTiming({casts:[{turn:0,skill:id,dmg:5}],pvp:{idle:[true]}},0).length,1);
});
test('ice atlas preserves rectangular frame proportions and uses a fixed reduced-motion frame',()=>{
 const s=load(),id='hu_khong_bang_nguc',spec=s.fireFxSpec(id,5);let draw;
 s.fireFxImage=()=>({state:'ready',image:{}});
 const ctx={canvas:{width:480,height:240},globalAlpha:1,save(){},restore(){},drawImage(...args){draw=args;}};
 const layout={handX:40,handY:110,targetX:330,targetY:125,groundY:180,heroHeight:80,targetWidth:48,targetHeight:80,reducedMotion:true};
 assert(s.drawFireFx(ctx,s.fireFxPlan(id,.2,5),layout));
 assert(Math.abs(draw[7]/draw[8]-480/330)<1e-9);
 const first=draw.slice(1,3);s.drawFireFx(ctx,s.fireFxPlan(id,.6,5),layout);
 assert.deepEqual(draw.slice(1,3),first);
 assert.equal(spec.anchorY/spec.frameHeight,270/330);
});
test('short server cooldown cannot compress an authored ice animation before the monster response',()=>{
 const s=load(),source=fs.readFileSync('wwwroot/js/battle.js','utf8');
 const fn=source.slice(source.indexOf('/** Nhịp chung'),source.indexOf('/** Đợt quái'));
 const cd=D+.25+1;   // ngắn hơn đi bộ + lượt: buộc co nhịp chuyến
 Object.assign(s,{S:{battle:{hits:1,casts:[{turn:0,skill:'hu_khong_bang_nguc',dmg:10}]},stats:{},exploreCooldownMs:cd*1000,exploreReadyInMs:cd*1000},recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,elapsed:0});
 vm.runInContext('function exploreLeft(){return S.exploreReadyInMs-elapsed*1000;}'+fn,s);
 const start=s.battleCastStart(s.S);
 s.elapsed=start+D-.01;let ph=s.battlePhase();assert.equal(ph.name,'fight');assert(ph.s<.75,'authored animation must not be squeezed into a shorter server cooldown');
 assert.equal(s.battleMonsterActionReady(s.S.battle,ph),false);
 s.elapsed=start+D+.1;ph=s.battlePhase();assert.equal(ph.name,'fight');assert.equal(s.battleMonsterActionReady(s.S.battle,ph),true);
});
test('runtime ice sheet has a bounded decode size instead of the full authoring atlas',()=>{
 const s=load(),spec=s.fireFxSpec('hu_khong_bang_nguc',5);
 const png=fs.readFileSync('wwwroot/'+spec.src),width=png.readUInt32BE(16),height=png.readUInt32BE(20);
 assert(width*height*4<=16*1024*1024,'runtime atlas must decode to at most 16 MiB');
 assert.equal(width,spec.columns*spec.frameWidth);assert.equal(height,9*spec.frameHeight);
});
test('monster action gate suppresses charged telegraphs while authored skill is playing',()=>{
 const s=load(),b={casts:[{turn:0,skill:'hu_khong_bang_nguc',dmg:10}]};
 for(const seconds of [.8*K,IMPACT,2.8*K,D-.001]){
  const progress=s.battleSkillTiming(b,0).progress(seconds);
  assert.equal(s.battleMonsterActionReady(b,{name:'fight',i:0,...progress}),false);
 }
 assert.equal(s.battleMonsterActionReady(b,{name:'fight',i:0,s:.8,rep:false}),true);
 assert.equal(s.battleMonsterActionReady({casts:[]},{name:'fight',i:0,s:.5}),true,'ordinary monster telegraphs keep their existing timing');
});
test('a faster server update cannot replace the on-screen cast; rewards remain live and no battle queue accumulates',()=>{
 const s=load(),source=fs.readFileSync('wwwroot/js/battle.js','utf8');
 const fn=source.slice(source.indexOf('// One visual snapshot'),source.indexOf('/** Đợt quái'));
 Object.assign(s,{S:{id:7,realm:5,map:{area:1,diff:0},battle:{image:'old',hits:1,casts:[{turn:0,skill:'hu_khong_bang_nguc',dmg:10}]},stats:{},exploreCooldownMs:1350,exploreReadyInMs:1350,gold:10},recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,exploreLeft:()=>1350});
 vm.runInContext(fn,s);
 const start=2.5*1.35/(2.5+D+.25+1);   // đi bộ co theo cooldown 1,35s
 assert.equal(s.battleVisualFrame(0).battle.image,'old');
 s.S={...s.S,map:{area:2,diff:3},gold:50,battle:{image:'latest',hits:1,casts:[]},exploreReadyInMs:1000};s.recvAt=1500;
 let visual=s.battleVisualFrame((start+D-.001)*1000);assert.equal(visual.battle.image,'old');assert(visual.ph.s<.75);assert.equal(s.S.gold,50);
 assert.equal(visual.state.map.area,1);assert.equal(visual.state.map.diff,0);assert.equal(s.S.map.area,2);
 assert.equal(s.battleMonsterActionReady(visual.battle,visual.ph),false);
 visual=s.battleVisualFrame((start+D+.04)*1000);assert.equal(visual.battle.image,'old');assert.equal(s.battleMonsterActionReady(visual.battle,visual.ph),true);
 assert.equal(s.battleVisualFrame((start+D+.25+1+.001)*1000).battle.image,'latest','finish then catch up directly to latest, without replaying a backlog');
 s.S={...s.S,id:8,battle:{image:'other-character',hits:1,casts:[]}};
 assert.equal(s.battleVisualFrame(4300).battle.image,'other-character');
});
// cd 4.25s: đi bộ co còn 2.5*4.25/(2.5+D+.25+1), lượt băng bắt đầu ngay sau đó.
function playbackCtx(left){
 const s=load(),source=fs.readFileSync('wwwroot/js/battle.js','utf8');
 const fn=source.slice(source.indexOf('// One visual snapshot'),source.indexOf('/** Đợt quái'));
 Object.assign(s,{S:{id:7,realm:5,map:{area:1},battle:{image:'ice',win:true,hits:1,casts:[{turn:0,skill:'hu_khong_bang_nguc',dmg:10}]},stats:{},exploreCooldownMs:4250,exploreReadyInMs:left},
  recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,left});
 vm.runInContext('function exploreLeft(){return left;}'+fn,s);
 return s;
}
const ICE_CAST_AT=2.5*4.25/(2.5+D+.25+1);
test('fresh battle starts in sync with the server clock (walk first)',()=>{
 const s=playbackCtx(4250);
 assert.equal(s.battleVisualFrame(10000).ph.name,'walk');
 assert(Math.abs(s.battleCastStart(s.S)-ICE_CAST_AT)<1e-9);
});
test('joining mid-trip anchors to the server clock but never skips past the cast start',()=>{
 let s=playbackCtx(4250-1000);                                   // server đã đi 1s: chưa tới chiêu → đúng nhịp server
 let ph=s.battleVisualFrame(10000).ph;assert.equal(ph.name,'walk');
 ph=s.battleVisualFrame(10000+(ICE_CAST_AT-1+.01)*1000).ph;assert.equal(ph.name,'fight');assert(ph.s<.01);
 s=playbackCtx(200);                                             // gần hết chuyến: nhảy tới đầu chiêu, không bỏ chiêu
 ph=s.battleVisualFrame(10000).ph;assert.equal(ph.name,'fight');assert(ph.s<1e-9);
});
test('a re-sent state for the same battle (shifted receipt, same trip end) does not replay the cast',()=>{
 const s=playbackCtx(4250);
 s.battleVisualFrame(0);
 s.S={...s.S,battle:{...s.S.battle},exploreReadyInMs:3000};s.recvAt=1250;   // đồng bộ lại: cùng mốc kết thúc chuyến
 const end=s.battleVisualFrame(7000).ph;assert.equal(end.name,'win');assert(end.k>=1);
 assert.equal(s.battleVisualFrame(7100).ph.name,'win','same battle stays finished instead of restarting');
});
test('tab becoming visible again re-anchors instead of fast-forwarding hidden time',()=>{
 const s=playbackCtx(4250);
 s.battleVisualFrame(0);
 s.left=4250-1000;s.battleVisualReset();
 assert.equal(s.battleVisualFrame(60000).ph.name,'walk','re-anchored to server time, not 60s of hidden time');
});
test('switching area/map/difficulty drops the on-screen cast immediately; other actions keep it',async()=>{
 const s=load(),source=fs.readFileSync('wwwroot/js/battle.js','utf8');
 vm.runInContext(source.slice(source.indexOf('// One visual snapshot'),source.indexOf('/** Đợt quái')),s);
 const core=fs.readFileSync('wwwroot/js/core.js','utf8'),i=core.indexOf('async function act(');
 vm.runInContext('let busy=false;'+core.slice(i,core.indexOf('async function channel',i)),s);
 const cast={turn:0,skill:'hu_khong_bang_nguc',dmg:10};
 Object.assign(s,{S:{id:7,realm:5,map:{area:1},battle:{image:'old',hits:1,casts:[cast]},stats:{},exploreCooldownMs:4250,exploreReadyInMs:4250,gold:0},
  recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,exploreLeft:()=>4250,notifyHost(){},live(){},localLog(){},showCraft(){},
  setState(st){s.S=st;}});
 const reply=image=>async()=>({state:{...s.S,battle:{image,hits:1,casts:[cast]}}});
 assert.equal(s.battleVisualFrame(1000).battle.image,'old');
 s.api=reply('server-update');assert.equal(await s.act('sell/1'),true);
 assert.equal(s.battleVisualFrame(1100).battle.image,'old','other actions keep the cast on screen until it ends');
 for(const [path,image] of [['area/2','new-area'],['map/3/area/1','new-map'],['diff/1','new-diff']]){
   s.api=reply(image);assert.equal(await s.act(path),true);
   assert.equal(s.battleVisualFrame(1200).battle.image,image,path+' must show the new battle right away');
 }
});
