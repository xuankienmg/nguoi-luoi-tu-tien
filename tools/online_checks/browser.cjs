const {chromium}=require('C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 const context=await browser.newContext({viewport:{width:1240,height:820}});
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5189/');
 await page.waitForURL('**/online-login.html?**');
 const id=Date.now()+'-'+Math.random().toString(36).slice(2);
 await page.click('#signup');await page.waitForURL('**/online-signup.html?**');
 await page.fill('#email',`ui-${id}@example.com`);await page.fill('#password',`Local-UI-Test-${id}`);
 let signupRequests=0;page.on('request',request=>{if(request.url().endsWith('/api/session/signup'))signupRequests++});
 await page.fill('#confirmPassword','Different-password');await page.click('#signupSubmit');
 if(signupRequests!==0||!await page.locator('#confirmPassword').evaluate(input=>input.validity.customError))throw new Error('Mismatched password was not blocked.');
 await page.fill('#confirmPassword',`Local-UI-Test-${id}`);await page.click('#signupSubmit');
 await page.waitForURL('http://127.0.0.1:5189/');
 await page.waitForFunction(()=>typeof S!=='undefined'&&S?.id);
 const player=await page.evaluate(()=>({id:S.id,realm:S.realm,online:onlineSession.enabled,admin:devMode}));
 if(!player.online||player.realm!==0||player.admin)throw new Error('Online UI failed to load a fresh non-admin character.');
 await page.screenshot({path:path.resolve('.local/online-ui.png'),fullPage:true});
 // Client-side state can be changed in memory, but the next authoritative sync replaces it.
 await page.evaluate(()=>{S.realm=9;S.inventory.linh_thach=2000000000;});
 await page.evaluate(async()=>{const result=await api('GET',`/api/players/${S.id}`);setState(result.state??result)});
 if(await page.evaluate(()=>S.realm!==0||S.inventory.linh_thach===2000000000))throw new Error('Client tampering survived authoritative sync.');
 await page.click('#onlineLogout');await page.waitForURL('**/online-login.html?**');
 if(await page.evaluate(()=>sessionStorage.getItem('van-dao-online-session')))throw new Error('Logout left the client session behind.');
 await browser.close();if(errors.length)throw new Error(errors.join('\n'));
 console.log('PASS: browser signup/login through desktop proxy, fresh server character, admin disabled, forged client state replaced, logout.');
})().catch(e=>{console.error(e);process.exit(1)});
