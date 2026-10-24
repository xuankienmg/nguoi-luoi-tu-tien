/* Vấn Đạo · js/core.js: kết nối server, trạng thái, act(), tiện ích giao diện. Tách từ index.html (tools/split_index.py). */
/* ============================================================
   KẾT NỐI SERVER (ASP.NET Core + SQLite)
   Toàn bộ luật chơi nằm ở server, trình duyệt chỉ hiển thị.
   ============================================================ */
const KEY = 'van-dao-player-id';
const Q = new URLSearchParams(location.search);
const MODE = Q.get('mode') || 'full';          // full | strip | panel
/** Tab đang ẩn (hoặc dải taskbar, không có bảng): khỏi vẽ, đỡ giật. Không đọc layout nên rẻ, gọi mỗi khung được. */
const paneOff = id => MODE === 'strip' || !!document.getElementById('pane-' + id)?.hidden;
document.documentElement.classList.add(MODE);
document.body.classList.add(MODE);
if (Q.get('opaque') === '1') document.body.classList.add('opaque');
// Gửi tin nhắn cho cửa sổ desktop (WebView2). Trên trình duyệt thường thì bỏ qua.
const notifyHost = (type, extra = {}) => { try { window.chrome?.webview?.postMessage({type, ...extra}); } catch(e){} };
let DATA, ITEM = {}, S, recvAt = 0, busy = false;

let postSeq = 0;           // tăng mỗi lần gửi lệnh POST: sync() bỏ kết quả GET đã cũ (gửi trước lệnh) để không ghi đè trạng thái mới
async function api(method, url){
  if (method !== 'GET') postSeq++;
  const r = await fetch(url, {method, headers:{'Content-Type':'application/json'}, body: method === 'POST' ? '{}' : undefined});
  const j = await r.json().catch(() => ({}));
  if (r.status === 426 && j.update) window.showUpdateGate?.(j.update);    // bản game đã cũ: server từ chối, hiện màn hình cập nhật
  if (!r.ok){ const e = new Error(j.error || 'Không kết nối được máy chủ.'); e.status = r.status; throw e; }
  return j;
}
const realm = () => DATA.realms[S.realm];
/** Trang phục đang mặc (S.skin, rỗng = theo cảnh giới) → bộ ảnh nhân vật: 'hero' (sprite chiến đấu) · 'heroStand' · 'heroMed' (ngồi thiền) · 'heroCast' (tung chiêu). */
/** Trang phục cần rương trang phục (unlockItem) chỉ dùng được khi đã mở rương đó (S.skinsUnlocked). */
const skinOpen = s => !s.unlockItem || (S?.skinsUnlocked || []).includes(s.id);
// trang phục mở bằng rương: mặc ngay, không cần cảnh giới (khớp GameService.SkinWearable)
const skinWearable = s => skinOpen(s) && (!!s.unlockItem || S.realm >= s.minRealm);
const skinGender = () => S?.gender || (DATA.skins?.find(x => x.id === S?.skin)?.gender) || 'male';
const autoSkin = () => skinGender() === 'female' ? (DATA.skins || []).filter(s => s.gender === 'female' && s.minRealm <= S.realm && skinWearable(s)).sort((a,b) => b.minRealm - a.minRealm)[0] || null : null;
const curSkin = () => (S && S.skin && DATA.skins?.find(x => x.id === S.skin && skinWearable(x))) || (S ? autoSkin() : null);
const skinArt = k => curSkin()?.[k] || realm()[k];
/** Chương có Boss rơi nguyên liệu đột phá item: " (chương N)" để ghi kèm tên Boss. */
const bossChapterFor = item => (DATA.chapters || []).find(x => x.bossItem === item || x.hardBossItem === item || x.nightmareBossItem === item);
const bossDifficultyFor = item => { const c = bossChapterFor(item); return c?.nightmareBossItem === item ? 2 : c?.hardBossItem === item ? 1 : 0; };
const bossChapterTag = item => { const c = bossChapterFor(item), d = bossDifficultyFor(item); return c ? ` · chương ${c.index + 1}${d ? d === 2 ? ' · Ác Mộng' : ' · Khó' : ''}` : ''; };
/** Dòng gợi ý sau khi lên đại cảnh giới ri: Boss giữ nguyên liệu cho lần đột phá kế. */
const bossChapterHint = ri => { const r = DATA.realms[ri], c = r && bossChapterFor(r.bossItem);
  return c ? `Đột phá kế cần ${ITEM[r.bossItem].name}: Boss ${c.boss}${bossChapterTag(r.bossItem)}` : ''; };
// Tu vi dự đoán phía client giữa hai lần đồng bộ; server mới là nguồn chính xác
const qiNow = () => Math.min(S.qiReq, S.qi + S.rate * (performance.now() - recvAt) / 1000);
const exploreLeft = () => Math.max(0, S.exploreReadyInMs - (performance.now() - recvAt));
const hasAll = cost => cost.every(c => c.have >= c.need);
const fmtFull = n => Math.floor(n).toLocaleString('vi-VN');
// Floor compact values so an almost-full cultivation bar never rounds up to its target.
const fmtCompact = n => {
  const value = Math.floor(n), magnitude = Math.abs(value);
  const units = [[1e12, 'T'], [1e9, 'B'], [1e6, 'M'], [1e3, 'K']];
  const unit = units.find(([scale]) => magnitude >= scale);
  if (!unit || !Number.isFinite(value)) return fmtFull(value);
  const scaled = Math.trunc(value / (unit[0] / 100)) / 100;
  return scaled.toLocaleString('vi-VN', {maximumFractionDigits: 2}) + unit[1];
};
// Shared display formatter: preserve familiar values below one million.
const fmt = n => Math.abs(n) >= 1e6 ? fmtCompact(n) : fmtFull(n);
const fmtDecimal = n => Math.abs(n) >= 1e6 ? fmtCompact(n) : Number(n).toLocaleString('vi-VN', {maximumFractionDigits: 2});
// For plain display strings only, never HTML, IDs, form values or game calculations.
function fmtNumberText(text){
  return String(text).replace(/(^|[^\p{L}\p{N}_])([+-]?(?:\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:[.,]\d+)?))(?![\p{L}\p{N}_])/gu, (_, prefix, token) => {
    const normalized = /\.\d{3}(?:\.|,|$)/.test(token) ? token.replace(/\./g, '').replace(',', '.')
      : /,\d{3}(?:,|\.|$)/.test(token) ? token.replace(/,/g, '') : token.replace(',', '.');
    const value = Number(normalized);
    return prefix + (Math.abs(value) >= 1e6 ? (token.startsWith('+') ? '+' : '') + fmtCompact(value) : token);
  });
}
function setQiDisplay(el, current, required){
  if (!el) return;
  el.textContent = `${fmtCompact(current)} / ${fmtCompact(required)}`;
  el.title = `${fmtFull(current)} / ${fmtFull(required)}`;
}
const $ = id => document.getElementById(id);

let lastPull = 0;
/** Hậu tố id rương theo mức độ khó của map (Thường · Khó · Ác mộng), khớp GameData.DiffChestSuffix. */
const DIFF_CHEST = ['', '_kho', '_ac_mong'];
const chestOf = (k, dif) => k + (DIFF_CHEST[dif || 0] || '');
/** Các loại rương, quý → thường, mỗi loại mức cao trước (thứ tự hiện trong thông báo nhận rương). */
const CHEST_IDS = ['ruong_boss', 'ruong_hiem', 'ruong_thuong'].flatMap(k => [2, 1, 0].map(d => chestOf(k, d)));
let lastLogKey = '';
function setState(st){
  const prevKey = lastLogKey, prev = S;
  S = st; recvAt = performance.now();
  // rương rơi từ quái: số rương trong túi tăng so với lần trước → thông báo
  if (prev && prev.id === S.id){
    const got = CHEST_IDS.map(k => [k, (S.inventory[k] || 0) - (prev.inventory?.[k] || 0)]).filter(([, n]) => n > 0);
    if (got.length && typeof showChestDrop === 'function') showChestDrop(got);
    const pm = prev.map, nm = S.map;
    if (pm && nm && pm.realm === nm.realm && pm.area <= DATA.areas && nm.area < pm.area){
      const loss = (S.logs || []).find(l => /^Thua ở ải/.test(l.text));
      if (loss && Date.parse(loss.at) > mapPickAt) showKnockback(pm.area, nm.area);
    }
  }
  const top = S.logs[0]; lastLogKey = top ? top.at + top.text : '';
  if (prevKey && top && lastLogKey !== prevKey && (top.kind === 'loot' || top.kind === 'gold') && typeof floats !== 'undefined'){
    const loot = (top.text.split(': ')[1] || '').replace(/\.$/, '').split(', ').filter(x => x.startsWith('+')).slice(0, 3);
    loot.forEach((txt, i) => floats.push({x:0, row:i, text:txt, t:-i*.15, c: i === 0 ? null : '#e9dfc4'}));
    if (top.kind === 'gold') rings.push({t:0, c:'#e6c46a', big:false});
  }
  renderAll();
  // Hành trang đầy: nhật ký mới kể từ lần trước (lần đầu mở game: chỉ dòng tổng kết lúc vắng mặt)
  if (typeof bagWatch === 'function'){
    const i = prevKey ? S.logs.findIndex(l => l.at + l.text === prevKey) : -1;
    bagWatch(prevKey ? S.logs.slice(0, i < 0 ? S.logs.length : i) : S.logs.slice(0, 3).filter(l => /^Vắng mặt/.test(l.text)));
  }
}
function localLog(text, kind){ S.logs.unshift({at:new Date().toISOString(), text, kind}); renderLog(); }

async function act(path){              // trả về true khi server đã nhận lệnh thành công
  if (busy) return false; busy = true;
  const prevRealm = S.realm;
  try {
    const res = await api('POST', `/api/players/${S.id}/${path}`);
    setState(res.state);
    notifyHost('changed');
    if (path === 'advance'){                     // hiệu ứng chạy sau khi lần vẽ lại của setState đã xong, tránh khựng khung đầu
      const k = res.kind || 'good';
      requestAnimationFrame(() => requestAnimationFrame(() => playBreak(k)));
      notifyHost('break', {kind: k});
    }
    if (res.craft) showCraft(res.craft);
    return true;
  } catch(e){ localLog(e.message, 'bad'); ddErr = {msg: e.message, at: performance.now()}; return false; }
  finally { busy = false; live(); }
}
async function channel(){
  // phản hồi ngay trên màn hình, server xác nhận sau
  const g = S.channelGain;
  S.qi = Math.min(S.qiReq, qiNow() + g); recvAt = performance.now();
  floats.push({x:(Math.random()-.5)*80, y:-170, text:'+' + fmt(g), t:0}); live();
  // seq: lệnh khác (bán, luyện…) gửi sau thì kết quả tu luyện này đã cũ, không ghi đè số linh thạch / hành trang mới
  try { const req = api('POST', `/api/players/${S.id}/channel`), seq = postSeq, res = await req; if (!busy && seq === postSeq) setState(res.state); notifyHost('changed'); }
  catch(e){ localLog(e.message, 'bad'); }
}
async function sync(){
  if (busy || !S) return;
  const seq = postSeq;
  try { const r = await api('GET', `/api/players/${S.id}`); if (!busy && seq === postSeq) setState(r.state ?? r); } catch(e){}
}

/* ============================================================
   GIAO DIỆN
   ============================================================ */
function icon(k){
  const it = ITEM[k], c = it.color;
  const ticketClass = k === 've_tai_xiu' ? ' ticket-premium ticket-gear' : k === 've_linh_thu' ? ' ticket-premium ticket-pet' : '';
  if (it.icon) return `<img class="ic${ticketClass}" src="${it.icon}" alt="" style="${it.tint ? `filter:${it.tint}` : ''}">`;
  const shapes = {
    stone:`<path d="M12 2.5 20 8.5 16.8 20.5H7.2L4 8.5Z" fill="${c}" opacity=".9"/><path d="M12 2.5 9.5 8.5 12 20.5 14.5 8.5Z M4 8.5H20" fill="none" stroke="#0e1416" stroke-opacity=".35" stroke-width="1"/>`,
    pill:`<circle cx="12" cy="12" r="8" fill="${c}"/><ellipse cx="9.3" cy="9" rx="3" ry="2" fill="#fff" opacity=".55"/>`,
    herb:`<path d="M12 21.5c0-3 .5-6 2-8.5" stroke="#5f8a52" stroke-width="1.6" fill="none"/><path d="M14 13c-5 1-9-2-10-8 6-1 10 2 10 8Z" fill="${c}"/><path d="M14 13c3-1 6-4 6-8-4 0-7 3-6 8Z" fill="${c}" opacity=".75"/>`,
    artifact:`<path d="M12 3c-3 0-5 2-5 5 0 2 1 3 2 4-2 1-3 3-3 5 0 2.5 2.7 4 6 4s6-1.5 6-4c0-2-1-4-3-5 1-1 2-2 2-4 0-3-2-5-5-5Z" fill="${c}"/><path d="M9 12h6" stroke="#0e1416" stroke-opacity=".4" stroke-width="1.4"/><circle cx="10" cy="7" r="1.4" fill="#fff" opacity=".5"/>`,
    quest:`<rect x="5" y="3" width="14" height="18" rx="2" fill="${c}"/><path d="M8 8h8M8 12h8M8 16h5" stroke="#0e1416" stroke-opacity=".45" stroke-width="1.4"/>`,
    core:`<circle cx="12" cy="12" r="10" fill="none" stroke="${c}" stroke-opacity=".5" stroke-width="1.4"/><circle cx="12" cy="12" r="6.5" fill="${c}"/><circle cx="10" cy="10" r="2" fill="#fff" opacity=".5"/>`,
    ore:`<path d="M12 2 21 12 12 22 3 12Z" fill="${c}"/><path d="M12 2 12 22 M3 12H21" stroke="#0e1416" stroke-opacity=".3"/>`,
  };
  return `<svg class="ic" viewBox="0 0 24 24" aria-hidden="true">${shapes[it.type]}</svg>`;
}

/* ---- Giữ nhịp hiệu ứng lặp khi vẽ lại DOM ----
   Các bảng (Hành trang, Lò ghép…) vẽ lại bằng innerHTML nên phần tử mới khiến animation lặp
   (viền đồ Đỏ, lấp lánh đồ Thiên…) chạy lại từ đầu → giật. Neo mọi animation vô hạn vào gốc
   document.timeline: phần tử mới tiếp tục đúng pha cũ (animation-delay âm vẫn giữ độ lệch). */
(function(){
  if (!document.getAnimations) return;
  let queued = false;
  const align = () => {
    queued = false;
    for (const a of document.getAnimations()){
      if (!(a instanceof CSSAnimation) || a.startTime === 0 || a.playState !== 'running') continue;
      if (a.effect?.getTiming().iterations !== Infinity) continue;
      a.startTime = 0;
    }
  };
  const kick = () => { if (!queued){ queued = true; requestAnimationFrame(align); } };
  new MutationObserver(kick).observe(document.documentElement, {childList:true, subtree:true, attributes:true, attributeFilter:['class']});
  addEventListener('DOMContentLoaded', kick); kick();
})();
