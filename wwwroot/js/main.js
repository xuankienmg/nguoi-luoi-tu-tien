/* Vấn Đạo · js/main.js: vòng lặp, tooltip, hiệu ứng đột phá, Admin, khởi động. Tách từ index.html (tools/split_index.py). */
/* ============================================================
   VÒNG LẶP & KHỞI ĐỘNG
   ============================================================ */
let lastFrame = performance.now(), lastLive = 0;
function frame(now){
  const dt = Math.min(.1, (now - lastFrame)/1000); lastFrame = now;
  if (W && S && MODE !== 'panel') drawScene(now/1000, dt);
  if (S) drawInventoryAvatar(now/1000);
  if (now - lastLive > 200){ live(); lastLive = now; }
  requestAnimationFrame(frame);
}

// Báo cho cửa sổ C# khung game đã vẽ xong (tin "shape") để hiện dải lần đầu.
let lastShape = '';
function reportShape(force){
  if (MODE !== 'strip' || !window.chrome?.webview) return;
  const d = devicePixelRatio || 1, c = cv.getBoundingClientRect();
  const qi = document.querySelector('.altar .qi').getBoundingClientRect(), al = document.querySelector('.altar').getBoundingClientRect();
  const px = v => Math.round(v * d);
  const shape = { card: {x:px(al.left), y:px(al.top), w:px(al.width), h:px(al.height), r:px(8)} };
  const json = JSON.stringify(shape);
  if (!force && json === lastShape) return;
  lastShape = json;
  notifyHost('shape', shape);
}
window.reportShape = reportShape;

// Tooltip khi rê chuột lên phần tử có data-tip (giữ trong vùng hiển thị của cửa sổ)
let tipEl = null;
function showTip(el){
  if (!el || !el.isConnected || !el.dataset.tip || (sortOpen && el.id === 'sortBtn') || (sellOpen && el.id === 'sellBtn')){ hideTip(); return; }
  tipEl = tipEl || $('tip');
  tipEl._for = el;
  tipEl.classList.remove('rich');
  if (el.dataset.card && el.dataset.k && ITEM[el.dataset.k]){ tipEl.innerHTML = itemCardHtml(el.dataset.k, el.dataset.card, el.dataset.slot, +(el.dataset.copy || 0)); tipEl.classList.add('card'); }
  else if (el.dataset.skcard && typeof skById === 'function' && skById(el.dataset.skcard)){ tipEl.innerHTML = skTipHtml(skById(el.dataset.skcard)); tipEl.classList.add('card'); }
  else if (el.dataset.tipTpl && $(el.dataset.tipTpl)){ tipEl.innerHTML = $(el.dataset.tipTpl).innerHTML; tipEl.classList.remove('card'); tipEl.classList.add('rich'); }   // tooltip có bố cục: nội dung lấy từ <template>
  else {
    const [head, ...rest] = el.dataset.tip.split('\n');
    tipEl.innerHTML = `<b>${head}</b>${rest.length ? '\n' + rest.join('\n') : ''}`;
    tipEl.classList.remove('card');
  }
  tipEl.hidden = false;
  const r = el.getBoundingClientRect(), t = tipEl.getBoundingClientRect();
  let box = {left:4, top:4, right:innerWidth-4, bottom:innerHeight-4};
  if (MODE === 'strip'){ const q = document.querySelector('.altar').getBoundingClientRect(); box = {left:q.left+2, top:q.top+2, right:q.right-2, bottom:q.bottom-2}; }
  let x = r.left + r.width/2 - t.width/2, y = r.top - t.height - 6;
  if (tipEl.classList.contains('card')){
    // thẻ chi tiết: đặt bên phải ô, hết chỗ thì bên trái
    x = r.right + 8 + t.width <= box.right ? r.right + 8 : r.left - 8 - t.width;
    y = r.top;
  }
  else if (y < box.top) y = r.bottom + 6;
  if (y + t.height > box.bottom) y = Math.max(box.top, box.bottom - t.height);
  x = Math.max(box.left, Math.min(x, box.right - t.width));
  tipEl.style.left = x + 'px'; tipEl.style.top = y + 'px';
}
function hideTip(){ if (tipEl){ tipEl.hidden = true; tipEl._for = null; } }
document.addEventListener('mouseover', e => { const el = e.target.closest('[data-tip]'); if (el) showTip(el); else hideTip(); });
document.addEventListener('mouseleave', hideTip);
document.addEventListener('focusin', e => { const el = e.target.closest('[data-tip]'); if (el) showTip(el); });
document.addEventListener('focusout', hideTip);

// ===== Hiệu ứng đột phá =====
// kind: 'good' lên tầng · 'gold' lên cảnh giới mới · 'bad' thất bại
let breakFx = null, burstTimer = 0;
async function playBreak(kind, fromHost){
  if (kind === 'crit') kind = 'gold'; else if (kind === 'major') kind = 'bad';
  if (fromHost){                                 // cửa sổ khác vừa đột phá → lấy trạng thái mới trước
    await sync();
    await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));   // đợi lần vẽ lại xong rồi mới chạy hiệu ứng, tránh khựng khung đầu
  }
  const el = $('burst'), title = el.querySelector('b'), sub = el.querySelector('small');
  // chữ "Thành công" / "Thất bại" tách từng ký tự để chạy hiệu ứng lần lượt
  const word = kind === 'bad' ? 'Thất bại' : 'Thành công';
  title.innerHTML = [...word].map((ch, i) => `<i style="--i:${i}">${ch === ' ' ? '&nbsp;' : ch}</i>`).join('');
  if (kind === 'gold') sub.textContent = `Đột phá ${S.realmName} · thiên địa dị tượng`;
  else if (kind === 'bad') sub.textContent = `Linh khí phản phệ · lần sau +${Math.round(S.bonus*100)}% tỉ lệ`;
  else sub.textContent = `${S.realmName} ${S.stageName}`;
  el.className = 'burst ' + kind; el.hidden = true; void el.offsetWidth; el.hidden = false;   // chạy lại animation
  clearTimeout(burstTimer); burstTimer = setTimeout(() => { el.hidden = true; }, 2300);
  breakFx = {kind, t: 0, dur: kind === 'gold' ? 2.4 : kind === 'bad' ? 1.2 : 1.6};
  const r = realm();
  rings.push({t:0, c: kind === 'bad' ? '#e58472' : kind === 'gold' ? '#f2c94c' : r.aura, big: kind === 'gold'});
  if (kind === 'gold') setTimeout(() => rings.push({t:0, c: r.aura, big: true}), 350);
}
window.playBreak = playBreak;

// Các hàm cửa sổ desktop (C#) gọi vào
// =====================================================================
// Cờ god / break / fast lưu ở server theo nhân vật (S.admin), các lệnh khác thực hiện một lần.
// =====================================================================
let adminOn = false, devMode = false;   // devMode: server chạy môi trường Development (GET /api/env) — chỉ khi đó mới có admin mode
try { adminOn = localStorage.getItem('van-dao-admin') === '1'; } catch(e){}
let realmClicks = 0, realmClickAt = 0;
document.addEventListener('click', e => {
  if (e.target.closest('#sRealm, #hudRealm, .hud .realm')){
    const now = Date.now();
    if (now - realmClickAt < 1500) { realmClicks++; } else { realmClicks = 1; }
    realmClickAt = now;
    if (realmClicks >= 3){ realmClicks = 0; setAdmin(!adminOn); }
  }
});
function setAdmin(on){
  if (on && !devMode) return;   // bản phát hành: không có admin mode (server cũng chặn lệnh admin)
  adminOn = on; try { localStorage.setItem('van-dao-admin', on ? '1' : '0'); } catch(e){}
  document.body.classList.toggle('admin-on', on);
  const t = $('tab-admin'); if (t) t.hidden = !on;
  if (S && DATA) renderMap();                   // mô tả ô ải chỉ hiện khi bật admin
  if (on){ renderAdmin(true); openTab('admin'); } else if ($('tab-admin')?.getAttribute('aria-selected') === 'true') openTab('req');
  localLog(on ? 'Admin mode: BẬT' : 'Admin mode: TẮT', on ? 'good' : '');
}
addEventListener('keydown', e => {
  if (e.ctrlKey && e.shiftKey && (e.key === 'A' || e.key === 'a')){
    e.preventDefault(); if (e.repeat) return;
    // nhấn 2 lần trong 1 giây mới bật / tắt admin mode
    if (Date.now() - adminArmAt < 1000){ adminArmAt = 0; setAdmin(!adminOn); } else adminArmAt = Date.now();
  }
  // phím ẩn Alt+Shift+R: reset nhân vật về từ đầu (bấm 2 lần trong 3 giây để xác nhận, giống nút trong Admin)
  if (e.altKey && e.shiftKey && !e.ctrlKey && e.code === 'KeyR'){
    e.preventDefault();
    if (Date.now() - resetArmAt < 3000){ resetArmAt = 0; devMode ? adminAct('resetall') : act('reset'); }   // bản phát hành: reset thường (không qua admin)
    else { resetArmAt = Date.now(); localLog('Nhấn Alt+Shift+R lần nữa trong 3 giây để reset nhân vật về từ đầu (mất toàn bộ tiến trình).', 'bad'); }
  }
});
let resetArmAt = 0, adminArmAt = 0;
const adminAct = (action, arg, n) => act(`admin/${action}?${new URLSearchParams({...(arg != null ? {arg} : {}), ...(n ? {n} : {})})}`);
let admKey = '';
/** Admin: cờ "boss" — khiêu chiến Boss không cần chìa khóa. */
const adminBoss = () => (S?.admin || '').split(',').includes('boss');
const admCatalog = {group:'gear', query:'', quality:'', set:'', slot:'', category:'', theme:'', source:'', selected:'', qty:1, page:0};
let admRecent = [];
try { const saved = JSON.parse(localStorage.getItem('van-dao-adm-recent') || '[]'); if (Array.isArray(saved)) admRecent = saved.filter(x => typeof x === 'string').slice(0,10); } catch(e){}
const admEsc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const admSearch = value => String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d').replace(/Đ/g,'D').toLowerCase();
const admGroups = [['gear','Trang bị'],['gem','Tinh Thạch'],['pill','Đan dược'],['material','Nguyên liệu'],['chest','Rương'],['pet','Linh thú'],['other','Khác']];
function admGroup(it){
  if (it.slot) return 'gear';
  if (it.type === 'gem') return 'gem';
  if (['pill','elixir'].includes(it.type)) return 'pill';
  if (['stone','herb','boss','key'].includes(it.type)) return 'material';
  if (['chest','skin'].includes(it.type)) return 'chest';
  if (['egg','petfood','petmaterial','petgear'].includes(it.type)) return 'pet';
  return 'other';
}
function admCatalogItems(){
  const a = admCatalog, terms = admSearch(a.query).split(/\s+/).filter(Boolean);
  return DATA.items.filter(it => admGroup(it) === a.group
    && (!a.quality || it.rarity === a.quality)
    && (!a.set || it.set === a.set) && (!a.slot || it.slot === a.slot)
    && (!a.category || it.gemCategory === a.category)
    && (!a.theme || it.name.split(' · ')[0] === a.theme)
    && (!a.source || it.type === a.source)
    && terms.every(t => admSearch([it.name,it.id,it.base,it.kind,it.set].join(' ')).includes(t)))
    .sort((x,y) => RAR_KEYS.indexOf(x.rarity)-RAR_KEYS.indexOf(y.rarity) || x.name.localeCompare(y.name,'vi'));
}
function admCatalogHtml(){
  const a=admCatalog, items=admCatalogItems(), size=36, pages=Math.max(1,Math.ceil(items.length/size));
  a.page=Math.max(0,Math.min(a.page,pages-1));
  const groupItems=DATA.items.filter(i=>admGroup(i)===a.group);
  const options=(key,label,rows)=>`<label>${label}<select data-adm-filter="${key}"><option value="">Tất cả</option>${rows.map(([id,name])=>`<option value="${admEsc(id)}" ${a[key]===id?'selected':''}>${admEsc(name)}</option>`).join('')}</select></label>`;
  const unique=key=>[...new Set(groupItems.map(i=>i[key]).filter(Boolean))].sort((x,y)=>x.localeCompare(y,'vi')).map(v=>[v,v]);
  const selected=ITEM[a.selected];
  const card=it=>`<button type="button" class="adm-item ${a.selected===it.id?'selected':''} r-${it.rarity}" data-adm-pick="${admEsc(it.id)}" data-card="admin-item" data-k="${admEsc(it.id)}" data-tip="${admEsc(it.name)}" aria-pressed="${a.selected===it.id}">${itemImg(it.id)}<span><b class="rc-${it.rarity}">${admEsc(it.name)}</b><small>${admEsc(rar(it).n)}${it.itemLevel?' · Cấp '+it.itemLevel:''}${it.gemCategory?' · '+(it.gemCategory==='defense'?'Phòng thủ':'Tấn công'):''}</small></span></button>`;
  return `<nav class="adm-catalog-groups" aria-label="Nhóm vật phẩm">${admGroups.map(([id,name])=>`<button data-adm-group="${id}" class="${a.group===id?'on':''}" aria-pressed="${a.group===id}">${name}</button>`).join('')}</nav>
    <div class="adm-catalog-filters"><label class="adm-search">Tìm vật phẩm<input id="admSearch" value="${admEsc(a.query)}" placeholder="Tên hoặc ID · hỗ trợ không dấu" autocomplete="off"></label>
    ${options('quality','Phẩm',RAR_KEYS.map((r,i)=>[r,Q_NAMES[i]]))}
    ${a.group==='gear'?options('set','Bộ',unique('set'))+options('slot','Vị trí',(DATA.slots||[]).map(x=>[x.id,x.name])):''}
    ${a.group==='gem'?options('category','Loại',[['attack','Tấn công'],['defense','Phòng thủ']])+options('theme','Dòng',[...new Set(groupItems.map(i=>i.name.split(' · ')[0]))].sort().map(x=>[x,x])):''}
    ${a.group==='material'?options('source','Loại nguyên liệu',[['stone','Tài nguyên'],['herb','Linh thảo'],['boss','Nguyên liệu Boss'],['key','Chìa khóa']]):''}</div>
    <div class="adm-catalog-count">${items.length} vật phẩm <span><button data-adm-page="-1" ${a.page===0?'disabled':''} aria-label="Trang trước">‹</button> ${a.page+1}/${pages} <button data-adm-page="1" ${a.page+1>=pages?'disabled':''} aria-label="Trang sau">›</button></span></div>
    <div class="adm-catalog-grid">${items.slice(a.page*size,(a.page+1)*size).map(i=>card(i)).join('')||'<p>Không tìm thấy vật phẩm. Thử bỏ bớt bộ lọc.</p>'}</div>
    <div class="adm-catalog-receive"><div>${selected?`<b class="rc-${selected.rarity}">${admEsc(selected.name)}</b><small>${admEsc(selected.id)}</small>`:'Chọn vật phẩm trong danh sách'}</div><label>Số lượng<input id="admCatalogQty" type="number" min="1" max="1000000" value="${a.qty}"></label><button data-adm="cataloggive" ${!selected||busy?'disabled':''}>Nhận món</button>${selected?.slot&&selected.set?`<button data-adm="catalogset" ${busy?'disabled':''}>Nhận cả bộ</button>`:''}</div>
    ${admRecent.some(id=>ITEM[id])?`<details class="adm-catalog-recent"><summary>Đã dùng gần đây</summary><div class="adm-catalog-grid">${admRecent.filter(id=>ITEM[id]).map(id=>card(ITEM[id])).join('')}</div></details>`:''}`;
}
function admPaintCatalog(){const host=$('admCatalog');if(host)host.innerHTML=admCatalogHtml();}
async function admReceiveCatalog(wholeSet){
  const it=ITEM[admCatalog.selected];if(!it||busy)return;
  const qty=Math.max(1,Math.min(1000000,Math.floor(admCatalog.qty)||1));
  if(wholeSet && (!it.slot||!it.set))return;
  const result=await adminAct(wholeSet?'giveset':'give',wholeSet?`${it.set}|${RAR_KEYS.indexOf(it.rarity)}`:it.id,qty);
  if(result){admRecent=[it.id,...admRecent.filter(id=>id!==it.id)].slice(0,10);try{localStorage.setItem('van-dao-adm-recent',JSON.stringify(admRecent));}catch(e){}admPaintCatalog();}
}
document.addEventListener('click',e=>{
  const group=e.target.closest('#admCatalog [data-adm-group]'), pick=e.target.closest('#admCatalog [data-adm-pick]'), page=e.target.closest('#admCatalog [data-adm-page]');
  if(group){Object.assign(admCatalog,{group:group.dataset.admGroup,quality:'',set:'',slot:'',category:'',theme:'',source:'',selected:'',page:0});admPaintCatalog();}
  else if(pick){admCatalog.selected=pick.dataset.admPick;admPaintCatalog();}
  else if(page&&!page.disabled){admCatalog.page+=Number(page.dataset.admPage);admPaintCatalog();}
});
document.addEventListener('input',e=>{
  if(e.target.id==='admCatalogQty'){admCatalog.qty=Math.max(1,Math.min(1000000,Math.floor(+e.target.value)||1));return;}
  if(e.target.id!=='admSearch')return;
  const pos=e.target.selectionStart;admCatalog.query=e.target.value;admCatalog.page=0;admPaintCatalog();const input=$('admSearch');input.focus();input.setSelectionRange(pos,pos);
});
document.addEventListener('change',e=>{
  const key=e.target.closest('#admCatalog [data-adm-filter]')?.dataset.admFilter;if(!key)return;
  const previous=ITEM[admCatalog.selected];admCatalog[key]=e.target.value;admCatalog.page=0;
  const items=admCatalogItems();
  if(!items.some(i=>i.id===admCatalog.selected)){
    const variant=key==='quality'&&previous?items.find(i=>previous.base?i.base===previous.base:i.type==='gem'&&i.name.split(' · ')[0]===previous.name.split(' · ')[0]):null;
    admCatalog.selected=variant?.id||'';
  }
  admPaintCatalog();
});
function renderAdmin(force){
  const pane = $('pane-admin'); if (!pane || !S) return;
  document.body.classList.toggle('admin-on', adminOn);
  $('tab-admin').hidden = !adminOn;
  if (!adminOn){ pane.innerHTML = ''; admKey = ''; return; }
  const flags = (S.admin || '').split(',').filter(Boolean);
  // trạng thái game cập nhật liên tục: chỉ vẽ lại khi có thay đổi liên quan, để không mất ô đang nhập
  const ownedPets = S.pet?.owned || [];
  const key = [S.admin, S.skillPoints, S.realm, S.stage, ownedPets.map(x => `${x.id}:${x.stars}:${x.level}`).join()].join('|');
  if (!force && key === admKey && pane.innerHTML) return;
  admKey = key;
  const keepRealm = $('admRealm')?.value;
  const keepPetN = $('admPetN')?.value, keepPet = $('admPet')?.value, keepPetStar = $('admPetStar')?.value, keepEl = $('admEl')?.value;
  // Linh thú: mọi Thú Đản + đan dược / nguyên liệu linh thú (theo thứ tự khai báo ở server)
  const petItems = DATA.items.filter(i => ['egg', 'petfood', 'petmaterial', 'petgear'].includes(i.type));
  const realms = DATA.realms.flatMap((r, i) => r.stages.map((st, j) => `<option value="${i}:${j}" ${i === S.realm && j === S.stage ? 'selected' : ''}>${r.name} · ${st}</option>`)).join('');
  const F = (id, n, d) => `<button data-adm="flag" data-arg="${id}" class="${flags.includes(id) ? 'on' : ''}" title="${d}">${n}: ${flags.includes(id) ? 'BẬT' : 'tắt'}</button>`;
  pane.innerHTML = `<div class="adm-p">
    <section><h4>Chế độ</h4><div class="adm-row">${F('god', 'Bất tử', 'Quái không gây sát thương, không thua trận')}${F('break', 'Đột phá chắc chắn', 'Tỉ lệ 100%, bỏ điều tức')}<span class="adm-grp">Tốc độ thám hiểm: ${[['fast05', '0.5'], ['fast2', '2'], ['fast3', '3'], ['fast5', '5']].map(([id, k]) => `<button data-adm="flag" data-arg="${id}" class="${flags.includes(id) || (id === 'fast3' && flags.includes('fast')) ? 'on' : ''}" title="${k === '0.5' ? 'Mỗi chuyến chậm gấp đôi' : `Mỗi chuyến nhanh gấp ${k} lần`} · bấm lần nữa để tắt">×${k}</button>`).join('')}</span>${F('boss', 'Boss không cần chìa khóa', 'Khiêu chiến Boss không cần và không tiêu hao chìa khóa, đánh liên tục tới khi rời ải Boss')}${F('map', 'Mở full bản đồ', 'Mở mọi chương, mọi ải và cả mức Khó / Ác mộng · không đánh dấu đã hạ Boss nên vẫn nhận thưởng hạ Boss lần đầu · tắt là về tiến độ thật')}</div>
      <small>Cờ chế độ lưu theo nhân vật cho tới khi tắt.</small></section>
    <section><h4>Tài nguyên & vật phẩm</h4>
      <div class="adm-row"><button data-adm="stones" data-n="1000">+1.000 linh thạch</button><button data-adm="stones" data-n="100000">+100.000</button><button data-adm="stones" data-n="10000000">+10 triệu</button></div>
      <div id="admCatalog">${admCatalogHtml()}</div></section>
    <section><h4>Tu vi & cảnh giới</h4>
      <div class="adm-row"><button data-adm="qi">Đầy tu vi</button><button data-adm="clearcd">Xóa điều tức / suy yếu</button></div>
      <div class="adm-row"><select id="admRealm">${realms}</select><button data-adm="realm">Chuyển tới</button></div></section>
    <section><h4>Kỹ năng</h4>
      <div class="adm-row"><button data-adm="points" data-n="5">+5 điểm</button><button data-adm="points" data-n="50">+50 điểm</button><select id="admEl">${DATA.elements.filter(e => e.open).map(e => `<option value="${e.id}" ${e.id === (keepEl || 'hoa') ? 'selected' : ''}>${e.name} hệ</option>`).join('')}<option value="all" ${keepEl === 'all' ? 'selected' : ''}>Tất cả hệ</option></select><button data-adm="maxskill">Học max</button><button data-adm="resetskills">Reset cây kỹ năng</button></div>
      <small>Điểm kỹ năng: ${S.skillPoints}</small></section>
    <section><h4>Linh Thú</h4>
      <div class="adm-row">${F('petevo', 'Tiến hóa không cần điều kiện', 'Tiến hóa linh thú bỏ qua cấp, cảnh giới và không tốn nguyên liệu')}</div>
      <div class="adm-row"><select id="admPet">${petItems.map(it => `<option value="${it.id}" ${it.id === keepPet ? 'selected' : ''}>${it.name}</option>`).join('')}</select>
        <input id="admPetN" type="number" min="1" value="${keepPetN || 1}"><button data-adm="petgive">Nhận</button><button data-adm="petkit">Nhận tất cả</button></div>
      <div class="adm-row"><select id="admPetStar">${ownedPets.length ? ownedPets.map(x => `<option value="${x.id}" ${x.id === keepPetStar ? 'selected' : ''}>${x.name} · Lv.${x.level} · ${x.stars || 0}★</option>`).join('') : '<option value="">Chưa có linh thú</option>'}</select>
        <button data-adm="petstar" data-n="1">+1★</button><button data-adm="petstar" data-n="5">Tối đa 5★</button>
        <button data-adm="petlevel" data-n="10">+10 cấp</button><button data-adm="petlevel" data-n="1000">Max cấp (${S.pet?.levelCap ?? 100})</button></div>
      <small>Chọn Thú Đản / đan dược linh thú để nhận theo số lượng · "Nhận tất cả" cho mọi loại · Tăng sao / cấp cho linh thú đã chọn, không cần con hiến hay Linh Thú Đan.</small></section>
    <section><h4>Thám hiểm & trận đánh</h4>
      <div class="adm-row"><button data-adm="explore">Kết thúc chuyến ngay</button><button data-adm="unlock">Mở toàn bộ ải + chìa khóa Boss</button></div></section>
    <section><h4>Nhân vật</h4>
      <div class="adm-row"><button data-adm="clearflag">Gỡ cờ cảnh báo dữ liệu</button></div>
      <div class="adm-row"><button data-adm="resetall">Reset nhân vật về từ đầu</button></div>
      <small>Xóa toàn bộ tu vi, cảnh giới, vật phẩm, trang bị, kỹ năng, ải và nhật ký. Giữ tên và cờ chế độ admin.</small></section>
  </div>`;
  if (keepRealm && $('admRealm')) $('admRealm').value = keepRealm;
}
document.addEventListener('click', async e => {
  const b = e.target.closest('#pane-admin [data-adm]'); if (!b) return;
  const a = b.dataset.adm;
  if (a === 'cataloggive' || a === 'catalogset') { await admReceiveCatalog(a === 'catalogset'); }
  else if (a === 'petgive') adminAct('give', $('admPet').value, Math.max(1, +$('admPetN').value || 1));
  else if (a === 'petstar' || a === 'petlevel') adminAct(a, $('admPetStar').value, +b.dataset.n);
  else if (a === 'petkit') adminAct('petkit', null, Math.max(1, +$('admPetN').value || 1));
  else if (a === 'give') adminAct('give', b.dataset.arg || $('admItem').value, Math.max(1, +(b.dataset.n || $('admN').value || 1)));
  else if (a === 'realm') adminAct('realm', $('admRealm').value);
  else if (a === 'maxskill') adminAct('maxskill', $('admEl').value);
  else if (a === 'resetskills' && !b.dataset.armed){ b.dataset.armed = 1; b.textContent = 'Bấm lần nữa để reset'; setTimeout(() => renderAdmin(true), 3000); }
  else if (a === 'resetall' && !b.dataset.armed){ b.dataset.armed = 1; b.textContent = 'Bấm lần nữa để xóa sạch nhân vật'; setTimeout(() => renderAdmin(true), 3000); }
  else adminAct(a, b.dataset.arg ?? null, b.dataset.n ? +b.dataset.n : 0);
});
function openTab(name, exact){
  document.body.classList.toggle('rank-panel', name === 'rank');
  if (name === 'craft' && !exact) name = 'fuse';                // tab Luyện chế: mặc định mở Luyện khí
  if (LC_SUBS.includes(name)) lcSub = name;
  const top = LC_SUBS.includes(name) ? 'craft' : name;
  document.querySelectorAll('.tab').forEach(x => x.setAttribute('aria-selected', x.dataset.tab === top));
  ['req','inv','craft','fuse','petfuse','sell','spin','map','skill','pet','market','rank','admin','vault'].forEach(id => { const p = $('pane-'+id); if (p) p.hidden = id !== name; });
  if (name === 'pet') renderPet(); else if (name === 'vault') { if (typeof renderVault === 'function') renderVault(); } else renderPane(name);        // tab đang ẩn đã bỏ qua lần vẽ trước → vẽ lại khi mở
  if (name === 'rank') loadRank();                              // bảng xếp hạng: tải khi mở tab
  if (name === 'market') loadMarket();                          // Chợ: tải khi mở tab
  document.querySelectorAll('[data-lc]').forEach(b => { b.classList.toggle('on', b.dataset.lc === name); b.setAttribute('aria-selected', b.dataset.lc === name); });
  if (MODE === 'panel' && typeof notifyHost === 'function') notifyHost('tab', {tab: name}); // cửa sổ đổi cỡ theo tab
}
window.openTab = openTab;
const NAV_SHORTCUTS = [
  {code:'KeyI', key:'I', tab:'inv', label:'Hành trang'},
  {code:'KeyM', key:'M', tab:'map', label:'Bản đồ'},
  {code:'KeyD', key:'D', tab:'req', label:'Đột phá'},
  {code:'KeyP', key:'P', tab:'market', label:'Chợ'},
  {code:'KeyK', key:'K', tab:'skill', label:'Công pháp'},
  {code:'KeyL', key:'L', tab:'craft', label:'Luyện chế'},
];
function handleNavShortcut(e){
  if (!S || e.defaultPrevented || e.repeat || e.isComposing || e.keyCode === 229 || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;
  if (e.target?.closest?.('input,textarea,select,[contenteditable]:not([contenteditable="false"]),[role="textbox"]') ||
      document.activeElement?.isContentEditable || document.querySelector('dialog[open],[aria-modal="true"]')) return;
  const shortcut = NAV_SHORTCUTS.find(s => s.code === e.code);
  if (!shortcut) return;
  e.preventDefault();
  if (MODE === 'strip') notifyHost('open-panel', {tab:shortcut.tab});
  else openTab(shortcut.tab);
}
addEventListener('keydown', handleNavShortcut);
function showBrowserSettings(){
  let dialog = $('gameSettings');
  if (!dialog){
    dialog = document.createElement('dialog'); dialog.id = 'gameSettings'; dialog.className = 'game-settings';
    dialog.setAttribute('aria-labelledby', 'gameSettingsTitle');
    dialog.innerHTML = `<h2 id="gameSettingsTitle">Cài đặt</h2><h3>Phím tắt</h3>
      <dl>${NAV_SHORTCUTS.map(s => `<div><dt>${s.label}</dt><dd><kbd>${s.key}</kbd></dd></div>`).join('')}</dl>
      <p>Phím tắt hoạt động khi cửa sổ game đang được chọn, không áp dụng khi nhập văn bản.</p>
      <div class="game-settings-actions"><button type="button" data-settings-code>Nhập code quà tặng…</button><button type="button" data-settings-close>Đóng</button></div>`;
    document.body.appendChild(dialog);
    dialog.addEventListener('click', e => {
      if (e.target !== dialog) return;
      const bounds = dialog.getBoundingClientRect();
      if (e.clientX < bounds.left || e.clientX > bounds.right || e.clientY < bounds.top || e.clientY > bounds.bottom) dialog.close();
    });
    dialog.querySelector('[data-settings-close]').onclick = () => dialog.close();
    dialog.querySelector('[data-settings-code]').onclick = () => { const code = prompt('Nhập code quà tặng'); if (code) redeemCode(code); };
  }
  if (!dialog.open) dialog.showModal();
}
// sync() là hàm toàn cục nên C# gọi được window.sync() trực tiếp
window.setOpaque = on => document.body.classList.toggle('opaque', !!on);

async function boot(){
  try { if(typeof onlineSession!=='undefined')await onlineSession.ensure(); }
  catch(error){if($('log'))$('log').textContent=error.message;return;}
  // bản game quá cũ (app_config trên Supabase) → chỉ hiện màn hình cập nhật, không vào game
  const ver = await checkVersion();
  if (ver?.blocked){ showUpdateGate(ver); return; }
  try {
    DATA = await api('GET', '/api/data');
    try { devMode = !!(await api('GET', '/api/env')).dev; } catch(e){}
    if (!devMode) adminOn = false;                               // bản phát hành: không có admin mode dù máy từng bật
    DATA.items.forEach(i => ITEM[i.id] = i);
    let id = null; try { id = localStorage.getItem(KEY); } catch(e){}
    let st = null;
    if (id){ try { const r = await api('GET', `/api/players/${id}`); st = r.state ?? r; } catch(e){ if (e.status !== 404) throw e; } }
    // không có hoặc mất mã tu sĩ (cửa sổ desktop đổi cổng mỗi lần mở) → hỏi server tu sĩ đang chơi
    if (!st){ const r = await api('GET', '/api/players/current'); st = r.state ?? r; }
    try { localStorage.setItem(KEY, st.id); } catch(e){}
    setState(st);
  } catch(e){
    $('log').innerHTML = `<h3>Nhật ký tu hành</h3><p class="bad">${e.message} Hãy chạy server bằng lệnh dotnet run.</p>`;
    return;
  }
  resize(); addEventListener('resize', resize);
  if ($('btnPet')) $('btnPet').onclick = () => MODE === 'strip' ? notifyHost('open-panel', {tab:'pet'}) : openTab('pet');
  if ($('btnVault')) $('btnVault').onclick = () => MODE === 'strip' ? notifyHost('open-panel', {tab:'vault'}) : openTab('vault');
  $('btnBag').onclick = () => MODE === 'strip' ? notifyHost('open-panel', {tab:'inv'}) : openTab('inv');
  document.body.appendChild($('tip')); // tooltip phải nằm ngoài vùng bị ẩn ở chế độ bảng
  document.body.appendChild($('burst'));
  document.body.appendChild($('craftToast'));
  initInventory();
  // đột phá đại cảnh giới diễn ra trong tab Đột phá (trận pháp, 5 giai đoạn, kết quả); lên tầng thì làm ngay
  // nút Đột phá ngoài màn hình chính: luôn mở tab Đột phá (đột phá thật bấm trong tab, có trận pháp + kết quả)
  $('btnAdvance').onclick = () => MODE === 'strip' ? notifyHost('open-panel', {tab:'req'}) : openTab('req');
  initDan(); initMap(); initBreak(); initSkill(); initMarket(); initPet();
  document.addEventListener('contextmenu', e => { if (MODE !== 'strip') e.preventDefault(); });
  document.querySelectorAll('.tab').forEach(t => t.onclick = () => openTab(t.dataset.tab));
  NAV_SHORTCUTS.forEach(s => {
    const tab = $('tab-' + s.tab); if (!tab) return;
    tab.setAttribute('aria-keyshortcuts', s.key); tab.title = `${s.label} (${s.key})`;
  });
  if (Q.get('tab')) openTab(Q.get('tab'));
  // dải taskbar: kéo thanh tiêu đề để di chuyển, mở bảng, mở menu cài đặt
  // kéo cả thanh tiêu đề (trừ nút cài đặt) để di chuyển cửa sổ
  document.querySelector('.strip-head').addEventListener('mousedown', e => {
    if (e.button !== 0 || e.target.closest('button')) return;
    e.preventDefault(); hideTip(); notifyHost('drag');
  });
  $('btnNext').onclick = () => {                                                      // sang ải kế, tối đa ải cao nhất đã mở
    const a = S.map.area, top = Math.min(S.map.areaMax, mapAreaCount(S.map));
    if (a >= top || a > mapAreaCount(S.map)) return;
    mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e){}
    act(`area/${a + 1}`);
  };
  $('btnBoss').onclick = () => {                                                      // vòng xoáy đỏ: vào thẳng ải Boss khi đã mở ải x-15
    const m = S.map, N = mapAreaCount(S.map);
    if (m.area > N || m.areaMax < N || (!(S.inventory[m.keyItem] > 0) && !adminBoss())) return;
    mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e){}
    act(`area/${N + 1}`);
  };
  $('btnPanel').onclick = () => MODE === 'strip' ? notifyHost('open-panel', {tab:'map'}) : openTab('map');
  $('btnRank').onclick = () => window.chrome?.webview ? notifyHost('open-panel', {tab:'rank'}) : window.open('?mode=panel&tab=rank', 'van-dao-rank', 'width=900,height=740');
  // cửa sổ desktop: menu cài đặt gốc (có mục Nhập code); chạy trên trình duyệt: hỏi code quà tặng
  $('btnMenu').onclick = () => window.chrome?.webview ? notifyHost('menu') : showBrowserSettings();
  addEventListener('keydown', e => { if (e.key === 'Escape' && MODE === 'panel' && !document.querySelector('#starterChoice[open]')) notifyHost('close-panel'); });
  setInterval(sync, 4000);
  if (MODE === 'strip'){
    addEventListener('resize', () => reportShape()); setInterval(reportShape, 1000);
    // cửa sổ C# chỉ hiện dải khi nhận hình lần đầu → đợi font (tối đa 1.5s) và khung canvas đầu tiên đã vẽ xong
    const fontsReady = Promise.race([document.fonts?.ready, new Promise(r => setTimeout(r, 1500))]);
    fontsReady.then(() => requestAnimationFrame(() => requestAnimationFrame(() => reportShape(true))));
  }
  document.addEventListener('visibilitychange', () => { if (!document.hidden) sync(); });
  requestAnimationFrame(frame);
  showUpdateBanner(ver);                                         // có bản mới (chưa bắt buộc): nhắc nhẹ
}
boot();
