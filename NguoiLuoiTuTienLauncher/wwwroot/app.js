// State
let launcherState = {
  status: 'ready', // 'ready', 'update_available', 'downloading', 'playing'
  currentVersion: '0.7.0',
  latestVersion: null,
  downloadUrl: null,
  
  // Launcher update info
  launcherVersion: '1.0.0',
  hasLauncherUpdate: false,
  latestLauncherVersion: null,
  launcherDownloadUrl: null,

  settings: {},
  player: { name: 'Đạo hữu', realm: 'Luyện Khí' },
  theme: 'dark'
};

// Window Controls
function windowMinimize() {
  postMessage({ type: 'minimize' });
}

function windowClose() {
  postMessage({ type: 'close' });
}

function postMessage(obj) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(obj);
  } else {
    console.log('PostMessage (dev/browser mode):', obj);
  }
}

// Theme Toggle
function applyTheme(theme) {
  launcherState.theme = theme;
  if (theme === 'light') {
    document.body.classList.add('light');
  } else {
    document.body.classList.remove('light');
  }
  const btn = document.getElementById('theme-toggle-btn');
  if (btn) {
    btn.textContent = theme === 'light' ? '☀️' : '🌙';
    btn.title = theme === 'light' ? 'Chuyển sang Tối' : 'Chuyển sang Sáng';
  }
}

function toggleTheme() {
  const newTheme = launcherState.theme === 'dark' ? 'light' : 'dark';
  applyTheme(newTheme);
  // Persist theme to settings
  postMessage({
    type: 'save_settings',
    settings: {
      ...gatherCurrentSettings(),
      theme: newTheme
    }
  });
}

// Ngôn ngữ (dùng chung với game qua window.json): lưu rồi tải lại trang, i18n.js dịch giao diện khi là English
const currentLang = () => (window.LANG === 'en' ? 'en' : 'vi');

function setLanguage(lang) {
  lang = lang === 'en' ? 'en' : 'vi';
  postMessage({ type: 'save_settings', settings: { ...gatherCurrentSettings(), language: lang } });
  if (lang !== currentLang()) window.setLang(lang);
}

function toggleLanguage() {
  setLanguage(currentLang() === 'en' ? 'vi' : 'en');
}

function showLanguage() {
  const btn = document.getElementById('lang-toggle-btn');
  if (btn) btn.textContent = currentLang().toUpperCase();
  const sel = document.getElementById('setting-language');
  if (sel) sel.value = currentLang();
}
document.addEventListener('DOMContentLoaded', showLanguage);

function gatherCurrentSettings() {
  return {
    language: currentLang(),
    gitHubRepo: document.getElementById('setting-repo')?.value?.trim() || '',
    gamePath: document.getElementById('setting-path')?.value?.trim() || '',
    currentVersion: document.getElementById('setting-version')?.value?.trim() || '',
    launcherVersion: document.getElementById('setting-launcher-version')?.value?.trim() || '',
    autoCheckOnStartup: document.getElementById('setting-autocheck')?.checked !== false,
    closeLauncherOnPlay: document.getElementById('setting-closeonplay')?.checked === true,
    theme: launcherState.theme
  };
}

// Drag Header Support
document.addEventListener('DOMContentLoaded', () => {
  const header = document.getElementById('window-header');
  if (header) {
    header.addEventListener('mousedown', (e) => {
      if (e.target.closest('button') || e.target.closest('input') || e.target.closest('a')) {
        return;
      }
      postMessage({ type: 'drag' });
    });
  }

  // Request initial data from C#
  postMessage({ type: 'get_init_data' });
});

// Tab Switcher
let previousTab = 'home';

function toggleSettingsTab() {
  const settingsView = document.getElementById('view-settings');
  if (settingsView && !settingsView.classList.contains('hidden')) {
    switchTab(previousTab || 'home');
  } else {
    const current = ['home', 'patch'].find(t => !document.getElementById(`view-${t}`)?.classList.contains('hidden'));
    if (current) previousTab = current;
    switchTab('settings');
  }
}

function switchTab(tab) {
  const views = ['home', 'patch', 'settings'];
  views.forEach(v => {
    const el = document.getElementById(`view-${v}`);
    const btn = document.getElementById(`tab-btn-${v}`);
    if (el) el.classList.add('hidden');
    if (btn) {
      btn.classList.remove('active');
    }
  });

  const headerSettingsBtn = document.getElementById('btn-header-settings');
  if (headerSettingsBtn) {
    if (tab === 'settings') {
      headerSettingsBtn.classList.add('active');
    } else {
      headerSettingsBtn.classList.remove('active');
    }
  }

  const activeView = document.getElementById(`view-${tab}`);
  const activeBtn = document.getElementById(`tab-btn-${tab}`);
  if (activeView) {
    activeView.classList.remove('hidden');
    // Re-trigger fadeInUp animation on tab content
    const animated = activeView.querySelectorAll('.animate-fadeInUp');
    animated.forEach(el => {
      el.classList.remove('animate-fadeInUp');
      requestAnimationFrame(() => el.classList.add('animate-fadeInUp'));
    });
  }
  if (activeBtn) {
    activeBtn.classList.add('active');
  }
}

// Handle Messages from C# WebView2
if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', (event) => {
    const data = event.data;
    handleHostMessage(data);
  });
}

function handleHostMessage(data) {
  switch (data.type) {
    case 'init_data':
      handleInitData(data);
      break;
    case 'status_update':
      updateStatus(data.text);
      break;
    case 'update_checked':
      handleUpdateChecked(data);
      break;
    case 'download_progress':
      handleDownloadProgress(data);
      break;
    case 'update_finished':
      handleUpdateFinished(data);
      break;
    case 'launcher_download_progress':
      handleLauncherDownloadProgress(data);
      break;
    case 'launcher_update_error':
      handleLauncherUpdateError(data);
      break;
    case 'settings_saved':
      handleSettingsSaved(data);
      break;
    case 'game_launched':
      updateStatus('Game đang khởi chạy...');
      break;
    case 'online_state':
      handleOnlineState(data);
      break;
    case 'online_result':
      handleOnlineResult(data);
      break;
  }
}

function handleInitData(data) {
  launcherState.settings = data.settings || {};
  launcherState.player = data.player || { name: 'Đạo hữu', realm: 'Luyện Khí' };
  launcherState.currentVersion = data.settings.currentVersion || '0.7.0';
  launcherState.launcherVersion = data.settings.launcherVersion || '1.0.0';

  // Ngôn ngữ đã lưu (có thể vừa đổi trong game) khác trang đang hiện → tải lại bằng ngôn ngữ đó
  const savedLang = data.settings.language === 'en' ? 'en' : 'vi';
  if (savedLang !== currentLang()) { window.setLang(savedLang); return; }

  // Apply saved theme
  applyTheme(data.settings.theme || 'dark');

  // Update Player Profile in Header
  document.getElementById('player-name-display').textContent = launcherState.player.name;
  document.getElementById('player-realm-display').textContent = launcherState.player.realm;
  renderOnlineState();

  // Update Version Badges
  document.getElementById('badge-current-version').textContent = 'v' + launcherState.currentVersion;
  document.getElementById('badge-launcher-version').textContent = 'v' + launcherState.launcherVersion;
  document.getElementById('header-launcher-ver').textContent = 'v' + launcherState.launcherVersion;
  document.getElementById('status-details').textContent = `Game v${launcherState.currentVersion} · Launcher v${launcherState.launcherVersion}`;
  document.getElementById('patch-tag-title').textContent = 'v' + launcherState.currentVersion;

  // Update Settings Inputs
  document.getElementById('setting-repo').value = launcherState.settings.gitHubRepo || 'xuankienmg/nguoi-luoi-tu-tien';
  document.getElementById('setting-path').value = launcherState.settings.gamePath || 'D:\\Game VanDao';
  document.getElementById('setting-version').value = launcherState.currentVersion;
  document.getElementById('setting-launcher-version').value = launcherState.launcherVersion;
  document.getElementById('setting-autocheck').checked = launcherState.settings.autoCheckOnStartup !== false;
  document.getElementById('setting-closeonplay').checked = launcherState.settings.closeLauncherOnPlay !== false;

  document.getElementById('footer-game-dir').textContent = launcherState.settings.gamePath || 'D:\\Game VanDao';
}

function updateStatus(text) {
  const statusTitle = document.getElementById('status-title');
  if (statusTitle) {
    statusTitle.innerHTML = `<span class="w-[5px] h-[5px] rounded-full animate-pulse" style="background: var(--gold-accent);"></span> ${text}`;
    statusTitle.className = 'font-semibold text-[11px] flex items-center gap-1.5';
    statusTitle.style.color = 'var(--text-primary)';
  }
}

/** Markdown tối giản của ghi chú GitHub → HTML an toàn: ###/#### tiêu đề, gạch đầu dòng, **đậm**, `code`.
 *  Bỏ "## Có gì mới" (thẻ đã có tiêu đề); link chỉ giữ chữ vì bấm vào sẽ mở ngay trong Launcher. */
function markdownToHtml(md) {
  const esc = s => s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const inline = s => esc(s)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\[([^\]]+)\]\([^)\s]+\)/g, '$1');
  const out = [];
  let list = null;
  const flush = () => { if (list) { out.push('<ul>' + list.join('') + '</ul>'); list = null; } };
  for (const raw of String(md).replace(/\r/g, '').split('\n')) {
    const line = raw.trim();
    let m;
    if (!line || /^##\s/.test(line)) { flush(); continue; }
    if ((m = line.match(/^#{1,6}\s+(.*)/))) { flush(); out.push('<h4>' + inline(m[1]) + '</h4>'); }
    else if ((m = line.match(/^[-*]\s+(.*)/))) (list ||= []).push('<li>' + inline(m[1]) + '</li>');
    else { flush(); out.push('<p>' + inline(line) + '</p>'); }
  }
  flush();
  return out.join('');
}

/** Ghi chú markdown → một dòng chữ trơn (cho thẻ tóm tắt). */
const markdownToText = md => String(md).replace(/\r/g, '').split('\n')
  .map(l => l.trim()).filter(l => l && !/^##\s/.test(l))
  .map(l => l.replace(/^#{1,6}\s+|^[-*]\s+/, '').replace(/\*\*(.+?)\*\*/g, '$1').replace(/`([^`]+)`/g, '$1').replace(/\[([^\]]+)\]\([^)\s]+\)/g, '$1'))
  .join(' · ');

function renderReleaseNotes(data) {
  const available = !!data.releaseTitle || data.changelog != null || !!data.publishedAt;
  const version = data.latestVersion;
  const title = available ? (data.releaseTitle || `Bản phát hành v${version}`) : 'Thông tin bản phát hành';
  const notes = available
    ? (data.changelog?.trim() || 'Bản phát hành này chưa có ghi chú cập nhật.')
    : (data.errorMessage ? 'Không thể tải thông tin cập nhật. Hãy kiểm tra kết nối và thử lại.' : 'Chưa có thông tin bản phát hành trên GitHub.');
  document.getElementById('patch-tag-title').textContent = available && version ? `v${version}` : 'Chưa có thông tin';
  const date = data.publishedAt ? new Date(data.publishedAt) : null;
  document.getElementById('patch-date-title').textContent = date && !Number.isNaN(date.getTime())
    ? date.toLocaleDateString('vi-VN') : '';
  const body = document.getElementById('patch-body-text');
  body.innerHTML = markdownToHtml(notes);
  body.classList.add('md-notes');
  body.classList.remove('whitespace-pre-wrap');
  document.getElementById('card-latest-title').textContent = title;
  document.getElementById('card-latest-tag').textContent = available && version ? `CẬP NHẬT v${version}` : 'Thông tin cập nhật';
  const plain = markdownToText(notes);
  document.getElementById('card-latest-desc').textContent = plain.length > 100 ? plain.substring(0, 100) + '…' : plain;
  document.getElementById('patch-badge-new').classList.toggle('hidden', !data.hasUpdate);
}

/** Ngày giờ phát hành theo giờ máy người chơi, vd "22/10/2026 · 14:30". */
function formatReleaseTime(value) {
  const date = value ? new Date(value) : null;
  if (!date || Number.isNaN(date.getTime())) return '';
  const pad = n => String(n).padStart(2, '0');
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} · ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Nhật ký cập nhật: mỗi bản phát hành gần nhất một thẻ (phiên bản, ngày giờ, tiêu đề, ghi chú). */
function renderReleaseList(data) {
  const list = document.getElementById('patch-notes-list');
  const releases = Array.isArray(data.recentReleases) ? data.recentReleases : [];
  if (!list || releases.length === 0) return;               // không có danh sách: giữ thẻ đơn của renderReleaseNotes
  const el = (tag, className, text, style) => {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (style) node.setAttribute('style', style);
    if (text != null) node.textContent = text;
    return node;
  };
  list.replaceChildren(...releases.map((release, index) => {
    const card = el('div', 'patch-card space-y-4');
    const head = el('div', 'flex items-center justify-between gap-3');
    const tags = el('div', 'flex items-center gap-2');
    tags.append(el('span', 'px-2.5 py-1 rounded font-bold text-[11px] tracking-wide', release.version ? `v${release.version}` : 'Bản phát hành',
      'background: rgba(229, 193, 88, 0.1); color: var(--gold-light); border: 1px solid rgba(229, 193, 88, 0.2);'));
    if (index === 0) tags.append(el('span', 'px-2 py-0.5 rounded text-[10px] font-bold tracking-wide uppercase', 'Mới nhất',
      'background: rgba(229, 193, 88, 0.18); color: var(--gold-accent);'));
    const time = formatReleaseTime(release.publishedAt);
    head.append(tags, el('span', 'text-[11px]', time ? `Phát hành ${time}` : '', 'color: var(--text-dimmer);'));
    const notes = el('div', 'md-notes text-[12px] leading-relaxed', null, 'color: var(--text-muted);');
    notes.innerHTML = markdownToHtml(release.changelog?.trim() || 'Bản phát hành này chưa có ghi chú cập nhật.');
    card.append(head,
      el('h3', 'text-sm font-semibold', release.title || `Bản phát hành v${release.version}`, 'color: var(--text-primary);'),
      notes);
    return card;
  }));
}

function handleUpdateChecked(data) {
  renderReleaseNotes(data);
  renderReleaseList(data);
  launcherState.currentVersion = data.currentVersion || launcherState.currentVersion;
  launcherState.launcherVersion = data.currentLauncherVersion || launcherState.launcherVersion;

  document.getElementById('badge-current-version').textContent = 'v' + launcherState.currentVersion;
  document.getElementById('badge-launcher-version').textContent = 'v' + launcherState.launcherVersion;
  document.getElementById('header-launcher-ver').textContent = 'v' + launcherState.launcherVersion;

  // 1. Kiểm tra cập nhật Launcher
  if (data.hasLauncherUpdate && data.latestLauncherVersion) {
    launcherState.hasLauncherUpdate = true;
    launcherState.latestLauncherVersion = data.latestLauncherVersion;
    launcherState.launcherDownloadUrl = data.launcherDownloadUrl;

    // Hiện pill ở header
    const headerBtn = document.getElementById('header-launcher-update-btn');
    headerBtn.classList.remove('hidden');
    document.getElementById('header-launcher-update-ver').textContent = 'v' + data.latestLauncherVersion;

    // Hiện banner trang chủ
    const banner = document.getElementById('launcher-update-banner');
    banner.classList.remove('hidden');
    document.getElementById('banner-launcher-ver').textContent = 'v' + data.latestLauncherVersion;

    // Hiện nút nâng cấp trong Settings
    document.getElementById('btn-launcher-update-settings').classList.remove('hidden');
  } else {
    launcherState.hasLauncherUpdate = false;
    document.getElementById('header-launcher-update-btn').classList.add('hidden');
    document.getElementById('launcher-update-banner').classList.add('hidden');
    document.getElementById('btn-launcher-update-settings').classList.add('hidden');
  }

  // 2. Kiểm tra cập nhật Game
  if (data.hasUpdate && data.latestVersion) {
    launcherState.status = 'update_available';
    launcherState.latestVersion = data.latestVersion;
    launcherState.downloadUrl = data.downloadUrl;

    // Show patch new badge
    document.getElementById('patch-badge-new').classList.remove('hidden');


    // Change Button to UPDATE GAME
    const btn = document.getElementById('main-action-btn');
    btn.className = 'riot-btn-update px-10 h-10 rounded-md font-black text-[14px] tracking-[0.15em] text-white uppercase title-font flex items-center justify-center gap-2';
    document.getElementById('btn-icon').textContent = '⬇';
    document.getElementById('btn-label').textContent = 'CẬP NHẬT';

    // Status Bar
    const statusTitle = document.getElementById('status-title');
    statusTitle.className = 'font-semibold text-[11px] flex items-center gap-1.5';
    statusTitle.style.color = 'var(--gold-accent)';
    statusTitle.innerHTML = `<span class="w-[5px] h-[5px] rounded-full animate-ping" style="background: var(--gold-accent);"></span> Phiên bản mới: v${data.latestVersion}`;
    document.getElementById('status-details').textContent = data.assetSize > 0 
      ? `${(data.assetSize / (1024 * 1024)).toFixed(1)} MB` 
      : 'GitHub Release';

    document.getElementById('download-speed').textContent = 'Sẵn sàng tải';
    document.getElementById('download-percent').textContent = '0%';
    document.getElementById('progress-bar-fill').style.width = '0%';
    document.getElementById('progress-bar-fill').className = 'progress-fill w-0';
    document.getElementById('progress-bar-fill').style.background = 'linear-gradient(90deg, var(--azure), #0284c7)';
  } else {
    launcherState.status = 'ready';
    setPlayReadyUI();
    const statusTitle = document.getElementById('status-title');
    statusTitle.className = 'font-semibold text-[11px] flex items-center gap-1.5';
    statusTitle.style.color = 'var(--text-primary)';
    statusTitle.innerHTML = `<span class="w-[5px] h-[5px] rounded-full" style="background: var(--gold-accent); box-shadow: 0 0 6px var(--gold-dim);"></span> ${data.statusText || 'Sẵn sàng'}`;
    document.getElementById('status-details').textContent = `Game v${launcherState.currentVersion} · Launcher v${launcherState.launcherVersion}`;
  }
}

function setPlayReadyUI() {
  const btn = document.getElementById('main-action-btn');
  btn.className = 'riot-btn-play px-10 h-10 rounded-md font-black text-[14px] tracking-[0.15em] text-white uppercase title-font flex items-center justify-center gap-2';
  btn.style.background = '';
  document.getElementById('btn-icon').textContent = '▶';
  document.getElementById('btn-label').textContent = 'CHƠI NGAY';

  const progressFill = document.getElementById('progress-bar-fill');
  progressFill.style.width = '100%';
  progressFill.className = 'progress-fill w-full';
  progressFill.style.background = 'linear-gradient(90deg, var(--gold-accent), var(--gold-light))';
  document.getElementById('download-speed').textContent = 'Sẵn sàng khởi chạy';
  document.getElementById('download-percent').textContent = '100%';
  document.getElementById('progress-glow').classList.add('hidden');
}

function handleDownloadProgress(data) {
  if (launcherState.status === 'ready') return;
  launcherState.status = 'downloading';
  const progressBar = document.getElementById('progress-bar-fill');
  progressBar.style.width = data.percent + '%';
  progressBar.style.background = 'linear-gradient(90deg, var(--azure), #38bdf8)';
  document.getElementById('download-percent').textContent = data.percent + '%';
  document.getElementById('download-speed').textContent = `${data.speed} MB/s`;

  const statusTitle = document.getElementById('status-title');
  statusTitle.className = 'font-semibold text-[11px] flex items-center gap-1.5';
  statusTitle.style.color = 'var(--gold-light)';
  statusTitle.innerHTML = `<span class="w-[5px] h-[5px] rounded-full animate-pulse" style="background: var(--gold-accent);"></span> ${data.status}`;
  document.getElementById('progress-glow').classList.remove('hidden');

  const btn = document.getElementById('main-action-btn');
  btn.className = 'cursor-not-allowed opacity-70 px-10 h-10 rounded-md font-black text-[14px] tracking-[0.15em] text-white uppercase title-font flex items-center justify-center gap-2';
  btn.style.background = 'var(--bg-subtle)';
  document.getElementById('btn-icon').textContent = '⏳';
  document.getElementById('btn-label').textContent = 'ĐANG TẢI...';
}

function handleUpdateFinished(data) {
  if (data.success) {
    launcherState.status = 'ready';
    launcherState.currentVersion = data.currentVersion;
    document.getElementById('badge-current-version').textContent = 'v' + data.currentVersion;
    document.getElementById('status-details').textContent = `Game v${launcherState.currentVersion} · Launcher v${launcherState.launcherVersion}`;
    document.getElementById('patch-badge-new').classList.add('hidden');
    postMessage({ type: 'check_update' });
    setPlayReadyUI();

    const statusTitle = document.getElementById('status-title');
    statusTitle.className = 'font-semibold text-[11px] flex items-center gap-1.5';
    statusTitle.style.color = 'var(--text-primary)';
    statusTitle.innerHTML = `<span class="w-[5px] h-[5px] rounded-full" style="background: var(--gold-accent); box-shadow: 0 0 6px var(--gold-dim);"></span> ${data.message}`;
  } else {
    launcherState.status = 'update_available';
    alert(data.message || 'Lỗi khi cập nhật game!');
    const btn = document.getElementById('main-action-btn');
    btn.className = 'riot-btn-update px-10 h-10 rounded-md font-black text-[14px] tracking-[0.15em] text-white uppercase title-font flex items-center justify-center gap-2';
    document.getElementById('btn-icon').textContent = '⬇';
    document.getElementById('btn-label').textContent = 'THỬ LẠI';
  }
}

// Launcher Update Handlers
function promptLauncherUpdate() {
  if (!launcherState.launcherDownloadUrl) {
    alert('Không tìm thấy đường link tải Launcher mới trong release.');
    return;
  }

  const ver = launcherState.latestLauncherVersion || 'Mới';
  if (!confirm(`Bạn có muốn tải và cập nhật Launcher lên phiên bản v${ver}? Launcher sẽ tự động khởi động lại sau khi cập nhật.`)) {
    return;
  }

  // Mở modal tiến trình Launcher update
  document.getElementById('launcher-update-modal').classList.remove('hidden');
  document.getElementById('launcher-modal-desc').textContent = `Đang tải bản cập nhật Launcher v${ver}...`;
  document.getElementById('launcher-modal-bar').style.width = '0%';

  postMessage({
    type: 'start_launcher_update',
    downloadUrl: launcherState.launcherDownloadUrl
  });
}

function handleLauncherDownloadProgress(data) {
  document.getElementById('launcher-modal-bar').style.width = data.percent + '%';
  document.getElementById('launcher-modal-percent').textContent = data.percent + '%';
  document.getElementById('launcher-modal-speed').textContent = `${data.speed} MB/s`;
  document.getElementById('launcher-modal-desc').textContent = data.status;
}

function handleLauncherUpdateError(data) {
  document.getElementById('launcher-update-modal').classList.add('hidden');
  alert(data.message || 'Lỗi khi cập nhật Launcher!');
}

// User Actions
function handleMainAction() {
  if (launcherState.status === 'ready') {
    if (onlineBlocksLaunch()) return;
    postMessage({ type: 'launch_game' });
  } else if (launcherState.status === 'update_available') {
    if (!launcherState.downloadUrl) {
      alert(`Đã phát hiện phiên bản mới: v${launcherState.latestVersion || ''}\n\nTuy nhiên gói cài đặt (.zip) chưa được đính kèm vào Release trên GitHub.\nVui lòng chờ tác giả tải file nén game (.zip) lên mục Assets của GitHub Releases để cập nhật tự động!`);
      return;
    }
    postMessage({
      type: 'start_update',
      downloadUrl: launcherState.downloadUrl,
      newVersion: launcherState.latestVersion
    });
  }
}

function checkUpdateNow() {
  updateStatus('Đang kiểm tra GitHub Releases (Game & Launcher)...');
  postMessage({ type: 'check_update' });
}

function browseGameFolder() {
  postMessage({ type: 'browse_folder' });
}

function saveSettings() {
  const newSettings = gatherCurrentSettings();
  postMessage({ type: 'save_settings', settings: newSettings });
}

function handleSettingsSaved(data) {
  launcherState.settings = data.settings;
  launcherState.currentVersion = data.settings.currentVersion;
  launcherState.launcherVersion = data.settings.launcherVersion;
  document.getElementById('footer-game-dir').textContent = data.settings.gamePath;
  document.getElementById('badge-current-version').textContent = 'v' + data.settings.currentVersion;
  document.getElementById('badge-launcher-version').textContent = 'v' + data.settings.launcherVersion;
  document.getElementById('header-launcher-ver').textContent = 'v' + data.settings.launcherVersion;
  document.getElementById('status-details').textContent = `Game v${launcherState.currentVersion} · Launcher v${launcherState.launcherVersion}`;

  // Apply theme from saved settings
  if (data.settings.theme) {
    applyTheme(data.settings.theme);
  }

  // Only show save confirmation if Settings tab is active (not theme toggle)
  const settingsView = document.getElementById('view-settings');
  if (settingsView && !settingsView.classList.contains('hidden')) {
    alert('Đã lưu cấu hình Launcher thành công!');
  }
}
