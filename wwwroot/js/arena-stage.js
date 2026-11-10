/* Vấn Đạo · js/arena-stage.js: Lôi đài trên màn hình chính. Trận vừa đánh (S.arena, server tính bằng PvpCombat) diễn bằng chính
   drawPixelBattle như trận đánh quái: mình bên trái ra đòn đủ kỹ năng, đối thủ (tu sĩ, quay mặt sang trái) phản đòn bên phải.
   Mỗi trận chỉ diễn một lần (nhớ id trong localStorage); diễn xong quay lại thám hiểm. */
let arStage = null, arSeen = 0;
try { arSeen = +localStorage.getItem('van-dao-arena-seen') || 0; } catch(e){}

const AR_ENTER = 1, AR_END = 3;                                        // giây: đối thủ bước vào · đứng lại sau trận

/** Trận Lôi đài đang diễn: {b, ph} cho drawPixelBattle, hoặc null (diễn xong / không có). */
function arenaStageBattle(){
  if (MODE === 'panel' || !S) return null;
  const rp = S.arena;
  if (rp && rp.id !== arSeen && !arStage){
    arSeen = rp.id; try { localStorage.setItem('van-dao-arena-seen', rp.id); } catch(e){}
    arStage = {b: {...rp.battle, pvp: {...rp.pvp, oldRank: rp.oldRank, newRank: rp.newRank, practice: !!rp.practice}}, t0: performance.now(), trip: 900000 + rp.id % 100000};
    pfx = [];                                                            // bỏ chữ / tia lửa còn bay của trận thám hiểm trước đó
  }
  if (!arStage) return null;
  const ph = arenaPhase(arStage, (performance.now() - arStage.t0) / 1000);
  if (!ph){ arStage = null; return null; }
  return {b: arStage.b, ph};
}

/** Lịch diễn: vào trận → mỗi lượt 1 giây (liên kích dài thêm EX_REP, giống battlePhase) → thắng / thua. */
function arenaPhase(st, e){
  const b = st.b, hits = Math.max(1, b.hits), trip = st.trip;
  if (e < AR_ENTER) return {name: 'enter', k: e / AR_ENTER, trip, wave: 0};
  let x = e - AR_ENTER;
  const petTurns = new Set((b.pet?.casts || []).filter(c => !(c.at >= .85)).map(c => c.turn));   // lượt linh thú của mình ra chiêu: thêm pha 'pet' sau đòn của tu sĩ (như battlePhase)
  for (let k = 0; k < hits; k++){
    const timing=typeof battleSkillTiming==='function' ? battleSkillTiming(b,k) : null;
    const L = timing ? timing.length : 1 + (b.extra?.[k] ? EX_REP : 0);
    const heroEnd = timing ? timing.heroEnd : .7 + (b.extra?.[k] ? EX_REP : 0);
    if (petTurns.has(k) && x >= heroEnd){
      if (x < heroEnd + PET_CAST_SECONDS) return {name: 'pet', i: k, s: (x - heroEnd) / PET_CAST_SECONDS, trip, wave: 0};
      x -= PET_CAST_SECONDS;
    }
    if (x < L){
      const progress=timing ? timing.progress(x) : {s:!b.extra?.[k] || x < .62 ? x : x < .62 + EX_REP ? .28 + (x - .62) : x - EX_REP,rep:!!b.extra?.[k] && x>=.62 && x<.62+EX_REP};
      return {name: 'fight', i: k, ...progress, trip, wave: 0};
    }
    x -= L;
  }
  return x < AR_END ? {name: b.win ? 'win' : 'lose', k: Math.min(1, x / 1.5), trip} : null;
}

/** Đòn phản của đối thủ: lao tới trước mặt mình, chém, lùi về (thay cho monAttack của quái). */
const PVP_FX_FILTER = {thuy: 'hue-rotate(185deg) saturate(1.1)', moc: 'hue-rotate(85deg)', tho: 'hue-rotate(-25deg) saturate(.7) brightness(.95)',
  loi: 'hue-rotate(245deg) brightness(1.25)', bang: 'hue-rotate(170deg) saturate(.55) brightness(1.35)', gio: 'hue-rotate(105deg) saturate(.7) brightness(1.2)'};

/** Công pháp / đòn thường của đối thủ: quả cầu năng lượng theo hệ bay từ đối thủ sang mình (8 khung fxSheet như chiêu của mình). Trả true nếu đã vẽ (lượt dùng công pháp: không lao tới chém). */
function pvpFoeFx(k, pv, i, o){
  const id = pv.foeSkill?.[i] || pv.foeBasic, def = typeof skById === 'function' ? skById(id) : null, skill = !!pv.foeSkill?.[i];
  const {heroX, wolfX, g, sz, U, fxAfter} = o, filter = PVP_FX_FILTER[def?.element] || 'none';
  const hx = wolfX - U(10), tx = heroX + U(8), y = g - Math.round(sz * .55), ty = g - Math.round(sz * .45), scale = skill ? 1.5 : 1;
  fxAfter.push(() => {
    if (typeof fxSheet === 'undefined' || !fxSheet.complete || !fxSheet.naturalWidth) return;
    const draw = (f, x, yy) => { const d = Math.round(sz * scale); pctx.filter = filter; pctx.drawImage(fxSheet, (f % 4) * 48, Math.floor(f / 4) * 48, 48, 48, Math.round(x - d / 2), Math.round(yy - d / 2), d, d); pctx.filter = 'none'; };
    if (!skill){ if (k >= .3 && k < .75) draw(k < .45 ? 5 : k < .6 ? 6 : 7, tx, ty); }   // đòn thường: lao tới chém, nổ nguyên tố theo hệ tại chỗ trúng
    else if (k < .2) draw(Math.min(2, Math.floor(k / .07)), hx, y);
    else if (k < .65) draw(k < .4 ? 3 : 4, hx + (tx - hx) * (k - .2) / .45, y + (ty - y) * (k - .2) / .45);
    else draw(k < .78 ? 5 : k < .9 ? 6 : 7, tx, ty);
  });
  return skill;
}

function pvpLunge(k, o){
  const {heroX, wolfX, g, U} = o, near = heroX + U(26), e = v => v * v * (3 - 2 * v);
  if (o.pv && pvpFoeFx(k, o.pv, o.i, o)) return {wx: wolfX, knock: 3, numCol: '#ffb38f', sparks: ['#ffb38f', '#fff0c2', '#e0584a'], nSparks: 10, spread: 50};   // công pháp: đứng yên phóng chiêu
  const wx = k < .35 ? Math.round(wolfX + (near - wolfX) * e(k / .35)) : k < .75 ? near : Math.round(near + (wolfX - near) * e((k - .75) / .25));
  if (k >= .3 && k < .6) pslash(heroX + U(8), g - U(24), MODE === 'strip' ? 8 : 18, -1.9, 1.3, Math.min(1, (k - .3) * 4), k < .45 ? 1 : 1 - (k - .45) * 6);
  return {wx, knock: 3, numCol: '#ffb38f', sparks: ['#ffb38f', '#fff0c2', '#e0584a'], nSparks: 10, spread: 50};
}

/** Chữ riêng của trận PK trong một lượt: tên chiêu đối thủ, né, thiêu đốt lên mình, đối thủ hồi máu, mình bị khống. */
function pvpTurnFeedback(pv, i, gfx, o){
  const {heroX, wolfX, g, sz, msz, U} = o, big = MODE !== 'strip';
  if (o.idle){
    const ctl = typeof BATTLE_CONTROL !== 'undefined' ? BATTLE_CONTROL[pv.idleKind?.[i]] : null;
    if (ctl) gfx('pvidle', .1, () => pfx.push({type:'label', x: heroX, y: g - sz - 14, text: ctl.name, col: ctl.col, life: 0, max: 1}));
  }
  if (pv.foeDodged?.[i] && !o.idle) gfx('pvdodge', .47, () => pfx.push({type:'label', x: wolfX, y: g - msz - 16, text: 'Né!', col: '#9fd8ff', life: 0, max: .9}));
  if (pv.foeCasts?.[i] && !o.held && !o.last && big)
    gfx('pvcast', .78, () => pfx.push({type:'label', x: wolfX, y: g - msz - 14, text: pv.foeCasts[i], col: '#ffb38f', life: 0, max: 1.1}));
  if (pv.foeBurn?.[i]) gfx('pvburn', .8, () => pfx.push({type:'num', x: heroX + U(6), y: g - sz - 10, text: '-' + pv.foeBurn[i], col: '#ff7a28', life: 0, max: .9}));
  if (pv.foeHeal?.[i]) gfx('pvheal', .97, () => pfx.push({type:'num', x: wolfX + U(10), y: g - msz - 30, text: '+' + pv.foeHeal[i], col: HEAL_COL, life: 0, max: .8}));
}
