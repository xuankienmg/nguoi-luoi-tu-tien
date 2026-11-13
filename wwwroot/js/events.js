/* Bảng Sự kiện (tab "event"): danh sách sự kiện bên trái, nội dung sự kiện đang chọn bên phải.
   Server quyết định mọi trạng thái (S.dailyLogin, S.event); file này chỉ hiển thị và gửi lệnh nhận quà. */
(function () {
  let sel = 'login', autoShownFor = '';
  const loginReady = d => !!d && d.online && !d.claimedToday && !d.blocked;
  const dqReady = () => typeof dailyQuestReady === 'function' ? dailyQuestReady(S.dailyQuest) : 0;   // js/daily-quest.js
  const eventReady = ev => ev?.active ? (ev.milestones || []).filter(m => m.ready).length : 0;
  const leftText = ms => {
    const s = Math.max(0, Math.floor((ms - Date.now()) / 1000)), h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60);
    return h ? `${h} giờ ${m} phút` : `${m} phút`;
  };
  const tip = k => `${ITEM[k]?.name || k}${ITEM[k]?.desc ? '&#10;' + ITEM[k].desc : ''}`;

  /** Danh sách sự kiện: thêm sự kiện mới = thêm một mục ở đây (id, tên, mô tả ngắn, có quà chờ, nội dung). */
  const EVENTS = [
    {id: 'login', name: 'Điểm danh', icon: 'calendar',
      sub: () => { const d = S.dailyLogin; return d ? `Ngày ${d.today + 1}/7 · ${d.claimedToday ? 'đã nhận' : 'chưa nhận'}` : ''; },
      ready: () => loginReady(S.dailyLogin), badge: () => 'Nhận', body: loginHtml},
    {id: 'quest', name: 'Nhiệm vụ ngày', icon: 'scroll',   // nội dung: js/daily-quest.js
      sub: () => { const q = S.dailyQuest; return q ? `Hoạt Lực ${q.points}/${q.maxPoints} · tuần ${fmt(q.weekPoints)}` : 'Mở sau nhiệm vụ tân thủ'; },
      ready: () => dqReady() > 0, badge: () => `${dqReady()} rương`,
      body: () => dailyQuestHtml(S.dailyQuest, {cell, fmt, clock, warn: warnHtml, busy})},
    {id: 'thienco', name: 'Thiên Cơ', icon: 'gift',
      sub: () => S.event?.active ? `Tuần ${trackName(S.event.track)} · còn ${dayLeft(S.event.endsAt)}` : 'Chưa mở',
      ready: () => eventReady(S.event) > 0, badge: () => `${eventReady(S.event)} quà`, body: thienCoHtml},
    {id: 'spin', name: 'Vòng quay', icon: 'wheel',
      sub: () => `Vé: ${fmt(S.inventory?.ve_tai_xiu || 0)} · Linh Thú: ${fmt(S.inventory?.ve_linh_thu || 0)}`,
      ready: () => false, badge: () => '', body: () => ''},
  ];
  const CHECK = '<svg class="ev-check" viewBox="0 0 24 24" aria-hidden="true"><path d="m5 12 5 5 9-10"/></svg>';
  const CLOCK = '<svg class="ev-clock" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>';
  const ICON = {
    wheel: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="2"/><path d="M12 3.5V10M12 14v6.5M3.5 12H10M14 12h6.5M6 6l4.6 4.6M13.4 13.4 18 18M18 6l-4.6 4.6M10.6 13.4 6 18"/></svg>',
    calendar: '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="5" width="16" height="15" rx="1.5"/><path d="M4 10h16M8 3v4M16 3v4M9 15l2 2 4-4"/></svg>',
    scroll: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 4h11a2 2 0 0 1 0 4H7z"/><path d="M7 4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10V8"/><path d="M9 12h6M9 16h4"/></svg>',
    gift: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 10h16v10H4z"/><path d="M3 7h18v3H3zM12 7v13"/><path d="M12 7c-1.5-3-5-3.5-5-1.2C7 7 9.5 7 12 7zm0 0c1.5-3 5-3.5 5-1.2C17 7 14.5 7 12 7z"/></svg>',
  };

  const clock = ms => {   // đếm ngược hh:mm:ss
    const s = Math.max(0, Math.floor((ms - Date.now()) / 1000)), p = n => String(n).padStart(2, '0');
    return `${p(Math.floor(s / 3600))}:${p(Math.floor(s % 3600 / 60))}:${p(s % 60)}`;
  };
  const dayLeft = ms => {
    const s = Math.max(0, Math.floor((ms - Date.now()) / 1000)), d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600);
    return d ? `${d} ngày ${h} giờ` : leftText(ms);
  };
  const trackName = t => t === 'pet' ? 'Linh Thú' : 'Trang Bị';
  /** Ô vật phẩm: icon + số lượng góc dưới phải, rê chuột xem tên và mô tả. */
  const cell = (r, cls = '') => `<span class="ev-cell ${cls}" data-tip="${tip(r.item)}">${itemImg(r.item)}${r.qty > 1 ? `<em>×${fmt(r.qty)}</em>` : ''}</span>`;
  const firstLine = k => (ITEM[k]?.desc || '').split(/(?<=\.)\s/)[0].replace(/\.$/, '');
  const warnHtml = (online, blocked, what) => !online ? `<p class="ev-warn">Mất kết nối máy chủ: cần online để nhận ${what}.</p>`
    : blocked ? `<p class="ev-warn">${blocked}</p>` : '';

  // ---------- Điểm danh ----------
  function loginHtml() {
    const d = S.dailyLogin; if (!d) return '';
    const n = d.days.length, got = d.claimedToday ? d.today + 1 : d.today;   // số ô đã nhận trong vòng này
    const cards = d.days.map((rw, i) => {
      const done = i < d.today || (i === d.today && d.claimedToday), now = i === d.today && !d.claimedToday, big = i === n - 1;
      const foot = done ? `<i class="dl-foot">${CHECK}Đã nhận</i>`
        : now ? `<button type="button" class="dl-foot dl-claim" data-dl-claim ${loginReady(d) && !busy ? '' : 'disabled'}>Nhận quà</button>`
        : `<i class="dl-foot">${big ? 'Quà lớn' : `Sau ${i - d.today} ngày`}</i>`;
      return `<li class="dl-day${done ? ' done' : ''}${now ? ' today' : ''}${big ? ' big' : ''}">
        <span class="dl-n">NGÀY ${i + 1}</span>
        <span class="dl-rw">${rw.map(r => cell(r)).join('')}</span>
        ${foot}</li>`;
    }).join('');
    const rules = [
      ['Mỗi ngày một ô quà', 'Vào game và bấm nhận. Ngày mới bắt đầu lúc 00:00 giờ Việt Nam.'],
      ['Bỏ lỡ không mất tiến độ', 'Hôm sau đăng nhập vẫn nhận tiếp ô kế tiếp.'],
      ['Ngày 7 là quà lớn', 'Nhận xong ngày 7 thì quay lại ngày 1.'],
      ['Cần online', 'Quà nhận vào thẳng Hành trang; cần đủ ô trống.'],
    ].map(([t, x], i) => `${i + 1}. ${t}: ${x}`).join('&#10;');
    return `<header class="ev-head"><div><small>ĐIỂM DANH · VÒNG 7 NGÀY</small><h2>Quà đăng nhập hằng ngày <i class="ev-q" data-tip="Cách hoạt động&#10;${rules}">?</i></h2></div>
        <span class="ev-pill">${CLOCK}<span>Ngày mới sau</span><b data-ev-clock>${clock(d.resetAt)}</b></span></header>
      ${warnHtml(d.online, d.blocked, 'quà đăng nhập')}
      <ol class="dl-days">${cards}</ol>
      <div class="ev-prog"><span class="ev-bar"><i style="width:${got / n * 100}%"></i></span><span>Đã nhận <b>${got} / ${n}</b></span></div>
`;
  }

  // ---------- Thiên Cơ ----------
  function thienCoHtml() {
    const ev = S.event;
    const head = sub => `<header class="ev-head"><div><small>SỰ KIỆN TUẦN</small><h2>Thiên Cơ</h2></div>${sub}</header>`;
    if (!ev?.active) {
      const next = ev?.nextStartsAt ? `Mở sau <b>${dayLeft(ev.nextStartsAt)}</b> · tuần ${trackName(ev.nextTrack)}` : 'Hiện chưa có sự kiện.';
      return head('') + `<section class="ev-card"><p class="ev-empty">${next}</p></section>`;
    }
    const ms = ev.milestones || [], pts = ev.points, last = ms.length - 1;
    const next = ms.find(m => pts < m.points), ready = ms.filter(m => m.ready).length;
    const off = busy || !ev.online || !!ev.blocked;
    // phần tô giữa mốc i-1 và i (0..1); trước mốc đầu tính từ 0
    const seg = i => { const a = i <= 0 ? 0 : ms[i - 1].points, b = ms[i].points; return Math.max(0, Math.min(1, (pts - a) / (b - a))); };
    const cols = ms.map((m, i) => {
      const reached = pts >= m.points, st = m.claimed ? 'done' : m.ready ? 'ready' : 'locked';
      const inFill = i === 0 ? 0 : Math.max(0, seg(i) * 2 - 1), outFill = i === last ? 0 : Math.min(1, seg(i + 1) * 2);
      const action = m.claimed ? `<i class="tc-st ok">Đã nhận</i>`
        : m.ready ? `<button type="button" class="tc-claim" data-ev-claim="${i}" ${off ? 'disabled' : ''}>Nhận</button>`
        : `<i class="tc-st">còn ${fmt(m.points - pts)}</i>`;
      return `<li class="tc-col ${st}${i === last ? ' last' : ''}">
        <span class="tc-node">${i ? `<span class="tc-line in"><i style="width:${inFill * 100}%"></i></span>` : ''}${i < last ? `<span class="tc-line out"><i style="width:${outFill * 100}%"></i></span>` : ''}
          <span class="tc-dot">${m.claimed ? CHECK : i + 1}</span></span>
        <b class="tc-pts${reached ? ' on' : ''}">${fmt(m.points)}</b>
        <div class="tc-card">${m.rewards.map(r => cell(r)).join('')}<div class="tc-act">${action}</div></div></li>`;
    }).join('');
    const fin = ms[last]?.rewards?.[0];
    const finHtml = fin ? `<section class="ev-card gold tc-fin" aria-label="Quà mốc cuối">${cell(fin, 'lg')}
        <div><small>MỐC ${fmt(ms[last].points)} · ${(ITEM[fin.item]?.name || '').toUpperCase()}</small><p>${ITEM[fin.item]?.desc || ''}</p></div></section>` : '';
    const weeks = `<div class="tc-weeks"><span class="tc-week on"><small>TUẦN NÀY</small><b>${trackName(ev.track)}</b> · còn ${dayLeft(ev.endsAt)}</span>
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg>
        <span class="tc-week"><small>TUẦN SAU</small><b>${trackName(ev.nextTrack)}</b></span></div>`;
    return head(weeks) + warnHtml(ev.online, ev.blocked, 'quà sự kiện') + `
      <section class="ev-card tc-score" aria-label="Điểm tuần này"><span class="tc-big"><b class="ev-serif">${fmt(pts)}</b> điểm</span>
        <span class="tc-how"><span>Mỗi lượt <b>${ev.track === 'pet' ? 'Quay Linh Thú' : 'quay trang bị'}</b> = 1 điểm</span>
          <small>${next ? `Còn <b>${fmt(next.points - pts)}</b> điểm tới mốc ${fmt(next.points)}` : 'Đã đạt mọi mốc'}</small></span>
        <button type="button" class="ev-sub-btn" data-ev-spin="${ev.track === 'pet' ? 'pet' : 'gear'}">${ICON.wheel}Đến Vòng quay</button></section>
      <div class="tc-road-wrap"><ol class="tc-road" aria-label="Mốc quà">${cols}</ol></div>
      <div class="tc-bottom">${finHtml}
        <div class="tc-all"><button type="button" class="ev-main-btn" data-ev-claim="-1" ${ready && !off ? '' : 'disabled'}>${ready ? `Nhận tất cả (${ready} mốc)` : 'Chưa có mốc chờ nhận'}</button>
          <small>Hết tuần, quà đã đạt mà chưa nhận tự gửi vào Hành trang.</small></div></div>`;
  }

  // ---------- khung chính ----------
  // Vòng quay giữ nguyên giao diện của spin.js: chuyển hẳn #pane-spin vào khung phải khi chọn, trả về chỗ cũ khi rời.
  const spinHome = $('pane-spin')?.parentElement;
  function render() {
    const pane = $('pane-event'); if (!pane || !S) return;
    if (!EVENTS.some(e => e.id === sel)) sel = EVENTS[0].id;
    const cur = EVENTS.find(e => e.id === sel);
    if (!pane.querySelector('.ev-shell'))
      pane.innerHTML = '<div class="ev-shell"><nav class="ev-list" role="tablist" aria-label="Danh sách sự kiện"></nav><section class="ev-body" role="tabpanel"></section></div>';
    const nav = pane.querySelector('.ev-list'), body = pane.querySelector('.ev-body'), spin = $('pane-spin');
    const list = EVENTS.map(e => `<button type="button" role="tab" class="ev-item" data-ev-sel="${e.id}" aria-selected="${e.id === sel}">
      <span class="ev-ic">${ICON[e.icon]}</span><span class="ev-txt"><b>${e.name}</b><small>${e.sub()}</small></span>${e.ready() ? `<span class="ev-badge">${e.badge()}</span>` : ''}</button>`).join('');
    if (nav.dataset.h !== list) { nav.dataset.h = list; nav.innerHTML = list; }
    body.classList.toggle('ev-body-spin', cur.id === 'spin');
    if (cur.id === 'spin') {
      if (spin) {
        if (spin.parentElement !== body) { body.dataset.h = ''; body.replaceChildren(spin); }
        // openTab ẩn các pane khác, kể cả pane-spin đã nằm trong bảng Sự kiện.
        // Luôn khôi phục hiển thị và cập nhật vé/lượt quay khi mở lại bảng.
        spin.hidden = false;
        renderSpin();
      }
      return;
    }
    if (spin && spin.parentElement === body) { spin.hidden = true; spinHome?.appendChild(spin); }
    const html = cur.body(), key = html.replace(/(data-ev-(?:clock|left)>)[^<]*/g, '$1');   // đồng hồ chạy không bắt vẽ lại
    if (body.dataset.h !== key) { body.dataset.h = key; body.innerHTML = html; }
  }
  PANE_RENDER.event = render;
  /** Chọn mục sự kiện (main.js: openTab('spin') → bảng Sự kiện, mục Vòng quay). */
  window.selectEvent = id => { if (EVENTS.some(e => e.id === id)) { sel = id; paneDirty.add('event'); } };   // openTab vẽ lại bảng

  function toast(day, rw) {
    const el = $('craftToast'); if (!el || !rw?.length) return;
    const list = rw.filter(r => ITEM[r.item]).map(r => `<span>${itemImg(r.item)}<b class="rc-${ITEM[r.item].rarity}">${ITEM[r.item].name}</b> × ${fmt(r.qty)}</span>`).join('');
    el.classList.remove('brief', 'fast');
    el.innerHTML = `<span class="ct-ic">${itemImg(rw[0].item)}</span><span class="ct-t">Nhận quà đăng nhập</span>
      <span class="ct-o">Ngày <b>${day + 1}</b> · đã vào Hành trang</span><span class="ct-u sp-ev-got">${list}</span>`;
    el.hidden = true; void el.offsetWidth; el.hidden = false;
    clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 3600);
  }

  $('pane-event')?.addEventListener('click', async e => {
    if (e.target.closest('#pane-spin')) return;   // nút trong Vòng quay do spin.js xử lý (tránh nhận quà Thiên Cơ 2 lần)
    const b = e.target.closest('button'); if (!b || b.disabled) return;
    if (b.dataset.evSel) { sel = b.dataset.evSel; render(); return; }
    if (b.dataset.evSpin) { spinMode = b.dataset.evSpin; spinModePicked = true; spinKey = ''; sel = 'spin'; render(); return; }
    if (b.dataset.dlClaim !== undefined) {
      b.disabled = true;
      const d = S.dailyLogin, rw = d.days[d.today];
      if (await act('login/claim')) toast(d.today, rw);
      // Khi yêu cầu thất bại, HTML không đổi nhưng nút vừa bị khóa thủ công.
      $('pane-event').querySelector('.ev-body').dataset.h = '';
      render(); button(); return;
    }
    if (b.dataset.dqGo) {
      const t = b.dataset.dqGo;
      if (t === 'event') { sel = 'login'; render(); }
      else if (t === 'arena') { selectRankTab('arena'); openTab('rank', true); }
      else openTab(t, true);
      return;
    }
    if (b.dataset.dqClaim !== undefined || b.dataset.wqClaim !== undefined) {
      const weekly = b.dataset.wqClaim !== undefined, i = +(weekly ? b.dataset.wqClaim : b.dataset.dqClaim);
      b.disabled = true;
      await act(`${weekly ? 'weekly' : 'daily'}-quest/claim?i=${i}`);
      $('pane-event').querySelector('.ev-body').dataset.h = '';
      render(); button(); return;
    }
    if (b.dataset.evClaim !== undefined) {
      const i = +b.dataset.evClaim, pick = (S.event?.milestones || []).filter((m, j) => m.ready && (i < 0 || j === i));
      b.disabled = true;
      if (await act(`event/claim?i=${i}`) && typeof spinEventToast === 'function') spinEventToast(pick);
      render(); button();
    }
  });

  /** Mở bảng Sự kiện: cửa sổ desktop riêng (như Thiên bảng) hoặc tab trong bản web. */
  function open(id) {
    if (id) sel = id;
    if (window.chrome?.webview && MODE !== 'panel') notifyHost('open-panel', {tab: 'event'});
    else openTab('event');
  }
  window.openEvents = open;

  /** Nút Sự kiện: chấm đỏ khi có sự kiện còn quà chờ nhận. */
  function button() {
    const b = $('btnLogin'); if (!b || !S) return;
    const ready = EVENTS.filter(e => e.ready()).length;
    const t = ready ? 'Sự kiện\nCó quà chờ nhận' : 'Sự kiện\nĐiểm danh · Nhiệm vụ · Thiên Cơ';
    if (b.dataset.k === `${ready}|${t}`) return;
    b.dataset.k = `${ready}|${t}`; b.hidden = false; b.classList.toggle('ready', ready > 0); b.dataset.tip = t;
  }

  window.renderDailyLogin = function () {
    button();
    // Bản web: mở bảng Sự kiện ở mục Điểm danh một lần mỗi ngày nếu còn quà (không đè hộp chọn khởi đầu).
    const d = S?.dailyLogin, key = `${S?.id}|${d?.resetAt}`;
    if (MODE === 'full' && loginReady(d) && autoShownFor !== key && !S.starterChoice?.required) { autoShownFor = key; open('login'); }
  };
  if ($('btnLogin')) $('btnLogin').onclick = () => open();
  setInterval(() => button(), 30000);
  setInterval(() => {   // đồng hồ đếm ngược: chỉ đổi chữ, không vẽ lại bảng
    if (!S?.dailyLogin) return;
    const c = document.querySelector('#pane-event:not([hidden]) [data-ev-clock]'); if (c) c.textContent = clock(S.dailyLogin.resetAt);
    const s = document.querySelector('#pane-event:not([hidden]) [data-ev-left]'); if (s) s.textContent = leftText(S.dailyLogin.resetAt);
  }, 1000);
})();
