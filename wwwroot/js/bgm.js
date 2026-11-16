/* Nhạc nền, 2 bài phát lặp:
   - main   : audio/bgm-atlas-china.mp3 ("Atlas China" — vadim_makes_sound), màn chính
   - bicanh : audio/bgm-bicanh.mp3 ("Anime Opening – Emotional Journey" — alex-morgan), mọi màn Bí Cảnh (body.swarm-open)
   Desktop: Bí Cảnh là cửa sổ WebView riêng (MODE panel, tab=bicanh) → cửa sổ chính tạm dừng khi cửa sổ đó mở (BroadcastChannel).
   Các cửa sổ panel khác không phát. Bật/tắt + âm lượng lưu localStorage, đồng bộ giữa các cửa sổ.
   API: BGM.toggle(), BGM.setOn(bool), BGM.setVolume(0..1), BGM.on, BGM.volume */
(function (global) {
  'use strict';
  const KEY = 'van-dao-bgm';
  const SRC = { main: 'audio/bgm-atlas-china.mp3', bicanh: 'audio/bgm-bicanh.mp3' };
  const st = { on: true, vol: 0.25 };                       // mặc định nhỏ để không át tiếng game
  try { Object.assign(st, JSON.parse(localStorage.getItem(KEY) || '{}')); } catch (e) { }
  const save = () => { try { localStorage.setItem(KEY, JSON.stringify(st)); } catch (e) { } };
  const isMode = m => typeof MODE !== 'undefined' && MODE === m;
  const isRealmWin = () => isMode('panel') && typeof Q !== 'undefined' && Q.get('tab') === 'bicanh';
  const canPlay = () => !isMode('panel') || isRealmWin();
  const realmOpen = () => document.body.classList.contains('swarm-open');
  const audios = {};
  let winHidden = false;                                    // desktop: cửa sổ Bí Cảnh bị ẩn (C# gọi winShown)
  let armed = false, started = false, otherRealmAt = 0, lastTrack = null, lastBeat = 0, announced = false;
  let chan = null; try { chan = new BroadcastChannel('van-dao-bgm'); } catch (e) { }

  const audio = n => audios[n] || (audios[n] = Object.assign(new Audio(SRC[n]), { loop: true, preload: 'auto', volume: st.vol }));
  const hidden = () => document.hidden;

  /** Bài cần phát lúc này (null = im lặng). */
  function wanted() {
    if (!st.on || !canPlay() || hidden() || winHidden) return null;
    if (isRealmWin()) return realmOpen() ? 'bicanh' : null;           // cửa sổ Bí Cảnh: chỉ phát khi đang hiện màn
    if (realmOpen()) return 'bicanh';                                  // bản web: Bí Cảnh mở ngay trong trang chính
    if (Date.now() - otherRealmAt < 5000) return null;                 // cửa sổ Bí Cảnh đang phát (nhịp báo mỗi 2 giây)
    return 'main';
  }
  /** Nút loa nổi góc dưới phải, chỉ hiện khi đang ở màn Bí Cảnh: bật/tắt nhạc nền ngay trong màn. */
  let btn = null;
  const ICON_ON = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9v6h4l5 4V5L8 9H4z"/><path d="M16 9a4 4 0 0 1 0 6M18.5 6.5a8 8 0 0 1 0 11"/></svg>';
  const ICON_OFF = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9v6h4l5 4V5L8 9H4z"/><path d="M17 9l5 6M22 9l-5 6"/></svg>';
  function syncButton() {
    const show = canPlay() && realmOpen();
    if (!show) { if (btn) btn.hidden = true; return; }
    if (!btn) {
      btn = document.createElement('button'); btn.type = 'button'; btn.className = 'bgm-btn';
      btn.onclick = () => { global.BGM.toggle(); syncButton(); };
      document.body.appendChild(btn);
    }
    const label = st.on ? 'Tắt nhạc nền' : 'Bật nhạc nền';
    btn.hidden = false; btn.title = label; btn.setAttribute('aria-label', label); btn.setAttribute('aria-pressed', String(st.on));
    const html = st.on ? ICON_ON : ICON_OFF; if (btn.dataset.s !== String(st.on)) { btn.innerHTML = html; btn.dataset.s = String(st.on); }
  }
  function apply() {
    syncButton();
    const want = wanted();
    for (const n in SRC) {
      if (n !== want) { if (audios[n] && !audios[n].paused) audios[n].pause(); continue; }
      const a = audio(n); a.volume = st.vol;
      if (a.paused) {
        if (n === 'bicanh' && lastTrack !== 'bicanh') a.currentTime = 0;   // vào Bí Cảnh: bài riêng chạy từ đầu; nhạc chính thì tiếp tục đúng chỗ đang nghe dở
        a.play().catch(() => arm());                                   // trình duyệt chặn tự phát → đợi lần bấm đầu tiên
      }
    }
    if (want) lastTrack = want;
    // cửa sổ Bí Cảnh báo cho cửa sổ chính biết (nhịp 2 giây; báo tắt ngay khi đóng)
    const open = isRealmWin() && want === 'bicanh';
    if (chan && (open !== announced || (open && Date.now() - lastBeat > 2000))) { announced = open; lastBeat = Date.now(); chan.postMessage({ realm: open }); }
  }
  function arm() {
    if (armed) return; armed = true;
    const go = () => { armed = false; removeEventListener('pointerdown', go, true); removeEventListener('keydown', go, true); apply(); };
    addEventListener('pointerdown', go, true); addEventListener('keydown', go, true);
  }

  if (chan) chan.onmessage = e => {
    const m = e.data || {};
    if ('realm' in m) otherRealmAt = m.realm ? Date.now() : 0;
    if ('on' in m) st.on = !!m.on;
    if ('vol' in m) st.vol = m.vol;
    if (started) apply();
  };
  document.addEventListener('visibilitychange', () => { if (started) apply(); });

  global.BGM = {
    get on() { return st.on; },
    get volume() { return st.vol; },
    start() { if (started) return; started = true; setInterval(apply, 400); apply(); },
    setOn(v) { st.on = !!v; save(); chan?.postMessage({ on: st.on }); apply(); },
    toggle() { this.setOn(!st.on); return st.on; },
    /** Desktop: C# gọi khi cửa sổ Bí Cảnh ẩn/hiện. Ẩn → im ngay; hiện lại → bài Bí Cảnh chạy từ đầu. */
    winShown(v) {
      winHidden = !v;
      if (v) { lastTrack = null; if (audios.bicanh) audios.bicanh.currentTime = 0; }
      apply();
    },
    setVolume(v) {
      st.vol = Math.max(0, Math.min(1, +v || 0)); save();
      for (const n in audios) audios[n].volume = st.vol;
      chan?.postMessage({ vol: st.vol });
    },
  };
  // cửa sổ Bí Cảnh không chạy boot() của trang chính → tự khởi động
  if (isRealmWin()) global.BGM.start();
})(window);
