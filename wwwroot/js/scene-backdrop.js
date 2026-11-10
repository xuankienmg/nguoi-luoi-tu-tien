/* Painted combat environments with a small animated lava light layer. */
const conceptBackdrop = new Image();
conceptBackdrop.src = 'img/backgrounds/concept-courtyard-v1.webp';
const hellBackdrop = new Image();
hellBackdrop.src = 'img/backgrounds/hell-fire-v1.webp';
const hellLavaGlow = new Image();
hellLavaGlow.src = 'img/backgrounds/hell-fire-glow-v1.webp';

function backdropImageReady(image){ return image.complete && image.naturalWidth > 0; }
function conceptBackdropReady(hell = false){
  return (hell && backdropImageReady(hellBackdrop)) || backdropImageReady(conceptBackdrop);
}

// Sprite Fusion Falling Leaves, © 2026 Hugo Duprez (MIT, see licenses/falling-leaves.txt).
// Canvas adaptation of the three pixel shapes and the pendulum fall/flip motion.
const courtyardLeafShapes = [
  ['......DDDD..','...DDDLLLBD.','.DDLLLRLBBBD','TRRRRRRRRRRD','.DDBBBBBRSSD','...DDDSSSSD.','......DDDD..'],
  ['...DDD...','..DLLBD..','.DLLLBBD.','DLLRBBBSD','DLBBRBSSD','.DBBBRSD.','..DDSSD..','....T....'],
  ['.....D.....','....DLD....','.DD.DLD.DD.','.DLDDLDDLD.','..DLLLBBD..','DDDLLRBBDDD','.DLLBRBSSD.','..DDBRSDD..','....DRD....','.....T.....'],
];
const courtyardLeafPalettes = [
  ['#ffc86a','#f08a2a','#b8521a','#5e240e'], ['#ff8a66','#d8382a','#9a1f22','#470f15'],
  ['#fff09a','#f0c83a','#b88a1c','#5a3f10'], ['#c99a62','#9a6a3b','#6b4424','#33200f'],
];
let courtyardLeafSprites;

function drawCourtyardLeaves(context,width,height){
  if (typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  courtyardLeafSprites ??= courtyardLeafShapes.map(rows => courtyardLeafPalettes.map(([L,B,S,D]) => {
    const leaf = document.createElement('canvas');
    leaf.width = rows[0].length;
    leaf.height = rows.length;
    const ink = leaf.getContext('2d');
    const colors = {L,B,S,D,R:S,T:D};
    rows.forEach((row,y) => [...row].forEach((pixel,x) => {
      if (pixel === '.') return;
      ink.fillStyle = colors[pixel];
      ink.fillRect(x,y,1,1);
    }));
    return leaf;
  }));

  const time = performance.now()/1000;
  const floor = height*.8;
  context.imageSmoothingEnabled = false;
  // The source preset has 80 leaves and litter. This backdrop uses just eight and no litter.
  for (let i=0;i<8;i++){
    const fall = 6.1+(i%4)*.67;
    const rest = 1.6+(i%3)*.4;
    const elapsed = (time+i*1.618)%(fall+rest);
    const moving = elapsed < fall;
    const t = Math.min(elapsed,fall);
    const swing = t*(1.8+(i%4)*.25)+i*1.27;
    const progress = t/fall;
    const descent = progress+.033*Math.sin(progress*Math.PI)*Math.sin(swing*2);
    const x = width*(.06+((i*.618034)%1)*.84+.05*progress)+Math.sin(swing)*Math.min(width*.047,height*.055);
    const sprite = courtyardLeafSprites[i%3][i%4];
    const pixel = Math.max(.5,Math.min(1.35,height/480))*(i%3===0?1:.82);
    const y = floor*descent-sprite.height*pixel/2;
    const squash = moving ? .35+.65*Math.abs(Math.cos(t*(i%3===0?1.6:.63)+i)) : 1;
    const fade = moving ? Math.min(1,t/.38) : Math.min(1,(fall+rest-elapsed)/.85);
    context.save();
    context.globalAlpha = fade*(i%3===0?.82:.62);
    context.translate(x,y);
    context.rotate(Math.cos(swing)*(moving?.75:.225));
    context.scale(pixel*squash,pixel);
    context.drawImage(sprite,-sprite.width/2,-sprite.height/2);
    context.restore();
  }
}

function drawHellFireBackdrop(context,width,height){
  // Preserve both lava falls in the 5:4 panel. The strip crops vertically around the floor.
  const plateHeight = Math.max(height,width*hellBackdrop.naturalHeight/hellBackdrop.naturalWidth);
  const top = .8*(height-plateHeight);
  context.drawImage(hellBackdrop,0,top,width,plateHeight);
  const reduced = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const time = reduced ? 0 : performance.now()/1000;
  context.globalCompositeOperation = 'lighter';
  if (backdropImageReady(hellLavaGlow)){
    context.globalAlpha = .21 + .11*Math.sin(time*2.7) + .06*Math.sin(time*5.3+.7);
    context.drawImage(hellLavaGlow,0,top,width,plateHeight);
  }
  for (let i=0;i<24;i++){
    const phase = (time*(.16+i%4*.025)+i*.618034)%1;
    // Give both sides more embers while leaving the center open for hit numbers.
    const bank = i%2 ? .69 : .03;
    const x = width*(bank+((i*.381966)%1)*.28+.011*Math.sin(time*1.7+i));
    const y = height*(.77-phase*(.24+i%3*.055));
    const radius = Math.max(.55,height*.0035)*(1-phase*.47);
    context.globalAlpha = Math.sin(Math.PI*phase)*.76;
    context.fillStyle = i%4 ? '#fa762d' : '#ffcc78';
    context.beginPath();
    context.ellipse(x,y,radius*.64,radius*1.32,-.26,0,Math.PI*2);
    context.fill();
  }
}

function drawConceptBackdrop(context,width,height,hell=false){
  if (width<=0 || height<=0 || !conceptBackdropReady(hell)) return false;
  context.save();
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = 'high';
  if (hell && backdropImageReady(hellBackdrop)){
    drawHellFireBackdrop(context,width,height);
  } else {
    const iw=conceptBackdrop.naturalWidth,ih=conceptBackdrop.naturalHeight,k=Math.max(width/iw,height/ih);
    context.drawImage(conceptBackdrop,(width-iw*k)/2,(height-ih*k)/2,iw*k,ih*k);
    context.fillStyle=hell?'rgba(75,16,11,.48)':'rgba(3,14,21,.13)';
    context.fillRect(0,0,width,height);
    if (!hell) drawCourtyardLeaves(context,width,height);
  }
  context.restore();
  return true;
}
