/* js/endless.js: mức Thiên Giới (Diff 5) trong tab Bản đồ — tầng hiện tại / tầng cao nhất, vài tầng quanh đỉnh, nhắc luật.
   Dữ liệu: S.map.endless (EndlessDto, Game/Dtos.cs); luật và công thức: Game/EndlessData.cs. */
const ENDLESS_DIFF = 5, ENDLESS_BOSS_EVERY = 10;
/** Nhãn ải / tầng: Thiên Giới ghi "tầng N", các mức khác "R-N" (word=true thì thêm chữ "ải"). */
function mapAreaLabel(R, n, word){
  if ((S.map?.diff || 0) === ENDLESS_DIFF) return `tầng ${n}`;
  return word ? `ải ${R}-${n}` : `${R}-${n}`;
}
/** Từ "Tầng" cho tiêu đề trơn ("Tầng 5" trùng khóa dịch tầng cảnh giới "Stage 5"), nên chọn theo ngôn ngữ ngay ở đây. */
const FLOOR_WORD = () => window.LANG === 'en' ? 'Floor' : 'Tầng';
const endlessTag = n => n % ENDLESS_BOSS_EVERY === 0 ? 'boss' : n % 5 === 0 ? 'elite' : '';

function renderEndless(m, diffSel){
  const e = m.endless, pane = $('pane-map'), top = Math.min(e.top, e.maxFloor), paneTop = pane.scrollTop;
  const oldGo = pane.querySelector('.edl-go input'), typing = oldGo && document.activeElement === oldGo ? oldGo.value : null;   // giữ số đang gõ khi đồng bộ vẽ lại
  const hero = skinArt('hero').replace('.png', '_48.png');
  const pic = src => `<img src="${src.replace('.png', '_48.png')}" onerror="this.onerror=null;this.src='${src}'" alt="">`;
  const card = a => {
    const f = a.index, tag = endlessTag(f), locked = !a.open && !adminOn, cur = f === e.floor;
    const st = cur ? 'cur' : locked ? 'locked' : a.win ? 'win' : 'lose';
    const stText = cur ? 'Đang luyện' : locked ? 'Chưa mở' : a.win ? 'Vượt được' : 'Nguy hiểm';
    const mons = locked ? '<p class="edl-hid">Chưa thám thính được tầng này.</p>'
      : `<div class="edl-wv">${a.waves.map(w => `<div class="w ${w.tier}"><span class="pic" data-tip="${mapMonsterTip(w)}">${pic(w.image)}</span>
          <span class="wt"><small>${w.name}${w.tier === 'elite' ? ' · tinh anh' : w.tier === 'boss' ? ' · Boss' : ''}</small>
          <span class="ws" title="Máu · Công · Thủ"><i class="hp">${fmtCompact(w.hp)}</i><i class="at">${fmtCompact(w.atk)}</i><i class="df">${fmtCompact(w.def)}</i></span></span></div>`).join('')}</div>`;
    const go = !locked && !cur;
    return `<article class="edl-card ${st}${tag ? ' ' + tag : ''}">
      <header><b>${FLOOR_WORD()} ${f}</b>${tag === 'boss' ? '<em class="bs">Boss</em>' : tag === 'elite' ? '<em class="el">Tinh anh</em>' : ''}<span class="pl">${a.place.split(' · ')[0]}</span><i class="${st}">${stText}</i></header>
      ${mons}
      <footer>${locked ? '' : `<span class="${a.win ? 'ok' : 'bad'}">${a.win ? `Dự đoán: thắng sau ${a.hits} đòn` : 'Dự đoán: thua, cần mạnh hơn'}</span>`}
        <button type="button" data-efloor="${f}" ${go || cur ? '' : 'disabled'}>${cur ? 'Về màn hình chính' : go ? 'Xuất chinh' : 'Khóa'}</button></footer></article>`;
  };
  const pct = Math.min(100, top / e.maxFloor * 100);
  const nextBossIn = e.nextBoss - e.floor;
  pane.innerHTML = `<div class="mpx mpx-endless">
    <div class="mpx-row">${diffSel}</div>
    <header class="edl-hd">
      <div class="edl-ttl"><small>Thiên Giới · tầng tăng dần</small><b>${FLOOR_WORD()} ${e.floor}</b>
        <span>${e.floor < top ? `Tầng cao nhất đã mở: <em>${top}</em>` : `Đang ở tầng cao nhất đã mở`} · tối đa ${e.maxFloor}</span></div>
      <div class="edl-nav">
        <button type="button" data-efloor="${Math.max(1, e.floor - 10)}" ${e.floor > 1 ? '' : 'disabled'}>« 10</button>
        <button type="button" data-efloor="${Math.max(1, e.floor - 1)}" ${e.floor > 1 ? '' : 'disabled'}>‹ Tầng trước</button>
        <button type="button" class="top" data-efloor="${top}" ${e.floor === top ? 'disabled' : ''}>⚑ Về tầng cao nhất (${top})</button>
        <button type="button" data-efloor="${Math.min(top, e.floor + 1)}" ${e.floor < top ? '' : 'disabled'}>Tầng kế ›</button>
        <button type="button" data-efloor="${Math.min(top, e.floor + 10)}" ${e.floor < top ? '' : 'disabled'}>10 »</button>
        <form class="edl-go" data-etop="${top}"><input type="number" name="f" min="1" max="${top}" value="${e.floor}" aria-label="Chọn tầng"><button type="submit">Đến tầng</button></form>
      </div>
      <div class="edl-prog"><span>Tầng ${top} / ${e.maxFloor} · Boss kế tiếp: tầng ${e.nextBoss}${nextBossIn > 0 ? ` (còn ${nextBossIn} tầng)` : ' (tầng này)'}</span><i><b style="width:${pct.toFixed(1)}%"></b></i></div>
    </header>
    <div class="edl-list">${m.endless.preview.map(card).join('')}</div>
    <p class="edl-note">Mỗi 5 tầng có quái tinh anh; mỗi ${e.bossEvery} tầng có Boss (không cần lệnh, lần đầu hạ mỗi Boss nhận Rương Boss). Mỗi 25 tầng đổi chủ đề quái. Thắng ở tầng cao nhất sẽ mở tầng kế, thua thì lùi một tầng. Chỉ số quái tăng dần theo tầng, thưởng tăng chậm hơn.</p>
  </div>`;
  pane.scrollTop = paneTop;
  if (typing !== null){ const inp = pane.querySelector('.edl-go input'); inp.value = typing; inp.focus(); }
}
/** Bấm nút tầng trong màn hình Thiên Giới; trả về true nếu đã xử lý. */
function endlessClick(e){
  if (e.target.closest('.edl-go')){   // ô nhập tầng: bấm nút "Đến tầng" (submit) xử lý ở endlessGo
    if (e.target.matches('button[type=submit]')){ e.preventDefault(); endlessGo(e.target.form); }
    return true;
  }
  const b = e.target.closest('[data-efloor]'); if (!b) return false;
  if (b.disabled) return true;
  const n = +b.dataset.efloor;
  if (n === S.map.area){ backToMain(); return true; }
  mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e2){}
  mapTravel(`area/${n}`);
  return true;
}
/** Xuất chinh tới tầng nhập trong ô (1..tầng cao nhất đã mở). */
function endlessGo(form){
  const top = +form.dataset.etop, n = Math.round(+form.f.value);
  if (!(n >= 1)) return;
  const f = Math.min(top, n);
  if (f === S.map.area) return;
  mapPickAt = Date.now(); try { localStorage.setItem('van-dao-map-pick', mapPickAt); } catch(e2){}
  mapTravel(`area/${f}`);
}
document.addEventListener('submit', e => { const f = e.target.closest?.('.edl-go'); if (f){ e.preventDefault(); endlessGo(f); } });
