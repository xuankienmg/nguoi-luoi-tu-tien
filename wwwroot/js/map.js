/* Vấn Đạo · js/map.js: tab Bản đồ. Tách từ index.html (tools/split_index.py). */
// =====================================================================
// BẢN ĐỒ: 10 chương × (15 ải + ải Boss). S.map.realm = chương đang đánh (0-based); thắng Boss chương cao nhất mở chương kế
// =====================================================================
function renderMap(){
  const pane = $('pane-map'), oldInfo = $('mpxInfo'), paneTop = pane.scrollTop;
  const previousInfoKey = oldInfo?.dataset.infoKey;
  const infoScrolls = oldInfo ? [...oldInfo.children].map(el => ({top:el.scrollTop,left:el.scrollLeft})) : [];
  const m = displayedMap(), c = (m.chapters || DATA.chapters)[m.realm], R = m.realm + 1, keyN = S.inventory[m.keyItem] || 0, dif = m.diff || 0;
  const chOpen = i => m.chapterOpen ? m.chapterOpen[i] : DATA.chapters[i].enabled && i <= m.chapterMax;
  const chDone = i => m.chapterCleared ? m.chapterCleared[i] : i < m.chapterMax, back = chDone(m.realm);
  const N = mapAreaCount(m), B = N + 1, inBoss = m.area > N;
  const bossOpen = m.areaMax >= N, canBoss = bossOpen && (keyN > 0 || adminBoss());
  // chọn mức độ khó: Thường · Khó (nối tiếp sau chương 10 Thường) · Ác mộng (nối tiếp sau chương 10 Khó); mức chưa mở thì khóa
  const diffSel = `<label class="mpx-diff d${dif}" data-tip="Mức độ khó&#10;Khó: nối tiếp sau chương 10 Thường (ải 1 mạnh hơn ải 15 chương 10 ~10%), mỗi chương ×1,3; thưởng theo chương 10 ×1,5, mỗi chương thêm +8% và đồ phẩm cao dần&#10;Ác mộng: nối tiếp sau chương 10 Khó, mỗi chương ×1,3; thưởng theo chương 10 ×2,5, mỗi chương thêm +8% và đồ phẩm cao dần&#10;Địa Ngục 1: 30 ải + Boss mỗi chương, dành cho Độ Kiếp / Chân Tiên; quái có skill địa ngục riêng, thưởng ×3,5 và +8% mỗi chương&#10;Hạ Boss chương 10 ở mức trước để mở mức kế, rồi đi lần lượt từ chương 1">
    <select id="mpDiff" aria-label="Mức độ khó">${MAP_DIFFS.map((n, i) => `<option value="${i}" ${i === dif ? 'selected' : ''} ${m.diffOpen && !m.diffOpen[i] ? 'disabled' : ''}>${n}${m.diffOpen && !m.diffOpen[i] ? ' 🔒' : ''}</option>`).join('')}</select></label>`;
  // thanh chọn chương 1–10: đã qua ✓ · đang mở sáng · chưa mở / tạm khóa có ổ khóa
  const chapterBar = `<nav class="mpx-chs" aria-label="Chọn chương">${(m.chapters || DATA.chapters).map(x => {
    const open = chOpen(x.index), done = chDone(x.index), cur = x.index === m.realm;
    const tip = `Chương ${x.index + 1} · ${x.name}&#10;Boss: ${open ? x.boss + ' · ' + x.bossLair : '???'}`
      + (!x.enabled ? '&#10;Tạm khóa, sẽ mở ở bản cập nhật sau' : !open ? `&#10;Đánh bại Boss chương ${x.index}${dif ? ' mức ' + MAP_DIFFS[dif] : ''} để mở` : done ? '&#10;Đã hạ Boss: mọi ải đã mở' : '');
    return `<button class="${cur ? 'on' : ''}${done ? ' done' : ''}${open ? '' : ' lk'}" data-mch="${x.index}" aria-pressed="${cur}" ${open ? '' : 'aria-disabled="true"'} data-tip="${tip}">`
      + `<b>${x.index + 1}</b>${open ? (done ? '<i>✓</i>' : '') : `<span class="lkc">${LOCK_SVG}</span>`}</button>`; }).join('')}</nav>`;
  const pass = a => { const st = mapStateOf(a);
    return mapFilter === 'all' || (mapFilter === 'open' && st !== 'locked') || (mapFilter === 'win' && st === 'win')
      || (mapFilter === 'lose' && st === 'lose') || (mapFilter === 'boss' && a.index === B); };
  const FILTERS = [['all', 'Tất cả'], ['open', 'Đã mở'], ['win', 'Vượt qua'], ['lose', 'Nguy hiểm'], ['boss', 'Boss']];

  // ---- đường ải ngang, xếp zigzag 2 tầng cho vừa 16 ải không cần cuộn (tọa độ %, co theo khung) ----
  const pts = m.areas.map(a => mapPos(a.index));
  const seg = (from, to) => pts.slice(from, to).map((p, k) => `${k ? 'L' : 'M'}${p.x} ${p.y}`).join(' ');
  const reach = Math.min(m.areaMax + (bossOpen ? 1 : 0), B);                   // đoạn đã đi qua
  const hero = skinArt('hero').replace('.png', '_48.png');
  const fr = mapFrontier(m);
  const node = a => {
    const st = mapStateOf(a), p = mapPos(a.index), boss = a.index === B;
    const go = st !== 'locked' && st !== 'cur' && (!boss || canBoss);
    const why = st === 'locked' ? (boss ? `Mở ải ${R}-${N} trước` : `Thắng ải ${R}-${a.index - 1} để mở`) : boss && !canBoss && st !== 'cur' ? `Cần ${ITEM[m.keyItem].name}` : boss ? `Boss ${m.boss} · mỗi lần khiêu chiến tốn 1 ${ITEM[m.keyItem].name}` : '';
    const lab = boss ? 'BOSS' : `${a.elite ? '❖ ' : ''}${R}-${a.index}${st === 'win' ? ' ✓' : st === 'lose' ? ' nguy hiểm' : ''}`;   // ❖ = có quái tinh anh
    const isTop = !boss && a.index === fr.top;                          // ⚑ ải cao nhất đã mở
    return `<button class="mpx-n ${st}${boss ? ' bs' : ''}${a.elite ? ' el' : ''}${isTop ? ' top' + (fr.knocked ? ' knock' : '') : ''}" style="left:${p.x}%;top:${p.y}%" data-mx="${a.index}" ${go ? '' : 'aria-disabled="true"'}
      aria-label="Ải ${R}-${a.index}${boss ? ' Boss' : ''}" ${why ? `data-tip="${why}"` : ''}>
      <span class="o">${boss ? `<img src="${m.bossImage.replace('.png', '_48.png')}" alt="">` : st === 'locked' ? LOCK_SVG : st === 'cur' ? `<img class="me" src="${hero}" alt="">` : ''}</span>
      ${st === 'cur' ? '<span class="now">đang đánh</span>' : ''}${isTop && st !== 'cur' ? `<span class="fl" data-tip="Ải cao nhất đã mở${fr.knocked ? '&#10;Thua ở đây nên bị lùi về ' + R + '-' + m.area : ''}">⚑</span>` : ''}<span class="lb">${lab}</span></button>`;
  };
  // vạch ngăn giữa các vùng (nét đứt), đặt giữa ải cuối vùng trước và ải đầu vùng sau
  const seps = m.areas.filter((a, i) => i > 0 && a.index <= N && m.areas[i - 1].place !== a.place).map(a => (mapPos(a.index - 1).x + mapPos(a.index).x) / 2);
  const note = `${back ? `Chương ${R} đã qua, mọi ải đã mở. ` : 'Thắng ải cao nhất sẽ mở ải kế tiếp, thua thì lùi một ải. Hạ Boss chương để mở chương kế. '}${ITEM[m.keyItem].name} rơi ở ải ${R}-1 đến ${R}-${N}, mỗi lần khiêu chiến Boss tốn 1 tấm.&#10;Bấm vào ải để xuất chinh; rê chuột để xem thông tin.`;
  const old = document.querySelector('#pane-map .mpx-track'), keepX = old && mapScrollRealm === m.realm ? old.scrollLeft : null;
  $('pane-map').innerHTML = `<div class="mpx${dif === 3 ? ' mpx-hell' : ''}">
    <div class="mpx-row">${diffSel}${chapterBar}</div>
    <header class="mp-hd mpx-hd"><div><b>Chương ${R} · ${m.chapterName || c.name}${dif ? ` <em class="mpx-dtag d${dif}">${MAP_DIFFS[dif]}</em>` : ''} <small class="mp-back">${back ? 'đã qua' : 'gợi ý ' + (m.suggestedRealm || c.realm)}</small> <i class="mpx-q" data-tip="${note}">?</i></b>
      <span>${!m.area ? `Đang xem chương ${R} · Nhân vật đang đánh ải ${S.map.realm+1}-${S.map.area}` : inBoss ? `Đang khiêu chiến Boss · ${m.bossLair}` : `Đang đánh ải ${R}-${m.area} · ${m.areas[m.area - 1].place}`}${m.area && !inBoss && m.area < fr.top ? ` · <em class="mpx-top ${fr.knocked ? 'down' : ''}">⚑ Ải cao nhất ${R}-${fr.top}${fr.knocked ? ' (bị lùi do thua)' : ''}</em>` : ''}</span></div></header>
    <div class="mpx-wrap"><button class="mpx-nav l" data-mnav="-1" aria-label="Cuộn trái">‹</button><div class="mpx-track"><div class="mpx-lane" style="--map-width:${Math.max(1240, B * 78)}px">
      <svg class="mpx-path" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
        <path d="${seg(0, B)}" class="ln"/>${reach > 1 ? `<path d="${seg(0, reach)}" class="dn"/>` : ''}</svg>
      ${seps.map(x => `<i class="mpx-sep" style="left:${x}%"></i>`).join('')}
      ${m.areas.map(node).join('')}
    </div></div><button class="mpx-nav r" data-mnav="1" aria-label="Cuộn phải">›</button></div>
    <div class="mpx-info" id="mpxInfo"></div>
  </div>`;
  if (oldInfo) $('mpxInfo').replaceWith(oldInfo);
  renderMapInfo();
  if (oldInfo && oldInfo.dataset.infoKey === previousInfoKey) [...oldInfo.children].forEach((el,i) => {
    el.scrollTop = infoScrolls[i]?.top || 0; el.scrollLeft = infoScrolls[i]?.left || 0;
  });
  pane.scrollTop = paneTop;
  // cuộn ngang: giữ vị trí cũ giữa các lần vẽ lại; mới mở / đổi bản đồ thì đưa ải đang đánh vào giữa
  const tr = document.querySelector('#pane-map .mpx-track');
  if (keepX != null && !mapNeedCenter) tr.scrollLeft = keepX; else mapCenter();
  mapScrollRealm = m.realm;
}
/** Đưa ải đang đánh vào giữa khung (tab đang ẩn thì để dành tới lúc mở tab). */
function mapCenter(){
  const tr = document.querySelector('#pane-map .mpx-track'), lane = tr?.firstElementChild;
  if (!tr || !tr.clientWidth){ mapNeedCenter = true; return; }
  const m = displayedMap();
  tr.scrollLeft = Math.max(0, lane.clientWidth * mapPos(m.area || 1).x / 100 - tr.clientWidth / 2);
  mapNeedCenter = false;
}
/** Trạng thái ô ải: locked · win · lose · cur · boss (Boss đã mở, chưa đánh). */
function mapStateOf(a){
  const m = displayedMap(), B = mapBossArea(m);
  if (a.index === B) return m.area > mapAreaCount(m) ? 'cur' : a.open ? 'boss' : 'locked';
  return !a.open ? 'locked' : a.index === m.area ? 'cur' : a.win ? 'win' : 'lose';
}
/** Vị trí ô ải (%): ải 1 bên trái → Boss bên phải, hai tầng so le trên/dưới. */
function mapPos(i, m = displayedMap()){
  return {x: 3.5 + (i - 1) * 91 / mapAreaCount(m), y: i > mapAreaCount(m) ? 52 : 54 + 16 * Math.sin(i * 0.95)};
}
function mapMonsterTip(monster, hidden = false){
  if(hidden)return 'Chưa thám thính được quái này.';
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
  const ratings = [['crit','Bạo kích'],['critResist','Kháng bạo kích'],['accuracy','Chính xác'],['dodge','Né tránh'],['combo','Liên kích'],['comboResist','Kháng liên kích'],['reflect','Phản đòn'],['reflectResist','Kháng phản đòn']]
    .map(([key,label]) => `&#10;${label}: ${Number(monster[key] || 0).toLocaleString('vi-VN',{maximumFractionDigits:2})}%`).join('');
  return `${esc(monster.name || monster.monster)}&#10;Tấn công: ${fmt(monster.atk)}&#10;Phòng thủ: ${fmt(monster.def)}&#10;Máu: ${fmt(monster.hp)}${ratings}&#10;Chỉ số không giới hạn trên; tính sau đối kháng.${(monster.skills || []).map(skill => `&#10;${esc(skill.name)}: ${esc(skill.description)}`).join('')}`;
}
/** Bảng dưới: thông tin ải đang rê chuột (mặc định ải đang đánh). */
function renderMapInfo(){
  const host = $('mpxInfo'); if (!host) return;
  const m = displayedMap(), R = m.realm + 1, N = mapAreaCount(m), B = N + 1, keyN = S.inventory[m.keyItem] || 0, dif2 = m.diff || 0;
  const a = m.areas[Math.min(mapHover || m.area || 1, B) - 1], st = mapStateOf(a), boss = a.index === B, hidden = st === 'locked' && !boss;
  const pct = v => v >= 1 ? '100%' : (Math.round(v * 1000) / 10).toLocaleString('vi-VN') + '%';
  // phần thưởng: gom vào danh sách, xếp theo nhóm (tài nguyên → đan → linh thảo → lệnh / rương → trang bị), trong nhóm theo tỉ lệ giảm dần
  const rows = [];
  const groupOf = k => { const it = ITEM[k] || {}; return k === 'linh_thach' ? 0 : it.slot || it.type === 'petgear' ? 5 : it.type === 'chest' ? 4 : it.type === 'herb' ? 2 : it.type === 'key' || it.type === 'boss' ? 3 : 1; };
  const cell = (k, chance, qty, name) => { if (ITEM[k]) rows.push({k, chance, qty, name, g: groupOf(k)}); return ''; };
  const chip = r => r.k === 'qi' ? `<div class="mpx-rw" data-tip="Tu vi ×5 khi hạ Boss"><span class="dd-fr qi">☯</span><span class="t"><small>Tu vi ×5</small><em>100%</em></span></div>`
    : `<div class="mpx-rw" data-tip="${itemTip(r.k)}"><span class="dd-fr r-${ITEM[r.k].rarity || 'common'}">${itemImg(r.k)}${r.qty ? `<b>${r.qty}</b>` : ''}</span><span class="t"><small>${r.name || ITEM[r.k].name}</small><em>${pct(r.chance)}</em></span></div>`;
  let rewards;
  const bw = boss && a.waves?.[0];   // Rương Boss: mọi mức khó chỉ rơi 1 rương (GameData.DiffBossChests)
  if (boss) rewards = (bw?.stoneMax ? cell('linh_thach', 1, bw.stoneMin === bw.stoneMax ? bw.stoneMin : `${bw.stoneMin}-${bw.stoneMax}`) : '') + (m.bossItem ? cell(m.bossItem, 1) : '') + cell(m.bossChest || chestOf('ruong_boss', dif2), 1) + (rows.push({k: 'qi', chance: 1, g: 0}), '');
  else {
    const k = a.index, drops = a.drops || m.drops || [], gear = drops.filter(x => x.gear);
    rewards = drops.filter(x => !x.gear && k >= (x.minArea || 1)).map(x => {
      const stone = x.item === 'linh_thach', sc = stone ? (1 + (k - 1) * .05) * (a.elite ? 1.5 : 1) * (m.rewardMult || 1) : a.drops ? 1 : (m.rewardMult || 1), lo = Math.round(x.min * sc), hi = Math.round(x.max * sc);
      return cell(x.item, a.drops ? x.chance : Math.min(1, x.chance * (stone || x.chance >= 1 ? 1 : 1 + (k - 1) * .03)), lo === hi ? (lo > 1 ? lo : '') : `${lo}-${hi}`);
    }).join('');
    if (gear.length){
      const any = 1 - gear.reduce((p, x) => p * (1 - (a.drops ? x.chance : Math.min(1, x.chance * (1 + (k - 1) * .03)))), 1), v = DATA.items.find(i => i.base === gear[0].item);
      if (v) rewards += cell(v.id, any, '', 'Trang bị ' + (v.set || ''));
    }
    if(!a.drops){
      rewards += cell(m.keyItem, m.keyChanceMin);
      if (m.chestChance) rewards += cell(chestOf('ruong_thuong', dif2), m.chestChance);
      if (m.rareChestChance && k >= m.rareChestMinArea) rewards += cell(chestOf('ruong_hiem', dif2), (a.elite ? 1.5 : 1) * m.rareChestChance);
    }
  }
  if(boss){
    // trang bị linh thú: một lần rơi (ô + phẩm ngẫu nhiên) → gộp một ô, hiện món phẩm cao nhất có thể rơi
    const petGear=(a.drops||[]).filter(x=>ITEM[x.item]?.type==='petgear'),top=petGear.reduce((t,x)=>!t||FUSE_R.indexOf(ITEM[x.item].rarity)>FUSE_R.indexOf(ITEM[t.item].rarity)?x:t,null);
    rewards+=(a.drops||[]).filter(x=>!petGear.includes(x)).map(x=>cell(x.item,x.chance,x.min===x.max?x.min:`${x.min}-${x.max}`)).join('');
    if(top)rewards+=cell(top.item,petGear.reduce((n,x)=>n+x.chance,0),'','Trang bị linh thú');
  }
  rows.sort((x, y) => x.g - y.g || y.chance - x.chance);
  rewards = rows.map(chip).join('');
  const img = (a.image || m.bossImage).replace('.png', '_hd.png');
  const stats = hidden ? '<p class="mpx-hid">Chưa thám thính được ải này.</p>'
    : boss || adminOn ? (!boss && a.waves?.length > 1
        ? `<p class="mpx-st">Máu ${a.waves.map(w => `<b>${fmt(w.hp)}</b>`).join(' · ')} · Công ${a.waves.map(w => `<b>${fmt(w.atk)}</b>`).join(' · ')}</p>`
        : `<p class="mpx-st">Máu <b>${fmt(a.hp)}</b> · Công <b>${fmt(a.atk)}</b> · Thủ <b>${fmt(a.def)}</b></p>`) + `
      <p class="mpx-pr ${a.win ? 'ok' : 'bad'}">${a.win ? `Dự đoán: thắng sau ${a.hits} đòn` : 'Dự đoán: thua, cần mạnh hơn'}</p>`
    : st === 'win' ? '<p class="mpx-hid">Đã vượt qua ải này.</p>' : st === 'lose' ? '<p class="mpx-hid">Yêu khí dày đặc, cẩn thận.</p>' : '';
  const fr = mapFrontier(m);
  const bossOpen = m.areaMax >= N, canBoss = bossOpen && (keyN > 0 || adminBoss());
  const foot = boss ? `<p class="mpx-key"><span class="mp-need ${keyN > 0 ? 'ok' : ''}">${itemImg(m.keyItem)} ${keyN} / 1</span>
      ${st === 'cur' ? '<i>Đang khiêu chiến · mỗi trận tốn 1 lệnh</i>' : !bossOpen ? `<i class="bad">Mở ải ${R}-${N} trước</i>` : !canBoss ? `<i class="bad">Cần ${ITEM[m.keyItem].name}</i>` : `<i>Bấm Boss để khiêu chiến${m.realm === m.chapterMax && DATA.chapters[R]?.enabled ? ` · thắng mở chương ${R + 1}` : ''}</i>`}</p>`
    : st === 'cur' ? '' : `<p class="mpx-key"><i class="${st === 'locked' ? 'bad' : ''}">${st === 'locked' ? `Thắng ải ${R}-${a.index - 1} để mở` : 'Bấm vào ải để xuất chinh'}</i></p>`;
  const knock = st === 'cur' && fr.knocked ? `<p class="mpx-down">⚑ Thua ở ${R}-${fr.top}, đang luyện ở ${R}-${a.index} <button data-retry="${fr.top}">Thử lại ${R}-${fr.top}</button></p>` : '';
  const hdImg = src => `<img src="${src.replace('.png', '_hd.png')}" onerror="this.onerror=null;this.src=this.src.replace('_hd.png','_48.png')" alt="">`;
  // ải thường: 3 đợt quái (ảnh nhỏ + tên + chỉ số rút gọn), trạng thái ải là nhãn cạnh tiêu đề; ải Boss: 1 ảnh lớn
  const chipSt = hidden ? ['locked', 'Chưa mở'] : st === 'cur' ? ['cur', 'Đang luyện'] : st === 'win' ? ['ok', 'Đã vượt'] : st === 'lose' ? ['bad', 'Nguy hiểm'] : st === 'locked' ? ['locked', 'Chưa mở'] : ['', ''];
  const wvStat = w => hidden ? '<span class="ws">???</span>' : `<span class="ws" title="Máu · Công · Thủ"><i class="hp">${fmtCompact(w.hp)}</i><i class="at">${fmtCompact(w.atk)}</i><i class="df">${fmtCompact(w.def)}</i></span>`;
  const mon = !boss && a.waves?.length > 1
    ? `<section class="mpx-mon wv"><div class="tx"><p class="ey"><span>Ải ${R}-${a.index} · ${a.place} · ${a.waves.length} đợt</span>${chipSt[1] ? `<b class="mpx-chip ${chipSt[0]}">${chipSt[1]}</b>` : ''}</p>${knock}
        <div class="mpx-wv">${a.waves.map((w, i) => `<div class="w ${w.tier}"><span class="pic ${hidden ? 'unk' : ''}" data-tip="${mapMonsterTip(w,hidden)}">${hdImg(w.image)}</span>
          <span class="wt"><i>Đợt ${i + 1}${w.tier === 'elite' ? ' · tinh anh' : ''}</i><small>${hidden ? '???' : w.name}</small>${wvStat(w)}</span></div>`).join('')}</div>
        <div class="mpx-ft">${adminOn && !hidden ? `<p class="mpx-pr ${a.win ? 'ok' : 'bad'}">${a.win ? `Dự đoán: thắng sau ${a.hits} đòn` : 'Dự đoán: thua, cần mạnh hơn'}</p>` : ''}${foot}</div></div></section>`
    : `<section class="mpx-mon ${boss ? 'bs' : ''}">
      <div class="pic ${hidden ? 'unk' : ''}" data-tip="${mapMonsterTip(a.waves?.[0] || a,hidden)}">${hdImg(img.replace('_hd.png', '.png'))}</div>
      <div class="tx"><p class="ey">${boss ? `Ải ${R}-${B} · Boss` : `Ải ${R}-${a.index}`} · ${boss ? m.bossLair : a.place}</p>
        <p class="nm">${hidden ? '???' : a.monster}</p>${stats}${foot}</div></section>`;
  const infoKey = `${dif2}:${m.realm}:${a.index}`;
  const scrolls = host.dataset.infoKey === infoKey ? [...host.children].map(el => ({top:el.scrollTop,left:el.scrollLeft})) : [];
  const html = `${mon}
    <section class="mpx-rws"><p class="dd-sec">Phần thưởng · tỉ lệ rơi mỗi trận thắng</p><div class="g">${rewards}</div></section>`;
  if (host._mapInfoHtml !== html) {
    host.innerHTML = html;
    host._mapInfoHtml = html;
    [...host.children].forEach((el,i) => { el.scrollTop = scrolls[i]?.top || 0; el.scrollLeft = scrolls[i]?.left || 0; });
  }
  host.dataset.infoKey = infoKey;
}
/** Tên mức độ khó (giống GameData.DiffNames). */
const MAP_DIFFS = ['Thường', 'Khó', 'Ác mộng', 'Địa Ngục 1'];
let mapFilter = 'all', mapHover = 0, mapScrollRealm = -1, mapNeedCenter = true;
let mapPreview = null, mapPreviewRequest = 0;
function mapAreaCount(m = S.map){ return m.normalAreaCount || DATA.areas; }
function mapBossArea(m = S.map){ return m.bossAreaIndex || mapAreaCount(m) + 1; }
function displayedMap(){
  if(mapPreview && (mapPreview.diff !== (S.map.diff || 0) || mapPreview.realm === S.map.realm)) mapPreview=null;
  return mapPreview || S.map;
}
async function previewChapter(chapter){
  const request=++mapPreviewRequest, player=S.id, diff=S.map.diff||0;
  if(chapter===S.map.realm){mapPreview=null;mapHover=0;mapNeedCenter=true;renderMap();return;}
  try {
    const view=await api('GET',`/api/players/${encodeURIComponent(player)}/map/${chapter}`);
    if(request!==mapPreviewRequest||S.id!==player||(S.map.diff||0)!==diff)return;
    mapPreview=view;mapHover=0;mapNeedCenter=true;renderMap();
  } catch(e){if(request===mapPreviewRequest)localLog(e.message,'bad');}
}
/** Lần cuối người chơi tự chọn ải (ms): sau đó mới coi là "tự xuống ải thấp", không phải bị lùi do thua. */
let mapPickAt = 0;
try { mapPickAt = +localStorage.getItem('van-dao-map-pick') || 0; } catch(e){}
/** Ải cao nhất đã mở của bản đồ đang xem và nhân vật có đang bị lùi xuống dưới do thua không. */
function mapFrontier(m = S.map){
  const top = Math.min(m.areaMax, mapAreaCount(m));
  if (!m.area) return {top, knocked:false};
  if (m.area > mapAreaCount(m) || m.area >= top) return {top, knocked: false};
  const loss = (S.logs || []).find(l => /^Thua ở ải|^Vắng mặt .*\(thua \d+\)/.test(l.text));
  return {top, knocked: !!loss && Date.parse(loss.at) > mapPickAt};
}
function initMap(){
  const pane = $('pane-map');
  new IntersectionObserver(es => { if (mapNeedCenter && es.some(e => e.isIntersecting)) mapCenter(); }).observe(pane);
  // lăn chuột dọc trên dải đường ải = cuộn ngang
  pane.addEventListener('wheel', e => {
    const tr = e.target.closest('.mpx-track'); if (!tr || Math.abs(e.deltaX) > Math.abs(e.deltaY) || tr.scrollWidth <= tr.clientWidth) return;
    tr.scrollLeft += e.deltaY; e.preventDefault();
  }, {passive: false});
  pane.addEventListener('change', e => {
    if (e.target.id !== 'mpDiff') return;
    mapPreview=null;mapPreviewRequest++;mapHover = 0; mapNeedCenter = true; act(`diff/${e.target.value}`);
  });
  pane.addEventListener('click', e => {
    const mc = e.target.closest('[data-mch]');
    if (mc){ if (mc.getAttribute('aria-disabled') !== 'true') previewChapter(+mc.dataset.mch); return; }
    const nv = e.target.closest('[data-mnav]');
    if (nv){ const tr = pane.querySelector('.mpx-track'); tr.scrollBy({left: +nv.dataset.mnav * tr.clientWidth * .7, behavior: 'smooth'}); return; }
    const f = e.target.closest('[data-mf]');
    if (f){ mapFilter = f.dataset.mf; renderMap(); return; }
    const n = e.target.closest('[data-mx]');                     // bấm ô ải = xuất chinh ngay
    const rt = e.target.closest('[data-retry]');                                       // nút "Thử lại" ải cao nhất (bảng thông tin)
    if (rt){ mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e2){} act(`area/${rt.dataset.retry}`); return; }
    if (n && n.getAttribute('aria-disabled') !== 'true'){
      mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e){}   // tự chọn ải: không còn tính là bị lùi
      const chapter=displayedMap().realm;
      mapPreviewRequest++;
      act(chapter===S.map.realm?`area/${n.dataset.mx}`:`map/${chapter}/area/${n.dataset.mx}`);
    }
  });
  pane.addEventListener('mouseover', e => {
    const n = e.target.closest('[data-mx]'); if (!n || +n.dataset.mx === mapHover) return;
    mapHover = +n.dataset.mx; renderMapInfo();
  });
  pane.addEventListener('mouseleave', () => { if (mapHover){ mapHover = 0; renderMapInfo(); } });
  pane.addEventListener('mouseout', e => {
    if (e.target.closest('.mpx-track') && !e.relatedTarget?.closest?.('.mpx-track') && mapHover){ mapHover = 0; renderMapInfo(); }
  });
}
