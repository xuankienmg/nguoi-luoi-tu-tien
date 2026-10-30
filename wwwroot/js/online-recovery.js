/* Quên mật khẩu (gửi thư) và đặt lại mật khẩu (trang mở từ liên kết trong thư, thường trên trình duyệt ngoài game). */
const recoveryForm=document.getElementById('form'),message=document.getElementById('message');
const loginLink=document.getElementById('loginLink');
if(loginLink)loginLink.search=location.search;
async function post(path,body,token){
  const headers={'Content-Type':'application/json'};
  if(token)headers.Authorization='Bearer '+token;
  const response=await fetch(path,{method:'POST',headers,body:JSON.stringify(body)});
  return {ok:response.ok,result:await response.json().catch(()=>({}))};
}
function busy(on){recoveryForm.querySelectorAll('button').forEach(b=>b.disabled=on)}

if(recoveryForm.dataset.action==='recover'){
  recoveryForm.addEventListener('submit',async event=>{
    event.preventDefault();
    busy(true);message.textContent='Đang gửi…';
    try{
      const {ok,result}=await post('/api/session/recover',{email:recoveryForm.email.value});
      message.textContent=ok?result.message:authError(result,'Chưa gửi được email.');
    }catch{message.textContent='Không kết nối được máy chủ.'}finally{busy(false)}
  });
}

if(recoveryForm.dataset.action==='reset'){
  // Supabase chuyển về trang này với #access_token=…&type=recovery, hoặc #error_code=… khi liên kết hỏng
  const params=new URLSearchParams(location.hash.slice(1));
  const token=params.get('type')==='recovery'?params.get('access_token'):null;
  history.replaceState(null,'',location.pathname);   // không để token nằm trên thanh địa chỉ / lịch sử
  if(token)recoveryForm.hidden=false;
  else message.textContent=authError({error_code:params.get('error_code'),error_description:params.get('error_description')},
    'Liên kết đặt lại mật khẩu không hợp lệ. Hãy dùng liên kết mới nhất trong email hoặc yêu cầu gửi lại.');
  const confirmPassword=document.getElementById('confirmPassword');
  const validate=()=>confirmPassword.setCustomValidity(confirmPassword.value!==recoveryForm.password.value?'Mật khẩu xác nhận không khớp.':'');
  confirmPassword.addEventListener('input',validate);recoveryForm.password.addEventListener('input',validate);
  recoveryForm.addEventListener('submit',async event=>{
    event.preventDefault();validate();
    if(!recoveryForm.reportValidity())return;
    busy(true);message.textContent='Đang đổi mật khẩu…';
    try{
      const {ok,result}=await post('/api/session/password',{password:recoveryForm.password.value},token);
      if(!ok){message.textContent=authError(result,'Chưa đổi được mật khẩu.');return}
      recoveryForm.hidden=true;
      message.textContent='Đã đổi mật khẩu. Quay lại game và đăng nhập bằng mật khẩu mới.';
      fetch('/api/session/logout',{method:'POST',headers:{Authorization:'Bearer '+token}}).catch(()=>{});   // bỏ phiên tạm của liên kết
    }catch{message.textContent='Không kết nối được máy chủ.'}finally{busy(false)}
  });
}
