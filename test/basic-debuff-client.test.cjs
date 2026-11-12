const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const feedback=fs.readFileSync('wwwroot/js/battle-feedback.js','utf8');
const ctx=()=>{const c=vm.createContext({});vm.runInContext(feedback.slice(feedback.indexOf('const BATTLE_CONTROL'),feedback.indexOf('/** Chữ "Băng vỡ"')),c);return c};

test('đòn thường gây khống: chỉ hiện từ lúc đòn trúng',()=>{
  const c=ctx();
  c.b={monHpLeft:[100],monControl:['stun'],basicSecondary:['stun'],casts:[]};
  assert.equal(vm.runInContext('battleControlAt(b,{name:"fight",i:0,s:.2})',c),'');
  assert.equal(vm.runInContext('battleControlAt(b,{name:"fight",i:0,s:.5})',c),'stun');
});

test('đòn thường gây hiệu ứng: hiện chữ một lần trên quái',()=>{
  const c=ctx(),out=[];c.emit=f=>out.push(f);c.seen={};
  c.b={basicSecondary:['','paralyze'],casts:[]};
  vm.runInContext('battleSecondaryFeedback(b,{name:"fight",i:1,s:.5,trip:1},seen,emit,0,0,9,9);battleSecondaryFeedback(b,{name:"fight",i:1,s:.6,trip:1},seen,emit,0,0,9,9)',c);
  assert.equal(out.length,1);assert.equal(out[0].text,'Tê liệt');assert.equal(out[0].x,9);
});

test('sát thương theo lượt phân biệt nhiễm độc và thiêu đốt trong 2 lượt',()=>{
  const c=ctx();
  c.b={basicSecondary:['poison','','',''],casts:[{turn:2,secondary:'burn'}]};
  assert.equal(vm.runInContext('battleDotKind(b,0)',c),'poison');
  assert.equal(vm.runInContext('battleDotKind(b,1)',c),'poison');
  assert.equal(vm.runInContext('battleDotKind(b,3)',c),'burn');
});

test('hiệu ứng mới (khống, suy yếu) đè thiêu đốt cũ; thiêu đốt / nhiễm độc mới thì không tính là đè',()=>{
  const c=ctx();
  c.b={basicSecondary:['stun','poison','',''],casts:[{turn:2,secondary:'weaken'},{turn:3,secondary:'wind'}],pet:{casts:[]}};
  assert.equal(vm.runInContext('battleDotOverridden(b,0)',c),true);
  assert.equal(vm.runInContext('battleDotOverridden(b,1)',c),false);
  assert.equal(vm.runInContext('battleDotOverridden(b,2)',c),true);
  assert.equal(vm.runInContext('battleDotOverridden(b,3)',c),false);   // Phong hộ là buff tu sĩ
  c.b={pet:{casts:[{turn:0,paralyzeTurns:1,controlKind:'freeze'}]}};
  assert.equal(vm.runInContext('battleDotOverridden(b,0)',c),true);
});

test('nhãn hiệu ứng bất lợi dưới thanh máu: thiêu đốt / nhiễm độc, suy yếu, khống',()=>{
  const c=ctx();
  c.b={burnPct:5,weakened:[true,false],casts:[]};
  assert.equal(vm.runInContext('battleDebuffLabels(b,{name:"fight",i:0,s:.5},"poison","").map(d=>d.text).join("|")',c),'Nhiễm độc|Suy yếu');
  assert.equal(vm.runInContext('battleDebuffLabels(b,{name:"fight",i:1,s:.5},"burn","freeze").map(d=>d.text).join("|")',c),'Thiêu đốt|Đóng băng');
  assert.equal(vm.runInContext('battleDebuffLabels(b,{name:"fight",i:1,s:.5},false,"").length',c),0);
});

test('thanh máu vẽ nhãn hiệu ứng bên dưới thanh, không còn nhãn khống cạnh tên',()=>{
  const src=fs.readFileSync('wwwroot/js/health-bars.js','utf8'),texts=[];
  const fake={canvas:{width:800},measureText:t=>({width:String(t).length*6}),fillText:(t,x,y)=>texts.push({t,y}),strokeText(){},fillRect(){},strokeRect(){},beginPath(){},moveTo(){},lineTo(){},closePath(){},stroke(){},save(){},restore(){}};
  const c=vm.createContext({fmtCompact:String});vm.runInContext(src,c);c.ctx=fake;
  const box=vm.runInContext('drawBattleHp(ctx,{x:400,y:100,width:120,state:{fraction:1,ghost:1,current:10,max:10},name:"Quái",kind:"enemy",rage:null,debuffs:[{text:"Choáng",col:"#fff"}],compact:false})',c);
  const label=texts.find(x=>x.t==='Choáng');
  assert.ok(label,'thiếu nhãn');
  assert.ok(label.y>box.top+box.height,'nhãn phải nằm dưới thanh máu');
});
