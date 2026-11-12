/* js/leaderboard.js: Bảng xếp hạng online (Supabase qua server game) · 2 tab Cảnh giới / Lực chiến. */
let lbBy = 'realm', lbData = null, lbLoading = false, lbAt = 0;
try { const saved = localStorage.getItem('van-dao-lb-by'); lbBy = ['realm','power','progress','pet','alchemy','arena'].includes(saved) ? saved : 'realm'; } catch(e){}
const lbEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));

function initRank(){
  const pane = $('pane-rank'); if (!pane || pane.dataset.ready) return;
  pane.dataset.ready = 1;
  pane.innerHTML = `<div class="lb">
    ${lbSymbols}
    <header class="lb-heading">${lbIcon('board')}<div><h2>Thiên bảng</h2><p>Luận thực lực · Định tiên vị</p></div><span class="lb-seal">Vạn đạo tranh phong</span></header>
    <div class="lb-top">
      <div class="lb-tabs" role="tablist">
        <button data-lb="power" role="tab">${lbIcon('power')}Lực chiến</button>
        <button data-lb="realm" role="tab">${lbIcon('realm')}Cảnh giới</button>
        <button data-lb="progress" role="tab">${lbIcon('board')}Vượt ải</button>
        <button data-lb="pet" role="tab">${lbIcon('realm')}Linh thú</button>
        <button data-lb="alchemy" role="tab">${lbIcon('board')}Luyện đan</button>
        <button data-lb="arena" role="tab">${lbIcon('power')}Lôi đài</button>
      </div>
      <button class="lb-ref" id="lbRef" aria-label="Làm mới bảng xếp hạng" data-tip="Làm mới&#10;Bảng tự cập nhật mỗi 30 giây">↻</button>
    </div>
    <div class="lb-note" id="lbNote" hidden></div>
    <div class="lb-body"><aside id="lbChampion" class="lb-champion" hidden></aside><section class="lb-results"><div class="lb-columns"><span>Hạng</span><span>Đạo hữu</span><span id="lbColumn">Cảnh giới</span></div><ol class="lb-list" id="lbList"></ol></section></div>
    <div class="lb-me" id="lbMe"></div>
    <div class="ar" id="arenaView" hidden></div>
    <footer class="lb-footer">Tự cập nhật mỗi 30 giây</footer>
  </div>`;
  pane.addEventListener('click', async e => {
    const clear = e.target.closest('[data-lb-clear]');
    if (clear && devMode) {
      e.stopPropagation();
      clear.disabled = true;
      try {
        const result = await api('POST', `/api/players/${S.id}/admin/clearflag?${new URLSearchParams({arg: clear.dataset.lbClear})}`);
        if (result.state) setState(result.state);
        await loadRank(true);
      } catch (error) { clear.disabled = false; localLog(error.message, 'bad'); }
      return;
    }
    const t = e.target.closest('[data-lb]');
    if (t && t.dataset.lb !== lbBy){ lbBy = t.dataset.lb; try { localStorage.setItem('van-dao-lb-by', lbBy); } catch(e){} loadRank(); }
    if (e.target.closest('#lbRef')) loadRank(true);
    const row = e.target.closest('[data-pid]'); if (row) lbShowProfile(row.dataset.pid);
  });
  pane.addEventListener('keydown', e => {
    if (e.target.closest('[data-lb-clear]')) return;
    const row = e.target.closest('[data-pid]');
    if (row && (e.key === 'Enter' || e.key === ' ')){ e.preventDefault(); lbShowProfile(row.dataset.pid); }
  });
  setInterval(() => { if (!$('pane-rank').hidden && !document.hidden && !arenaFighting()) loadRank(true); }, 30000);
}

async function loadRank(force){
  initRank();
  if (lbBy === 'arena'){ renderRank(); return loadArena(force); }
  if (!S || lbLoading) return;
  if (!force && lbData?.by === lbBy && performance.now() - lbAt < 20000){ renderRank(); return; }
  lbLoading = true; renderRank();
  try { lbData = await api('GET', `/api/players/${S.id}/leaderboard?by=${lbBy}`); lbAt = performance.now(); }
  catch(e){ lbData = {enabled: true, by: lbBy, rows: [], error: e.message}; }
  finally { lbLoading = false; renderRank(); }
}

/** Chú thích dấu * đỏ: nhân vật từng mở bằng bản game cũ (SaveGuard) — vẫn xếp hạng nhưng không vào top 10, không giao dịch ở Chợ. */
const LB_FLAG_TIP = 'Dữ liệu từng được mở bằng bản game cũ&#10;Vẫn được xếp hạng nhưng không vào top 10 và không giao dịch ở Chợ';
const lbLabels = {realm: 'Cảnh giới', power: 'Lực chiến', progress: 'Vượt ải', pet: 'Linh thú', alchemy: 'Luyện đan', arena: 'Lôi đài'};
/** Danh hiệu Đệ nhất (Game/Arena.cs → Titles) cạnh tên người chơi. */
function lbTitles(list){
  return (list || []).map(t => `<span class="lb-title tt-${lbEsc(t.id)}" data-tip="${lbEsc(t.desc)}">${lbEsc(t.name)}</span>`).join('');
}
function lbProgressText(value){
  const n = Number(value ?? 0);
  if (n === 0) return 'Chưa vượt ải';
  const names = ['Thường', 'Khó', 'Ác mộng', 'Địa Ngục 1', 'Địa Ngục 2'];
  const difficulty = Math.floor(n / 10000), chapter = Math.floor(n % 10000 / 100) + 1, area = n % 100;
  const chapterCount = DATA.chapters?.filter(c => c.enabled).length || DATA.chapters?.length || 10;
  const maxArea = difficulty === 3 ? 30 : (DATA.areas || 15);
  if (!Number.isSafeInteger(n) || n < 0 || !names[difficulty] || chapter > chapterCount || area < 1 || area > maxArea)
    return `${names[difficulty] ? names[difficulty] + ' · ' : ''}Tiến độ cần cập nhật`;
  return `${names[difficulty]} · ${chapter}-${area}`;
}

/** Bảng Luyện đan: "Cửu Phẩm" (màu theo phẩm như khung đan) + danh xưng + EXP. */
const LB_ALCHEMY_RC = ['common','uncommon','rare','epic','legendary','mythic','divine','immortal','dao','supreme','kimtien'];
function lbAlchemy(r){
  const [grade, title] = String(r.alchemyName || 'Nhất Phẩm - Luyện Đan Sư Sơ Cấp').split(' - ');
  const rank = ['Nhất','Nhị','Tam','Tứ','Ngũ','Lục','Thất','Bát','Cửu','Thập','Thập Nhất'].indexOf(grade.replace(/ Phẩm$/, ''));
  return `<span class="lb-alc r-${LB_ALCHEMY_RC[Math.max(0, rank)]}"><b>${lbEsc(grade)}</b><small>${lbEsc(title || '')}</small><small>${fmt(r.alchemyExp || 0)} EXP</small></span>`;
}
function lbScore(r, by){
  return by === 'alchemy' ? lbAlchemy(r) : by === 'progress' ? lbEsc(lbProgressText(r.progress)) : by === 'pet' ? (r.pet
    ? `<span class="lb-pet"><img src="${lbEsc(r.pet.image)}" alt="${lbEsc(r.pet.name)}"><span><b>${lbEsc(r.pet.name)}</b><small>Cấp ${fmt(r.pet.level)} · ${fmt(r.pet.stars)} ★</small><small>Lực chiến ${fmt(r.petPower || 0)}</small></span></span>`
    : `<span class="lb-pet-pending">${fmt(r.petPower || 0)}<small>${r.petPower ? 'Lực chiến' : 'Chưa có linh thú'}</small></span>`)
    : by === 'power' ? fmt(r.power) : `${lbEsc(r.realmName)}<small>${lbEsc(r.stageName)}</small>`;
}
/** Đầu thẻ Đệ nhất: huy hiệu danh hiệu của bảng đang xem ("Chiến Lực Vô Song"…), chưa có danh hiệu thì chữ "Đệ nhất". */
const lbBoardTitle = r => (r.titles || []).find(t => t.id === lbBy);
const lbFirstHead = r => `<div class="lb-first">${lbBoardTitle(r) ? lbTitles([lbBoardTitle(r)]) : 'Đệ nhất'}</div>`;
/** Thẻ Đệ nhất của bảng Linh thú: ảnh lớn của linh thú, sao, cấp, lực chiến, rồi tới chủ nhân. */
function lbPetChampion(r){
  const p = r.pet, hd = String(p.image || '').replace(/(img\/pet\/[^/]+_[0-2])\.png$/, '$1_hd.png');
  const stars = Math.max(0, Math.min(5, Number(p.stars) || 0));
  return `${lbFirstHead(r)}<div class="lb-pet-hero"><img src="${lbEsc(hd)}" alt="${lbEsc(p.name)}" onerror="this.onerror=null;this.src='${lbEsc(p.image)}'"></div>`
    + `<h3>${lbEsc(p.name)}</h3><div class="lb-stars" aria-label="${stars} sao">${'★'.repeat(stars)}<i>${'★'.repeat(5 - stars)}</i></div><p>Cấp ${fmt(p.level)}</p>`
    + `<strong>${fmt(r.petPower || 0)}</strong><p>Lực chiến linh thú</p>`
    + `<div class="lb-owner">Chủ nhân<b>${lbEsc(r.name)}</b><small>${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}</small></div><div class="lb-motto">Độc bộ thiên hạ</div>`;
}
function lbRow(r, by){
  const top = r.rank >= 1 && r.rank <= 3 ? ` t${r.rank}` : '';
  const realmTxt = `${lbEsc(r.realmName)} · ${lbEsc(r.stageName)}`;
  const main = lbScore(r, by);
  const sub = by === 'pet' || by === 'power' || by === 'alchemy' ? realmTxt : `Lực chiến ${fmt(r.power)}`;
  return `<li class="lb-row${top}${r.me ? ' me' : ''}"${r.id ? ` data-pid="${lbEsc(r.id)}" role="button" tabindex="0" aria-label="Xem trang bị của ${lbEsc(r.name)}" data-tip="Xem trang bị&#10;${lbEsc(r.name)}"` : ''}><span class="lb-rk">${r.rank || '—'}</span>
    <span class="lb-nm"><b>${lbEsc(r.name)}</b>${r.flagged ? `<sup class="lb-flag" aria-label="Đã đánh dấu" data-tip="${LB_FLAG_TIP}">*</sup>` : ''}${r.me ? '<em class="lb-you">Bạn</em>' : ''}${lbTitles(r.titles)}<small>${sub}</small></span><span class="lb-val">${main}${devMode && r.flagged && r.id ? `<button type="button" data-lb-clear="${lbEsc(r.id)}" aria-label="Gỡ cờ dữ liệu cho ${lbEsc(r.name)}">Gỡ cờ</button>` : ''}</span></li>`;
}

function renderRank(){
  initRank();
  const pane = $('pane-rank'); if (!pane || !S) return;
  pane.querySelectorAll('[data-lb]').forEach(b => { const on = b.dataset.lb === lbBy; b.classList.toggle('on', on); b.setAttribute('aria-selected', on); });
  const arena = lbBy === 'arena';
  $('arenaView').hidden = !arena;
  pane.querySelector('.lb-body').hidden = arena; $('lbMe').hidden = arena;
  if (arena){ pane.dataset.by = 'arena'; $('lbNote').hidden = true; $('lbFlagNote')?.setAttribute('hidden', ''); $('lbRef').disabled = arLoading; renderArena(); return; }
  $('lbRef').disabled = lbLoading;
  const d = lbData?.by === lbBy ? lbData : null, note = $('lbNote');
  pane.dataset.by = lbBy;
  $('lbColumn').textContent = lbLabels[lbBy] || 'Cảnh giới';
  const champion = d?.enabled && !d.error ? d.rows.find(r => r.rank === 1) : null;
  const hero = champion ? champion.hero || DATA.realms[champion.realm]?.hero : null;   // trang phục đang mặc, chưa có thì ảnh cảnh giới
  $('lbChampion').hidden = !champion;
  $('lbChampion').innerHTML = champion && lbBy === 'pet' && champion.pet ? lbPetChampion(champion) : champion ? `${lbFirstHead(champion)}${hero ? lbFigure(hero, champion.realm, champion.name) : lbIcon(lbBy === 'power' ? 'power' : 'realm')}<h3>${lbEsc(champion.name)}</h3>${lbTitles(champion.titles?.filter(t => t !== lbBoardTitle(champion))) ? `<div class="lb-titles">${lbTitles(champion.titles.filter(t => t !== lbBoardTitle(champion)))}</div>` : ''}<p>${lbEsc(champion.realmName)} · ${lbEsc(champion.stageName)}</p><strong>${lbScore(champion, lbBy)}</strong><p>${lbLabels[lbBy]}</p><div class="lb-motto">Độc bộ thiên hạ</div>` : '';
  let msg = '';
  if (d && !d.enabled) msg = 'Bảng xếp hạng online chưa được cấu hình (appsettings.json → Leaderboard: Url, AnonKey).';
  else if (d?.error) msg = d.error;
  else if (d?.tainted) msg = 'Nhân vật này đã dùng admin mode nên không được xếp hạng. Tu lại từ đầu (không bật cờ admin) để tham gia.';
  else if (d?.flagged) msg = 'Dữ liệu nhân vật của bạn từng được mở bằng bản game cũ: vẫn được xếp hạng nhưng mang dấu * đỏ, không vào top 10 và không giao dịch ở Chợ.';
  note.hidden = !msg; note.textContent = msg;
  $('lbMe').innerHTML = !d || !d.enabled || d.tainted || d.error ? ''
    : d.me ? `<div>Hạng của bạn <b>#${d.me.rank || '—'}</b></div><ol class="lb-list">${lbRow({...d.me, me:true}, lbBy)}</ol>`
    : 'Chưa có hạng: tiến độ sẽ được gửi lên trong giây lát.';
  $('lbList').innerHTML = !d ? `<li class="lb-empty">${lbLoading ? 'Đang tải…' : ''}</li>`
    : d.rows.length ? d.rows.map(r => lbRow(r, lbBy)).join('')
    : d.enabled && !d.error ? '<li class="lb-empty">Chưa có tu sĩ nào trên bảng.</li>' : '';
  // chú thích cuối bảng khi có người mang dấu *
  let fn = $('lbFlagNote');
  if (!fn){ fn = document.createElement('p'); fn.id = 'lbFlagNote'; fn.className = 'lb-flag-note'; $('lbList').after(fn); }
  const anyFlag = !!d && (d.rows.some(r => r.flagged) || d.me?.flagged);
  fn.hidden = !anyFlag;
  fn.innerHTML = anyFlag ? '<span class="lb-flag">*</span> Dữ liệu từng được mở bằng bản game cũ: vẫn xếp hạng nhưng không vào top 10 và không giao dịch ở Chợ.' : '';
}

const lbSymbols = "<svg width=\"0\" height=\"0\" style=\"position:absolute\" aria-hidden=\"true\"><defs>\n<symbol id=\"lb-board\" viewBox=\"0 0 64 64\"><path d=\"M15 10h34v44H15z\" fill=\"#272b27\" stroke=\"currentColor\" stroke-width=\"2\"/><path d=\"M11 15h42M11 49h42M22 5v10m20-10v10\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3\"/><path d=\"M23 40V30h6v10m5 0V23h6v17M20 44h24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/><path d=\"m32 8 4 5-4 5-4-5z\" fill=\"currentColor\"/></symbol>\n<symbol id=\"lb-power\" viewBox=\"0 0 64 64\"><path d=\"m32 3 25 14v30L32 61 7 47V17z\" fill=\"#322324\" stroke=\"currentColor\"/><g fill=\"#ead2b2\" stroke=\"#b97a61\" stroke-width=\"1.5\"><path d=\"m17 10 8 5 20 30-6 4-20-30z\"/><path d=\"m47 10-8 5-20 30 6 4 20-30z\"/></g><path d=\"m14 38 15 10m6 0 15-10M20 45l-6 9m30-9 6 9\" stroke=\"currentColor\" stroke-width=\"4\"/><path d=\"m32 24 4 8-4 8-4-8z\" fill=\"#efb375\"/></symbol>\n<symbol id=\"lb-realm\" viewBox=\"0 0 64 64\"><circle cx=\"32\" cy=\"32\" r=\"27\" fill=\"#182e2b\" stroke=\"currentColor\"/><circle cx=\"32\" cy=\"32\" r=\"22\" fill=\"none\" stroke=\"currentColor\" opacity=\".3\"/><g fill=\"#294b42\" stroke=\"currentColor\" stroke-width=\"1.6\"><path d=\"M32 46C18 34 25 19 32 12c7 7 14 22 0 34z\"/><path d=\"M32 46C16 48 9 34 10 26c11 0 22 6 22 20zM32 46c16 2 23-12 22-20-11 0-22 6-22 20z\"/><path d=\"M32 47C19 51 14 45 12 39c9-2 16 0 20 8zm0 0c13 4 18-2 20-8-9-2-16 0-20 8z\"/></g><path d=\"m32 22 3 8-3 8-3-8z\" fill=\"#c9ead6\"/></symbol></defs></svg>";
function lbIcon(name){return `<svg class="lb-icon" viewBox="0 0 64 64" aria-hidden="true"><use href="#lb-${name}"/></svg>`}

/* ---- Hồ sơ: bấm một dòng → trang bị đang mặc + chỉ số (lúc gửi điểm gần nhất) ---- */
let lbPfReq = 0;
function lbCloseProfile(){ document.getElementById('lbPf')?.remove(); lbPfReq++; }
async function lbShowProfile(pid){
  lbCloseProfile();
  const req = ++lbPfReq;
  const el = document.createElement('div');
  el.id = 'lbPf'; el.className = 'lb-pf'; el.setAttribute('role', 'dialog'); el.setAttribute('aria-modal', 'true');
  el.innerHTML = '<div class="lb-pf-card"><p class="lb-pf-wait">Đang tải trang bị…</p></div>';
  el.addEventListener('click', e => { if (e.target === el || e.target.closest('[data-pf-x]')) lbCloseProfile(); });
  document.body.appendChild(el);
  let pf;
  try { pf = await api('GET', `/api/players/${S.id}/leaderboard/${pid}`); }
  catch(err){ if (req === lbPfReq) el.querySelector('.lb-pf-card').innerHTML = `<p class="lb-pf-wait">${lbEsc(err.message)}</p><button class="lb-pf-x" data-pf-x>Đóng</button>`; return; }
  if (req !== lbPfReq) return;
  const card = el.querySelector('.lb-pf-card');
  card.innerHTML = lbProfileHtml(pf);
  card.addEventListener('click', e => {
    const t = e.target.closest('[data-pf-tab]'); if (!t) return;
    lbPfTab = t.dataset.pfTab;
    card.querySelectorAll('[data-pf-tab]').forEach(b => { const on = b.dataset.pfTab === lbPfTab; b.classList.toggle('on', on); b.setAttribute('aria-selected', on); });
    card.querySelectorAll('[data-pf-view]').forEach(v => v.hidden = v.dataset.pfView !== lbPfTab);
  });
  card.querySelector('[data-pf-x]')?.focus();
}

/** Tab đang mở của hồ sơ ('gear' Trang bị · 'pet' Linh thú), giữ khi mở hồ sơ người khác. */
let lbPfTab = 'gear';
/** Ô trang bị linh thú (Game/PetTraining.cs → Slots); người chơi đã mở menu Linh thú thì lấy tên từ server. */
const LB_PET_SLOTS = [['collar', 'Hạng Khuyên', 'Công'], ['armor', 'Móng Vuốt', 'Thủ'], ['charm', 'Hộ Mệnh Phù', 'Huyết']];
/** Ô trang bị (nhân vật và linh thú) trong hồ sơ: chỉ hiện icon như Hành trang, rê chuột xem thông tin trang bị. */
function lbGearIcon(name, k, sub = ''){
  const it = k && ITEM[k];
  if (!k) return `<div class="iv-slot lb-pf-eq empty" data-tip="${lbEsc(name)}${sub ? ' · ' + lbEsc(sub) : ''}&#10;Trống" aria-label="${lbEsc(name)}: trống"><span class="nm">${lbEsc(name)}</span></div>`;
  if (!it) return `<div class="iv-slot lb-pf-eq" data-tip="${lbEsc(name)}&#10;Vật phẩm bản game khác" aria-label="${lbEsc(name)}">?</div>`;
  return `<div class="iv-slot has lb-pf-eq r-${it.rarity}" data-card="view" data-k="${k}" data-tip="1" tabindex="0" aria-label="${lbEsc(name)}: ${lbEsc(it.name)}">${itemImg(k)}<span class="lv">${itemLv(it)}</span></div>`;
}
/** Hồ sơ giống Hành trang: tab Trang bị (ô trang bị hai bên ảnh nhân vật + chỉ số) và tab Linh thú (linh thú mạnh nhất + trang bị linh thú). */
/** Nhân vật đứng ở Bảng xếp hạng: cùng mô hình với Hành trang (hoạt ảnh theo trang phục + trận pháp và hiệu ứng chân theo cảnh giới), drawLbFigures vẽ mỗi khung hình. */
const lbFigure = (hero, realm, name) => `<canvas class="lb-fig" width="320" height="320" data-hero="${lbEsc(hero)}" data-realm="${+realm || 0}" role="img" aria-label="${lbEsc(name)}"></canvas>`;
function drawLbFigures(t){
  for (const c of document.querySelectorAll('canvas.lb-fig')){
    if (!c.offsetParent) continue;
    const g = c.getContext('2d'), hero = c.dataset.hero, realm = +c.dataset.realm;
    g.setTransform(1, 0, 0, 1, 0, 0); g.clearRect(0, 0, 320, 320);
    drawInventoryFormation(g, realm, t, reduce);   // cùng hiệu ứng Hành trang: trận pháp + hiệu ứng chân theo cảnh giới
    if (typeof drawInventoryFootRealmFx === 'function') drawInventoryFootRealmFx(g, realm, t, reduce);
    if (!drawStandHero(g, hero, realm, t, false)){   // trang phục chưa có hoạt ảnh: ảnh nhân vật tĩnh, chân chạm đất như khung Hành trang
      const im = skImg(hero);
      if (im){ const h = 290, w = Math.min(320, h * im.naturalWidth / im.naturalHeight); g.drawImage(im, (320 - w) / 2, 306 - h, w, h); }
    }
  }
}
function lbProfileHtml(pf){
  const st = pf.stats || {}, has = Object.keys(st).length > 0;
  const stat = (n, v) => `<div><span>${n}</span><b>${v}</b></div>`, pct = v => Math.round(v || 0) + '%';
  const slotName = Object.fromEntries(DATA.slots.map(sd => [sd.id, sd.name]));
  const side = ids => ids.filter(id => slotName[id]).map(id => lbGearIcon(slotName[id], pf.equipment?.[id])).join('');
  const extra = DATA.slots.map(sd => sd.id).filter(id => !SLOT_LEFT.includes(id) && !SLOT_RIGHT.includes(id));
  const hero = pf.hero || DATA.realms[pf.realm]?.hero;
  // Chỉ số giống Hành trang: nút "Chỉ số" cạnh Lực chiến, bấm ghim bảng 2 cột (main.js showTip đọc data-tip-tpl)
  const rate = v => `${fmtRating(v)}%`;
  const statRows = [['Tấn công', fmt(st.atk || 0)], ['Phòng thủ', fmt(st.def || 0)], ['Máu', fmt(st.hp || 0)],
    ['Bạo kích', rate(st.crit)], ['Kháng bạo kích', rate(st.crit_resist)], ['Chính xác', rate(st.accuracy)], ['Né tránh', rate(st.dodge ?? st.speed)],
    ['Liên kích', rate(st.combo)], ['Kháng liên kích', rate(st.combo_resist)], ['Phản đòn', rate(st.reflect)], ['Kháng phản đòn', rate(st.reflect_resist)]]
    .map(([n, v]) => `<div class="st"><span>${n}</span><b>${v}</b></div>`).join('');
  const statBtn = has ? ` <button type="button" class="iv-statbtn" data-tip="Chỉ số nhân vật" data-tip-tpl="lbPfStatsTip" data-tip-pin="1" aria-label="Chỉ số nhân vật">Chỉ số</button>` : '';
  const gear = `<div class="lb-pf-doll">
      <div class="lb-pf-col">${side(SLOT_LEFT)}</div>
      <div class="lb-pf-fig">${hero ? lbFigure(hero, pf.realm, pf.name) : lbIcon('realm')}<p class="iv-pow">Lực chiến <b>${fmt(pf.power)}</b>${statBtn}</p></div>
      <div class="lb-pf-col">${side([...SLOT_RIGHT, ...extra])}</div>
    </div>
    ${has ? `<template id="lbPfStatsTip"><b>Chỉ số nhân vật</b><div class="iv-stats">${statRows}</div><small>Chỉ số phụ cộng dồn không giới hạn, trừ chỉ số kháng của đối phương trước khi tính tỉ lệ kích hoạt.</small></template>` : ''}`;
  const p = pf.pet;
  let pet = '<p class="lb-pf-wait">Chưa có linh thú</p>';
  if (p){
    const stars = Math.max(0, Math.min(5, Number(p.stars) || 0)), awaken = Number(st.pet_awaken) || 0;
    const hd = String(p.image || '').replace(/(img\/pet\/[^/]+_[0-2])\.png$/, '$1_hd.png');
    const rules = S.pet?.training?.slots;
    const petSlots = LB_PET_SLOTS.map(([id, name, statName]) => {
      const r = rules?.find(x => x.id === id);
      return lbGearIcon(r?.name || name, pf.petGear?.[id], r?.statName || statName);
    }).join('');
    pet = `<div class="lb-pf-pet">
        <div class="lb-pf-pet-fig"><img src="${lbEsc(hd)}" alt="${lbEsc(p.name)}" onerror="this.onerror=null;this.src='${lbEsc(p.image)}'"></div>
        <div class="lb-pf-pet-info">
          <h4>${lbEsc(p.name)}</h4>
          <div class="lb-stars" aria-label="${stars} sao">${'★'.repeat(stars)}<i>${'★'.repeat(5 - stars)}</i></div>
          <p>Cấp ${fmt(p.level)}${awaken ? ` · Thức tỉnh ${['I','II','III'][awaken - 1] || awaken}` : ''}</p>
          <p class="lb-pf-pet-pow">Lực chiến linh thú <b>${fmt(pf.petPower || 0)}</b></p>
          ${st.pet_atk != null ? `<div class="lb-pf-stats lb-pf-pet-stats">${stat('Công', fmt(st.pet_atk))}${stat('Thủ', fmt(st.pet_def || 0))}${stat('Huyết', fmt(st.pet_hp || 0))}</div>` : ''}
        </div>
      </div>
      ${st.pet_atk != null ? `<div class="lb-pf-gear">${petSlots}</div>` : '<small class="lb-pf-at">Người này chưa chạy bản có gửi trang bị linh thú</small>'}`;
  }
  const tab = (id, label) => `<button type="button" role="tab" data-pf-tab="${id}" class="${lbPfTab === id ? 'on' : ''}" aria-selected="${lbPfTab === id}">${label}</button>`;
  const view = (id, html) => `<div class="lb-pf-view" role="tabpanel" data-pf-view="${id}"${lbPfTab === id ? '' : ' hidden'}>${html}</div>`;
  return `
    <header><div><h3>${lbEsc(pf.name)}${pf.me ? ' <em class="lb-you">Bạn</em>' : ''}</h3>${pf.titles?.length ? `<div class="lb-titles">${lbTitles(pf.titles)}</div>` : ''}<p>${lbEsc(pf.realmName)} · ${lbEsc(pf.stageName)} · Lực chiến <b>${fmt(pf.power)}</b></p></div>
      <button class="lb-pf-x" data-pf-x aria-label="Đóng">✕</button></header>
    <nav class="lb-pf-tabs" role="tablist">${tab('gear', 'Trang bị')}${tab('pet', 'Linh thú')}</nav>
    ${view('gear', gear)}${view('pet', pet)}
    <small class="lb-pf-at">Cập nhật ${new Date(pf.updatedAt).toLocaleString('vi-VN')}${has ? '' : ' · người này chưa chạy bản có gửi trang bị'}</small>`;
}
addEventListener('keydown', e => { if (e.key === 'Escape' && document.getElementById('lbPf')){ e.stopPropagation(); lbCloseProfile(); } }, true);
