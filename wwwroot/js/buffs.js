/* js/buffs.js: hiệu lực đan hỗ trợ đang có (Tụ Linh, Đan Tâm, Tật Hành, Thú Linh, Bế Quan) — biểu tượng + đếm ngược. */
const BUFF_Q = ['q0', 'q1', 'q2'];
function buffLeft(b){ return Math.max(0, b.remainingMs - (performance.now() - recvAt)); }
function buffClock(ms){
  const s = Math.ceil(ms / 1000), h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60), ss = s % 60;
  return h ? `${h}:${String(m).padStart(2, '0')}:${String(ss).padStart(2, '0')}` : `${m}:${String(ss).padStart(2, '0')}`;
}
/** Vẽ vào mọi .buff-row: một ô mỗi hiệu lực; chỉ dựng lại khi danh sách đổi, còn lại chỉ cập nhật đồng hồ. */
function renderBuffs(){
  const list = (S?.buffs || []).filter(b => buffLeft(b) > 0), key = list.map(b => b.id + b.quality).join('|');
  document.querySelectorAll('.buff-row').forEach(row => {
    if (row._key !== key){
      row._key = key;
      row.hidden = !list.length;
      row.innerHTML = list.map(b => `<span class="buff ${BUFF_Q[b.quality]}" data-buff="${b.id}" data-tip="${b.name} · ${b.qualityName}&#10;${b.effect}"><img src="${b.icon}" alt=""><b></b></span>`).join('');
    }
    for (const b of list){ const t = row.querySelector(`[data-buff="${b.id}"] b`); if (t) t.textContent = buffClock(buffLeft(b)); }
  });
}
setInterval(() => { if (S && !document.hidden) renderBuffs(); }, 1000);
