const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
function context(desktop){
 const messages=[],opened=[];
 const scope={window:{chrome:desktop?{webview:{postMessage:m=>messages.push(m)}}:undefined,open:u=>opened.push(u)},setInterval:()=>{},notifyHost:(type,extra)=>messages.push({type,...extra})};
 vm.createContext(scope);vm.runInContext(fs.readFileSync('wwwroot/js/version.js','utf8'),scope);
 vm.runInContext("verOpen('https://example.com/release')",scope);return {messages,opened};
}
const desktop=context(true);assert.equal(desktop.messages[0]?.type,'open-launcher');assert.equal(desktop.opened.length,0);
assert.equal(context(false).opened[0],'https://example.com/release');
console.log('PASS desktop launcher handoff and browser download fallback.');
