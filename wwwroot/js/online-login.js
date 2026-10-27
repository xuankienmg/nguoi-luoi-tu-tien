const loginForm=document.getElementById('form'),message=document.getElementById('message');
const confirmPassword=document.getElementById('confirmPassword');
function validateConfirmation(){
  if(confirmPassword)confirmPassword.setCustomValidity(confirmPassword.value!==loginForm.password.value?'Mật khẩu xác nhận không khớp.':'');
}
if(confirmPassword){
  confirmPassword.addEventListener('input',validateConfirmation);
  loginForm.password.addEventListener('input',validateConfirmation);
}
for(const id of ['signup','loginLink']){
  const link=document.getElementById(id);
  if(link)link.search=location.search;
}
function finishLogin(result,returnTo){
  result.expires_at ||= Math.floor(Date.now()/1000)+(result.expires_in||3600);
  sessionStorage.setItem('van-dao-online-session',JSON.stringify(result));
  if(new URLSearchParams(location.search).get('native')==='1'&&window.chrome?.webview){
    window.chrome.webview.postMessage({type:'online-authenticated',session:result});return;
  }
  const target=new URL(returnTo||new URLSearchParams(location.search).get('returnTo')||'/',location.origin);
  location.replace(target.origin===location.origin&&!/online-(login|signup)/.test(target.pathname)?target.pathname+target.search:'/');
}
async function submitLogin(action){
  if(action==='signup')validateConfirmation();
  if(!loginForm.reportValidity())return;
  const buttons=[...loginForm.querySelectorAll('button')];buttons.forEach(b=>b.disabled=true);message.textContent='Đang kết nối…';
  try{
    const response=await fetch('/api/session/'+action,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:loginForm.email.value,password:loginForm.password.value})});
    const result=await response.json();
    if(!response.ok)throw new Error(result.error_description||result.msg||result.error||'Không đăng nhập được.');
    if(!result.access_token){message.textContent='Tài khoản đã tạo. Hãy xác nhận email rồi đăng nhập.';return}
    finishLogin(result);
  }catch(error){message.textContent=error.message}finally{buttons.forEach(b=>b.disabled=false)}
}
loginForm.addEventListener('submit',event=>{event.preventDefault();submitLogin(loginForm.dataset.action==='signup'?'signup':'signin')});
const googleButton=document.getElementById('google'),pendingKey='van-dao-google-pkce';
const base64url=bytes=>btoa(String.fromCharCode(...bytes)).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
googleButton.addEventListener('click',async()=>{
  googleButton.disabled=true;message.textContent='Đang mở Google…';
  try{
    const response=await fetch('/api/session/google',{cache:'no-store'}),config=await response.json();
    if(!response.ok)throw new Error(config.error||'Không kết nối được Google.');
    if(!config.enabled)throw new Error('Chưa bật Google OAuth trên Supabase local. Cần cấu hình Google Client ID và Client Secret.');
    const verifier=base64url(crypto.getRandomValues(new Uint8Array(32)));
    const challenge=base64url(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(verifier))));
    sessionStorage.setItem(pendingKey,JSON.stringify({verifier,created:Date.now(),returnTo:new URLSearchParams(location.search).get('returnTo')||'/'}));
    const url=new URL(config.authorizeUrl);url.searchParams.set('provider','google');url.searchParams.set('code_challenge',challenge);url.searchParams.set('code_challenge_method','s256');
    if(new URLSearchParams(location.search).get('native')==='1'&&window.chrome?.webview){
      window.chrome.webview.postMessage({type:'online-google',url:url.href});
      message.textContent='Hoàn tất đăng nhập trong trình duyệt rồi quay lại game.';
    }else{
      url.searchParams.set('redirect_to',location.origin+'/online-login.html');location.assign(url.href);
    }
  }catch(error){sessionStorage.removeItem(pendingKey);message.textContent=error.message}finally{googleButton.disabled=false}
});
window.completeGoogleCode=async code=>{
  googleButton.disabled=true;message.textContent='Đang xác nhận tài khoản Google…';
  try{
    const pending=JSON.parse(sessionStorage.getItem(pendingKey)||'null');sessionStorage.removeItem(pendingKey);
    if(!pending?.verifier||Date.now()-pending.created>10*60*1000)throw new Error('Phiên đăng nhập Google hết hạn. Hãy thử lại.');
    const response=await fetch('/api/session/google',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({authCode:code,codeVerifier:pending.verifier})});
    const result=await response.json();if(!response.ok||!result.access_token)throw new Error(result.error_description||result.msg||result.error||'Google chưa xác nhận tài khoản.');
    finishLogin(result,pending.returnTo);
  }catch(error){message.textContent=error.message}finally{googleButton.disabled=false}
};
window.googleLoginError=error=>{sessionStorage.removeItem(pendingKey);message.textContent=error;googleButton.disabled=false};
const callback=new URLSearchParams(location.search);
if(callback.has('code')||callback.has('error')){
  history.replaceState(null,'','/online-login.html');
  if(callback.has('error'))window.googleLoginError(callback.get('error_description')||'Đăng nhập Google đã bị hủy.');
  else window.completeGoogleCode(callback.get('code'));
}
