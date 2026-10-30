/* Thẻ Tài khoản: chọn chế độ chơi và đăng nhập online ngay trong Launcher.
   Launcher (C#) gọi backend và lưu phiên mã hoá cho game; trang chỉ nhận email, không giữ token. */
const onlineState = { available: false, mode: 'Offline', email: null, signup: false, busy: false };
const $acct = id => document.getElementById(id);

function setPlayMode(mode) {
  if (mode === 'Online' && !onlineState.available) return;
  acctMessage('');
  postMessage({ type: 'online_mode', mode });
}

function acctMessage(text) { $acct('acct-message').textContent = text || ''; }

function renderOnlineState() {
  const online = onlineState.mode === 'Online' && onlineState.available, signed = online && onlineState.email != null;
  $acct('mode-offline').classList.toggle('active', !online);
  $acct('mode-online').classList.toggle('active', online);
  $acct('mode-online').disabled = !onlineState.available;
  $acct('acct-offline').hidden = online || !onlineState.available;
  $acct('acct-unavailable').hidden = onlineState.available;
  $acct('acct-form').hidden = !online || signed;
  $acct('acct-signed').hidden = !signed;
  $acct('acct-signed-email').textContent = onlineState.email || '';
  $acct('acct-confirm').hidden = !onlineState.signup;
  $acct('acct-password').autocomplete = onlineState.signup ? 'new-password' : 'current-password';
  $acct('acct-submit').textContent = onlineState.signup ? 'Tạo tài khoản' : 'Đăng nhập';
  $acct('acct-switch').textContent = onlineState.signup ? 'Đã có tài khoản? Đăng nhập' : 'Tạo tài khoản mới';
  $acct('acct-forgot').hidden = onlineState.signup;
  // thanh dưới: online thì hiện tài khoản thay cho nhân vật offline trên máy
  const player = launcherState.player || {};
  $acct('player-name-display').textContent = online ? (signed ? onlineState.email : 'Tài khoản online') : player.name || 'Đạo hữu';
  $acct('player-realm-display').textContent = online ? (signed ? 'Nhân vật online' : 'Chưa đăng nhập') : player.realm || 'Luyện Khí';
}

function handleOnlineState(data) {
  onlineState.available = !!data.available;
  onlineState.mode = data.mode === 'Online' ? 'Online' : 'Offline';
  onlineState.email = data.email ?? null;
  renderOnlineState();
}

function setAcctBusy(busy) {
  onlineState.busy = busy;
  $acct('acct-form').querySelectorAll('button').forEach(b => b.disabled = busy);
}

function sendSession(action, withPassword) {
  const email = $acct('acct-email'), password = $acct('acct-password');
  if (!email.value.trim() || !email.checkValidity()) { acctMessage('Hãy nhập email hợp lệ.'); email.focus(); return; }
  if (withPassword) {
    if (password.value.length < 8) { acctMessage('Mật khẩu cần ít nhất 8 ký tự.'); password.focus(); return; }
    if (onlineState.signup && $acct('acct-confirm').value !== password.value) { acctMessage('Mật khẩu xác nhận không khớp.'); $acct('acct-confirm').focus(); return; }
  }
  setAcctBusy(true);
  acctMessage(action === 'recover' || action === 'resend' ? 'Đang gửi…' : 'Đang kết nối…');
  postMessage({ type: 'online_session', action, email: email.value.trim(), password: withPassword ? password.value : null, remember: $acct('acct-remember').checked });
}

function handleOnlineResult(data) {
  setAcctBusy(false);
  const body = data.body || {}, ok = data.status >= 200 && data.status < 300;
  $acct('acct-resend').hidden = body.error_code !== 'email_not_confirmed';
  if (body.signedIn) {
    $acct('acct-password').value = $acct('acct-confirm').value = '';
    onlineState.signup = false;
    acctMessage('');
    return;
  }
  if (!ok) { acctMessage(authError(body, 'Không đăng nhập được.')); return; }
  if (data.action === 'signup') {
    acctMessage('Tài khoản đã tạo. Hãy mở thư xác nhận trong email rồi đăng nhập.');
    $acct('acct-resend').hidden = false;
    return;
  }
  acctMessage(body.message || '');
}

/** VÀO GAME ở chế độ Online mà chưa đăng nhập: chặn lại và đưa người chơi tới ô email. */
function onlineBlocksLaunch() {
  if (onlineState.mode !== 'Online' || !onlineState.available || onlineState.email != null) return false;
  switchTab('home');
  acctMessage('Hãy đăng nhập tài khoản online trước khi vào game.');
  $acct('acct-email').focus();
  return true;
}

document.addEventListener('DOMContentLoaded', () => {
  $acct('acct-form').addEventListener('submit', e => { e.preventDefault(); if (!onlineState.busy) sendSession(onlineState.signup ? 'signup' : 'signin', true); });
  $acct('acct-resend').addEventListener('click', () => sendSession('resend', false));
  $acct('acct-forgot').addEventListener('click', e => { e.preventDefault(); sendSession('recover', false); });
  $acct('acct-switch').addEventListener('click', e => { e.preventDefault(); onlineState.signup = !onlineState.signup; acctMessage(''); renderOnlineState(); });
  $acct('acct-signout').addEventListener('click', () => { acctMessage(''); postMessage({ type: 'online_signout' }); });
  renderOnlineState();
});
