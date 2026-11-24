const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const vm=require('node:vm');
const src=fs.readFileSync('wwwroot/js/forge.js','utf8');
const pure=src.slice(src.indexOf('const FORGE_KINDS'),src.indexOf('function renderForge('));
test('result descriptions reflect protection and triple great relics',()=>{
 const ctx=vm.createContext({DATA:{forgeRelics:[]}});vm.runInContext(pure,ctx);
 ctx.F={relics:['cuu_chuyen_lien','thien_cong_an']};
 assert.match(vm.runInContext("forgeEffectText('break',F)",ctx),/75%/);
 assert.match(vm.runInContext("forgeEffectText('great',F)",ctx),/20%/);
 assert.equal(vm.runInContext("forgeEffectText('great',{relics:[]})",ctx),'+2 cấp');
});
test('new relic images use supplied catalog icons',()=>{
 const ctx=vm.createContext({DATA:{forgeRelics:[]}});vm.runInContext(pure,ctx);
 assert.match(vm.runInContext("forgeRelicIcon({icon:'img/forge/relics/hoa_long_dinh.png'})",ctx),/hoa_long_dinh.png/);
 assert.doesNotMatch(vm.runInContext("forgeRelicIcon({})",ctx),/undefined/);
});
