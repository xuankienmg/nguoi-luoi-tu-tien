/* Nhiệm vụ tân thủ: thẻ nhỏ trên màn hình chính. Server quyết định trạng thái (S.newbieQuest); client chỉ hiển thị. */
(function () {
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const itemName = id => (typeof ITEM !== 'undefined' && ITEM[id]?.name) || id;
  let card = null, lastKey = '';
  // Người chơi bấm × thì thu thẻ thành nút nhỏ (nhớ trên máy); bấm nút nhỏ để mở lại.
  const HIDE_KEY = 'van-dao-nq-min';
  let collapsed = false;
  try { collapsed = localStorage.getItem(HIDE_KEY) === '1'; } catch (e) {}
  const setCollapsed = on => { collapsed = on; try { on ? localStorage.setItem(HIDE_KEY, '1') : localStorage.removeItem(HIDE_KEY); } catch (e) {} lastKey = ''; window.renderNewbieQuest(); };

  window.newbieQuestVisible = s => !!s?.newbieQuest && !s.starterChoice?.required;

  // Chữ cố định nằm trong <span> riêng để i18n.js dịch được (không dính số).
  window.newbieQuestHtml = function (q, mode, min) {
    const n = `${q.step + 1}/${q.total}`;
    if (min && mode !== 'strip')
      return `<button type="button" class="nq-pill" data-nq="show" aria-label="Mở nhiệm vụ tân thủ"><span>Nhiệm vụ tân thủ</span> ${n}${q.canClaim ? ' ✓' : ''}</button>`;
    if (mode === 'strip')
      return `<button type="button" class="nq-line" data-nq="open"><span>Nhiệm vụ</span> ${n} · <span>${esc(q.title)}</span>${q.canClaim ? ' ✓' : ''}</button>`;
    const rewards = q.rewards.map(r => `${fmt(r.qty)} <span>${esc(itemName(r.item))}</span>`).join(' · ');
    const go = q.tab && !q.canClaim ? `<button type="button" data-nq="go"><span>Đi tới</span></button>` : '';
    const claim = q.canClaim ? `<button type="button" class="nq-claim" data-nq="claim"><span>Nhận thưởng</span></button>` : '';
    const pct = Math.round(100 * Math.min(q.progress, q.need) / q.need);
    return `<header><small><span>Nhiệm vụ tân thủ</span> ${n}</small><h2>${esc(q.title)}</h2></header>`
      + `<button type="button" class="nq-x" data-nq="hide" aria-label="Thu gọn" title="Thu gọn">×</button>`
      + `<p class="nq-hint">${esc(q.hint)}</p>`
      + `<div class="nq-progress" role="progressbar" aria-label="Tiến độ" aria-valuemin="0" aria-valuemax="${q.need}" aria-valuenow="${q.progress}"><i style="width:${pct}%"></i></div>`
      + `<p class="nq-reward"><span>Thưởng:</span> ${rewards}</p>`
      + `<div class="nq-actions">${go}${claim}</div>`;
  };

  async function onClick(e) {
    const b = e.target.closest('[data-nq]'), q = S?.newbieQuest;
    if (!b || !q) return;
    const tab = q.tab || 'req';
    if (b.dataset.nq === 'hide') setCollapsed(true);
    else if (b.dataset.nq === 'show') setCollapsed(false);
    else if (b.dataset.nq === 'go') openTab(tab, true);
    else if (b.dataset.nq === 'claim') await act('newbie-quest/claim');
    else if (b.dataset.nq === 'open')
      window.chrome?.webview ? notifyHost('open-panel', {tab}) : window.open(`?mode=panel&tab=${encodeURIComponent(tab)}`, 'van-dao-quest', 'width=960,height=760');
  }

  window.renderNewbieQuest = function () {
    if (!window.newbieQuestVisible(S)) { card?.remove(); card = null; lastKey = ''; return; }
    const q = S.newbieQuest, key = [q.step, q.progress, q.canClaim, MODE, collapsed].join('|');
    if (!card) {
      card = document.createElement('section');
      card.id = 'newbieQuest'; card.className = 'nq-card';
      card.setAttribute('aria-label', 'Nhiệm vụ tân thủ'); card.setAttribute('aria-live', 'polite');
      card.addEventListener('click', onClick);
      document.body.appendChild(card);
    }
    if (key === lastKey) return;
    lastKey = key;
    card.classList.toggle('nq-strip', MODE === 'strip');
    card.classList.toggle('nq-ready', q.canClaim);
    card.classList.toggle('nq-min', collapsed && MODE !== 'strip');
    card.innerHTML = window.newbieQuestHtml(q, MODE, collapsed);
  };
})();
