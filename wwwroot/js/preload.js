/* js/preload.js: màn hình preload lúc mới vào game. Nạp đầu tiên để hiện sớm nhất; main.js (boot) báo từng bước rồi gọi done().
   Dải taskbar của bản desktop (?mode=strip) không có màn preload: cửa sổ C# tự hiện sau khung vẽ đầu tiên. */
const Preload = (() => {
  const box = document.getElementById('preload');
  const hidden = !box || /[?&]mode=strip\b/.test(location.search);
  if (box && hidden) box.remove();
  const bar = box?.querySelector('.pl-bar i'), msg = box?.querySelector('.pl-msg'), pct = box?.querySelector('.pl-pct');
  const MIN_SHOW_MS = 450;                                   // tránh nháy khi tải quá nhanh
  const started = Date.now();
  let target = 0, shown = 0, closed = hidden, crawl = null;
  const paint = () => { if (closed) return; bar.style.width = shown + '%'; pct.textContent = Math.round(shown) + '%'; };
  /** Báo một bước: nhãn hiển thị và mốc tiến độ (0–99); thanh chỉ tiến, và bò chậm tới trước mốc kế để không đứng im khi chờ mạng. */
  function step(label, to) {
    if (closed) return;
    target = Math.max(target, Math.min(99, to));
    shown = Math.max(shown, target);
    if (label) msg.textContent = label;
    paint();
    clearInterval(crawl);
    crawl = setInterval(() => { if (shown < Math.min(95, target + 12)) { shown += .6; paint(); } }, 400);
  }
  /** Tải trước các hình chính (hoặc quá ms thì bỏ qua); lỗi tải một hình không chặn vào game. */
  function images(urls, ms = 5000) {
    const list = [...new Set((urls || []).filter(Boolean))];
    const loads = list.map(src => new Promise(done => { const im = new Image(); im.onload = im.onerror = done; im.src = src; }));
    return Promise.race([Promise.all(loads), new Promise(done => setTimeout(done, ms))]);
  }
  /** Tắt màn preload (xong, hoặc có lỗi cần hiện thông báo phía sau). Gọi nhiều lần vẫn an toàn. */
  function done() {
    if (closed) return;
    closed = true; clearInterval(crawl);
    bar.style.width = '100%'; pct.textContent = '100%';
    setTimeout(() => {
      box.classList.add('pl-done');
      setTimeout(() => box.remove(), 600);
    }, Math.max(0, MIN_SHOW_MS - (Date.now() - started)));
  }
  if (!closed) setTimeout(done, 45000);                      // chốt chặn: không bao giờ che màn hình quá 45 giây
  return { step, images, done, hide: done };
})();
