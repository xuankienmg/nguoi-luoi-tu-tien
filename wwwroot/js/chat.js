/* Vấn Đạo · js/chat.js: kênh Thế giới trong panel Nhật ký (chỉ khi chơi online). Luật nằm ở server (ChatRules.cs). */

/* chat-pure:start */
const CHAT_KEEP = 100;
/** Gộp tin mới (theo id tăng dần), bỏ trùng, giữ CHAT_KEEP tin cuối. */
function chatMerge(list, incoming){
  const last = list.length ? list[list.length - 1].id : 0;
  const out = list.concat(incoming.filter(m => m.id > last));
  return out.length > CHAT_KEEP ? out.slice(out.length - CHAT_KEEP) : out;
}
/** Tin chưa đọc: bỏ tin hệ thống và tin của chính mình. */
function chatUnread(msgs){ return msgs.filter(m => m.kind !== 'system' && !m.mine).length; }
function chatBadge(n){ return n <= 0 ? '' : n > 9 ? '9+' : String(n); }
/** watching = panel mở và đang ở tab Thế giới. null = dừng poll (cửa sổ ẩn). Lỗi liên tiếp: 4 → 8 → 16 giây. */
function chatPollDelay({hidden, watching, fails}){
  if (hidden) return null;
  if (fails > 0) return Math.min(16000, 4000 * 2 ** Math.min(fails, 2));
  return watching ? 4000 : 20000;
}
/* chat-pure:end */
