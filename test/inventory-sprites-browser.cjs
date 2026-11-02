const {chromium}=require(process.argv[2]||'playwright');
const {spawn}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve('build-check/inventory-sprites'),base='http://127.0.0.1:18497';
fs.mkdirSync(out,{recursive:true});
const server=spawn('dotnet',[path.resolve('bin/Debug/net8.0-windows/NguoiLuoiTuTien.dll'),'--web','--urls',base],{
  windowsHide:true,stdio:'pipe',env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',Online__Mode:'Offline',Leaderboard__Url:'',Leaderboard__AnonKey:'',ConnectionStrings__Game:`Data Source=${path.join(out,'preview.db')}`}
});
let log='',browser;server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
(async()=>{try{
  let ready=false;
  for(let i=0;i<100;i++){try{if((await fetch(base+'/api/env')).ok){ready=true;break}}catch{}await new Promise(r=>setTimeout(r,200));}
  assert(ready,log.slice(-1000));
  browser=await chromium.launch({channel:'msedge',headless:true});
  const page=await browser.newPage({viewport:{width:1120,height:850}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/api/version*',route=>route.fulfill({json:{blocked:false,current:'test'}}));
  await page.goto(base+'/?mode=panel&tab=inv');
  await page.waitForFunction(()=>typeof S!=='undefined'&&S&&typeof drawInventorySprite==='function');
  await page.evaluate(()=>{
    sync=async()=>{};setState=()=>{};S.starterChoice={required:false};renderStarterChoice();
    S.skinsUnlocked=DATA.skins.filter(s=>s.gender==='female').map(s=>s.id);
    S.gender='female';openTab('inv');
  });
  const ids=['luyen_khi_nu','truc_co_nu','kim_dan_nu','nguyen_anh_nu','hoa_than_nu','luyen_hu_nu','hop_the_nu'];
  for(const id of ids){
    await page.evaluate(id=>{S.skin=id;S.realm=DATA.skins.find(s=>s.id===id).minRealm;renderInv();},id);
    await page.waitForFunction(()=>{drawInventoryAvatar(0);return document.querySelector('#ivAvatar').style.visibility==='visible'&&document.querySelector('#ivAvatar').previousElementSibling.hidden;});
    const result=await page.evaluate(()=>{
      const c=document.querySelector('#ivAvatar'),g=c.getContext('2d');
      const hash=()=>g.getImageData(0,0,320,320).data.reduce((n,v,i)=>(n+v*(i%17+1))%1000000007,0);
      drawInventoryAvatar(0);const first=hash();drawInventoryAvatar(1);const next=hash();
      return {first,next,hero:skinArt('hero'),sheet:!!INVENTORY_SPRITE_SHEETS[skinArt('hero')]};
    });
    assert(result.sheet,id+' must use the new sheet: '+JSON.stringify(result));assert.notEqual(result.first,result.next,id+' must animate');
    await page.screenshot({path:path.join(out,id+'.png')});
  }
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.reload();
  await page.waitForFunction(()=>typeof S!=='undefined'&&S);
  await page.evaluate(()=>{
    sync=async()=>{};setState=()=>{};S.starterChoice={required:false};renderStarterChoice();S.gender='female';S.realm=4;S.skin='hoa_than_nu';S.skinsUnlocked=['hoa_than_nu'];renderInv();
  });
  await page.waitForFunction(()=>{drawInventoryAvatar(0);return document.querySelector('#ivAvatar').style.visibility==='visible';});
  const reducedResult=await page.evaluate(()=>{
    const g=document.querySelector('#ivAvatar').getContext('2d');
    const original=g.drawImage,draws=[];
    g.drawImage=function(...args){draws.push(args.slice(1));return original.apply(this,args);};
    drawInventoryAvatar(0);const first=Array.from(g.getImageData(0,0,320,320).data);
    const firstDraws=JSON.stringify(draws);draws.length=0;
    drawInventoryAvatar(1);const next=g.getImageData(0,0,320,320).data;
    const sameDraws=firstDraws===JSON.stringify(draws);g.drawImage=original;
    return {still:sameDraws,reduced:reduce,hero:skinArt('hero'),meanError:first.reduce((sum,v,i)=>sum+Math.abs(v-next[i]),0)/first.length};
  });
  assert(reducedResult.still,'reduced motion must remain still: '+JSON.stringify(reducedResult));
  assert.deepEqual(errors,[]);
  console.log('PASS real inventory: 7 female costumes animate, static image hides, skin switching works, reduced motion freezes, no JS errors.');
}finally{if(browser)await browser.close();server.kill();}})().catch(e=>{console.error(e);process.exitCode=1;});
