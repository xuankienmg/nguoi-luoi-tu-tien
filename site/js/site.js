// Điền nút tải và ghi chú phát hành từ release.json (workflow deploy-site sinh ra lúc deploy).
// Thiếu hoặc lỗi thì hỏi thẳng GitHub API (notes.js); vẫn lỗi thì nút giữ link /releases/latest có sẵn trong HTML.
// Thêm: tab ảnh trong phần Lối chơi, thang cảnh giới và bảng chương đọc từ data/wiki.json (dữ liệu wiki).
(() => {
  const { latest, renderNotes, num, date } = window.siteNotes;
  const mb = n => Math.round(n / 1048576) + ' MB';
  const L = window.siteLang || { lang: 'vi', t: k => k, onChange(){} };
  const $ = id => document.getElementById(id);

  function setButton(a, rel) {
    if (!a) return;
    a.href = rel.zip_url;
    const main = a.querySelector('.seal-main');
    main.textContent = L.t('download') + ' ';
    main.append(num('v' + rel.version));
  }

  function apply(rel) {
    setButton($('dl'), rel);
    setButton($('dl2'), rel);
    $('dlMeta').textContent = (rel.size ? mb(rel.size) + ' · ' : '') + 'Windows 10/11 64-bit';
    if (rel.published_at) $('dlNote').textContent = L.t('freeReleased', date(rel.published_at));
    $('relVersion').replaceChildren(num('v' + rel.version));
    $('relMeta').textContent = rel.published_at ? L.t('released', date(rel.published_at)) : '';
    if (rel.body) $('relNotes').innerHTML = renderNotes(rel.body);
  }

  // đổi ngôn ngữ: lang.js trả chữ gốc của nút tải / ghi chú, nên điền lại thông tin bản phát hành
  let current = null;
  L.onChange(() => { if (current) apply(current); });
  latest().then(rel => { current = rel; apply(rel); }).catch(err => console.warn('Không lấy được bản phát hành:', err.message));

  // ---- Tab ảnh (Thú điển / Ấp trứng, Hành trang / Bán trang bị) ----
  for (const box of document.querySelectorAll('[data-tabs]')) {
    const tabs = [...box.querySelectorAll('[role="tab"]')];
    const select = t => {
      for (const x of tabs) {
        const on = x === t;
        x.setAttribute('aria-selected', String(on));
        x.tabIndex = on ? 0 : -1;
        $(x.getAttribute('aria-controls')).hidden = !on;
      }
    };
    tabs.forEach((t, i) => {
      t.addEventListener('click', () => select(t));
      t.addEventListener('keydown', e => {
        const d = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0;
        if (!d) return;
        e.preventDefault();
        const n = tabs[(i + d + tabs.length) % tabs.length];
        select(n); n.focus();
      });
    });
  }

  // ---- Thang cảnh giới + bảng chương ----
  const ladder = $('ladder'), rows = $('chapterRows');
  if (!ladder || !rows) return;
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  let realms = [], chapters = [], picked = 5;

  function stagesText(r) {
    const s = r.stages || [];
    return s.length > 3 ? s[0] + ' – ' + s[s.length - 1] : s.join(' · ');
  }
  function showRealm(i) {
    picked = i;
    const r = realms[i];
    ladder.querySelectorAll('button').forEach((b, j) => {
      b.setAttribute('aria-pressed', String(j === i));
      b.classList.toggle('past', j < i);
    });
    const mine = chapters.filter(c => c.realm === r.name);
    const soon = L.lang === 'en' ? 'Coming soon' : 'Sắp mở';
    $('realmNum').textContent = (L.lang === 'en' ? 'Realm ' : 'Cảnh giới ') + (i + 1) + ' / ' + realms.length;
    $('realmName').textContent = r.name;
    $('realmDesc').textContent = r.desc || '';
    $('realmStages').textContent = stagesText(r);
    $('realmChapters').textContent = mine.length ? mine.map(c => c.name).join(' · ') : soon;
    $('realmLairs').textContent = mine.length ? mine.map(c => c.bossLair).join(' · ') : soon;
  }

  fetch('data/wiki.json').then(r => { if (!r.ok) throw new Error(r.status); return r.json(); }).then(d => {
    realms = d.realms || [];
    chapters = (d.chapters || []).filter(c => c.enabled !== false);
    if (!realms.length) throw new Error('Thiếu cảnh giới');
    picked = Math.min(picked, realms.length - 1);
    ladder.innerHTML = realms.map((r, i) => `<button type="button" aria-pressed="false"><span class="dot"></span><span class="nm">${esc(r.name)}</span></button>`).join('');
    ladder.querySelectorAll('button').forEach((b, i) => b.addEventListener('click', () => showRealm(i)));
    rows.innerHTML = chapters.map((c, i) => `<tr><td class="num">${String(i + 1).padStart(2, '0')}</td><td>${esc(c.name)}</td><td>${esc(c.realm)}</td><td>${esc(c.bossLair)}</td></tr>`).join('');
    showRealm(picked);
    L.onChange(() => showRealm(picked));
  }).catch(err => {
    $('realmCard').hidden = true;
    rows.closest('table').hidden = true;
    console.warn('Không đọc được dữ liệu wiki:', err.message);
  });
})();
