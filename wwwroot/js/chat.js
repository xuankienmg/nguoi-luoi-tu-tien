/* Vấn Đạo · js/chat.js: kênh Thế giới trong panel Nhật ký (chỉ khi chơi online). Luật nằm ở server (ChatRules.cs). */

/* chat-pure:start */
const CHAT_KEEP = 100;
/** Poll lùi 20 id: id gán lúc INSERT nhưng commit cuối request, nên tin id nhỏ có thể hiện sau tin id lớn. */
const CHAT_OVERLAP = 20;
/** Tin trong incoming có id chưa có trong list, xếp theo id. */
function chatFresh(list, incoming){
  const seen = new Set(list.map(m => m.id));
  return incoming.filter(m => !seen.has(m.id)).sort((a, b) => a.id - b.id);
}
/** Gộp tin mới: bỏ trùng theo id, chèn tin id thấp đến muộn đúng chỗ, giữ CHAT_KEEP tin cuối. */
function chatMerge(list, incoming){
  const out = list.concat(chatFresh(list, incoming));
  out.sort((a, b) => a.id - b.id);
  return out.length > CHAT_KEEP ? out.slice(out.length - CHAT_KEEP) : out;
}
/** Tin chưa đọc: bỏ tin hệ thống và tin của chính mình. */
function chatUnread(msgs){ return msgs.filter(m => m.kind !== 'system' && !m.mine).length; }
function chatBadge(n){ return n <= 0 ? '' : n > 9 ? '9+' : String(n); }
/** watching = panel mở và đang ở tab Thế giới. null = dừng poll (cửa sổ ẩn). Lỗi liên tiếp: 4 → 8 → 16 giây. */
function chatPollDelay({hidden, watching, fails}){
  if (hidden) return null;
  const base = watching ? 4000 : 20000;                             // panel đóng: lỗi cũng không poll dày hơn nhịp khỏe 20 s
  return fails > 0 ? Math.max(base, Math.min(16000, 4000 * 2 ** Math.min(fails, 2))) : base;
}
/* chat-pure:end */

/* ---------- Giao diện ---------- */
const CHAT = {on: false, tab: 'log', msgs: [], unread: 0, fails: 0, timer: 0, wait: 0, waitTimer: 0, sending: false, page: null, err: '', seq: 0, applied: 0, pollAt: 0};
const chatLastId = () => CHAT.msgs.length ? CHAT.msgs[CHAT.msgs.length - 1].id : 0;
const chatUrl = () => `/api/players/${S.id}/chat?after=${Math.max(0, chatLastId() - CHAT_OVERLAP)}`;
const chatOpen = () => !!(document.querySelector('.altar')?.classList.contains('log-open') || $('log')?.checkVisibility?.());
const chatWatching = () => CHAT.tab === 'chat' && chatOpen();
const chatTime = iso => { const d = new Date(iso); return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`; };
/** Mỗi cảnh giới một màu nhãn (chat.css .rc0–.rc9); cảnh giới mới ngoài bảng dùng màu cao nhất. */
const chatRealmClass = realm => 'rc' + Math.max(0, Math.min(9, realm | 0));

function chatLine(m){
  const p = document.createElement('p');
  p.className = m.kind === 'system' ? 'gold' : m.mine ? 'mine' : '';
  const t = document.createElement('time'); t.textContent = chatTime(m.at); p.append(t, ' ');
  if (m.kind !== 'system'){
    const tier = chatRealmClass(m.realm);
    const r = document.createElement('span'); r.className = 'chat-realm ' + tier; r.textContent = window.tr(m.realmName); p.append(r);
    const n = document.createElement('b'); n.className = 'chat-name ' + tier; n.setAttribute('translate', 'no'); n.textContent = m.name; p.append(n, ': ');
  }
  const body = document.createElement('span'); body.setAttribute('translate', 'no');   // i18n bỏ qua tin người chơi
  body.append(document.createTextNode(m.text));                   // nội dung người chơi: chỉ textContent, không bao giờ innerHTML
  if (m.kind === 'system') body.removeAttribute('translate');     // tin hệ thống vẫn được dịch
  p.append(body);
  return p;
}

function chatRenderMsgs(added){
  const box = $('chatMsgs'), atEnd = box.scrollHeight - box.scrollTop - box.clientHeight < 24, top = box.scrollTop;
  if (added === null){ box.replaceChildren(...CHAT.msgs.map(chatLine)); }
  else { added.forEach(m => box.append(chatLine(m))); while (box.childElementCount > CHAT_KEEP) box.firstElementChild.remove(); }
  if (atEnd || (added === null && !CHAT.rendered)) box.scrollTop = box.scrollHeight;  // đang đọc tin cũ thì không kéo xuống
  else if (added === null) box.scrollTop = top;
  CHAT.rendered = true;
}

function chatRenderBadge(){
  const txt = chatBadge(CHAT.unread);
  for (const id of ['chatTabBadge', 'chatBadge']){ const b = $(id); if (b){ b.textContent = txt; b.hidden = !txt; } }
  $('btnLog')?.setAttribute('aria-label', txt ? `${window.tr('Nhật ký tu hành')} · ${txt} ${window.tr('tin chưa đọc')}` : window.tr('Nhật ký tu hành'));
}

function chatRenderForm(){
  const pg = CHAT.page; if (!pg) return;
  const input = $('chatInput'), send = $('chatSend'), bar = $('chatBar');
  if (!input || !send || !bar) return;
  const locked = !!pg.lockReason;
  const msg = locked ? window.tr(pg.lockReason) : CHAT.err ? CHAT.err : CHAT.fails ? window.tr('Đang kết nối lại… tin mới sẽ hiện khi có mạng.') : '';
  $('chatForm').hidden = locked;
  const hint = $('chatHint')?.parentElement; if (hint) hint.hidden = locked;
  bar.hidden = !msg;
  bar.className = 'chat-bar' + (!locked && (CHAT.err || CHAT.fails) ? ' bad' : '');
  bar.textContent = msg;
  send.disabled = CHAT.sending || CHAT.wait > 0 || !input.value.trim();
  send.textContent = CHAT.wait > 0 ? CHAT.wait + 's' : window.tr('Gửi');
  $('chatCount').textContent = `${input.value.length}/${pg.maxLen}`;
  const on = $('chatOnline'); on.hidden = false; on.textContent = `${pg.online} ${window.tr('đạo hữu')}`;
}

function chatStartWait(sec){
  CHAT.wait = sec; clearInterval(CHAT.waitTimer);
  if (sec > 0) CHAT.waitTimer = setInterval(() => { CHAT.wait = Math.max(0, CHAT.wait - 1); chatRenderForm(); if (!CHAT.wait) clearInterval(CHAT.waitTimer); }, 1000);
  chatRenderForm();
}

/** seq: số thứ tự request; phản hồi cũ hơn phản hồi đã áp thì chỉ gộp tin (idempotent), không ghi đè trạng thái trang. */
function chatApply(pg, seq = ++CHAT.seq){
  const latest = seq >= CHAT.applied; if (latest) CHAT.applied = seq;
  if (!pg.enabled){ if (!latest) return; CHAT.on = false; $('log').classList.remove('chat-on'); $('logTabChat').hidden = true; $('chatOnline').hidden = true; chatSetTab('log'); return; }
  const first = !CHAT.on; CHAT.on = true; if (latest) CHAT.page = pg;
  $('logTabChat').hidden = false; $('log').classList.add('chat-on');
  const prevMax = chatLastId();
  const fresh = chatFresh(CHAT.msgs, pg.messages);                 // chỉ tin chưa từng hiện
  CHAT.msgs = chatMerge(CHAT.msgs, pg.messages);
  if (!first && !chatWatching()) CHAT.unread += chatUnread(fresh);
  if (first || fresh.some(m => m.id < prevMax)) chatRenderMsgs(null);   // tin đến muộn id thấp: dựng lại cả danh sách
  else if (fresh.length) chatRenderMsgs(fresh);
  if (latest && !CHAT.wait && pg.cooldownLeft) chatStartWait(pg.cooldownLeft);
  chatRenderBadge(); chatRenderForm();
}

async function chatPoll(){
  clearTimeout(CHAT.timer);
  if (!syncOnline()) return;
  if (CHAT.pollAt && Date.now() - CHAT.pollAt < 1000) return;      // đang có GET vừa gửi: request đó sẽ tự đặt lịch tiếp
  const seq = ++CHAT.seq; CHAT.pollAt = Date.now();
  try {
    if (!S?.id) throw new Error('no-player');                      // chưa nạp nhân vật: thử lại sau, không báo mất mạng
    chatApply(await api('GET', chatUrl()), seq); CHAT.fails = 0;
  } catch (e){ if (S?.id){ CHAT.fails++; chatRenderForm(); } }
  finally { CHAT.pollAt = 0; }
  chatSchedule();
}
function chatSchedule(){
  clearTimeout(CHAT.timer);
  if (!syncOnline()) return;                                         // bản offline: không bao giờ poll
  const d = chatPollDelay({hidden: document.hidden, watching: chatWatching(), fails: CHAT.fails});
  if (d !== null) CHAT.timer = setTimeout(chatPoll, d);
}

function chatSetTab(tab){
  CHAT.tab = tab === 'chat' && CHAT.on ? 'chat' : 'log';
  const chat = CHAT.tab === 'chat';
  $('logTabLog').setAttribute('aria-selected', !chat); $('logTabChat').setAttribute('aria-selected', chat);
  $('logList').hidden = chat; $('chatPane').hidden = !chat;
  $('log').classList.toggle('chat-mode', chat);
  if (chat){ CHAT.unread = 0; chatRenderBadge(); $('chatMsgs').scrollTop = $('chatMsgs').scrollHeight; chatPoll(); }
}

async function chatSend(e){
  e.preventDefault();
  const input = $('chatInput'), text = input.value.trim();
  if (!text || CHAT.sending || CHAT.wait) return;
  CHAT.sending = true; CHAT.err = ''; chatRenderForm();
  try {
    const seq = ++CHAT.seq;
    const pg = await api('POST', chatUrl(), {text});
    input.value = ''; chatApply(pg, seq); chatStartWait(pg.cooldownLeft || 3);
  } catch (err){
    CHAT.err = err.message || window.tr('Chưa gửi được.');         // giữ chữ trong ô để gửi lại
  } finally { CHAT.sending = false; chatRenderForm(); }
}

function initChat(){
  if (!$('chatPane') || !$('btnLog')) return;
  const badge = document.createElement('span'); badge.className = 'chat-badge on-btn'; badge.id = 'chatBadge'; badge.hidden = true;
  $('btnLog').append(badge);
  $('logTabLog').onclick = () => chatSetTab('log');
  $('logTabChat').onclick = () => chatSetTab('chat');
  $('chatForm').onsubmit = chatSend;
  $('chatInput').oninput = () => { CHAT.err = ''; chatRenderForm(); };
  document.addEventListener('visibilitychange', () => document.hidden ? clearTimeout(CHAT.timer) : chatPoll());
  $('btnLog').addEventListener('click', () => setTimeout(() => {      // mở/đóng panel → đổi nhịp 4 s / 20 s
    if (chatWatching()){ CHAT.unread = 0; chatRenderBadge(); $('chatMsgs').scrollTop = $('chatMsgs').scrollHeight; chatPoll(); }
    else chatSchedule();
  }));
  if (syncOnline()) chatPoll();
}
