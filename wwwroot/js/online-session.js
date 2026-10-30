/* Online mode keeps authentication separate from local/offline save selection. */
const onlineSession = {
  enabled:false, session:null, refreshing:null,
  async ensure(){
    if(window.__nativeSession)await window.__nativeSession;
    const response=await fetch('/api/session/config',{cache:'no-store'});
    if(!response.ok)throw new Error('Không đọc được chế độ kết nối.');
    this.enabled=!!(await response.json()).online;
    if(!this.enabled)return;
    try{this.session=JSON.parse(sessionStorage.getItem('van-dao-online-session')||'null')}catch{}
    if(!this.session?.access_token){this.login();throw new Error('Cần đăng nhập tài khoản online.')}
    if((this.session.expires_at||0)*1000<Date.now()+30000)await this.refresh();
    if(!document.getElementById('onlineLogout')){
      const button=document.createElement('button');button.id='onlineLogout';button.type='button';button.textContent='Đăng xuất online';
      button.style.cssText='position:fixed;right:8px;top:8px;z-index:10000;font-size:11px;padding:5px 9px;background:#172531;color:#eed5a0;border:1px solid #716143;border-radius:6px';
      button.onclick=async()=>{try{await fetch('/api/session/logout',{method:'POST',headers:{Authorization:'Bearer '+this.session.access_token}})}finally{this.clear();this.login()}};document.body.append(button);
    }
  },
  login(){
    if(window.__nativeSession){window.chrome.webview.postMessage({type:'online-login-required'});return}
    location.replace('/online-login.html?returnTo='+encodeURIComponent(location.pathname+location.search))
  },
  persist(){
    if(this.session)sessionStorage.setItem('van-dao-online-session',JSON.stringify(this.session));
    else sessionStorage.removeItem('van-dao-online-session');
    if(window.__nativeSession)window.chrome.webview.postMessage({type:'online-session-update',session:this.session});
  },
  clear(){this.session=null;this.persist()},
  async refresh(){
    if(this.refreshing)return this.refreshing;
    this.refreshing=(async()=>{
      const response=await fetch('/api/session/refresh',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken:this.session?.refresh_token})});
      const result=await response.json();
      if(!response.ok||!result.access_token){this.clear();this.login();throw new Error('Phiên đăng nhập hết hạn.')}
      result.expires_at ||= Math.floor(Date.now()/1000)+(result.expires_in||3600);
      this.session=result;this.persist();
    })().finally(()=>{this.refreshing=null});
    return this.refreshing;
  }
};
