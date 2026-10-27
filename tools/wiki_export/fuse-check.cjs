// Run from repository root: node tools/wiki_export/fuse-check.cjs
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const data=JSON.parse(fs.readFileSync('site/data/wiki.json','utf8'));
const ITEM=Object.fromEntries(data.items.map(i=>[i.id,i]));
const gear=data.items.find(i=>i.slot==='weapon'&&i.base&&i.rarity==='common');
const panes={},calls=[];
function pane(id){return panes[id]??= {innerHTML:'',handlers:{},grid:{scrollTop:0,clientWidth:0},querySelector(){return this.grid},querySelectorAll(){return []},addEventListener(name,fn){this.handlers[name]=fn}};}
const S={inventory:{[gear.id]:9},gemState:{vault:{stone_viem_so:4,stone_bang_so:5,gem_ruby_cui:9,gem_thien_phu:1}}};
const c={S,ITEM,DATA:data,busy:false,window:{},document:{addEventListener(){}},$:pane,itemTip:(id)=>ITEM[id].name,itemImg:id=>`<img src="${ITEM[id].icon}">`,itemLv:()=>1,fmt:String,stones:()=>1000,short:String,qWord:grade=>String.fromCharCode(60)+"span class=rc-"+grade+">"+grade+"</span>",openTab:(...args)=>calls.push(args),act:async path=>{calls.push(path);return true;}};
vm.createContext(c);vm.runInContext(fs.readFileSync('wwwroot/js/fuse.js','utf8'),c);
const forge=pane('pane-fuse');
const click=dataset=>forge.handlers.click({target:{closest:()=>({dataset,disabled:false,hasAttribute:key=>key==='data-fgs'&&Object.hasOwn(dataset,'fgs')})}});
const switchType=value=>forge.handlers.change({target:{matches:()=>true,value}});
(async()=>{
 c.renderFuse();assert.match(forge.innerHTML,/data-fuse-type/);assert.match(forge.innerHTML,/Hợp trang bị/);assert.match(forge.innerHTML,/Hợp Linh Thạch/);
 await click({fz:'auto'});assert.match(forge.innerHTML,/đã chọn 9\/9/);
 switchType('pet');assert.match(pane('pane-petfuse').innerHTML,/value="pet" selected/);assert.doesNotMatch(pane('pane-petfuse').innerHTML,/data-lc="petfuse"/);pane('pane-petfuse').handlers.change({target:{matches:()=>true,value:'gear'}});assert.match(forge.innerHTML,/đã chọn 9\/9/);
 switchType('gem');assert.match(forge.innerHTML,/Lò hợp Linh Thạch/);assert.match(forge.innerHTML,/đã chọn 0\/9/);assert.doesNotMatch(forge.innerHTML,new RegExp('data-fu="'+gear.id+'"'));
 await click({fz:'auto'});assert.match(forge.innerHTML,/đã chọn 9\/9/);assert.match(forge.innerHTML,/<b>90%<\/b>/);assert.equal((forge.innerHTML.match(/data-fgs=/g)||[]).length,9);
 switchType('gear');assert.match(forge.innerHTML,/đã chọn 9\/9/);assert.match(forge.innerHTML,/Lò luyện khí/);
 switchType('gem');await click({fgs:'0'});assert.match(forge.innerHTML,/đã chọn 8\/9/);await click({fz:'auto'});
 await click({fz:'go'});const request=calls.pop();assert.match(request,/^gem\/fuse-free\?/);const ids=new URLSearchParams(request.split('?')[1]).get('items').split(',');assert.equal(ids.length,9);assert.equal(ids.filter(id=>id==='stone_viem_so').length,4);assert.equal(ids.filter(id=>id==='stone_bang_so').length,5);assert.match(forge.innerHTML,/đã chọn 0\/9/);
 switchType('gear');await click({fz:'go'});assert.match(calls.pop(),/^fuse\?items=/);
 c.window.openGemFusion();assert.equal(calls.pop()[0],'fuse');assert.match(forge.innerHTML,/Lò hợp Linh Thạch/);
 console.log('PASS: shared forge, type switch, separate selections, nine mixed gems, chance display, removal/autofill, correct gem and gear endpoints, cleared furnace and vault shortcut');
})().catch(e=>{console.error(e);process.exitCode=1});
