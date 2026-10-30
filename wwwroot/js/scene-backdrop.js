/* Static concept artwork: full-resolution painting behind the existing combat layers. */
const conceptBackdrop=new Image();
conceptBackdrop.src='img/backgrounds/concept-courtyard-v1.webp';
function conceptBackdropReady(){return conceptBackdrop.complete&&conceptBackdrop.naturalWidth>0;}
function drawConceptBackdrop(context,width,height,hell=false){
  if(!conceptBackdropReady())return false;
  const iw=conceptBackdrop.naturalWidth,ih=conceptBackdrop.naturalHeight,k=Math.max(width/iw,height/ih);
  context.save();context.imageSmoothingEnabled=true;context.imageSmoothingQuality='high';
  context.drawImage(conceptBackdrop,(width-iw*k)/2,(height-ih*k)/2,iw*k,ih*k);
  // Quiet values behind HUD without changing the layout or hiding environmental detail.
  context.fillStyle=hell?'rgba(39,8,13,.72)':'rgba(3,14,21,.13)';context.fillRect(0,0,width,height);
  if(hell){
    const glow=context.createLinearGradient(0,height*.3,0,height);
    glow.addColorStop(0,'rgba(40,9,16,0)');glow.addColorStop(1,'rgba(244,77,23,.65)');
    context.fillStyle=glow;context.fillRect(0,0,width,height);
    const reduced=typeof matchMedia==='function'&&matchMedia('(prefers-reduced-motion: reduce)').matches;
    const time=reduced?0:performance.now()/1000;
    context.globalCompositeOperation='lighter';context.lineCap='round';
    // Irregular pools and glowing seams sit in the ground, below the fighters.
    for(let j=0;j<5;j++){
      const x=width*(j+.45)/5,y=height*(.94+.018*Math.sin(j*4.7));
      const radius=Math.max(4,width*(.045+.012*Math.sin(j*7)));
      context.save();context.translate(x,y);context.scale(1,.22);
      // Gradient coordinates follow the local pool transform.
      const local=context.createRadialGradient(0,0,0,0,0,radius);
      local.addColorStop(0,'rgba(255,168,65,.45)');local.addColorStop(1,'rgba(205,44,12,0)');
      context.fillStyle=local;context.beginPath();context.arc(0,0,radius,0,Math.PI*2);context.fill();context.restore();

    }
    // Low, translucent tongues of fire swell gently along the foreground.
    for(let j=0;j<14;j++){
      const phase=time*(.85+(j%3)*.12)+j*2.4;
      const swell=.65+.35*Math.sin(phase);
      const x=width*(j+.35)/14,y=height*1.025;
      const h=height*(.055+.035*swell+.012*Math.sin(j*5));
      const w=Math.max(2,Math.min(width*.018,height*.045));
      const sway=Math.sin(phase*.8)*w*.45;
      const flame=context.createLinearGradient(0,y,0,y-h);
      flame.addColorStop(0,'rgba(255,111,28,0)');
      flame.addColorStop(.25,'rgba(255,148,42,.28)');
      flame.addColorStop(.6,'rgba(242,76,20,.18)');
      flame.addColorStop(1,'rgba(205,44,12,0)');
      context.globalAlpha=.7;context.fillStyle=flame;
      context.beginPath();context.moveTo(x-w,y);
      context.bezierCurveTo(x-w*1.1,y-h*.35,x+w*.4+sway,y-h*.65,x+sway,y-h);
      context.bezierCurveTo(x+w*.7+sway,y-h*.55,x+w*1.1,y-h*.25,x+w,y);
      context.closePath();context.fill();
    }
    // Small drifting embers replace the repeated full-height zigzags.
    for(let j=0;j<20;j++){
      const p=(time*(.12+(j%4)*.025)+j*.618)%1;
      const x=width*((j*.381966)%1)+Math.sin(time+j*2)*width*.006;
      const y=height*(1-p*.23),r=Math.max(.6,height*.003)*(1-p*.5);
      context.globalAlpha=Math.sin(p*Math.PI)*.65;context.fillStyle=j%3?'#ff8840':'#ffd18a';
      context.beginPath();context.ellipse(x,y,r*.6,r,Math.sin(j),0,Math.PI*2);context.fill();
    }
  }
  context.restore();return true;
}
