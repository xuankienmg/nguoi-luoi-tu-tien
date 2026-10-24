// Điền nút tải và ghi chú phát hành từ release.json (workflow deploy-site sinh ra lúc deploy).
// Thiếu hoặc lỗi thì hỏi thẳng GitHub API (notes.js); vẫn lỗi thì nút giữ link /releases/latest có sẵn trong HTML.
(() => {
  const { latest, renderNotes, num, date } = window.siteNotes;
  const mb = n => Math.round(n / 1048576) + ' MB';
  const L = window.siteLang || { lang: 'vi', t: k => k, onChange(){} };

  function apply(rel) {
    const dl = document.getElementById('dl');
    dl.href = rel.zip_url;
    const main = dl.querySelector('.seal-main');
    main.textContent = L.t('download') + ' ';
    main.append(num('v' + rel.version));
    document.getElementById('dlMeta').textContent = (rel.size ? mb(rel.size) + ', ' : '') + 'Windows 64-bit';
    if (rel.published_at) document.getElementById('dlNote').textContent = L.t('freeReleased', date(rel.published_at));
    document.getElementById('relVersion').replaceChildren(num('v' + rel.version));
    const meta = document.getElementById('relMeta');
    meta.textContent = rel.published_at ? L.t('released', date(rel.published_at)) : '';
    if (rel.body) document.getElementById('relNotes').innerHTML = renderNotes(rel.body);
  }

  // đổi ngôn ngữ: lang.js trả chữ gốc của nút tải / ghi chú, nên điền lại thông tin bản phát hành
  let current = null;
  L.onChange(() => { if (current) apply(current); });
  latest().then(rel => { current = rel; apply(rel); }).catch(err => console.warn('Không lấy được bản phát hành:', err.message));
})();
