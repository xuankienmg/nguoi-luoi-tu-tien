// Tách thông tin từ văn bản OCR của CMND / CCCD. Chạy cả trình duyệt lẫn Node (để kiểm thử).
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.CmndParse = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  const strip = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const clean = s => s.replace(/^[\s:;.,\-–|_]+|[\s|_]+$/g, '').replace(/\s{2,}/g, ' ');
  const DATE = /(\d{1,2})\s*[\/\-.]\s*(\d{1,2})\s*[\/\-.]\s*(\d{4})/;
  const fmtDate = m => `${m[1].padStart(2, '0')}/${m[2].padStart(2, '0')}/${m[3]}`;
  const validDate = s => { const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(s || ''); return !!m && +m[1] >= 1 && +m[1] <= 31 && +m[2] >= 1 && +m[2] <= 12 && +m[3] >= 1900 && +m[3] <= 2100; };

  // OCR hay đọc nhầm chữ số thành chữ (0 → O/Q/D, 1 → I/l): sửa trong các cụm chủ yếu là số ("O56182004736", "3Q/Q7/2042").
  const DIGIT = { O: '0', o: '0', Q: '0', D: '0', I: '1', l: '1', '|': '1' };
  const fixDigits = line => line.replace(/[0-9OoQD][0-9OoQDIl|\/.\-]*[0-9OoQD]/g, run => {
    const real = (run.match(/\d/g) || []).length, chars = run.replace(/[\/.\-]/g, '').length;
    return real >= 3 && real >= chars / 2 ? run.replace(/[OoQDIl|]/g, c => DIGIT[c]) : run;
  });

  // Nhãn (không dấu) cho từng trường; dòng khớp nhãn thì phần sau dấu ":" hoặc dòng kế là giá trị.
  // Nhãn được viết "lỏng" vì OCR ảnh chụp hay làm sai / dính chữ ("Dateofbith", "Nabonalty"...).
  const LABELS = {
    name: /\b(ho\s*(va|&|v[aàá])?\s*te[nm]|h[oa]\s*v[aà]\s*te[nm]|ho,?\s*chu\s*dem\s*va\s*te[nm]|va\s*ten|full\s*n[a-z]{2,3})\b/,
    dob: /\b(ngay\s*sinh|ngay\s*s\w{1,3}h|nam\s*sinh|date\s*of\s*b\w*|dateofb\w*|oate\s*of\s*b\w*)\b/,
    sex: /\b(gioi\s*tinh|gi[o0]\w{0,4}nh|s[eo][xc])\b/,
    nation: /\b(qu\w{1,2}c\s*ti[ch]h?|nationality|nabonal\w*)\b/,
    origin: /\b(que\s*qu[a-z]n|place\s*of\s*orig\w*)\b/, // OCR: "Quê quên", "Place of origir"
    residence: /\b(noi\s*(thuong\s*tru|cu\s*tru)|place\s*of\s*residence|thuong\s*tru|tru\s*\/\s*p[l1]ac[eo]|p[l1]ac[eo]\s*of\s*r[eo]si\w*)\b/, // OCR: "Nơi tu ng trú / Placo of rosidnge"
    birth: /\b(noi\s*dang\s*ky\s*khai\s*sinh|noi\s*sinh|p[l/]?ace\s*of\s*birth)\b/,
    expiry: /\b(co\s*gia\s*t\w{0,2}(\s*d\w{1,2}n)?|date\s*of\s*exp\w*|het\s*han|gia\s*tri\s*den)\b/,
    id: /\b(so|no|so\s*\/?\s*no)\b\s*[:.]/,
  };
  const anyLabel = key => Object.values(LABELS).some(re => re.test(key)) || /\b(dan\s*toc|ton\s*giao|dac\s*diem|nhan\s*dang|ngay\s*cap|ngay,?\s*thang|cuc\s*truong|giam\s*doc|cong\s*hoa|doc\s*lap|can\s*cuoc|chung\s*minh|identity|socialist|republic|dinh\s*danh|bo\s*cong\s*an|place|tru)\b/.test(key); // "place", "trú": nhãn địa chỉ bị OCR đọc hỏng ("trú Place đệ.") — địa chỉ thật không chứa 2 từ này

  // Bỏ "rác" OCR trong địa chỉ: từ có ký tự lạ, từ không có nguyên âm, từ IN HOA ngắn lẫn trong dòng có chữ thường.
  const KEEP_UP = new Set(['TP', 'TX', 'TT', 'KP', 'KV', 'HCM', 'TPHCM', 'P', 'Q', 'H', 'X']);
  const VOWEL = /[aeiouyăâêôơưáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]/i;
  function cleanAddr(s) {
    return s.split(/\r?\n/).map(line => {
      const mixed = /[a-zà-ỹ]/.test(line);
      const toks = line.split(/\s+/).map(t => t.replace(/^["“”'«»`]+|["“”'«»`]+$/g, '')).filter(t => { // bỏ ngoặc kép dính mép từ ("Campuchi)
        const w = t.replace(/^[,.;:]+|[,.;:]+$/g, '');
        if (!w) return false;
        if (/[\[\]{}|<>^~_=`"“”«»\\]/.test(w)) return false;
        if (/^\d/.test(w)) return true;
        if (!VOWEL.test(w)) return (w.length > 1 || w === w.toUpperCase()) && KEEP_UP.has(w.toUpperCase().replace(/\./g, '')); // "x" thường lẻ loi là rác, "X." viết tắt thì giữ
        if (mixed && w === w.toUpperCase() && w.length <= 4 && !KEEP_UP.has(w.replace(/\./g, ''))) return false;
        return true;
      }).map(t => mixed && /^[A-ZÀ-ỸĐ]{2,}[a-zà-ỹđ]/.test(t) ? t[0] + t.slice(1).toLowerCase() : t); // "HÒa" → "Hòa"
      return dropJunk(toks).join(' ');
    }).join('\n');
  }

  // Từ "đúng kiểu địa chỉ": số nhà (12, 12A, 3/4B), viết tắt (TP, KP…), hoặc âm tiết tiếng Việt viết hoa chữ đầu;
  // chữ thường chỉ chấp nhận các từ hành chính hay viết thường ("Thị xã", "Thành phố", "Tổ dân phố"…).
  const ADDR_LOW = /^(pho|xa|tran|thi|khu|ap|to|so|nha|dan|vuc|duong|phuong|huyen|tinh|quan|thon|xom|khom|lo|hem|ngo|ngach|kiet|doi)$/;
  const addrTok = t => {
    let w = t.replace(/^[,.;:]+|[,.;:]+$/g, '');
    if (!w || /^\d+\p{Lu}?([\/\-]\d+\p{Lu}?)*$/u.test(w) || KEEP_UP.has(w.toUpperCase().replace(/\./g, ''))) return true; // 12, 16A, 3/4B, 6Ạ
    if (/^[A-Z]{1,3}(-[A-Z]{1,3})+$/.test(w)) return true; // BR-VT
    const ab = w.match(/^(TP|TX|TT|KP|P|Q|X|H)\.(.+)$/); // "P.Nguyễn", "TP.Vũng"
    if (ab) w = ab[2];
    if (!/^[A-Za-zÀ-ỹĐđ]+$/.test(w) || !SYL.test(strip(w))) return false;
    return w[0] !== w[0].toLowerCase() || ADDR_LOW.test(strip(w));
  };
  const addrScore = s => { const t = String(s || '').split(/[\s,]+/).filter(Boolean); return t.length ? t.filter(addrTok).length / t.length : -1; };
  // Bỏ rác OCR trong một dòng địa chỉ: dòng có > 1/3 từ sai kiểu → bỏ cả dòng ("ri;, P ⁄3A8ZXÀ 2z Nó vo đi");
  // còn lại bỏ từ sai kiểu cùng các từ ngắn (≤ 2 chữ cái) liền kề trong cùng đoạn phẩy ("Phương Quới, De co Phương Bình" → bỏ "De co").
  function dropJunk(toks) {
    const bad = toks.map(t => !addrTok(t));
    if (bad.filter(Boolean).length > toks.length / 3) return [];
    if (toks.join('').replace(/[^A-Za-zÀ-ỹĐđ]/g, '').length < 3 && !/\d{2}/.test(toks.join('')) && !toks.some(t => /^\p{Lu}\p{Ll}/u.test(t) && addrTok(t))) return []; // chỉ còn mẩu vụn ("1u"), trừ số nhà ("430/16A")
    const short = t => !/\d/.test(t) && t.replace(/[^A-Za-zÀ-ỹĐđ]/g, '').length <= 2;
    const drop = bad.slice();
    for (let i = 0; i < toks.length; i++) if (bad[i]) {
      for (let j = i - 1; j >= 0 && short(toks[j]) && !/,$/.test(toks[j]); j--) drop[j] = true;
      for (let j = i + 1; j < toks.length && !/,$/.test(toks[j - 1]) && short(toks[j]); j++) drop[j] = true;
    }
    const out = [];
    // Chỗ bị bỏ rác coi như ranh giới đoạn (thêm ","), để phần đuôi rác còn sót ("…Thanh Hóa ri; vo đi, 1 Ghi") tách riêng và bị trimTail bỏ.
    toks.forEach((t, i) => { if (!drop[i]) out.push(t); else if (out.length && !/,$/.test(out[out.length - 1])) out[out.length - 1] += ','; });
    return out;
  }

  // Địa chỉ trên thẻ viết hoa đầu mỗi từ: bỏ từ rác ngắn chữ thường dính ở đầu do ảnh chân dung / viền ("hi á Tân Hiệp" → "Tân Hiệp").
  function trimLead(s) {
    const t = s.split(' ');
    const bad = w => { const L = w.replace(/[^A-Za-zÀ-ỹĐđ0-9]/g, ''); return !L || (L.length <= 3 && L[0] === L[0].toLowerCase() && !/\d/.test(L[0])); };
    while (t.length > 2 && bad(t[0])) t.shift();
    return trimTail(t.join(' ').replace(/^[\s,.;:]+/, ''));
  }
  // Rác ở cuối địa chỉ (địa chỉ luôn kết thúc bằng tên tỉnh): "An Giang 4 P" → "An Giang"; "An Giang, ó, ni" → "An Giang".
  const UNIT = /^(phuong|quan|to|so|khu|ap|doi|xom|khom|thon|hem|ngo|kiet|lo|can|nha|p|q|kp)$/;
  function trimTail(s) {
    let segs = s.split(/\s*,\s*/);
    const shortLower = w => { const L = w.replace(/[^A-Za-zÀ-ỹĐđ]/g, ''); return !/\d/.test(w) && L.length <= 2 && (!L || L[0] === L[0].toLowerCase()); };
    // Đoạn cuối là tên tỉnh: không bao giờ bắt đầu bằng số ("…, 1 Ghi") hay chỉ gồm chữ thường ngắn ("…, ó, ni").
    while (segs.length > 1 && (/^\d/.test(segs[segs.length - 1]) || segs[segs.length - 1].split(/\s+/).every(shortLower))) segs.pop();
    const t = segs[segs.length - 1].split(/\s+/);
    while (t.length > 1 && t[t.length - 1].replace(/[^A-Za-zÀ-ỹĐđ0-9]/g, '').length <= 1 && !UNIT.test(strip(t[t.length - 2]).replace(/\W/g, ''))) t.pop();
    segs[segs.length - 1] = t.join(' ');
    return segs.join(', ');
  }

  // Tên tỉnh/thành (63 tỉnh, theo cách in trên thẻ): sửa dấu OCR đọc sai ("Thanh Hỏa" → "Thanh Hóa") khi chữ không dấu khớp,
  // và thêm dấu phẩy trước tên tỉnh ở cuối nếu OCR làm mất ("Thành phố Thanh Hóa Thanh Hóa" → "…Thanh Hóa, Thanh Hóa").
  const PROVINCES = ['An Giang', 'Bà Rịa - Vũng Tàu', 'Bạc Liêu', 'Bắc Giang', 'Bắc Kạn', 'Bắc Ninh', 'Bến Tre', 'Bình Dương', 'Bình Định', 'Bình Phước', 'Bình Thuận', 'Cà Mau', 'Cao Bằng', 'Cần Thơ', 'Đà Nẵng', 'Đắk Lắk', 'Đắk Nông', 'Điện Biên', 'Đồng Nai', 'Đồng Tháp', 'Gia Lai', 'Hà Giang', 'Hà Nam', 'Hà Nội', 'Hà Tĩnh', 'Hải Dương', 'Hải Phòng', 'Hậu Giang', 'Hòa Bình', 'Hưng Yên', 'Khánh Hòa', 'Kiên Giang', 'Kon Tum', 'Lai Châu', 'Lâm Đồng', 'Lạng Sơn', 'Lào Cai', 'Long An', 'Nam Định', 'Nghệ An', 'Ninh Bình', 'Ninh Thuận', 'Phú Thọ', 'Phú Yên', 'Quảng Bình', 'Quảng Nam', 'Quảng Ngãi', 'Quảng Ninh', 'Quảng Trị', 'Sóc Trăng', 'Sơn La', 'Tây Ninh', 'Thái Bình', 'Thái Nguyên', 'Thanh Hóa', 'Thừa Thiên Huế', 'Tiền Giang', 'Trà Vinh', 'Tuyên Quang', 'Vĩnh Long', 'Vĩnh Phúc', 'Yên Bái', 'Hồ Chí Minh']
    .map(p => ({ p, w: strip(p).replace(/[^a-z ]/g, ' ').split(/\s+/).filter(Boolean) })).sort((a, b) => b.w.length - a.w.length);
  function fixProvince(s) {
    s = s.replace(/(^|,\s*)[AÁ]p\s+(?=\p{Lu})/gu, '$1Ấp '); // OCR đọc "Ấp" thành "Áp"
    const t = s.split(' '), core = x => strip(x).replace(/[^a-z]/g, '');
    for (let i = 0; i < t.length; i++) for (const { p, w } of PROVINCES) {
      const idx = []; // vị trí các từ có chữ (bỏ qua "-" trong "Bà Rịa - Vũng Tàu")
      for (let j = i; j < t.length && idx.length < w.length; j++) { if (!core(t[j])) continue; if (j > i && /,$/.test(t[j - 1])) break; idx.push(j); }
      if (idx.length !== w.length || idx.some((j, k) => core(t[j]) !== w[k])) continue;
      const end = idx[idx.length - 1], tail = t[end].match(/[,.;]*$/)[0];
      t.splice(i, end - i + 1, ...(p + tail).split(' '));
      break;
    }
    let out = t.join(' ');
    const last = PROVINCES.find(({ p }) => out.endsWith(' ' + p));
    if (last) {
      const before = out.slice(0, -last.p.length).trimEnd();
      if (before && !/[,\-]$/.test(before) && !/\b(pho|tinh|tp|thanh pho)\.?$/.test(strip(before))) out = before + ', ' + last.p;
    }
    return out;
  }

  // Nối 2 dòng địa chỉ: tên địa danh hiếm khi chỉ 1 từ, nên dòng trên kết thúc bằng 1 từ lẻ không có dấu phẩy ("…, Tân" + "Bình, Dĩ An")
  // là tên bị xuống dòng giữa chừng → nối bằng khoảng trắng; còn lại nối bằng ", ".
  const COUNTRIES = [[/\bcampuch\w*|\bcampu\w*chia\b|\bcambod\w*/, 'Campuchia'], [/\bl[aà]o\s*$|\bcong\s*hoa\s*dan\s*chu\s*nhan\s*dan\s*lao\b/, 'Lào'], [/\btrung\s*quoc\b/, 'Trung Quốc'], [/\bthai\s*lan\b/, 'Thái Lan']];
  function wrapJoin(s, sep, at) {
    const before = s.slice(0, at), lastSeg = before.slice(before.lastIndexOf(',') + 1).trim();
    return !sep.includes(',') && (/,/.test(before) || /^\p{Lu}\p{Ll}?$/u.test(lastSeg)) && /^[A-ZÀ-ỸĐ][^\s\d]*$/u.test(lastSeg) && lastSeg[0] !== lastSeg[0].toLowerCase() ? ' ' : ', ';
  }

  // OCR làm dính 2 chữ của tên ("KIMXUYÊN"): nếu cả từ không phải âm tiết nhưng tách được thành 2 âm tiết hợp lệ thì tách ("KIM XUYÊN").
  function splitGlued(w) {
    if (w.length < 5 || SYL.test(strip(w))) return w;
    for (let k = 2; k <= w.length - 2; k++) if (SYL.test(strip(w.slice(0, k))) && SYL.test(strip(w.slice(k)))) return w.slice(0, k) + ' ' + w.slice(k);
    return w;
  }

  // Số CCCD từ nhiều lượt đọc: lấy đa số theo từng chữ số (3 lượt "…003678", "…005671", "…005678" → "…005678").
  function voteId(ids) {
    const c = ids.filter(i => /^\d{12}$/.test(i || ''));
    if (c.length < 3) return '';
    let out = '';
    for (let p = 0; p < 12; p++) {
      const n = {}; c.forEach(i => { n[i[p]] = (n[i[p]] || 0) + 1; });
      const [d, k] = Object.entries(n).sort((x, y) => y[1] - x[1])[0];
      if (k * 2 <= c.length) return ''; // không có đa số ở vị trí này
      out += d;
    }
    return out;
  }

  // Dòng "rác" do OCR (ít chữ cái so với ký tự khác, vd "1 99 91 09V (gy3/2029 XE: TS ;i").
  const junk = l => { const t = l.replace(/\s/g, ''); return (t.match(/[A-Za-zÀ-ỹĐđ]/g) || []).length < 0.6 * t.length; };

  // Phần chữ sau nhãn hạn dùng + ngày trên cùng dòng ("Có giá trị đến: 30/10/2032 Bình, Dĩ An, Bình Dương" → "Bình, Dĩ An, Bình Dương").
  function expiryTail(line, key) {
    const m = key.match(LABELS.expiry);
    if (!m) return '';
    let rest = line.slice(m.index + m[0].length);
    const d = rest.match(DATE);
    rest = d ? rest.slice(d.index + d[0].length) : rest.slice(rest.search(/[:;]/) + 1);
    rest = clean(rest.replace(/^\s*\/?\s*date\s*of\s*exp\w*\s*[:;]?/i, ''));
    if (anyLabel(strip(rest)) || (rest.match(/[A-Za-zÀ-ỹĐđ]/g) || []).length < 4) return '';
    return wordScore(rest, /[\s,]+/) >= 0.6 ? rest : '';
  }

  // Lấy giá trị của một nhãn: phần sau ":" cùng dòng, nếu rỗng thì dòng kế; `multi` gộp thêm dòng tiếp theo chưa phải nhãn.
  function valueOf(lines, keys, re, multi, colonOnly, accept) {
    for (let i = 0; i < lines.length; i++) {
      if (!re.test(keys[i])) continue;
      const mt = keys[i].match(re);
      let rest = lines[i].slice(mt.index + mt[0].length), restKey = keys[i].slice(mt.index + mt[0].length);
      // Cắt tại nhãn kế tiếp trên cùng dòng (vd "Giới tính: Nam  Quốc tịch: Việt Nam").
      let cut = rest.length;
      for (const other of Object.values(LABELS)) { if (other === re) continue; const o = restKey.match(other); if (o && o.index < cut) cut = o.index; }
      rest = rest.slice(0, cut);
      // Trường chữ (tên, địa chỉ): chỉ nhận phần sau dấu ":" — phần nhãn tiếng Anh bị OCR đọc sai ("FXace ơf orig't") sẽ bị bỏ.
      if (colonOnly) {
        // Thẻ in "Place of residence Ấp Khánh Hưng" không có dấu ":" → lấy phần sau nhãn tiếng Anh nếu trông như chữ thật.
        // Dấu ":" chỉ tính khi đứng ngay sau nhãn; ":" rác ở cuối dòng ("…residenceÁp Khánh Hưng > ng x :") thì bỏ qua.
        const c = rest.search(/[:;]/), e = restKey.match(/(p[l1]ac[eo]\s*o\S{0,2}\s*(residen\w{0,2}|r[eo]s[il1]d\w{0,5}|r[eo]si\w{0,4}[cg]e|orig\w{0,3})|full\s*name)/); // OCR: "Placo oforigis", "rosidnge", "residonoe"
        const eEnd = e ? e.index + e[0].length : -1;
        if (c >= 0 && (!e || c <= eEnd + 1)) rest = rest.slice(c + 1);
        else if (e) { rest = rest.slice(eEnd).replace(/^\s*[:;]/, ''); if (wordScore(cleanAddr(rest), /[\s,]+/) < 0.6) rest = ''; }
        else rest = '';
      }
      else rest = rest.replace(/^[^:;]*\/[^:;]*[:;]/, '');
      let first = clean(rest.replace(/^\s*(\/\s*[A-Za-z ]+?)?\s*[:;]/, '').replace(/^\s*\/\s*[A-Za-z ]+$/, ''));
      first = clean(first.replace(/^\/?\s*(full\s*name|date\s*of\s*birth|sex|nationality|place\s*of\s*(origin|residence)|date\s*of\s*expiry)\s*[:;]?/i, ''));
      if (first.replace(/[^A-Za-zÀ-ỹ0-9]/g, '').length < 3 && !/^\p{Lu}\p{Ll}$/u.test(first)) first = ''; // chỉ còn rác 1-2 ký tự sau nhãn (giữ địa danh 2 chữ viết hoa đầu như "An")
      if (accept && first && !accept(first)) first = ''; // phần sau ":" là rác → giá trị ở dòng kế
      let parts = [];
      if (first) parts.push(first);
      let j = i + 1;
      // Giá trị nằm sau ":" cùng dòng, hoặc bắt đầu ở dòng kế; địa chỉ (multi) tối đa 2 dòng như trên thẻ.
      // Với địa chỉ, dòng toàn rác (bộ lọc rác bỏ sạch) không tính là một dòng.
      const empty = l => !clean(l) || junk(l) || (multi && !clean(cleanAddr(l)));
      if (!parts.length) {
        while (j < lines.length && empty(lines[j]) && !anyLabel(keys[j])) j++;
        if (j < lines.length && !anyLabel(keys[j])) { parts.push(clean(lines[j])); j++; }
        else continue;
      }
      if (multi) while (j < lines.length && parts.length < 2 && clean(lines[j])) {
        // Dòng 2 của địa chỉ hay nằm cùng hàng với "Có giá trị đến: dd/mm/yyyy" (ô hạn dùng ở góc trái dưới thẻ) → lấy phần sau ngày.
        if (anyLabel(keys[j])) { const t = expiryTail(lines[j], keys[j]); if (t) parts.push(t); break; }
        if (!empty(lines[j])) parts.push(clean(lines[j]));
        j++;
      }
      return clean(parts.join('\n'));
    }
    return '';
  }

  // OCR hay làm rơi dấu ở họ và đệm phổ biến: khôi phục khi từ đó đang không dấu (chỉ họ ở đầu, THI/VAN ở giữa tên).
  const SURNAME = { NGUYEN: 'NGUYỄN', TRAN: 'TRẦN', LE: 'LÊ', PHAM: 'PHẠM', HUYNH: 'HUỲNH', VO: 'VÕ', DANG: 'ĐẶNG', BUI: 'BÙI', DO: 'ĐỖ', NGO: 'NGÔ', DUONG: 'DƯƠNG', LY: 'LÝ', DINH: 'ĐINH', DAO: 'ĐÀO', DOAN: 'ĐOÀN', TRUONG: 'TRƯƠNG', LUONG: 'LƯƠNG', PHAN: 'PHAN', VU: 'VŨ', HOANG: 'HOÀNG', HO: 'HỒ', TO: 'TÔ', TA: 'TẠ', HA: 'HÀ', LAM: 'LÂM', MAI: 'MAI', CAO: 'CAO', TRINH: 'TRỊNH', THAI: 'THÁI', LUU: 'LƯU', QUACH: 'QUÁCH', CHAU: 'CHÂU', TANG: 'TĂNG' };
  const MIDDLE = { THI: 'THỊ', VAN: 'VĂN' };
  // Chữ chỉ dùng làm họ, không dùng làm tên/đệm: OCR hay thêm dấu chấm (nhiễu nền / dấu của chữ bên cạnh) → "MAI TRỊNH" thực ra là "MAI TRINH".
  const GIVEN = { 'TRỊNH': 'TRINH' };
  function restoreName(name) {
    const w = name.split(/\s+/).filter(Boolean);
    if (w.length < 2) return name;
    const s0 = strip(w[0]).toUpperCase();
    if (SURNAME[s0]) w[0] = SURNAME[s0]; // họ: sửa cả khi OCR đọc sai dấu (NGUYÊN → NGUYỄN)
    for (let i = 1; i < w.length - 1; i++) if (MIDDLE[w[i]]) w[i] = MIDDLE[w[i]];
    for (let i = 1; i < w.length; i++) if (GIVEN[w[i]]) w[i] = GIVEN[w[i]];
    return w.join(' ');
  }

  // Dòng MRZ mặt sau CCCD: "TRAN<<TAN<LUONG<<<<" → "TRAN TAN LUONG" (không dấu; họ phổ biến sẽ được khôi phục dấu).
  // Tên trên thẻ luôn in hoa: giữ dãy từ in hoa đầu dòng, bỏ rác chữ thường theo sau ("ĐINH THẾ QUANG gia ẤN" → "ĐINH THẾ QUANG").
  function upperRun(v) {
    const run = [], ts = v.split(/\s+/), letters = t => (t || '').replace(/[^A-Za-zÀ-ỹĐđ]/g, '');
    for (let i = 0; i < ts.length; i++) {
      const t = ts[i], L = letters(t);
      if (L.length < 2) continue; // tên tiếng Việt không có từ 1 chữ cái → rác OCR ("Ì NGUYỄN…")
      // Họ ngắn hay bị OCR đọc thành chữ thường ("Hồ THỊ KIM XUYÊN"): giữ khi là họ phổ biến đứng ngay trước từ in hoa.
      const next = letters(ts[i + 1]), surnameHead = !run.length && L[0] !== L[0].toLowerCase() && SURNAME[strip(L).toUpperCase()] && next.length >= 2 && next === next.toUpperCase();
      if (L === L.toUpperCase() || surnameHead) run.push(t); else if (run.length) break;
    }
    return run.length >= 2 ? run.join(' ') : v;
  }

  function mrzName(lines) {
    for (const l of lines) {
      const m = l.replace(/\s+/g, '').match(/([A-Z]{2,}(?:<{1,2}[A-Z]{2,})+)<{2,}/);
      if (m) return m[1].replace(/<+/g, ' ');
    }
    return '';
  }

  // Dự phòng khi không tìm thấy nhãn "Họ và tên": lấy dòng toàn chữ in hoa 2-6 từ, không phải tiêu đề quốc hiệu/loại giấy tờ.
  const STOP = /(cong hoa|xa hoi|chu nghia|viet nam|doc lap|tu do|hanh phuc|can cuoc|cong dan|chung minh|nhan dan|giay|socialist|republic|identity|citizen|card|bo cong an|ministry|quoc tich|que quan|noi |thuong tru|gia tri|ngay|gioi tinh)/;
  function guessName(lines, keys) {
    const cands = [];
    lines.forEach((l, i) => {
      const t = l.replace(/[^A-ZÀ-ỹĐa-zà-ỹđ\s'.-]/g, ' ').replace(/^[\s.'-]+|[\s.'-]+$/g, '').replace(/\s{2,}/g, ' ');
      const w = t.split(/\s+/);
      if (!t || /\d/.test(l) || t !== t.toUpperCase() || w.length < 2 || w.length > 6 || w.some(x => x.replace(/[.'-]/g, '').length < 2)) return;
      if (STOP.test(strip(t)) || anyLabel(strip(t))) return;
      cands.push({ i, t });
    });
    const near = cands.find(c => c.i > 0 && /(ten|name)/.test(keys[c.i - 1]));
    return (near || cands[0] || { t: '' }).t;
  }

  function parse(text) {
    const lines = String(text || '').normalize('NFC').split(/\r?\n/).map(l => fixDigits(l.trim())).filter(Boolean);
    const keys = lines.map(strip);
    const out = { id: '', name: '', dob: '', sex: '', nation: '', origin: '', residence: '', expiry: '', type: '' };

    // Số giấy tờ: ưu tiên 12 số (CCCD), nếu không có thì 9 số (CMND cũ). Chấp nhận ký tự tách bằng khoảng trắng.
    const joined = lines.map(l => l.replace(/(?<=\d)[\s.]+(?=\d)/g, '')).join('\n');
    // Tìm trên từng dòng nguyên bản trước (tránh dính số rác bên cạnh, vd "„2 036204006153"), rồi mới thử ghép số bị tách khoảng trắng.
    const findId = n => { const re = new RegExp('(?<!\\d)\\d{' + n + '}(?!\\d)'); for (const l of lines) { const m = l.match(re); if (m) return m[0]; } const m = joined.match(re); return m ? m[0] : ''; };
    const id12 = findId(12), id9 = id12 ? '' : findId(9);
    if (id12) { out.id = id12; out.type = 'CCCD'; }
    else if (id9) { out.id = id9; out.type = 'CMND'; }

    const nameOk = v => { const w = v.split(/\s+/).filter(Boolean); return w.length >= 2 && w.length <= 7 && !/\d/.test(v); };
    const tidy = v => v.replace(/[^A-ZÀ-ỹa-zà-ỹĐđ\s'.-]/g, ' ').replace(/^[\s.'-]+|[\s.'-]+$/g, '').replace(/\s{2,}/g, ' ').toUpperCase().split(' ').map(splitGlued).join(' ');
    // Tên sau "Full name:" phải có ≥ 2 từ viết hoa chữ đầu giống âm tiết tiếng Việt; rác ("«. ¡ l -' là ï", "rh| 1 Thhy lS'") → lấy dòng kế.
    const nameLike = v => v.split(/\s+/).filter(t => { const L = t.replace(/[^A-Za-zÀ-ỹĐđ]/g, ''); return L.length >= 2 && L[0] !== L[0].toLowerCase() && SYL.test(strip(L)); }).length >= 2;
    out.name = tidy(upperRun(valueOf(lines, keys, LABELS.name, false, true, nameLike)));
    if (!nameOk(out.name)) out.name = tidy(upperRun(guessName(lines, keys))); // nhãn bị OCR đọc sai/mất → đoán theo dòng chữ in hoa
    if (!nameOk(out.name)) out.name = mrzName(lines);                // mặt sau CCCD: lấy từ dòng MRZ
    out.name = restoreName(out.name);

    const dobVal = valueOf(lines, keys, LABELS.dob, false), dm = dobVal.match(DATE);
    if (dm) out.dob = fmtDate(dm);
    const expVal = valueOf(lines, keys, LABELS.expiry, false), em = expVal.match(DATE);
    if (em) out.expiry = fmtDate(em);
    const all = [...joined.matchAll(new RegExp(DATE.source, 'g'))].map(fmtDate);
    // Dự phòng: chỉ dùng ngày đầu tiên làm ngày sinh khi văn bản có nhắc tới "sinh/birth" (tránh nhầm ngày cấp ở mặt sau).
    if (!out.dob && all[0] && (out.id || /sinh|snh|birth/.test(keys.join(' ')))) out.dob = all[0]; // mặt trước (có số) → ngày đầu tiên là ngày sinh
    if (!out.expiry && all.length > 1 && /gia\s*tri|het\s*han|expir/.test(keys.join(' '))) out.expiry = all[all.length - 1];

    // Hạn dùng bị OCR làm hỏng: thẻ CCCD hết hạn đúng sinh nhật tuổi 25 / 40 / 60 → suy từ năm còn đọc được.
    if (!out.expiry && out.dob) {
      const [dd, mm, yy] = out.dob.split('/'), years = [...joined.matchAll(/(?<!\d)(20\d{2})(?!\d)/g)].map(m => +m[1]);
      const y = [25, 40, 60].map(a => +yy + a).find(a => years.includes(a));
      if (y) out.expiry = `${dd}/${mm}/${y}`;
    }
    // Không đọc được hạn: thẻ "Căn cước công dân" chỉ cấp từ 2016 đến 30/06/2024 → nếu cấp lúc nào trong khoảng đó cũng ra cùng một hạn thì hạn là chắc chắn.
    if (!out.expiry && validDate(out.dob) && /ong\s*dan\b|c[il1]t[il1]z/.test(keys.join(' ')) /* OCR: "ÔÔNG DẪN", "Cltizon" */ && !/dinh\s*danh|personal\s*ident/.test(keys.join(' '))) {
      const e1 = expiryFrom(out.dob, '01/01/2016'), e2 = expiryFrom(out.dob, '30/06/2024');
      if (e1 && e1 === e2 && e1 !== 'Không thời hạn') out.expiry = e1;
    }
    // Một trong hai ngày bị OCR đọc sai (vd ngày sinh "40/07/1982"): ngày/tháng sinh = ngày/tháng hết hạn khi cách nhau 25/40/60 năm.
    if (out.dob && out.expiry && validDate(out.expiry) !== validDate(out.dob)) {
      const [d1, m1, y1] = out.dob.split('/'), [d2, m2, y2] = out.expiry.split('/');
      if ([25, 40, 60].includes(+y2 - +y1)) {
        if (validDate(out.expiry)) out.dob = `${d2}/${m2}/${y1}`; else out.expiry = `${d1}/${m1}/${y2}`;
      }
    }

    // Giới tính: tìm "Nam"/"Nữ" ngay sau nhãn (cùng dòng hoặc dòng kế), không quét cả văn bản vì "Việt Nam", "năm sinh" cũng chứa "nam".
    for (let i = 0; i < lines.length && !out.sex; i++) {
      const m = keys[i].match(LABELS.sex); if (!m) continue;
      const seg = (keys[i].slice(m.index + m[0].length) + ' ' + (keys[i + 1] || '')).replace(/\bsex\b/g, ' ').replace(/qu\w{1,2}c\s*ti.*$/, '');
      const s = seg.match(/\b(nam|nu)\b/);
      if (s) out.sex = s[1] === 'nu' ? 'Nữ' : 'Nam';
    }

    // Quốc tịch: nhãn hay bị OCR sai, nên chỉ cần dòng "Việt Nam" không phải quốc hiệu và có dấu hiệu quốc tịch.
    const HEADER = /(cong hoa|nghia|socialist|republic|ministry|bo cong an|citizen|ctizen)/;
    if (keys.some(k => /\bviet\s*na\w?\b/.test(k) && !HEADER.test(k) && /(tich|nationa|nabon|naon|\bna\w{0,6}ty\b)/.test(k))) out.nation = 'Việt Nam';
    else out.nation = clean(valueOf(lines, keys, LABELS.nation, false).replace(/[^A-Za-zÀ-ỹ\s]/g, ''));

    // CMND / CCCD chỉ cấp cho công dân Việt Nam → đã nhận ra số giấy tờ thì quốc tịch chắc chắn là Việt Nam (OCR hay đọc ra rác như "và").
    if (out.id || wordScore(out.nation, /\s+/) < 1 || out.nation.length < 4) out.nation = out.id || /viet\s*na/.test(strip(out.nation)) ? 'Việt Nam' : '';

    const addr = re => fixProvince(trimLead(clean(cleanAddr(valueOf(lines, keys, re, true, true).replace(new RegExp(DATE.source, 'g'), ' '))).replace(/[\s,]*\n[\s,]*/g, (sep, at, s) => wrapJoin(s, sep, at)).replace(/[\s.,;]+$/, '')));
    out.origin = addr(LABELS.origin);
    // Quê quán ở nước ngoài (người gốc Việt sinh ở Campuchia…): tên nước không phải âm tiết tiếng Việt nên bị bộ lọc rác bỏ → nhận riêng.
    if (!out.origin) {
      const i = keys.findIndex(k => LABELS.origin.test(k)), near = i < 0 ? '' : keys.slice(i, i + 3).join(' '); // dòng nhãn + 2 dòng kế
      const c = COUNTRIES.find(([re]) => re.test(near)); if (c) out.origin = c[1];
    }
    out.residence = addr(LABELS.residence);
    // Thẻ CCCD mới không in "Quê quán": dùng "Nơi đăng ký khai sinh" thay thế.
    if (!out.origin) out.origin = addr(LABELS.birth);
    return out;
  }

  // Mã QR trên CCCD gắn chip: "số|số CMND cũ|họ tên|ddmmyyyy sinh|giới tính|nơi thường trú|ddmmyyyy cấp".
  const qrDate = s => /^\d{8}$/.test(s || '') ? `${s.slice(0, 2)}/${s.slice(2, 4)}/${s.slice(4)}` : '';
  function parseQr(text) {
    const p = String(text || '').split('|');
    if (p.length < 6 || !/^\d{12}$/.test(p[0])) return null;
    return { id: p[0], oldId: p[1] || '', name: (p[2] || '').normalize('NFC').toUpperCase().trim(), dob: qrDate(p[3]),
      sex: /^n[ữu]$/i.test(p[4]) ? 'Nữ' : /^nam$/i.test(p[4]) ? 'Nam' : '', residence: (p[5] || '').replace(/(\s*,)+/g, ',').replace(/,(?=\S)/g, ', ').trim(), issued: qrDate(p[6]) };
  }

  // Hạn CCCD: đổi thẻ ở tuổi 25 / 40 / 60; cấp trong vòng 2 năm trước mốc thì dùng đến mốc sau; cấp sau 58 tuổi thì không thời hạn.
  function expiryFrom(dob, issued) {
    const d = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(dob || ''), i = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(issued || '');
    if (!d || !i) return '';
    const issue = +`${i[3]}${i[2]}${i[1]}`;
    for (const age of [25, 40, 60]) {
      const y = +d[3] + age;
      if (issue < +`${y - 2}${d[2]}${d[1]}`) return `${d[1]}/${d[2]}/${y}`;
    }
    return 'Không thời hạn';
  }

  // Vùng thẻ trong ảnh: bao quanh các từ OCR đọc chắc (bỏ 5% ngoại lệ mỗi phía) + lề. Trả về null nếu vùng gần bằng cả ảnh (không cần cắt).
  function cardBox(words, W, H) {
    const ws = (words || []).filter(w => w.confidence >= 60 && /[A-Za-zÀ-ỹ]{2,}/.test(w.text) && w.bbox.y1 - w.bbox.y0 >= 6);
    if (ws.length < 6) return null;
    const pct = (a, p) => { const s = a.slice().sort((x, y) => x - y); return s[Math.round(p * (s.length - 1))]; };
    // Ngưỡng 2% / 98% + lề 12%: cắt chặt hơn làm mất chữ đầu/cuối dòng ("An Bình" → "Bình", "Quê quán" → "ê quán").
    const x0 = pct(ws.map(w => w.bbox.x0), .02), x1 = pct(ws.map(w => w.bbox.x1), .98), y0 = pct(ws.map(w => w.bbox.y0), .02), y1 = pct(ws.map(w => w.bbox.y1), .98);
    const px = (x1 - x0) * .12, py = (y1 - y0) * .1, b = { x: Math.max(0, Math.floor(x0 - px)), y: Math.max(0, Math.floor(y0 - py)) };
    b.w = Math.min(W, Math.ceil(x1 + px)) - b.x; b.h = Math.min(H, Math.ceil(y1 + py)) - b.y;
    return b.w < .9 * W || b.h < .9 * H ? b : null;
  }

  // Các vùng [x, y, w, h] cần thử tìm mã QR, theo thứ tự rẻ → đắt: cả ảnh; góc trên-phải của thẻ (nếu đã biết vị trí thẻ); rồi quét cửa sổ trượt.
  // Mã QR chiếm phần rất nhỏ của ảnh chụp nên phải cắt/phóng đúng vùng thì mới giải mã được.
  function qrWindows(W, H, box) {
    const out = [[0, 0, W, H]], clamp = ([x, y, w, h]) => { x = Math.max(0, x); y = Math.max(0, y); return [Math.round(x), Math.round(y), Math.round(Math.min(w, W - x)), Math.round(Math.min(h, H - y))]; };
    if (box) {
      out.push(clamp([box.x + box.w * .5, box.y - box.h * .15, box.w * .65, box.h * .6]));
      out.push(clamp([box.x + box.w * .62, box.y - box.h * .12, box.w * .5, box.h * .42]));
    }
    for (const f of [.4, .28]) {
      const ww = W * f, hh = H * Math.min(1, f * (W > H ? 1.2 : .8)), nx = Math.ceil((W - ww) / (ww * .5)) + 1, ny = Math.ceil((H - hh) / (hh * .5)) + 1;
      for (let b = 0; b < ny; b++) for (let a = 0; a < nx; a++) out.push(clamp([nx > 1 ? (W - ww) * a / (nx - 1) : 0, ny > 1 ? (H - hh) * b / (ny - 1) : 0, ww, hh]));
    }
    return out.filter(r => r[2] >= 40 && r[3] >= 40);
  }

  // Âm tiết tiếng Việt hợp lệ (phụ âm đầu + nguyên âm + phụ âm cuối): loại rác OCR kiểu "TKƯƠI", "EMS.S", "PL".
  const SYL = /^(ngh|ng|nh|ch|gh|gi|kh|ph|th|tr|qu|[bcdghklmnprstvx])?[aeiouy]+(ch|ng|nh|[cmnptk])?$/;
  const plausible = w => /\d/.test(w) || KEEP_UP.has(w.toUpperCase().replace(/\./g, '')) || (/^[A-Za-zÀ-ỹĐđ]+$/.test(w) && SYL.test(strip(w)));
  const wordScore = (s, sep) => { const w = String(s || '').split(sep).filter(Boolean); return w.length ? w.filter(plausible).length / w.length : -1; };
  const okDob = d => validDate(d) && +d.slice(6) <= new Date().getFullYear();
  const idFits = (id, dob, sex) => { // số CCCD: chữ số 4 = thế kỷ + giới tính, chữ số 5-6 = 2 số cuối năm sinh
    if (!/^\d{12}$/.test(id) || !okDob(dob)) return false;
    const c = Math.floor(+dob.slice(6) / 100) - 19, g = +id[3];
    return +id.slice(4, 6) === +dob.slice(8) && (g >> 1) === c && (!sex || (g & 1) === (sex === 'Nữ' ? 1 : 0));
  };

  // Ghép địa chỉ của 2 lượt đọc theo từng đoạn (tách bởi dấu phẩy). Lấy bản đáng tin hơn (hòa thì giữ a), rồi:
  // - bỏ đoạn chỉ có ở bản đó mà nằm kẹp giữa 2 đoạn cả hai bản cùng có → rác ("Hoằng Lộc, Kha Xe, Thành phố…" → bỏ "Kha Xe");
  // - bản kia có thêm đoạn ở đầu/cuối mà bản này thiếu hẳn → bổ sung (lượt này bị mất "Hoằng Lộc").
  function mergeAddr(a, b) {
    const sa = addrScore(a), sb = addrScore(b), segs = s => String(s || '').split(/\s*,\s*/).filter(Boolean), key = s => strip(s).replace(/[^a-z0-9]/g, '');
    let main = segs(sb > sa ? b : a); const other = segs(sb > sa ? a : b);
    if (!main.length || !other.length) return main.join(', ') || other.join(', ');
    const ok = new Set(other.map(key)), common = main.map(s => ok.has(key(s)));
    const first = common.indexOf(true), last = common.lastIndexOf(true);
    if (first < 0) return main.join(', ');
    main = main.filter((s, i) => common[i] || i < first || i > last);
    const mk = new Set(main.map(key)), oFirst = other.findIndex(s => mk.has(key(s))), oLast = other.length - 1 - [...other].reverse().findIndex(s => mk.has(key(s)));
    if (main.findIndex(s => ok.has(key(s))) === 0) main = [...other.slice(0, oFirst), ...main];
    if (ok.has(key(main[main.length - 1]))) main = [...main, ...other.slice(oLast + 1)];
    return main.join(', ');
  }

  // Ghép kết quả đọc cả ảnh (a) với đọc vùng thẻ đã cắt (b): từng ô lấy giá trị đáng tin hơn, hòa thì giữ a.
  function merge(a, b) {
    const out = Object.assign({}, a);
    if (!b) return out;
    // Họ tên: ưu tiên bản mà mọi từ đều giống âm tiết tiếng Việt; hòa thì giữ a, trừ khi chỉ b có họ phổ biến.
    const nw = n => String(n || '').split(/\s+/).filter(Boolean), nameOk = n => nw(n).length >= 2 && nw(n).length <= 7;
    const sa = nameOk(a.name) ? wordScore(a.name, /\s+/) : -1, sb = nameOk(b.name) ? wordScore(b.name, /\s+/) : -1;
    const knownSurname = n => !!SURNAME[strip(nw(n)[0] || '').toUpperCase()] || nw(n)[0] === 'NGUYỄN';
    if (sb > sa || (sb === sa && sb === 1 && a.name !== b.name && knownSurname(b.name) && !knownSurname(a.name))) out.name = b.name;
    // Bỏ từ rác dính ở đầu/cuối tên ("TRẦN VĂN ĐỨC PL" → "TRẦN VĂN ĐỨC"), còn lại ít nhất 2 từ.
    { const t = nw(out.name); while (t.length > 2 && !plausible(t[t.length - 1])) t.pop(); while (t.length > 2 && !plausible(t[0])) t.shift(); out.name = t.join(' '); }
    out.dob = okDob(a.dob) ? a.dob : okDob(b.dob) ? b.dob : '';
    out.sex = a.sex || b.sex;
    out.nation = [a.nation, b.nation].includes('Việt Nam') ? 'Việt Nam' : [a.nation, b.nation].find(n => n && wordScore(n, /\s+/) === 1) || '';
    // Số giấy tờ: ưu tiên 12 số khớp ngày sinh/giới tính; không khớp thì 12 số của b (ảnh cắt rõ hơn), rồi a.
    const ids = [b.id, a.id].filter(Boolean), c12 = ids.filter(i => /^\d{12}$/.test(i));
    out.id = c12.find(i => idFits(i, out.dob, out.sex)) || c12[0] || ids[0] || '';
    out.type = /^\d{12}$/.test(out.id) ? 'CCCD' : out.id ? 'CMND' : '';
    if (out.type) out.nation = 'Việt Nam'; // CMND / CCCD chỉ cấp cho công dân Việt Nam
    if (!out.sex && out.type === 'CCCD') out.sex = +out.id[3] % 2 ? 'Nữ' : 'Nam'; // chữ số 4 của CCCD: chẵn = nam, lẻ = nữ
    for (const k of ['origin', 'residence']) out[k] = mergeAddr(a[k], b[k]);
    // Hạn thẻ CCCD trùng ngày/tháng sinh và cách năm sinh 25/40/60 năm; ngày khác (vd ngày cấp) là đọc nhầm.
    if (validDate(out.dob)) {
      const [dd, mm, yy] = out.dob.split('/'), ys = [a.expiry, b.expiry].filter(validDate).map(e => e.split('/'));
      const hit = ys.find(([d, m, y]) => d === dd && m === mm && [25, 40, 60].includes(+y - +yy)) || ys.find(([, , y]) => [25, 40, 60].includes(+y - +yy));
      out.expiry = hit ? `${dd}/${mm}/${hit[2]}` : '';
    } else out.expiry = [a.expiry, b.expiry].find(validDate) || '';
    return out;
  }

  // Quê quán bị cụt (mất chữ đầu / đoạn tỉnh cuối) nhưng khớp phần đuôi nơi thường trú (thường lấy từ QR, đầy đủ) → lấy theo thường trú.
  // "Bình, Phú Giáo" + thường trú "…, Ấp Bình Tiến, An Bình, Phú Giáo, Bình Dương" → "An Bình, Phú Giáo, Bình Dương".
  // Chỉ nối chữ đầu khi đoạn đầu của quê quán là 1 từ lẻ (dấu hiệu bị cắt), và cần khớp ít nhất 2 đoạn.
  function completeOrigin(origin, residence) {
    const segs = s => String(s || '').split(/\s*,\s*/).filter(Boolean), key = s => strip(s).replace(/[^a-z0-9 ]/g, '').trim();
    const o = segs(origin), r = segs(residence);
    if (o.length < 2 || !r.length) return origin;
    for (let i = 0; i + o.length <= r.length; i++) {
      const k0 = key(o[0]), r0 = key(r[i]);
      if (!(r0 === k0 || (!/\s/.test(k0) && r0.endsWith(' ' + k0)))) continue;
      if (!o.slice(1).every((s, k) => k === o.length - 2 ? key(r[i + k + 1]).startsWith(key(s)) : key(r[i + k + 1]) === key(s))) continue;
      const full = r.slice(i).join(', ');
      return full.length > origin.length ? full : origin;
    }
    return origin;
  }

  return { parse, parseQr, expiryFrom, strip, cardBox, merge, qrWindows, completeOrigin, voteId };
});
