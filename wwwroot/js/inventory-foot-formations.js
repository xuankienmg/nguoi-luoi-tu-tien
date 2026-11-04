/* Optional foot formation effect. Previewed separately until enabled in the game. */
function drawInventoryFootFormation(g, realmIndex, time, reduced=false, imageLoader=skImg){
  const tier=Math.max(0,Math.min(9,Math.floor(Number(realmIndex)||0)))+1;
  const image=imageLoader(`img/fx/inventory-formations-v1/tier-${String(tier).padStart(2,'0')}.png`);
  if(!image)return false;
  const seconds=reduced?0:(Number.isFinite(time)?Math.max(0,time):0);
  const radius=86+(tier-1)*1.8;
  g.save();
  try{
    // Rotate in the ground plane, then project that plane into an ellipse.
    g.translate(160,296);
    g.scale(1,.23);
    g.rotate((seconds%60)*Math.PI*2/60);
    g.globalAlpha=reduced ? .52 : .52+.035*Math.sin(seconds*Math.PI/3);
    g.globalCompositeOperation='screen';
    g.drawImage(image,-radius,-radius,radius*2,radius*2);
  }finally{g.restore();}
  return true;
}
