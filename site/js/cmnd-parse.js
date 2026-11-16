// Tách thông tin từ văn bản OCR của CMND / CCCD. Chạy cả trình duyệt lẫn Node (để kiểm thử).
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.CmndParse = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  const strip = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const clean = s => s.replace(/^[\s:;.,\-–|_]+|[\s|_]+$/g, '').replace(/\s{2,}/g, ' ');
  const DATE = /(\d{1,2})\s*[\/\-.]\s*(\d{1,2})\s*[\/\-.]\s*(\d{4})/;
  const fmtDate = m => `${m[1].padStart(2, '0')}/${m[2].padStart(2, '0')}/${m[3]}`;

  // Nhãn (không dấu) cho từng trường; dòng khớp nhãn thì phần sau dấu ":" hoặc dòng kế là giá trị.
  // Nhãn được viết "lỏng" vì OCR ảnh chụp hay làm sai / dính chữ ("Dateofbith", "Nabonalty"...).
  const LABELS = {
    name: /\b(ho\s*(va|&|v[aàá])?\s*te[nm]|h[oa]\s*v[aà]\s*te[nm]|ho,?\s*chu\s*dem\s*va\s*te[nm]|va\s*ten|full\s*n[a-z]{2,3})\b/,
    dob: /\b(ngay\s*sinh|ngay\s*s\w{1,3}h|nam\s*sinh|date\s*of\s*b\w*|dateofb\w*|oate\s*of\s*b\w*)\b/,
    sex: /\b(gioi\s*tinh|gi[o0]\w{0,4}nh|s[eo][xc])\b/,
    nation: /\b(qu\w{1,2}c\s*ti[ch]h?|nationality|nabonal\w*)\b/,
    origin: /\b(que\s*quan|place\s*of\s*origin)\b/,
    residence: /\b(noi\s*(thuong\s*tru|cu\s*tru)|place\s*of\s*residence|thuong\s*tru)\b/,
    birth: /\b(noi\s*dang\s*ky\s*khai\s*sinh|noi\s*sinh|p[l/]?ace\s*of\s*birth)\b/,
    expiry: /\b(co\s*gia\s*tri(\s*den)?|date\s*of\s*exp\w*|het\s*han|gia\s*tri\s*den)\b/,
    id: /\b(so|no|so\s*\/?\s*no)\b\s*[:.]/,
  };
  const anyLabel = key => Object.values(LABELS).some(re => re.test(key)) || /\b(dan\s*toc|ton\s*giao|dac\s*diem|nhan\s*dang|ngay\s*cap|ngay,?\s*thang|cuc\s*truong|giam\s*doc|cong\s*hoa|doc\s*lap|can\s*cuoc|chung\s*minh|identity|socialist|republic|dinh\s*danh|bo\s*cong\s*an)\b/.test(key);

  // Bỏ "rác" OCR trong địa chỉ: từ có ký tự lạ, từ không có nguyên âm, từ IN HOA ngắn lẫn trong dòng có chữ thường.
  const KEEP_UP = new Set(['TP', 'TX', 'TT', 'KP', 'KV', 'HCM', 'TPHCM', 'P', 'Q', 'H', 'X']);
  const VOWEL = /[aeiouyăâêôơưáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]/i;
  function cleanAddr(s) {
    return s.split(/\r?\n/).map(line => {
      const mixed = /[a-zà-ỹ]/.test(line);
      return line.split(/\s+/).filter(t => {
        const w = t.replace(/^[,.;:]+|[,.;:]+$/g, '');
        if (!w) return false;
        if (/[\[\]{}|<>^~_=`"“”«»\\]/.test(w)) return false;
        if (/^\d/.test(w)) return true;
        if (!VOWEL.test(w)) return KEEP_UP.has(w.toUpperCase().replace(/\./g, ''));
        if (mixed && w === w.toUpperCase() && w.length <= 4 && !KEEP_UP.has(w.replace(/\./g, ''))) return false;
        return true;
      }).join(' ');
    }).join('\n');
  }

  // Dòng "rác" do OCR (ít chữ cái so với ký tự khác, vd "1 99 91 09V (gy3/2029 XE: TS ;i").
  const junk = l => { const t = l.replace(/\s/g, ''); return (t.match(/[A-Za-zÀ-ỹĐđ]/g) || []).length < 0.6 * t.length; };

  // Lấy giá trị của một nhãn: phần sau ":" cùng dòng, nếu rỗng thì dòng kế; `multi` gộp thêm dòng tiếp theo chưa phải nhãn.
  function valueOf(lines, keys, re, multi, colonOnly) {
    for (let i = 0; i < lines.length; i++) {
      if (!re.test(keys[i])) continue;
      const mt = keys[i].match(re);
      let rest = lines[i].slice(mt.index + mt[0].length), restKey = keys[i].slice(mt.index + mt[0].length);
      // Cắt tại nhãn kế tiếp trên cùng dòng (vd "Giới tính: Nam  Quốc tịch: Việt Nam").
      let cut = rest.length;
      for (const other of Object.values(LABELS)) { if (other === re) continue; const o = restKey.match(other); if (o && o.index < cut) cut = o.index; }
      rest = rest.slice(0, cut);
      // Trường chữ (tên, địa chỉ): chỉ nhận phần sau dấu ":" — phần nhãn tiếng Anh bị OCR đọc sai ("FXace ơf orig't") sẽ bị bỏ.
      if (colonOnly) { const c = rest.search(/[:;]/); rest = c >= 0 ? rest.slice(c + 1) : ''; }
      else rest = rest.replace(/^[^:;]*\/[^:;]*[:;]/, '');
      let first = clean(rest.replace(/^\s*(\/\s*[A-Za-z ]+?)?\s*[:;]/, '').replace(/^\s*\/\s*[A-Za-z ]+$/, ''));
      first = clean(first.replace(/^\/?\s*(full\s*name|date\s*of\s*birth|sex|nationality|place\s*of\s*(origin|residence)|date\s*of\s*expiry)\s*[:;]?/i, ''));
      if (first.replace(/[^A-Za-zÀ-ỹ0-9]/g, '').length < 3) first = ''; // chỉ còn rác 1-2 ký tự sau nhãn
      let parts = [];
      if (first) parts.push(first);
      let j = i + 1;
      if (!parts.length) {
        while (j < lines.length && (!clean(lines[j]) || junk(lines[j]))) j++;
        if (j < lines.length && !anyLabel(keys[j])) { parts.push(clean(lines[j])); j++; }
        else continue;
      }
      if (multi) while (j < lines.length && parts.length < 3 && clean(lines[j]) && !anyLabel(keys[j])) { if (!junk(lines[j])) parts.push(clean(lines[j])); j++; }
      return clean(parts.join('\n'));
    }
    return '';
  }

  // OCR hay làm rơi dấu ở họ và đệm phổ biến: khôi phục khi từ đó đang không dấu (chỉ họ ở đầu, THI/VAN ở giữa tên).
  const SURNAME = { NGUYEN: 'NGUYỄN', TRAN: 'TRẦN', LE: 'LÊ', PHAM: 'PHẠM', HUYNH: 'HUỲNH', VO: 'VÕ', DANG: 'ĐẶNG', BUI: 'BÙI', DO: 'ĐỖ', NGO: 'NGÔ', DUONG: 'DƯƠNG', LY: 'LÝ', DINH: 'ĐINH', DAO: 'ĐÀO', DOAN: 'ĐOÀN', TRUONG: 'TRƯƠNG', LUONG: 'LƯƠNG', PHAN: 'PHAN', VU: 'VŨ', HOANG: 'HOÀNG' };
  const MIDDLE = { THI: 'THỊ', VAN: 'VĂN' };
  function restoreName(name) {
    const w = name.split(/\s+/).filter(Boolean);
    if (w.length < 2) return name;
    const s0 = strip(w[0]).toUpperCase();
    if (SURNAME[s0]) w[0] = SURNAME[s0]; // họ: sửa cả khi OCR đọc sai dấu (NGUYÊN → NGUYỄN)
    for (let i = 1; i < w.length - 1; i++) if (MIDDLE[w[i]]) w[i] = MIDDLE[w[i]];
    return w.join(' ');
  }

  // Dòng MRZ mặt sau CCCD: "TRAN<<TAN<LUONG<<<<" → "TRAN TAN LUONG" (không dấu; họ phổ biến sẽ được khôi phục dấu).
  // Tên trên thẻ luôn in hoa: giữ dãy từ in hoa đầu dòng, bỏ rác chữ thường theo sau ("ĐINH THẾ QUANG gia ẤN" → "ĐINH THẾ QUANG").
  function upperRun(v) {
    const run = [];
    for (const t of v.split(/\s+/)) {
      const L = t.replace(/[^A-Za-zÀ-ỹĐđ]/g, '');
      if (!L) continue;
      if (L === L.toUpperCase()) run.push(t); else if (run.length) break;
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
    const lines = String(text || '').normalize('NFC').split(/\r?\n/).map(l => l.trim()).filter(Boolean);
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
    const tidy = v => v.replace(/[^A-ZÀ-ỹa-zà-ỹĐđ\s'.-]/g, ' ').replace(/^[\s.'-]+|[\s.'-]+$/g, '').replace(/\s{2,}/g, ' ').toUpperCase();
    out.name = tidy(upperRun(valueOf(lines, keys, LABELS.name, false, true)));
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

    // Giới tính: tìm "Nam"/"Nữ" ngay sau nhãn (cùng dòng hoặc dòng kế), không quét cả văn bản vì "Việt Nam", "năm sinh" cũng chứa "nam".
    for (let i = 0; i < lines.length && !out.sex; i++) {
      const m = keys[i].match(LABELS.sex); if (!m) continue;
      const seg = (keys[i].slice(m.index + m[0].length) + ' ' + (keys[i + 1] || '')).replace(/\bsex\b/g, ' ').replace(/qu\w{1,2}c\s*ti.*$/, '');
      const s = seg.match(/\b(nam|nu)\b/);
      if (s) out.sex = s[1] === 'nu' ? 'Nữ' : 'Nam';
    }

    // Quốc tịch: nhãn hay bị OCR sai, nên chỉ cần dòng "Việt Nam" không phải quốc hiệu và có dấu hiệu quốc tịch.
    const HEADER = /(cong hoa|nghia|socialist|republic|ministry|bo cong an|citizen|ctizen)/;
    if (keys.some(k => /\bviet\s*nam\b/.test(k) && !HEADER.test(k) && /(tich|nationa|nabon|naon|\bna\w{0,6}ty\b)/.test(k))) out.nation = 'Việt Nam';
    else out.nation = clean(valueOf(lines, keys, LABELS.nation, false).replace(/[^A-Za-zÀ-ỹ\s]/g, ''));

    const addr = re => clean(cleanAddr(valueOf(lines, keys, re, true, true))).replace(/\s*\n\s*/g, ', ').replace(/[\s.,;]+$/, '');
    out.origin = addr(LABELS.origin);
    out.residence = addr(LABELS.residence);
    // Thẻ CCCD mới không in "Quê quán": dùng "Nơi đăng ký khai sinh" thay thế.
    if (!out.origin) out.origin = addr(LABELS.birth);
    return out;
  }

  return { parse, strip };
});
