const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const scope=vm.createContext({skActive:()=> 'hoa'});vm.runInContext(fs.readFileSync('wwwroot/js/battle-feedback.js','utf8'),scope);
const battle={monHpLeft:[500,480],monControl:['freeze',''],casts:[{turn:0,skill:'bang_tien',secondary:'freeze'}],weakened:[true,true],windGuard:[true,true]};
const phase=s=>({name:'fight',i:0,s,trip:1});
assert.equal(scope.battleHeroHpAt({heal:[50]},phase(.94),600,650,true,true),600);
assert.equal(scope.battleHeroHpAt({heal:[50]},phase(.95),600,650,true,true),650,'end-turn heal must update HP even on held/killing turns');
assert.equal(scope.battleHeroHpAt({heal:[50]},phase(.85),600,550,false,false),500,'heal must not appear before .95');
assert.equal(scope.battleControlAt(battle,phase(.44)),'','skill control must not appear before impact');
assert.equal(scope.battleControlAt(battle,phase(.45)),'freeze');
assert.equal(scope.battleControlAt(battle,{...phase(.1),rep:true}),'freeze','extra strike happens after the skill impact');
assert.equal(scope.battleSecondaryStatus({...battle,casts:[{turn:0,secondary:'weaken'}]},phase(.4),'weaken'),false);
assert.equal(scope.battleSecondaryStatus({...battle,casts:[{turn:0,secondary:'weaken'}]},phase(.45),'weaken'),true);
assert.equal(scope.battleSecondaryStatus(battle,{...phase(.1),i:1},'weaken'),true);
assert.equal(scope.battleSecondaryStatus({...battle,windGuard:[false]},phase(.9),'wind'),false);
const events=[],seen={},all={casts:['weaken','heal','stun','paralyze','freeze','wind','burn'].map((secondary,n)=>({turn:0,skill:'skill'+n,secondary}))};
for(const s of [.1,.44,.45,.5,.8,1])scope.battleSecondaryFeedback(all,phase(s),seen,e=>events.push(e),10,20,100,80);
assert.equal(events.length,6,'one label per triggered secondary, burn uses its existing feedback');
assert(events.filter(e=>['Phong hộ +20% né','Hồi phục'].includes(e.text)).every(e=>e.x===10),'hero buffs must label hero');
assert(events.filter(e=>!['Phong hộ +20% né','Hồi phục'].includes(e.text)).every(e=>e.x===100),'debuffs must label target');
const source=fs.readFileSync('wwwroot/js/skills.js','utf8'),fn=source.match(/function skSecondaryChance\([\s\S]*?\n}/);
assert(fn,'secondary chance helper missing');vm.runInContext(fn[0],scope);
const skill={maxLv:5,secondaryEffect:{chancePerLv:10}};
for(const [lv,want] of [[0,0],[1,10],[3,30],[5,50],[999,50],[-1,0]])assert.equal(scope.skSecondaryChance(skill,lv),want);
assert.equal(scope.skSecondaryChance({maxLv:0},5),0);
console.log('PASS secondary impact timing, continued states, target/hero labels, duplicate suppression and tooltip chance boundaries.');

for(const active of ['hoa','thuy','moc','tho','loi','bang','gio'])
for(const element of ['hoa','thuy','moc','tho','loi','bang','gio']) {
 const s={element,maxLv:5,secondaryEffect:{chancePerLv:10,target:'monster'}};
 assert.equal(scope.skSecondaryChance(s,5,active),element===active?50:0);
 s.secondaryEffect.target='hero';assert.equal(scope.skSecondaryChance(s,5,active),50);
}
console.log('PASS all 49 manual/element tooltip combinations and beneficial effects.');
