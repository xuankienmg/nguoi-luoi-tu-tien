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
    const img = `<td class="thumb"><img alt="" src="${it.url}"><span class="st">Đang chờ…</span></td>`;
    tr.innerHTML = img + COLS.map(([k]) => `<td><input name="${k}"${k === 'type' ? ' readonly' : ''}></td>`).join('') +
      '<td><button type="button" class="x" aria-label="Xóa ảnh này">×</button></td>';
    tr.querySelector('.x').onclick = () => {
      const i = items.indexOf(it); if (i < 0 || it.state === 'run') return;
      items.splice(i, 1); URL.revokeObjectURL(it.url); tr.remove(); refreshButtons();
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

  // Giải mã QR trên CCCD gắn chip (jsQR). Thử cả ảnh và góc trên-phải (vị trí QR mặt trước) ở vài mức phóng to.
  async function readQr(file) {
    if (!window.jsQR) return null;
    try {
      const bmp = await createImageBitmap(file), W = bmp.width, H = bmp.height;
      const regions = [[0, 0, W, H], [W * 0.55, 0, W * 0.45, H * 0.6], [W * 0.5, 0, W * 0.5, H * 0.5]];
      for (const [x, y, w, h] of regions) for (const scale of [1, 2, 3]) {
        const cw = Math.round(w * scale), ch = Math.round(h * scale);
        if (cw * ch > 9e6) continue;
        const c = document.createElement('canvas'); c.width = cw; c.height = ch;
        const g = c.getContext('2d', { willReadFrequently: true }); g.imageSmoothingQuality = 'high'; g.drawImage(bmp, x, y, w, h, 0, 0, cw, ch);
        const hit = jsQR(g.getImageData(0, 0, cw, ch).data, cw, ch, { inversionAttempts: 'attemptBoth' });
        const qr = hit && CmndParse.parseQr(hit.data);
        if (qr) return qr;
      }
    } catch { /* không đọc được QR → chỉ dùng OCR */ }
    return null;
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
          const { data } = await worker.recognize(await prepare(it.file));
          it.raw = data.text; const r = CmndParse.parse(data.text);
          // Còn ô trống → đọc thêm một lượt với cách xử lý ảnh khác, chỉ điền vào ô trống.
          if (['id', 'name', 'dob', 'sex', 'origin', 'residence', 'expiry'].some(k => !r[k])) {
            mark(it, 'run', 'Đang đọc lượt 2…');
            try { // lượt 2 lỗi thì vẫn giữ kết quả lượt 1
              const r2 = CmndParse.parse((await worker.recognize(await prepare(it.file, 2))).data.text);
              for (const k of Object.keys(r)) if (!r[k] && r2[k]) r[k] = r2[k];
            } catch { /* bỏ qua */ }
          }
          // Mã QR (nếu đọc được) chính xác hơn OCR: ghi đè số, tên, ngày sinh, giới tính, thường trú; suy hạn dùng từ ngày cấp.
          const qr = await readQr(it.file);
          it.qr = qr;
          if (qr) {
            Object.assign(r, { id: qr.id, type: 'CCCD', name: qr.name || r.name, dob: qr.dob || r.dob, sex: qr.sex || r.sex,
              residence: qr.residence || r.residence, nation: r.nation || 'Việt Nam' });
            const exp = CmndParse.expiryFrom(r.dob, qr.issued);
            if (exp && (!r.expiry || !/^\d{2}\/\d{2}\/\d{4}$/.test(r.expiry))) r.expiry = exp;
          }
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
