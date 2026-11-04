const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('wwwroot/js/breakthrough.js','utf8');
function setup(reduced=false){
  const code=source.slice(source.indexOf('function btFrame('),source.indexOf('\nrequestAnimationFrame(btFrame);',source.indexOf('function btFrame(')));
  const gradient={addColorStop(){}};
  const g=new Proxy({}, {get:(_,key)=>key.startsWith('create')?()=>gradient:(...args)=>{
    for(const value of args)if(typeof value==='number')assert(Number.isFinite(value),`${key}: finite coordinates`);
  },set:()=>true});
  const canvas={_sized:true,_top:0,_bot:0,width:96,height:84,offsetParent:{},getContext:()=>g};
  const scope=vm.createContext({requestAnimationFrame(){},paneOff:()=>false,$:()=>canvas,devicePixelRatio:1,
    S:{realm:0,stage:0},DATA:{realms:[{id:'luyen_khi',stages:[{}],heroMed:'med.png'}]},
    BT_PAL:{luyen_khi:{glow:'#67bb99',rune:'#67bb99',mote:'#ffffff'}},reduce:reduced,
    btProc:null,btFx:null,btCave:{complete:true,naturalWidth:100,naturalHeight:100},
    btEvolutionState:()=>({rank:0,breath:0,lift:0}),btReady:()=>false,
    btDrawFormation:()=>null,curSkin:()=>null,btImgs:{'med.png':{complete:false}},
    btMotes:[{a:0,r:30,y:60,s:1,life:.5}],btFrameLast:null});
  vm.runInContext(code,scope);
  return scope;
}
test('full breakthrough frame draws safely and qi particles rise with elapsed time',()=>{
  const scope=setup();
  scope.btFrame(1000);const y=scope.btMotes[0].y;
  scope.btFrame(1100);
  assert(scope.btMotes[0].y<y);
  assert(Math.abs(scope.btMotes[0].y-(y-1))<1e-9);
});
test('reduced motion keeps qi particles stationary',()=>{
  const scope=setup(true);
  scope.btFrame(1000);const y=scope.btMotes[0].y;
  scope.btFrame(2000);assert.equal(scope.btMotes[0].y,y);
});
test('hidden menu resets timing so returning does not jump particles',()=>{
  const scope=setup();scope.btFrame(1000);
  scope.paneOff=()=>true;scope.btFrame(2000);
  const y=scope.btMotes[0].y;
  scope.paneOff=()=>false;scope.btFrame(90000);
  assert.equal(scope.btMotes[0].y,y);
});
