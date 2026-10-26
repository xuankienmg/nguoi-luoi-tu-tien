/** Server HP is after recovery; reveal recovery at end-turn, including held/killing turns. */
function battleHeroHpAt(battle,phase,before,after,killing,held){
  if(phase.s>=.95)return Math.max(0,after);
  if(phase.s>=.85&&!killing&&!held)return Math.max(0,after-(battle.heal?.[phase.i]||0));
  return Math.max(0,before);
}
/* Chỉ hiện chữ né khi server xác nhận; không gieo lại xác suất ở client. */
function battleDodgeFeedback(battle,phase,seen,emit,x,y){
  if(phase.name!=='fight'||phase.s<.85||battle.dodged?.[phase.i]!==true||battle.monSkip?.[phase.i])return false;
  const key=`${phase.trip}:dodge:${phase.i}`;
  if(seen[key])return false;
  seen[key]=1;emit({type:'dodge',x,y,text:'Né tránh',life:0,max:.9});return true;
}
function drawBattleDodgeLabel(ctx,label,options){
  const {sx,sy,width,reduced,big}=options,k=Math.max(0,Math.min(1,label.life/label.max));
  ctx.save();
  try{
    ctx.globalAlpha=reduced?1:Math.min(1,k/.08)*Math.min(1,(1-k)/.25);
    ctx.font=`700 ${big?16:12}px "Be Vietnam Pro",system-ui,sans-serif`;
    ctx.textAlign='center';ctx.textBaseline='alphabetic';ctx.lineJoin='round';
    const half=ctx.measureText(label.text).width/2+3,x=Math.max(half,Math.min(width-half,label.x*sx)),y=label.y*sy-(reduced?0:k*14);
    ctx.lineWidth=3;ctx.strokeStyle='#132326';ctx.fillStyle=label.col||'#b3e9e3';
    ctx.strokeText(label.text,x,y);ctx.fillText(label.text,x,y);
  }finally{ctx.restore()}
}
function battleDivineFeedback(battle,phase,seen,emit,x,y){
  if(phase.name!=='fight')return;
  const turn=battle.divine?.turns?.[phase.i];if(!turn)return;
  const labels=[['boosted',.45,`Thiên cơ +${Math.round(battle.divine?.boostPct||100)}%`,'#ead09a'],['nullified',.85,'Vô hiệu hóa','#a6dacc'],['saved',.85,'Bất tử','#f0bd82']];
  for(const [flag,at,text,col] of labels){
    const key=`${phase.trip}:divine:${flag}:${phase.i}`;
    if(!turn[flag]||phase.s<at||seen[key])continue;
    seen[key]=1;emit({type:'dodge',x,y,text,col,life:0,max:1});
  }
}
function drawBattleDivine(ctx,battle,phase,width,height,big){
  const ability=battle.divine;if(!ability?.tier)return;
  const turn=ability.turns?.[phase.i||0];
  const charged=phase.name==='fight'&&(phase.s>=.85 ? turn?.charged :
    (phase.i>0 && ability.turns?.[phase.i-1]?.charged && !(phase.s>=.45&&turn?.boosted)));
  ctx.save();
  try{
    const y=height-9,col=charged?'#f2dc94':'#afc5b5';
    ctx.strokeStyle=col;ctx.fillStyle='#152420';ctx.lineWidth=1;
    ctx.beginPath();ctx.moveTo(13,y-10);ctx.lineTo(19,y-4);ctx.lineTo(13,y+2);ctx.lineTo(7,y-4);ctx.closePath();ctx.fill();ctx.stroke();
    ctx.font=`600 ${big?11:9}px "Be Vietnam Pro",system-ui,sans-serif`;ctx.textAlign='left';ctx.textBaseline='alphabetic';ctx.fillStyle=col;
    const quality={6:'Thần',7:'Tiên',8:'Đạo',9:'Huyền Tiên'}[ability.tier];
    const text=(big?ability.name:quality)+(charged?' · Sẵn sàng':'');
    ctx.fillText(text,24,y,Math.max(20,width-32));
  }finally{ctx.restore()}
}
/** Khống chế quái (server gửi MonControl / Shatter theo lượt): tên, màu nhãn. */
const BATTLE_CONTROL = {
  paralyze: {name:'Tê liệt', col:'#d4adff'},
  stun: {name:'Choáng', col:'#f3d27a'},
  freeze: {name:'Đóng băng', col:'#9fe3ff'},
};
/** Loại khống đang hiện trên quái ở pha này: từ lúc linh thú thi triển tới hết lượt; lượt sau đóng băng giữ băng tới khi đòn đánh vỡ. */
function battleControlAt(battle,phase){
  if(!battle||!['fight','pet'].includes(phase.name)||!(battle.monHpLeft?.[phase.i]>0)&&phase.name!=='pet')return '';
  const kind=battle.monControl?.[phase.i]||'';
  if(kind){
    const ownerCast=(battle.casts||[]).some(c=>c.turn===phase.i&&c.secondary===kind);
    if(ownerCast)return phase.name==='pet'||phase.rep||phase.s>=.45?kind:'';
    const petCast=(battle.pet?.casts||[]).some(c=>c.turn===phase.i&&c.paralyzeTurns>0&&!(c.at>=.85));
    if(!petCast)return phase.name==='fight'?kind:'';          // khống từ lượt trước / kỹ năng: cả lượt
    if(phase.name==='pet')return phase.s>=.5?kind:'';
    return phase.s>=.7&&!phase.rep?kind:'';
  }
  if(battle.shatter?.[phase.i]&&phase.name==='fight'&&phase.s<.4)return 'freeze';   // băng còn bám tới khi đòn lượt này đánh vỡ
  return '';
}
const BATTLE_SECONDARY = {
  weaken:{text:'Suy yếu −20% công',col:'#79c7ef',target:'monster'},
  heal:{text:'Hồi phục',col:'#8bdea0',target:'hero'},
  stun:{text:'Choáng',col:'#f3d27a',target:'monster'},
  paralyze:{text:'Tê liệt',col:'#d4adff',target:'monster'},
  freeze:{text:'Đóng băng',col:'#9fe3ff',target:'monster'},
  wind:{text:'Phong hộ +20% né',col:'#a3e5cd',target:'hero'},
};
function battleSecondaryStatus(battle,phase,kind){
  if(!['fight','pet'].includes(phase.name))return false;
  const active=kind==='wind'?battle.windGuard?.[phase.i]:battle.weakened?.[phase.i];
  if(!active)return false;
  const started=(battle.casts||[]).some(c=>c.turn===phase.i&&c.secondary===kind);
  return !started||phase.name==='pet'||phase.rep||phase.s>=.45;
}
function battleSecondaryFeedback(battle,phase,seen,emit,hx,hy,tx,ty){
  if(phase.name!=='fight'||phase.rep||phase.s<.45)return;
  for(const cast of battle.casts||[]){
    if(cast.turn!==phase.i)continue;
    const effect=BATTLE_SECONDARY[cast.secondary],key=`${phase.trip}:secondary:${phase.i}:${cast.skill}`;
    if(!effect||seen[key])continue;
    seen[key]=1;emit({type:'label',x:effect.target==='hero'?hx:tx,y:effect.target==='hero'?hy:ty,text:effect.text,col:cast.secondary==='heal'&&typeof HEAL_COL!=='undefined'?HEAL_COL:effect.col,life:0,max:1});
  }
}
/** Chữ "Băng vỡ" khi đòn đánh trúng quái đang đóng băng (server đã cộng thêm sát thương). */
function battleControlFeedback(battle,phase,seen,emit,x,y){
  if(phase.name!=='fight'||phase.s<.4||!battle.shatter?.[phase.i])return;
  const key=`${phase.trip}:shatter:${phase.i}`;
  if(seen[key])return;
  seen[key]=1;emit({type:'dodge',x,y,text:'Băng vỡ +10%',col:BATTLE_CONTROL.freeze.col,life:0,max:1});
}
/** Dấu trạng thái nét cao: hình dạng riêng, tâm nhân vật luôn thoáng. */
function drawBattleStatus(ctx,{kind,x,y,width,height,strength=1,time=0,reduced=false}){
  if(width<=0||height<=0)return;
  ctx.save();
  try{
    const rx=width*.54,ry=height*.54,pulse=reduced?1:.94+.06*Math.sin(time*2);
    ctx.lineWidth=Math.max(1.2,Math.min(2,width/45));ctx.lineJoin='round';ctx.lineCap='round';
    const diamond=(cx,cy,r)=>{ctx.beginPath();ctx.moveTo(cx,cy-r);ctx.lineTo(cx+r,cy);ctx.lineTo(cx,cy+r);ctx.lineTo(cx-r,cy);ctx.closePath();ctx.fill();ctx.stroke()};
    if(kind==='target'){
      ctx.strokeStyle='#dd8276';ctx.globalAlpha=.7;
      for(const [a,b] of [[.12,1.15],[2,3.03],[3.25,4.28],[5.15,6.18]]){ctx.beginPath();ctx.ellipse(x,y,rx,height*.09,0,a,b);ctx.stroke()}
      return;
    }
    if(kind==='shield'||kind==='ward'){
      ctx.globalAlpha=(.35+.45*Math.max(0,Math.min(1,strength)))*pulse;
      ctx.strokeStyle='#8ad9eb';ctx.fillStyle='rgba(88,177,199,.055)';
      ctx.beginPath();ctx.moveTo(x,y-ry);ctx.lineTo(x+rx*.85,y-ry*.65);ctx.lineTo(x+rx*.8,y+ry*.25);ctx.quadraticCurveTo(x+rx*.4,y+ry*.8,x,y+ry);ctx.quadraticCurveTo(x-rx*.4,y+ry*.8,x-rx*.8,y+ry*.25);ctx.lineTo(x-rx*.85,y-ry*.65);ctx.closePath();ctx.fill();ctx.stroke();
      // Hai cạnh trong gợi vỏ khiên, không phủ lên mặt nhân vật.
      for(const sign of [-1,1]){ctx.beginPath();ctx.moveTo(x+sign*rx*.69,y-ry*.5);ctx.lineTo(x+sign*rx*.65,y+ry*.15);ctx.stroke()}
      return;
    }
    if(kind==='paralyze'){
      // Tia điện tím chớp quanh thân, đổi vị trí theo nhịp
      ctx.strokeStyle='#d4adff';ctx.shadowColor='#b883ff';ctx.shadowBlur=reduced?0:6;
      const beat=reduced?0:Math.floor(time*8)%3;
      for(let i=0;i<3;i++){
        const a=(i/3)*Math.PI*2+beat*.7,cx=x+Math.cos(a)*rx*.92,cy=y+Math.sin(a)*ry*.8;
        ctx.globalAlpha=reduced?.8:.55+.45*((i+beat)%2);
        ctx.beginPath();ctx.moveTo(cx-5,cy-7);ctx.lineTo(cx+1,cy-1);ctx.lineTo(cx-2,cy+1);ctx.lineTo(cx+5,cy+7);ctx.stroke();
      }
      return;
    }
    if(kind==='weaken'){
      ctx.strokeStyle='#79c7ef';ctx.globalAlpha=.85;
      for(const sign of [-1,1]){const cx=x+sign*rx*.85;ctx.beginPath();ctx.moveTo(cx,y-ry*.45);ctx.lineTo(cx,y+ry*.25);ctx.moveTo(cx-4,y+ry*.1);ctx.lineTo(cx,y+ry*.3);ctx.lineTo(cx+4,y+ry*.1);ctx.stroke()}
      return;
    }
    if(kind==='stun'){
      // Ba ngôi sao vàng xoay trên đầu
      ctx.fillStyle='#f3d27a';ctx.strokeStyle='#7a5a1c';ctx.lineWidth=1;
      const spin=reduced?0:time*3,top=y-ry*.62;                  // ngay trên đầu, dưới thanh máu
      const star=(cx,cy,r)=>{ctx.beginPath();for(let k=0;k<10;k++){const a=-Math.PI/2+k*Math.PI/5,rr=k%2?r*.45:r;ctx.lineTo(cx+Math.cos(a)*rr,cy+Math.sin(a)*rr)}ctx.closePath();ctx.fill();ctx.stroke()};
      for(let i=0;i<3;i++){
        const a=spin+i*Math.PI*2/3,depth=(Math.sin(a)+1)/2;
        ctx.globalAlpha=.55+.45*depth;star(x+Math.cos(a)*rx*.55,top+Math.sin(a)*ry*.16,Math.max(5.5,width*.12)*(.65+.35*depth));
      }
      return;
    }
    if(kind==='freeze'){
      // Lớp băng xanh mờ + tinh thể ở góc, mặt quái vẫn thấy rõ
      ctx.globalAlpha=.22*pulse;ctx.fillStyle='#bfefff';
      ctx.beginPath();ctx.ellipse(x,y,rx*.95,ry*.95,0,0,Math.PI*2);ctx.fill();
      ctx.globalAlpha=.9;ctx.strokeStyle='#9fe3ff';ctx.fillStyle='#e8fbff';
      const crystal=(cx,cy,r)=>{ctx.beginPath();ctx.moveTo(cx,cy-r);ctx.lineTo(cx+r*.45,cy);ctx.lineTo(cx,cy+r);ctx.lineTo(cx-r*.45,cy);ctx.closePath();ctx.fill();ctx.stroke()};
      crystal(x-rx*.85,y+ry*.55,ry*.32);crystal(x-rx*.62,y+ry*.72,ry*.2);crystal(x+rx*.82,y+ry*.5,ry*.28);crystal(x+rx*.55,y-ry*.7,ry*.18);
      return;
    }
    if(kind==='control'){
      ctx.strokeStyle='#c4b8ed';ctx.fillStyle='#172135';
      // Hai gông khóa ở hai bên, kèm khóa nhỏ gần chân.
      for(const sign of [-1,1]){ctx.beginPath();ctx.moveTo(x+sign*rx*.65,y-ry*.5);ctx.lineTo(x+sign*rx,y-ry*.5);ctx.lineTo(x+sign*rx,y+ry*.55);ctx.lineTo(x+sign*rx*.65,y+ry*.55);ctx.stroke()}
      ctx.beginPath();ctx.arc(x,y+ry*.55,3,Math.PI,0);ctx.stroke();ctx.fillRect(x-4,y+ry*.55,8,6);ctx.strokeRect(x-4,y+ry*.55,8,6);return;
    }
    if(kind==='elite'){
      ctx.strokeStyle='#bc9be8';ctx.fillStyle='#352849';ctx.globalAlpha=.8*pulse;
      for(const sign of [-1,1]){diamond(x+sign*rx,y-ry*.2,3);ctx.beginPath();ctx.moveTo(x+sign*rx*.83,y+ry*.15);ctx.lineTo(x+sign*rx*.94,y+ry*.55);ctx.lineTo(x+sign*rx*.65,y+ry*.75);ctx.stroke()}
      return;
    }
    if(kind==='enrage'){
      ctx.strokeStyle='#eb7a56';ctx.globalAlpha=.85*pulse;
      for(const sign of [-1,1]){ctx.beginPath();ctx.moveTo(x+sign*rx*.75,y+ry*.8);ctx.lineTo(x+sign*rx,y+ry*.15);ctx.lineTo(x+sign*rx*.8,y+ry*.35);ctx.lineTo(x+sign*rx*.88,y-ry*.35);ctx.stroke()}
      return;
    }
    if(kind==='dash'){
      ctx.strokeStyle='#a4d5b8';ctx.globalAlpha=.7;
      for(let i=0;i<3;i++){ctx.beginPath();ctx.moveTo(x-rx*(1+i*.16),y+ry*(.2+i*.2));ctx.lineTo(x-rx*.45,y+ry*(.1+i*.2));ctx.stroke()}
    }
  }finally{ctx.restore()}
}
