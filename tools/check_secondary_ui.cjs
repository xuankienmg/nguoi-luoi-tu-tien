const {chromium}=require(process.argv[2]||'playwright'),{spawn}=require('node:child_process'),fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve('build-check/secondary-effects'),base='http://127.0.0.1:18486';
const server=spawn('dotnet',[path.join(out,'NguoiLuoiTuTien.dll'),'--web','--urls',base],{cwd:out,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Game:`Data Source=${path.join(out,'secondary-ui.db')}`},windowsHide:true,stdio:'pipe'});
let log='',browser;server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
(async()=>{try{
  let ready=false;for(let i=0;i<100;i++){try{if((await fetch(base+'/api/env')).ok){ready=true;break}}catch{}await new Promise(r=>setTimeout(r,200))}assert(ready,log.slice(-1500));
  const data=await(await fetch(base+'/api/data')).json(),active=data.skillTree.filter(s=>s.tree==='active');assert.equal(active.length,49);
  assert(active.every(s=>s.secondaryEffect?.chancePerLv===10));assert(data.skillTree.filter(s=>s.tree!=='active').every(s=>s.secondaryEffect===null));
  browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:740,height:680}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));await page.addInitScript(()=>window.requestAnimationFrame=()=>0);
  await page.goto(base+'/?mode=panel&tab=skill');await page.waitForFunction(()=>typeof S!=='undefined'&&S&&typeof DATA!=='undefined'&&DATA.skillTree?.length>0,null,{polling:100});
  await page.evaluate(()=>{sync=async()=>{};postSeq++});
  for(const element of ['hoa','thuy','moc','tho','loi','bang','gio']){
    const skill=active.find(s=>s.element===element);
    await page.evaluate(({element,id})=>{S.realm=6;S.unlockedElements=DATA.elements.map(e=>e.id);S.skills[id]=1;skEl=element;renderSkill()}, {element,id:skill.id});
    const tip=await page.evaluate(id=>skTipHtml(skById(id)),skill.id);assert(tip.includes('10% '+skill.secondaryEffect.name));assert(tip.includes(skill.secondaryEffect.description));
    const tip5=await page.evaluate(id=>{S.skills[id]=5;return skTipHtml(skById(id))},skill.id);assert(tip5.includes('50% '+skill.secondaryEffect.name));
    await page.mouse.move(5,5);
    await page.locator(`#pane-skill .sk-n[data-sk="${skill.id}"]`).hover();
    await page.waitForFunction(name=>!$('tip').hidden&&$('tip').textContent.includes('50%')&&$('tip').textContent.includes(name),skill.secondaryEffect.name,{polling:100});
    await page.screenshot({path:path.join(out,element+'-secondary-tooltip.png')});
  }
  const fixtures=JSON.parse(fs.readFileSync('build-check/secondary-fixtures.json','utf8'));assert.equal(fixtures.length,7);
  await page.setViewportSize({width:640,height:360});await page.goto(base+'/?mode=strip');await page.waitForFunction(()=>typeof S!=='undefined'&&S,null,{polling:100});
  await page.evaluate(()=>{sync=async()=>{};postSeq++});
  for(const fixture of fixtures){
    const cast=fixture.battle.casts.find(c=>c.secondary);assert(cast,'fixture has no proc '+fixture.element);
    await page.evaluate(({fixture,turn})=>{S.battle=fixture.battle;S.realm=3;S.stats.hp=fixture.battle.heroHp;S.activeManual=fixture.element;S.skills[fixture.skill]=5;S.skillSlots=[fixture.battle.basic,fixture.skill];resize();battleDone={};pfx=[];drawPixelBattle(10,.016,{name:'fight',i:turn,s:.55,trip:1,rep:false},realm())},{fixture,turn:cast.turn});
    await page.waitForFunction(()=>sprite48(skinArt('hero'))&&sprite48(S.battle.image)&&skImg(skinArt('heroCast')||HC_REF.src),null,{polling:100});
    await page.waitForFunction(id=>fireFxAvailable(id,3),fixture.skill,{polling:100});
    const result=await page.evaluate(({turn,element})=>{
      battleDone={};pfx=[];const ph={name:'fight',i:turn,s:.55,trip:1,rep:false};
      drawPixelBattle(10,.016,ph,realm());
      return {labels:pfx.filter(p=>p.type==='label').map(p=>p.text),control:battleControlAt(S.battle,ph),weak:battleSecondaryStatus(S.battle,ph,'weaken'),wind:battleSecondaryStatus(S.battle,ph,'wind')};
    },{turn:cast.turn,element:fixture.element});
    if(['tho','loi','bang'].includes(fixture.element))assert.equal(result.control,cast.secondary);
    if(fixture.element==='thuy')assert(result.weak);
    if(fixture.element==='gio')assert(result.wind);
    if(fixture.element!=='hoa')assert(result.labels.length>=1,'missing secondary label '+fixture.element);
    await page.locator('#stage').screenshot({path:path.join(out,fixture.element+'-secondary-combat.png')});
  }
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(out,'secondary-ui-results.json'),JSON.stringify({activeSkills:49,elements:7,tooltipLevels:[1,5],realBattleFixtures:7,errors},null,2));
  console.log('PASS browser: 49 API secondary definitions, seven Lv.1/Lv.5 tooltips, seven server-simulated combat proc fixtures, control/buff labels and zero JS errors.');
}finally{if(browser)await browser.close();server.kill()}})().catch(e=>{console.error(e);process.exitCode=1});
