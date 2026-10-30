const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('wwwroot/js/inventory.js','utf8');
test('gear card lists socket effects only in the dedicated gem section',()=>{
  const c=context();
  Object.assign(c,{canWear:()=>true,compareOn:false,RAR_KEYS:['common'],MS_R:'Right',MS_L2:'Double',KBD:s=>s,
    bagCount:()=>1,qWord:()=> 'Phàm',fmt:String,itemSetHtml:()=>'',DATA:{realms:[{name:'Realm'}]},
    ITEM:{gear:{name:'Gear',slot:'weapon',rarity:'common',effects:[{id:'tinh_tam',text:'Vào trận với 25 nộ'}]},
      gem:{rarity:'common',special:'Khảm Tinh Thạch: +7% liên kích',effects:[{id:'lien_kich',value:7,text:'+7% liên kích'},{id:'hap_linh',value:3,text:'+3% tu vi'}]}}});
  c.S.equipSlots=['weapon'];c.S.gemState={slots:{weapon:['gem']},maxSockets:{weapon:1}};
  vm.runInContext(source.slice(source.indexOf('function itemCardHtml('),source.indexOf('function liveInv(')),c);
  const html=vm.runInContext('itemCardHtml("gear","slot","weapon")',c);
  const support=html.split('<div class="iv-gem-card">')[0];
  assert.ok(support.includes('Vào trận với 25 nộ'));
  assert.ok(!support.includes('liên kích'));
  assert.ok(!support.includes('Tu vi:'));
  assert.equal((html.match(/\+7% liên kích/g)||[]).length,1);
});
function context(){
  const c=vm.createContext({S:{gemState:{bonuses:{}}},fmtDecimal:n=>String(n),fmtNumberText:s=>s||'',adminOn:false,rar:()=>({n:'Phàm'})});
  vm.runInContext(source.match(/const STAT_KEYS = .*;/)[0]+source.match(/const fmtRating = .*;/)[0]+source.slice(source.indexOf('function formatItemGemEffectText('),source.indexOf('function itemTip('))+source.slice(source.indexOf('function itemTip('),source.indexOf('function herbSrcLines(')),c);
  vm.runInContext(source.slice(source.indexOf('function gemCombatStatsHtml('),source.indexOf('function gearFxHtml(')),c);
  return c;
}
test('gem summary shows all eight uncapped ratings including zero and exact decimals',()=>{
  const c=context();
  c.S.gemState.bonuses={crit:0,critResist:90,accuracy:450,dodge:1000.25,extraHit:450,comboResist:450,reflect:450,reflectResist:450};
  const html=vm.runInContext('gemCombatStatsHtml()',c);
  for(const name of ['Bạo kích','Kháng bạo kích','Chính xác','Né tránh','Liên kích','Kháng liên kích','Phản đòn','Kháng phản đòn'])assert.ok(html.includes(name),name);
  for(const value of ['0%','90%','450%','1.000,25%'])assert.ok(html.includes(value),html);
  assert.match(html,/cơ hội phản công/i);
  assert.doesNotMatch(html,/trần 30%|NaN|undefined/);
});
test('socketed tooltip sums three new rating effects instead of retaining the first gem text',()=>{
  const c=context();
  c.DATA={realms:[{name:'Realm'}]};
  for(const [id,name] of [['gem_combo_resist','Kháng liên kích'],['gem_reflect_resist','Kháng phản đòn'],['gem_crit_resist','Kháng bạo kích'],['gem_accuracy','Chính xác'],['gem_dodge','Né tránh']]){
    c.ITEM={gear:{name:'Gear',slot:'head',rarity:'common'},gem:{name:'Gem',rarity:'common',special:'Khảm Tinh Thạch: +30%',effects:[{id,name,value:30,text:'+30% '+name}]}};
    c.S.gemState={slots:{head:['gem','gem','gem']},maxSockets:{head:3}};
    const tip=vm.runInContext('itemTip("gear", "", "head")',c);
    assert.ok(tip.includes('+90% '+name.toLowerCase()),tip);
  }
});
test('exported gem cards and socket tooltips agree for all eight specialties',()=>{
  const data=JSON.parse(fs.readFileSync('site/data/wiki.json','utf8'));
  const checks=[['hoa','Bạo kích +90%'],['loi','+90% liên kích'],['bang','+90% kháng bạo kích'],['tho','+90% kháng liên kích'],['phong','+90% né tránh'],['quang','+90% chính xác'],['am','+90% phản đòn'],['vu_tru','+90% kháng phản đòn']];
  for(const [theme,expected] of checks){
    const c=context(),gem=data.items.find(i=>i.id===`gem_${theme}_lv10`);
    c.DATA=data;c.ITEM={gear:{name:'Gear',slot:'head',rarity:'common'},[gem.id]:gem};
    c.S.gemState={slots:{head:Array(3).fill(gem.id)},maxSockets:{head:3}};
    const tip=vm.runInContext('itemTip("gear", "", "head")',c);
    assert.ok(tip.includes(expected),theme+': '+tip);
    if(theme==='hoa')assert.ok(!gem.effects.some(e=>e.id==='crit'),'primary crit is not duplicated as an effect');
  }
});
