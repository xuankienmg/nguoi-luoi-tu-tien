/* Vấn Đạo · js/market.js: Chợ giao dịch giữa người chơi (Supabase qua server game) · Mua / Bán / Của tôi. */
let mkView = 'vip', mkData = null, mkLoading = false, mkAt = 0, mkPick = null, mkCat = 'all', mkSort = 'new', mkQuery = '';
const MK_CATS = [['all', 'Tất cả'], ['gear', 'Trang bị'], ['pill', 'Đan dược'], ['mat', 'Nguyên liệu'], ['other', 'Khác']];
const mkCatOf = it => it.slot ? 'gear' : ['pill', 'elixir'].includes(it.type) ? 'pill' : ['herb', 'boss', 'ore', 'core'].includes(it.type) ? 'mat' : 'other';
const mkTradable = it => !!it && (['ve_tai_xiu', 've_linh_thu'].includes(it.id) ? mkData?.canSellTickets === true : !['linh_thach', 'thuong_pham_linh_thach'].includes(it.id));   // khớp GameService.Tradable: vé quay chỉ admin bán, linh thạch và Cực Phẩm không bán
const mkEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
const mkItem = (k, n) => { const it = ITEM[k]; return `<span class="mk-it" tabindex="0" data-tip="${mkEsc(it.name)}" data-card="bag" data-k="${k}">${icon(k)}<b class="rc-${it.rarity}">${mkEsc(it.name)}</b>${n > 1 ? `<em>×${fmt(n)}</em>` : ''}</span>`; };
const mkLeft = iso => { const h = Math.max(0, (Date.parse(iso) - Date.now()) / 36e5); return h >= 24 ? `${Math.floor(h / 24)} ngày` : h >= 1 ? `${Math.floor(h)} giờ` : `${Math.ceil(h * 60)} phút`; };
const mkStones = () => S?.inventory?.linh_thach || 0;
/* Tiền tệ của tin rao (khớp MarketService.Currencies): linh thạch hoặc Cực phẩm linh thạch (id thuong_pham_linh_thach) */
const MK_CUP = 'thuong_pham_linh_thach';
let mkSellCur = 'linh_thach', mkCurFilter = 'all';
const mkCurOf = x => x?.currency === MK_CUP ? MK_CUP : 'linh_thach';
const mkBal = c => S?.inventory?.[c] || 0;
const mkCurName = c => c === MK_CUP ? 'Cực phẩm linh thạch' : 'linh thạch';
const mkCurIc = c => c === MK_CUP ? MK_VIP : MK_GEM;
const mkClock = ms => { const t = Math.max(0, Math.ceil(ms / 1000)); return [Math.floor(t / 3600), Math.floor(t / 60) % 60, t % 60].map(n => String(n).padStart(2, '0')).join(':'); };
let mkBagSort = 'rare';
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
async function mkList(item, qty, price, currency = 'linh_thach'){
  if (busy) return;
  busy = true;
  const button = $('mkSell')?.querySelector('[type=submit]');
  if (button){ button.disabled = true; button.textContent = 'Đang treo bán…'; }
  mkShowNotice('');
  try {
    const res = await api('POST', `/api/players/${S.id}/market/list/${item}?${new URLSearchParams({qty, price, currency})}`);
    mkPick = null;
    mkNotice = `Đã treo bán ${fmt(qty)} ${ITEM[item]?.name || item} với tổng giá ${fmt(price)} ${mkCurName(currency)}.`;
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


/* ===== Giao diện 11/2026: mỗi tab = khung chính + khung chi tiết bên phải ===== */
let mkShopPick = '', mkShopCat = 'all', mkVipGroup = 'all', mkBuyPick = null, mkHideMine = false, mkBagCat = 'all', mkBagQuery = '', mkMineFilter = 'all', mkCodeVal = '';
const MK_GEM = '<i class="mk-gem" aria-hidden="true"></i>', MK_VIP = '<i class="mk-gem vip" aria-hidden="true"></i>';
const mkRef = () => `<button type="button" class="mk-ref" data-mk-ref ${mkLoading ? 'disabled' : ''} aria-label="Làm mới chợ" data-tip="Làm mới">↻</button>`;
const mkKind = it => it.kind || MK_CATS.find(([c]) => c === mkCatOf(it))?.[1] || '';
const mkPills = (list, cur, attr) => list.map(([k, n, c]) => `<button type="button" class="mk-pill${k === cur ? ' on' : ''}" ${attr}="${mkEsc(k)}" aria-pressed="${k === cur}">${mkEsc(n)}${c != null ? ` · ${fmt(c)}` : ''}</button>`).join('');
const mkStep = (key, max, value = 1) => `<span class="mk-step"><button type="button" data-mk-step="-1" data-for="${mkEsc(key)}" aria-label="Bớt">−</button><input type="number" min="1" max="${Math.max(1, max)}" value="${value}" aria-label="Số lượng mua ${mkEsc(ITEM[key]?.name || key)}" data-mk-shop-qty="${mkEsc(key)}"><button type="button" data-mk-step="1" data-for="${mkEsc(key)}" aria-label="Thêm">+</button></span>`;
const mkHead = (k, sub = '') => { const it = ITEM[k]; return `<div class="mk-head"><span class="mk-big r-${it.rarity}" data-card="bag" data-k="${mkEsc(k)}">${icon(k)}</span><div><h3 class="rc-${it.rarity}">${mkEsc(it.name)}</h3>${sub ? `<small>${sub}</small>` : ''}</div></div>`; };
function mkShopTotal(inp){
  const t = $('pane-market')?.querySelector(`[data-mk-total="${CSS.escape(inp.dataset.mkShopQty)}"]`);
  if (t) t.innerHTML = `${t.dataset.vip ? MK_VIP : MK_GEM}${fmt((+t.dataset.price || 0) * mkClampShopQty(inp))}`;
}

function mkBuyView(d){
  const q = mkQuery.trim().toLowerCase();
  let rows = d.listings.filter(l => ITEM[l.itemId] && (mkCat === 'all' || mkCatOf(ITEM[l.itemId]) === mkCat) && (mkCurFilter === 'all' || mkCurOf(l) === mkCurFilter) && (!q || ITEM[l.itemId].name.toLowerCase().includes(q)) && !(mkHideMine && l.mine));
  if (mkSort === 'cheap') rows.sort((a, b) => (mkCurOf(a) === MK_CUP) - (mkCurOf(b) === MK_CUP) || a.price / a.qty - b.price / b.qty);   // hai loại tiền không so trực tiếp: linh thạch trước, Cực phẩm sau
  else if (mkSort === 'rare') rows.sort((a, b) => RAR_KEYS.indexOf(ITEM[b.itemId].rarity) - RAR_KEYS.indexOf(ITEM[a.itemId].rarity));
  if (!rows.some(l => l.id === mkBuyPick)) mkBuyPick = (rows.find(l => !l.mine) || rows[0])?.id ?? null;
  const unit = l => Math.ceil(l.price / l.qty);
  const poor = l => mkBal(mkCurOf(l)) < l.price;
  const row = l => { const it = ITEM[l.itemId], on = l.id === mkBuyPick, ic = mkCurIc(mkCurOf(l));
    return `<div class="mk-trow${on ? ' on' : ''}${l.mine ? ' mine' : ''}" data-mk-buy-pick="${l.id}">
      <button type="button" class="mk-tname" data-mk-buy-pick="${l.id}" aria-pressed="${on}"><span class="mk-tic r-${it.rarity}" data-card="bag" data-k="${mkEsc(l.itemId)}">${icon(l.itemId)}</span><span><b class="rc-${it.rarity}">${mkEsc(it.name)}</b><small>${mkEsc(mkKind(it))}</small></span></button>
      <span class="mk-seller">${mkEsc(l.sellerName)}</span><span class="mk-left">${mkLeft(l.expiresAt)}</span><span class="mk-num">${fmt(l.qty)}</span><span class="mk-num">${ic}${fmt(unit(l))}</span><b class="mk-num">${fmt(l.price)}</b>
      <span class="mk-act">${l.mine ? '<span class="mk-tag">Của bạn</span>' : `<button type="button" class="mk-btn mk-sm" data-mk-buy="${l.id}" ${d.blocked || poor(l) ? 'disabled' : ''}>Mua</button>`}</span></div>`; };
  const l = rows.find(x => x.id === mkBuyPick), lc = l && mkCurOf(l), lic = l && mkCurIc(lc);
  const side = l ? `${mkHead(l.itemId)}${mkItemDetails(l.itemId)}
    <div class="mk-box mk-kv"><span>Người bán</span><b>${mkEsc(l.sellerName)}</b><span>Còn lại</span><b>${mkLeft(l.expiresAt)}</b><span>Số lượng</span><b>${fmt(l.qty)}</b><span>Tiền tệ</span><b>${lic}${mkCurName(lc) === 'linh thạch' ? 'Linh thạch' : mkCurName(lc)}</b><span>Đơn giá</span><b>${lic}${fmt(unit(l))}</b><span>Bạn đang có</span><b>${fmt(S.inventory[l.itemId] || 0)}</b></div>
    <div class="mk-buybar"><div class="mk-total"><span>Thanh toán</span><b>${lic}${fmt(l.price)}</b></div>
    ${l.mine ? '<p class="mk-hint">Đây là tin của bạn. Hủy tin ở tab Của tôi.</p>' : `<button type="button" class="mk-btn mk-gold mk-big-btn" data-mk-buy="${l.id}" ${d.blocked || poor(l) ? 'disabled' : ''}>${poor(l) ? `Không đủ ${mkCurName(lc)}` : `Mua cả tin · ${fmt(l.qty)} món`}</button><small class="mk-hint">Mua trọn tin bán. Bấm 2 lần để xác nhận.</small>`}</div>`
    : '<p class="mk-empty">Chọn một tin để xem chi tiết.</p>';
  return `<div class="mk-split"><section class="mk-panel mk-main">
      <div class="mk-filter">
        <select id="mkCat" aria-label="Loại vật phẩm">${MK_CATS.map(([k, n]) => `<option value="${k}" ${k === mkCat ? 'selected' : ''}>${n}</option>`).join('')}</select>
        <input id="mkQ" type="search" aria-label="Tìm vật phẩm" placeholder="Tìm tên vật phẩm…" value="${mkEsc(mkQuery)}">
        <select id="mkSort" aria-label="Sắp xếp"><option value="new">Mới nhất</option><option value="cheap" ${mkSort === 'cheap' ? 'selected' : ''}>Đơn giá thấp</option><option value="rare" ${mkSort === 'rare' ? 'selected' : ''}>Phẩm cao</option></select>
        <select id="mkCurF" aria-label="Tiền tệ">${[['all', 'Mọi tiền tệ'], ['linh_thach', 'Linh thạch'], [MK_CUP, 'Cực phẩm linh thạch']].map(([k, n]) => `<option value="${k}" ${k === mkCurFilter ? 'selected' : ''}>${n}</option>`).join('')}</select>
        ${mkRef()}
      </div>
      <div class="mk-subbar"><span><b>${rows.length}</b> tin đang bán · thuế ${d.taxPct}% do người bán chịu</span><label class="mk-check"><input type="checkbox" id="mkHideMine" ${mkHideMine ? 'checked' : ''}> Ẩn tin của tôi</label></div>
      <div class="mk-thead" aria-hidden="true"><span>Vật phẩm</span><span>Người bán</span><span>Còn lại</span><span>SL</span><span>Đơn giá</span><span>Tổng</span><span></span></div>
      <div class="mk-tbody">${rows.map(row).join('') || `<p class="mk-empty">${d.listings.length ? 'Không có món phù hợp.' : 'Chợ đang trống: hãy là người đầu tiên treo bán.'}</p>`}</div>
    </section><aside class="mk-panel mk-side">${side}</aside></div>`;
}

function mkSellView(d){
  const all = Object.entries(S.inventory).filter(([k, n]) => n > 0 && mkTradable(ITEM[k]))
    .sort(([a,na], [b,nb]) => {
      const x=ITEM[a], y=ITEM[b], name=x.name.localeCompare(y.name,'vi');
      if(mkBagSort==='name') return name;
      if(mkBagSort==='qty') return nb-na || name;
      if(mkBagSort==='level') return (y.itemLevel || 0)-(x.itemLevel || 0) || (y.minRealm || 0)-(x.minRealm || 0) || name;
      if(mkBagSort==='type') return mkCatOf(x).localeCompare(mkCatOf(y)) || name;
      return RAR_KEYS.indexOf(y.rarity)-RAR_KEYS.indexOf(x.rarity) || name;
    });
  const cats = MK_CATS.filter(([k]) => k === 'all' || all.some(([i]) => mkCatOf(ITEM[i]) === k)).map(([k, n]) => [k, n, k === 'all' ? all.length : all.filter(([i]) => mkCatOf(ITEM[i]) === k).length]);
  if (!cats.some(([k]) => k === mkBagCat)) mkBagCat = 'all';
  const q = mkBagQuery.trim().toLowerCase();
  const own = all.filter(([k]) => (mkBagCat === 'all' || mkCatOf(ITEM[k]) === mkBagCat) && (!q || ITEM[k].name.toLowerCase().includes(q)));
  if (mkPick && (!(S.inventory[mkPick] > 0) || !mkTradable(ITEM[mkPick]))) mkPick = null;
  const openN = d.mine.filter(m => m.role === 'sell' && m.status === 'open').length;
  const full = !d.unlimitedListings && openN >= d.maxOpen;
  let form = `<div class="mk-empty mk-pick-hint">${MK_GEM}<p>Chọn một món trong hành trang để treo bán.</p></div>`;
  if (mkPick){
    const it = ITEM[mkPick], have = S.inventory[mkPick];
    const cur = mkSellCur === MK_CUP ? MK_CUP : 'linh_thach', ic = mkCurIc(cur);
    const same = d.listings.filter(l => l.itemId === mkPick && !l.mine && mkCurOf(l) === cur);   // giá tham khảo cùng loại tiền
    const low = same.length ? Math.min(...same.map(l => Math.ceil(l.price / l.qty))) : 0;
    const unit = low || (cur === MK_CUP ? 10 :Math.max(1, (it.sell || 1) * 2));
    form = `<form class="mk-form" id="mkSell">
      ${mkHead(mkPick, `Trong túi ${fmt(have)}`)}
      <p class="mk-hint">${[it.sell ? `Bán cho NPC ${fmt(it.sell)}/món` : '', low ? `Rẻ nhất trên chợ ${fmt(low)} ${mkCurName(cur)}/món` : `Chưa ai treo bán món này bằng ${mkCurName(cur)}`].filter(Boolean).join(' · ')}</p>
      ${mkItemDetails(mkPick)}
      <div class="mk-qty-row"><label for="mkQty">Số lượng</label><span class="mk-qty-ctl"><span class="mk-step"><button type="button" data-mk-step="-1" data-for="mkQty" aria-label="Bớt">−</button><input id="mkQty" type="number" min="1" max="${have}" value="1"><button type="button" data-mk-step="1" data-for="mkQty" aria-label="Thêm">+</button></span><button type="button" class="mk-btn mk-sm" data-mk-qty-all ${have < 2 ? 'disabled' : ''}>Tất cả</button></span></div>
      <div class="mk-cur-row" role="group" aria-label="Nhận tiền bằng"><span>Nhận bằng</span>${[['linh_thach', 'Linh thạch', MK_GEM], [MK_CUP, 'Cực phẩm', MK_VIP]].map(([k, n, g]) => `<button type="button" class="mk-pill mk-cur${k === cur ? ' on' : ''}" data-mk-cur="${k}" aria-pressed="${k === cur}">${g}${n}</button>`).join('')}</div>
      <label class="mk-field">Tổng giá · ${cur === MK_CUP ? 'Cực phẩm linh thạch' : 'Linh thạch'}<input id="mkPrice" type="number" min="1" max="2000000000" value="${unit}"></label>
      <div class="mk-box mk-kv" id="mkNet"></div>
      <button type="submit" class="mk-btn mk-gold mk-big-btn" ${d.blocked || full ? 'disabled' : ''}>Treo bán ${d.listingDays} ngày</button>
      <small class="mk-hint">${full ? `Đã treo đủ ${d.maxOpen} tin. Hủy bớt ở tab Của tôi.` : 'Hủy tin hoặc hết hạn: vật phẩm tự về lại túi.'}</small>
    </form>`;
  }
  const max = d.unlimitedListings ? '∞' : d.maxOpen;
  return `<div class="mk-sell">
    <div class="mk-quota">
      <div class="mk-box"><div class="mk-quota-row"><small>Tin đang treo</small><b>${openN} / ${max}</b></div>${d.unlimitedListings ? '' : `<span class="mk-bar"><i style="width:${Math.min(100, openN / Math.max(1, d.maxOpen) * 100)}%"></i></span>`}</div>
      <div class="mk-box"><small>Giao dịch mỗi 24 giờ</small><b>Tối đa ${d.unlimitedListings ? '∞' : fmt(d.maxDaily)}</b></div>
      <div class="mk-box"><small>Thuế giao dịch</small><b>${d.taxPct}% · trừ vào tiền bán</b></div>
    </div>
    <div class="mk-split"><section class="mk-panel mk-main mk-stock">
      <div class="mk-toolbar"><h3>Hành trang</h3><small>${all.length} loại bán được</small><span class="mk-spacer"></span><input id="mkBagQ" type="search" aria-label="Tìm trong túi" placeholder="Tìm trong túi…" value="${mkEsc(mkBagQuery)}"><select id="mkBagSort" aria-label="Sắp xếp">${MK_BAG_SORTS.map(([k,n])=>`<option value="${k}" ${k===mkBagSort?'selected':''}>${n}</option>`).join('')}</select>${mkRef()}</div>
      <div class="mk-toolbar">${mkPills(cats, mkBagCat, 'data-mk-bag-cat')}</div>
      <div class="mk-bag">${own.length ? own.map(([k, n]) => `<button type="button" class="mk-cell r-${ITEM[k].rarity}${k === mkPick ? ' on' : ''}" aria-label="${mkEsc(ITEM[k].name)} · ${fmt(n)} món" aria-pressed="${k === mkPick}" data-mk-pick="${k}" data-card="bag" data-k="${k}">${icon(k)}<em>${n > 1 ? fmt(n) : ''}</em></button>`).join('') : `<p class="mk-empty">${all.length ? 'Không có món phù hợp.' : 'Không có vật phẩm giao dịch được.'}</p>`}</div>
    </section><aside class="mk-panel mk-side mk-post"><h3 class="mk-title">Treo bán vật phẩm</h3>${form}</aside></div></div>`;
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
  const offers = shop.offers.filter(o => ITEM[o.item]).sort((a, b) => a.daily - b.daily);   // món cố định (Thẻ đổi tên) đứng đầu
  const cats = MK_CATS.filter(([k]) => k === 'all' || offers.some(o => mkCatOf(ITEM[o.item]) === k)).map(([k, n]) => [k, n, k === 'all' ? offers.length : offers.filter(o => mkCatOf(ITEM[o.item]) === k).length]);
  if (!cats.some(([k]) => k === mkShopCat)) mkShopCat = 'all';
  const shown = offers.filter(o => mkShopCat === 'all' || mkCatOf(ITEM[o.item]) === mkShopCat);
  if (!offers.some(o => o.item === mkShopPick)) mkShopPick = (offers.find(o => left(o) > 0) || offers[0])?.item || '';
  const card = o => { const it = ITEM[o.item], out = !left(o), on = o.item === mkShopPick;
    return `<button type="button" class="mk-card r-${it.rarity}${on ? ' on' : ''}${out ? ' out' : ''}" data-mk-shop-pick="${mkEsc(o.item)}" aria-pressed="${on}" data-card="bag" data-k="${mkEsc(o.item)}"><span class="mk-badge">${out ? 'Hết phần' : o.daily ? `còn ${left(o)}/${o.limit}` : ''}</span>${icon(o.item)}<b class="rc-${it.rarity}">${mkEsc(it.name)}</b><span class="mk-cost">${MK_GEM}${fmt(o.price)}</span></button>`; };
  const o = offers.find(x => x.item === mkShopPick);
  let side = '<p class="mk-empty">Chọn một món để xem chi tiết.</p>';
  if (o){
    const l = left(o), c = can(o), have = (S.inventory[o.item] || 0) + (S.gemState?.vault?.[o.item] || 0);
    side = `${mkHead(o.item)}${mkItemDetails(o.item)}
      <div class="mk-boxes"><div class="mk-box"><small>Đang có</small><b>${fmt(have)}</b></div><div class="mk-box"><small>${o.daily ? 'Đợt này còn' : 'Giới hạn'}</small><b>${o.daily ? `${l}<small> / ${o.limit}</small>` : 'Không giới hạn'}</b></div></div>
      <div class="mk-buybar">
        ${l && c > 0 ? `<div class="mk-qty-row"><span>Số lượng</span><span class="mk-qty-ctl">${mkStep(o.item, c)}<button type="button" class="mk-btn mk-sm" data-mk-max="${mkEsc(o.item)}" ${c < 2 ? 'disabled' : ''}>Tối đa</button></span></div>` : ''}
        <div class="mk-total"><span>Đơn giá ${fmt(o.price)}</span><b data-mk-total="${mkEsc(o.item)}" data-price="${o.price}">${MK_GEM}${fmt(o.price)}</b></div>
        <button type="button" class="mk-btn mk-gold mk-big-btn" data-mk-shop="${mkEsc(shop.id)}" data-mk-shop-item="${mkEsc(o.item)}" ${c < 1 ? 'disabled' : ''}>${!l ? 'Hết phần đợt này' : c < 1 ? 'Không đủ linh thạch' : 'Mua'}</button>
      </div>`;
  }
  return `<div class="mk-split"><section class="mk-panel mk-main">
      <header class="mk-shop-head"><img class="mk-shop-keeper" src="img/hero_kimdan_nu_thumb.webp" alt=""><div class="mk-shop-say"><h3>${mkEsc(shop.name)}</h3><p>“Hàng tốt, giá có lý. Nửa ngày một mẻ mới, đạo hữu xem đi!”</p></div><div class="mk-reset"><small>Đổi hàng sau</small><b data-mk-reset="${shop.resetAt}">${mkClock(shop.resetAt - Date.now())}</b></div>${devMode && adminOn ? '<button type="button" class="mk-shop-admin" data-mk-shop-reset data-tip="Admin: đổi sang mẻ hàng khác của đợt này và mua lại từ đầu&#10;Chỉ nhân vật này thấy, hết hiệu lực khi sang đợt mới">↻ Làm mới</button>' : ''}${mkRef()}</header>
      <div class="mk-toolbar">${mkPills(cats, mkShopCat, 'data-mk-shop-cat')}<span class="mk-spacer"></span><small>Còn hàng ${offers.filter(x => left(x) > 0).length}/${offers.length}</small></div>
      <div class="mk-cards">${shown.map(card).join('') || '<p class="mk-empty">Không có món phù hợp.</p>'}</div>
    </section><aside class="mk-panel mk-side">${side}</aside></div>`;
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
  return `<section class="mk-box mk-donate"><h4>Ủng hộ game</h4><div class="mk-donate-body">
    ${d.qr ? `<img class="mk-donate-qr" src="${mkEsc(d.qr)}" alt="QR MoMo" onerror="this.remove()">` : ''}
    <ol>
      <li>${d.momo ? `<span>Chuyển MoMo:</span> <b>${mkEsc(d.momo)}</b>` : '<span>Quét mã QR bằng MoMo</span>'}${d.name ? ` · <b>${mkEsc(d.name)}</b>` : ''}</li>
      <li><span>Nội dung chuyển khoản:</span> <b>NLTT + Tên </b></li>
      <li><span>Gửi ảnh chuyển khoản để nhận code:</span> <b>${d.contact ? link : 'admin'}</b></li>
      <li>Nhập code ở ô bên dưới</li>
    </ol></div></section>`;
}
/** Tạp Hóa VIP (mkData.vip): tiêu Cực Phẩm Linh Thạch từ code donate; luôn mở kể cả khi Chợ người chơi chưa cấu hình. */
function mkVipView(){
  const vip = mkData?.vip;
  if (!vip) return `<p class="mk-empty">${mkLoading ? 'Đang tải…' : mkEsc(mkData?.error || 'Tạp Hóa VIP chưa mở cửa.')}</p>`;
  const bal = Number(vip.balance) || 0;
  const can = o => Math.floor(bal / o.price);   // không giới hạn số lần mua: tối đa theo Cực Phẩm đang có
  const price = o => `<span class="mk-cost vip">${MK_VIP}${fmt(o.price)}</span>`;
  const buy = (o, qty = true) => o.owned ? '<span class="mk-owned">✓ Đã mở</span>' : `<span class="mk-vbuy-ctl">${qty && can(o) > 1 ? mkStep(o.item, can(o)) : ''}<button type="button" class="mk-btn mk-blue mk-sm" data-vip-buy="${mkEsc(o.item)}" ${can(o) < 1 ? 'disabled' : ''}>Mua</button></span>`;
  const special = {
    goi_toc_hanh: ['Gói Tốc Hành', 'img/items/goi_toc_hanh.png', 'Mở thám hiểm ×2 (bật/tắt ở màn hình chính) · +10 Bổ Nguyên Đan Trung phẩm · +10 Thượng phẩm', false],
    goi_linh_thu: ['Gói Linh Thú', 'img/items/goi_linh_thu.png', 'Mở ra 2 Thượng Cổ Thú Đản · +1.000 Linh Thú Đan', true],
  };
  const card = o => {
    if (special[o.item]){ const [n, img, txt, qty] = special[o.item];
      return `<div class="mk-vcard mk-wide r-dao${o.owned ? ' owned' : ''}"><img class="ic" src="${img}" alt=""><div class="mk-vtext"><b class="rc-dao">${n}</b><small>${txt}</small></div><div class="mk-vbuy">${o.owned ? '' : price(o)}${buy(o, qty)}</div></div>`; }
    if (o.item === 'vip_herb'){
      const ch = (o.choices || []).filter(id => ITEM[id]), cur = ch.includes(mkVipHerb) ? mkVipHerb : ch[0];
      if (!cur) return '';
      return `<div class="mk-vcard mk-wide r-${ITEM[cur].rarity}"><span data-card="bag" data-k="${mkEsc(cur)}">${icon(cur)}</span><div class="mk-vtext"><select data-vip-herb aria-label="Chọn linh thảo">${ch.map(id => `<option value="${mkEsc(id)}" ${id === cur ? 'selected' : ''}>${mkEsc(ITEM[id].name)}</option>`).join('')}</select><small>Linh thảo tự chọn · bậc tối đa theo cảnh giới + 1</small></div><div class="mk-vbuy">${price(o)}${buy(o)}</div></div>`;
    }
    if (!ITEM[o.item]) return '';
    const it = ITEM[o.item], meta = o.item === 'linh_thach' ? `Nhận ${fmt(o.qty)} linh thạch mỗi viên` : o.qty > 1 ? `×${fmt(o.qty)} mỗi lần` : '';
    return `<div class="mk-vcard r-${it.rarity}"><span class="mk-vic" data-card="bag" data-k="${mkEsc(o.item)}">${icon(o.item)}</span><b class="rc-${it.rarity}">${mkEsc(it.name)}</b>${meta ? `<small>${meta}</small>` : ''}${price(o)}${buy(o)}</div>`;
  };
  const groups = [...new Set(vip.offers.map(o => o.group))];
  if (mkVipGroup !== 'all' && !groups.includes(mkVipGroup)) mkVipGroup = 'all';
  const pills = [['all', 'Tất cả', null], ...groups.map(g => [g, g, vip.offers.filter(o => o.group === g).length])];
  return `<div class="mk-split mk-vip"><aside class="mk-panel mk-side mk-vip-side">
      <div class="mk-balance"><small>Số dư Cực phẩm</small><b>${MK_VIP}${fmt(bal)}</b><span>1.000đ ủng hộ = 1 Cực Phẩm Linh Thạch</span></div>
      ${mkVipDonate()}
      <div class="mk-box mk-code"><label for="mkCode">Nhập code quà tặng</label><div><input id="mkCode" type="text" autocomplete="off" spellcheck="false" placeholder="Nhập code…" value="${mkEsc(mkCodeVal)}"><button type="button" class="mk-btn mk-blue" data-mk-redeem>Nhận</button></div></div>
    </aside><section class="mk-panel mk-main">
      <div class="mk-toolbar">${mkPills(pills, mkVipGroup, 'data-mk-vip-group')}<span class="mk-spacer"></span>${mkRef()}</div>
      ${groups.filter(g => mkVipGroup === 'all' || g === mkVipGroup).map(g => `<h4 class="mk-sec">${mkEsc(g)}</h4><div class="mk-vgrid">${vip.offers.filter(o => o.group === g).map(card).join('')}</div>`).join('')}
    </section></div>`;
}

const MK_STATUS = {open: 'Đang treo', sold: 'Đã bán', cancelled: 'Đã hủy', expired: 'Hết hạn'};
function mkMineView(d){
  const groups = [
    ['open', 'Đang treo', m => m.role === 'sell' && m.status === 'open'],
    ['sold', 'Đã bán', m => m.role === 'sell' && m.status === 'sold'],
    ['buy', 'Đã mua', m => m.role === 'buy'],
    ['closed', 'Hết hạn · đã hủy', m => m.role === 'sell' && (m.status === 'expired' || m.status === 'cancelled')],
  ];
  const by = Object.fromEntries(groups.map(([k, , f]) => [k, d.mine.filter(f)]));
  // tổng thu / chi tách theo loại tiền
  const sum = (list, f) => ['linh_thach', MK_CUP].map(c => [c, list.filter(m => mkCurOf(m) === c).reduce((a, m) => a + (+m[f] || 0), 0)])
    .filter(([c, n]) => n || c === 'linh_thach').map(([c, n]) => `${mkCurIc(c)}${fmt(n)}`).join(' ');
  const earned = sum(by.sold, 'payout'), spent = sum(by.buy, 'price');
  if (mkMineFilter !== 'all' && !by[mkMineFilter]) mkMineFilter = 'all';
  const row = m => {
    const it = ITEM[m.itemId], buy = m.role === 'buy', k = buy ? 'buy' : m.status;
    const money = buy ? `−${fmt(m.price)}` : m.status === 'sold' ? `+${fmt(m.payout)}` : fmt(m.price);
    const info = m.status === 'open' && !buy ? `Còn ${mkLeft(m.expiresAt)}` : buy ? `Người bán: ${mkEsc(m.otherName || '—')}` : m.status === 'sold' ? `Người mua: ${mkEsc(m.otherName || '—')}` : `${MK_STATUS[m.status] || m.status} · vật phẩm về lại túi`;
    return `<div class="mk-mrow st-${k}">${it ? `<span class="mk-tic r-${it.rarity}" data-card="bag" data-k="${mkEsc(m.itemId)}">${icon(m.itemId)}</span><b class="rc-${it.rarity}">${mkEsc(it.name)}</b>` : `<span></span><b>${mkEsc(m.itemId)}</b>`}
      <span class="mk-muted">×${fmt(m.qty)}</span><span class="mk-money">${mkCurIc(mkCurOf(m))}${money}</span><span class="mk-muted mk-info">${info}</span>
      <span class="mk-act">${!buy && m.status === 'open' ? `<button type="button" class="mk-btn mk-sm mk-danger" data-mk-cancel="${m.id}">Hủy tin</button>` : `<span class="mk-state">${buy ? 'Đã mua' : MK_STATUS[m.status] || m.status}</span>`}</span></div>`;
  };
  const max = d.unlimitedListings ? '∞' : d.maxOpen;
  const list = groups.filter(([k]) => (mkMineFilter === 'all' || mkMineFilter === k) && by[k].length).map(([k, n]) => `<div class="mk-grp">${n}</div>${by[k].map(row).join('')}`).join('');
  return `<div class="mk-mine">
    <div class="mk-stats">
      <div class="mk-stat"><i class="mk-dot st-open"></i><div><b>${by.open.length}<small> / ${max}</small></b><small>Đang treo</small></div></div>
      <div class="mk-stat"><i class="mk-dot st-sold"></i><div><b>${by.sold.length}</b><small>Đã bán · thu ${earned}</small></div></div>
      <div class="mk-stat"><i class="mk-dot st-buy"></i><div><b>${by.buy.length}</b><small>Đã mua · chi ${spent}</small></div></div>
      <div class="mk-stat"><i class="mk-dot st-closed"></i><div><b>${by.closed.length}</b><small>Hết hạn · đã hủy</small></div></div>
    </div>
    <section class="mk-panel mk-main">
      <div class="mk-toolbar"><h3>Giao dịch của tôi</h3><span class="mk-spacer"></span>${mkPills([['all', 'Tất cả', d.mine.length], ...groups.map(([k, n]) => [k, n, by[k].length])], mkMineFilter, 'data-mk-mine')}${mkRef()}</div>
      <div class="mk-mlist">${list || '<p class="mk-empty">Chưa có giao dịch nào.</p>'}</div>
      <small class="mk-hint">Thuế giao dịch ${d.taxPct}% · tin bán có hiệu lực ${d.listingDays} ngày · tiền bán và đồ trả về tự vào túi.</small>
    </section></div>`;
}

/** Vị trí cuộn của các vùng đang cuộn trong Chợ, khóa theo tên lớp + thứ tự (khung vẽ lại có cùng cấu trúc). */
function mkScrolls(pane){
  const seen = {}, out = [];
  for (const el of pane.querySelectorAll('*')){
    if (!el.scrollTop && !el.scrollLeft) continue;
    const k = el.className.toString(), i = seen[k] = (seen[k] ?? -1) + 1;
    out.push([k, i, el.scrollTop, el.scrollLeft]);
  }
  return out;
}
function mkRestoreScrolls(pane, list){
  for (const [k, i, top, left] of list){
    const el = [...pane.getElementsByClassName(k.split(/\s+/)[0] || '')].filter(x => x.className.toString() === k)[i];
    if (el){ el.scrollTop = top; el.scrollLeft = left; }
  }
}
function renderMarket(){
  const pane = $('pane-market'); if (!pane || !S) return;
  document.querySelectorAll('[data-mk-view]').forEach(b => b.setAttribute('aria-selected', String(b.dataset.mkView === mkView)));
  const d = mkData;
  const keep = ['mkQ', 'mkBagQ', 'mkCode'].includes(document.activeElement?.id) ? document.activeElement.id : '';
  let body;
  if (mkView === 'shop') body = mkShopView();
  else if (mkView === 'vip') body = mkVipView();
  else if (!d) body = `<p class="mk-empty">${mkLoading ? 'Đang tải…' : ''}</p>`;
  else if (!d.enabled) body = '<p class="mk-note">Chợ chưa được cấu hình (appsettings.json → Leaderboard: Url, AnonKey).</p>';
  else body = (d.error ? `<p class="mk-note">${mkEsc(d.error)}</p>` : '') + (d.blocked ? `<p class="mk-note">${mkEsc(d.blocked)}</p>` : '')
    + (mkView === 'sell' ? mkSellView(d) : mkView === 'mine' ? mkMineView(d) : mkBuyView(d));
  // vẽ lại thay mới cả khung: giữ vị trí cuộn của từng vùng trong cùng tab con (bấm chọn món, đổi số lượng, tự làm mới 30 giây…)
  const scrolls = pane.dataset.mkv === mkView ? mkScrolls(pane) : null;
  pane.innerHTML = `<div class="mk mk-v-${mkView}">
    <div id="mkNotice" class="mk-notice${mkNoticeBad ? ' bad' : ''}" role="status" aria-live="polite" ${mkNotice ? '' : 'hidden'}>${mkEsc(mkNotice)}</div>
    <div class="mk-body" aria-busy="${mkLoading}">${body}</div></div>`;
  pane.dataset.mkv = mkView;
  if (scrolls) mkRestoreScrolls(pane, scrolls);
  if (keep){ const q = $(keep); if (q){ q.focus({preventScroll: true}); try { q.setSelectionRange(q.value.length, q.value.length); } catch(e){} } }
  mkNet();
}
function mkNet(){
  const el = $('mkNet'); if (!el || !mkData) return;
  const qty = Math.max(1, +$('mkQty').value || 1), price = Math.max(0, +$('mkPrice').value || 0), fee = Math.ceil(price * mkData.taxPct / 100);
  el.innerHTML = `<span>Đơn giá</span><b>${fmt(Math.ceil(price / qty))}</b><span>Thuế ${mkData.taxPct}%</span><b class="mk-minus">−${fmt(fee)}</b><span>Thu về khi bán hết</span><b class="mk-gain">${mkCurIc(mkSellCur)}${fmt(price - fee)}</b>`;
}

function initMarket(){
  document.querySelector('.subtabs[data-group="market"]')?.addEventListener('click', e => {
    const b = e.target.closest('[data-mk-view]'); if (!b) return;
    mkView = b.dataset.mkView; renderMarket();
  });
  const pane = $('pane-market'); if (!pane) return;
  const qtyInput = key => key === 'mkQty' ? $('mkQty') : pane.querySelector(`[data-mk-shop-qty="${CSS.escape(key)}"]`);
  const redeem = () => {
    const code = $('mkCode')?.value.trim(), btn = pane.querySelector('[data-mk-redeem]');
    if (!code || typeof redeemCode !== 'function') return;
    if (btn) btn.disabled = true;
    mkCodeVal = '';
    Promise.resolve(redeemCode(code)).finally(() => loadMarket(true));
  };
  pane.addEventListener('click', e => {
    const v = e.target.closest('[data-mk-view]'); if (v){ mkView = v.dataset.mkView; renderMarket(); return; }
    if (e.target.closest('[data-mk-ref]')){ loadMarket(true); return; }
    const rs = e.target.closest('[data-mk-shop-reset]');
    if (rs){ rs.disabled = true; hideTip(); adminAct('shopreset').then(() => loadMarket(true)); return; }   // admin: mẻ hàng Tạp Hóa mới
    const pill = e.target.closest('[data-mk-shop-cat],[data-mk-vip-group],[data-mk-bag-cat],[data-mk-mine]');
    if (pill){
      if ('mkShopCat' in pill.dataset) mkShopCat = pill.dataset.mkShopCat;
      else if ('mkVipGroup' in pill.dataset) mkVipGroup = pill.dataset.mkVipGroup;
      else if ('mkBagCat' in pill.dataset) mkBagCat = pill.dataset.mkBagCat;
      else mkMineFilter = pill.dataset.mkMine;
      renderMarket(); return;
    }
    const sp = e.target.closest('[data-mk-shop-pick]'); if (sp){ mkShopPick = sp.dataset.mkShopPick; renderMarket(); return; }
    const st = e.target.closest('[data-mk-step]');
    if (st){
      const inp = qtyInput(st.dataset.for); if (!inp) return;
      inp.value = String(Math.max(1, Math.min(Number(inp.max) || 1, (Number(inp.value) || 1) + Number(st.dataset.mkStep))));
      if (inp.id === 'mkQty') mkNet(); else mkShopTotal(inp);
      return;
    }
    const mx = e.target.closest('[data-mk-max]'); if (mx){ const inp = qtyInput(mx.dataset.mkMax); if (inp){ inp.value = inp.max; mkShopTotal(inp); } return; }
    if (e.target.closest('[data-mk-qty-all]')){ const inp = $('mkQty'); if (inp){ inp.value = inp.max; mkNet(); } return; }
    if (e.target.closest('[data-mk-redeem]')){ redeem(); return; }
    const pk = e.target.closest('[data-mk-pick]'); if (pk){ mkPick = pk.dataset.mkPick; renderMarket(); return; }
    const cu = e.target.closest('[data-mk-cur]');
    if (cu){                                        // đổi tiền nhận: giữ số lượng, giá về mức tham khảo của loại tiền mới
      if (cu.dataset.mkCur === mkSellCur) return;
      const qty = $('mkQty')?.value;
      mkSellCur = cu.dataset.mkCur; renderMarket();
      if ($('mkQty') && qty !== undefined) $('mkQty').value = qty;
      mkNet(); return;
    }
    const b = e.target.closest('[data-mk-buy]');
    if (b){                                         // bấm 2 lần để xác nhận mua
      if (!b.dataset.armed){ b.dataset.armed = 1; b.dataset.label = b.textContent; b.textContent = 'Xác nhận mua?'; b.classList.add('arm'); setTimeout(() => { if (b.isConnected){ delete b.dataset.armed; b.textContent = b.dataset.label; b.classList.remove('arm'); } }, 3000); }
      else mkAct(`buy/${b.dataset.mkBuy}`);
      return;
    }
    const bp = e.target.closest('[data-mk-buy-pick]'); if (bp){ mkBuyPick = Number(bp.dataset.mkBuyPick); renderMarket(); return; }
    const vb = e.target.closest('[data-vip-buy]');
    if (vb){
      const key = vb.dataset.vipBuy, n = mkClampShopQty(qtyInput(key), true), item = key === 'vip_herb' ? pane.querySelector('[data-vip-herb]')?.value || key : key;
      vb.disabled = true; act(`vip/${item}?n=${n}`).then(() => loadMarket(true)); return;
    }
    const sh = e.target.closest('[data-mk-shop]');
    if (sh){
      const n = mkClampShopQty(qtyInput(sh.dataset.mkShopItem), true);
      sh.disabled = true; act(`shop/${sh.dataset.mkShop}/${sh.dataset.mkShopItem}?n=${n}`).then(() => loadMarket(true)); return;
    }
    const c = e.target.closest('[data-mk-cancel]'); if (c) mkAct(`cancel/${c.dataset.mkCancel}`);
  });
  pane.addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.id === 'mkCode'){ e.preventDefault(); redeem(); } });
  pane.addEventListener('input', e => {
    if (e.target.matches('[data-mk-shop-qty]')){ mkClampShopQty(e.target); mkShopTotal(e.target); }
    if (e.target.id === 'mkQ'){ mkQuery = e.target.value; renderMarket(); }
    if (e.target.id === 'mkBagQ'){ mkBagQuery = e.target.value; renderMarket(); }
    if (e.target.id === 'mkCode') mkCodeVal = e.target.value;
    if (e.target.id === 'mkQty' || e.target.id === 'mkPrice') mkNet();
  });
  pane.addEventListener('change', e => {
    if (e.target.matches('[data-mk-shop-qty]')){ mkClampShopQty(e.target, true); mkShopTotal(e.target); }
    if (e.target.matches('[data-vip-herb]')){ mkVipHerb = e.target.value; renderMarket(); }
    if (e.target.id === 'mkHideMine'){ mkHideMine = e.target.checked; renderMarket(); }
    if (e.target.id === 'mkBagSort'){
      const qty = $('mkQty')?.value, price = $('mkPrice')?.value;
      mkBagSort = e.target.value; renderMarket();
      if ($('mkQty') && qty !== undefined) $('mkQty').value = qty;
      if ($('mkPrice') && price !== undefined) $('mkPrice').value = price;
      mkNet();
    }
    if (e.target.id === 'mkCat'){ mkCat = e.target.value; renderMarket(); }
    if (e.target.id === 'mkSort'){ mkSort = e.target.value; renderMarket(); }
    if (e.target.id === 'mkCurF'){ mkCurFilter = e.target.value; renderMarket(); }
  });
  pane.addEventListener('submit', e => {
    if (e.target.id !== 'mkSell') return;
    e.preventDefault();
    const qty = Math.min(S.inventory[mkPick] || 0, Math.max(1, Math.floor(+$('mkQty').value || 1)));
    const price = Math.floor(+$('mkPrice').value || 0);
    if (price < 1) return;
    mkList(mkPick, qty, price, mkSellCur);
  });
  setInterval(() => {                               // đếm ngược đổi hàng Tạp Hóa; hết giờ thì tải đợt mới
    const el = pane.querySelector('[data-mk-reset]'); if (!el) return;
    const ms = +el.dataset.mkReset - Date.now();
    el.textContent = mkClock(ms);
    if (ms <= 0 && !mkLoading){ delete el.dataset.mkReset; loadMarket(true); }
  }, 1000);
  setInterval(() => { if (!pane.hidden && !document.hidden && !busy && document.activeElement?.closest?.('#pane-market') == null) loadMarket(true); }, 30000);
}
