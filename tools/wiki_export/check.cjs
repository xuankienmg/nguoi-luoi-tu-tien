// Run from the repository root: node tools/wiki_export/check.cjs
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const data=JSON.parse(fs.readFileSync('site/data/wiki.json','utf8'));
const source=fs.readFileSync('site/js/wiki.js','utf8');
const elements=new Map(),docEvents={},windowEvents={};
function element(id){if(!elements.has(id))elements.set(id,{innerHTML:'',textContent:'',value:'',open:false,events:{},addEventListener(k,f){this.events[k]=f;},showModal(){this.open=true;},close(){this.open=false;this.events.close?.();},scrollIntoView(){},parentElement:{scrollTop:0}});return elements.get(id);}
const location={hash:''};
const context={document:{getElementById:element,addEventListener(k,f){docEvents[k]=f;}},location,history:{replaceState(a,b,url){location.hash=url;}},window:{addEventListener(k,f){windowEvents[k]=f;}},URLSearchParams,console,fetch:async()=>({ok:true,json:async()=>data})};
vm.runInNewContext(source,context);
const flush=()=>new Promise(resolve=>setImmediate(resolve));
function route(hash){location.hash=hash;windowEvents.hashchange();}
function click(kind,value){docEvents.click({target:{closest(selector){return selector===`[data-${kind}]`?{dataset:{[kind]:value},disabled:false}:null;}}});}
(async()=>{
 await flush();
 assert.ok(element('summary').innerHTML.includes(data.items.filter(i=>i.slot).length.toLocaleString('vi-VN')));
 assert.equal((element('results').innerHTML.match(/class="card"/g)||[]).length,24);
 const firstPage=element('results').innerHTML;click('page','2');assert.notEqual(element('results').innerHTML,firstPage);assert.equal((element('results').innerHTML.match(/class="card"/g)||[]).length,24);
 route('#tab=gear&rarity=legendary&realm=0');assert.match(element('resultCount').textContent,/kết quả/);assert.match(element('results').innerHTML,/Thiên/);
 route('#tab=gear&q=khongcovatphamxyz');assert.match(element('results').innerHTML,/Không tìm thấy/);
 route('#tab=alchemy&q=bo+nguyen');assert.match(element('resultCount').textContent,/3 kết quả/);
 click('page','2');assert.equal((element('results').innerHTML.match(/class="card"/g)||[]).length,3);
 click('item','bo_nguyen_dan_thuong');assert.equal(element('detail').open,true);assert.ok(element('detailBody').innerHTML.includes(data.brews.find(b=>b.out==='bo_nguyen_dan_thuong').in.linh_thach.toLocaleString('vi-VN')));assert.match(element('detailBody').innerHTML,/Cửu Diệp Hồn Liên/);
 element('detail').close();
 route('#tab=pets');assert.match(element('resultCount').textContent,/14 kết quả/);
 click('pet',data.pets[0].id);assert.match(element('detailBody').innerHTML,/Ba hình thái/);const petHash=location.hash;element('detail').close();route(petHash);assert.equal(element('detail').open,true);assert.match(element('detailBody').innerHTML,new RegExp(data.pets[0].name));element('detail').close();
 for(let c=0;c<data.chapters.length;c++)for(let d=0;d<3;d++){
   route(`#tab=map&chapter=${c}&difficulty=${d}`);
   assert.equal((element('results').innerHTML.match(/<details /g)||[]).length,16);
   assert.ok(!element('results').innerHTML.includes('undefined'));
 }
 route('#tab=realms');assert.match(element('results').innerHTML,/Đột phá/);
 route('#tab=materials&type=herb');assert.match(element('results').innerHTML,/Linh thảo/);
 route('#tab=gems');assert.match(element('resultCount').textContent,/25 kết quả/);assert.equal((element('results').innerHTML.match(/class="card"/g)||[]).length,25);
 assert.ok(!element('results').innerHTML.includes('<div class="stats"></div>'));
 assert.match(element('results').innerHTML,/Cổ phù chứa chân hỏa viễn cổ thô ráp/);
 assert.match(element('results').innerHTML,/Khai khiếu: Mở \+1 Lỗ Khảm/);
 route('#tab=gems&gemKind=Tiên+Châu');assert.match(element('resultCount').textContent,/11 kết quả/);
 route('#tab=gems&gemKind=Cổ+Phù');assert.match(element('resultCount').textContent,/14 kết quả/);
 route('#tab=gems&gemTier=so');assert.match(element('resultCount').textContent,/10 kết quả/);
 route('#tab=gems&gemTier=cao');assert.match(element('resultCount').textContent,/11 kết quả/);
 route('#tab=gems&gemTier=dinh');assert.match(element('resultCount').textContent,/4 kết quả/);
 route('#tab=gems&q=khai+khieu');assert.match(element('resultCount').textContent,/1 kết quả/);
 route('#tab=gems&q=khongtonTai123');assert.match(element('results').innerHTML,/Không tìm thấy Cổ Phù hoặc Tiên Châu/);
 route('#tab=gems&view=recipes');assert.match(element('resultCount').textContent,/25 công thức/);assert.equal((element('results').innerHTML.match(/class="card"/g)||[]).length,25);
 route('#tab=gems&view=recipes&gemCat=stone');assert.match(element('resultCount').textContent,/9 công thức/);
 route('#tab=gems&view=recipes&gemCat=gem');assert.match(element('resultCount').textContent,/5 công thức/);
 route('#tab=gems&view=recipes&gemCat=transcend');assert.match(element('resultCount').textContent,/6 công thức/);
 route('#tab=gems&view=recipes&gemCat=herb');assert.match(element('resultCount').textContent,/5 công thức/);
 route('#tab=gems&view=recipes&rarity=divine');assert.match(element('resultCount').textContent,/4 công thức/);
 route('#tab=gems&view=guide');assert.match(element('resultCount').textContent,/4 chuyên mục/);assert.equal((element('results').innerHTML.match(/<details class="area" open>/g)||[]).length,4);assert.equal((element('selects').innerHTML.match(/<select /g)||[]).length,1);
 route('#tab=gems&view=guide&q=Horadric');assert.match(element('resultCount').textContent,/1 chuyên mục/);
 route('#tab=gems&view=guide&q=khongtonTai123');assert.match(element('results').innerHTML,/Không tìm thấy mục cẩm nang phù hợp/);
 click('item','gem_thien_phu');assert.equal(element('detail').open,true);assert.match(element('detailBody').innerHTML,/Cổ Phù Tiên Châu/);assert.match(element('detailBody').innerHTML,/Kho Đỉnh Cấp/);element('detail').close();
 click('item','gem_ruby_cui');assert.equal(element('detail').open,true);assert.match(element('detailBody').innerHTML,/Huyết Căn Thảo/);assert.match(element('detailBody').innerHTML,/Thăng hoa Xích Diễm Tiên Châu \(dùng Cổ Phù\)/);element('detail').close();
 click('item','huyet_can_thao');assert.equal(element('detail').open,true);assert.match(element('detailBody').innerHTML,/Ngưng luyện Tiên Châu/);assert.ok(!element('detailBody').innerHTML.includes('<div class="stats"></div>'));element('detail').close();
 const ids=new Set(data.items.map(i=>i.id)),families=new Set(data.items.map(i=>i.base));
 for(const b of data.brews){assert.ok(ids.has(b.out));for(const id of Object.keys(b.in))assert.ok(ids.has(id),id);}
 for(const r of data.gemRecipes){assert.ok(ids.has(r.resultItem),r.resultItem);for(const id of Object.keys(r.inputs))assert.ok(ids.has(id),id);}
 for(const [herbId,gemId] of Object.entries(data.herbToGem)){assert.ok(ids.has(herbId),herbId);assert.ok(ids.has(gemId),gemId);}
 for(const c of data.chapters)for(const d of c.difficulties)for(const drop of d.drops)assert.ok(ids.has(drop.item)||families.has(drop.item),drop.item);
 const images=[...data.items.map(i=>i.icon),...data.pets.flatMap(p=>p.forms.map(f=>f.image)),...data.chapters.flatMap(c=>c.difficulties.flatMap(d=>d.areas.flatMap(a=>a.waves.map(w=>w.image))))].filter(Boolean);
 for(const image of images)assert.ok(fs.existsSync(path.join('site/img/wiki',image.replace(/^img\//,''))),image);
 console.log(`PASS: navigation, search, filters, pagination rendering, Chú Linh (25 items & 25 recipes), pet deep links, 480 areas and ${new Set(images).size} image paths.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
