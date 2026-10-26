/* Cosmetic skin layers. No gameplay state, random animation, timers or image assets. */
const SKIN_FX_THEMES = {
  thunder:{kind:'thunder',color:'#b583ff',light:'#eee0ff',accent:'#7dbaff'},
  immortal:{kind:'immortal',color:'#f3d17e',light:'#fff6d8',accent:'#8ae5d3'},
  luyenkhi:{kind:'spirit',color:'#82b6bc',light:'#d3f1eb',accent:'#d3f1eb'},
  trucco:{kind:'spirit',color:'#55bcd7',light:'#b2fff3',accent:'#b2fff3'},
  kimdan:{kind:'spirit',color:'#e5bf61',light:'#fff0b0',accent:'#fff0b0'},
  nguyenanh:{kind:'spirit',color:'#bf92f3',light:'#f2ddff',accent:'#f2ddff'},
  hoathan:{kind:'spirit',color:'#b6d8ee',light:'#ffffff',accent:'#ffffff'},
  luyenhu:{kind:'spirit',color:'#79aaff',light:'#d5eaff',accent:'#d5eaff'},
  hopthe:{kind:'spirit',color:'#e4c174',light:'#fff0ca',accent:'#fff0ca'}
};
function skinFxTheme(hero){
  if (typeof hero !== 'string') return null;
  if (/hero_dokiep_(nam|nu)\.png$/.test(hero)) return SKIN_FX_THEMES.thunder;
  if (/hero_chantien_(nam|nu)\.png$/.test(hero)) return SKIN_FX_THEMES.immortal;
  const id=/hero_(luyenkhi|trucco|kimdan|nguyenanh|hoathan|luyenhu|hopthe)(?:_nu)?\.png$/.exec(hero)?.[1];
  return SKIN_FX_THEMES[id] || null;
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
  if(layer==='back'){
    const glow=g.createRadialGradient(0,0,h*.1,0,0,h*.6);
    glow.addColorStop(0,theme.color+'30');glow.addColorStop(1,theme.color+'00');
    g.fillStyle=glow;g.fillRect(-h*.6,-h*.6,h*1.2,h*1.2);
    if(immortal){
      // The crown stays visible inside a celestial ring; ornament strokes stay outside the face.
      stroke(()=>g.ellipse(0,-h*.12,radius,radius,0,0,tau));
      stroke(()=>g.ellipse(0,-h*.12,radius*1.13,radius*1.13,0,0,tau));
      for(let i=0;i<8;i++){
        const a=i*tau/8+t*.12, r=radius*1.065;
        g.save();g.translate(Math.cos(a)*r,Math.sin(a)*r-h*.12);g.rotate(a+Math.PI/4);
        g.fillStyle=i%2?theme.accent:theme.light;
        g.fillRect(-h*.009,-h*.009,h*.018,h*.018);g.restore();
      }
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
    }else{
      stroke(()=>g.ellipse(0,h*.16,radius,h*.38,Math.sin(t*.2)*.16,0,tau));
    }
    if(action==='run'&&!reduced){
      g.strokeStyle=theme.accent;
      for(let i=0;i<3;i++)stroke(()=>{
        const y=h*(.2+i*.075);
        g.moveTo(-h*(.3+i*.07),y);
        g.quadraticCurveTo(-h*.75,y+h*.03,-h*(.9+i*.08),y-h*.02);
      });
    }
  }else if(layer==='front'){
    const count=reduced?2:height<64?4:8;
    for(let i=0;i<count;i++){
      const a=i*tau/count+t*(thunder?.23:.13), r=h*(.3+.035*Math.sin(t+i));
      const px=Math.cos(a)*r, py=Math.sin(a)*h*.38;
      // Keep particles on the silhouette's perimeter, away from the face and hands.
      if(Math.abs(px)<h*.18&&py<0)continue;
      g.fillStyle=i%2?theme.accent:theme.light;
      const s=Math.max(.35,h*(immortal?.007:.005));
      g.beginPath();g.arc(px,py,s,0,tau);g.fill();
    }
    if(burst>0){
      g.globalAlpha*=burst;g.strokeStyle=theme.light;
      const hx=h*.25, hy=-h*.02, r=h*(.035+.055*burst);
      stroke(()=>g.ellipse(hx,hy,r,r*.7,-.35,0,tau));
    }
  }
  g.restore();return true;
}
