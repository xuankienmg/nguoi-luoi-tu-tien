/* Vấn Đạo · js/notify.js: thông báo nổi (hợp thành, rương, bị lùi ải), đi tới Boss. Tách từ index.html (tools/split_index.py). */
function showCraft(c){
  const el = $('craftToast'), out = ITEM[c.output.item]; if (!el || !out) return;
  const chest = c.recipe.startsWith('chest:');
  if (!chest){
    const fail = c.recipe === 'fuse:fail';
    el.innerHTML = `<span class="ct-ic">${itemImg(c.output.item)}</span><span class="ct-t ${fail ? 'bad' : ''}">${fail ? 'Thất bại' : 'Thành công'}</span>`;
    el.classList.add('fast', 'brief');
    el.hidden = true; void el.offsetWidth; el.hidden = false;
    clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 1000);
    return;
  }
  el.classList.remove('brief');
  const used = c.inputs.map(u => `<span>${ITEM[u.item] ? itemImg(u.item) : ''}−${fmt(u.qty)} ${ITEM[u.item]?.name || u.item}</span>`).join('');
  const outs = c.outputs?.length ? c.outputs : [c.output];          // mở nhiều rương: nhiều món nhận được
  const got = outs.map(o => `<b class="rc-${ITEM[o.item]?.rarity}">${ITEM[o.item]?.name || o.item}</b> × ${fmt(o.qty)}`).join(' · ');
  el.innerHTML = `<span class="ct-ic">${itemImg(c.output.item)}</span><span class="ct-t">Mở rương</span>
    <span class="ct-o">Nhận ${got}</span><span class="ct-u">${used}</span>`;
  const ms = outs.length > 1 ? 2400 : 1400;                         // mở rương: thông báo ngắn (hay mở liên tục)
  el.classList.toggle('fast', outs.length === 1);
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, ms);
}

// Thông báo nhận rương (rơi khi diệt yêu / hạ Boss): rương quý nhất lên đầu
/** Thông báo nổi: thua ở ải cao nhất nên bị lùi một ải. */
function showKnockback(from, to){
  $('craftToast')?.classList.remove('brief');
  const el = $('craftToast'); if (!el) return;
  const R = S.map.realm + 1, top = Math.min(S.map.areaMax, mapAreaCount(S.map));
  el.innerHTML = `<span class="ct-ic" style="display:grid;place-items:center;font-size:26px;color:#ff9a6a">⚑</span><span class="ct-t">Thua ở ải ${R}-${from}</span>
    <span class="ct-o">Lùi về ải ${R}-${to} · ải cao nhất vẫn là ${R}-${top}</span><span class="ct-u"><span>Mở Bản đồ, bấm ải có cờ ⚑ để thử lại</span></span>`;
  el.classList.add('fast');
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 2600);
}
function showChestDrop(got){
  $('craftToast')?.classList.remove('brief');
  const el = $('craftToast'); if (!el || !ITEM[got[0][0]]) return;
  const list = got.map(([k, n]) => `<b class="rc-${ITEM[k].rarity}">${ITEM[k].name}</b> × ${fmt(n)}`).join(' · ');
  el.innerHTML = `<span class="ct-ic">${itemImg(got[0][0])}</span><span class="ct-t">Nhận rương</span>
    <span class="ct-o">${list}</span><span class="ct-u"><span>Nhấp đúp trong Hành trang để mở</span></span>`;
  el.classList.add('fast');
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 1800);
}

async function ddBrew(k, n){
  if (busy || (n > 0 && brewMax(brewOf(k)) < n)) return;
  const before = S.inventory[k] || 0;
  await act(`brew/${k}?n=${n}`);
  if ((S.inventory[k] || 0) > before) ddAnim = {t0: performance.now(), item: k};
}
/** Đến Boss rơi nguyên liệu đột phá của cảnh giới hiện tại: chuyển sang chương của Boss đó (nếu đã mở), đủ lệnh thì vào ải Boss. */
async function ddGoBoss(){
  const c = bossChapterFor(realm().bossItem), difficulty = bossDifficultyFor(realm().bossItem);
  if (c && difficulty !== (S.map.diff || 0)) {
    if (!await act(`diff/${difficulty}`)) { openTab('map'); return; }
  }
  if (!c || c.index > S.map.chapterMax){ openTab('map'); return; }
  if (S.map.realm !== c.index) await act(`map/${c.index}`);
  const m = S.map;
  if (m.areaMax >= mapAreaCount(S.map) && ((S.inventory[m.keyItem] || 0) > 0 || adminBoss())) await act(`area/${mapAreaCount(S.map) + 1}`);
  openTab('map');
}
function initDan(){
  $('pane-craft').addEventListener('click', e => {
    const p = e.target.closest('[data-pill]');
    if (p){ ddSel = p.dataset.pill; try { localStorage.setItem('van-dao-dd-sel', ddSel); } catch(err){} renderCraft(); return; }
    const bw = e.target.closest('[data-brew]');
    if (bw){ if (!bw.disabled) ddBrew(bw.dataset.brew, +bw.dataset.n); return; }
    const b = e.target.closest('[data-dd]'); if (!b || b.disabled) return;
    const id = b.dataset.dd;
    if (id === 'brew') ddBrew(ddCur(),1);
    else if (id === 'ten') ddBrew(ddCur(),10);
    else if (id === 'all') ddBrew(ddCur(),0);
    else if (id === 'advance') openTab('req');
    else if (id === 'boss') ddGoBoss();
  });
  addEventListener('keydown', e => {
    if ($('pane-craft').hidden || e.ctrlKey || e.metaKey || e.altKey || /input|textarea/i.test(e.target.tagName)) return;
    if (e.key === 'Enter' && !e.target.closest('button')){ e.preventDefault(); if (!busy && brewMax(brewOf(ddCur())) > 0) ddBrew(ddCur(),1); }
  });
}
