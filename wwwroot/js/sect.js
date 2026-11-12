/* js/sect.js: Tông Môn (bảng trong sách: tổng quan / thành viên / đơn / cửa hàng) + kênh chat tông trong panel Nhật ký.
   Luật nằm ở server (SectData.cs, ServerSect.cs); file này chỉ hiển thị và gửi hành động. */

/* sect-pure:start */
const SECT_ROLES = {leader: 'Tông chủ', elder: 'Trưởng lão', disciple: 'Đệ tử'};
const sectRoleName = role => SECT_ROLES[role] || '';
const sectIsOfficer = role => role === 'leader' || role === 'elder';
/** Quyền trục xuất (khớp ServerSect.KickAsync): Tông chủ đuổi mọi người, Trưởng lão chỉ đuổi đệ tử. */
const sectCanKick = (me, target) => me === 'leader' ? target !== 'leader' : me === 'elder' && target === 'disciple';
/** Tiến độ quỹ lên cấp kế, 0–1; null khi đã tối đa. */
const sectFundProgress = (fund, next) => next == null ? null : next <= 0 ? 1 : Math.max(0, Math.min(1, fund / next));
/** Trạng thái một món ở cửa hàng tông (khớp ServerSect.BuyAsync). */
function sectShopState(item, level, contribution){
  if (level < item.minLevel) return 'locked';
  if (item.bought >= item.limit) return 'soldout';
  if (contribution < item.price) return 'poor';
  return 'ok';
}
/** "3 giờ 5 phút" / "12 phút" cho thời gian chờ vào tông. */
function sectWaitText(sec){
  const m = Math.ceil(Math.max(0, sec) / 60), h = Math.floor(m / 60);
  return h > 0 ? `${h} giờ${m % 60 ? ` ${m % 60} phút` : ''}` : `${m} phút`;
}
/** "vừa xong" · "5 phút trước" · "3 giờ trước" · "2 ngày trước". */
function sectSeen(iso, now = Date.now()){
  const min = Math.max(0, Math.floor((now - Date.parse(iso)) / 60000));
  return min < 5 ? 'vừa xong' : min < 60 ? `${min} phút trước` : min < 1440 ? `${Math.floor(min / 60)} giờ trước` : `${Math.floor(min / 1440)} ngày trước`;
}
/** Công trình trên ảnh sơn môn (x, y là % theo ảnh 3:2) và view mà nó mở. */
const SECT_SPOTS = [
  {id: 'hall', view: 'info', name: 'Chính Điện', x: 50, y: 20, tip: 'Chính Điện&#10;Cấp tông, nâng cấp, thông báo, nhận đệ tử, rời tông'},
  {id: 'tower', view: 'members', name: 'Đệ Tử Phòng', x: 27, y: 50, tip: 'Đệ Tử Phòng&#10;Thành viên, chức vị, duyệt đơn xin vào'},
  {id: 'vault', view: 'shop', name: 'Tàng Bảo Các', x: 13, y: 24, tip: 'Tàng Bảo Các&#10;Đổi cống hiến lấy đan dược và vé quay'},
  {id: 'alchemy', view: 'daily', name: 'Đan Phòng', x: 71, y: 46, tip: 'Đan Phòng&#10;Điểm danh và quyên linh thạch lấy cống hiến'},
  {id: 'boss', view: 'boss', name: 'Bệ Phong Ấn', x: 87, y: 17, tip: 'Bệ Phong Ấn&#10;Boss tông mỗi ngày: cả tông cùng đánh, hạ Boss nhận rương và buff tu vi'},
];
/** % máu Boss còn lại, 0–100. */
const sectBossHpPct = b => b.maxHp > 0 ? Math.max(0, Math.min(100, (1 - b.damage / b.maxHp) * 100)) : 0;
/** defeated · spent (hết lượt) · open. */
function sectBossStatus(b){ return b.defeated ? 'defeated' : b.hitsLeft <= 0 ? 'spent' : 'open'; }
/** Chấm đỏ trên công trình khi còn việc cần làm. */
function sectSpotAlert(id, d, boss){
  if (id === 'boss') return !!boss && (boss.canClaim || (!boss.defeated && boss.hitsLeft > 0));
  if (id === 'alchemy') return !d.me.checkedIn;
  if (id === 'tower') return d.applications.length > 0;
  return false;
}
/* sect-pure:end */

const SECT = {data: null, loading: false, at: 0, view: 'hub', notice: '', bad: false, err: '', boss: null, bossAt: 0, bossErr: '', bossLoading: false};
const SECT_BUFF_PCT = 3;   // khớp SectBossData.BuffQiPct
const sectEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
const sectRealmName = r => DATA?.realms?.[r]?.name || '';

async function loadSect(force){
  if (!S || SECT.loading) return;
  if (!force && SECT.data && performance.now() - SECT.at < 10000){ renderSect(); return; }
  SECT.loading = true; if (!SECT.data) renderSect();
  try { SECT.data = await api('GET', `/api/players/${S.id}/sect`); SECT.at = performance.now(); SECT.err = ''; }
  catch(e){ SECT.err = e.message || 'Không tải được Tông Môn.'; }
  finally { SECT.loading = false; if (!SECT.data?.sect) SECT.boss = null; renderSect(); sectChatRefresh(); if (SECT.data?.sect) loadSectBoss(); }
}
async function loadSectBoss(force){
  if (!S) return;
  if (SECT.bossLoading){ if (force) SECT.bossReload = true; return; }   // đang tải: lượt sau hành động phải tải lại sau khi xong
  if (!force && SECT.boss && performance.now() - SECT.bossAt < 10000) return;
  SECT.bossLoading = true;
  try { SECT.boss = await api('GET', `/api/players/${S.id}/sect/boss`); SECT.bossAt = performance.now(); SECT.bossErr = ''; }
  catch(e){ SECT.bossErr = e.message || 'Không tải được Boss tông.'; }
  finally {
    SECT.bossLoading = false; renderSect();
    if (SECT.bossReload){ SECT.bossReload = false; loadSectBoss(true); }
  }
}


/** Gửi hành động tông: server trả trạng thái nhân vật + trang tông mới. */
async function sectAct(path, body){
  if (busy || !S) return false;
  busy = true; SECT.notice = ''; renderSect();
  try {
    const res = await api('POST', `/api/players/${S.id}/sect/${path}`, body);
    const hadSect = SECT.data?.sect?.id;
    SECT.data = res.sect; SECT.at = performance.now();
    SECT.notice = res.message || ''; SECT.bad = false;
    setState(res.state); notifyHost('changed');
    if (!res.sect?.sect){ SECT.boss = null; SECT.view = 'hub'; }                 // rời / giải tán tông: bỏ Boss của tông cũ
    else if (res.sect.sect.id !== hadSect){ SECT.boss = null; SECT.view = 'hub'; loadSectBoss(true); }   // vào tông mới
    else if (path.startsWith('boss/')) loadSectBoss(true);
    return true;
  } catch(e){
    SECT.notice = e.message || 'Không thực hiện được.'; SECT.bad = true;
    return false;
  } finally { busy = false; renderSect(); sectChatRefresh(); }
}

function renderSect(){
  const pane = $('pane-sect'); if (!pane) return;
  const d = SECT.data;
  if (!d){ pane.innerHTML = `<div class="sect"><p class="sect-empty">${SECT.err ? sectEsc(SECT.err) : 'Đang tải Tông Môn…'}</p></div>`; return; }
  const note = SECT.notice ? `<p class="sect-note${SECT.bad ? ' bad' : ''}" role="status">${sectEsc(SECT.notice)}</p>` : '';
  pane.innerHTML = `<div class="sect">${d.sect ? sectHome(d) : sectLobby(d)}${note}</div>`;
}

/* ---------- Chưa có tông: tạo + danh sách ---------- */
function sectLobby(d){
  const me = d.me, low = me.myRealm < me.minRealm, wait = me.rejoinWaitSec > 0;
  const stones = S?.inventory?.linh_thach || 0;
  const lock = low ? `Cần đạt ${sectEsc(sectRealmName(me.minRealm))} để lập hoặc gia nhập Tông Môn.`
    : wait ? `Vừa rời tông, còn ${sectWaitText(me.rejoinWaitSec)} nữa mới gia nhập được tông khác.` : '';
  const create = `<form class="sect-card sect-create" data-sect-create>
      <h3>Khai sơn lập phái</h3>
      <p>Lập Tông Môn mới, tự làm Tông chủ. Phí <b>${fmt(me.createCost)}</b> linh thạch.</p>
      <div class="sect-row"><label class="chat-sr" for="sectName">Tên tông</label>
        <input id="sectName" maxlength="16" minlength="2" placeholder="Tên tông (2–16 ký tự)" ${low || wait ? 'disabled' : ''}>
        <button type="submit" ${low || wait || stones < me.createCost ? 'disabled' : ''}>Lập tông</button></div>
    </form>`;
  const rows = d.list.map(s => `<li class="sect-li">
      <div><b class="sect-name" translate="no">${sectEsc(s.name)}</b> <span class="sect-lv">Cấp ${s.level}</span>
        <small>${s.members} thành viên · Tông chủ <span translate="no">${sectEsc(s.leaderName)}</span> · từ ${sectEsc(sectRealmName(s.minRealm))}${s.autoAccept ? ' · tự duyệt' : ''}</small></div>
      ${s.applied ? `<button type="button" class="ghost" data-sect="cancel/${s.id}">Rút đơn</button>`
        : `<button type="button" data-sect="apply/${s.id}" ${low || wait || me.myRealm < s.minRealm ? 'disabled' : ''}>${s.autoAccept ? 'Gia nhập' : 'Xin vào'}</button>`}
    </li>`).join('');
  return `<header class="sect-head"><h2>Tông Môn</h2><p>Gia nhập tông để nhận buff tu luyện, điểm cống hiến và kênh chat riêng.</p></header>
    ${lock ? `<p class="sect-note bad">${lock}</p>` : ''}
    ${me.contribution > 0 ? `<p class="sect-sub">Cống hiến còn giữ: <b>${fmt(me.contribution)}</b></p>` : ''}
    ${create}
    <section class="sect-card"><h3>Các Tông Môn</h3>${rows ? `<ul class="sect-list">${rows}</ul>` : '<p class="sect-empty">Chưa có Tông Môn nào. Hãy là người khai sơn đầu tiên.</p>'}</section>`;
}

/* ---------- Đã có tông ---------- */
function sectHome(d){
  const s = d.sect, me = d.me;
  const prog = sectFundProgress(s.fund, s.nextCost), apps = d.applications.length;
  const views = [['hub', 'Sơn môn'], ['info', 'Chính Điện'], ['members', `Đệ Tử Phòng (${s.members}${apps ? ` · ${apps} đơn` : ''})`],
    ['shop', 'Tàng Bảo Các'], ['daily', 'Đan Phòng'], ['boss', 'Bệ Phong Ấn']];
  if (!views.some(([k]) => k === SECT.view)) SECT.view = 'hub';
  const body = {hub: sectHub, info: sectInfo, members: sectMembersView, shop: sectShop, daily: sectDaily, boss: () => sectBossView(SECT.boss)}[SECT.view](d);
  return `<header class="sect-head">
      <h2><span translate="no">${sectEsc(s.name)}</span> <span class="sect-lv">Cấp ${s.level}</span></h2>
      <p>Tu luyện <b class="up">+${s.qiPct}%</b> · ${sectEsc(sectRoleName(me.role))} · Cống hiến <b>${fmt(me.contribution)}</b></p>
      <div class="sect-fund"><span>Quỹ tông <b>${fmt(s.fund)}</b>${s.nextCost != null ? ` / ${fmt(s.nextCost)} để lên cấp ${s.level + 1}` : ' · cấp tối đa'}</span>
        ${prog != null ? `<div class="sect-bar"><i style="width:${(prog * 100).toFixed(1)}%"></i></div>` : ''}</div>
      ${s.notice ? `<p class="sect-notice" translate="no">${sectEsc(s.notice)}</p>` : ''}
    </header>
    <nav class="sect-tabs" role="tablist">${views.map(([k, n]) => `<button type="button" role="tab" aria-selected="${k === SECT.view}" data-sect-view="${k}">${n}</button>`).join('')}</nav>
    ${body}`;
}

/** Ảnh sơn môn: mỗi công trình là một nút mở view tương ứng. */
function sectHub(d){
  const spots = SECT_SPOTS.map(sp => `<button type="button" class="sect-spot sp-${sp.id}${sectSpotAlert(sp.id, d, SECT.boss) ? ' alert' : ''}"
      style="left:${sp.x}%;top:${sp.y}%" data-sect-view="${sp.view}" data-tip="${sp.tip}" aria-label="${sp.name}"><b>${sp.name}</b></button>`).join('');
  return `<div class="sect-scene" role="group" aria-label="Sơn môn">${spots}</div>`;
}

/** Đệ Tử Phòng: đơn xin vào (chỉ Tông chủ / Trưởng lão, khi có đơn) rồi danh sách thành viên. */
function sectMembersView(d){
  const apps = sectIsOfficer(d.me.role) && d.applications.length
    ? `<h3 class="sect-h">Đơn xin vào (${d.applications.length})</h3>${sectApps(d)}<h3 class="sect-h">Thành viên</h3>` : '';
  return apps + sectMembers(d);
}

function sectInfo(d){
  const s = d.sect, me = d.me, leader = me.role === 'leader', officer = sectIsOfficer(me.role);
  const levels = d.levels.map(l => `<tr class="${l.level === s.level ? 'on' : ''}"><td>Cấp ${l.level}</td><td>${l.cost ? fmt(l.cost) : '—'}</td><td>+${l.qiPct}%</td></tr>`).join('');
  const realms = (DATA?.realms || []).map((r, i) => i >= 1 ? `<option value="${i}" ${i === s.minRealm ? 'selected' : ''}>${sectEsc(r.name)}</option>` : '').join('');
  return `<section class="sect-card"><h3>Cấp tông</h3>
      <table class="sect-levels"><thead><tr><th>Cấp</th><th>Quỹ cần</th><th>Tu luyện</th></tr></thead><tbody>${levels}</tbody></table>
      ${leader && s.nextCost != null ? `<button type="button" data-sect="upgrade" ${s.fund < s.nextCost ? 'disabled' : ''}>Nâng lên cấp ${s.level + 1} (−${fmt(s.nextCost)} quỹ)</button>` : ''}
    </section>
    ${officer ? `<form class="sect-card" data-sect-notice><h3>Thông báo tông</h3>
      <div class="sect-row"><label class="chat-sr" for="sectNotice">Thông báo</label><input id="sectNotice" maxlength="120" value="${sectEsc(s.notice)}" placeholder="Lời nhắn cho đồng môn"><button type="submit">Lưu</button></div></form>` : ''}
    ${leader ? `<form class="sect-card" data-sect-settings><h3>Nhận đệ tử</h3>
      <div class="sect-row"><label><input type="checkbox" id="sectAuto" ${s.autoAccept ? 'checked' : ''}> Tự duyệt</label>
        <label>Từ <select id="sectMin">${realms}</select></label><button type="submit">Lưu</button></div></form>` : ''}
    <div class="sect-foot"><button type="button" class="danger" data-sect-leave>${leader && s.members <= 1 ? 'Giải tán tông' : 'Rời tông'}</button></div>`;
}

function sectDaily(d){
  const me = d.me, stones = S?.inventory?.linh_thach || 0;
  const donate = me.donateCost.map((c, i) => `<button type="button" data-sect="donate/${i}" ${!me.donatesLeft || stones < c ? 'disabled' : ''}>
      <b>${['Nhỏ', 'Vừa', 'Lớn'][i]}</b><small>${fmt(c)} linh thạch</small><small class="up">+${me.donatePoints[i]} cống hiến</small></button>`).join('');
  return `<section class="sect-card"><h3>Cống hiến hằng ngày</h3>
      <div class="sect-row"><button type="button" data-sect="checkin" ${me.checkedIn ? 'disabled' : ''}>${me.checkedIn ? 'Đã điểm danh' : 'Điểm danh +10'}</button>
        <span class="sect-sub">Quyên góp còn <b>${me.donatesLeft}</b> lần hôm nay</span></div>
      <div class="sect-donate">${donate}</div>
    </section>`;
}

function sectMembers(d){
  const me = d.me;
  const rows = d.members.map(m => {
    const acts = [];
    if (!m.me && me.role === 'leader'){
      if (m.role === 'disciple') acts.push(`<button type="button" class="ghost" data-sect="role/${m.playerId}?role=elder">Phong Trưởng lão</button>`);
      if (m.role === 'elder') acts.push(`<button type="button" class="ghost" data-sect="role/${m.playerId}?role=disciple">Bãi chức</button>`);
      acts.push(`<button type="button" class="ghost" data-sect-confirm="transfer/${m.playerId}" data-q="Nhường chức Tông chủ cho ${sectEsc(m.name)}?">Nhường chức</button>`);
    }
    if (!m.me && sectCanKick(me.role, m.role)) acts.push(`<button type="button" class="ghost danger" data-sect-confirm="kick/${m.playerId}" data-q="Trục xuất ${sectEsc(m.name)} khỏi tông?">Trục xuất</button>`);
    return `<li class="sect-li${m.me ? ' me' : ''}">
      <div><span class="sect-who"><span class="sect-role ${m.role}">${sectRoleName(m.role)}</span> <b class="chat-name ${chatRealmClass(m.realm)}" translate="no">${sectEsc(m.name)}</b></span>
        <small><span class="chat-realm ${chatRealmClass(m.realm)}">${sectEsc(m.realmName)}</span> Cống hiến ${fmt(m.total)} · ${sectSeen(m.lastSeen)}</small></div>
      ${acts.length ? `<div class="sect-acts">${acts.join('')}</div>` : ''}</li>`;
  }).join('');
  return `<section class="sect-card"><ul class="sect-list">${rows}</ul><p class="sect-sub">Trưởng lão tối đa ${d.maxElders} người.</p></section>`;
}

function sectApps(d){
  const rows = d.applications.map(a => `<li class="sect-li"><div><b class="chat-name ${chatRealmClass(a.realm)}" translate="no">${sectEsc(a.name)}</b>
      <small><span class="chat-realm ${chatRealmClass(a.realm)}">${sectEsc(a.realmName)}</span> ${sectSeen(a.at)}</small></div>
      <div class="sect-acts"><button type="button" data-sect="decide/${a.id}?ok=true">Nhận</button><button type="button" class="ghost" data-sect="decide/${a.id}?ok=false">Từ chối</button></div></li>`).join('');
  return `<section class="sect-card">${rows ? `<ul class="sect-list">${rows}</ul>` : '<p class="sect-empty">Chưa có đơn nào.</p>'}</section>`;
}

function sectShop(d){
  const s = d.sect, me = d.me;
  const cards = d.shop.map(it => {
    const st = sectShopState(it, s.level, me.contribution), name = ITEM[it.itemId]?.name || it.itemId;
    const why = st === 'locked' ? `Tông cấp ${it.minLevel}` : st === 'soldout' ? 'Hết lượt tuần này' : '';
    return `<div class="sect-item ${st}">
      <span class="sect-ic" data-card="bag" data-k="${it.itemId}">${typeof icon === 'function' ? icon(it.itemId) : ''}</span>
      <b>${sectEsc(name)}</b><small>${it.bought}/${it.limit} tuần này</small>
      <button type="button" data-sect="buy/${it.itemId}?n=1" ${st !== 'ok' ? 'disabled' : ''}>${why || `${fmt(it.price)} cống hiến`}</button></div>`;
  }).join('');
  return `<section class="sect-card"><p class="sect-sub">Cống hiến hiện có <b>${fmt(me.contribution)}</b> · lượt mua làm mới 0h thứ Hai.</p><div class="sect-shop">${cards}</div></section>`;
}

function sectBossView(b){
  if (!b) return `<section class="sect-card"><p class="sect-empty">${SECT.bossErr ? sectEsc(SECT.bossErr) : 'Đang tải Boss tông…'}</p></section>`;
  const st = sectBossStatus(b), pct = sectBossHpPct(b);
  const stText = st === 'defeated' ? 'Đã hạ' : st === 'spent' ? 'Hết lượt' : 'Đang chờ đồng môn';
  const slots = Array.from({length: b.hitsPerDay}, (_, i) => `<i class="sect-slot${i < b.myHits ? ' used' : ''}"></i>`).join('');
  const attack = b.defeated ? '' : `<button type="button" data-sect="boss/attack" ${b.hitsLeft > 0 ? '' : 'disabled'}>Tấn công · còn ${b.hitsLeft}/${b.hitsPerDay} lượt</button>`;
  const chest = !b.defeated ? '' : `<button type="button" data-sect="boss/chest" ${b.canClaim ? '' : 'disabled'}>${b.claimed ? 'Đã nhận rương' : b.myHits ? 'Nhận rương' : 'Chưa đánh lượt nào'}</button>`;
  const buff = b.buffToday ? `<p class="sect-sub up">Hôm nay cả tông được +${SECT_BUFF_PCT}% tu vi nhờ hạ Boss hôm qua.</p>`
    : b.buffTomorrow ? `<p class="sect-sub up">Ngày mai cả tông được +${SECT_BUFF_PCT}% tu vi.</p>` : '';
  const rank = b.ranking.map((r, i) => `<li class="sect-li${r.me ? ' me' : ''}${i < b.topBonus.length ? ' top' : ''}">
      <div><b>${i + 1}. <span translate="no">${sectEsc(r.name)}</span></b><small>${r.hits} lượt${i < b.topBonus.length ? ` · thưởng top +${b.topBonus[i]} cống hiến` : ''}</small></div>
      <b>${fmt(Math.round(r.damage))}</b></li>`).join('');
  return `<section class="sect-card sect-boss ${st}">
      <img class="sect-boss-img" src="${sectEsc(b.image)}" alt="">
      <div class="sect-boss-info"><h3 translate="no">${sectEsc(b.name)}</h3><p class="sect-sub">${sectEsc(b.desc)}</p>
        <div class="sect-bar hp"><i style="width:${pct.toFixed(1)}%"></i></div>
        <p class="sect-sub"><b>${fmt(Math.max(0, Math.round(b.maxHp - b.damage)))}</b> / ${fmt(Math.round(b.maxHp))} máu · ${stText}</p>
        <div class="sect-slots" aria-label="Lượt đánh hôm nay">${slots}<span class="sect-sub">Bạn đã gây <b>${fmt(Math.round(b.myDamage))}</b> sát thương</span></div>
        <div class="sect-row">${attack}${chest}</div>${buff}</div>
    </section>
    <section class="sect-card"><h3>Sát thương hôm nay</h3>${rank ? `<ul class="sect-list">${rank}</ul>` : '<p class="sect-empty">Chưa ai ra tay hôm nay.</p>'}
      <p class="sect-sub">Hạ Boss trước 0h: mọi người đã đánh nhận rương (nhận trong ngày), top 3 thưởng thêm cống hiến, cả tông +${SECT_BUFF_PCT}% tu vi ngày mai.</p></section>`;
}

function initSect(){
  const pane = $('pane-sect'); if (!pane) return;
  pane.addEventListener('click', e => {
    const v = e.target.closest('[data-sect-view]');
    if (v){ SECT.view = v.dataset.sectView; SECT.notice = ''; renderSect(); if (SECT.view === 'boss') loadSectBoss(); return; }
    const b = e.target.closest('[data-sect]');
    if (b && !b.disabled){ sectAct(b.dataset.sect); return; }
    const c = e.target.closest('[data-sect-confirm]');
    if (c && confirm(window.tr(c.dataset.q))){ sectAct(c.dataset.sectConfirm); return; }
    if (e.target.closest('[data-sect-leave]')){
      const solo = SECT.data?.sect?.members <= 1 && SECT.data?.me?.role === 'leader';
      if (confirm(window.tr(solo ? 'Giải tán Tông Môn? Không thể hoàn tác.' : 'Rời tông? 24 giờ sau mới gia nhập được tông khác.'))) sectAct('leave');
    }
  });
  pane.addEventListener('submit', e => {
    e.preventDefault();
    const f = e.target;
    if (f.matches('[data-sect-create]')) sectAct('create', {text: $('sectName').value});
    else if (f.matches('[data-sect-notice]')) sectAct('notice', {text: $('sectNotice').value});
    else if (f.matches('[data-sect-settings]')) sectAct(`settings?auto=${$('sectAuto').checked}&minRealm=${$('sectMin').value}`);
  });
}

/* ---------- Kênh chat Tông Môn (tab trong panel Nhật ký) ---------- */
const SCHAT = {on: false, msgs: [], unread: 0, fails: 0, timer: 0, wait: 0, waitTimer: 0, sending: false, page: null, err: '', seq: 0, applied: 0, rendered: false};
window.sectChatOn = () => SCHAT.on;
const sectChatWatching = () => typeof CHAT !== 'undefined' && CHAT.tab === 'sect' && chatOpen();
const sectChatUrl = () => `/api/players/${S.id}/sect/chat?after=${Math.max(0, (SCHAT.msgs.at(-1)?.id || 0) - CHAT_OVERLAP)}`;

function sectChatRenderMsgs(added){
  const box = $('sectChatMsgs'); if (!box) return;
  const atEnd = box.scrollHeight - box.scrollTop - box.clientHeight < 24;
  if (added === null) box.replaceChildren(...SCHAT.msgs.map(chatLine));
  else { added.forEach(m => box.append(chatLine(m))); while (box.childElementCount > CHAT_KEEP) box.firstElementChild.remove(); }
  if (atEnd || !SCHAT.rendered) box.scrollTop = box.scrollHeight;
  SCHAT.rendered = true;
}
function sectChatRenderBadge(){
  const t = chatBadge(SCHAT.unread), b = $('sectChatBadge');
  if (b){ b.textContent = t; b.hidden = !t; }
}
function sectChatRenderForm(){
  const pg = SCHAT.page, input = $('sectChatInput'), send = $('sectChatSend'), bar = $('sectChatBar');
  if (!pg || !input || !send || !bar) return;
  const locked = !!pg.lockReason;
  const msg = locked ? window.tr(pg.lockReason) : SCHAT.err || (SCHAT.fails ? window.tr('Đang kết nối lại… tin mới sẽ hiện khi có mạng.') : '');
  $('sectChatForm').hidden = locked;
  bar.hidden = !msg; bar.className = 'chat-bar' + (!locked && (SCHAT.err || SCHAT.fails) ? ' bad' : ''); bar.textContent = msg;
  send.disabled = SCHAT.sending || SCHAT.wait > 0 || !input.value.trim();
  send.textContent = SCHAT.wait > 0 ? SCHAT.wait + 's' : window.tr('Gửi');
}
function sectChatStartWait(sec){
  SCHAT.wait = sec; clearInterval(SCHAT.waitTimer);
  if (sec > 0) SCHAT.waitTimer = setInterval(() => { SCHAT.wait = Math.max(0, SCHAT.wait - 1); sectChatRenderForm(); if (!SCHAT.wait) clearInterval(SCHAT.waitTimer); }, 1000);
  sectChatRenderForm();
}
function sectChatApply(pg, seq = ++SCHAT.seq){
  const latest = seq >= SCHAT.applied; if (latest) SCHAT.applied = seq;
  const tab = $('logTabSect'); if (!tab) return;
  if (!pg.enabled){
    if (!latest) return;
    const was = SCHAT.on;
    SCHAT.on = false; SCHAT.msgs = []; SCHAT.rendered = false; SCHAT.unread = 0; tab.hidden = true; sectChatRenderBadge();
    if (!(typeof CHAT !== 'undefined' && CHAT.on)) $('log').classList.remove('chat-on');
    if (was && typeof CHAT !== 'undefined' && CHAT.tab === 'sect') chatSetTab('log');
    return;
  }
  const first = !SCHAT.on; SCHAT.on = true; if (latest) SCHAT.page = pg;
  tab.hidden = false; $('log').classList.add('chat-on');
  const prevMax = SCHAT.msgs.at(-1)?.id || 0;
  const fresh = chatFresh(SCHAT.msgs, pg.messages);
  SCHAT.msgs = chatMerge(SCHAT.msgs, pg.messages);
  if (!first && !sectChatWatching()) SCHAT.unread += chatUnread(fresh);
  if (first || fresh.some(m => m.id < prevMax)) sectChatRenderMsgs(null);
  else if (fresh.length) sectChatRenderMsgs(fresh);
  if (latest && !SCHAT.wait && pg.cooldownLeft) sectChatStartWait(pg.cooldownLeft);
  sectChatRenderBadge(); sectChatRenderForm();
}
async function sectChatPoll(){
  clearTimeout(SCHAT.timer);
  if (!S?.id){ sectChatSchedule(); return; }
  const seq = ++SCHAT.seq;
  try { sectChatApply(await api('GET', sectChatUrl()), seq); SCHAT.fails = 0; }
  catch(e){ SCHAT.fails++; sectChatRenderForm(); }
  sectChatSchedule();
}
function sectChatSchedule(){
  clearTimeout(SCHAT.timer);
  // không có tông: hỏi lại thưa (60 s) — vào tông qua bảng Tông Môn sẽ gọi sectChatRefresh ngay
  const d = chatPollDelay({hidden: document.hidden, watching: sectChatWatching(), fails: SCHAT.fails});
  if (d !== null) SCHAT.timer = setTimeout(sectChatPoll, SCHAT.on ? d : 60000);
}
/** Sau hành động tông (vào / rời / lập): đồng bộ kênh chat ngay. */
function sectChatRefresh(){
  const inSect = !!SECT.data?.sect;
  if (inSect !== SCHAT.on || inSect) sectChatPoll();
}
window.sectChatShown = () => { SCHAT.unread = 0; sectChatRenderBadge(); const b = $('sectChatMsgs'); if (b) b.scrollTop = b.scrollHeight; sectChatPoll(); };

async function sectChatSend(e){
  e.preventDefault();
  const input = $('sectChatInput'), text = input.value.trim();
  if (!text || SCHAT.sending || SCHAT.wait) return;
  SCHAT.sending = true; SCHAT.err = ''; sectChatRenderForm();
  try {
    const seq = ++SCHAT.seq;
    const pg = await api('POST', sectChatUrl(), {text});
    input.value = ''; sectChatApply(pg, seq); sectChatStartWait(pg.cooldownLeft || 3);
  } catch(err){ SCHAT.err = err.message || window.tr('Chưa gửi được.'); }
  finally { SCHAT.sending = false; sectChatRenderForm(); }
}

function initSectChat(){
  if (!$('sectChatPane')) return;
  $('logTabSect').onclick = () => chatSetTab('sect');
  $('sectChatForm').onsubmit = sectChatSend;
  $('sectChatInput').oninput = () => { SCHAT.err = ''; sectChatRenderForm(); };
  document.addEventListener('visibilitychange', () => document.hidden ? clearTimeout(SCHAT.timer) : sectChatPoll());
  sectChatSchedule();
  setTimeout(sectChatPoll, 1500);                                   // chờ main.js nạp nhân vật
}

initSect();
initSectChat();
