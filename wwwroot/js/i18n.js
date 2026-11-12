/* js/i18n.js: chuyển ngôn ngữ giao diện (Tiếng Việt ↔ English).
   Game viết bằng tiếng Việt; khi chọn English, lớp này dịch mọi chữ hiển thị trong DOM (kể cả chữ server gửi về)
   và chữ vẽ lên canvas theo từ điển js/i18n-en.js. Logic game không đổi: chỉ chữ người chơi thấy được thay.
   Từ điển sinh/kiểm tra bằng: node tools/i18n_extract.cjs (liệt kê đoạn còn thiếu bản dịch). */
(() => {
  const KEY = 'van-dao-lang';
  const q = new URLSearchParams(location.search);
  let lang = q.get('lang');
  try { if (lang) localStorage.setItem(KEY, lang); else lang = localStorage.getItem(KEY); } catch(e){}
  lang = lang === 'en' ? 'en' : 'vi';
  window.LANG = lang;
  /** Đổi ngôn ngữ: lưu lại rồi tải lại trang để mọi phần vẽ lại bằng ngôn ngữ mới. */
  window.setLang = l => {
    l = l === 'en' ? 'en' : 'vi';
    try { localStorage.setItem(KEY, l); } catch(e){}
    if (l === lang) return;
    const u = new URL(location.href); u.searchParams.set('lang', l); location.replace(u);
  };
  window.tr = s => s;
  if (lang !== 'en') return;

  document.documentElement.lang = 'en';
  const D = Object.assign(Object.create(null), window.I18N_EN);
  // khóa là mảnh nối sau dấu phân cách ("· bỏ lại {0} món"): thêm bản bỏ dấu đầu để khớp sau khi tách câu theo " · " / ", "
  for (const [k, v] of Object.entries(D)){
    const m = /^([·,;] )(.+)$/.exec(k);
    if (m && !(m[2] in D) && v.startsWith(m[1])) D[m[2]] = v.slice(m[1].length);
  }
  const VI = /[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/i;
  const norm = s => s.replace(/\s+/g, ' ').trim();
  const esc = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  // Mẫu có chỗ trống {0}, {1}…: biên thành regex, mẫu dài (cụ thể) thử trước.
  // Chỗ trống được phép rỗng (nhánh `${a ? '…' : ''}`) và khoảng trắng sát chỗ trống là tuỳ chọn.
  const PATS = [], TERMS = [], UPPER = Object.create(null);
  for (const [k, v] of Object.entries(D)){
    if (!k.includes('{')){ if (k.length <= 40){ UPPER[k.toUpperCase()] = v.toUpperCase(); if (VI.test(k)) TERMS.push(k); } continue; }
    const parts = k.split(/\{(\d+)\}/), order = [];
    let re = '^';
    parts.forEach((p, i) => {
      if (i % 2){ order.push(+p); re += '([\\s\\S]*?)'; return; }
      const a = i > 0 && p.startsWith(' '), b = i < parts.length - 1 && p.endsWith(' ');
      const mid = p.slice(a ? 1 : 0, b ? -1 : undefined);
      re += (a ? '\\s?' : '') + esc(mid) + (b && mid ? '\\s?' : '');
    });
    PATS.push({ re: new RegExp(re + '$'), order, en: v, lit: parts.filter((_, i) => !(i % 2)).join('').length });
  }
  PATS.sort((a, b) => b.lit - a.lit);
  TERMS.sort((a, b) => b.length - a.length);
  const TERM_RE = TERMS.length ? new RegExp(`(?<![\\p{L}\\p{N}])(${TERMS.map(esc).join('|')})(?![\\p{L}\\p{N}])`, 'gu') : null;

  /** Tách theo sep, bỏ qua sep nằm trong (…) hoặc […]. */
  function splitTop(s, sep){
    const out = []; let depth = 0, from = 0;
    for (let i = 0; i < s.length; i++){
      const c = s[i];
      if (c === '(' || c === '[') depth++;
      else if ((c === ')' || c === ']') && depth) depth--;
      else if (!depth && s.startsWith(sep, i)){ out.push(s.slice(from, i)); i += sep.length - 1; from = i + 1; }
    }
    out.push(s.slice(from));
    return out;
  }
  const SEPS = ['. ', ' · ',' — ', ' – ', ' | ', '; ', ', ', ': ', ' / ', ' → '];
  const VIG = new RegExp(VI.source, 'gi');
  const left = r => r === null ? Infinity : (r.match(VIG) || []).length;   // số chữ tiếng Việt còn sót: càng ít càng tốt
  const memo = new Map();
  /** Dịch một dòng đã chuẩn hoá khoảng trắng; trả về null nếu không dịch được phần nào. */
  function line(s, depth){
    if (s in D) return D[s];
    if (s + '.' in D) return D[s + '.'].replace(/\.$/, '');
    if (s in UPPER) return UPPER[s];
    const bare = s.replace(/[.:!…]$/, '');
    if (bare !== s && bare in D) return D[bare] + s.slice(-1);
    if (!VI.test(s) || depth > 4) return null;
    if (memo.has(s)) return memo.get(s);
    // thử mọi cách (mẫu có chỗ trống, tách theo dấu phân cách, bỏ số/ký hiệu, thay thuật ngữ) rồi chọn bản còn ít chữ Việt nhất
    let best = null, score = Infinity;
    const offer = r => { const k = left(r); if (k < score){ best = r; score = k; } return k === 0; };
    let tried = 0;
    // câu đứng trước ". " bị tách mất dấu chấm: thử cả mẫu có dấu chấm cuối rồi bỏ dấu chấm ở bản dịch
    const dot = !/[.!?…:]$/.test(s);
    for (const p of PATS){
      let m = p.re.exec(s), cut = false;
      if (!m && dot){ m = p.re.exec(s + '.'); cut = !!m; }
      if (!m) continue;
      const fill = (_, i) => { const g = m[p.order.indexOf(+i) + 1] ?? '', c = g.trim(); return c ? g.replace(c, () => line(c, depth + 1) ?? c) : g; };
      const en = p.en.replace(/\{(\d+)\}/g, fill);
      if (offer(cut ? en.replace(/\.$/, '') : en) || ++tried >= 4) break;
    }
    if (score) for (const sep of SEPS){
      if (!s.includes(sep)) continue;
      // ghép lại các phần liền kề thành đoạn dài nhất có trong từ điển (câu có sẵn dấu phân cách bên trong)
      const parts = splitTop(s, sep), out = [];
      if (parts.length < 2) continue;
      let hit = false;
      for (let i = 0; i < parts.length;){
        let j = parts.length, r = null;
        for (; j > i + 1; j--){ const c = parts.slice(i, j).join(sep); r = D[c] ?? D[c + sep.trim()] ?? null; if (r !== null){ if (!(c in D)) r = r.slice(0, -sep.trim().length); break; } }
        if (r === null) r = line(parts[i], depth + 1);
        if (r !== null) hit = true;
        out.push(r ?? parts.slice(i, j).join(sep)); i = j;
      }
      if (hit && offer(out.join(sep))) break;
    }
    // số / ký hiệu đứng đầu hoặc cuối: "+3 Kiếm Sắt", "Kiếm Sắt ×2", "Kiếm Sắt (Huyền)"
    let m = score && /^([+\-−×x]?\s?[\d.,]+%?\s?[×x]?\s|[+\-−]\s?)(.+)$/.exec(s);
    if (m){ const r = line(m[2], depth + 1); if (r !== null) offer(m[1] + r); }
    m = score && /^(.+?)(\s?[×x]\s?[\d.,]+|\s\+\d+|\s?\([^()]*\)|\s?\[[^[\]]*\])$/.exec(s);
    if (m){ const a = line(m[1], depth + 1), b = m[2].replace(/([([])([^()[\]]*)/, (_, o, x) => o + (line(x, depth + 1) ?? x));
      if (a !== null || b !== m[2]) offer((a ?? m[1]) + b); }
    if (score && TERM_RE){ const r = s.replace(TERM_RE, t => D[t]); if (r !== s) offer(r); }
    if (memo.size > 20000) memo.clear();
    memo.set(s, best);
    return best;
  }

  const cache = new Map();
  /** Dịch một chuỗi bất kỳ (giữ khoảng trắng đầu/cuối, dịch từng dòng). */
  function tr(s){
    if (typeof s !== 'string' || !s) return s;
    let r = cache.get(s);
    if (r !== undefined) return r;
    const m = /^(\s*)([\s\S]*?)(\s*)$/.exec(s);
    r = m[2] ? m[1] + m[2].split('\n').map(l => { const n = norm(l); return n ? (line(n, 0) ?? l) : l; }).join('\n') + m[3] : s;
    if (cache.size > 20000) cache.clear();
    cache.set(s, r);
    return r;
  }
  window.tr = tr;

  // ---------- áp lên DOM ----------
  const ATTRS = ['data-tip', 'title', 'aria-label', 'placeholder', 'alt'];
  const SKIP = { SCRIPT: 1, STYLE: 1, TEXTAREA: 1 };
  const NO_TR = '[translate="no"]';                                  // nội dung người chơi (chat): không dịch
  function text(n){
    if (SKIP[n.parentNode?.nodeName] || n.parentElement?.closest(NO_TR)) return;
    const t = n.data, r = tr(t);
    if (r !== t) n.data = r;
  }
  function attr(el, a){
    const v = el.getAttribute(a);
    if (v && !el.closest(NO_TR)){ const r = tr(v); if (r !== v) el.setAttribute(a, r); }
  }
  function walk(root){
    if (root.nodeType === 3){ text(root); return; }
    if (root.nodeType !== 1 || SKIP[root.nodeName]) return;
    const w = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
    for (let n = root; n; n = w.nextNode()){
      if (n.nodeType === 3) text(n);
      else { for (const a of ATTRS) if (n.hasAttribute(a)) attr(n, a);
        if (n.nodeName === 'INPUT' && /^(button|submit)$/i.test(n.type)) attr(n, 'value'); }
    }
  }
  new MutationObserver(recs => {
    for (const r of recs){
      if (r.type === 'childList') r.addedNodes.forEach(walk);
      else if (r.type === 'characterData') text(r.target);
      else attr(r.target, r.attributeName);
    }
  }).observe(document.documentElement, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  walk(document.documentElement);
  document.title = tr(document.title);

  // ---------- chữ vẽ trên canvas và hộp thoại ----------
  const C2D = CanvasRenderingContext2D.prototype;
  for (const f of ['fillText', 'strokeText', 'measureText']){
    const orig = C2D[f];
    C2D[f] = function(t, ...rest){ return orig.call(this, typeof t === 'string' ? tr(t) : t, ...rest); };
  }
  for (const f of ['alert', 'confirm', 'prompt']){
    const orig = window[f];
    window[f] = (msg, ...rest) => orig.call(window, tr(String(msg ?? '')), ...rest);
  }
})();
