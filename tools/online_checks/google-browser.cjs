const {chromium}=require('C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert=require('node:assert/strict'),{createHash}=require('node:crypto');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const page=await browser.newPage();
  await page.goto('http://127.0.0.1:5189/online-login.html');
  assert.equal(await page.locator('#google').isVisible(),false);
  // Google stays hidden in the product; expose it only inside this integration test.
  await page.locator('#google').evaluate(button=>button.hidden=false);
  await page.click('#google');
  await page.getByText('Chưa bật Google OAuth',{exact:false}).waitFor();
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('van-dao-online-session')),null);
  await page.route('**/api/session/google',async route=>{
   if(route.request().method()==='GET')return route.fulfill({json:{enabled:true,authorizeUrl:'http://127.0.0.1:55431/auth/v1/authorize'}});
   const body=route.request().postDataJSON();assert.equal(body.authCode,'test-code');
   assert.equal(createHash('sha256').update(body.codeVerifier).digest('base64url'),challenge);
   await route.fulfill({status:400,json:{error:'Mã thử nghiệm bị từ chối.'}});
  });
  let challenge;
  await page.route('http://127.0.0.1:55431/auth/v1/authorize?**',async route=>{
   const url=new URL(route.request().url());challenge=url.searchParams.get('code_challenge');
   assert.equal(url.searchParams.get('provider'),'google');assert.equal(url.searchParams.get('code_challenge_method'),'s256');
   assert.equal(url.searchParams.get('redirect_to'),'http://127.0.0.1:5189/online-login.html');
   assert.equal(challenge.length,43);await route.fulfill({body:'Mock OAuth provider'});
  });
  await page.click('#google');await page.waitForURL('**/auth/v1/authorize?**');
  await page.goto('http://127.0.0.1:5189/online-login.html?code=test-code');
  await page.getByText('Mã thử nghiệm bị từ chối.',{exact:true}).waitFor();
  assert.equal(new URL(page.url()).search,'');
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('van-dao-google-pkce')),null);
  await page.goto('http://127.0.0.1:5189/online-login.html?code=unsolicited');
  await page.getByText('Phiên đăng nhập Google hết hạn. Hãy thử lại.',{exact:true}).waitFor();
  console.log('PASS: disabled provider, PKCE S256 round trip (mock provider), rejected exchange, code cleanup and unsolicited callback. Real Google credentials still required.');
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
