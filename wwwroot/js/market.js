/* Vấn Đạo · js/market.js: Chợ giao dịch giữa người chơi (Supabase qua server game) · Mua / Bán / Của tôi. */
let mkView = 'shop', mkData = null, mkLoading = false, mkAt = 0, mkPick = null, mkCat = 'all', mkSort = 'new', mkQuery = '';
const MK_CATS = [['all', 'Tất cả'], ['gear', 'Trang bị'], ['pill', 'Đan dược'], ['mat', 'Nguyên liệu'], ['other', 'Khác']];
const mkCatOf = it => it.slot ? 'gear' : ['pill', 'elixir'].includes(it.type) ? 'pill' : ['herb', 'boss', 'ore', 'core'].includes(it.type) ? 'mat' : 'other';
const mkTradable = it => !!it && (['ve_tai_xiu', 've_linh_thu'].includes(it.id) ? mkData?.canSellTickets === true : !['linh_thach', 'thuong_pham_linh_thach'].includes(it.id));   // khớp GameService.Tradable: vé quay chỉ admin bán, linh thạch và Thượng Phẩm không bán
const mkEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
const mkItem = (k, n) => { const it = ITEM[k]; return `<span class="mk-it" tabindex="0" data-tip="${mkEsc(it.name)}" data-card="bag" data-k="${k}">${icon(k)}<b class="rc-${it.rarity}">${mkEsc(it.name)}</b>${n > 1 ? `<em>×${fmt(n)}</em>` : ''}</span>`; };
const mkLeft = iso => { const h = Math.max(0, (Date.parse(iso) - Date.now()) / 36e5); return h >= 24 ? `${Math.floor(h / 24)} ngày` : h >= 1 ? `${Math.floor(h)} giờ` : `${Math.ceil(h * 60)} phút`; };
const mkStones = () => S?.inventory?.linh_thach || 0;
const mkClock = ms => { const t = Math.max(0, Math.ceil(ms / 1000)); return [Math.floor(t / 3600), Math.floor(t / 60) % 60, t % 60].map(n => String(n).padStart(2, '0')).join(':'); };
let mkBagSort = 'rare', mkLayout = 'grid';   // mkLayout: Tạp Hóa, Mua, Của tôi xem dạng lưới (mặc định) hay danh sách
try { if (localStorage.getItem('van-dao-mk-layout') === 'list') mkLayout = 'list'; } catch(e){}
const MK_BAG_SORTS = [['rare','Phẩm cao'],['name','Tên A–Z'],['qty','Số lượng nhiều'],['level','Cấp cao'],['type','Loại vật phẩm']];
function mkItemDetails(k){
  const it = ITEM[k]; if (!it) return '';
  const quality = typeof RARITY !== 'undefined' ? RARITY[it.rarity]?.n : null;
  const meta = [quality ? `Phẩm ${quality}` : '', it.kind || MK_CATS.find(([c]) => c === mkCatOf(it))?.[1]];
  if (it.slot){
    if (it.itemLevel) meta.push(`Cấp ${it.itemLevel}`);
    if (typeof DATA !== 'undefined' && DATA.realms?.[it.minRealm || 0]) meta.push(`Yêu cầu ${DATA.realms[it.minRealm || 0].name}`);
  }
  const stats = [['atk','Công',''],['def','Thủ',''],['hp','Máu',''],['critPct','Bạo kích','%'],['speedPct','Né tránh','%'],['qiPct','Tu luyện','%']]
    .filter(([key]) => it[key]).map(([key,name,unit]) => `${name} +${fmt(it[key])}${unit}`);
  const effects = (it.effects || []).map(e => e.text).filter(Boolean);
  return `<div class="mk-details"><div class="mk-detail-meta">${meta.filter(Boolean).map(mkEsc).join(' · ')}</div>${stats.length ? `<div class="mk-detail-stats">${stats.map(s => `<span>${mkEsc(s)}</span>`).join('')}</div>` : ''}${it.set ? `<div>Bộ ${mkEsc(it.set)}</div>` : ''}${effects.length || it.special ? `<div class="mk-detail-effects">${[...effects, it.special].filter(Boolean).map(mkEsc).join(' · ')}</div>` : ''}${it.use || it.desc ? `<p>${mkEsc(it.use || it.desc)}</p>` : ''}</div>`;
}
let mkNotice = '', mkNoticeBad = false;
function mkShowNotice(text, bad = false){
  mkNotice = text; mkNoticeBad = bad;
  const el = $('mkNotice');
  if (el){ el.textContent = text; el.hidden = !text; el.classList.toggle('bad', bad); }
}
async function mkList(item, qty, price){
  if (busy) return;
  busy = true;
  const button = $('mkSell')?.querySelector('[type=submit]');
  if (button){ button.disabled = true; button.textContent = 'Đang treo bán…'; }
  mkShowNotice('');
  try {
    const res = await api('POST', `/api/players/${S.id}/market/list/${item}?${new URLSearchParams({qty, price})}`);
    mkPick = null;
    mkNotice = `Đã treo bán ${fmt(qty)} ${ITEM[item]?.name || item} với tổng giá ${fmt(price)} linh thạch.`;
    mkNoticeBad = false;
    setState(res.state);
    notifyHost('changed');
    renderMarket();
    await loadMarket(true);
  } catch(e){
    mkShowNotice(e.message || 'Không thể treo bán. Vui lòng thử lại.', true);
  } finally {
    busy = false;
    if (button?.isConnected){ button.disabled = false; button.textContent = `Treo bán ${mkData.listingDays} ngày`; }
  }
}

async function loadMarket(force){
  if (!S || mkLoading) return;
  if (!force && mkData && performance.now() - mkAt < 15000){ renderMarket(); return; }
  mkLoading = true; renderMarket();
  try { mkData = await api('GET', `/api/players/${S.id}/market`); mkAt = performance.now(); }
  catch(e){ mkData = {enabled: true, listings: [], mine: [], error: e.message}; }
  finally { mkLoading = false; renderMarket(); }
}
async function mkAct(path){ await act(`market/${path}`); loadMarket(true); }

// Nhận linh thạch đã bán / đồ trả về: chạy nền mỗi phút ở một cửa sổ (dải hoặc bản web), không chen khi đang thao tác
async function marketClaimTick(){
  if (!S || busy || MODE === 'panel' || S.realm < 1) return;
  try {
    const req = api('POST', `/api/players/${S.id}/market/claim`), seq = postSeq, res = await req;
    if (res.message && !busy && seq === postSeq){ setState(res.state); notifyHost('changed'); }   // nhật ký đã ghi giao dịch
  } catch(e){}
}
setInterval(marketClaimTick, 60000);

function mkBuyView(d){
  const q = mkQuery.trim().toLowerCase();
  let rows = d.listings.filter(l => ITEM[l.itemId] && (mkCat === 'all' || mkCatOf(ITEM[l.itemId]) === mkCat) && (!q || ITEM[l.itemId].name.toLowerCase().includes(q)));
  if (mkSort === 'cheap') rows.sort((a, b) => a.price / a.qty - b.price / b.qty);
  else if (mkSort === 'rare') rows.sort((a, b) => RAR_KEYS.indexOf(ITEM[b.itemId].rarity) - RAR_KEYS.indexOf(ITEM[a.itemId].rarity));
  return `<div class="mk-filter">
      <select id="mkCat" aria-label="Loại vật phẩm">${MK_CATS.map(([k, n]) => `<option value="${k}" ${k === mkCat ? 'selected' : ''}>${n}</option>`).join('')}</select>
      <input id="mkQ" aria-label="Tìm vật phẩm" placeholder="Tìm tên vật phẩm…" value="${mkEsc(mkQuery)}">
      <select id="mkSort" aria-label="Sắp xếp"><option value="new">Mới nhất</option><option value="cheap" ${mkSort === 'cheap' ? 'selected' : ''}>Đơn giá thấp</option><option value="rare" ${mkSort === 'rare' ? 'selected' : ''}>Phẩm cao</option></select>
    </div>
    <div class="mk-list-head"><span>${rows.length} tin đang bán</span><span>Tổng giá · Linh thạch</span></div>
    <ul class="mk-list">${rows.length ? rows.map(l => `<li class="mk-row${l.mine ? ' mine' : ''}">
      ${mkItem(l.itemId, l.qty)}
      <span class="mk-meta">${mkEsc(l.sellerName)} · còn ${mkLeft(l.expiresAt)}</span>
      <span class="mk-price"><b>${fmt(l.price)}</b><small>${l.qty > 1 ? `${fmt(Math.ceil(l.price / l.qty))}/món` : 'Linh thạch'}</small></span>
      ${l.mine ? '<span class="mk-tag">Của bạn</span>' : `<button data-mk-buy="${l.id}" ${d.blocked || mkStones() < l.price ? 'disabled' : ''}>Mua</button>`}
    </li>`).join('') : `<li class="mk-empty">${d.listings.length ? 'Không có món phù hợp.' : 'Chợ đang trống: hãy là người đầu tiên treo bán.'}</li>`}</ul>`;
}

function mkSellView(d){
  const own = Object.entries(S.inventory).filter(([k, n]) => n > 0 && mkTradable(ITEM[k]))
    .sort(([a,na], [b,nb]) => {
      const x=ITEM[a], y=ITEM[b], name=x.name.localeCompare(y.name,'vi');
      if(mkBagSort==='name') return name;
      if(mkBagSort==='qty') return nb-na || name;
      if(mkBagSort==='level') return (y.itemLevel || 0)-(x.itemLevel || 0) || (y.minRealm || 0)-(x.minRealm || 0) || name;
      if(mkBagSort==='type') return mkCatOf(x).localeCompare(mkCatOf(y)) || name;
      return RAR_KEYS.indexOf(y.rarity)-RAR_KEYS.indexOf(x.rarity) || name;
    });
  if (mkPick && (!(S.inventory[mkPick] > 0) || !mkTradable(ITEM[mkPick]))) mkPick = null;
  const openN = d.mine.filter(m => m.role === 'sell' && m.status === 'open').length;
  let form = '<p class="mk-hint">Chọn một món trong hành trang để treo bán.</p>';
  if (mkPick){
    const it = ITEM[mkPick], have = S.inventory[mkPick];
    const same = d.listings.filter(l => l.itemId === mkPick && !l.mine);
    const low = same.length ? Math.min(...same.map(l => Math.ceil(l.price / l.qty))) : 0;
    const unit = low || Math.max(1, (it.sell || 1) * 2);
    form = `<form class="mk-form" id="mkSell">
      ${mkItem(mkPick, 0)}<small>Có ${fmt(have)}${it.sell ? ` · bán cho NPC ${fmt(it.sell)}/món` : ''}${low ? ` · rẻ nhất trên chợ ${fmt(low)}/món` : ''}</small>
      ${mkItemDetails(mkPick)}
      <label>Số lượng <input id="mkQty" type="number" min="1" max="${have}" value="1"></label>
      <label>Tổng giá · Linh thạch <input id="mkPrice" type="number" min="1" max="2000000000" value="${unit}"></label>
      <p class="mk-net" id="mkNet"></p>
      <button type="submit" ${d.blocked || (!d.unlimitedListings && openN >= d.maxOpen) ? 'disabled' : ''}>Treo bán ${d.listingDays} ngày</button>
    </form>`;
  }
  return `<p class="mk-hint">Đang treo ${openN}/${d.unlimitedListings ? '∞' : d.maxOpen} tin · thuế ${d.taxPct}% trừ vào tiền bán · tối đa ${d.unlimitedListings ? '∞' : d.maxDaily} giao dịch/24 giờ khi treo bán.</p>
    <div class="mk-sell-layout"><section class="mk-stock"><h3>Hành trang <small>${own.length} loại vật phẩm</small></h3><label class="mk-bag-order">Sắp xếp <select id="mkBagSort">${MK_BAG_SORTS.map(([k,n])=>`<option value="${k}" ${k===mkBagSort?'selected':''}>${n}</option>`).join('')}</select></label><div class="mk-bag">${own.length ? own.map(([k, n]) => `<button class="mk-cell r-${ITEM[k].rarity}${k === mkPick ? ' on' : ''}" aria-label="${mkEsc(ITEM[k].name)} · ${fmt(n)} món" aria-pressed="${k === mkPick}" data-mk-pick="${k}" data-card="bag" data-k="${k}">${icon(k)}<em>${n > 1 ? fmt(n) : ''}</em></button>`).join('') : '<p class="mk-empty">Không có vật phẩm giao dịch được.</p>'}</div></section><section class="mk-post"><h3>Treo bán vật phẩm</h3>${form}</section></div>`;
}

function mkClampShopQty(input, commit = false){
  if (!input) return 1;
  if (input.value === '' && !commit) return 1;
  const max = Math.max(1, Number(input.max) || 1);
  const qty = Math.max(1, Math.min(max, Math.floor(Number(input.value) || 1)));
  input.value = String(qty);
  return qty;
}

/** Tạp Hóa bà Dĩm (mkData.shop): món cố định + 10 món ngẫu nhiên mỗi 12 giờ, luôn mở kể cả khi Chợ người chơi chưa cấu hình. */
function mkShopView(){
  const shop = mkData?.shop;
  if (!shop) return `<p class="mk-empty">${mkLoading ? 'Đang tải…' : mkEsc(mkData?.error || 'Tạp hóa chưa mở cửa.')}</p>`;
  const left = o => o.daily ? Math.max(0, o.limit - o.bought) : 99;
  const can = o => Math.min(left(o), Math.floor(mkStones() / o.price));
  const row = o => `<li class="mk-row${o.daily && !left(o) ? ' bought' : ''}">
      ${mkItem(o.item, 0)}
      <span class="mk-meta">Đang có ${fmt((S.inventory[o.item] || 0) + (S.gemState?.vault?.[o.item] || 0))}${o.daily ? ` · đợt này còn ${left(o)}/${o.limit}` : ITEM[o.item].use ? ' · ' + mkEsc(ITEM[o.item].use) : ''}</span>
      <span class="mk-price"><b>${fmt(o.price)}</b><small>Linh thạch</small></span>
      ${!left(o) ? '<span class="mk-tag">Hết phần</span>' : `<span class="mk-shop-buy">${left(o) > 1 ? `<input type="number" min="1" max="${Math.max(1, can(o))}" value="1" aria-label="Số lượng mua ${mkEsc(ITEM[o.item].name)}" data-mk-shop-qty="${o.item}">` : ''}<button data-mk-shop="${shop.id}" data-mk-shop-item="${o.item}" ${can(o) < 1 ? 'disabled' : ''}>Mua</button></span>`}
    </li>`;
  const offers = shop.offers.filter(o => ITEM[o.item]).sort((a, b) => a.daily - b.daily);   // món cố định (Thẻ đổi tên) đứng đầu
  return `<section class="mk-shop">
    <header class="mk-shop-head"><img class="mk-shop-keeper" src="img/hero_kimdan_nu_thumb.webp" alt=""><div><h3>${mkEsc(shop.name)}</h3><p>“Hàng tốt, giá có lý. Nửa ngày một mẻ mới, đạo hữu xem đi!”</p><small data-mk-reset="${shop.resetAt}">Đổi hàng sau ${mkClock(shop.resetAt - Date.now())}</small></div></header>
    <div class="mk-list-head"><span>Hàng đợt này · ${offers.length} món</span><span>Giá · Linh thạch</span></div>
    <ul class="mk-list">${offers.map(row).join('')}</ul></section>`;
}

let mkVipHerb = '';   // linh thảo đang chọn ở Tạp Hóa VIP: giữ qua các lần vẽ lại
/** Thông tin donate hiện ở Tạp Hóa VIP. Để trống momo và qr thì ẩn cả khung. qr: ảnh trong wwwroot (ảnh lỗi thì tự ẩn). */
const VIP_DONATE = {
  momo: '',                           // số điện thoại MoMo (để trống: chỉ dùng mã QR)
  name: 'BUI XUAN KIEN',              // tên chủ tài khoản
  qr: 'img/donate/momo-qr.png',
  contact: 'Zalo 0932550167',         // nơi nhắn để nhận code
  contactUrl: 'https://zalo.me/0932550167',   // bấm vào contact thì mở link này (trình duyệt ngoài)
};
function mkVipDonate(){
  const d = VIP_DONATE;
  if (!d.momo && !d.qr) return '';
  const link = d.contactUrl ? `<a href="${mkEsc(d.contactUrl)}" target="_blank" rel="noopener">${mkEsc(d.contact || d.contactUrl)}</a>` : mkEsc(d.contact);
  return `<section class="mk-donate">
    ${d.qr ? `<img class="mk-donate-qr" src="${mkEsc(d.qr)}" alt="QR MoMo" onerror="this.remove()">` : ''}
    <div><h4>Ủng hộ game</h4><ol>
      <li>${d.momo ? `<span>Chuyển MoMo:</span> <b>${mkEsc(d.momo)}</b>` : '<span>Quét mã QR bằng MoMo</span>'}${d.name ? ` · <b>${mkEsc(d.name)}</b>` : ''}</li>
      <li>1.000đ = 1 Linh Thạch Thượng Phẩm</li>
      <li><span>Nội dung chuyển khoản:</span> <b>NLTT ${mkEsc(S?.name || '')}</b></li>
      <li><span>Gửi ảnh chuyển khoản để nhận code:</span> <b>${d.contact ? link : 'admin'}</b></li>
      <li>Nhập code ở ô Nhập code quà tặng</li>
    </ol></div></section>`;
}
/** Tạp Hóa VIP (mkData.vip): tiêu Linh Thạch Thượng Phẩm từ code donate; luôn mở kể cả khi Chợ người chơi chưa cấu hình. */
function mkVipView(){
  const vip = mkData?.vip;
  if (!vip) return `<p class="mk-empty">${mkLoading ? 'Đang tải…' : mkEsc(mkData?.error || 'Tạp Hóa VIP chưa mở cửa.')}</p>`;
  const bal = Number(vip.balance) || 0;
  const can = o => Math.floor(bal / o.price);   // không giới hạn số lần mua: tối đa theo Thượng Phẩm đang có
  const qtyIn = o => `<input type="number" min="1" max="${Math.max(1, can(o))}" value="1" aria-label="Số lượng mua ${mkEsc(ITEM[o.item]?.name || o.item)}" data-mk-shop-qty="${mkEsc(o.item)}">`;
  const buy = (o, qty = true) => o.owned ? '<span class="mk-tag">Đã mở</span>' : `<span class="mk-shop-buy">${qty ? qtyIn(o) : ''}<button data-vip-buy="${mkEsc(o.item)}" ${can(o) < 1 ? 'disabled' : ''}>Mua</button></span>`;
  const row = o => {
    const price = `<span class="mk-price"><b>${fmt(o.price)}</b><small>Thượng Phẩm</small></span>`;
    if (o.item === 'vip_herb'){
      const ch = (o.choices || []).filter(id => ITEM[id]), cur = ch.includes(mkVipHerb) ? mkVipHerb : ch[0];
      if (!cur) return '';
      return `<li class="mk-row mk-vip-herb">${mkItem(cur, 0)}
      <span class="mk-meta"><select data-vip-herb aria-label="Chọn linh thảo">${ch.map(id => `<option value="${mkEsc(id)}" ${id === cur ? 'selected' : ''}>${mkEsc(ITEM[id].name)}</option>`).join('')}</select>Linh thảo tự chọn · bậc tối đa theo cảnh giới + 1</span>${price}${buy(o)}</li>`;
    }
    if (o.item === 'goi_toc_hanh')
      return `<li class="mk-row${o.owned ? ' bought' : ''}"><span class="mk-it"><img class="ic" src="img/items/goi_toc_hanh.png" alt=""><b class="rc-dao">Gói Tốc Hành</b></span>
      <span class="mk-meta">Mở thám hiểm ×2 (bật/tắt ở màn hình chính) · +10 Bổ Nguyên Đan Trung phẩm · +10 Thượng phẩm</span>${price}${buy(o, false)}</li>`;
    if (o.item === 'goi_linh_thu')
      return `<li class="mk-row"><span class="mk-it"><img class="ic" src="img/items/goi_linh_thu.png" alt=""><b class="rc-dao">Gói Linh Thú</b></span>
      <span class="mk-meta">Mở ra 2 Thượng Cổ Thú Đản · +1.000 Linh Thú Đan</span>${price}${buy(o)}</li>`;
    if (!ITEM[o.item]) return '';
    return `<li class="mk-row">${mkItem(o.item, 0)}
      <span class="mk-meta">${o.item === 'linh_thach' ? `Nhận ${fmt(o.qty)} linh thạch mỗi viên` : o.qty > 1 ? `×${fmt(o.qty)} mỗi lần` : ''}</span>${price}${buy(o)}</li>`;
  };
  const groups = [...new Set(vip.offers.map(o => o.group))];
  return `<section class="mk-shop mk-vip">
    <header class="mk-shop-head"><div><h3>${mkEsc(vip.name)}</h3><p>${`Có ${fmt(bal)} Linh Thạch Thượng Phẩm · 1.000đ donate = 1 viên`}</p></div></header>
    ${mkVipDonate()}
    ${groups.map(g => `<div class="mk-list-head"><span>${mkEsc(g)}</span><span>Giá · Thượng Phẩm</span></div>
    <ul class="mk-list">${vip.offers.filter(o => o.group === g).map(row).join('')}</ul>`).join('')}</section>`;
}

const MK_STATUS = {open: 'Đang treo', sold: 'Đã bán', cancelled: 'Đã hủy', expired: 'Hết hạn'};
function mkMineView(d){
  if (!d.mine.length) return '<p class="mk-empty">Chưa có giao dịch nào.</p>';
  return `<ul class="mk-list">${d.mine.map(m => {
    const buy = m.role === 'buy', st = buy ? 'Đã mua' : MK_STATUS[m.status] || m.status;
    const money = buy ? `−${fmt(m.price)}` : m.status === 'sold' ? `+${fmt(m.payout)}` : fmt(m.price);
    const who = m.otherName ? (buy ? `của ${mkEsc(m.otherName)}` : `cho ${mkEsc(m.otherName)}`) : m.status === 'open' ? `còn ${mkLeft(m.expiresAt)}` : '';
    return `<li class="mk-row st-${buy ? 'buy' : m.status}">${ITEM[m.itemId] ? mkItem(m.itemId, m.qty) : mkEsc(m.itemId)}
      <span class="mk-meta">${st}${who ? ' · ' + who : ''}</span><span class="mk-price"><b>${money}</b></span>
      ${!buy && m.status === 'open' ? `<button data-mk-cancel="${m.id}">Hủy</button>` : '<span></span>'}</li>`;
  }).join('')}</ul>`;
}

function renderMarket(){
  const pane = $('pane-market'); if (!pane || !S) return;
  const d = mkData;
  const keepQ = document.activeElement?.id === 'mkQ';
  let body;
  if (mkView === 'shop') body = mkShopView();
  else if (mkView === 'vip') body = mkVipView();
  else if (!d) body = `<p class="mk-empty">${mkLoading ? 'Đang tải…' : ''}</p>`;
  else if (!d.enabled) body = '<p class="mk-note">Chợ chưa được cấu hình (appsettings.json → Leaderboard: Url, AnonKey).</p>';
  else body = (d.error ? `<p class="mk-note">${mkEsc(d.error)}</p>` : '') + (d.blocked ? `<p class="mk-note">${mkEsc(d.blocked)}</p>` : '')
    + (mkView === 'sell' ? mkSellView(d) : mkView === 'mine' ? mkMineView(d) : mkBuyView(d));
  const gridable = mkView !== 'sell';
  pane.innerHTML = `<div class="mk${gridable && mkLayout === 'grid' ? ' mk-gridview' : ''}">
    <header class="mk-heading"><svg class="mk-emblem" viewBox="0 0 48 48" aria-hidden="true"><path d="M7 21h34L36 9H12zM10 21v19h28V21M5 40h38M18 40V29h12v11M16 9l-3 12m11-12v12m8-12 3 12" fill="none" stroke="currentColor" stroke-width="1.8"/><path d="m24 2 4 4-4 4-4-4z" fill="currentColor"/></svg><div><h2>Phường thị</h2><p>Giao thương cùng đạo hữu</p></div><div class="mk-wallet"><small>Linh thạch của bạn</small><span class="mk-stone">${icon('linh_thach')}${fmt(mkStones())}</span></div></header>
    <div class="mk-top"><div class="mk-tabs" role="tablist">${[['shop', 'Tạp Hóa'], ['vip', 'Tạp Hóa VIP'], ['buy', 'Mua'], ['sell', 'Bán'], ['mine', 'Của tôi']].map(([k, n]) =>
      `<button role="tab" data-mk-view="${k}" class="${k === mkView ? 'on' : ''}" aria-selected="${k === mkView}">${n}</button>`).join('')}</div>
      ${gridable ? `<div class="mk-layout" role="group" aria-label="Kiểu hiển thị">${[['list', 'Danh sách', '<path d="M3 4h14M3 10h14M3 16h14"/>'], ['grid', 'Lưới', '<path d="M3 3h5v5H3zM12 3h5v5h-5zM3 12h5v5H3zM12 12h5v5h-5z"/>']].map(([k, n, d]) =>
        `<button data-mk-layout="${k}" class="${k === mkLayout ? 'on' : ''}" aria-pressed="${k === mkLayout}" aria-label="Xem dạng ${n.toLowerCase()}" data-tip="${n}"><svg viewBox="0 0 20 20" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6">${d}</svg></button>`).join('')}</div>` : ''}
      <button class="mk-ref" data-mk-ref ${mkLoading ? 'disabled' : ''} aria-label="Làm mới chợ" data-tip="Làm mới">↻</button></div>
    <div id="mkNotice" class="mk-notice${mkNoticeBad ? ' bad' : ''}" role="status" aria-live="polite" ${mkNotice ? '' : 'hidden'}>${mkEsc(mkNotice)}</div>
    <section class="mk-content" aria-busy="${mkLoading}">${body}</section><footer class="mk-footer">${d?.enabled ? `Thuế giao dịch ${d.taxPct ?? 0}% · Tin bán có hiệu lực ${d.listingDays ?? 0} ngày` : 'Mua bán vật phẩm bằng linh thạch'}</footer></div>`;
  if (keepQ){ const q = $('mkQ'); q.focus(); q.setSelectionRange(q.value.length, q.value.length); }
  mkNet();
}
function mkNet(){
  const el = $('mkNet'); if (!el || !mkData) return;
  const qty = Math.max(1, +$('mkQty').value || 1), price = Math.max(0, +$('mkPrice').value || 0);
  el.textContent = `Đơn giá ${fmt(Math.ceil(price / qty))} · thực nhận ${fmt(price - Math.ceil(price * mkData.taxPct / 100))} linh thạch`;
}

function initMarket(){
  const pane = $('pane-market'); if (!pane) return;
  pane.addEventListener('click', e => {
    const v = e.target.closest('[data-mk-view]'); if (v){ mkView = v.dataset.mkView; renderMarket(); return; }
    const ly = e.target.closest('[data-mk-layout]');
    if (ly){ mkLayout = ly.dataset.mkLayout; try { localStorage.setItem('van-dao-mk-layout', mkLayout); } catch(e){} renderMarket(); return; }
    if (e.target.closest('[data-mk-ref]')){ loadMarket(true); return; }
    const pk = e.target.closest('[data-mk-pick]'); if (pk){ mkPick = pk.dataset.mkPick; renderMarket(); return; }
    const b = e.target.closest('[data-mk-buy]');
    if (b){                                         // bấm 2 lần để xác nhận mua
      if (!b.dataset.armed){ b.dataset.armed = 1; b.textContent = 'Xác nhận?'; b.classList.add('arm'); setTimeout(() => { if (b.isConnected){ delete b.dataset.armed; b.textContent = 'Mua'; b.classList.remove('arm'); } }, 3000); }
      else mkAct(`buy/${b.dataset.mkBuy}`);
      return;
    }
    const vb = e.target.closest('[data-vip-buy]');
    if (vb){
      const row = vb.closest('.mk-row'), n = mkClampShopQty(row.querySelector('[data-mk-shop-qty]'), true), item = row.querySelector('[data-vip-herb]')?.value || vb.dataset.vipBuy;
      vb.disabled = true; act(`vip/${item}?n=${n}`).then(() => loadMarket(true)); return;
    }
    const sh = e.target.closest('[data-mk-shop]');
    if (sh){
      const q = sh.parentElement.querySelector('[data-mk-shop-qty]'), n = mkClampShopQty(q, true);
      sh.disabled = true; act(`shop/${sh.dataset.mkShop}/${sh.dataset.mkShopItem}?n=${n}`).then(() => loadMarket(true)); return;
    }
    const c = e.target.closest('[data-mk-cancel]'); if (c) mkAct(`cancel/${c.dataset.mkCancel}`);
  });
  pane.addEventListener('input', e => {
    if (e.target.matches('[data-mk-shop-qty]')) mkClampShopQty(e.target);
    if (e.target.id === 'mkQ'){ mkQuery = e.target.value; renderMarket(); }
    if (e.target.id === 'mkQty' || e.target.id === 'mkPrice') mkNet();
  });
  pane.addEventListener('change', e => {
    if (e.target.matches('[data-mk-shop-qty]')) mkClampShopQty(e.target, true);
    if (e.target.matches('[data-vip-herb]')){ mkVipHerb = e.target.value; renderMarket(); }
    if (e.target.id === 'mkBagSort'){
      const qty = $('mkQty')?.value, price = $('mkPrice')?.value;
      mkBagSort = e.target.value; renderMarket();
      if ($('mkQty') && qty !== undefined) $('mkQty').value = qty;
      if ($('mkPrice') && price !== undefined) $('mkPrice').value = price;
      mkNet();
    }
    if (e.target.id === 'mkCat'){ mkCat = e.target.value; renderMarket(); }
    if (e.target.id === 'mkSort'){ mkSort = e.target.value; renderMarket(); }
  });
  pane.addEventListener('submit', e => {
    if (e.target.id !== 'mkSell') return;
    e.preventDefault();
    const qty = Math.min(S.inventory[mkPick] || 0, Math.max(1, Math.floor(+$('mkQty').value || 1)));
    const price = Math.floor(+$('mkPrice').value || 0);
    if (price < 1) return;
    mkList(mkPick, qty, price);
  });
  setInterval(() => {                               // đếm ngược đổi hàng Tạp Hóa; hết giờ thì tải đợt mới
    const el = pane.querySelector('[data-mk-reset]'); if (!el) return;
    const ms = +el.dataset.mkReset - Date.now();
    el.textContent = `Đổi hàng sau ${mkClock(ms)}`;
    if (ms <= 0 && !mkLoading){ delete el.dataset.mkReset; loadMarket(true); }
  }, 1000);
  setInterval(() => { if (!pane.hidden && !document.hidden && !busy && document.activeElement?.closest?.('#pane-market') == null) loadMarket(true); }, 30000);
}
