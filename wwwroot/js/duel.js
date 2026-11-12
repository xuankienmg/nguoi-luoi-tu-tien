/* Vấn Đạo · js/duel.js: cơ chế đánh mới dùng chung toàn game (Lôi đài, Boss tông, sau này các trận khác). Server mô phỏng cả trận (DuelDto), client chỉ hiện popup nhẹ: hai ấn tròn đối mặt, thanh máu chảy về phía mình, nhật ký như trao đổi chiêu. Gọi: duelShow(duel, {win, verdict, message, title}). */
let duelTimer = 0;
function duelClose(){ clearInterval(duelTimer); duelTimer = 0; $('duelPop')?.remove(); }

function duelSide(s, cls){
  const pet = s.pet ? `<span class="dl-pet">${s.petImage ? `<img src="${lbEsc(s.petImage)}" alt="">` : ''}${lbEsc(s.pet)}</span>` : '';
  return `<div class="dl-side ${cls}"><div class="dl-av">${s.hero ? `<img src="${lbEsc(s.hero)}" alt="">` : ''}</div>
    <b class="dl-name">${lbEsc(s.name)}</b>${pet}
    <div class="dl-hp"><i></i></div><div class="dl-hpn"></div></div>`;
}

const DUEL_SRC = {first: ['✦', 'src-skill'], basic: ['⚔', 'src-basic'], skill: ['✺', 'src-skill'], extra: ['≫', 'src-extra'], bolt: ['ϟ', 'src-bolt'],
  burn: ['♨', 'src-burn'], pet: ['❖', 'src-pet'], reflect: ['↺', 'src-ref'], petreflect: ['↺', 'src-ref'], heal: ['✚', 'src-heal']};

function duelShow(d, opt = {}){
  duelClose();
  const steps = d.steps || [], message = opt.message || '', win = opt.win !== false, verdict = opt.verdict || (win ? 'Thắng' : 'Bại');
  const root = document.createElement('div');
  root.id = 'duelPop'; root.className = 'duel-pop'; root.setAttribute('role', 'dialog'); root.setAttribute('aria-label', opt.title || 'Diễn biến trận đấu');
  root.innerHTML = `<div class="dl-box">
    <header class="dl-top"><h3>${lbEsc(opt.title || 'Lôi đài')}</h3><button type="button" class="dl-x" data-dl="close" aria-label="Đóng">✕</button></header>
    <div class="dl-field">${duelSide(d.me, 'me')}<span class="dl-vs" aria-hidden="true">VS</span>${duelSide(d.foe, 'foe')}</div>
    <ul class="dl-log" aria-live="polite"></ul>
    <div class="dl-end" hidden><span class="dl-seal ${win ? 'win' : 'lose'}">${verdict}</span><p></p></div>
    <div class="dl-btns"><button type="button" class="ar-go" data-dl="skip">Bỏ qua</button><button type="button" class="ar-go dl-done" data-dl="close" hidden>Đóng</button></div>
  </div>`;
  document.body.appendChild(root);
  const me = root.querySelector('.dl-side.me'), foe = root.querySelector('.dl-side.foe'), log = root.querySelector('.dl-log');
  const setHp = (el, hp, max) => {
    const v = Math.max(0, hp), pct = Math.max(0, Math.min(100, v / max * 100));
    el.querySelector('.dl-hp i').style.width = pct + '%';
    el.querySelector('.dl-hpn').innerHTML = `<b>${fmt(Math.round(v))}</b><span> / ${fmt(Math.round(max))}</span><em>${pct >= 1 || v <= 0 ? Math.round(pct) : '<1'}%</em>`;
    el.classList.toggle('down', v <= 0);
  };
  const curMe = d.me.cur ?? d.me.hp, curFoe = d.foe.cur ?? d.foe.hp;   // cur: máu lúc vào trận (Boss tông có máu chung, không đầy)
  setHp(me, curMe, d.me.hp); setHp(foe, curFoe, d.foe.hp);
  const pop = (el, text, cls) => {                                  // số máu mất bay lên từ ấn bị đánh
    if (reduce || !text) return;
    const n = document.createElement('span'); n.className = 'dl-pop ' + cls; n.textContent = text;
    el.querySelector('.dl-av').appendChild(n); setTimeout(() => n.remove(), 900);
  };
  let i = 0, done = false, prevMe = curMe, prevFoe = curFoe;
  const chip = p => {
    const [g, cls] = DUEL_SRC[p.kind] || ['•', ''];
    const name = p.kind === 'pet' ? `Linh thú ${lbEsc(p.name)}` : p.kind === 'petreflect' ? `Linh thú ${lbEsc(p.name)} phản đòn` : p.kind === 'heal' && p.name !== 'Hồi máu' ? `${lbEsc(p.name)} hồi` : lbEsc(p.name);
    return `<span class="src ${cls}${p.crit ? ' crit' : ''}"><u>${g}</u><span>${name}</span><b>${p.kind === 'heal' ? '+' : '-'}${fmt(Math.round(p.value))}</b>${p.crit ? '<i>Bạo kích</i>' : ''}</span>`;
  };
  const finish = () => {
    if (done) return; done = true; clearInterval(duelTimer); duelTimer = 0;
    const last = steps[steps.length - 1];
    if (last){ setHp(me, last.hpMe, d.me.hp); setHp(foe, last.hpFoe, d.foe.hp); }
    const end = root.querySelector('.dl-end');
    end.hidden = false; end.className = 'dl-end ' + (win ? 'good' : 'bad');
    end.querySelector('p').textContent = message || '';
    root.querySelector('[data-dl=skip]').hidden = true;
    const ok = root.querySelector('.dl-done'); ok.hidden = false; ok.focus();
  };
  const step = () => {
    const s = steps[i++]; if (!s){ finish(); return; }
    const atk = s.byMe ? me : foe, tgt = s.byMe ? foe : me;
    const lostFoe = Math.max(0, prevFoe - s.hpFoe), lostMe = Math.max(0, prevMe - s.hpMe);   // máu thật mất sau lượt (đã trừ khiên / giảm sát thương)
    prevMe = s.hpMe; prevFoe = s.hpFoe;
    if (!reduce){
      atk.classList.add('hit'); setTimeout(() => atk.classList.remove('hit'), 220);
      if (!s.dodged && !s.nullified && !s.control){ tgt.classList.add('struck'); setTimeout(() => tgt.classList.remove('struck'), 380); }
    }
    setHp(me, s.hpMe, d.me.hp); setHp(foe, s.hpFoe, d.foe.hp);
    const whoName = s.byMe ? d.me.name : d.foe.name, tgtName = s.byMe ? d.foe.name : d.me.name;
    const dealt = s.byMe ? lostFoe : lostMe, taken = s.byMe ? lostMe : lostFoe;
    pop(tgt, dealt > 0 ? '-' + fmt(Math.round(dealt)) : (s.dodged || s.nullified ? 'Né' : ''), dealt > 0 ? 'dmg' : 'miss');
    if (taken > 0) pop(atk, '-' + fmt(Math.round(taken)), 'dmg');
    const hurt = s.parts.filter(p => !p.self), self = s.parts.filter(p => p.self);
    const rows = [];
    if (s.control) rows.push(`<p class="dl-note">${lbEsc(whoName)} bị ${{stun: 'choáng', paralyze: 'tê liệt', freeze: 'đóng băng'}[s.control] || 'khống chế'}, mất lượt</p>`);
    if (s.dodged) rows.push(`<p class="dl-note">${lbEsc(tgtName)} né tránh đòn</p>`);
    else if (s.nullified) rows.push(`<p class="dl-note">Thần hộ của ${lbEsc(tgtName)} vô hiệu đòn</p>`);
    if (hurt.length) rows.push(`<div class="dl-chips">${hurt.map(chip).join('')}</div><p class="dl-net">${lbEsc(tgtName)} mất <b>${fmt(Math.round(dealt))}</b> máu</p>`);
    if (self.length) rows.push(`<div class="dl-chips self">${self.map(chip).join('')}</div>`);
    if (!rows.length) rows.push(`<p class="dl-note">${lbEsc(whoName)} không gây được sát thương</p>`);
    const li = document.createElement('li'); li.className = s.byMe ? 'me' : 'foe';
    li.innerHTML = `<span class="dl-who"><span class="dl-rd">Lượt <b>${s.round || ''}</b></span> ${lbEsc(whoName)}</span>${rows.join('')}`;
    log.prepend(li); while (log.children.length > 14) log.lastChild.remove();
    if (i >= steps.length) setTimeout(finish, 300);
  };
  root.addEventListener('click', e => {
    const t = e.target.closest('[data-dl]');
    if (!t){ if (e.target === root && done) duelClose(); return; }
    if (t.dataset.dl === 'skip'){ while (i < steps.length) step(); finish(); } else duelClose();
  });
  // Mở popup trước, trận chỉ bắt đầu sau khi ảnh hai bên đã tải xong và popup đã hiện hẳn (có nhịp dừng để xem hai bên đối mặt, thanh máu đầy)
  const gap = Math.max(200, Math.min(800, 10000 / Math.max(1, steps.length)));
  const start = () => { if (done || !root.isConnected) return; duelTimer = setInterval(step, gap); step(); };
  const urls = [d.me.hero, d.me.petImage, d.foe.hero, d.foe.petImage].filter(Boolean);
  const loaded = Promise.all(urls.map(u => new Promise(res => { const im = new Image(); im.onload = im.onerror = res; im.src = u; })));
  Promise.race([loaded, new Promise(res => setTimeout(res, 2000))]).then(() => {
    if (!root.isConnected) return;
    root.classList.add('open');
    setTimeout(start, reduce ? 0 : 900);
  });
}
