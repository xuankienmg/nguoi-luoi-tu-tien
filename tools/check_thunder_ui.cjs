const {chromium}=require(process.argv[2]||'playwright'),{spawn}=require('node:child_process'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve('bin/thunder-check'),base='http://127.0.0.1:18479';
const server=spawn('dotnet',[path.join(out,'NguoiLuoiTuTien.dll'),'--web','--urls',base],{cwd:out,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Game:`Data Source=${path.join(out,'ui-fixture-v2.db')}`},windowsHide:true,stdio:'pipe'});
server.stdout.resume();server.stderr.resume();let browser;
(async()=>{try{
 for(let i=0;i<100;i++){try{if((await fetch(base+'/api/env')).ok)break;}catch{}await new Promise(r=>setTimeout(r,150));}
 browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:740,height:680}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(base+'/?mode=panel&tab=pet');await page.waitForFunction(()=>typeof S!=='undefined'&&S?.pet?.owned?.some(p=>p.defId==='thai_co_loi_long'));
 await page.waitForFunction(()=>document.querySelector('.pet-portrait img')?.naturalWidth>0);
 assert.ok((await page.locator('#pane-pet').innerText()).includes('Thái Cổ Lôi Long'));
 assert.equal(await page.evaluate(()=>DATA.petDefs.length),14);
 assert.ok(await page.evaluate(()=>S.pet.owned.find(p=>p.defId==='thai_co_loi_long').skillText.includes('50%')&&S.pet.owned.find(p=>p.defId==='thai_co_loi_long').skillText.includes('1 lượt')));
 await page.screenshot({path:path.join(out,'thunder-panel.png')});
 for(let f=0;f<3;f++)assert.equal((await fetch(base+`/img/pet/thai_co_loi_long_${f}.png`)).status,200);
 await page.setViewportSize({width:420,height:300});await page.goto(base+'/?mode=strip');await page.waitForFunction(()=>typeof S!=='undefined'&&S?.battle?.pet?.name==='Thái Cổ Lôi Long');await page.waitForTimeout(600);
 await page.screenshot({path:path.join(out,'thunder-battle.png')});assert.deepEqual(errors,[]);
 console.log('PASS real UI: 14 species, dragon portrait, 3 forms, approved skill text, deployed 420px combat and no JS errors.');
}finally{if(browser)await browser.close();server.kill();}})().catch(e=>{console.error(e);process.exitCode=1;});
