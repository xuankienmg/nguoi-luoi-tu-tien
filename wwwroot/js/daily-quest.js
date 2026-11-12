/* Vấn Đạo · js/daily-quest.js: nội dung mục "Nhiệm vụ ngày" trong bảng Sự kiện (events.js).
   Server quyết định mọi trạng thái (S.dailyQuest); file này chỉ dựng HTML. Thao tác nút do events.js xử lý:
   data-dq-claim = rương ngày, data-wq-claim = rương tuần, data-dq-go = tab cần mở.
   Bố cục: "Lò Hoạt Lực" (cung 270° với 5 mốc rương) + danh sách việc, dải tuần 4 dấu ấn bên dưới. */
(function () {
  const reachable = (cs, pts) => (cs || []).filter(c => !c.claimed && pts >= c.points).length;
  /** Số rương đang chờ nhận (0 khi offline / bị chặn / chưa mở khóa). */
  window.dailyQuestReady = q => q && q.online && !q.blocked ? reachable(q.chests, q.points) + reachable(q.weekChests, q.weekPoints) : 0;

  const stateOf = (c, pts) => c.claimed ? 'done' : pts >= c.points ? 'ready' : 'locked';
  const CHECK = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m5 12 5 5 9-10"/></svg>';

  // Cung lò: bắt đầu 135° (dưới trái), quét 270° theo chiều kim đồng hồ tới 45° (dưới phải); tâm (100,100), bán kính 80.
  const R = 80, A0 = 135, SWEEP = 270;
  const pt = f => { const a = (A0 + SWEEP * f) * Math.PI / 180; return [100 + R * Math.cos(a), 100 + R * Math.sin(a)]; };
  const ARC = (() => { const [x0, y0] = pt(0), [x1, y1] = pt(1); return `M${x0.toFixed(1)} ${y0.toFixed(1)} A${R} ${R} 0 1 1 ${x1.toFixed(1)} ${y1.toFixed(1)}`; })();

  /** Lò Hoạt Lực: cung tiến độ tới mốc cuối, mỗi mốc rương là một nút trên cung. */
  function furnace(q, fmt) {
    const top = q.chests.length ? q.chests[q.chests.length - 1].points : 100;
    const fill = Math.min(1, q.points / top);
    const next = q.chests.find(c => q.points < c.points);
    const nodes = q.chests.map(c => {
      const [x, y] = pt(c.points / top), st = stateOf(c, q.points);
      return `<g class="dq-node ${st}" transform="translate(${x.toFixed(1)} ${y.toFixed(1)})"><circle r="13"/><text dy="4">${c.points}</text></g>`;
    }).join('');
    const caption = next ? `Còn <b>${fmt(next.points - q.points)}</b> tới rương ${next.points}` : 'Đã đủ mọi mốc hôm nay';
    return `<figure class="dq-furnace" role="img" aria-label="Hoạt Lực hôm nay ${q.points} trên ${top}">
      <svg viewBox="0 0 200 200" aria-hidden="true">
        <path class="dq-arc-bg" d="${ARC}" pathLength="100"/>
        <path class="dq-arc" d="${ARC}" pathLength="100" style="stroke-dasharray:${(fill * 100).toFixed(2)} 100"/>
        ${nodes}
      </svg>
      <figcaption><b class="dq-num">${fmt(q.points)}</b><span>Hoạt Lực hôm nay</span><small>${caption}</small></figcaption>
    </figure>`;
  }

  const action = (c, pts, kind, i, off) => c.claimed ? `<i class="dq-st ok">${CHECK}Đã nhận</i>`
    : pts >= c.points ? `<button type="button" class="dq-claim" data-${kind}-claim="${i}" ${off ? 'disabled' : ''}>Nhận</button>`
    : `<i class="dq-st">còn ${pts < c.points ? c.points - pts : 0}</i>`;

  /** Danh sách rương ngày (dưới lò): mốc · quà · thao tác. */
  const chestList = (q, off, cell) => `<ol class="dq-chests" aria-label="Rương ngày">${q.chests.map((c, i) =>
    `<li class="dq-chest ${stateOf(c, q.points)}"><b class="dq-mark">${c.points}</b>
      <span class="dq-rw">${c.rewards.map(r => cell(r)).join('')}</span>${action(c, q.points, 'dq', i, off)}</li>`).join('')}</ol>`;

  /** Việc trong ngày: giữ thứ tự cố định để thành thói quen. */
  const taskList = (q, fmt) => `<ul class="dq-tasks" aria-label="Việc trong ngày">${q.tasks.map(t => {
    const done = t.progress >= t.need;
    const tail = done ? `<i class="dq-st ok">${CHECK}Xong</i>`
      : t.tab ? `<button type="button" class="dq-go" data-dq-go="${t.tab}">Đi tới</button>` : '';
    return `<li class="dq-task${done ? ' done' : ''}">
      <span class="dq-tick" aria-hidden="true">${done ? CHECK : ''}</span>
      <span class="dq-t"><b>${t.title}</b>${t.need > 1 ? `<span class="dq-prog"><span class="dq-meter"><i style="width:${t.progress / t.need * 100}%"></i></span><small>${fmt(t.progress)}/${fmt(t.need)}</small></span>` : ''}</span>
      <span class="dq-pt">+${t.points}</span>${tail}</li>`;
  }).join('')}</ul>`;

  /** Dải tuần: 4 dấu ấn trên một đường; đoạn tô theo điểm tuần. */
  function weekTrack(q, off, cell, fmt) {
    const cs = q.weekChests, top = cs.length ? cs[cs.length - 1].points : 1;
    const stops = cs.map((c, i) => `<li class="dq-seal ${stateOf(c, q.weekPoints)}" style="--at:${c.points / top}">
        <span class="dq-stamp">${c.points}</span>
        <span class="dq-rw">${c.rewards.map(r => cell(r)).join('')}</span>${action(c, q.weekPoints, 'wq', i, off)}</li>`).join('');
    return `<section class="dq-week" aria-label="Rương tuần">
      <p class="dq-week-h"><b class="dq-num sm">${fmt(q.weekPoints)}</b> Hoạt Lực tuần này <small>cộng dồn từ mỗi ngày, làm mới sáng thứ Hai</small></p>
      <div class="dq-rail"><i style="width:${Math.min(100, q.weekPoints / top * 100)}%"></i></div>
      <ol class="dq-seals">${stops}</ol></section>`;
  }

  /** HTML mục Nhiệm vụ ngày. h = {cell, fmt, clock, warn, busy}. */
  window.dailyQuestHtml = function (q, h) {
    if (!q) return `<header class="ev-head"><div><h2>Nhiệm vụ ngày</h2></div></header>
      <section class="ev-card dq-locked"><p class="ev-empty">Hoàn thành nhiệm vụ tân thủ để mở nhiệm vụ ngày.</p></section>`;
    const off = h.busy || !q.online || !!q.blocked;
    const rules = [
      'Việc tự hoàn thành khi đủ số lần; Hoạt Lực cộng ngay.',
      'Ngày mới bắt đầu lúc 00:00 giờ Việt Nam; tuần mới bắt đầu thứ Hai.',
      'Hoạt Lực mỗi ngày cộng dồn vào điểm tuần.',
      'Quà tăng theo cảnh giới; cần online và đủ ô trống Hành trang.',
    ].map((x, i) => `${i + 1}. ${x}`).join('&#10;');
    return `<header class="ev-head"><div><h2>Nhiệm vụ ngày <i class="ev-q" data-tip="Cách hoạt động&#10;${rules}">?</i></h2></div>
        <span class="ev-pill"><span>Ngày mới sau</span><b data-ev-clock>${h.clock(q.resetAt)}</b></span></header>
      ${h.warn(q.online, q.blocked, 'rương nhiệm vụ')}
      <div class="dq-main">
        <section class="dq-left">${furnace(q, h.fmt)}${chestList(q, off, h.cell)}</section>
        ${taskList(q, h.fmt)}
      </div>
      ${weekTrack(q, off, h.cell, h.fmt)}`;
  };
})();
