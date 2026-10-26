/* Realm cosmetics, independent of costume. No gameplay state, timers or assets. */
const SKIN_FX_THEMES = {
  2:{kind:'gold',color:'#e5bf61',light:'#fff0b0',accent:'#ffe29a'},
  3:{kind:'soul',color:'#bf92f3',light:'#f2ddff',accent:'#d9b4ff'},
  4:{kind:'divine',color:'#a9d9f0',light:'#ffffff',accent:'#d5f4ff'},
  5:{kind:'void',color:'#79aaff',light:'#d5eaff',accent:'#9bcaff'},
  6:{kind:'dragon',color:'#d58a54',light:'#fff0ca',accent:'#edbb68'},
  7:{kind:'dao',color:'#e6bf60',light:'#fff4cb',accent:'#f5db91'},
  8:{kind:'thunder',color:'#b583ff',light:'#eee0ff',accent:'#7dbaff'},
  9:{kind:'immortal',color:'#f3d17e',light:'#fff6d8',accent:'#8ae5d3'}
};
function skinFxTheme(_hero, realmIndex=typeof S!=='undefined' && S ? S.realm : -1){
  return SKIN_FX_THEMES[realmIndex] || null;
}

/** Coordinates are relative to the visible figure, in the caller's canvas units. */
function drawSkinFx(g, hero, layer, x, ground, height, time, action='idle', progress=0, reduced=false){
  const theme = skinFxTheme(hero);
  if (!theme || !Number.isFinite(height) || height <= 0) return false;
  const h=height, t=reduced ? 0 : time, tau=Math.PI*2;
  const casting=!reduced && (action==='cast'||action==='slash');
  const burst=casting ? Math.sin(Math.PI*Math.max(0,Math.min(1,progress))) : 0;
  const thunder=theme.kind==='thunder', immortal=theme.kind==='immortal';
  const cy=ground-h*.53, radius=h*(immortal?.36:.32);
  g.save();
  // Inherit hit flashes/fades from the caller without leaking state into sprites or other FX.
  g.translate(x,cy);
  g.globalAlpha *= reduced ? .24 : .48 + burst*.16;
  g.lineWidth=Math.max(.35,h*.005);
  g.strokeStyle=theme.color;g.fillStyle=theme.light;
  const stroke=draw=>{g.beginPath();draw();g.stroke();};
  const ring=(x,y,rx,ry,angle=0)=>stroke(()=>g.ellipse(x*h,y*h,rx*h,ry*h,angle,0,tau));
  const lotus=(x,y,r)=>{
    for(let i=0;i<7;i++){
      g.save();g.translate((x+(i-3)*r*.2)*h,y*h);g.rotate((i-3)*.29);
      stroke(()=>{g.moveTo(0,.02*h);g.quadraticCurveTo(-r*h*.32,-r*h*.3,0,-r*h);g.quadraticCurveTo(r*h*.32,-r*h*.3,0,.02*h);});g.restore();
    }
  };
  if(layer==='back'){
    const glow=g.createRadialGradient(0,0,h*.1,0,0,h*.6);
    glow.addColorStop(0,theme.color+'30');glow.addColorStop(1,theme.color+'00');
    g.fillStyle=glow;g.fillRect(-h*.6,-h*.6,h*1.2,h*1.2);
    if(theme.kind==='soul'){
      for(const side of [-1,1])stroke(()=>{
        g.moveTo(side*h*.22,h*.4);g.bezierCurveTo(side*h*(.44+.025*Math.sin(t)),h*.15,side*h*.16,-h*.2,side*h*.33,-h*.4);
      });
      if(action==='meditate'){
        // Abstract spirit, independent of the equipped costume.
        ring(.22,-.4,.045,.045);ring(.22,-.26,.13,.23);
        stroke(()=>{g.moveTo(h*.22,-h*.35);g.quadraticCurveTo(h*.08,-h*.15,h*.12,-h*.1);g.quadraticCurveTo(h*.22,-h*.04,h*.32,-h*.1);g.quadraticCurveTo(h*.36,-h*.15,h*.22,-h*.35);});
      }
    }else if(theme.kind==='divine'){
      ring(0,-.28,.25,.25);
      for(let i=0;i<6;i++){const a=i*tau/6+t*.14;g.beginPath();g.arc(Math.cos(a)*h*.32,(Math.sin(a)*.32-.16)*h,h*.006,0,tau);g.fill();}
    }else if(theme.kind==='void'){
      for(const side of [-1,1])ring(0,.02,.38,.17,side*.65+t*.06);
      for(let i=0;i<4;i++){
        const a=i*tau/4+t*.2;g.save();g.translate(Math.cos(a)*h*.37,Math.sin(a)*h*.32);g.rotate(a);
        stroke(()=>{g.moveTo(0,-h*.045);g.lineTo(h*.025,0);g.lineTo(0,h*.045);g.lineTo(-h*.025,0);g.closePath();});g.restore();
      }
    }else if(theme.kind==='dragon'){
      g.save();g.rotate(Math.sin(t*.24)*.12);g.strokeStyle=theme.accent;
      stroke(()=>{g.moveTo(-h*.22,h*.38);g.bezierCurveTo(-h*.58,h*.14,h*.48,h*.18,h*.28,-h*.22);g.bezierCurveTo(h*.13,-h*.5,-h*.27,-h*.48,-h*.32,-h*.28);});
      stroke(()=>{g.moveTo(-h*.32,-h*.28);g.lineTo(-h*.4,-h*.32);g.lineTo(-h*.35,-h*.34);g.lineTo(-h*.34,-h*.42);g.moveTo(-h*.32,-h*.28);g.quadraticCurveTo(-h*.49,-h*.22,-h*.47,-h*.13);});g.restore();
    }else if(theme.kind==='dao'){
      ring(0,-.05,.34,.34);ring(0,-.05,.39,.39);
      for(let i=0;i<8;i++){
        const a=i*tau/8+t*.13;g.save();g.translate(Math.cos(a)*h*.365,(Math.sin(a)*.365-.05)*h);g.rotate(a);
        stroke(()=>{g.moveTo(-h*.02,-h*.025);g.lineTo(h*.02,-h*.025);g.moveTo(0,-h*.025);g.lineTo(0,h*.025);g.moveTo(-h*.018,h*.012);g.lineTo(h*.018,h*.012);});g.restore();
      }
      for(let i=0;i<4;i++){const a=i*tau/4-t*.18;ring(Math.cos(a)*.39,Math.sin(a)*.39-.05,.008,.008);}
    }else if(immortal){
      // The crown stays visible inside a celestial ring; ornament strokes stay outside the face.
      stroke(()=>g.ellipse(0,-h*.12,radius,radius,0,0,tau));
      stroke(()=>g.ellipse(0,-h*.12,radius*1.13,radius*1.13,0,0,tau));
      for(let i=0;i<8;i++){
        const a=i*tau/8+t*.12, r=radius*1.065;
        g.save();g.translate(Math.cos(a)*r,Math.sin(a)*r-h*.12);g.rotate(a+Math.PI/4);
        g.fillStyle=i%2?theme.accent:theme.light;
        g.fillRect(-h*.009,-h*.009,h*.018,h*.018);g.restore();
      }
      g.strokeStyle=theme.accent;lotus(0,.49,action==='meditate'?.23:.13);
      for(const side of [-1,1])stroke(()=>{g.moveTo(side*h*.2,h*.35);g.bezierCurveTo(side*h*.45,h*.1,side*h*.22,-h*.1,side*h*.34,-h*.34);});
    }else if(thunder){
      // Two continuous, low-frequency arcs rather than strobing lightning flashes.
      for(const side of [-1,1]){
        stroke(()=>{
          for(let i=0;i<9;i++){
            const u=i/8, px=side*h*(.28+.045*Math.sin(i*2.3+t*1.4));
            const py=-h*.44+u*h*.86;
            if(i===0)g.moveTo(px,py);else g.lineTo(px,py);
          }
        });
      }
      g.strokeStyle=theme.accent;
      stroke(()=>g.ellipse(0,h*.47,h*.31,h*.065,0,0,tau));
      if(action==='meditate'){
        ring(0,-.05,.38,.38);
        for(let i=0;i<6;i++){const a=i*tau/6+t*.1,b=a+tau/6;stroke(()=>{g.moveTo(Math.cos(a)*h*.38,(Math.sin(a)*.38-.05)*h);g.lineTo(Math.cos(a+.3)*h*.3,(Math.sin(a+.3)*.3-.05)*h);g.lineTo(Math.cos(b)*h*.38,(Math.sin(b)*.38-.05)*h);});}
      }
    }else{
      stroke(()=>g.ellipse(0,h*.16,radius,h*.38,Math.sin(t*.2)*.16,0,tau));
    }
    if(['run','walk','groundRun'].includes(action)&&!reduced){
      if(theme.kind==='dao'||immortal){g.save();g.globalAlpha*=.55;lotus(-.55,.48,.12);g.restore();}
      g.strokeStyle=theme.accent;
      for(let i=0;i<3;i++)stroke(()=>{
        const y=h*(.2+i*.075);
        g.moveTo(-h*(.3+i*.07),y);
        g.quadraticCurveTo(-h*.75,y+h*.03,-h*(.9+i*.08),y-h*.02);
      });
    }
  }else if(layer==='front'){
    if(action==='meditate'&&theme.kind==='gold'){
      const glow=g.createRadialGradient(0,h*.1,h*.008,0,h*.1,h*.1);
      glow.addColorStop(0,'#fff9dc');glow.addColorStop(.3,'#f4cf76aa');glow.addColorStop(1,'#e5bf6100');
      g.fillStyle=glow;g.fillRect(-h*.1,0,h*.2,h*.2);
      g.fillStyle=theme.light;g.beginPath();g.arc(0,h*.1,h*(.025+.002*Math.sin(t*1.5)),0,tau);g.fill();
    }
    const count=reduced?2:height<64?4:8;
    for(let i=0;i<count;i++){
      const a=i*tau/count+t*(thunder?.23:.13), r=h*(.3+.035*Math.sin(t+i));
      const px=Math.cos(a)*r, py=theme.kind==='gold'?h*(.4-((i/count+t*.065)%1)*.8):Math.sin(a)*h*.38;
      // Keep particles on the silhouette's perimeter, away from the face and hands.
      if(Math.abs(px)<h*.18&&py<0)continue;
      g.fillStyle=i%2?theme.accent:theme.light;
      const s=Math.max(.35,h*(immortal?.007:.005));
      if(immortal){g.save();g.translate(px,py);g.rotate(i+t*.1);stroke(()=>g.ellipse(0,0,s,s*2,.4,0,tau));g.restore();}
      else {g.beginPath();g.arc(px,py,s,0,tau);g.fill();}
    }
    if(burst>0){
      g.globalAlpha*=burst;g.strokeStyle=theme.light;
      const hx=h*.25, hy=-h*.02, r=h*(.035+.055*burst);
      if(immortal){g.save();g.translate(hx,hy);lotus(0,.04,.1+.045*burst);g.restore();}
      else if(thunder)stroke(()=>{g.moveTo(hx-h*.06,hy-h*.08);g.lineTo(hx+h*.015,hy-h*.025);g.lineTo(hx-h*.015,hy+h*.025);g.lineTo(hx+h*.07,hy+h*.09);});
      else stroke(()=>g.ellipse(hx,hy,r,r*.7,-.35,0,tau));
    }
  }
  g.restore();return true;
}
