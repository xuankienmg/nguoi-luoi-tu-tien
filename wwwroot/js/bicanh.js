/* Vấn Đạo · js/bicanh.js: Thái Hư Bí Cảnh — leo 15 tầng, chọn cửa / cổ vật. Mọi logic ở server (GameService.BiCanh.cs); file này chỉ vẽ S.biCanh. */
(function () {
  const ICON = { fight: '⚔️', elite: '💀', event: '🔮', shop: '🏮', spring: '🧘', boss: '👹' };
  const END = { dead: 'Bại trận', retreat: 'Rút lui', clear: 'Chinh phục Thái Hư' };
  const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const pct = x => Math.round(Math.max(0, x) * 100);
  let root, busy = false, msg = '', msgKind = '', lastHtml = '', askLeave = false;
  const paint = html => { html = `<div class="bc-panel" role="document">${html}</div>`; if (html !== lastHtml) { const keep = root.querySelector('.bc-panel')?.scrollTop || 0; lastHtml = html; root.innerHTML = html; root.querySelector('.bc-panel').scrollTop = keep; } };   // cửa sổ nổi dùng chung với Yêu Triều

  async function send(path) {
    if (busy || typeof S === 'undefined' || !S) return;
    busy = true; render();
    try {
      const res = await api('POST', `/api/players/${S.id}/bicanh/${path}`);
      msg = res.message || ''; msgKind = res.kind || '';
      setState(res.state);
      if (path.startsWith('door')) { const fs = S.biCanh?.run?.fights || []; if (fs.length) showDuel(fs.length - 1, msg); }   // trận vừa đánh: panel thông tin trận chiến (js/duel.js, như Lôi đài / Boss tông)
    } catch (e) { msg = e.message; msgKind = 'bad'; }
    finally { busy = false; render(); }
  }

  function showDuel(i, message) {
    const f = S.biCanh?.run?.fights?.[i];
    if (f?.duel && typeof duelShow === 'function') duelShow(f.duel, { win: f.win, title: 'Thái Hư Bí Cảnh', message: message || '' });
  }

  function hpBar(run) {
    const now = Math.round(Number(run.maxHp) * run.hp);
    return `<div class="bc-hp" title="Máu giữ qua các tầng"><i style="width:${pct(run.hp)}%"></i><span>${fmt(now)} / ${fmt(Number(run.maxHp))} (${pct(run.hp)}%)</span></div>`;
  }
  function track(run) {
    let h = '';
    for (let f = 1; f <= run.maxFloor; f++) {
      const cls = [f % 5 === 0 ? 'boss' : '', f <= run.cleared ? 'done' : '', f === run.floor && run.phase !== 'over' ? 'cur' : ''].join(' ');
      h += `<span class="bc-dot ${cls}" title="Tầng ${f}">${f % 5 === 0 ? '👹' : f}</span>`;
    }
    return `<div class="bc-track">${h}</div>`;
  }
  const relicCard = (r, attr) => `<button class="bc-card bc-relic" ${attr}><span class="bc-ico">${r.icon}</span><b>${esc(r.name)}</b><em>${esc(r.set)}</em><p>${esc(r.text)}</p></button>`;

  function fights(run) {
    if (!run.fights?.length) return '';
    return `<div class="bc-fights">${run.fights.map((f, i) => `
      <div class="bc-fight ${f.win ? 'win' : 'lose'}">
        <img src="${esc(f.image)}" alt="">
        <div><b>${esc(f.name)}</b>${f.note ? ` <em class="bc-mech">${esc(f.note)}</em>` : ''}
          <span>${f.win ? 'Thắng' : 'Bại'} · ${f.turns} lượt · ${f.crits} đòn bạo</span>
          <span>Máu ${pct(f.hpBefore)}% → ${pct(f.hpAfter)}%${f.healed > 0 ? ` (cổ vật hồi ${pct(f.healed)}%)` : ''}</span></div>
        ${f.duel ? `<button class="bc-btn ghost bc-duel" data-duel="${i}">Xem diễn biến</button>` : ''}
      </div>`).join('')}</div>`;
  }

  function main(run) {
    if (run.phase === 'doors') return `<h3>Tầng ${run.floor}: chọn một cửa</h3><div class="bc-cards">${run.doors.map((d, i) => `
      <button class="bc-card bc-door k-${d.kind}" data-door="${i}">
        <span class="bc-ico">${ICON[d.kind] || '❔'}</span><b>${esc(d.name)}</b>
        ${d.image ? `<img src="${esc(d.image)}" alt="">` : ''}${d.monster ? `<em>${esc(d.monster)}</em>` : ''}
        <p>${esc(d.hint)}</p>${d.mechanic ? `<p class="bc-mech">${esc(d.mechanic)}</p>` : ''}
      </button>`).join('')}</div>`;
    if (run.phase === 'relic') return `<h3>Chọn một cổ vật (chỉ hiệu lực trong lượt này)</h3>
      <div class="bc-cards">${run.offer.map((r, i) => relicCard(r, `data-relic="${i}"`)).join('')}</div>
      <button class="bc-btn ghost" data-relic="-1">Bỏ qua</button>`;
    if (run.phase === 'event' && run.event) return `<h3>🔮 ${esc(run.event.title)}</h3><p class="bc-story">${esc(run.event.text)}</p>
      <div class="bc-choices">${run.event.choices.map((c, i) => `<button class="bc-btn" data-event="${i}">${esc(c.label)}${c.chance < 100 ? ` <small>${c.chance}%</small>` : ''}</button>`).join('')}</div>`;
    if (run.phase === 'shop') return `<h3>🏮 Thương nhân Thái Hư</h3><p class="bc-story">"Hư Thạch đổi bảo vật, đạo hữu cứ xem."</p>
      <div class="bc-cards">${run.shop.map((s, i) => `<button class="bc-card bc-relic" data-shop="${i}" ${s.bought || run.stones < s.cost ? 'disabled' : ''}>
        <span class="bc-ico">${s.icon}</span><b>${esc(s.name)}</b><p>${esc(s.text)}</p><em>${s.bought ? 'Đã mua' : `${s.cost} Hư Thạch`}</em></button>`).join('')}</div>
      <button class="bc-btn ghost" data-shop="-1">Rời đi</button>`;
    if (run.phase === 'over') return `<h3>${END[run.end] || 'Kết thúc'} · qua ${run.cleared}/${run.maxFloor} tầng</h3>
      ${rewards(run, 'Đã nhận')}<button class="bc-btn gold" data-close>Rời bí cảnh</button>`;
    return '';
  }
  function rewards(run, title) {
    if (!run.rewards?.length) return `<p class="bc-note">${title}: chưa có (qua ít nhất 1 tầng mới có thưởng).</p>`;
    return `<div class="bc-rw"><span>${title}:</span>${run.rewards.filter(r => ITEM[r.item]).map(r => `<span class="bc-rwi">${itemImg(r.item)}<b>${fmt(Number(r.qty))}</b> ${esc(ITEM[r.item].name)}</span>`).join('')}</div>`;
  }

  function side(run) {
    const relics = run.relics.length ? run.relics.map(r => `<li title="${esc(r.text)}"><span>${r.icon}</span>${esc(r.name)}<small>${esc(r.text)}</small></li>`).join('') : '<li class="bc-note">Chưa có cổ vật</li>';
    const sets = run.sets.map(s => `<li class="${s.active ? 'on' : ''}">Bộ ${esc(s.name)} ${s.have}/${s.need}: ${esc(s.text)}</li>`).join('');
    return `<aside class="bc-side">
      <h4>Cổ vật</h4><ul class="bc-relics">${relics}</ul>${sets ? `<ul class="bc-sets">${sets}</ul>` : ''}
      <p class="bc-note">Công ${run.atkBonus >= 0 ? '+' : ''}${run.atkBonus}% · Giảm sát thương ${run.reduce}%</p>
      <h4>Nhật ký</h4><ol class="bc-log">${run.log.slice().reverse().map(l => `<li>${esc(l)}</li>`).join('')}</ol>
    </aside>`;
  }

  function intro(bc) {
    const left = bc.unlimited ? 1 : bc.limit - bc.used;
    return `<div class="bc-intro">
      <p>Hư không vỡ thành 15 tầng, mỗi tầng 2–3 cửa: <b>⚔️ quái</b>, <b>💀 tinh anh</b>, <b>🔮 kỳ ngộ</b>, <b>🏮 thương nhân</b>, <b>🧘 linh tuyền</b>. Boss trấn giữ tầng 5, 10, 15.</p>
      <ul><li>Trận tự đánh; <b>máu giữ qua các tầng</b>, không tự hồi.</li>
        <li>Hạ tinh anh / Boss được chọn <b>cổ vật</b>; gom đủ bộ để kích hoạt combo.</li>
        <li>Chết hoặc rút lui vẫn giữ thưởng các tầng đã qua: linh thạch, Tinh Thiết, đan, tinh thạch; qua tầng 15 có Rương hiếm.</li></ul>
      <p class="bc-note">${bc.unlimited ? `Admin: không giới hạn lượt · Tầng sâu nhất ${bc.best}` : `Lượt hôm nay ${bc.used}/${bc.limit} · Tầng sâu nhất ${bc.best}`}</p>
      <button class="bc-btn gold" data-enter ${left <= 0 ? 'disabled' : ''}>${left <= 0 ? 'Hết lượt hôm nay' : 'Tiến vào'}</button>
    </div>`;
  }

  function render() {
    if (!root || root.hidden) return;
    const bc = typeof S !== 'undefined' && S && S.biCanh, run = bc && bc.run;
    const head = `<header class="bc-head"><h2>Thái Hư Bí Cảnh</h2>${run ? `<span class="bc-floor">Tầng ${Math.min(run.floor, run.maxFloor)}/${run.maxFloor}</span><span class="bc-stone">💎 ${run.stones} Hư Thạch</span>` : ''}
      <button class="bc-x" data-x aria-label="Đóng"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg></button></header>`;
    const note = msg ? `<p class="bc-msg ${msgKind}">${esc(msg)}</p>` : '';
    if (!bc) return paint(head + '<p class="bc-note">Cần đăng nhập.</p>');
    if (!run) return paint(head + note + intro(bc));
    paint(head + `${hpBar(run)}${track(run)}${note}
      <div class="bc-body ${busy ? 'busy' : ''}"><section class="bc-main">${fights(run)}${main(run)}
        ${run.phase !== 'over' ? `<div class="bc-foot">${rewards(run, 'Rút lui bây giờ nhận')}${askLeave ? `<div class="bc-confirm"><span>Rút lui khỏi Thái Hư? Nhận thưởng các tầng đã qua, lượt này kết thúc.</span><button class="bc-btn gold" data-leave-ok>Rút lui</button><button class="bc-btn ghost" data-leave-no>Ở lại</button></div>` : `<button class="bc-btn ghost" data-leave>Rút lui</button>`}</div>` : ''}
      </section>${side(run)}</div>`);
  }

  function onClick(e) {
    if (e.target === root && !busy) return close();   // bấm ra ngoài cửa sổ nổi
    const b = e.target.closest('button'); if (!b || b.disabled || busy) return;
    const d = b.dataset;
    if (d.x !== undefined) return close();
    if (d.enter !== undefined) { msg = ''; return send('enter'); }
    if (d.door !== undefined) return send(`door?i=${d.door}`);
    if (d.relic !== undefined) return send(`relic?i=${d.relic}`);
    if (d.event !== undefined) return send(`event?c=${d.event}`);
    if (d.shop !== undefined) return send(`shop?i=${d.shop}`);
    if (d.duel !== undefined) return showDuel(+d.duel, '');
    if (d.leave !== undefined) { askLeave = true; return render(); }   // xác nhận ngay trong màn (không dùng hộp thoại của trình duyệt)
    if (d.leaveNo !== undefined) { askLeave = false; return render(); }
    if (d.leaveOk !== undefined) { askLeave = false; return send('leave'); }
    if (d.close !== undefined) { msg = ''; return send('close'); }
  }

  function open() {
    if (!root) {
      root = document.createElement('div'); root.className = 'bicanh'; root.hidden = true;
      root.setAttribute('role', 'dialog'); root.setAttribute('aria-label', 'Thái Hư Bí Cảnh');
      root.addEventListener('click', onClick);
      window.addEventListener('keydown', e => { if (root.hidden) return; e.stopPropagation(); if (e.key === 'Escape') { close(); e.preventDefault(); } }, true);
      document.body.appendChild(root);
    }
    msg = ''; askLeave = false; root.hidden = false; document.body.classList.add('swarm-open'); render();
  }
  function close() { if (root) root.hidden = true; document.body.classList.remove('swarm-open'); window.SwarmGame?.realms?.(); }   // đóng cửa sổ: quay lại màn chọn Bí Cảnh

  // vẽ lại khi state đổi (lệnh khác, đồng bộ online) trong lúc đang mở
  const prevRender = window.renderAll;
  if (typeof prevRender === 'function') window.renderAll = function () { const r = prevRender.apply(this, arguments); render(); return r; };
  window.BiCanh = { open, close, render };
})();
