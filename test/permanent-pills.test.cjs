const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('wwwroot/js/inventory.js','utf8');
test('permanent pills expose remaining cap and cumulative bonus',()=>{
  const start=source.indexOf('// Permanent pill progress');
  assert.notEqual(start,-1);
  const code=source.slice(start,source.indexOf('// End permanent pill progress',start));
  const ctx=vm.createContext({S:{permanentPills:[{id:'thien_luc_dan',label:'Tấn công',used:35,cap:100,total:350,unit:''}]},fmtDecimal:String, document:{addEventListener(){}}});
  vm.runInContext(code,ctx);
  const text=vm.runInContext("permanentPillText('thien_luc_dan')",ctx);
  assert.match(text,/35 \/ 100/); assert.match(text,/65/); assert.match(text,/350/);
  assert.equal(vm.runInContext("permanentPillText('other')",ctx),'');
});
test('permanent pills allow capped bulk consumption',()=>{
  const code=source.match(/const canUseAll = [^\n]+/)[0];
  const ctx=vm.createContext({S:{permanentPills:[{id:'thien_luc_dan'}]}});
  vm.runInContext(code,ctx);
  assert.equal(vm.runInContext("canUseAll({id:'thien_luc_dan',kind:'Đan vĩnh viễn',type:'elixir'})",ctx),true);
  assert.equal(vm.runInContext("canUseAll({id:'tay_tuy_dan',type:'elixir'})",ctx),false);
});

test('buttons remain usable after a state render while an action is completing',()=>{
  const start=source.indexOf('// Permanent pill progress');
  const code=source.slice(start,source.indexOf('// End permanent pill progress',start));
  const ctx=vm.createContext({S:{inventory:{pill:2},permanentPills:[{id:'pill',label:'Tấn công',used:1,cap:100,total:10,unit:''}]},fmtDecimal:String,busy:true,document:{addEventListener(){}}});
  vm.runInContext(code,ctx);
  assert.doesNotMatch(vm.runInContext("permanentPillHtml('pill')",ctx),/disabled/);
});
