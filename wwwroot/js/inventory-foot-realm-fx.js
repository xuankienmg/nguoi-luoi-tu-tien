/* Realm-specific inventory ground effects. Drawn behind the hero and shared by the zoom view. */
const INVENTORY_FOOT_REALMS = [
  {name:'Luyện Khí',effect:'Linh khí',color:'#9ed8cd',light:'#dcfff2'},
  {name:'Trúc Cơ',effect:'Bát giác đạo cơ',color:'#59cdb1',light:'#c4ffe7'},
  {name:'Kim Đan',effect:'Kim đan nhật luân',color:'#e5b956',light:'#fff0b6'},
  {name:'Nguyên Anh',effect:'Tử liên nguyên thần',color:'#b688ea',light:'#efdbff'},
  {name:'Hóa Thần',effect:'Thanh quang kiếm trận',color:'#80d8ef',light:'#ecfcff'},
  {name:'Luyện Hư',effect:'Hư không pháp luân',color:'#7999ee',light:'#dce5ff'},
  {name:'Hợp Thể',effect:'Lưỡng nghi hợp nhất',color:'#e6a45e',light:'#ffe4b3'},
  {name:'Đại Thừa',effect:'Kim liên đại đạo',color:'#ebc75b',light:'#fff5ce'},
  {name:'Độ Kiếp',effect:'Tử điện lôi trận',color:'#b48bff',light:'#e8edff'},
  {name:'Chân Tiên',effect:'Chu thiên tinh đồ',color:'#f0d38a',light:'#b9f6ed'}
];

function drawInventoryFootRealmFx(g,realmIndex,time,reduced=false){
  const tier=Math.max(0,Math.min(9,Math.floor(Number(realmIndex)||0)));
  const theme=INVENTORY_FOOT_REALMS[tier],tau=Math.PI*2;
  const t=reduced?0:(Number.isFinite(time)?Math.max(0,time):0);
  const radius=76+tier*3,angle=(t%60)*tau/60;
  g.save();
  try{
    g.translate(160,290);g.scale(1,.25);g.rotate(angle);
    g.globalCompositeOperation='screen';g.lineCap='round';g.lineJoin='round';
    g.shadowColor=theme.color;g.shadowBlur=4;
    const path=(draw,alpha=.65,width=1.2,color=theme.color)=>{
      g.globalAlpha=alpha;g.lineWidth=width;g.strokeStyle=color;g.beginPath();draw();g.stroke();
    };
    const ring=(r,alpha=.5,width=1)=>path(()=>g.arc(0,0,r,0,tau),alpha,width);
    const dot=(x,y,r=1.8,alpha=.8)=>{
      g.globalAlpha=alpha;g.fillStyle=theme.light;g.beginPath();g.arc(x,y,r,0,tau);g.fill();
    };
    const radial=(count,r,draw)=>{
      for(let i=0;i<count;i++){
        g.save();try{g.rotate(i*tau/count);g.translate(0,-r);draw(i);}finally{g.restore();}
      }
    };
    const diamond=(size=4)=>path(()=>{
      g.moveTo(0,-size);g.lineTo(size*.55,0);g.lineTo(0,size);g.lineTo(-size*.55,0);g.closePath();
    },.8,1,theme.light);
    const lotus=(count,r,length,alpha=.6)=>radial(count,r,()=>path(()=>{
      g.moveTo(0,0);g.bezierCurveTo(-length*.5,-length*.4,-length*.35,-length*.9,0,-length);
      g.bezierCurveTo(length*.35,-length*.9,length*.5,-length*.4,0,0);
    },alpha,1.2));
    const polygon=(count,r,offset=0)=>path(()=>{
      for(let i=0;i<=count;i++){
        const a=i*tau/count+offset,x=Math.cos(a)*r,y=Math.sin(a)*r;
        if(i===0)g.moveTo(x,y);else g.lineTo(x,y);
      }
    },.65,1.3);
    ring(radius,.4,1.3);
    if(tier===0){
      // Deliberately quiet: soft qi arcs and six breathing light points.
      for(let i=0;i<3;i++)path(()=>g.arc(0,0,radius-10,i*tau/3,i*tau/3+.7),.35,2);
      radial(6,radius,()=>dot(0,0,1.5,reduced ? .5 : .45+.2*Math.sin(t*.8)));
    }else if(tier===1){
      polygon(8,radius-8);polygon(8,radius-21,Math.PI/8);
      radial(8,radius-8,()=>diamond(5));ring(radius-29,.3);
    }else if(tier===2){
      ring(radius-8,.8,1.8);ring(radius-24,.5);ring(15,.7,2);
      dot(0,0,6,.5);
      radial(24,radius-13,i=>path(()=>{g.moveTo(0,0);g.lineTo(0,i%3===0?-9:-4);},.55));
      radial(8,radius-29,()=>diamond(3));
    }else if(tier===3){
      lotus(8,30,46,.75);lotus(8,36,32,.35);ring(radius-10,.45);
      radial(16,radius-10,()=>dot(0,0,1.6,.65));
    }else if(tier===4){
      ring(radius-10,.6);polygon(8,radius-23);
      radial(8,32,()=>{
        path(()=>{g.moveTo(0,-43);g.lineTo(-3,-31);g.lineTo(-2,-5);g.lineTo(2,-5);g.lineTo(3,-31);g.closePath();},.8,1.2,theme.light);
        path(()=>{g.moveTo(-6,-5);g.lineTo(6,-5);g.moveTo(0,-5);g.lineTo(0,3);},.6,1.5);
      });
    }else if(tier===5){
      for(let band=0;band<3;band++){
        const r=radius-band*13;
        for(let i=0;i<6;i++)path(()=>g.arc(0,0,r,i*tau/6+band*.13,i*tau/6+.52+band*.13),.6-band*.1,band?1:2);
      }
      radial(6,radius-15,i=>path(()=>{
        g.moveTo(-7,4);g.lineTo(-1,-4);g.lineTo(4,1);g.lineTo(9,-10);
      },.6,1,theme.light));
    }else if(tier===6){
      ring(radius-9,.65);ring(radius-16,.3);
      path(()=>{g.moveTo(0,-55);g.bezierCurveTo(62,-55,62,0,0,0);g.bezierCurveTo(-62,0,-62,55,0,55);},.8,2);
      ring(55,.6,1.3);dot(0,-28,5,.75);dot(0,28,5,.35);
      radial(16,radius-10,()=>diamond(3));
    }else if(tier===7){
      lotus(24,53,36,.75);lotus(12,27,47,.55);ring(radius-7,.8,1.8);
      ring(28,.6);radial(24,radius-7,()=>dot(0,0,1.5,.85));
      polygon(12,43,Math.PI/12);
    }else if(tier===8){
      ring(radius-12,.6);polygon(8,radius-25);ring(39,.4);
      const flash=reduced ? .65 : .55+.25*Math.pow(.5+.5*Math.sin(t*1.6),4);
      radial(8,37,i=>path(()=>{
        g.moveTo(0,0);g.lineTo(-5,-13);g.lineTo(5,-22);g.lineTo(-4,-33);g.lineTo(3,-48);
        g.moveTo(5,-22);g.lineTo(14,-27);g.lineTo(16,-35);
      },flash,1.8,theme.light));
      radial(8,radius-12,()=>diamond(6));
    }else{
      ring(radius-7,.8);ring(radius-16,.5);lotus(16,53,31,.5);polygon(12,45);
      for(let i=0;i<48;i++){
        const a=i*tau/48,r=radius-23-(i%3)*7;
        dot(Math.cos(a)*r,Math.sin(a)*r,i%4?1:2,.8);
        if(i%4===0)path(()=>{
          g.moveTo(Math.cos(a)*r,Math.sin(a)*r);
          const b=a+tau/12;g.lineTo(Math.cos(b)*(r-8),Math.sin(b)*(r-8));
        },.4,.7,theme.light);
      }
      radial(12,45,()=>diamond(5));ring(24,.65);dot(0,0,3,.6);
    }
  }finally{g.restore();}
  return true;
}
