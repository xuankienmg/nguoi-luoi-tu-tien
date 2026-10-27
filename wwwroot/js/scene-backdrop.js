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
    context.strokeStyle='#ff8b42';context.lineWidth=Math.max(1,height*.005);
    for(let j=0;j<6;j++){
      const x=width*(j+.35)/6;
      context.beginPath();context.moveTo(x,height);context.lineTo(x+width*.022,height*.93);
      context.lineTo(x-width*.018,height*.88);context.lineTo(x+width*.035,height*.82);context.stroke();
    }
  }
  context.restore();return true;
}
