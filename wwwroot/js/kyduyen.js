/* js/kyduyen.js: Kỳ duyên Thiên Thư: 4 khung giờ mỗi ngày, chọn một trong hai lối, gom 10 quyển Thiên Thư. */
const KD_SEEN = 'van-dao-kd-seen-' + MODE;   // mỗi cửa sổ (dải / bảng / web) nhớ riêng, tránh cửa sổ này "nuốt" thông báo của cửa sổ kia
const kdTime = ms => new Date(ms).toLocaleTimeString('vi-VN', {hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh'})   // giờ Việt Nam, khớp 08 · 12 · 19 · 22 giờ;
const kdAttr = s => String(s ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;');
function kdPath(d){ return d >= 10 ? 'Chính' : d <= -10 ? 'Tà' : 'Trung'; }
/** Lời đáp chỉ ghi tên lối chọn: tỉ lệ, giá, phần thưởng giấu tới khi chọn. */
const kdChoices = (o, off) => o.choices.map((c, i) => `<button type="button" data-kd="${i}" ${i === 2 ? 'class="kd-skip"' : ''} ${off ? 'disabled' : ''}>${c.label}</button>`).join('');   // lời đáp thứ ba (2) luôn là "Bỏ đi"
let kdResult = null, kdResultTimer = 0, kdCardSig = '';   // kết quả vừa chọn: hiện trên màn hình chính vài giây
const KD_WINDOW = 600000;   // mỗi khung mở 10 phút (KyDuyen.Window)
/** Thanh đếm ngược tới lúc khung đóng + số mm:ss; kdTick cập nhật mỗi giây. */
const kdLeft = end => Math.max(0, end - Date.now());
const kdBar = end => `${Math.min(100, kdLeft(end) / KD_WINDOW * 100)}%`;
const kdClock = end => { const s = Math.ceil(kdLeft(end) / 1000); return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
function kdTimer(o){
  return `<span class="kd-timer-row" data-tip="Còn tới ${kdTime(o.endsAt)}"><span class="kd-timer"><i data-kd-end="${o.endsAt}" style="width:${kdBar(o.endsAt)}"></i></span><em data-kd-end="${o.endsAt}">${kdClock(o.endsAt)}</em></span>`;
}
/** Đếm ngược dài h:mm:ss tới lúc khung kế tiếp mở. */
const kdLong = at => { const s = Math.max(0, Math.ceil((at - Date.now()) / 1000)); return `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
function kdTick(){
  document.querySelectorAll('[data-kd-end]').forEach(el => {
    if (el.tagName === 'I') el.style.width = kdBar(+el.dataset.kdEnd); else el.textContent = kdClock(+el.dataset.kdEnd);
  });
  document.querySelectorAll('[data-kd-next]').forEach(el => { el.textContent = kdLong(+el.dataset.kdNext); });
}
setInterval(kdTick, 1000);

const KD_HOURS = [8, 12, 19, 22], KD_VN = 7 * 3600000;   // khớp KyDuyen.Hours, giờ Việt Nam
let kdBook = -1;   // quyển Thiên Thư đang xem lời tựa (-1 = quyển đang thu thập)
/** Bốn khung trong ngày (giờ VN) của khung đang mở, hoặc của khung kế tiếp (sau 22:10 là ngày mai): đã qua · đang mở · kế tiếp · sắp tới. */
function kdSlots(k){
  const now = Date.now(), ref = k.open ? k.open.endsAt - KD_WINDOW : k.nextAt, day = Math.floor((ref + KD_VN) / 86400000) * 86400000 - KD_VN;
  return KD_HOURS.map(h => {
    const at = day + h * 3600000, open = !!k.open && now >= at && now < at + KD_WINDOW;
    const st = open ? 'open' : at === k.nextAt ? 'next' : at + KD_WINDOW <= now ? 'past' : 'later';
    const note = st === 'open' ? `<em data-kd-end="${k.open.endsAt}">${kdClock(k.open.endsAt)}</em>`
      : st === 'next' ? `<em data-kd-next="${k.nextAt}">${kdLong(k.nextAt)}</em>` : `<span>${st === 'past' ? 'Đã qua' : 'Sắp tới'}</span>`;
    return `<li class="kd-slot ${st}"><b>${String(h).padStart(2, '0')}:00</b>${note}</li>`;
  }).join('');
}
/** Thẻ kỳ duyên đang mở, hoặc màn chờ khi ngoài khung. */
function kdStageHtml(k){
  if (!k.open) return `<section class="kd-stage kd-idle" aria-label="Kỳ duyên">
      <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>
      <h3>Chưa có kỳ duyên</h3>
      <p><span>Khung kế tiếp lúc</span> <b>${kdTime(k.nextAt)}</b>. <span>Cứ tiếp tục treo máy, kỳ duyên sẽ tự tìm đến.</span></p>
    </section>`;
  return `<section class="kd-stage" aria-label="Kỳ duyên">
      <div class="kd-art" aria-hidden="true"><svg viewBox="0 0 800 200" preserveAspectRatio="xMidYMax slice">
        <path d="M0 200V120l70-40 60 30 90-70 80 60 70-40 110 70 90-60 100 50 130-40v120Z" fill="#162722"/>
        <path d="M0 200v-40l120-30 90 20 140-50 100 40 120-30 110 30 120-20v80Z" fill="#0f1c18"/>
        <circle cx="400" cy="120" r="18" fill="none" stroke="#e3c27a" stroke-opacity=".55" stroke-width="1.5"/>
        <circle cx="400" cy="120" r="32" fill="none" stroke="#e3c27a" stroke-opacity=".22"/>
        <circle cx="620" cy="40" r="3" fill="#f1d79a" fill-opacity=".5"/><circle cx="180" cy="60" r="2" fill="#f1d79a" fill-opacity=".4"/></svg>
        <span class="kd-pill">Kỳ duyên</span>
      </div>
      <div class="kd-body">
        <h3>${k.open.title}</h3>${kdTimer(k.open)}
        <p>${k.open.text}</p>
        <div class="kd-choices">${kdChoices(k.open, busy || k.blocked)}</div>
        <small class="kd-note">Hết giờ chưa chọn thì kỳ duyên tan biến, không mất gì.</small>
      </div>
    </section>`;
}
/** Đạo Tâm: thanh Tà ↔ Chính (hiện trong khoảng ±2 lần ngưỡng) + ba cái kết. */
function kdHeartHtml(k){
  const span = 20, pct = (Math.max(-span, Math.min(span, k.daoTam)) + span) / (2 * span) * 100, path = kdPath(k.daoTam);
  const ends = (k.endings || []).map(e => `<li class="kd-endopt ${e.path === 'Chính' ? 'chinh' : e.path === 'Tà' ? 'ta' : 'trung'}${e.path === path ? ' on' : ''}${k.ending === e.name ? ' got' : ''}">
      <b>${e.name}</b><small>${e.path === 'Chính' ? 'Đạo Tâm ≥ +10' : e.path === 'Tà' ? 'Đạo Tâm ≤ −10' : 'Đạo Tâm −9 đến +9'}</small><span>${e.bonus}</span></li>`).join('');
  return `<aside class="kd-heart" aria-label="Đạo Tâm">
      <h3>Đạo Tâm <small><b>${k.daoTam > 0 ? '+' : ''}${k.daoTam}</b> · <span>${path}</span></small></h3>
      <div class="kd-meter" role="img" aria-label="Đạo Tâm ${k.daoTam}"><i style="left:${pct}%"></i></div>
      <div class="kd-scale"><span>Tà</span><span>Trung</span><span>Chính</span></div>
      ${k.ending ? `<p class="kd-end"><b>${k.ending}</b> ${k.endingText}</p>` : '<p class="kd-hint">Mỗi lựa chọn đẩy Đạo Tâm về một phía. Gom đủ mười quyển Thiên Thư, Đạo Tâm quyết định cái kết.</p>'}
      ${ends ? `<h4>Ba cái kết</h4><ul>${ends}</ul>` : ''}
    </aside>`;
}
/** Thiên Thư: lưới 10 quyển, 9 vạch trang mỗi quyển; bấm quyển đã đủ để đọc lời tựa. */
function kdBooksHtml(k){
  const ROMAN = ['I','II','III','IV','V','VI','VII','VIII','IX','X'];
  const pages = k.books.reduce((n, b) => n + b.pages, 0), need = k.books.reduce((n, b) => n + b.need, 0), done = k.books.filter(b => b.pages >= b.need).length;
  const cur = k.books.findIndex(b => b.open && b.pages < b.need), sel = kdBook >= 0 && kdBook < k.books.length ? kdBook : Math.max(0, cur);
  const cards = k.books.map((b, i) => {
    const full = b.pages >= b.need, st = full ? 'done' : !b.open ? 'lock' : b.pages > 0 || i === cur ? 'cur' : '';
    const title = b.name;   // giữ nguyên tên đầy đủ: bản dịch tra theo cả chuỗi
    const pips = Array.from({length: b.need}, (_, j) => `<i${j < b.pages ? ' class="on"' : ''}></i>`).join('');
    const sub = !b.open ? (DATA?.realms?.[i]?.name ? `Mở ở cảnh giới ${DATA.realms[i].name}` : 'Mở ở cảnh giới kế') : full ? 'Đã đủ' : i === cur ? 'Đang thu thập' : 'Chưa có trang';
    return `<li><button type="button" class="kd-book ${st}${i === sel ? ' sel' : ''}" data-kd-book="${i}" aria-pressed="${i === sel}" aria-label="${kdAttr(b.name)}">
        <span class="kd-bk-top"><em>${ROMAN[i] || i + 1}</em><small>${b.open ? `${b.pages}/${b.need}` : ''}</small></span>
        <b>${title}</b><span class="kd-bk-bonus">${b.bonus}</span>
        <span class="kd-pips" aria-hidden="true">${pips}</span><small class="kd-bk-sub">${sub}</small></button></li>`;
  }).join('');
  const sb = k.books[sel];
  const lore = sb ? `<div class="kd-lore"><span aria-hidden="true">“</span><div><b>${sb.name}</b>
      <p>${sb.lore || (sb.open ? 'Gom đủ trang để đọc lời tựa của quyển này.' : 'Quyển này chưa mở.')}</p></div></div>` : '';
  return `<section class="kd-tome" aria-label="Thiên Thư">
      <h3>Thiên Thư <small><span>${pages}/${need} trang</span> · <span>${done}/${k.books.length} quyển</span></small></h3>
      <ol class="kd-grid">${cards}</ol>${lore}
    </section>`;
}

function renderKyDuyen(){
  const pane = $('pane-kyduyen'), k = S?.kyDuyen; if (!pane || !k) return;
  const warn = !k.online ? '<p class="sp-ev-warn">Mất kết nối máy chủ: kỳ duyên chỉ xuất hiện khi online.</p>'
    : k.blocked ? `<p class="sp-ev-warn">${k.blocked}</p>` : '';
  pane.innerHTML = `<div class="kd2">${warn}
    <section class="kd-sched" aria-label="Lịch kỳ duyên">
      <div class="kd-sched-h"><small>${k.open ? 'Khung đang mở' : 'Khung kế tiếp'}</small><b>${k.open ? kdTime(k.open.endsAt - KD_WINDOW) : kdTime(k.nextAt)}</b></div>
      <ol>${kdSlots(k)}</ol>
      <p>Mỗi khung mở 10 phút. Bỏ lỡ khung nào thì chờ khung sau.</p>
    </section>
    <div class="kd-row">${kdStageHtml(k)}${kdHeartHtml(k)}</div>
    ${kdBooksHtml(k)}</div>`;
}

/** Thẻ kỳ duyên ngay trên màn hình chính (dải game / bản web): câu hỏi + lời đáp, rồi kết quả sau khi chọn. Cửa sổ Tu luyện không có thẻ. */
function renderKdCard(){
  const wrap = document.querySelector('.stage-wrap'); if (!wrap || MODE === 'panel') return;
  let card = $('kdCard');
  if (!card){ card = document.createElement('div'); card.id = 'kdCard'; card.className = 'kd-card'; card.hidden = true; wrap.appendChild(card); }
  const k = S?.kyDuyen, o = k?.open;
  const sig = kdResult ? 'r|' + kdResult : o ? `o|${o.key}|${o.title}|${o.text}|${k.blocked || ''}` : '';   // không khóa theo busy: act() tự chặn bấm trùng; tiêu đề: admin đổi kỳ duyên trong cùng khung; lời kể: sang câu hỏi thứ hai
  if (sig === kdCardSig) return;                  // đồng bộ 4 giây/lần: không vẽ lại khi không đổi (giữ hover, không nháy)
  kdCardSig = sig;
  card.hidden = !sig;
  if (kdResult) card.innerHTML = `<b class="kd-card-t">✦ Kỳ duyên</b><p>${kdResult}</p>`;
  else if (o) card.innerHTML = `<b class="kd-card-t">✦ ${o.title}</b>${kdTimer(o)}<p>${o.text}</p><div class="kd-choices">${kdChoices(o, !!k.blocked)}</div>`;
}

/** Gọi mỗi lần nhận state: chấm đỏ trên tab, thẻ trên màn hình chính, báo khay hệ thống khi kỳ duyên mới xuất hiện (chỉ dải game báo để không lặp). */
function kyDuyenNotice(){
  const o = S?.kyDuyen?.open, tab = $('tab-kyduyen');
  if (tab){ tab.hidden = !S?.kyDuyen; tab.classList.toggle('kd-dot', !!o); }   // máy chủ cũ không có kỳ duyên → ẩn tab
  $('tab-g-tuluyen')?.classList.toggle('kd-dot', !!o);                          // Kỳ duyên nằm trong nhóm Tu luyện: chấm cả trên tab nhóm
  renderKdCard();
  if (!o || MODE !== 'strip') return;
  let seen = ''; try { seen = localStorage.getItem(KD_SEEN) || ''; } catch(e){}
  const mark = S.id + '|' + o.key;                // theo nhân vật: đổi nhân vật vẫn báo
  if (seen === mark) return;
  try { localStorage.setItem(KD_SEEN, mark); } catch(e){}
  notifyHost('kyduyen', {title: o.title});        // host chỉ hiện bóng thông báo khi dải đang ẩn
}

document.addEventListener('click', e => {
  const bk = e.target.closest('#pane-kyduyen [data-kd-book]'); if (!bk) return;
  kdBook = +bk.dataset.kdBook; renderKyDuyen();
});
document.addEventListener('click', async e => {
  const b = e.target.closest('#pane-kyduyen [data-kd], #kdCard [data-kd]'); if (!b || b.disabled) return;
  if (await act(`kyduyen?c=${b.dataset.kd}`) && !S.kyDuyen?.open){   // còn câu hỏi thứ hai: chưa có kết quả
    const log = (S.logs || []).find(l => l.text.startsWith('Kỳ duyên · '));
    kdResult = log ? log.text.replace(/^Kỳ duyên · [^:]*: /, '') : null;   // lời kể + thu hoạch, bỏ tiền tố đã có ở tiêu đề thẻ
    clearTimeout(kdResultTimer); kdResultTimer = setTimeout(() => { kdResult = null; renderKdCard(); }, 6000);
  }
  renderKyDuyen(); renderKdCard();   // act() vẽ lại khi còn busy (nút bị khóa) → vẽ lại sau khi xong
});
// Admin "Kích hoạt kỳ duyên": quên mọi dấu đã xem để dải game báo khay hệ thống lại
document.addEventListener('click', e => {
  if (!e.target.closest('[data-adm="kyduyen"]')) return;
  try { Object.keys(localStorage).filter(k => k.startsWith('van-dao-kd-seen-')).forEach(k => localStorage.removeItem(k)); } catch(err){}
});
