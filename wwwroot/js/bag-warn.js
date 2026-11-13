/* js/bag-warn.js: báo Hành trang đầy — huy hiệu trên nút Hành trang, dải cảnh báo trên màn chơi,
   thông báo nổi khi vừa đầy và nhắc lại (tối đa 5 phút / lần) khi vật phẩm rơi bị bỏ lại. */
const BAG_NEAR = 3;                       // còn ≤ 3 ô trống: cảnh báo vàng "sắp đầy"
const BAG_REMIND_MS = 5 * 60 * 1000;
let bagLost = 0, bagToastAt = 0, bagWasFull = null;

/** Số món bị bỏ lại trong các dòng nhật ký mới (server ghi "hành trang đầy nên bỏ lại N món", "bỏ lại Rương Boss"). */
function bagLostIn(logs){
  let n = 0;
  for (const l of logs){
    const m = /bỏ lại (\d+) món/.exec(l.text); if (m) n += +m[1];
    if (/bỏ lại Rương Boss/.test(l.text)) n += 1;
  }
  return n;
}

function bagOpen(){                       // mở Hành trang (dải taskbar: bật bảng Tu luyện)
  if (MODE === 'strip') notifyHost('open-panel', {tab: 'inv'}); else openTab('inv');
}

function bagToast(lost){
  const el = $('craftToast'); if (!el) return;
  el.classList.remove('brief');
  el.innerHTML = `<span class="ct-ic"><img src="img/ui_hanh_trang.png" alt=""></span><span class="ct-t bad">Hành trang đầy</span>
    <span class="ct-o">${lost ? `Đã bỏ lại <b>${fmt(lost)}</b> vật phẩm rơi` : 'Vật phẩm rơi sẽ bị bỏ lại'}</span>
    <span class="ct-u"><span>Bán, luyện hoặc vứt bớt để nhặt tiếp</span></span>`;
  el.classList.add('fast');
  el.hidden = true; void el.offsetWidth; el.hidden = false;
  clearTimeout(craftTimer); craftTimer = setTimeout(() => { el.hidden = true; }, 3200);
  bagToastAt = Date.now();
}

/** Gọi sau mỗi lần nhận trạng thái mới (core.js → setState). newLogs: các dòng nhật ký mới kể từ lần trước. */
function bagWatch(newLogs){
  if (!S) return;
  const free = S.bagCap - S.bagUsed, full = free <= 0;
  const lostNow = bagLostIn(newLogs);
  if (full) bagLost += lostNow; else bagLost = 0;
  // thông báo nổi: vừa đầy (kể cả lúc mở game), hoặc vẫn đầy và tiếp tục bỏ lại đồ (nhắc lại sau 5 phút)
  if (full && (bagWasFull !== true || (lostNow > 0 && Date.now() - bagToastAt > BAG_REMIND_MS))) bagToast(bagLost);
  bagWasFull = full;

  const lvl = full ? 'full' : free <= BAG_NEAR ? 'near' : '';
  const btn = $('btnBag');
  if (btn){
    btn.classList.toggle('bag-full', lvl === 'full'); btn.classList.toggle('bag-near', lvl === 'near');
    btn.dataset.tip = lvl === 'full' ? `Hành trang ĐẦY (${S.bagUsed}/${S.bagCap})\nVật phẩm rơi đang bị bỏ lại — bán hoặc luyện bớt`
      : lvl === 'near' ? `Hành trang sắp đầy (${S.bagUsed}/${S.bagCap})\nCòn ${free} ô trống` : 'Hành trang\nTrang bị, chỉ số và vật phẩm';
  }
  const tab = $('tab-inv'); if (tab) tab.classList.toggle('bag-full', full);
  let bar = $('bagWarn');
  if (!full){ if (bar) bar.hidden = true; return; }
  if (!bar){
    bar = document.createElement('button');
    bar.id = 'bagWarn'; bar.className = 'bag-warn'; bar.type = 'button';
    bar.addEventListener('click', bagOpen);
    document.querySelector('.stage-wrap')?.appendChild(bar);
  }
  bar.hidden = false;
  bar.innerHTML = `<b>Hành trang đầy</b> ${S.bagUsed}/${S.bagCap}${bagLost ? ` · bỏ lại ${fmt(bagLost)} món` : ''}`;
  bar.setAttribute('aria-label', `Hành trang đầy ${S.bagUsed}/${S.bagCap}. Bấm để mở Hành trang`);
}
