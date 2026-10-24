// Ghi chú phát hành dùng chung cho trang chủ (site.js) và trang Cập nhật (updates.js):
// đọc release.json / releases.json (workflow deploy-site sinh ra lúc deploy), dựng Markdown tối giản.
(() => {
  const REPO = 'xuankienmg/nguoi-luoi-tu-tien';
  const isGameZip = a => /^VanDao_v\d+\.\d+\.\d+\.zip$/i.test(a.name) && a.state === 'uploaded';
  const L = () => window.siteLang || { lang: 'vi', t: k => k, onChange(){} };

  /** Các bản phát hành có ZIP game, mới nhất trước. Thiếu file của site thì hỏi thẳng GitHub API. */
  async function fromGitHub(limit) {
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=${limit}`, { headers: { Accept: 'application/vnd.github+json' } });
    if (!r.ok) throw new Error('GitHub API ' + r.status);
    const list = [];
    for (const rel of await r.json()) {
      if (rel.draft || rel.prerelease) continue;
      const zip = (rel.assets || []).find(isGameZip);
      if (!zip) continue;
      list.push({
        tag: rel.tag_name, version: rel.tag_name.replace(/^v/i, ''), published_at: rel.published_at,
        zip_url: zip.browser_download_url, size: zip.size, sha256: (zip.digest || '').replace(/^sha256:/, ''),
        body: rel.body || ''
      });
    }
    if (!list.length) throw new Error('Chưa có bản phát hành kèm ZIP');
    return list;
  }

  async function siteJson(name) {
    const r = await fetch(name, { cache: 'no-cache' });
    if (!r.ok) throw new Error(name + ' ' + r.status);
    return r.json();
  }

  // Cormorant Garamond chỉ có số kiểu cổ (0 trông như o), nên số phiên bản dùng font thân bài
  const num = t => Object.assign(document.createElement('span'), { className: 'num', textContent: t });
  const date = s => new Date(s).toLocaleDateString(L().lang === 'en' ? 'en-GB' : 'vi-VN', { day: 'numeric', month: L().lang === 'en' ? 'short' : 'numeric', year: 'numeric' });
  const esc = s => s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // Markdown tối giản cho ghi chú GitHub: ##/### tiêu đề, gạch đầu dòng, **đậm**, [link](https://…).
  // Trang không dẫn sang GitHub: link GitHub chỉ giữ chữ, URL GitHub trơn thì bỏ.
  function inline(s) {
    return esc(s)
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\[([^\]]+)\]\((https:\/\/[^)\s]+)\)/g, (_, text, url) => /^https:\/\/([^/]+\.)?github\.com\//.test(url) ? text : `<a href="${url}">${text}</a>`)
      .replace(/(^|\s)https:\/\/github\.com\/[^\s<]+/g, '$1');
  }
  // Bản cũ dùng ghi chú GitHub tự sinh: bỏ "by @ai in <PR>", dòng so sánh / Full Changelog và lời chào người đóng góp mới.
  const skip = line => /^(\*\*)?Full Changelog|^So sánh:|made their first contribution/i.test(line);
  const tidy = s => s.replace(/\s+by @[\w-]+ in https:\/\/github\.com\/\S+$/, '');
  function renderNotes(md) {
    const out = []; let list = null, group = null;
    const open = () => { if (!group) { group = []; } };
    const flushList = () => { if (list) { group.push('<ul>' + list.join('') + '</ul>'); list = null; } };
    const flushGroup = () => { flushList(); if (group && group.length) out.push('<div>' + group.join('') + '</div>'); group = null; };
    for (const raw of md.replace(/\r/g, '').split('\n')) {
      const line = raw.trim();
      if (!line || /^##\s/.test(line) && !/^###/.test(line) || skip(line)) continue; // bỏ "## Có gì mới": trang đã có tiêu đề
      let m;
      if ((m = line.match(/^#{3,}\s+(.*)/))) { flushGroup(); open(); group.push('<h4>' + inline(m[1]) + '</h4>'); }
      else if ((m = line.match(/^[-*]\s+(.*)/))) { open(); (list ||= []).push('<li>' + inline(tidy(m[1])) + '</li>'); }
      else { open(); flushList(); group.push('<p>' + inline(line) + '</p>'); }
    }
    flushGroup();
    return out.join('');
  }

  window.siteNotes = {
    /** Bản mới nhất (trang chủ). */
    latest: () => siteJson('release.json').then(j => { if (!j.zip_url) throw new Error('release.json thiếu zip_url'); return j; })
      .catch(() => fromGitHub(10).then(list => list[0])),
    /** Mọi bản phát hành, mới nhất trước (trang Cập nhật). */
    all: () => siteJson('releases.json').then(j => { if (!Array.isArray(j) || !j.length) throw new Error('releases.json rỗng'); return j; })
      .catch(() => fromGitHub(100)),
    renderNotes, num, date, esc,
  };
})();
