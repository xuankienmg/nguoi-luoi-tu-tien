// Trang Cập nhật: mọi bản phát hành từ releases.json (workflow deploy-site sinh ra lúc deploy), mới nhất trước.
// Mỗi bản có neo #vX.Y.Z để dẫn thẳng tới một phiên bản.
(() => {
  const { all, renderNotes, num, date, esc } = window.siteNotes;
  const L = window.siteLang || { lang: 'vi', t: k => k, onChange(){} };
  const list = document.getElementById('relList'), index = document.getElementById('verIndex');

  function entry(rel, i) {
    const el = document.createElement('article');
    el.className = 'release rel-entry';
    el.id = 'v' + rel.version;
    const head = document.createElement('div');
    head.className = 'release-head';
    const h2 = document.createElement('h2');
    h2.append(num('v' + rel.version));
    if (i === 0) h2.append(Object.assign(document.createElement('small'), { className: 'rel-badge', textContent: L.t('upLatest') }));
    const meta = Object.assign(document.createElement('p'), { className: 'release-meta', textContent: rel.published_at ? L.t('released', date(rel.published_at)) : '' });
    head.append(h2, meta);
    const notes = document.createElement('div');
    notes.className = 'release-notes';
    notes.innerHTML = renderNotes(rel.body || '') || `<p>${esc(L.t('upEmpty'))}</p>`;
    el.append(head, notes);
    return el;
  }

  function render(releases) {
    list.replaceChildren(...releases.map(entry));
    index.replaceChildren(...releases.map(rel => {
      const a = document.createElement('a');
      a.href = '#v' + rel.version;
      a.append(num('v' + rel.version));
      return a;
    }));
    // neo trong URL (vd. updates.html#v0.8.2): danh sách dựng sau khi tải xong nên cuộn lại tay
    if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView();
  }

  let current = null;
  L.onChange(() => { if (current) render(current); });
  all().then(releases => { current = releases; render(releases); })
    .catch(err => { list.innerHTML = `<p class="release-meta">${esc(L.t('upError'))}</p>`; console.warn('Không lấy được các bản phát hành:', err.message); });
})();
