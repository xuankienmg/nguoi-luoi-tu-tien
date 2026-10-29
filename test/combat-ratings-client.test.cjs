const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const inventory=fs.readFileSync('wwwroot/js/inventory.js','utf8');
const map=fs.readFileSync('wwwroot/js/map.js','utf8');
const feedback=fs.readFileSync('wwwroot/js/battle-feedback.js','utf8');
test('equipment tooltip lists eight uncapped ratings including decimal values',()=>{
  const c=vm.createContext({fmtDecimal:n=>Number(n).toLocaleString('vi-VN',{maximumFractionDigits:2})});
  vm.runInContext(inventory.match(/const STAT_KEYS = .*;/)[0]+inventory.match(/const fmtRating = .*;/)[0]+inventory.slice(inventory.indexOf('function itemStats('),inventory.indexOf('// tooltip')),c);
  c.item={critPct:1000.25,critResistPct:350,accuracyPct:200,speedPct:50,comboPct:75,comboResistPct:25,reflectPct:10,reflectResistPct:15};
  const text=vm.runInContext('itemStats(item).join(" | ")',c);
  for(const name of ['Bạo kích','Kháng bạo kích','Chính xác','Né tránh','Liên kích','Kháng liên kích','Phản đòn','Kháng phản đòn'])assert.ok(text.includes(name),text);
  assert.ok(text.includes('1.000,25%'));assert.ok(!text.includes('NaN'));
});
test('monster tooltip exposes eight ratings including zero and hides undiscovered monsters',()=>{
  const c=vm.createContext({fmt:n=>String(n)});
  vm.runInContext(map.slice(map.indexOf('function mapMonsterTip('),map.indexOf('/** Bảng dưới')),c);
  c.mon={name:'<Boss>',hp:100,atk:10,def:1,crit:350,critResist:450,accuracy:0,dodge:200,combo:400,comboResist:450,reflect:400,reflectResist:450};
  const tip=vm.runInContext('mapMonsterTip(mon)',c);
  for(const name of ['Bạo kích','Kháng bạo kích','Chính xác','Né tránh','Liên kích','Kháng liên kích','Phản đòn','Kháng phản đòn'])assert.ok(tip.includes(name),tip);
  assert.ok(tip.includes('Chính xác: 0%'));assert.ok(tip.includes('&lt;Boss&gt;'));
  assert.equal(vm.runInContext('mapMonsterTip(mon,true)',c),'Chưa thám thính được quái này.');
});
test('enemy reactions emit separately once and do not duplicate sampled or repeated hero attacks',()=>{
  const c=vm.createContext({});vm.runInContext(feedback,c);
  c.b={counterDamage:[50],monsterExtraDamage:[20],monsterReflectDamage:[10]};
  c.seen={};const events=[];c.emit=e=>events.push(e);
  vm.runInContext('for(let j=0;j<3;j++)battleEnemyRatingFeedback(b,{name:"fight",trip:1,i:0,s:.99},seen,emit,10,20,100,20)',c);
  assert.equal(events.filter(e=>e.type==='num').length,2);
  assert.ok(events.some(e=>e.text==='Liên kích!'));assert.ok(events.some(e=>e.text==='Phản công!'));
  assert.equal(vm.runInContext('battleEnemyMainDamage(b,0)',c),30);
  vm.runInContext('battleEnemyRatingFeedback(b,{name:"fight",trip:1,i:0,s:.99,rep:true},seen,emit,10,20,100,20)',c);
  assert.equal(events.length,4);
  vm.runInContext('battleEnemyRatingFeedback(b,{name:"enter",trip:2,i:0,s:1},seen,emit,10,20,100,20)',c);
  assert.equal(events.length,4);
});
test('HP reveals enemy counter then primary strike then combo and finally healing',()=>{
  const c=vm.createContext({});vm.runInContext(feedback,c);
  c.b={monsterReflectDamage:[10],monsterExtraDamage:[20],heal:[5]};
  const hp=s=>{c.s=s;return vm.runInContext('battleHeroHpAt(b,{i:0,s},100,45,false,false)',c)};
  assert.equal(hp(.5),100);assert.equal(hp(.6),90);assert.equal(hp(.86),60);assert.equal(hp(.94),40);assert.equal(hp(.96),45);
});
test('lethal counter shows butterfly resurrection before the normal enemy strike',()=>{
  const c=vm.createContext({});vm.runInContext(feedback,c);
  c.b={monsterReflectDamage:[150],pet:{casts:[{turn:0,kind:'butterfly_rebirth',at:.55,heal:25}]}};
  assert.equal(vm.runInContext('battleHeroHpAt(b,{i:0,s:.6},100,10,false,false)',c),25);
});
test('hero combo and pet phases preserve counter damage already revealed in the main phase',()=>{
  const c=vm.createContext({});vm.runInContext(feedback,c);
  c.b={monsterReflectDamage:[10]};
  assert.equal(vm.runInContext('battleHeroHpAt(b,{name:"fight",i:0,s:.28,rep:true},100,60,false,false)',c),90);
  assert.equal(vm.runInContext('battleHeroHpAt(b,{name:"pet",i:0,s:0},100,60,false,false)',c),90);
  assert.equal(vm.runInContext('battleHeroHpAt(b,{name:"pet",i:0,s:.99},100,60,false,false)',c),90);
});
