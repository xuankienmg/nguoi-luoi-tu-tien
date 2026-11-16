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
  async function prepare(file) {
    try {
      const bmp = await createImageBitmap(file);
      const scale = Math.min(3, Math.max(1, 2000 / bmp.width));
      const c = document.createElement('canvas');
      c.width = Math.round(bmp.width * scale); c.height = Math.round(bmp.height * scale);
      const g = c.getContext('2d', { willReadFrequently: true });
      g.imageSmoothingQuality = 'high'; g.drawImage(bmp, 0, 0, c.width, c.height);
      const im = g.getImageData(0, 0, c.width, c.height), d = im.data;
      for (let i = 0; i < d.length; i += 4) {
        const y = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        const v = Math.max(0, Math.min(255, (y - 128) * 1.35 + 128));
        d[i] = d[i + 1] = d[i + 2] = v;
      }
      g.putImageData(im, 0, 0);
      return c;
    } catch { return file; }
  }

  async function run() {
    if (working) return;
    if (!window.Tesseract) return setStatus('Chưa tải được bộ đọc ảnh (cần kết nối mạng). Hãy tải lại trang.', true);
    working = true; $('barWrap').hidden = false;
    try {
      if (!worker) { setStatus('Đang tải dữ liệu ngôn ngữ…'); worker = await Tesseract.createWorker('vie', 1, { langPath: 'https://cdn.jsdelivr.net/gh/tesseract-ocr/tessdata_best@4.1.0', gzip: false, logger: m => { if (cur && m.status === 'recognizing text') { $('bar').style.width = Math.round(m.progress * 100) + '%'; } } }); }
      let it;
      while ((it = items.find(i => i.state === 'wait'))) {
        cur = it; const idx = items.indexOf(it) + 1;
        mark(it, 'run', 'Đang đọc…'); setStatus(`Đang đọc ảnh ${idx}/${items.length}…`); $('bar').style.width = '0';
        try {
          const { data } = await worker.recognize(await prepare(it.file));
          it.raw = data.text; const r = CmndParse.parse(data.text);
          for (const [k, v] of Object.entries(r)) { const el = it.tr.querySelector(`[name=${k}]`); if (el) el.value = v; }
          const found = Object.values(r).filter(Boolean).length;
          found > 1 ? mark(it, 'done', 'Xong') : mark(it, 'err', 'Không nhận ra — thử ảnh rõ hơn');
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
    return [first, last, mid, gender(d.sex), d.id, $('dStation').value.trim(), $('dContract').value, $('dDept').value.trim(), d.phone, d.email,
      $('dJoin').value, isoDate(d.dob), d.residence, d.temp];
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
