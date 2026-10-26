const {chromium}=require(process.argv[2]||'playwright');
const {spawn}=require('node:child_process'),fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve('build-check/four-elements'),base='http://127.0.0.1:18483';
const server=spawn('dotnet',[path.join(out,'NguoiLuoiTuTien.dll'),'--web','--urls',base],{cwd:out,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Game:`Data Source=${path.join(out,'four-elements-ui.db')}`},windowsHide:true,stdio:'pipe'});
let log='';server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
let browser;
(async()=>{try{
  let started=false;
  for(let i=0;i<100;i++){try{if((await fetch(base+'/api/env')).ok){started=true;break}}catch{}await new Promise(r=>setTimeout(r,200));}
  assert(started,'server did not start: '+log.slice(-1500));
  const data=await (await fetch(base+'/api/data')).json();
  assert.equal(data.elements.filter(e=>e.open).length,7);
  const added=data.skillTree.filter(s=>['tho','loi','bang','gio'].includes(s.element));assert.equal(added.length,76);
  browser=await chromium.launch({channel:'msedge',headless:true});
  const page=await browser.newPage({viewport:{width:740,height:680}}),errors=[],missing=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400&&/\/img\/|four-elements/.test(r.url()))missing.push(r.url()+' '+r.status())});
  await page.goto(base+'/?mode=panel&tab=skill');
  await page.waitForFunction(()=>typeof S!=='undefined'&&S&&typeof DATA!=='undefined'&&DATA.skillTree?.length>0);
  for(const element of ['tho','loi','bang','gio']){
    await page.evaluate(element=>{
      S.realm=6;S.unlockedElements=DATA.elements.map(e=>e.id);S.activeManual=element;S.skillPoints=1000;
      for(const skill of DATA.skillTree.filter(s=>s.element===element&&s.category!=='Đòn thường'))S.skills[skill.id]=5;
      S.skillSlots=[DATA.skillTree.find(s=>s.element===element&&s.category==='Đòn thường').id,DATA.skillTree.find(s=>s.element===element&&s.category==='Tuyệt kỹ').id];
      skEl=element;renderSkill();
    },element);
    assert.equal(await page.locator('#pane-skill .sk-n').count(),18,element+' tree UI');
    await page.waitForFunction(()=>[...document.querySelectorAll('#pane-skill img')].every(im=>im.complete&&im.naturalWidth>0));
    await page.screenshot({path:path.join(out,element+'-tree.png')});
  }
  await page.goto(base+'/fx-preview/four-elements.html');
  await page.waitForFunction(()=>typeof FOUR_ELEMENTS_FX_DATA!=='undefined'&&FOUR_ELEMENTS_FX_DATA.length===32);
  assert.equal(await page.locator('article').count(),32);
  for(const element of ['tho','loi','bang','gio']){
    await page.selectOption('#element',element);assert.equal(await page.locator('article:not([hidden])').count(),8);
  }
  for(const skill of added.filter(s=>s.category!=='Căn cơ'&&s.category!=='Bị động')){
    assert.equal((await fetch(base+'/fx-preview/assets/'+skill.id+'.webp')).status,200,'missing WebP '+skill.id);
    await page.evaluate(id=>{selected=FOUR_ELEMENTS_FX_DATA.find(s=>s.skillId===id);playing=false;time=1100;draw()},skill.id);
    await page.waitForFunction(id=>fireFxAvailable(id,6),skill.id);
    const drawing=await page.evaluate(()=>{draw();const c=document.querySelector('#battle'),g=c.getContext('2d');return {pixels:g.getImageData(550,60,230,290).data.filter((v,i)=>i%4===3&&v>0).length,cache:fireFxCacheInfo().size}});
    assert(drawing.pixels>100,skill.id+' invisible combat effect');assert(drawing.cache<=2);
  }
  const directionCases=[['thach_dan','left'],['son_nhac','above'],['dong_dia','below'],['tho_giap','center']];
  const capture=path.join(out,'cast-directions');fs.mkdirSync(capture,{recursive:true});
  for(const [id,direction] of directionCases){
    await page.evaluate(id=>{selected=FOUR_ELEMENTS_FX_DATA.find(s=>s.skillId===id);playing=false;time=0;draw()},id);
    await page.waitForFunction(id=>fireFxAvailable(id,6),id);
    assert.equal(await page.evaluate(()=>selected.castDirection),direction);
    for(let frame=0;frame<24;frame++){
      await page.evaluate(t=>{time=t;draw()},frame*100);
      await page.locator('#battle').screenshot({path:path.join(capture,id+'-'+String(frame).padStart(2,'0')+'.png')});
    }
  }
  await page.selectOption('#element','');await page.check('#reduced');await page.selectOption('#enemySize','1.4');await page.selectOption('#background','light');
  await page.screenshot({path:path.join(out,'four-elements-preview-light.png'),fullPage:true});
  await page.selectOption('#element','loi');await page.uncheck('#reduced');await page.selectOption('#background','');
  await page.evaluate(()=>{playing=false;time=1100;draw()});await page.screenshot({path:path.join(out,'four-elements-preview.png'),fullPage:true});
  await page.setViewportSize({width:420,height:300});await page.screenshot({path:path.join(out,'four-elements-small.png')});
  assert.deepEqual(errors,[]);assert.deepEqual(missing,[]);
  fs.writeFileSync(path.join(out,'ui-results.json'),JSON.stringify({elements:4,treeNodes:76,casts:32,directions:4,capturedFrames:96,errors,missing,fixture:'Client rendering uses a local state fixture; real server actions covered by four_element_checks.'},null,2));
  console.log('PASS real API: 7 open manuals, 76 new tree nodes. Browser: 4 trees, 76 icons, 32 combat effects, 4 cast paths/96 captured frames, filters, light/reduced motion/small viewport, no JS errors or missing images.');
}finally{if(browser)await browser.close();server.kill()}})().catch(e=>{console.error(e);process.exitCode=1});
