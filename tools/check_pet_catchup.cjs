// Run from the repository root: node tools/check_pet_catchup.cjs
// Khung hình thưa (cửa sổ nền, lượt ngắn) không được làm mất phản hồi linh thú, nhất là thần thông sau phản công (at .9).
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict');
const scope=vm.createContext({Image:class{set src(x){this.complete=false;}}});
vm.runInContext(fs.readFileSync('wwwroot/js/battle.js','utf8').match(/^const HEAL_COL = [^;]+;/m)[0],scope);
vm.runInContext(fs.readFileSync('wwwroot/js/pet-battle.js','utf8'),scope);
const rebirth={turn:3,dmg:0,heal:28,guard:0,skill:'Thất Thải Niết Bàn',kind:'butterfly_rebirth',isMiracle:true,at:.9};
const support={turn:2,dmg:0,heal:12,guard:15,skill:'Thất Thải Linh Quang',kind:'butterfly',at:.45};
const battle={hits:5,win:false,waves:[{start:0}],pet:{name:'Thất Thải Hồ Điệp',casts:[support,rebirth]}};
// giống battle.js: mỗi khung hình gọi phản hồi theo cảnh linh thú, rồi bù phần đã lỡ
function play(frames){
  const seen={},events=[];
  for(const ph of frames){
    const scene=scope.petSceneForPhase(battle,ph);
    scope.petBattleFeedback(scene.battle,scene.phase,seen,e=>events.push(e),10,20,100,80);
    scope.petBattleCatchUp(battle,ph,seen,e=>events.push(e),10,20,100,80);
  }
  return events;
}
const count=(events,text)=>events.filter(e=>e.text===text).length;
const fight=(i,s)=>({name:'fight',i,s,rep:false,trip:7,wave:0}),pet=(i,s)=>({name:'pet',i,s,trip:7,wave:0});
// 1. Khung thưa: lượt 3 chỉ có một khung ở s .885, kế đó đã sang lượt 4 → vẫn hiện thần thông và số hồi máu đúng một lần
let ev=play([fight(2,.3),pet(2,.6),fight(2,.9),fight(3,.4),fight(3,.885),fight(4,.2),fight(4,.6)]);
assert.equal(count(ev,'Thần thông · Thất Thải Niết Bàn'),1,'Rebirth label shown after the frame skipped past turn 3');
assert.equal(count(ev,'+28'),1,'Rebirth heal shown once');
assert.equal(ev.filter(e=>e.type==='revive').length,1,'Hero HP bar refill animation starts once');
// 2. Khung thưa nhảy qua cả pha linh thú của lượt 2 → tuyệt kỹ thường vẫn hiện
ev=play([fight(2,.5),fight(2,.8),fight(3,.2)]);
assert.equal(count(ev,'Thất Thải Linh Quang'),1,'Skipped pet phase still shows the cast');
assert.equal(count(ev,'Khiên +15'),1);
// 3. Trận kết thúc ngay sau lượt có thần thông (khung kế tiếp đã là pha thua) → vẫn hiện
ev=play([fight(3,.5),{name:'lose',k:.1,trip:7}]);
assert.equal(count(ev,'Thần thông · Thất Thải Niết Bàn'),1,'Final phase flushes pending miracle');
// 4. Khung dày: không nhân đôi khi đường thường đã hiện
const dense=[];for(let i=2;i<=4;i++){for(let s=0;s<.7;s+=.02)dense.push(fight(i,s));if(i===2)for(let s=0;s<1;s+=.02)dense.push(pet(i,s));for(let s=.7;s<1;s+=.02)dense.push(fight(i,s));}
ev=play(dense);
assert.equal(count(ev,'Thần thông · Thất Thải Niết Bàn'),1);assert.equal(count(ev,'+28'),1);
assert.equal(count(ev,'Thất Thải Linh Quang'),1);assert.equal(count(ev,'+12'),1);
// 5. Không hiện trước thời điểm: đang ở lượt 3 trước phản công thì chưa có thần thông
ev=play([fight(3,.5),fight(3,.8)]);
assert.equal(count(ev,'Thần thông · Thất Thải Niết Bàn'),0,'No early miracle feedback');
console.log('PASS pet feedback catch-up: skipped turn ends, skipped pet phases, final phase, no duplicates, no early feedback.');
