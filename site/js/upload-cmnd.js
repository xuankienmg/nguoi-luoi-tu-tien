// Trang /upload-cmnd: chọn nhiều ảnh → OCR lần lượt (Tesseract.js, chạy trong trình duyệt) → bảng sửa được → Excel / JSON.
(() => {
  const $ = id => document.getElementById(id);
  const MAX = 10 * 1024 * 1024;
  const COLS = [['type', 'Loại giấy tờ'], ['id', 'Số giấy tờ'], ['name', 'Họ và tên'], ['dob', 'Ngày sinh'], ['sex', 'Giới tính'], ['nation', 'Quốc tịch'], ['origin', 'Quê quán'], ['residence', 'Nơi thường trú'], ['expiry', 'Có giá trị đến'], ['phone', 'Phone Number'], ['email', 'Email'], ['temp', 'Temporary Address']];
  const items = []; // { file, url, tr, state: 'wait'|'run'|'done'|'err' }
  let working = false, worker = null;
  window.cmndItems = items; // phục vụ kiểm thử / gỡ lỗi

  const setStatus = (t, err) => { $('status').textContent = t; $('status').classList.toggle('err', !!err); };
  const rowData = it => Object.fromEntries(COLS.map(([k]) => [k, it.tr.querySelector(`[name=${k}]`).value.trim()]));
  const done = () => items.filter(i => i.state === 'done' || i.state === 'err');

  function refreshButtons() {
    const has = done().length > 0;
    $('xlsx').disabled = $('copy').disabled = !has;
    $('clear').disabled = !items.length;
    $('tbl').hidden = !items.length;
  }

  function addRow(it) {
    const tr = document.createElement('tr');
    const img = `<td class="thumb"><img alt="" src="${it.url}"><span class="st">Đang chờ…</span><button type="button" class="raw" hidden title="Sao chép văn bản OCR gốc (để báo lỗi đọc sai)">Văn bản OCR</button></td>`;
    tr.innerHTML = img + COLS.map(([k]) => `<td><input name="${k}"${k === 'type' ? ' readonly' : ''}></td>`).join('') +
      '<td><button type="button" class="x" aria-label="Xóa ảnh này">×</button></td>';
    tr.querySelector('.x').onclick = () => {
      const i = items.indexOf(it); if (i < 0 || it.state === 'run') return;
      items.splice(i, 1); URL.revokeObjectURL(it.url); tr.remove(); refreshButtons();
    };
    tr.querySelector('.raw').onclick = async () => {
      try { await navigator.clipboard.writeText((it.raw || []).join('\n\n===== lượt kế =====\n\n')); setStatus('Đã sao chép văn bản OCR của ảnh này.'); }
      catch { setStatus('Không sao chép được — trình duyệt chặn clipboard.', true); }
    };
    it.tr = tr; $('rows').appendChild(tr);
  }

  const mark = (it, state, text) => { it.state = state; const s = it.tr.querySelector('.st'); s.textContent = text; s.classList.toggle('err', state === 'err'); };

  function add(files) {
    let skipped = 0;
    for (const f of files) {
      if (!/^image\//.test(f.type) || f.size > MAX) { skipped++; continue; }
      const it = { file: f, url: URL.createObjectURL(f), state: 'wait' };
      items.push(it); addRow(it);
    }
    if (skipped) setStatus(`Bỏ qua ${skipped} tệp không phải ảnh hoặc quá 10 MB.`, true);
    refreshButtons(); run();
  }

  // Tiền xử lý để OCR giữ được dấu tiếng Việt: phóng ảnh nhỏ lên (~2000px ngang), chuyển xám, tăng tương phản.
  // pass 1: cách chính. pass 2 (chỉ chạy khi còn ô trống): ảnh nhỏ phóng x3 không tăng tương phản; ảnh lớn giữ màu gốc.
  async function prepare(file, pass = 1) {
    let bmp;
    try { bmp = await createImageBitmap(file); }
    catch { throw new Error('Trình duyệt không mở được ảnh này (định dạng HEIC/ảnh hỏng?). Hãy chuyển sang JPG/PNG.'); }
    {
      // Ảnh nhỏ (< 1000px): phóng lên ~2000px + tăng tương phản. Ảnh lớn: giữ nguyên cỡ, chỉ chuyển xám (phóng/tăng tương phản làm OCR đọc sai dấu).
      // Luôn trả về canvas: Tesseract.js không nhận ImageBitmap ("Error attempting to read image").
      const small = bmp.width < 1000, keepColor = pass === 2 && !small;
      const contrast = small && pass === 1 ? 1.35 : 1;
      const scale = small ? Math.min(3, (pass === 1 ? 2000 : 3 * bmp.width) / bmp.width) : 1;
      const c = document.createElement('canvas');
      c.width = Math.round(bmp.width * scale); c.height = Math.round(bmp.height * scale);
      const g = c.getContext('2d', { willReadFrequently: true });
      g.imageSmoothingQuality = 'high'; g.drawImage(bmp, 0, 0, c.width, c.height);
      if (keepColor) return c;
      const im = g.getImageData(0, 0, c.width, c.height), d = im.data;
      for (let i = 0; i < d.length; i += 4) {
        const y = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        const v = Math.max(0, Math.min(255, (y - 128) * contrast + 128));
        d[i] = d[i + 1] = d[i + 2] = v;
      }
      g.putImageData(im, 0, 0);
      return c;
    }
  }

  // Giải mã QR trên CCCD gắn chip. Ưu tiên zxing-wasm (đọc được QR chụp nghiêng/mờ tốt hơn jsQR nhiều), jsQR là dự phòng khi không tải được zxing.
  // Thử các vùng từ rẻ → đắt (xem CmndParse.qrWindows): cả ảnh → góc trên-phải thẻ (theo vị trí chữ OCR) → quét cửa sổ trượt (có giới hạn thời gian).
  // Thẻ mẫu mới ("Số định danh cá nhân") không có QR mặt trước nên chỉ thử cả ảnh.
  async function readQr(file, box, ocrW, text) {
    if (!window.ZXingWASM && !window.jsQR) return null;
    try {
      const bmp = await createImageBitmap(file), W = bmp.width, H = bmp.height, k = ocrW ? W / ocrW : 1;
      const bx = box && { x: box.x * k, y: box.y * k, w: box.w * k, h: box.h * k };
      const noQr = /dinh\s*danh|personal\s*ident/.test(CmndParse.strip(text || ''));
      const wins = CmndParse.qrWindows(W, H, bx), t0 = Date.now();
      for (let j = 0; j < wins.length; j++) {
        if (noQr && j > 0) break;
        if (j >= (bx ? 3 : 1) && Date.now() - t0 > 8000) break; // phần quét trượt: tối đa ~8 giây / ảnh
        const [x, y, w, h] = wins[j];
        for (const t of j === 0 ? [800, 1300] : [500, 800]) {
          const sc = Math.min(4, Math.max(0.5, t / Math.min(w, h))), cw = Math.round(w * sc), ch = Math.round(h * sc);
          if (cw * ch > 5e6) continue;
          const c = document.createElement('canvas'); c.width = cw; c.height = ch;
          const g = c.getContext('2d', { willReadFrequently: true }); g.imageSmoothingQuality = 'high'; g.drawImage(bmp, x, y, w, h, 0, 0, cw, ch);
          for (const norm of [false, true]) {
            const im = g.getImageData(0, 0, cw, ch);
            if (norm) stretch(im.data);
            const raw = window.ZXingWASM
              ? (await ZXingWASM.readBarcodes(im, { formats: ['QRCode'], tryHarder: true, tryRotate: true, tryInvert: true, maxNumberOfSymbols: 1 }))[0]?.text
              : jsQR(im.data, cw, ch, { inversionAttempts: 'attemptBoth' })?.data;
            const qr = raw && CmndParse.parseQr(raw);
            if (qr) return qr;
          }
        }
      }
    } catch { /* không đọc được QR → chỉ dùng OCR */ }
    return null;
  }
  // Chuyển xám + kéo giãn tương phản (tương đương normalise) tại chỗ.
  function stretch(d) {
    let lo = 255, hi = 0;
    for (let i = 0; i < d.length; i += 4) { const y = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2]; d[i] = y; if (y < lo) lo = y; if (y > hi) hi = y; }
    const m = hi > lo ? 255 / (hi - lo) : 1;
    for (let i = 0; i < d.length; i += 4) d[i] = d[i + 1] = d[i + 2] = (d[i] - lo) * m;
  }

  async function run() {
    if (working) return;
    if (!window.Tesseract) return setStatus('Chưa tải được bộ đọc ảnh (cần kết nối mạng). Hãy tải lại trang.', true);
    working = true; $('barWrap').hidden = false;
    try {
      if (!worker) { setStatus('Đang tải dữ liệu ngôn ngữ…'); worker = await loadWorker(); }
      let it;
      while ((it = items.find(i => i.state === 'wait'))) {
        cur = it; const idx = items.indexOf(it) + 1;
        mark(it, 'run', 'Đang đọc…'); setStatus(`Đang đọc ảnh ${idx}/${items.length}…`); $('bar').style.width = '0';
        try {
          const canvas = await prepare(it.file);
          const { data } = await worker.recognize(canvas);
          it.raw = [data.text]; let r = CmndParse.parse(data.text);
          const ids = [r.id]; // số đọc được ở từng lượt → bầu theo từng chữ số khi đủ 3 lượt
          // Thẻ nhỏ trong ảnh rộng / nền rối: cắt đúng vùng thẻ (theo vị trí các chữ đọc chắc), phóng to, đọc lại rồi ghép từng ô với lượt đầu.
          const box = CmndParse.cardBox(data.words, canvas.width, canvas.height);
          if (box) {
            mark(it, 'run', 'Đang đọc vùng thẻ…');
            try { // lượt cắt lỗi thì vẫn giữ kết quả lượt 1
              const cw = Math.min(2400, Math.max(1600, box.w)), c2 = document.createElement('canvas');
              c2.width = cw; c2.height = Math.round(box.h * cw / box.w);
              const g2 = c2.getContext('2d'); g2.imageSmoothingQuality = 'high'; g2.drawImage(canvas, box.x, box.y, box.w, box.h, 0, 0, c2.width, c2.height);
              const t2 = (await worker.recognize(c2)).data.text; it.raw.push(t2);
              const rc = CmndParse.parse(t2); ids.push(rc.id);
              r = CmndParse.merge(r, rc);
            } catch { /* bỏ qua */ }
          }
          // Còn ô trống → đọc thêm một lượt với cách xử lý ảnh khác, chỉ điền vào ô trống.
          if (['id', 'name', 'dob', 'sex', 'origin', 'residence', 'expiry'].some(k => !r[k])) {
            mark(it, 'run', 'Đang đọc lượt 2…');
            try { // lượt 2 lỗi thì vẫn giữ kết quả lượt 1
              const t3 = (await worker.recognize(await prepare(it.file, 2))).data.text; it.raw.push(t3);
              const r2 = CmndParse.parse(t3); ids.push(r2.id);
              const better = CmndParse.merge(r, r2).name; // tên: lượt này đọc rõ hơn thì thay
              for (const k of Object.keys(r)) if (!r[k] && r2[k]) r[k] = r2[k];
              r.name = better || r.name;
            } catch { /* bỏ qua */ }
          }
          const voted = CmndParse.voteId(ids);
          if (voted) { r.id = voted; r.type = 'CCCD'; }
          // Mã QR (nếu đọc được) chính xác hơn OCR: ghi đè số, tên, ngày sinh, giới tính, thường trú; suy hạn dùng từ ngày cấp.
          mark(it, 'run', 'Đang đọc mã QR…');
          const qr = await readQr(it.file, box, canvas.width, data.text);
          it.qr = qr;
          if (qr) {
            Object.assign(r, { id: qr.id, type: 'CCCD', name: qr.name || r.name, dob: qr.dob || r.dob, sex: qr.sex || r.sex,
              residence: qr.residence || r.residence, nation: r.nation || 'Việt Nam' });
            const exp = CmndParse.expiryFrom(r.dob, qr.issued);
            if (exp) r.expiry = exp; // suy từ ngày cấp trong QR chắc hơn OCR (OCR hay đọc nhầm ngày cấp / ngày sai thành hạn)
          }
          r.origin = CmndParse.completeOrigin(r.origin, r.residence);
          it.tr.querySelector('.raw').hidden = false;
          for (const [k, v] of Object.entries(r)) { const el = it.tr.querySelector(`[name=${k}]`); if (el) el.value = v; }
          const found = Object.values(r).filter(Boolean).length;
          found > 1 ? mark(it, 'done', qr ? 'Xong (có QR)' : 'Xong') : mark(it, 'err', 'Không nhận ra — thử ảnh rõ hơn');
        } catch (e) { mark(it, 'err', 'Lỗi: ' + (e && e.message || e)); }
        refreshButtons();
      }
      setStatus(`Đã xử lý ${items.length} ảnh. Hãy kiểm tra lại bảng.`);
    } catch (e) {
      setStatus('Lỗi khi đọc ảnh: ' + (e && e.message || e), true);
      items.forEach(i => { if (i.state === 'wait' || i.state === 'run') mark(i, 'err', 'Lỗi'); });
    } finally { cur = null; working = false; $('barWrap').hidden = true; refreshButtons(); }
  }
  let cur = null;

  // Dữ liệu tiếng Việt: bản "best_int" nén (~1,4 MB, cùng độ chính xác với tessdata_best ~ chục MB mà CDN tải rất chậm).
  // Bắt đầu tải ngay khi mở trang, hiện % tiến độ; trình duyệt lưu lại (IndexedDB) nên lần sau gần như tức thì.
  let workerPromise = null;
  function loadWorker() {
    if (!workerPromise) workerPromise = Tesseract.createWorker('vie', 1, {
      langPath: 'https://cdn.jsdelivr.net/npm/@tesseract.js-data/vie@1.0.0/4.0.0_best_int',
      logger: m => {
        if (m.status === 'recognizing text') { if (cur) $('bar').style.width = Math.round(m.progress * 100) + '%'; }
        else if (!worker && /loading|initializ/.test(m.status) && items.length) {
          $('barWrap').hidden = false; $('bar').style.width = Math.round((m.progress || 0) * 100) + '%';
          setStatus(`Đang tải dữ liệu ngôn ngữ… ${Math.round((m.progress || 0) * 100)}%`);
        }
      },
    }).catch(e => { workerPromise = null; throw e; });
    return workerPromise;
  }
  if (window.Tesseract) loadWorker().then(w => { worker = w; }).catch(() => { /* thử lại khi người dùng chọn ảnh */ });

  const drop = $('drop');
  $('file').addEventListener('change', e => { add([...e.target.files]); e.target.value = ''; });
  drop.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); $('file').click(); } });
  ['dragenter', 'dragover'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.add('over'); }));
  ['dragleave', 'drop'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.remove('over'); }));
  drop.addEventListener('drop', e => add([...e.dataTransfer.files]));
  document.addEventListener('paste', e => { const fs = [...(e.clipboardData?.files || [])]; if (fs.length) add(fs); });

  $('clear').addEventListener('click', () => {
    if (working) return setStatus('Đang đọc ảnh, vui lòng đợi xong rồi xóa.', true);
    items.splice(0).forEach(i => URL.revokeObjectURL(i.url)); $('rows').innerHTML = ''; setStatus(''); refreshButtons();
  });

  // Xuất Excel theo template nhập nhân sự (14 cột). Tên Việt: Last Name = họ (từ đầu), First Name = tên (từ cuối), Middle Name = phần giữa.
  const HEAD = ['First Name *', 'Last Name *', 'Middle Name', 'Gender *', 'National ID *', 'Station *', 'Contract Type *', 'Department', 'Phone Number *', 'Email', 'Joined Date (YYYY-MM-DD) *', 'Date of Birth (YYYY-mm-dd) *', 'Permanent Address *', 'Temporary Address *'];
  const isoDate = v => { const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(v || ''); return m ? `${m[3]}-${m[2].padStart(2, '0')}-${m[1].padStart(2, '0')}` : (v || ''); };
  const gender = v => /^n[ữu]$/i.test(v) ? 'Female' : /^nam$/i.test(v) ? 'Male' : '';
  function templateRow(it) {
    const d = rowData(it), w = d.name.split(/\s+/).filter(Boolean);
    const last = w.length > 1 ? w[0] : '', first = w.length > 1 ? w[w.length - 1] : (w[0] || ''), mid = w.slice(1, -1).join(' ');
    return [first, last, mid, gender(d.sex), d.id, '', '', '', d.phone, d.email,
      '', isoDate(d.dob), d.residence, d.temp];
  }
  $('xlsx').addEventListener('click', () => {
    if (!window.XLSX) return setStatus('Chưa tải được thư viện Excel (cần kết nối mạng).', true);
    const list = done();
    const rows = list.map(templateRow);
    const aoa = [HEAD, ...rows];
    const ws = XLSX.utils.aoa_to_sheet(aoa);
    // Số CMND/CCCD và số điện thoại lưu dạng chữ để không mất số 0 đầu.
    for (let r = 2; r <= aoa.length; r++) for (const col of ['E', 'I']) { const c = ws[col + r]; if (c) { c.t = 's'; c.z = '@'; } }
    ws['!cols'] = HEAD.map((h, c) => ({ wch: Math.min(48, Math.max(h.length, ...rows.map(r => String(r[c] ?? '').length)) + 3) }));
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Staff');
    XLSX.writeFile(wb, 'staff-' + new Date().toISOString().slice(0, 10) + '.xlsx');
    // Nhắc các ô bắt buộc (*) còn trống — file vẫn được xuất để bạn bổ sung trong Excel.
    const req = HEAD.map((h, c) => h.includes('*') ? c : -1).filter(c => c >= 0);
    const missing = rows.reduce((n, r) => n + req.filter(c => !String(r[c] ?? '').trim()).length, 0);
    setStatus(`Đã xuất Excel (${rows.length} dòng).` + (missing ? ` Còn ${missing} ô bắt buộc (*) trống, hãy bổ sung.` : ''), !!missing);
  });

  $('copy').addEventListener('click', async () => {
    try { await navigator.clipboard.writeText(JSON.stringify(done().map(rowData), null, 2)); setStatus('Đã sao chép JSON.'); }
    catch { setStatus('Không sao chép được — trình duyệt chặn clipboard.', true); }
  });
})();
