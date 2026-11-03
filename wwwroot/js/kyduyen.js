/* Vấn Đạo · js/kyduyen.js: Kỳ duyên Thiên Thư: 4 khung giờ mỗi ngày, chọn một trong hai lối, gom 10 quyển Thiên Thư. */
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
function kdTick(){
  document.querySelectorAll('[data-kd-end]').forEach(el => {
    if (el.tagName === 'I') el.style.width = kdBar(+el.dataset.kdEnd); else el.textContent = kdClock(+el.dataset.kdEnd);
  });
}
setInterval(kdTick, 1000);

function renderKyDuyen(){
  const pane = $('pane-kyduyen'), k = S?.kyDuyen; if (!pane || !k) return;
  const warn = !k.online ? '<p class="sp-ev-warn">Mất kết nối máy chủ: kỳ duyên chỉ xuất hiện khi online.</p>'
    : k.blocked ? `<p class="sp-ev-warn">${k.blocked}</p>` : '';
  const open = k.open ? `<section class="kd-open">
      <h4>${k.open.title}<small>Còn tới ${kdTime(k.open.endsAt)}</small></h4>${kdTimer(k.open)}
      <p>${k.open.text}</p>
      <div class="kd-choices">${kdChoices(k.open, busy || k.blocked)}</div>
    </section>`
    : `<p class="kd-wait">Chưa có kỳ duyên. Khung kế tiếp lúc <b>${kdTime(k.nextAt)}</b> (08:00 · 12:00 · 19:00 · 22:00, mỗi khung 10 phút).</p>`;
  const books = k.books.map((b, i) => `<li class="${b.pages >= b.need ? 'done' : b.open ? '' : 'lock'}" ${b.lore ? `data-tip="${kdAttr(b.lore)}"` : ''}>
      <b>${b.name}</b><span class="kd-bar"><i style="width:${Math.min(100, b.pages / b.need * 100)}%"></i></span>
      <small><span>${b.open ? `${b.pages}/${b.need}` : (DATA?.realms?.[i]?.name ? `Mở ở cảnh giới ${DATA.realms[i].name}` : 'Mở ở cảnh giới kế')}</span> · <span>${b.bonus}</span></small></li>`).join('');   // hai span: bản dịch "Mở ở cảnh giới {0}" không nuốt phần thưởng phía sau
  const end = k.ending ? `<p class="kd-end"><b>${k.ending}</b> ${k.endingText}</p>` : '';
  pane.innerHTML = `${warn}${open}
    <h4 class="dd-sec">Thiên Thư <small>Đạo Tâm ${k.daoTam > 0 ? '+' : ''}${k.daoTam} · ${kdPath(k.daoTam)}</small></h4>
    ${end}<ol class="kd-books">${books}</ol>`;
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
