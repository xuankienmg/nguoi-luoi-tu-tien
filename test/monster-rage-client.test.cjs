const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const src=fs.readFileSync('wwwroot/js/battle-feedback.js','utf8');
const start=src.indexOf('// Monster rage playback');
const context=vm.createContext({});
vm.runInContext(start<0?'':src.slice(start),context);
test('monster rage switches at counter impact and never reads another wave',()=>{
  const battle={monsterRage:[{before:75,after:100,ultimate:false,skillId:'beast'},{before:100,after:0,ultimate:true,skillId:'beast'},{before:0,after:25,ultimate:false,skillId:'magic'}]};
  context.b=battle;
  assert.equal(vm.runInContext('battleMonsterRageAt(b,{name:"fight",i:0,s:.84},0)',context),.75);
  assert.equal(vm.runInContext('battleMonsterRageAt(b,{name:"fight",i:0,s:.85},0)',context),1);
  assert.equal(vm.runInContext('battleMonsterRageAt(b,{name:"fight",i:1,s:.85},0)',context),0);
  assert.equal(vm.runInContext('battleMonsterRageAt(b,{name:"enter",i:2},2)',context),0);
});
test('legacy, PvP, ordinary enemies and repeated hero hits cannot trigger monster ultimates',()=>{
  context.b={monsterRage:[{before:100,after:0,ultimate:true,skillId:'weapon'}]};
  assert.equal(vm.runInContext('battleMonsterUltimate(b,{name:"fight",i:0,rep:true})',context),null);
  assert.equal(vm.runInContext('battleMonsterUltimate({...b,pvp:{}},{name:"fight",i:0})',context),null);
  assert.equal(vm.runInContext('battleMonsterRageAt({}, {name:"fight",i:0,s:1})',context),null);
  assert.equal(vm.runInContext('battleMonsterRageAt({monsterRage:[{before:-1,after:-1}]}, {name:"fight",i:0,s:1})',context),null);
});
test('charge and ultimate labels emit once per trip and turn',()=>{
  const events=[],seen={};context.seen=seen;context.emit=e=>events.push(e);
  context.b={monsterRage:[{before:75,after:100,ultimate:false,skillId:'beast'},{before:100,after:0,ultimate:true,skillId:'beast'}]};
  vm.runInContext('for(let n=0;n<3;n++)battleMonsterRageFeedback(b,{name:"fight",trip:1,i:0,s:.9},seen,emit,10,10,false)',context);
  assert.equal(events.length,1);assert.equal(events[0].text,'◆ Tụ lực');
  vm.runInContext('for(let n=0;n<3;n++)battleMonsterRageFeedback(b,{name:"fight",trip:1,i:1,s:.8},seen,emit,10,10,false)',context);
  assert.equal(events.length,2);assert.match(events[1].text,/Cuồng Thú/);
  vm.runInContext('battleMonsterRageFeedback(b,{name:"fight",trip:2,i:1,s:.8},seen,emit,10,10,true)',context);
  assert.equal(events.length,2);
});
test('extra attacks and pet casts retain the normal turn speed instead of compressing it',()=>{
  const source=fs.readFileSync('wwwroot/js/battle.js','utf8');
  const fn=source.slice(source.indexOf('/** Nhịp chung'),source.indexOf('/** Đợt quái'));
  const c=vm.createContext({S:{battle:{hits:10,extra:Array(10).fill(true),pet:{casts:Array.from({length:10},(_,turn)=>({turn,at:.45}))},win:true},stats:{speed:0},exploreCooldownMs:24900,exploreReadyInMs:24900},recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,exploreLeft:()=>22150});
  vm.runInContext(fn,c);
  const original=vm.runInContext('battlePhase()',c);
  assert.equal(original.name,'fight');assert.equal(original.i,0);
  assert.ok(Math.abs(original.s-.25)<.00001,JSON.stringify(original));
});

test('movement speed shortens travel and wave entrance without accelerating attacks',()=>{
  const source=fs.readFileSync('wwwroot/js/battle.js','utf8');
  const fn=source.slice(source.indexOf('/** Nhịp chung'),source.indexOf('/** Đợt quái'));
  const cooldown=2.5/2+6+2*.8/2+1;
  const c=vm.createContext({S:{battle:{hits:6,waves:[{start:0},{start:2},{start:4}],win:true},stats:{dodge:90,accuracy:50,moveSpeed:100},exploreCooldownMs:cooldown*1000,exploreReadyInMs:0},recvAt:0,PET_CAST_SECONDS:.8,EX_REP:.34,elapsed:1.5});
  c.exploreLeft=()=> (cooldown-c.elapsed)*1000;
  vm.runInContext(fn,c);
  let phase=vm.runInContext('battlePhase()',c);
  assert.equal(phase.name,'fight');assert.ok(Math.abs(phase.s-.25)<1e-8);
  c.elapsed=1.25+2+.2;
  phase=vm.runInContext('battlePhase()',c);
  assert.equal(phase.name,'enter');assert.equal(phase.wave,1);assert.ok(Math.abs(phase.k-.5)<1e-8);
});
test('all ultimate families draw bounded geometry and suppress contact burst on dodge',()=>{
  const source=fs.readFileSync('wwwroot/js/battle-fx.js','utf8');
  const fn=source.slice(source.indexOf('function monsterUltimateAttack('),source.indexOf('// ---- Hỏa hệ'));
  const arcs=[];const ctx={save(){},restore(){},beginPath(){},ellipse(){},moveTo(){},lineTo(){},stroke(){},fill(){},arc(...args){arcs.push(args)}};
  context.ctx=ctx;context.MODE='full';context.HIT=(.85-.78)/.22;
  vm.runInContext(fn,context);
  for(const skillId of ['beast','weapon','magic']){
    context.turn={ultimate:true,skillId};arcs.length=0;
    vm.runInContext('monsterCombatFx(turn,.4,{ctx,mx:150,my:50,mw:60,cx:40,cy:50,avoided:true,reduced:true,compact:true})',context);
    assert.ok(arcs.length<=1);
    for(const args of arcs)assert.ok(args.every(Number.isFinite));
  }
});

test('boss atlases use the first server impact and keep later warnings visual-only',()=>{
  const source=fs.readFileSync('wwwroot/js/battle-fx.js','utf8');
  const fn=source.slice(source.indexOf('const monsterSpriteId'),source.indexOf('function pline('));
  const c=vm.createContext({MONSTER_COUNTER_IMPACT:.85, fireFxImage:()=>({state:'ready',image:{}})});
  vm.runInContext(fn,c);
  assert.equal(vm.runInContext('bossCounterIdFor("img/hell2/boss_hoang_phong_dai_vuong.png")',c),'tam-trao');
  assert.equal(vm.runInContext('bossCounterIdFor("img/boss_hac_giao.png")',c),'duoi-rong-quat');
  assert.equal(vm.runInContext('bossCounterIdFor("img/quai_yeu_lang.png")',c),null);
  assert.equal(vm.runInContext('monsterCounterFollowThrough(0,.42)',c),(.85-.78)/.22);
  assert.equal(vm.runInContext('monsterCounterFollowThrough(.42,.42)',c),null);
  // dựng đòn sớm: k tăng đơn điệu 0 → HIT từ điểm bắt đầu tới lúc trúng, rồi khớp nhịp cũ
  const HITK=(.85-.78)/.22;
  for(const boss of [false,true]){
    const start=vm.runInContext(`monsterCounterLeadStart(${boss})`,c);
    assert.ok(start<.78);
    assert.equal(vm.runInContext(`monsterCounterVisualK(${start-.1},${boss})`,c),0);
    assert.equal(vm.runInContext(`monsterCounterVisualK(.85,${boss})`,c),HITK);
    assert.ok(Math.abs(vm.runInContext(`monsterCounterVisualK(1,${boss})`,c)-1)<1e-9);
    let prev=-1;
    for(let s=start;s<=1;s+=.01){const k=vm.runInContext(`monsterCounterVisualK(${s},${boss})`,c);assert.ok(k>=prev&&k<=1);prev=k;}
  }
  assert.ok(vm.runInContext('MONSTER_COUNTER_CONTACT_SECONDS',c)>.23&&vm.runInContext('BOSS_COUNTER_CONTACT_SECONDS',c)>.42);
  const result=vm.runInContext(`(()=>{
    const out={};
    for(const id of Object.keys(BOSS_COUNTER_SHEET)){
      const {spec}=bossCounterSheet(id);
      out[id]={
        windup:bossCounterFrames(spec,HIT-.0001,false,false),
        impact:bossCounterFrames(spec,HIT,false,false),
        dodge:bossCounterFrames(spec,HIT,true,false),
        still:bossCounterFrames(spec,HIT,false,true),
        after:bossCounterFrames(spec,1,false,false)
      };
    }
    return JSON.stringify(out);
  })()`,c);
  for(const [id,impact,still] of [['tam-trao',30,32],['xuyen-giap',45,45],['dia-chan',33,37],['duoi-rong-quat',44,47]]){
    const frames=JSON.parse(result)[id];
    const multi=id==='tam-trao'||id==='dia-chan';
    assert.deepEqual(frames.windup,{warning:impact-1,attack:impact-1});
    assert.deepEqual(frames.impact,{warning:multi?impact:null,attack:impact});
    assert.deepEqual(frames.dodge,{warning:null,attack:null});
    assert.deepEqual(frames.still,{warning:null,attack:still});
    assert.deepEqual(frames.after,{warning:null,attack:89});
  }
});
