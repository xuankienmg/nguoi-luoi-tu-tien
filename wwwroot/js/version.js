/* js/version.js: bắt cập nhật — bản quá cũ thì chặn chơi, có bản mới thì nhắc. Dữ liệu: GET /api/version (app_config trên Supabase). */
const verEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
// Cửa sổ desktop: mở Launcher để cập nhật (game tự thoát); trình duyệt thường: mở link tải
const VER_DESKTOP = !!window.chrome?.webview;
const verOpen = url => { if (VER_DESKTOP) notifyHost('open-launcher', {url}); else if (url) window.open(url, '_blank'); };

async function checkVersion(force){
  try { const r = await fetch(`/api/version${force ? '?force=true' : ''}`); return r.ok ? await r.json() : null; } catch(e){ return null; }
}

/** Màn hình chặn: phủ toàn bộ game, chỉ còn nút tải bản mới. */
function showUpdateGate(v){
  if (typeof Preload !== 'undefined') Preload.hide();
  if (!v || $('updGate')) return;
  const el = document.createElement('div');
  el.id = 'updGate'; el.className = 'upd-gate'; el.setAttribute('role', 'dialog'); el.setAttribute('aria-modal', 'true');
  el.innerHTML = `<div class="upd-card">
    <h2>Cần cập nhật phiên bản mới</h2>
    <p>Bản đang chạy <b>${verEsc(v.current)}</b> đã ngừng hỗ trợ. Bản mới nhất: <b>${verEsc(v.latest || v.min)}</b>.</p>
    ${v.message ? `<p class="upd-msg">${verEsc(v.message)}</p>` : ''}
    ${VER_DESKTOP ? '<button class="upd-dl" id="updDl">Mở Launcher để cập nhật</button>' : v.url ? '<button class="upd-dl" id="updDl">Tải bản mới</button>' : '<p class="upd-msg">Liên hệ người phát hành để lấy bản mới.</p>'}
    <button class="upd-re" id="updRe">Kiểm tra lại</button>
    ${quitButton().replace('<button', '<button class="upd-re"')}
    <small>${VER_DESKTOP ? 'Game sẽ tự đóng để Launcher tải và cài bản mới. Tiến trình tu luyện lưu trong máy nên vẫn giữ nguyên.' : 'Tiến trình tu luyện lưu trong máy nên vẫn giữ nguyên khi chạy bản mới. Tắt bản cũ trước khi mở bản mới.'}</small>
  </div>`;
  document.body.appendChild(el);
  $('updDl')?.addEventListener('click', () => verOpen(v.url));
  $('updRe').addEventListener('click', async () => { const n = await checkVersion(true); if (n && !n.blocked) location.reload(); });
  if (typeof hideTip === "function") hideTip();
  if (MODE === 'strip') requestAnimationFrame(() => requestAnimationFrame(() => window.reportShape?.(true)));   // dải game cần hình để hiện
}
window.showUpdateGate = showUpdateGate;

/** Có bản mới nhưng chưa bắt buộc: thanh nhắc nhỏ, đóng thì không nhắc lại bản đó. */
function showUpdateBanner(v){
  if (!v || v.status !== 'update' || MODE === 'strip' || $('updBar')) return;
  try { if (localStorage.getItem('van-dao-upd-skip') === v.latest) return; } catch(e){}
  const el = document.createElement('div');
  el.id = 'updBar'; el.className = 'upd-bar'; el.setAttribute('role', 'status');
  el.innerHTML = `<span>Đã có bản <b>${verEsc(v.latest)}</b>${v.message ? ' · ' + verEsc(v.message) : ''}</span>
    ${VER_DESKTOP ? '<button id="updBarDl">Cập nhật</button>' : v.url ? '<button id="updBarDl">Tải</button>' : ''}<button id="updBarX" aria-label="Đóng">✕</button>`;
  document.body.appendChild(el);
  $('updBarDl')?.addEventListener('click', () => verOpen(v.url));
  $('updBarX').addEventListener('click', () => { el.remove(); try { localStorage.setItem('van-dao-upd-skip', v.latest); } catch(e){} });
}

// game mở lâu: 30 phút kiểm tra lại một lần
setInterval(async () => { const v = await checkVersion(); if (v?.blocked) showUpdateGate(v); else showUpdateBanner(v); }, 30 * 60 * 1000);
