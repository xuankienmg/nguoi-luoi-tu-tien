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

/** Inventory-only realm backdrop. All ornaments are behind the avatar;
 * progression follows the realm, independently of the equipped costume. */
function drawInventoryRealmFx(g, realmIndex, time, reduced=false){
  const tier=Math.max(0,Math.min(9,Math.floor(Number(realmIndex)||0)));
  const palettes=[
    ['#9fb8ca','#dcebf1'],['#63cbb9','#caffed'],['#e4b75b','#fff0ba'],
    ['#b68cde','#ead4ff'],['#a9dcf2','#f0fbff'],['#738fe9','#c9ddff'],
    ['#e4aa65','#ffe5ac'],['#e9c568','#fff3ce'],['#b08aff','#e2d6ff'],['#edd58d','#fff9e6']
  ];
  const [color,light]=palettes[tier],tau=Math.PI*2,t=reduced?0:(Number.isFinite(time)?time:0);
  const cx=160,cy=104+Math.min(tier,4)*10,r=62+tier*7.5;
  const breath=reduced?1:.95+.05*Math.sin(t*.9);
  g.save();
  try{
    g.setTransform(1,0,0,1,0,0);g.globalAlpha=1;
    const field=g.createRadialGradient(160,158,12,160,158,170);
    field.addColorStop(0,color+(tier<2?'22':'38'));
    field.addColorStop(.52,color+'14');field.addColorStop(1,color+'00');
    g.fillStyle=field;g.fillRect(0,0,320,320);
    g.globalCompositeOperation='lighter';g.lineCap='round';
    const path=(draw,alpha=.6,width=1)=>{
      g.globalAlpha=alpha*breath;g.lineWidth=width;g.strokeStyle=color;
      g.beginPath();draw();g.stroke();
    };
    const ring=(x,y,rx,ry,angle=0,alpha=.5,width=1)=>path(()=>g.ellipse(x,y,rx,ry,angle,0,tau),alpha,width);
    const mote=(x,y,size=1.3,alpha=.7)=>{
      g.globalAlpha=alpha;g.fillStyle=light;g.beginPath();g.arc(x,y,size,0,tau);g.fill();
    };
    const sigil=(x,y,angle,size=6)=>{
      g.save();g.translate(x,y);g.rotate(angle);
      path(()=>{g.moveTo(0,-size);g.lineTo(size*.5,0);g.lineTo(0,size);g.lineTo(-size*.5,0);g.closePath();},.72);
      path(()=>{g.moveTo(-size*.7,-size*.65);g.lineTo(size*.7,-size*.65);},.45,.7);
      g.restore();
    };
    // A quiet head halo at entry level becomes a full-body wheel at high realms.
    ring(cx,cy,r,r,0,tier===0?.33:.55,tier<3?.8:1.15);
    if(tier>=1){
      ring(160,296,65+tier*5,10+tier*1.4,0,.3+tier*.025,.8);
      if(tier>=4)ring(160,296,76+tier*5,13+tier*1.5,0,.2,.7);
    }
    if(tier>=2)ring(cx,cy,r-7,r-7,0,.19,.65);
    if(tier===1){
      for(let i=0;i<4;i++){
        const a=i*tau/4+t*.07;sigil(cx+Math.cos(a)*r,cy+Math.sin(a)*r,a,4);
      }
    }else if(tier===2){
      const core=g.createRadialGradient(cx,cy,1,cx,cy,42);
      core.addColorStop(0,'#fff4cf80');core.addColorStop(.25,'#efc36345');core.addColorStop(1,'#e4b75b00');
      g.globalAlpha=.75;g.fillStyle=core;g.fillRect(cx-42,cy-42,84,84);
      ring(cx,cy,22,22,0,.72);ring(cx,cy,31,11,-t*.18,.55);
      for(let i=0;i<6;i++){const a=i*tau/6+t*.08;mote(cx+Math.cos(a)*r,cy+Math.sin(a)*r,1.4,.8);}
    }else if(tier===3){
      for(const side of [-1,1]){
        path(()=>{g.moveTo(cx+side*65,284);g.bezierCurveTo(cx+side*140,230,cx+side*80,132,cx+side*(80+4*Math.sin(t*.7)),65);},.55,1.5);
        path(()=>{g.moveTo(cx+side*81,270);g.bezierCurveTo(cx+side*114,207,cx+side*121,137,cx+side*77,82);},.23,.7);
      }
      for(let i=0;i<4;i++){const a=i*tau/4+t*.08;sigil(cx+Math.cos(a)*r,cy+Math.sin(a)*r,a,6);}
    }else if(tier===4){
      // Six spirit swords point inward from outside the figure's silhouette.
      for(let i=0;i<6;i++){
        const a=i*tau/6-Math.PI/2+t*.035;
        g.save();g.translate(cx+Math.cos(a)*(r+8),cy+Math.sin(a)*(r+8));g.rotate(a+Math.PI/2);
        path(()=>{g.moveTo(0,-13);g.lineTo(4,3);g.lineTo(0,8);g.lineTo(-4,3);g.closePath();},.8);
        path(()=>{g.moveTo(-6,8);g.lineTo(6,8);g.moveTo(0,8);g.lineTo(0,15);},.55,.8);
        g.restore();
      }
    }else if(tier===5){
      for(const side of [-1,1])ring(cx,cy+16,r+6,r*.43,side*.6+t*.035,.6,1.2);
      for(let i=0;i<6;i++){
        const a=i*tau/6-t*.065;sigil(cx+Math.cos(a)*(r+9),cy+Math.sin(a)*(r+9),a,7);
      }
    }else if(tier===6){
      // Two entwined golden currents suggest a coiling dragon without a face overlay.
      for(const side of [-1,1]){
        path(()=>{g.moveTo(cx+side*39,285);g.bezierCurveTo(cx+side*145,255,cx+side*147,164,cx+side*95,100);g.bezierCurveTo(cx+side*46,37,cx+side*111,21,cx+side*119,63);},.78,2);
        path(()=>{g.moveTo(cx+side*116,61);g.lineTo(cx+side*128,51);g.lineTo(cx+side*118,44);g.moveTo(cx+side*116,61);g.lineTo(cx+side*105,66);},.7,1.2);
        for(let i=0;i<8;i++){
          const a=.25+i*.17,x=cx+side*(90+25*Math.sin(a*2)),y=91+i*21;
          path(()=>{g.moveTo(x-side*4,y-4);g.lineTo(x+side*3,y);g.lineTo(x-side*4,y+4);},.35,.8);
        }
      }
    }else if(tier===7){
      ring(cx,cy,r+8,r+8,0,.47,.85);
      for(let i=0;i<12;i++){
        const a=i*tau/12+t*.035;sigil(cx+Math.cos(a)*(r-1),cy+Math.sin(a)*(r-1),a+Math.PI/2,7);
      }
      for(let i=0;i<8;i++){
        const a=i*tau/8-t*.02;
        path(()=>{g.moveTo(cx+Math.cos(a)*(r-15),cy+Math.sin(a)*(r-15));g.lineTo(cx+Math.cos(a+tau/8)*(r-15),cy+Math.sin(a+tau/8)*(r-15));},.24,.7);
      }
    }else if(tier===8){
      // A storm canopy, branching thunder pillars and a suspended seal.
      for(const side of [-1,1])for(let i=0;i<5;i++){
        const x=cx+side*(39+i*17),y=36+7*Math.sin(i*1.5+t*.22),size=27+(i%3)*5;
        const cloud=g.createRadialGradient(x,y,3,x,y,size);
        cloud.addColorStop(0,'#9583e94a');cloud.addColorStop(.4,'#6555bd27');cloud.addColorStop(1,'#40337b00');
        g.globalAlpha=.85;g.fillStyle=cloud;g.fillRect(x-size,y-size,size*2,size*2);
      }
      ring(cx,cy,r+8,r+8,0,.6,1.2);
      ring(cx,cy,r-15,r-15,0,.28,.7);
      for(let i=0;i<12;i++){
        const a=i*tau/12-t*.025;sigil(cx+Math.cos(a)*(r-3),cy+Math.sin(a)*(r-3),a,7);
        path(()=>g.arc(cx,cy,r+3,a+.04,a+.19),.75,1.8);
      }
      for(const side of [-1,1])for(let arc=0;arc<2;arc++){
        const points=[];
        for(let i=0;i<13;i++)points.push([cx+side*(99+arc*18+10*Math.sin(i*2.3+t*.8+arc)),36+i*20]);
        const bolt=()=>points.forEach(([x,y],i)=>i?g.lineTo(x,y):g.moveTo(x,y));
        path(bolt,arc?.07:.11,10);
        path(bolt,arc?.32:.9,arc?1:1.8);
        for(const i of [3,6,9]){
          const [x,y]=points[i];
          path(()=>{g.moveTo(x,y);g.lineTo(x+side*13,y+7);g.lineTo(x+side*8,y+17);g.lineTo(x+side*20,y+25);},.5,.9);
        }
      }
      // Counter-rotating orbit tracks connect the storm to the floor array.
      for(const side of [-1,1])ring(cx,cy+27,117,40,side*.52+t*.025,.34,.8);
      for(let i=0;i<6;i++){
        const a=i*tau/6+t*.055;
        mote(cx+Math.cos(a)*117,cy+27+Math.sin(a)*40,2,.8);
      }
      g.save();g.translate(160,292);g.scale(1,.22);
      const ground=g.createRadialGradient(0,0,8,0,0,117);
      ground.addColorStop(0,'#d1bdff32');ground.addColorStop(.6,'#7c65d322');ground.addColorStop(1,'#5a45a800');
      g.globalAlpha=.7;g.fillStyle=ground;g.fillRect(-117,-117,234,234);g.restore();
      ring(160,292,116,22,0,.75,1.2);ring(160,292,101,17,0,.4,.8);
      for(let i=0;i<12;i++){
        const a=i*tau/12-t*.05;
        sigil(160+Math.cos(a)*110,292+Math.sin(a)*20,a,4);
      }
    }else if(tier===9){
      // Translucent feather-shaped light forms an unmistakable immortal mantle.
      for(const side of [-1,1])for(let i=0;i<8;i++){
        const rootX=cx+side*(25+i*1.5),rootY=cy+16+i*9;
        const tipX=cx+side*(121+8*Math.sin(i*.7+t*.2)),tipY=47+i*27+3*Math.sin(t*.3+i*.4);
        const feather=g.createLinearGradient(rootX,rootY,tipX,tipY);
        feather.addColorStop(0,'#f7d89b04');feather.addColorStop(.55,'#ebc87832');
        feather.addColorStop(.88,'#fff1ba55');feather.addColorStop(1,'#fffce8a0');
        g.globalAlpha=.65*breath;g.fillStyle=feather;g.beginPath();g.moveTo(rootX,rootY);
        g.bezierCurveTo(cx+side*76,rootY-53,tipX-side*22,tipY+18,tipX,tipY);
        g.bezierCurveTo(tipX-side*5,tipY+37,cx+side*83,rootY+20,rootX,rootY);g.fill();
        path(()=>{g.moveTo(rootX,rootY);g.quadraticCurveTo(cx+side*97,rootY-14,tipX,tipY);},.45,.7);
      }
      // Layered celestial wheel: solar spokes, petal mandala and orbiting stars.
      g.save();g.shadowColor='#f5d98e';g.shadowBlur=7;
      ring(cx,cy,r+8,r+8,0,.85,1.4);g.restore();
      ring(cx,cy,r-1,r-1,0,.56,.8);ring(cx,cy,r-14,r-14,0,.4,.8);
      for(let i=0;i<24;i++){
        const a=i*tau/24+t*.018;
        path(()=>{g.moveTo(cx+Math.cos(a)*(r-10),cy+Math.sin(a)*(r-10));g.lineTo(cx+Math.cos(a)*(r+5),cy+Math.sin(a)*(r+5));},i%2?.28:.72,i%2?.6:1);
      }
      for(let i=0;i<12;i++){
        const a=i*tau/12-t*.022,rr=r-23;
        sigil(cx+Math.cos(a)*rr,cy+Math.sin(a)*rr,a,7);
        path(()=>{
          g.moveTo(cx+Math.cos(a)*(r-40),cy+Math.sin(a)*(r-40));
          g.quadraticCurveTo(cx+Math.cos(a-.12)*(r-5),cy+Math.sin(a-.12)*(r-5),cx+Math.cos(a+.13)*rr,cy+Math.sin(a+.13)*rr);
          g.quadraticCurveTo(cx+Math.cos(a+.37)*(r-5),cy+Math.sin(a+.37)*(r-5),cx+Math.cos(a+.26)*(r-40),cy+Math.sin(a+.26)*(r-40));
        },.3,.7);
        const sx=cx+Math.cos(a)*(r+8),sy=cy+Math.sin(a)*(r+8);
        path(()=>{g.moveTo(sx-3,sy);g.lineTo(sx+3,sy);g.moveTo(sx,sy-4);g.lineTo(sx,sy+4);},.85,.8);
      }
      for(const side of [-1,1])ring(cx,cy+29,122,34,side*.25+t*.012,.3,.8);
      // Three luminous petal tiers form a substantial lotus pedestal.
      for(let layer=0;layer<3;layer++)for(let i=0;i<9;i++){
        const spread=(i-4)/4,x=160+spread*(73-layer*13),top=278-layer*10-13*(1-spread*spread);
        const petal=g.createLinearGradient(x,top,160,305);
        petal.addColorStop(0,layer===2?'#fff7d470':'#efd08e38');petal.addColorStop(1,'#c5944904');
        g.globalAlpha=.7;g.fillStyle=petal;g.beginPath();g.moveTo(160,303);
        g.bezierCurveTo(x-24,294,x-14,top+9,x,top);g.bezierCurveTo(x+18,top+17,x+25,296,160,303);g.fill();
        path(()=>{g.moveTo(160,303);g.quadraticCurveTo(x-25,top+17,x,top);g.quadraticCurveTo(x+25,top+24,160,303);},.5,.8);
      }
      ring(160,301,106,14,0,.8,1.2);ring(160,298,119,22,0,.45,.8);
      for(let i=0;i<8;i++){
        const a=i*tau/8+t*.04;mote(cx+Math.cos(a)*117,cy+29+Math.sin(a)*44,1.6,.8);
      }
    }
    // Slow peripheral motes add life without crossing the face or flashing.
    const count=reduced?3:3+tier*2;
    for(let i=0;i<count;i++){
      const a=i*tau/count+t*(.045+tier*.003),rad=r+5+5*Math.sin(i*2.1+t*.35);
      mote(cx+Math.cos(a)*rad,cy+Math.sin(a)*rad,.7+(i%3)*.3,.3+.35*(.5+.5*Math.sin(i+t*.6)));
    }
  }finally{g.restore();}
}

/** Weapon cosmetics use authored canvas anchors, never an inferred hand pose. */
function drawInventoryWeaponFx(g,hero,time,reduced=false,tier=typeof S!=='undefined'&&S?S.realm:0){
  const sheet=typeof INVENTORY_SPRITE_SHEETS!=='undefined'?INVENTORY_SPRITE_SHEETS[hero]:null;
  const weapon=sheet?.weaponFx;
  if(!weapon||weapon.kind!=='sword')return false;
  if(weapon.layer)return drawInventorySprite(g,hero,time,reduced,weapon.layer);
  const theme=skinFxTheme(hero,tier)||{color:'#8fcbdc',light:'#e1faff'};
  const t=reduced?0:(Number.isFinite(time)?time:0),tau=Math.PI*2;
  const [x0,y0]=weapon.start,[x1,y1]=weapon.tip;
  const dx=x1-x0,dy=y1-y0,length=Math.hypot(dx,dy);
  if(!Number.isFinite(length)||length<1)return false;
  const nx=-dy/length,ny=dx/length;
  g.save();
  try{
    g.globalCompositeOperation='lighter';g.lineCap='round';
    const aura=g.createLinearGradient(x0,y0,x1,y1);
    aura.addColorStop(0,theme.color+'00');aura.addColorStop(.18,theme.color+'6a');
    aura.addColorStop(.8,theme.color+'8a');aura.addColorStop(1,theme.color+'00');
    g.strokeStyle=aura;g.globalAlpha=reduced?.35:.6+.08*Math.sin(t*tau/4);
    g.shadowColor=theme.color;g.shadowBlur=reduced?2:5;
    g.lineWidth=4;g.beginPath();g.moveTo(x0,y0);g.lineTo(x1,y1);g.stroke();
    g.shadowBlur=0;g.lineWidth=.9;g.globalAlpha=.62;g.strokeStyle=theme.light;
    g.beginPath();g.moveTo(x0+dx*.14,y0+dy*.14);g.lineTo(x1-dx*.07,y1-dy*.07);g.stroke();
    const count=reduced?1:3;
    for(let i=0;i<count;i++){
      const u=.16+.73*((t/4+i/count)%1),offset=reduced?0:Math.sin(t*1.8+i*2)*1.4;
      const x=x0+dx*u+nx*offset,y=y0+dy*u+ny*offset;
      g.globalAlpha=(reduced?.3:.65)*Math.sin(Math.PI*(u-.16)/.73);
      g.fillStyle=theme.light;g.beginPath();g.arc(x,y,.8,0,tau);g.fill();
      if(i===0){g.lineWidth=.65;g.beginPath();g.moveTo(x-2,y);g.lineTo(x+2,y);g.moveTo(x,y-2);g.lineTo(x,y+2);g.stroke();}
    }
  }finally{g.restore();}
  return true;
}
