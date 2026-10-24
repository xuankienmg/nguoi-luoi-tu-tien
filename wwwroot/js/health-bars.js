/* HP vẽ ở độ phân giải thật, độc lập với lưới pixel của nhân vật. */
const battleHpTrails = new Map();
function battleHpValue(value){ return Number.isFinite(value) ? Math.max(0,value) : 0; }
/** GuardLeft là số khiên cuối lượt; chỉ đổi khi quái ra đòn ở mốc .85. */
function battleGuardAt(battle,phase,waveStart=0){
  if(phase.name==='pet')phase={...phase,name:'fight',s:0};
  const initial=battleHpValue(battle.guard),history=battle.guardLeft;
  if(phase.name==='fight'&&phase.s<.85&&Number.isFinite(battle.guardBefore?.[phase.i||0]))
    return battleHpValue(battle.guardBefore[phase.i||0]);
  let index=-1;
  if(phase.name==='fight')index=Math.max(0,phase.i||0)-(phase.s>=.85?0:1);
  else if(phase.name==='enter')index=Math.max(0,waveStart)-1;
  else if(phase.name==='win'||phase.name==='lose')index=(history?.length||0)-1;
  if(index<0)return initial;
  return battleHpValue(history?.[Math.min(index,(history?.length||0)-1)]);
}
function battleHpText(value){
  const n=Math.round(battleHpValue(value));
  return fmtCompact(n);
}
function battleHpState(actor,key,current,max,dt,reducedMotion=false){
  max=battleHpValue(max);current=Math.min(max,battleHpValue(current));
  const fraction=max>0?current/max:0;
  let state=battleHpTrails.get(actor);
  if(!state||state.key!==key||state.max!==max){state={key,max,fraction,ghost:fraction,delay:0};battleHpTrails.set(actor,state)}
  if(reducedMotion||fraction>state.fraction){state.ghost=fraction;state.delay=0}
  else if(fraction<state.fraction){state.ghost=Math.max(state.ghost,state.fraction);state.delay=.28}
  const step=Math.max(0,Math.min(.1,Number.isFinite(dt)?dt:0));
  if(state.delay>0)state.delay=Math.max(0,state.delay-step);
  else state.ghost=Math.max(fraction,state.ghost-step*.85);
  state.fraction=fraction;
  return {current,max,fraction,ghost:Math.max(fraction,Math.min(1,state.ghost))};
}
function drawBattleHp(ctx,options){
  const {x,y,width,state,name,kind='hero',rage=null,compact=true}=options;
  const viewport=options.viewportWidth||ctx.canvas.width,barW=Math.min(width,viewport-8);
  const left=Math.max(4,Math.min(viewport-barW-4,x-barW/2)),top=Math.max(16,y),barH=compact?11:14;
  const low=state.fraction>0&&state.fraction<=.25;
  const color=kind==='hero'?(low?'#e98461':state.fraction<=.5?'#d4b464':'#75c4a2'):kind==='elite'?'#bc82d0':'#d86f76';
  const border=kind==='boss'?'#d2ac65':kind==='elite'?'#9471ab':low?'#c48b66':'#627d75';
  const shield=kind==='hero'&&state.current>0?battleHpValue(options.shield):0;
  ctx.save();
  try{
    ctx.textBaseline='alphabetic';ctx.textAlign='left';ctx.font=`600 ${compact?10:12}px "Be Vietnam Pro",system-ui,sans-serif`;
    const shieldLabel=shield>0?'+'+battleHpText(shield):'';
    const control=options.control&&typeof BATTLE_CONTROL!=='undefined'?BATTLE_CONTROL[options.control]:null;
    ctx.font=`700 ${compact?9:10}px "Be Vietnam Pro",system-ui,sans-serif`;
    const controlW=control?ctx.measureText(control.name).width+12:0;
    ctx.font=`600 ${compact?10:12}px "Be Vietnam Pro",system-ui,sans-serif`;
    const shieldSpace=shield>0?ctx.measureText(shieldLabel).width+16:control?controlW+4:0;
    let title=name;
    while(title.length>1&&ctx.measureText(title).width>barW-shieldSpace-2)title=title.slice(0,-1);
    if(title!==name)title=title.slice(0,-1)+'…';
    ctx.lineWidth=3;ctx.strokeStyle='#10191d';ctx.strokeText(title,left,top-4);
    ctx.fillStyle=kind==='boss'?'#edcc87':kind==='elite'?'#dcb1eb':'#e1e9db';ctx.fillText(title,left,top-4);
    if(control){
      // Nhãn khống (Tê liệt · Choáng · Đóng băng) bên phải tên quái
      const pillH=compact?12:14,pillX=left+barW-controlW,pillY=top-4-pillH+3;
      ctx.fillStyle='#10191dd9';ctx.fillRect(pillX,pillY,controlW,pillH);
      ctx.strokeStyle=control.col;ctx.lineWidth=1;ctx.strokeRect(pillX+.5,pillY+.5,controlW-1,pillH-1);
      ctx.font=`700 ${compact?9:10}px "Be Vietnam Pro",system-ui,sans-serif`;ctx.textAlign='center';ctx.fillStyle=control.col;
      ctx.fillText(control.name,pillX+controlW/2,pillY+pillH-3.5);
      ctx.font=`600 ${compact?10:12}px "Be Vietnam Pro",system-ui,sans-serif`;ctx.textAlign='left';
    }
    if(shield>0){
      ctx.textAlign='right';ctx.fillStyle='#a7deef';ctx.strokeText(shieldLabel,left+barW,top-4);ctx.fillText(shieldLabel,left+barW,top-4);
      const iconX=left+barW-ctx.measureText(shieldLabel).width-11,iconY=top-12;
      ctx.strokeStyle='#a7deef';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(iconX,iconY);ctx.lineTo(iconX+7,iconY);ctx.lineTo(iconX+7,iconY+4);ctx.lineTo(iconX+3.5,iconY+8);ctx.lineTo(iconX,iconY+4);ctx.closePath();ctx.stroke();
    }
    ctx.fillStyle='#101a1e';ctx.fillRect(left-1,top-1,barW+2,barH+2);
    ctx.strokeStyle=border;ctx.lineWidth=1;ctx.strokeRect(left-.5,top-.5,barW+1,barH+1);
    if(state.ghost>state.fraction){ctx.fillStyle='#d1ac83';ctx.fillRect(left,top,barW*state.ghost,barH)}
    if(state.fraction>0){ctx.fillStyle=color;ctx.fillRect(left,top,Math.max(1,barW*state.fraction),barH);ctx.fillStyle='#ffffff25';ctx.fillRect(left,top,barW*state.fraction,2)}
    if(shield>0&&state.max>0){
      // Lớp xanh nằm ngay trên HP; chiều dài tương ứng khiên / HP tối đa.
      const shieldWidth=barW*Math.min(1,shield/state.max);
      ctx.fillStyle='#79c9e8';ctx.fillRect(left,top,Math.max(1,shieldWidth),3);
    }
    const hp=`${battleHpText(state.current)} / ${battleHpText(state.max)}`;
    ctx.font=`700 ${compact?9:11}px "Be Vietnam Pro",system-ui,sans-serif`;ctx.textAlign='center';
    ctx.lineWidth=2;ctx.strokeStyle='#102021';ctx.fillStyle='#fff7e5';ctx.strokeText(hp,left+barW/2,top+barH-3);ctx.fillText(hp,left+barW/2,top+barH-3);
    if(rage!==null){const fraction=Math.max(0,Math.min(1,Number.isFinite(rage)?rage:0));ctx.fillStyle='#1d252b';ctx.fillRect(left,top+barH+3,barW,3);ctx.fillStyle=fraction>=1?'#ffe2a0':'#cda35b';ctx.fillRect(left,top+barH+3,barW*fraction,3)}
    if(options.burn!==null&&Number.isFinite(options.burn)){
      ctx.font=`600 ${compact?9:10}px "Be Vietnam Pro",system-ui,sans-serif`;ctx.textAlign='right';ctx.strokeStyle='#10191d';ctx.lineWidth=3;ctx.fillStyle='#ffba75';
      const label=`Cháy ${Math.round(options.burn)}%`;ctx.strokeText(label,left+barW,top+barH+12);ctx.fillText(label,left+barW,top+barH+12);
    }
  }finally{ctx.restore()}
  return {left,top,width:barW,height:barH+(rage!==null?6:0)};
}
