'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const fmt = n => Number(n || 0).toLocaleString('vi-VN');
  const norm = s => String(s ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g,'d').replace(/Đ/g,'D').toLowerCase();
  const tabs = {
    gear:['Trang bị','Chọn cấp, phẩm và ô trang bị để so sánh chỉ số gốc, hiệu ứng riêng và bộ trang bị.'],
    pets:['Linh thú','Khám phá các loài linh thú, thiên phú, kỹ năng và ba hình thái tiến hóa.'],
    map:['Bản đồ & các ải','Tra cứu từng đợt quái, Boss và linh thạch ở cả ba mức độ khó.'],
    alchemy:['Đan dược','Đan phương cho một viên: nguyên liệu, phí linh thạch và công dụng.'],
    materials:['Vật phẩm & linh thảo','Tìm công dụng, nơi thu thập linh thảo, rương và các vật phẩm khác.'],
    realms:['Cảnh giới','Các tầng tu hành, ô trang bị, hành trang và điều kiện đột phá.']
  };
  let data, items, tab='gear', query='', filters={}, page=1, currentId='';
  const size=24;
  const qualityName = q => q==='ancient' ? 'Thượng Cổ' : data.qualityNames[data.qualityKeys.indexOf(q)] || q;
  const color = q => q==='ancient' ? '#e9aaf4' : data.qualityColors[data.qualityKeys.indexOf(q)] || '#dfc487';
  const realmName = r => data.realms[r]?.name || 'Chưa xác định';
  const roleName = r => ({atk:'Tấn công',def:'Phòng thủ',hp:'Sinh lực',qi:'Tu luyện',crit:'Bạo kích'})[r] || r;
  function img(path, cls='icon', rarity='common', alt='') {
    return path ? `<img class="${cls}" src="img/wiki/${esc(path.replace(/^img\//,''))}" alt="${esc(alt)}" loading="lazy" style="--quality:${color(rarity)}">` : '<span class="icon" aria-hidden="true">◇</span>';
  }
  const badge = q => `<span class="badge" style="--quality:${color(q)}">${esc(qualityName(q))}</span>`;
  const stats = it => '<div class="stats">'+[['atk','Công'],['hp','Máu'],['def','Thủ'],['qiPct','Tu vi %'],['speedPct','Thân pháp %'],['critPct','Bạo kích %']].filter(([k])=>it[k]).map(([k,n])=>`<span>${n} ${fmt(it[k])}</span>`).join('')+'</div>';
  const match = x => !query || norm(x).includes(norm(query));
  function ingredient(id,n) {
    const it=items[id];
    return `<button type="button" class="ingredient" data-item="${esc(id)}">${img(it?.icon,'ingredient-icon',it?.rarity)}${esc(it?.name || id)}${n===undefined?'':` ×${fmt(n)}`}</button>`;
  }
  const block = (title,content) => `<section class="detail-block"><h3>${title}</h3>${content}</section>`;
  function select(key,label,options,defaultValue='') {
    const val=filters[key] ?? defaultValue;
    return `<select data-filter="${key}" aria-label="${label}">${options.map(([value,name])=>`<option value="${esc(value)}"${String(value)===String(val)?' selected':''}>${esc(name)}</option>`).join('')}</select>`;
  }
  function setFilters() {
    let html='';
    if(tab==='gear') html=select('realm','Cảnh giới',[['','Mọi cảnh giới'],...data.realms.map(r=>[r.index,r.name])])+select('rarity','Phẩm trang bị',[['','Mọi phẩm'],...data.qualityKeys.map(q=>[q,qualityName(q)])])+select('slot','Ô trang bị',[['','Mọi ô trang bị'],...data.slots.map(s=>[s.id,s.name])]);
    if(tab==='pets') html=select('element','Ngũ hành',[['','Mọi hệ'],...Object.entries({hoa:'Hỏa',thuy:'Thủy',moc:'Mộc',tho:'Thổ',loi:'Lôi',bang:'Băng',phong:'Phong',gio:'Phong'}).filter(([k])=>data.pets.some(p=>(p.elements||[p.element]).includes(k)))])+select('rarity','Phẩm linh thú',[['','Mọi phẩm'],...['common','uncommon','rare','epic','legendary','ancient'].map(q=>[q,qualityName(q)])]);
    if(tab==='map') html=select('chapter','Chương',data.chapters.map(c=>[c.index,`${c.index+1}. ${c.name}${c.enabled?'':' · Chưa mở'}`]),'0')+select('difficulty','Độ khó',[['0','Thường'],['1','Khó'],['2','Ác mộng']],'0');
    if(tab==='materials') html=select('type','Loại vật phẩm',[['','Mọi loại'],...Array.from(new Set(data.items.filter(i=>!i.slot).map(i=>i.type))).sort().map(t=>[t,typeName(t)])]);
    $('selects').innerHTML=html;
  }
  function typeName(t) { return ({herb:'Linh thảo',chest:'Rương',egg:'Thú đản',pill:'Đan dược',elixir:'Đan đặc biệt',stone:'Linh thạch',ticket:'Vé quay',key:'Chìa khóa',boss:'Nguyên liệu Boss',manual:'Công pháp',skin:'Trang phục',material:'Nguyên liệu'})[t] || data.items.find(i=>i.type===t)?.kind || t; }
  function route() {
    const p=new URLSearchParams(location.hash.slice(1));
    tab=tabs[p.get('tab')] ? p.get('tab') : 'gear';
    query=p.get('q') || ''; filters={};
    for(const k of ['realm','rarity','slot','element','chapter','difficulty','type']) if(p.has(k)) filters[k]=p.get(k);
    page=Math.max(1,Number(p.get('page'))||1); currentId=p.get('id')||'';
    if(!data)return;
    $('search').value=query;
    $('sectionNumber').textContent=`TÀNG THƯ / 0${Object.keys(tabs).indexOf(tab)+1}`;
    $('sectionTitle').textContent=tabs[tab][0]; $('sectionDesc').textContent=tabs[tab][1];
    $('nav').innerHTML=Object.entries(tabs).map(([id,[name]],i)=>`<a href="#tab=${id}"${id===tab?' aria-current="page"':''}><span>0${i+1}</span>${name}</a>`).join('');
    setFilters();render();
    if(currentId) showDetail(currentId.startsWith('pet:')?currentId.slice(4):currentId,currentId.startsWith('pet:'));
    else if($('detail').open)$('detail').close();
  }
  function save() {
    const p=new URLSearchParams({tab});
    if(query)p.set('q',query);
    for(const [k,v] of Object.entries(filters))if(v!=='')p.set(k,v);
    if(page>1)p.set('page',page);
    if(currentId)p.set('id',currentId);
    history.replaceState(null,'','#'+p.toString());
  }
  function card(it) {
    return `<button type="button" class="card" data-item="${esc(it.id)}"><div class="card-top">${img(it.icon,'icon',it.rarity)}${badge(it.rarity)}</div><strong>${esc(it.name)}</strong><small>${esc(it.kind||typeName(it.type))}${it.slot?` · Cấp ${it.itemLevel || 1} · ${esc(realmName(it.minRealm))}`:''}</small>${it.slot?stats(it):`<p class="desc">${esc(it.use||it.desc)}</p>`}</button>`;
  }
  function paginate(list,draw) {
    const pages=Math.max(1,Math.ceil(list.length/size));page=Math.min(page,pages);
    $('resultCount').textContent=`${fmt(list.length)} kết quả${query?' cho “'+query+'”':''}`;
    $('results').innerHTML=list.length ? '<div class="cards">'+list.slice((page-1)*size,page*size).map(draw).join('')+'</div>' : '<p class="empty">Không tìm thấy kết quả phù hợp.<br>Thử tên khác hoặc bỏ bớt bộ lọc.</p>';
    $('pagination').innerHTML=pages>1?`<button type="button" data-page="${page-1}"${page===1?' disabled':''}>← Trước</button><span>${page} / ${pages}</span><button type="button" data-page="${page+1}"${page===pages?' disabled':''}>Sau →</button>`:'';
  }
  function render() {
    $('pagination').innerHTML='';
    if(tab==='gear')return paginate(data.items.filter(i=>i.slot&&match([i.name,i.desc,i.set,i.kind,i.special,...(i.effects||[]).map(e=>e.text)].join(' '))&&(!filters.realm||i.minRealm===Number(filters.realm))&&(!filters.rarity||i.rarity===filters.rarity)&&(!filters.slot||i.slot===filters.slot)),card);
    if(tab==='pets')return paginate(data.pets.filter(p=>match([p.name,p.desc,roleName(p.role),p.talentText,p.skillText].join(' '))&&(!filters.element||(p.elements||[p.element]).includes(filters.element))&&(!filters.rarity||p.rarity===filters.rarity)),p=>`<button type="button" class="card" data-pet="${esc(p.id)}"><div class="card-top">${img(p.forms[0]?.image,'pet-icon',p.rarity)}${badge(p.rarity)}</div><strong>${esc(p.name)}</strong><small>${esc(roleName(p.role))}</small><p class="desc">${esc(p.desc)}</p>${stats(p)}</button>`);
    if(tab==='alchemy')return paginate(data.brews.filter(b=>match([items[b.out]?.name,items[b.out]?.use,...Object.keys(b.in).map(k=>items[k]?.name)].join(' '))),b=>{const it=items[b.out];return `<button type="button" class="card" data-item="${esc(it.id)}"><div class="card-top">${img(it.icon,'icon',it.rarity)}${badge(it.rarity)}</div><strong>${esc(it.name)}</strong><small>${Object.keys(b.in).filter(k=>k!=='linh_thach').length} loại nguyên liệu · ${fmt(b.in.linh_thach)} linh thạch</small><p class="desc">${esc(it.use||it.desc)}</p></button>`;});
    if(tab==='materials')return paginate(data.items.filter(i=>!i.slot&&match(i.name+' '+i.desc+' '+(i.use||''))&&(!filters.type||i.type===filters.type)),card);
    if(tab==='realms')return paginate(data.realms.filter(r=>match(r.name+' '+r.desc)),r=>`<article class="card"><p class="eyebrow">CẢNH GIỚI 0${r.index+1}</p><h3>${esc(r.name)}</h3><p class="desc">${esc(r.desc)}</p><small>${r.stages.length} tầng / kỳ · ${r.bagSlots} ô hành trang</small><p class="desc">${r.stages.map(esc).join(' · ')}</p><small>Trang bị: ${r.equipSlots.map(s=>esc(data.slots.find(x=>x.id===s)?.name||s)).join(', ')}</small>${r.breakCost?`<div class="detail-block"><strong>Đột phá cảnh giới kế tiếp</strong><div class="drop-list">${Object.entries(r.breakCost).map(([k,n])=>ingredient(k,n)).join('')}</div><p class="muted">Tỉ lệ gốc: ${fmt(r.breakChance*100)}%</p></div>`:'<p class="muted">Chưa có đột phá cảnh giới kế tiếp.</p>'}</article>`);
    if(tab==='map')renderMap();
  }
  function renderMap() {
    const c=data.chapters[Number(filters.chapter)||0]||data.chapters[0];
    const diff=Math.min(2,Math.max(0,Number(filters.difficulty)||0)),d=c.difficulties[diff];
    const areas=d.areas.filter(a=>match(a.place+' '+a.waves.map(w=>w.name).join(' ')+' '+(c.index+1)+'-'+a.index));
    $('resultCount').textContent=`${areas.length} ải · ${d.name}`;
    const herbs=d.herbs||data.herbDrops.filter(h=>h.chapters.includes(d.herbChapter));
    $('results').innerHTML=`<header class="chapter-header"><p class="eyebrow">CHƯƠNG ${c.index+1} / ${esc(d.name.toUpperCase())}</p><h3>${esc(c.name)}</h3><p class="muted">Cảnh giới gợi ý: ${esc(c.realm)} · Boss: ${esc(c.bossLair)}${c.enabled?'':' · Chương chưa mở'}</p><div class="drop-list">${ingredient(c.keyItem)}${c.bossItem?ingredient(c.bossItem):''}</div></header><p class="note">Mỗi ải thường gồm 3 đợt quái, từ ải 10 có tinh anh ở đợt cuối. Linh thạch dưới đây tính cho từng quái. Chỉ số đã tính theo mức độ khó, chưa tính hiệu ứng trong trận.</p>${areas.length?areas.map(a=>`<details class="area${a.index===16?' boss':''}"><summary><span>${c.index+1}-${a.index} · ${esc(a.place)}</span><small>${a.index===16?'BOSS':a.index>=10?'TINH ANH':'THƯỜNG'} · ${a.waves.length} đợt</small></summary><div class="area-content">${a.waves.map((w,i)=>`<div class="wave">${img(w.image,'monster',undefined)}<div><strong>${i+1}. ${esc(w.name)}</strong>${stats(w)}<p>Linh thạch: ${fmt(w.stoneMin)}–${fmt(w.stoneMax)}</p></div></div>`).join('')}${herbs.some(h=>a.index>=h.minArea)?`<p class="muted">Linh thảo có thể rơi tại ải này</p><div class="drop-list">${herbs.filter(h=>a.index>=h.minArea).map(h=>ingredient(h.item,undefined)).join('')}</div>`:''}</div></details>`).join(''):'<p class="empty">Không tìm thấy ải phù hợp.</p>'}<section class="paper"><h3>Vật phẩm trong bảng rơi của chương</h3><p class="muted">Danh sách nguyên liệu / vật phẩm gốc; phẩm trang bị được game chọn riêng. Phần thưởng thực tế còn tùy ải và loại quái.</p><div class="drop-list">${d.drops.filter(x=>x.item!=='linh_thach').map(x=>ingredient(x.item)).join('')}</div></section>`;
  }
  /** Nơi rơi khi hạ Boss: gom các chương liền nhau có cùng tỉ lệ / số lượng theo từng mức khó. */
  function bossSources(id) {
    const out=[];
    for(let d=0;d<3;d++){
      let run=null;const runs=[];
      for(const c of data.chapters.filter(c=>c.enabled)){
        const m=(c.difficulties[d]?.bossMaterials||[]).find(x=>x.item===id);
        const key=m?`${m.chance}|${m.min}|${m.max}`:'';
        if(m&&run&&run.key===key&&run.to===c.index-1){run.to=c.index;continue;}
        if(m){run={key,from:c.index,to:c.index,m};runs.push(run);}else run=null;
      }
      for(const r of runs)out.push(`<p class="detail-copy"><b>${esc(data.chapters[0].difficulties[d].name)}</b> · chương ${r.from+1}${r.to>r.from?'–'+(r.to+1):''}: ${fmt(Math.round(r.m.chance*1000)/10)}% · ${r.m.min===r.m.max?fmt(r.m.min):fmt(r.m.min)+'–'+fmt(r.m.max)} cái</p>`);
    }
    return out.join('');
  }
  function showDetail(id,pet=false) {
    const p=pet?data.pets.find(p=>p.id===id):null,it=items[id];
    if(!p&&!it)return;
    currentId=p?'pet:'+id:id;save();
    let html='';
    if(p) {
      const elements={hoa:'Hỏa',thuy:'Thủy',moc:'Mộc',tho:'Thổ',loi:'Lôi',bang:'Băng',gio:'Phong',phong:'Phong'};
      html=`<div class="detail-head">${img(p.forms[0]?.image,'pet-icon',p.rarity)}<div>${badge(p.rarity)}<h2 id="detailTitle">${esc(p.name)}</h2><p class="muted">Hệ ${esc((p.elements||[p.element]).map(e=>elements[e]||e).join(' · '))} · ${esc(roleName(p.role))}</p></div></div><p class="detail-copy">${esc(p.desc)}</p>${stats(p)}<p class="muted">Chỉ số gốc của loài; chỉ số thực tế tăng theo cảnh giới, cấp, tư chất, phẩm và số sao.</p>`;
      html+=block('Thiên phú',`<p class="detail-copy">${esc(p.talentText)}</p>`)+block('Kỹ năng',`<p class="detail-copy">${esc(p.skillText)}</p><p class="muted">Chu kỳ: mỗi ${p.skillEvery} lượt.</p>`)+block('Ba hình thái',`<div class="forms">${p.forms.map((f,i)=>`<div class="form">${img(f.image,'pet-form',p.rarity)}<strong>${esc(f.name)}</strong><p>${esc(f.skillName)}</p><p>Hệ số chia sẻ: ×${f.shareMult}</p></div>`).join('')}</div>`);
      if(p.miracle)html+=block(esc(p.miracle.name),`<p class="detail-copy">${esc(p.miracle.description)}</p>`);
      const q=['common','uncommon','rare','epic','legendary','ancient'].indexOf(p.rarity);
      if(q>=0)html+=block('Ấp linh thú',`<p class="detail-copy">Thời gian ấp trứng phẩm ${esc(qualityName(p.rarity))}: ${fmt(data.petHatchSeconds[q]/60)} phút. Thú đản cùng phẩm có thể nở ra nhiều loài.</p>`);
    } else {
      html=`<div class="detail-head">${img(it.icon,'icon',it.rarity)}<div>${badge(it.rarity)}<h2 id="detailTitle">${esc(it.name)}</h2><p class="muted">${esc(it.kind||typeName(it.type))}${it.slot?` · Cấp ${it.itemLevel} · ${esc(realmName(it.minRealm))}`:''}</p></div></div><p class="detail-copy">${esc(it.desc)}</p>${it.use?`<p class="detail-copy">${esc(it.use)}</p>`:''}${stats(it)}`;
      if(it.special)html+=block('Thuộc tính',`<p class="detail-copy">${esc(it.special)}</p>`);
      if(it.effects?.length)html+=block('Hiệu ứng trang bị',it.effects.map(e=>`<p class="effect"><b>${esc(e.name)}</b> · ${esc(e.text)}</p>`).join(''));
      const set=data.sets.find(s=>s.id===it.setId||s.name===it.set);
      if(set)html+=block('Bộ '+esc(set.name),set.milestones.map(m=>`<p class="effect"><b>${m.count} món:</b> ${m.bonuses.map(b=>esc(b.text)).join(' · ')}</p>`).join(''));
      const b=data.brews.find(b=>b.out===id);
      if(b)html+=block('Đan phương · 1 viên',`<div class="drop-list">${Object.entries(b.in).map(([k,n])=>ingredient(k,n)).join('')}</div><p class="muted">Cảnh giới tối thiểu: ${esc(realmName(it.minRealm))}. Luôn thành công khi đủ nguyên liệu và chỗ trong hành trang.</p>`);
      const recipes=data.brews.filter(b=>Object.hasOwn(b.in,id));
      if(recipes.length)html+=block('Dùng để luyện',`<div class="drop-list">${recipes.map(b=>ingredient(b.out,b.in[id])).join('')}</div><p class="muted">Số lượng hiển thị là lượng nguyên liệu này cần cho mỗi viên.</p>`);
      const boss=bossSources(id);
      if(boss)html+=block('Nguồn nhận · Hạ Boss',boss+'<p class="muted">Mỗi lần hạ Boss gieo tỉ lệ một lần; số lượng ngẫu nhiên trong khoảng.</p>');
      const herbs=data.herbDrops.filter(h=>h.item===id);
      if(herbs.length)html+=block('Thu thập linh thảo',herbs.map(h=>`<p class="detail-copy">Chương ${h.chapters.map(c=>c+1).join(', ')} · từ ải ${h.minArea} · ${h.min}–${h.max} cây · tỉ lệ gốc ${fmt(h.chance*100)}%.</p>`).join('')+'<p class="muted">Ở Khó / Ác mộng: linh thảo của chương cuối, cộng linh thảo bổ sung của đúng chương đó (số lượng nhân hệ số thưởng mức khó). Tỉ lệ thực tế còn phụ thuộc ải.</p>');
      if(it.sell)html+=block('Giá bán',`<p class="detail-copy">${fmt(it.sell)} linh thạch.</p>`);
    }
    $('detailBody').innerHTML=html;
    $('detailBody').parentElement.scrollTop=0;
    if(!$('detail').open)$('detail').showModal();
  }
  document.addEventListener('click',e=>{
    const item=e.target.closest('[data-item]'),pet=e.target.closest('[data-pet]'),pg=e.target.closest('[data-page]');
    if(item)showDetail(item.dataset.item);
    if(pet)showDetail(pet.dataset.pet,true);
    if(pg&&!pg.disabled){page=Number(pg.dataset.page);save();render();$('content').scrollIntoView({block:'start'});}
  });
  $('closeDetail').addEventListener('click',()=>$('detail').close());
  $('detail').addEventListener('click',e=>{if(e.target===$('detail')){const r=$('detail').getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)$('detail').close();}});
  $('detail').addEventListener('close',()=>{currentId='';save();});
  $('filters').addEventListener('submit',e=>e.preventDefault());
  $('search').addEventListener('input',e=>{query=e.target.value;page=1;save();render();});
  $('selects').addEventListener('change',e=>{if(!e.target.dataset.filter)return;filters[e.target.dataset.filter]=e.target.value;page=1;save();render();});
  window.addEventListener('hashchange',route);
  async function load() {
    try {
      const response=await fetch('data/wiki.json');if(!response.ok)throw new Error(response.status);
      data=await response.json();items=Object.fromEntries(data.items.map(i=>[i.id,i]));
      // Drop tables reference gear families; show their first quality variant for lookup.
      for(const it of data.items)if(it.base&&!items[it.base])items[it.base]=it;
      $('summary').innerHTML=`<span><b>${fmt(data.items.filter(i=>i.slot).length)}</b>trang bị</span><span><b>${data.pets.length}</b>linh thú</span><span><b>${data.chapters.length}</b>chương</span><span><b>${data.brews.length}</b>đan phương</span>`;
      route();
    } catch(error) {
      $('sectionTitle').textContent='Chưa tải được dữ liệu';
      $('results').innerHTML='<p class="empty">Không thể tải tàng thư. Hãy kiểm tra kết nối và thử lại.<br><button type="button" class="retry" id="retry">Tải lại</button></p>';
      $('retry').addEventListener('click',load);
      console.error('Wiki data:',error);
    }
  }
  load();
})();
