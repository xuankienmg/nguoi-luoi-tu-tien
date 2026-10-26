/* Linh thú: server-owned state; no local simulation of pets or rewards. */
(() => {
  'use strict';
  const ui = { tab: 'stable', selected: null, element: '', rarity: '', sort: 'power', egg: '', codexElement: '', codexRarity: '', codexOwned: '', release: null, multi: false, picked: new Set(), multiArm: false, merge: null, star: null, starDonors: [null,null], train: null, locks: new Set(), awakenDonor: null, pending: false, error: '', signature: '', state: null, received: 0, timer: null };
  let notice=null,noticeTimer=null;
  function noticeMarkup(){return `<div id="petNotice" class="pet-notice ${notice?.kind==='bad'?'is-error':'is-success'}" role="${notice?.kind==='bad'?'alert':'status'}" aria-live="polite"${notice?'':' hidden'}>${notice?`<button type="button" class="pet-notice-close" data-pet-action="notice-close" aria-label="Đóng thông báo">×</button><b>${notice.kind==='bad'?'Chưa thực hiện được':'Thành công'}</b><span>${esc(notice.message)}</span>`:''}</div>`;}
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const num = v => Number.isFinite(+v) ? +v : 0;
  const f = v => typeof fmt === 'function' ? fmt(num(v)) : String(num(v));
  const state = () => S?.pet || {owned: [], hatch: []};
  const defs = () => DATA?.petDefs || [];
  const definition = p => defs().find(d => d.id === p.defId) || {};
  const labels = {hoa:'Hỏa',thuy:'Thủy',loi:'Lôi',moc:'Mộc',tho:'Thổ',bang:'Băng',gio:'Phong',phong:'Phong',common:'Phàm',uncommon:'Linh',rare:'Huyền',epic:'Địa',legendary:'Thiên',ancient:'Thượng Cổ'};
  const label = v => labels[v] || v || '—';
  const quality = v => ({common:0,uncommon:1,rare:2,epic:3,legendary:4,ancient:5,'Phàm':0,'Linh':1,'Huyền':2,'Địa':3,'Thiên':4,'Thượng Cổ':5}[v] ?? 0);
  const paw = '<svg viewBox="0 0 64 64" aria-hidden="true"><ellipse cx="19" cy="20" rx="6" ry="9"/><ellipse cx="33" cy="14" rx="6" ry="9"/><ellipse cx="47" cy="21" rx="6" ry="9"/><ellipse cx="10" cy="34" rx="5" ry="7"/><path d="M16 46c0-9 10-19 17-19s19 13 19 22c0 11-11 7-19 7s-17 1-17-10Z"/></svg>';
  const glyphs={atk:'<path d="m5 4 12 12m-1-7 5-6-6 5m-5 7-6 6m-1-6 6 6"/>',def:'<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z"/>',hp:'<path d="M12 21 4 13C-3 5 8-1 12 7c4-8 15-2 8 6z"/>',hoa:'<path d="M12 2c3 7-1 8 3 11l3-6c8 15-16 19-14 5l4-6c-1 5 1 7 2 7 2-3 0-6 2-11Z"/>',bang:'<path d="M12 2v20M3 7l18 10M3 17 21 7M8 4l4 3 4-3M8 20l4-3 4 3"/>',moc:'<path d="M4 20C0 4 12 2 21 3c1 12-6 19-17 17Zm0 0L17 7"/>',tho:'<path d="m12 2 8 10-8 10-8-10zM8 12l4-5 4 5-4 5z"/>',loi:'<path d="M13 2 4 14h7l-1 8 10-13h-8z"/>',thuy:'<path d="M12 2C9 8 4 12 4 16c0 10 16 10 16 0 0-4-5-8-8-14Z"/>',gio:'<path d="M2 8h14c8 0 4-9 0-4M2 12h17c8 0 1 10-2 5M2 16h8"/>'};
  const petIcon=key=>`<svg class="pet-symbol" viewBox="0 0 24 24" aria-hidden="true">${glyphs[key]||glyphs.tho}</svg>`;
  const image = (id, form, src, silhouette = false) => `<span class="pet-art${silhouette ? ' pet-unknown' : ''}">${paw}<img src="${esc((src || `img/pet/${encodeURIComponent(id)}_${num(form)}.png`).replace(/(img\/pet\/[^/]+_[0-2])\.png$/,'$1_hd.png'))}" alt="" loading="lazy"></span>`;
  const button = (text, action, disabled = false, cls = '') => `<button type="button" class="pet-btn ${cls}" data-pet-action="${esc(action)}"${disabled || ui.pending ? ' disabled' : ''}>${text}</button>`;
  /** Các hệ của linh thú / loài: Thượng Cổ có hai hệ (elements), loài khác chỉ có hệ chính. */
  const els = p => p?.elements?.length ? p.elements : [p?.element].filter(Boolean);
  const elLabel = p => els(p).map(label).join(' · ') || '—';
  const elMarks = p => els(p).map((e,i)=>`<span class="pet-element-mark${i?' second':''}" data-element="${esc(e)}" title="${esc(label(e))}">${petIcon(e)}</span>`).join('');
  const badge = p => `${els(p).map(e=>`<span class="pet-badge pet-element-badge" data-element="${esc(e)}">${petIcon(e)}${esc(label(e))}</span>`).join('')}<span class="pet-badge pet-quality-${quality(p.rarity)}">${petIcon('tho')}${esc(label(p.rarity))}</span>`;
  const role = p => num(p.slot) === 1 ? 'Xuất chiến' : 'Đang nghỉ';
  const power = p => num(p.power) || num(p.atk) + num(p.def) + num(p.hp);   // lực chiến do server tính (như bảng xếp hạng Linh thú)
  const stars = p => num(p.stars) ? `<span class="pet-stars" title="${num(p.stars)}/5 sao · +${num(p.stars)*6}% chỉ số">${'★'.repeat(num(p.stars))}<i>${'★'.repeat(5-num(p.stars))}</i></span>` : '';
  const QN = ['Phàm','Linh','Huyền','Địa','Thiên'];
  /** Thanh phóng sinh hàng loạt: chọn nhanh theo phẩm (bỏ qua con xuất chiến và con đã có sao), tổng Linh Thú Đan nhận được. */
  function multiBar(owned){
    if(!ui.multi)return '';
    const pick=owned.filter(p=>ui.picked.has(String(p.id))&&!num(p.slot)), gain=pick.reduce((a,p)=>a+num(p.releaseValue),0);
    const byQ=QN.map((n,q)=>[n,q,owned.filter(p=>quality(p.rarity)===q&&!num(p.slot)&&!num(p.stars)).length]).filter(x=>x[2]);
    return `<div class="pet-multi"><div class="pet-multi-pick"><small>Chọn nhanh</small>${byQ.map(([n,q,c])=>button(`${n} ×${c}`,`pick:${q}`)).join('')}${button('Bỏ chọn','pick-clear',!pick.length)}</div>
      <div class="pet-multi-sum"><span>Đã chọn <b>${pick.length}</b> · nhận <b>${f(gain)}</b> Linh Thú Đan</span>${ui.multiArm?`${button(`Xác nhận phóng sinh ${pick.length} con`,'multi-confirm',!pick.length,'pet-danger')}${button('Hủy','multi-cancel')}`:button('Phóng sinh đã chọn','multi-arm',!pick.length,'pet-danger')}</div>
      <small class="pet-multi-note">Nhấp thẻ để chọn / bỏ. Chọn nhanh không lấy con đang xuất chiến và con đã có sao.</small></div>`;
  }
  const starRecipes = () => state().starRecipes || [
    {targetStars:1,sameSpeciesStars:0,otherCount:0,otherStars:0},
    ...[2,3,4,5].map(n=>({targetStars:n,sameSpeciesStars:n-1,otherCount:1,otherStars:n-2}))
  ];
  const starRecipe = p => starRecipes().find(r=>num(r.targetStars)===num(p.stars)+1);
  function donorFits(p,x,slot){
    const r=starRecipe(p);if(!r||String(x.id)===String(p.id)||num(x.slot))return false;
    if(slot===0)return x.defId===p.defId&&num(x.stars)===num(r.sameSpeciesStars);
    const q=quality(p.rarity),dq=quality(x.rarity);
    return num(r.otherCount)>0&&num(x.stars)===num(r.otherStars)&&dq<=q&&dq>=Math.max(0,q-1);
  }
  const chosenDonors = p => ui.starDonors.slice(0,1+num(starRecipe(p)?.otherCount)).map(id=>(state().owned||[]).find(x=>String(x.id)===id));
  const donorsReady = p => {const d=chosenDonors(p);return !!starRecipe(p)&&d.every((x,i)=>x&&donorFits(p,x,i))&&new Set(d.map(x=>x.id)).size===d.length;};
  function mergeBox(p){
    const r=starRecipe(p),head='<h4>Nguyên liệu ghép sao</h4>';
    if(!r)return '<section class="pet-merge">'+head+'<p class="pet-merge-hint">Đã đạt tối đa 5 sao.</p></section>';
    ui.starDonors=ui.starDonors.map((id,i)=>{const x=(state().owned||[]).find(x=>String(x.id)===id);return x&&donorFits(p,x,i)?id:null;});
    const selected=chosenDonors(p),ready=donorsReady(p);
    const slots=Array.from({length:1+num(r.otherCount)},(_,i)=>{
      const list=(state().owned||[]).filter(x=>donorFits(p,x,i)).sort((a,b)=>num(a.level)-num(b.level)||power(a)-power(b));
      const title=i===0?'1 con cùng loài '+r.sameSpeciesStars+'★':'Con bất kỳ · '+r.otherStars+'★ · cùng phẩm hoặc thấp hơn 1 phẩm';
      return '<div class="pet-star-material"><h4>'+title+'</h4><div class="pet-merge-list">'+(list.map(x=>{
        const picked=ui.starDonors[i]===String(x.id),taken=ui.starDonors.some((id,j)=>j!==i&&id===String(x.id));
        return '<button type="button" class="pet-merge-pick'+(picked?' on':'')+'" data-pet-action="star-donor:'+i+':'+esc(x.id)+'" aria-pressed="'+picked+'"'+(ui.pending||taken?' disabled':'')+'>'+image(x.defId,x.form,x.image)+'<span><b>'+esc(x.name)+' · '+num(x.stars)+'★</b><small>Lv.'+f(x.level)+' · '+esc(label(x.rarity))+(picked?' · Đã chọn':taken?' · Đã dùng ở ô khác':'')+'</small></span></button>';
      }).join('')||'<p class="pet-merge-hint">Chưa có linh thú đáp ứng điều kiện.</p>')+'</div></div>';
    }).join('');
    const confirm=ui.merge&&ready?'<div class="pet-star-confirm"><p class="pet-merge-hint">Hiến '+selected.map(x=>'<b>'+esc(x.name)+' '+num(x.stars)+'★ · Lv.'+f(x.level)+' · '+esc(label(x.rarity))+'</b>').join(' + ')+' để lên <b>'+r.targetStars+'★</b>? Các con hiến sẽ biến mất, hoàn nửa Linh Thú Đan đã cho ăn.</p><div class="pet-actions">'+button('Xác nhận ghép','merge-confirm',false,'pet-primary')+button('Hủy','merge-cancel')+'</div></div>':'<div class="pet-actions">'+button('Ghép lên '+r.targetStars+'★','merge-arm',!ready,'pet-primary')+'</div>';
    return '<section class="pet-merge">'+head+'<p class="pet-merge-hint">Chọn đủ '+(1+num(r.otherCount))+' con hiến. Không chọn con đang xuất chiến; mỗi con chỉ dùng một ô.</p>'+slots+confirm+'</section>';
  }
  function remaining(h) {
    if (h.ready) return 0;
    if (h.readyInSeconds != null) return Math.max(0, Math.ceil(num(h.readyInSeconds) - (performance.now()-ui.received)/1000));
    const at = typeof h.readyAt === 'number' ? h.readyAt * (h.readyAt < 1e12 ? 1000 : 1) : Date.parse(h.readyAt);
    return Number.isFinite(at) ? Math.max(0, Math.ceil((at-Date.now())/1000)) : 0;
  }
  const clock = n => n ? `${Math.floor(n/3600) ? Math.floor(n/3600)+':' : ''}${String(Math.floor(n/60)%60).padStart(2,'0')}:${String(n%60).padStart(2,'0')}` : 'Đã nở · có thể nhận';
  function detail(p) {
    if (!p) return `<div class="pet-empty">${paw}<h3>Chưa có linh thú</h3><p>Nhận Thú Đản từ rương Boss hoặc Quay Linh Thú, rồi đặt vào ổ ấp.</p>${button('Đến ổ ấp', 'tab:hatch', false, 'pet-primary')}</div>`;
    const d = definition(p), form = num(p.form), food = num(S.inventory?.linh_thu_dan), cap = num(state().levelCap), maxed = cap > 0 && num(p.level) >= cap;
    const appearance=p.appearanceForm>=0?num(p.appearanceForm):form;
    const rail = [0,1,2].map((i) => `<div class="${i === appearance ? 'current' : i <= form ? 'done' : ''}" title="${esc(d.forms?.[i]?.skillName || '')}"><button type="button" class="pet-form-choice" data-pet-action="appearance:${i}" aria-label="Ngoại hình ${esc(d.forms?.[i]?.name||['Ấu thể','Thành thể','Thần thú'][i])}" aria-pressed="${i===appearance}"${i>form||i===appearance||ui.pending?' disabled':''}>${image(p.defId,i,d.forms?.[i]?.image,i>form)}</button><span>${i <= form ? '✓' : '🔒'}</span><b>${esc(d.forms?.[i]?.name || ['Ấu thể','Thành thể','Thần thú'][i])}</b></div>`).join('');
    const stats = [['Công','atk','aptAtk'],['Thủ','def','aptDef'],['Huyết','hp','aptHp']].map(([label,k,a]) => {
      const raw = num(p[a]), pct = Math.max(0, Math.min(100, raw <= 1 ? raw*100 : raw));
      return `<div class="pet-stat"><span>${petIcon(k)}${label}</span><strong>${f(p[k])}</strong><small>Tư chất ${Math.round(pct)}%</small><meter min="0" max="100" value="${pct}">${pct}%</meter></div>`;
    }).join('');
    const slots = [button(num(p.slot) === 1 ? 'Đang xuất chiến' : 'Xuất chiến', 'slot:1', num(p.slot) === 1, 'pet-primary'), button('Bồi dưỡng', `train-open:${p.id}`)];
    if (num(p.slot)) slots.push(button('Nghỉ', 'slot:0'));
    return `<div class="pet-detail-head"><div><h3>${esc(p.name || d.name)} ${stars(p)}${num(p.training?.awaken)?` <em class="pet-awaken-tag">${esc(p.training.awakenName)}</em>`:''}</h3><p>${esc(p.formName || d.forms?.[form]?.name)} · Cấp ${f(p.level)}${cap ? '/'+f(cap) : ''}</p></div><div>${badge(p)}</div></div>
      <div class="pet-portrait">${image(p.defId,form,p.image)}${num(p.slot)===1?`<div class="pet-active-emblem" role="img" aria-label="Linh thú đang xuất chiến" title="Đang xuất chiến">${petIcon('atk')}<b>Xuất chiến</b></div>`:''}<span>${esc(role(p))}</span></div>
      <div class="pet-growth"><div class="pet-exp"><h4>Cấp ${f(p.level)} <small>/ ${f(cap)}</small></h4><span>${f(p.exp)} / ${f(p.expNeed)}</span><progress value="${num(p.exp)}" max="${Math.max(1,num(p.expNeed))}"></progress></div><h4>Tiến hóa</h4><div class="pet-evolution" aria-label="Tiến trình tiến hóa">${rail}</div></div>
      <div class="pet-stats">${stats}</div>
      <section class="pet-bonus"><h4>Cộng cho chủ nhân</h4><div><span>${petIcon("atk")}Công <b>+${f(p.shareAtk)}</b></span><span>${petIcon("def")}Thủ <b>+${f(p.shareDef)}</b></span><span>${petIcon("hp")}Huyết <b>+${f(p.shareHp)}</b></span></div><small>${num(p.slot) ? 'Phần cộng hiện tại do máy chủ tính.' : 'Cho xuất chiến hoặc hộ trận để nhận phần cộng.'}</small></section>
      <section class="pet-lore"><h4>Thiên phú</h4><p>${esc(p.talentText || d.talentText || 'Chưa có thông tin.')}</p><h4 class="pet-skill-summary"><span>${esc(d.forms?.[form]?.skillName || 'Kỹ năng')}</span><small>Thi triển mỗi ${f(p.skillEvery || d.skillEvery)} lượt</small></h4><p class="pet-skill-description">${esc(p.skillText || d.skillText || 'Chưa có thông tin kỹ năng.')}</p></section>
      ${p.miracle?`<section class="pet-miracle"><h4>Thần thông <small>1 lần mỗi trận</small></h4><b>${esc(p.miracle.name)}</b><p>${esc(p.miracle.description)}</p></section>`:''}
      <div class="pet-actions">${slots.join('')}</div>
      <section class="pet-feed"><h4>Cho ăn <small>Linh Thú Đan: ${f(food)}${maxed ? ' · Đã đạt giới hạn cấp' : ''}</small></h4><div class="pet-actions">${button('×1','feed:1',!food||maxed)}${button('×10','feed:10',food<10||maxed)}${button('×100','feed:100',food<100||maxed)}${button('Tối đa','feed:0',!food||maxed)}</div></section>
      <section class="pet-evolve"><h4>Tiến hóa</h4><p class="pet-evolve-hint">${esc(p.evolveHint || (form>=2?'Đã mở hình thái cuối.':form===0?'Yêu cầu: linh thú cấp 30 và đủ nguyên liệu.':'Yêu cầu: linh thú cấp 60, nhân vật Nguyên Anh và đủ nguyên liệu.'))}</p><div class="pet-costs">${(p.evolveCost || []).map(c => `<span class="${num(c.have)<num(c.need)?'pet-short':''}">${esc(ITEM[c.item]?.name || c.item)} <b>${f(c.have)}/${f(c.need)}</b></span>`).join('')}</div>${form>0?'<small>Chọn hình thái đã mở ở trên để đổi ngoại hình, giữ nguyên sức mạnh.</small>':''}${button(form>=2?'Đã tiến hóa tối đa':'Tiến hóa','evolve',form>=2,'pet-primary')}</section>
      <div class="pet-release">${ui.release === String(p.id) ? `<p>Phóng sinh ${esc(p.name || d.name)}? Linh thú sẽ rời chuồng, nhận <b>${f(p.releaseValue)}</b> Linh Thú Đan.</p>${button('Xác nhận phóng sinh','release-confirm',false,'pet-danger')}${button('Giữ lại','release-cancel')}` : button(`Phóng sinh · +${f(p.releaseValue)} Linh Thú Đan`,'release-arm',false,'pet-text')}</div>`;
  }
  function layoutStable(root) {
    const collection=root.querySelector('.pet-collection'), detail=root.querySelector('.pet-detail');
    if(!collection||!detail)return;
    collection.querySelector('h3').textContent='Chuồng thú';
    collection.querySelectorAll('.pet-card').forEach(card=>{const p=(state().owned||[]).find(p=>String(p.id)===card.dataset.petSelect);if(p){card.dataset.element=p.element;card.classList.toggle('pet-ancient',quality(p.rarity)===5);}});
    const stats=detail.querySelector('.pet-stats');
    if(!stats)return;
    const aptitude=document.createElement('section');aptitude.className='pet-aptitude';aptitude.innerHTML='<h4>Tư chất</h4>';
    const attributes=document.createElement('section');attributes.className='pet-attributes';attributes.innerHTML='<h4>Thuộc tính</h4>';
    const bonus=detail.querySelector('.pet-bonus'),talent=detail.querySelector('.pet-lore>p');
    if(bonus){
      const info=document.createElement('button');info.type='button';info.className='pet-owner-info';
      info.textContent='ⓘ';info.setAttribute('aria-label','Cộng cho chủ nhân');
      info.dataset.tip=['Cộng cho chủ nhân',...[...bonus.querySelectorAll('div>span')].map(el=>el.textContent),bonus.querySelector('small')?.textContent,talent?'Thiên phú: '+talent.textContent:''].filter(Boolean).map(esc).join('\n');
      attributes.querySelector('h4').append(info);bonus.remove();
    }
    stats.querySelectorAll('.pet-stat').forEach(stat=>{
      const name=stat.querySelector('span'),value=stat.querySelector('strong'),pct=stat.querySelector('small'),meter=stat.querySelector('meter');
      const row=document.createElement('div');row.className='pet-attribute-row';row.append(name,value);attributes.append(row);
      pct.textContent=Math.round(num(meter.value))+'%';
      const apt=document.createElement('div');apt.className='pet-aptitude-row';apt.append(name.cloneNode(true),meter,pct);aptitude.append(apt);
    });
    stats.replaceChildren(attributes,aptitude);
    const actions=detail.querySelector('.pet-actions'),feed=detail.querySelector('.pet-feed .pet-actions'),evolve=detail.querySelector('.pet-evolve');
    feed?.querySelectorAll('button').forEach(b=>{b.textContent='Cho ăn '+b.textContent;actions.append(b);});
    const evolveButton=evolve?.querySelector('button');if(evolveButton){evolveButton.title=evolve.querySelector('.pet-evolve-hint')?.textContent||'';actions.append(evolveButton);}
    const lore=detail.querySelector('.pet-lore');
    if(lore){
      if(talent){talent.remove();lore.querySelector('h4')?.remove();}
      lore.insertAdjacentHTML('afterbegin','<h4 class="pet-skill-title">Kỹ năng linh thú</h4>');
      const rules=document.createElement('button');rules.type='button';rules.className='pet-owner-info pet-rules-info';rules.textContent='ⓘ';rules.setAttribute('aria-label','Luật kỹ năng linh thú');
      rules.dataset.tip='Mỗi sao tăng 6% chỉ số, kỹ năng và thiên phú.\nSát thương tính theo công chủ nhân; không bạo kích hoặc kích hoạt liên kích.\nKhiên linh thú tối đa 30% máu; hiệu ứng cùng loại chỉ làm mới thời gian.';
      lore.querySelector('.pet-skill-title')?.append(rules);
    }
    const selected=(state().owned||[]).find(p=>String(p.id)===ui.selected);
    detail.querySelector('.pet-detail-head>div:last-child')?.insertAdjacentHTML('beforeend',`<span class="pet-badge pet-form-badge" data-form="${num(selected?.form)}">${paw}${esc(selected?.formName||['Ấu thể','Thành thể','Thần thú'][num(selected?.form)])}</span>`);
  }
  /** Chọn con chính và trọn bộ linh thú hiến theo công thức sao do server gửi. */
  function starTab(){
    const owned=state().owned||[], groups={};
    owned.forEach(p=>(groups[p.defId]=groups[p.defId]||[]).push(p));
    const species=Object.values(groups).map(g=>g.sort((a,b)=>num(b.stars)-num(a.stars)||num(b.level)-num(a.level)||power(b)-power(a)))
      .sort((a,b)=>b.length-a.length||quality(b[0].rarity)-quality(a[0].rarity));
    if(!owned.some(p=>String(p.id)===ui.star)){const g=species.find(g=>g.length>1&&num(g[0].stars)<5)||species[0];ui.star=g?String(g[0].id):null;ui.merge=null;ui.starDonors=[null,null];}
    const main=owned.find(p=>String(p.id)===ui.star);
    const left=`<section class="pet-star-list"><div class="pet-section-heading"><h4>Chọn linh thú chính</h4><small>Loài có nhiều con nằm trên</small></div>${species.map(g=>`<div class="pet-star-group"><p><b>${esc(g[0].name)}</b><small>${g.length} con</small></p><div class="pet-star-row">${g.map(p=>`<button type="button" class="pet-star-pick${String(p.id)===ui.star?' on':''}" data-pet-action="star-main:${esc(p.id)}" aria-pressed="${String(p.id)===ui.star}"${ui.pending?' disabled':''}>${image(p.defId,p.form,p.image)}<span><b>Lv.${f(p.level)} ${esc(label(p.rarity))}</b>${num(p.stars)?stars(p):'<small>0★</small>'}${num(p.slot)?'<small class="pet-star-active">Xuất chiến</small>':''}</span></button>`).join('')}</div></div>`).join('')||'<p class="pet-merge-hint">Chuồng đang trống.</p>'}</section>`;
    if(!main)return `<div class="pet-star">${left}<section class="pet-star-main"><div class="pet-empty">${paw}<h3>Chưa có linh thú</h3></div></section></div>`;
    const st=num(main.stars), next=Math.min(5,st+1), k=(1+next*.06)/(1+st*.06);
    const row=(n,key)=>`<div class="pet-star-stat"><span>${petIcon(key)}${n}</span><b>${f(main[key])}</b>${st<5?`<i>→</i><b class="up">${f(Math.round(num(main[key])*k))}</b>`:''}</div>`;
    const right=`<section class="pet-star-main"><div class="pet-star-head">${image(main.defId,main.form,main.image)}<div><h3>${esc(main.name)}</h3><p>${esc(label(main.rarity))} · ${esc(main.formName||'')} · Cấp ${f(main.level)}</p><div class="pet-star-big">${'★'.repeat(st)}<i>${'★'.repeat(5-st)}</i></div><small>Mỗi sao +6% chỉ số, kỹ năng và thiên phú · ${st}/5 sao (+${st*6}%)</small></div></div>
      <div class="pet-star-stats"><h4>${st<5?`Chỉ số khi lên ${next}★`:'Chỉ số (đã tối đa 5★)'}</h4>${row('Công','atk')}${row('Thủ','def')}${row('Huyết','hp')}</div>
      ${st<5&&(main.nextStarSkillText||main.nextStarTalentText)?`<section class="pet-star-effects"><h4>Khi lên ${next}★</h4><p><b>Kỹ năng:</b> ${esc(main.nextStarSkillText||'')}</p><p><b>Thiên phú:</b> ${esc(main.nextStarTalentText||'')}</p></section>`:''}
      ${mergeBox(main)}
    </section>`;
    return `<div class="pet-star">${left}${right}</div>`;
  }
  /** Bồi dưỡng: tư chất (tẩy luyện có khóa / bồi linh), trang bị linh thú, thức tỉnh huyết mạch, cộng hưởng ngũ hành. Luật và số do server gửi. */
  const RULES = () => state().training || {slots:[],tierNames:[],gear:[],fuseCount:3,fuseStones:[],maxLocks:2,maxAwaken:3,awakenBonus:.1,resonanceSame:.12,resonanceSupport:.05};
  const have = k => num(S.inventory?.[k]);
  const itemName = k => ITEM[k]?.name || k;
  const itemIcon = k => k && ITEM[k] && typeof icon === 'function' ? icon(k) : '';
  const pctTxt = v => (Math.round(num(v)*1000)/10).toLocaleString('vi-VN') + '%';
  const APT = [['atk','Công','aptAtk'],['def','Thủ','aptDef'],['hp','Huyết','aptHp']];
  function resonanceBox(p){
    const t=p.training||{}, R=RULES(), el=t.manualElement;
    const parts=t.resonanceParts||[];
    const partTxt=x=>x.kind==='same'?`${esc(label(x.element))}: cùng hệ +${pctTxt(x.pct)}`:x.kind==='support'?`${esc(label(x.element))}: tương sinh +${pctTxt(x.pct)}`:`${esc(label(x.element))}: không cộng hưởng`;
    const txt = parts.length>1 ? `Hai hệ cộng dồn với công pháp ${esc(label(el))} · ${parts.map(partTxt).join(' · ')} · Tổng <b>+${pctTxt(t.resonance)}</b> chỉ số`
      : t.resonanceKind==='same' ? `Cùng hệ công pháp đang kích hoạt (${esc(label(el))}): <b>+${pctTxt(t.resonance)}</b> chỉ số`
      : t.resonanceKind==='support' ? `Tương sinh với công pháp ${esc(label(el))}: <b>+${pctTxt(t.resonance)}</b> chỉ số`
      : `Không cộng hưởng với công pháp ${esc(label(el))}. Cùng hệ +${pctTxt(R.resonanceSame)}, tương sinh +${pctTxt(R.resonanceSupport)}.`;
    return `<section class="pet-train-box pet-resonance ${t.resonanceKind?'on':''}"><h4>Cộng hưởng ngũ hành</h4><p>${txt}</p><small>Vòng tương sinh: Mộc/Phong → Hỏa → Thổ → Lôi → Thủy/Băng → Mộc/Phong. Đổi công pháp kích hoạt ở tab Kỹ năng.</small></section>`;
  }
  /** Tooltip cách nhận đan bồi dưỡng (chuỗi từ server: PetTrainingRules.Sources). */
  function srcTip(id,name){ const s=RULES().sources?.[id]; return esc(name) + (s ? '&#10;' + esc(s) : ''); }
  function aptBox(p){
    const t=p.training||{}, R=RULES(), locks=[...ui.locks].filter(k=>APT.some(a=>a[0]===k));
    const pills=1+locks.length, stones=num(t.rerollStones), reroll=have('hoan_cot_dan');
    const rows=APT.map(([k,n,a])=>{const v=num(p[a]),max=num(t.aptMax),lock=ui.locks.has(k),full=v>=max;
      return `<div class="pet-train-apt"><span>${petIcon(k)}${n}</span><meter min="0" max="${max||100}" value="${v}"></meter><b>${v}<small>/${max}</small></b>
        <div class="pet-train-apt-btns"><button type="button" class="pet-btn pet-lock${lock?' on':''}" data-pet-action="lock:${k}" aria-pressed="${lock}"${ui.pending||(!lock&&locks.length>=num(R.maxLocks))?' disabled':''}>${lock?'Đã khóa':'Khóa'}</button></div></div>`;}).join('');
    return `<section class="pet-train-box"><h4>Tư chất <small>Khoảng của phẩm: ${num(t.aptMin)}–${num(t.aptMax)}</small></h4>${rows}
      <div class="pet-costs"><span class="${reroll<pills?'pet-short':''}" data-tip="${srcTip('hoan_cot_dan','Hoán Cốt Đan')}">Hoán Cốt Đan <b>${f(reroll)}/${pills}</b></span><span class="${have('linh_thach')<stones?'pet-short':''}">Linh thạch <b>${f(stones)}</b></span></div>
      <div class="pet-actions">${button(`Tẩy luyện${locks.length?` (giữ ${locks.length})`:''}`,'reroll',reroll<pills||have('linh_thach')<stones,'pet-primary')}</div>
      <small>Tẩy luyện random lại tư chất chưa khóa trong khoảng của phẩm (khóa tối đa ${num(R.maxLocks)}, mỗi tư chất khóa thêm 1 Hoán Cốt Đan). Tư chất tăng chỉ số theo cấp linh thú.</small></section>`;
  }
  function gearBox(p){
    const t=p.training||{}, R=RULES(), info=id=>(R.gear||[]).find(g=>g.id===id);
    const slots=(R.slots||[]).map(s=>{const id=t.gear?.[s.id]||'',g=info(id);
      return `<div class="pet-gear-slot${id?' on':''}"><span class="pet-gear-ic">${itemIcon(id)||'<i>＋</i>'}</span><span><b>${esc(s.name)}</b><small>${id?`${esc(itemName(id))} · +${g?g.pct:0}% ${esc(s.statName)}`:`Trống · cộng % ${esc(s.statName)}`}</small></span>${id?button('Tháo',`ungear:${s.id}`):''}</div>`;}).join('');
    const bag=(R.gear||[]).filter(g=>have(g.id)>0).sort((a,b)=>a.slot.localeCompare(b.slot)||b.tier-a.tier);
    const list=bag.map(g=>{const s=(R.slots||[]).find(x=>x.id===g.slot)||{},n=have(g.id);
      return `<div class="pet-gear-item">${itemIcon(g.id)}<span><b>${esc(itemName(g.id))} ×${f(n)}</b><small>${esc(s.name||'')} · +${g.pct}% ${esc(s.statName||'')}</small></span>${button('Mặc',`gear:${g.id}`,t.gear?.[g.slot]===g.id)}</div>`;}).join('');
    return `<section class="pet-train-box"><h4>Trang bị linh thú <small>Công +${num(t.gearPct?.atk)}% · Thủ +${num(t.gearPct?.def)}% · Huyết +${num(t.gearPct?.hp)}%</small></h4><div class="pet-gear-slots">${slots}</div>
      <div class="pet-gear-bag">${list||'<p class="pet-merge-hint">Chưa có trang bị linh thú. Rơi ngẫu nhiên khi hạ Boss (ô và phẩm ngẫu nhiên, tối đa Thiên).</p>'}</div>
      <div class="pet-actions">${button(`Hợp thành ${num(R.fuseCount)}→1 (Luyện chế › Linh thú)`,'petfuse',false,'pet-text')}</div><small>Phẩm ${esc((R.tierNames||[]).at(-1)||'Thánh')} không rơi ở map, chỉ nhận từ hợp thành.</small></section>`;
  }
  function awakenBox(p){
    const t=p.training||{}, R=RULES(), lv=num(t.awaken), max=num(R.maxAwaken)||3, need=num(t.awakenDonorStars);
    if(ui.awakenDonor&&!(state().owned||[]).some(x=>String(x.id)===ui.awakenDonor&&x.defId===p.defId&&!num(x.slot)&&num(x.stars)>=need&&String(x.id)!==String(p.id)))ui.awakenDonor=null;
    const donors=(state().owned||[]).filter(x=>String(x.id)!==String(p.id)&&x.defId===p.defId&&!num(x.slot)&&num(x.stars)>=need).sort((a,b)=>num(a.stars)-num(b.stars)||num(a.level)-num(b.level));
    const pips=Array.from({length:max},(_,i)=>`<i class="${i<lv?'on':''}">◆</i>`).join('');
    const pay=(t.awakenCost||[]).every(c=>num(c.have)>=num(c.need));
    const body=lv>=max?`<p class="pet-merge-hint">${esc(t.awakenHint||'')}</p>`:`<p class="pet-merge-hint">${esc(t.awakenHint||'')}</p>
      <div class="pet-costs">${(t.awakenCost||[]).map(c=>`<span class="${num(c.have)<num(c.need)?'pet-short':''}">${esc(itemName(c.item))} <b>${f(c.have)}/${f(c.need)}</b></span>`).join('')}</div>
      ${t.awakenGate?`<p class="pet-train-sub">Con hiến: cùng loài, từ ${need}★, đang nghỉ</p><div class="pet-merge-list">${donors.map(x=>{const on=ui.awakenDonor===String(x.id);return `<button type="button" class="pet-merge-pick${on?' on':''}" data-pet-action="awaken-donor:${esc(x.id)}" aria-pressed="${on}"${ui.pending?' disabled':''}>${image(x.defId,x.form,x.image)}<span><b>${esc(x.name)} · ${num(x.stars)}★</b><small>Lv.${f(x.level)} · ${esc(label(x.rarity))}${on?' · Đã chọn':''}</small></span></button>`;}).join('')||'<p class="pet-merge-hint">Chưa có con hiến phù hợp.</p>'}</div>
      <div class="pet-actions">${button(`Thức tỉnh lên ${lv+1}/${max}`,'awaken',!ui.awakenDonor||!pay,'pet-primary')}</div>`:''}`;
    return `<section class="pet-train-box pet-awaken-box"><h4>Thức tỉnh huyết mạch <span class="pet-awaken-pips">${pips}</span></h4><p>Sau 5★, mỗi cấp thức tỉnh +${Math.round(num(R.awakenBonus)*100)}% chỉ số (kỹ năng mạnh lên theo Linh Uy). Hiện tại: <b>+${Math.round(lv*num(R.awakenBonus)*100)}%</b>.</p>${body}<small>Con hiến sẽ biến mất, trang bị của nó về túi, hoàn nửa Linh Thú Đan đã cho ăn.</small></section>`;
  }
  function trainTab(){
    const owned=[...(state().owned||[])].sort((a,b)=>num(b.slot)-num(a.slot)||power(b)-power(a));
    if(!owned.some(p=>String(p.id)===ui.train))ui.train=ui.selected&&owned.some(p=>String(p.id)===ui.selected)?ui.selected:owned[0]?String(owned[0].id):null;
    const main=owned.find(p=>String(p.id)===ui.train);
    const left=`<section class="pet-star-list"><div class="pet-section-heading"><h4>Chọn linh thú</h4><small>Con xuất chiến nằm trên</small></div><div class="pet-train-pets">${owned.map(p=>`<button type="button" class="pet-star-pick${String(p.id)===ui.train?' on':''}" data-pet-action="train-pick:${esc(p.id)}" aria-pressed="${String(p.id)===ui.train}"${ui.pending?' disabled':''}>${image(p.defId,p.form,p.image)}<span><b>${esc(p.name)} ${num(p.training?.awaken)?`<em class="pet-awaken-tag">${esc(p.training.awakenName)}</em>`:''}</b><small>Lv.${f(p.level)} · ${esc(label(p.rarity))} · ${num(p.stars)}★${num(p.slot)?' · Xuất chiến':''}</small></span></button>`).join('')}</div></section>`;
    if(!main)return `<div class="pet-star">${left}<section class="pet-star-main"><div class="pet-empty">${paw}<h3>Chưa có linh thú</h3></div></section></div>`;
    const right=`<section class="pet-star-main pet-train"><div class="pet-star-head">${image(main.defId,main.form,main.image)}<div><h3>${esc(main.name)} ${stars(main)}</h3><p>${esc(elLabel(main))} · ${esc(label(main.rarity))} · ${esc(main.formName||'')} · Cấp ${f(main.level)}</p><div class="pet-train-stats"><span>${petIcon('atk')}Công <b>${f(main.atk)}</b></span><span>${petIcon('def')}Thủ <b>${f(main.def)}</b></span><span>${petIcon('hp')}Huyết <b>${f(main.hp)}</b></span></div></div></div>
      ${resonanceBox(main)}${aptBox(main)}${gearBox(main)}${awakenBox(main)}</section>`;
    return `<div class="pet-star">${left}${right}</div>`;
  }
  function stable() {
    const owned = state().owned || [], list = owned.filter(p => (!ui.element || els(p).includes(ui.element)) && (!ui.rarity || p.rarity === ui.rarity));
    list.sort((a,b) => ui.sort === 'name' ? String(a.name).localeCompare(String(b.name),'vi') : ui.sort === 'power' ? power(b)-power(a) : ui.sort === 'rarity' ? quality(b.rarity)-quality(a.rarity) : num(b.level)-num(a.level));
    if (!list.some(p => String(p.id) === ui.selected)) { ui.selected = list.length ? String(list[0].id) : null; ui.release = null; }
    const options = (key,caption,value) => `<select data-pet-filter="${key}" aria-label="${caption}"><option value="">${caption}</option>${[...new Set(owned.flatMap(p=>key==='element'?els(p):[p[key]]).filter(Boolean))].map(v=>`<option${v===value?' selected':''} value="${esc(v)}">${esc(label(v))}</option>`).join('')}</select>`;
    return `<div class="pet-stable"><section class="pet-collection"><div class="pet-collection-head"><h3>Linh thú của bạn</h3><span>${owned.length}/${f(state().capacity)}</span>${button(ui.multi?'Xong':'Phóng sinh hàng loạt','multi',false,'pet-text pet-multi-toggle')}</div>${multiBar(owned)}<div class="pet-filters">${options('element','Mọi hệ',ui.element)}${options('rarity','Mọi phẩm',ui.rarity)}<select data-pet-filter="sort" aria-label="Sắp xếp">${[['power','Lực chiến'],['level','Cấp cao'],['rarity','Phẩm cao'],['name','Tên']].map(([k,v])=>`<option value="${k}"${ui.sort===k?' selected':''}>${v}</option>`).join('')}</select></div><div class="pet-cards">${list.map(p=>`<button type="button" class="pet-card${String(p.id)===ui.selected&&!ui.multi?' selected':''}${ui.multi&&ui.picked.has(String(p.id))?' picked':''}${ui.multi&&num(p.slot)?' unpickable':''}" data-pet-select="${esc(p.id)}" aria-pressed="${String(p.id)===ui.selected}">${elMarks(p)}<span class="pet-card-level">Lv.${f(p.level)}</span>${stars(p)}${image(p.defId,p.form,p.image)}<strong>${esc(p.name || definition(p).name)}</strong><small>${esc(elLabel(p))} · ${esc(label(p.rarity))} · Cấp ${f(p.level)}</small><span class="pet-slot${num(p.slot)?' active':''}">${esc(role(p))}</span></button>`).join('') || `<p class="pet-list-empty">${owned.length ? 'Không có linh thú khớp bộ lọc.' : 'Chuồng đang trống. Hãy ấp Thú Đản đầu tiên.'}</p>`}</div></section><section class="pet-detail" aria-label="Chi tiết linh thú">${list.length || !owned.length ? detail(list.find(p=>String(p.id)===ui.selected)) : '<div class="pet-empty"><p>Đổi bộ lọc để chọn linh thú.</p></div>'}</section></div>`;
  }
  function hatch() {
    const p = state(), occupied = (p.hatch || []).filter(h=>h.egg), count = Math.max(num(p.hatchSlots),occupied.length), full = (p.owned || []).length >= num(p.capacity);
    const eggs = Object.entries(S.inventory || {}).filter(([id,n])=>n>0 && (ITEM[id]?.type === 'egg' || id.startsWith('thu_dan_')));
    if(!eggs.some(([id])=>id===ui.egg))ui.egg=eggs[0]?.[0]||'';
    const eggArt=(id,detail=false)=>`<img class="${id==='thu_dan_thuong_co'?'ancient-egg-art':''}" src="${esc(detail&&id==='thu_dan_thuong_co'?'img/pet/egg_thuong_co_hd.png':ITEM[id]?.icon||`img/pet/egg_${id.replace('thu_dan_','')}.png`)}" alt="">`;
    const ready=occupied.filter(h=>remaining(h)===0).length;
    return `<div class="pet-hatch"><header class="pet-page-heading"><div><span class="pet-eyebrow">DƯỠNG DỤC LINH THÚ</span><h3>Ấp trứng</h3><p>Gieo một mối duyên, đón bạn đồng hành.</p></div><div class="pet-summary"><span><b>${occupied.length}/${count}</b> ổ đang ấp</span><span><b>${ready}</b> có thể nhận</span><span><b>${(p.owned||[]).length}/${f(p.capacity)}</b> chuồng thú</span></div></header><div class="pet-hatch-layout"><section class="pet-incubator"><div class="pet-section-heading"><h4>Đài ấp linh thú</h4><small>Tiếp tục ấp khi bạn rời đi</small></div><div class="pet-nests">${Array.from({length:Math.max(4,count)},(_,slot)=>{
      const h = occupied.find(x=>num(x.slot)===slot);
      const locked=slot>=count;
      return `<section class="pet-nest ${locked?'is-locked':h?'is-incubating':'is-empty'}"><div class="pet-nest-heading"><h4>Ổ ấp ${slot+1}</h4><span>${locked?'Chưa mở':h?(remaining(h)===0?'Đã nở':'Đang ấp'):'Sẵn sàng'}</span></div>${locked?`<div class="pet-nest-stage"><span class="pet-nest-lock">♙</span></div><b>Ổ ấp chưa mở</b><small>Tăng cảnh giới để mở thêm ổ ấp.</small>`:h ? `<div class="pet-nest-stage"><div class="pet-egg-art">${eggArt(h.egg,true)}</div></div><b>${esc(ITEM[h.egg]?.name || h.egg)}</b><p class="pet-countdown" data-pet-timer="${slot}"></p><div class="pet-nest-actions">${button('Nhận linh thú',`collect:${slot}`,remaining(h)>0||full,'pet-primary pet-collect')}${button(`Ấp ngay · ${f(h.instantCost)} linh thạch`,`now:${slot}`,remaining(h)===0||num(S.inventory?.linh_thach)<num(h.instantCost),'pet-now')}</div>${full?'<small class="pet-short">Chuồng đã đầy. Phóng sinh để có chỗ nhận.</small>':''}` : `<div class="pet-nest-stage">${ui.egg?`<div class="pet-egg-art pet-egg-preview">${eggArt(ui.egg,true)}</div>`:paw}</div><b>${ui.egg?esc(ITEM[ui.egg]?.name||ui.egg):'Chờ một mối duyên'}</b><small>${ui.egg?'Trứng đã chọn từ kho bên cạnh':'Nhận Thú Đản từ rương Boss hoặc Quay Linh Thú.'}</small>${button('Bắt đầu ấp',`hatch:${slot}:${ui.egg}`,full||!ui.egg,'pet-primary')}`}</section>`;
    }).join('')}</div><p class="pet-hatch-note">Chọn trứng trong kho, sau đó chọn ổ trống để bắt đầu ấp. Linh thú mới nhận sẽ xuất hiện trong Chuồng thú.</p></section><aside class="pet-egg-vault"><div class="pet-section-heading"><h4>Thú Đản trong túi</h4><span>${f(eggs.reduce((sum,[,n])=>sum+num(n),0))} trứng</span></div><div class="pet-egg-list">${eggs.map(([id,n])=>`<button type="button" class="pet-egg-choice ${ui.egg===id?'selected':''}" data-pet-action="egg:${esc(id)}" aria-pressed="${ui.egg===id}">${eggArt(id)}<span><b>${esc(ITEM[id]?.name||id)}</b><small>${ui.egg===id?'Đang chọn':'Chọn để đặt vào ổ'}</small></span><strong>×${f(n)}</strong></button>`).join('')||'<div class="pet-vault-empty"><span>◇</span><b>Kho trứng đang trống</b><p>Nhận Thú Đản từ rương Boss hoặc Quay Linh Thú.</p></div>'}</div><div class="pet-vault-help"><h4>Một hành trình mới</h4><p>Mỗi Thú Đản mang đến một linh thú. Giữ chỗ trống trong chuồng để nhận khi trứng nở.</p></div></aside></div></div>`;
  }
  function codex() {
    const owned = state().owned || [], known = new Set(owned.map(p=>p.defId));
    const list=defs().filter(d=>(!ui.codexElement||els(d).includes(ui.codexElement))&&(!ui.codexRarity||d.rarity===ui.codexRarity)&&(!ui.codexOwned||(ui.codexOwned==='owned')===known.has(d.id)));
    const filter=(key,caption,values)=>`<select data-pet-filter="${key}" aria-label="${caption}"><option value="">${caption}</option>${values.map(v=>`<option value="${esc(v)}"${ui[key]===v?' selected':''}>${esc(label(v))}</option>`).join('')}</select>`;
    return `<div class="pet-codex"><header class="pet-page-heading"><div><span class="pet-eyebrow">VẠN LINH ĐỒ GIÁM</span><h3>Thú điển</h3><p>Khám phá các loài linh thú và ba hình thái trưởng thành.</p></div><div class="pet-codex-progress"><b>${known.size}<small> / ${defs().length}</small></b><span>loài đang sở hữu</span><progress value="${known.size}" max="${Math.max(1,defs().length)}"></progress></div></header><div class="pet-codex-toolbar">${filter('codexElement','Tất cả hệ',[...new Set(defs().flatMap(els))])}${filter('codexRarity','Tất cả phẩm',[...new Set(defs().map(d=>d.rarity))])}<select data-pet-filter="codexOwned" aria-label="Trạng thái sở hữu"><option value="">Tất cả trạng thái</option><option value="owned"${ui.codexOwned==='owned'?' selected':''}>Đang sở hữu</option><option value="missing"${ui.codexOwned==='missing'?' selected':''}>Chưa sở hữu</option></select><span>${list.length} loài</span></div><div class="pet-species-grid">${list.map(d=>`<section class="pet-species ${known.has(d.id)?'is-known':'is-undiscovered'}"><header><div><h4>${esc(d.name)}</h4><div class="pet-species-badges">${badge(d)}</div></div><span class="pet-discovery">${known.has(d.id)?'✓ Đang sở hữu':'Chưa sở hữu'}</span></header><div class="pet-forms">${[0,1,2].map(i=>{const unlocked=owned.some(p=>p.defId===d.id&&num(p.form)>=i);return `<div class="${unlocked?'unlocked':'locked'}">${image(d.id,i,d.forms?.[i]?.image,!unlocked)}<span>${esc(d.forms?.[i]?.name || ['Ấu thể','Thành thể','Thần thú'][i])}</span><small>${unlocked?'✓ Đã đạt':'Chưa đạt'}</small></div>`;}).join('')}</div><footer><p>${esc(d.desc || d.role || 'Ấp Thú Đản để kết duyên cùng linh thú này.')}</p>${d.talentText?`<span class="pet-species-talent">✦ ${esc(d.talentText)}</span>`:''}${d.miracle?`<p class="pet-codex-miracle"><b>Thần thông · ${esc(d.miracle.name)}</b><br>${esc(d.miracle.description)}</p>`:''}</footer></section>`).join('')||'<div class="pet-vault-empty"><b>Không có loài phù hợp</b><p>Thử thay đổi bộ lọc để xem thêm linh thú.</p></div>'}</div></div>`;
  }
  function tick() {
    const root = document.getElementById('pane-pet');
    const p = state(), hatches = p.hatch || [];
    document.getElementById('btnPet')?.classList.toggle('ready',hatches.some(h=>h.egg&&remaining(h)===0)||(p.owned||[]).some(p=>p.canEvolve));
    if (!root) return;
    root.querySelectorAll('[data-pet-timer]').forEach(el=>{
      const h=hatches.find(h=>num(h.slot)===num(el.dataset.petTimer)); if (!h) return;
      const left=remaining(h); el.textContent=clock(left);
      const nest=el.closest('.pet-nest');
      nest.querySelector('.pet-collect').disabled=ui.pending||left>0||(p.owned||[]).length>=num(p.capacity);
      nest.querySelector('.pet-now').disabled=ui.pending||left===0||num(S.inventory?.linh_thach)<num(h.instantCost);
    });
  }
  async function send(path) {
    if (ui.pending || busy) return;
    const previous=new Set((state().owned||[]).map(p=>p.id));let hatched=false,message='';
    clearTimeout(noticeTimer);notice=null;
    ui.pending=true; busy=true; ui.error=''; render(true);
    try { const res=await api('POST',`/api/players/${encodeURIComponent(S.id)}/${path}`); setState(res.state); notifyHost('changed'); ui.release=null;if(path.includes('/merge?')){ui.starDonors=[null,null];ui.merge=null;}message=res.message||'Đã cập nhật linh thú.';
      if(path.startsWith('hatch/collect')){const born=(state().owned||[]).find(p=>!previous.has(p.id));if(born){ui.selected=String(born.id);hatched=true;}}
    }
    catch(e) { ui.error=e.message || 'Không kết nối được máy chủ. Hãy thử lại.'; }
    finally { ui.pending=false; busy=false;notice={message:ui.error||message,kind:ui.error?'bad':'good'}; render(true);noticeTimer=setTimeout(()=>{notice=null;const el=document.getElementById('petNotice');if(el)el.hidden=true;},ui.error?12000:6000);if(hatched&&!matchMedia('(prefers-reduced-motion: reduce)').matches)document.querySelector('#pane-pet .pet-portrait')?.animate([{opacity:.2,filter:'brightness(2)',transform:'scale(.85)'},{opacity:1,filter:'brightness(1)',transform:'scale(1)'}],{duration:650,easing:'ease-out'}); }
  }
  // Reuse existing images, controls and scroll containers when server values change.
  // Keys also keep a pet's card intact when feeding changes the level sort order.
  function reconcile(target, source) {
    const key=node=>node.nodeType===1?(node.id||['petSelect','petAction','petFilter'].map(k=>node.dataset[k]?k+':'+node.dataset[k]:'').find(Boolean)||''):'';
    [...source.childNodes].forEach((next,index)=>{
      const nextKey=key(next),at=target.childNodes[index];
      let current=nextKey?[...target.childNodes].find(n=>key(n)===nextKey):at;
      if(!current||current.nodeType!==next.nodeType||current.nodeName!==next.nodeName||(!nextKey&&key(current))){
        target.insertBefore(next.cloneNode(true),at||null);return;
      }
      if(current!==at)target.insertBefore(current,at||null);
      if(next.nodeType!==1){if(current.nodeValue!==next.nodeValue)current.nodeValue=next.nodeValue;return;}
      [...current.attributes].forEach(a=>{if(!next.hasAttribute(a.name))current.removeAttribute(a.name);});
      [...next.attributes].forEach(a=>{if(current.getAttribute(a.name)!==a.value)current.setAttribute(a.name,a.value);});
      reconcile(current,next);
    });
    while(target.childNodes.length>source.childNodes.length)target.lastChild.remove();
  }
  function action(value) {
    if(ui.pending)return;
    if(value==='notice-close'){
      clearTimeout(noticeTimer);notice=null;
      const el=document.getElementById('petNotice');if(el)el.hidden=true;
      document.querySelector('#pane-pet .pet-actions [data-pet-action="evolve"]:not(:disabled),#pane-pet .pet-actions button:not(:disabled)')?.focus({preventScroll:true});
      return;
    }
    const [kind,arg,...rest]=value.split(':'), p=(state().owned||[]).find(p=>String(p.id)===ui.selected);
    if (kind==='tab') {ui.tab=arg;ui.release=null;render(true);return;}
    if (kind==='egg') {ui.egg=arg;render(true);return;}
    if (kind==='release-arm') {ui.release=ui.selected;render(true);return;}
    if (kind==='release-cancel') {ui.release=null;render(true);return;}
    if (kind==='multi') {ui.multi=!ui.multi;ui.picked.clear();ui.multiArm=false;render(true);return;}
    if (kind==='pick') {(state().owned||[]).filter(x=>quality(x.rarity)===num(arg)&&!num(x.slot)&&!num(x.stars)).forEach(x=>ui.picked.add(String(x.id)));ui.multiArm=false;render(true);return;}
    if (kind==='pick-clear') {ui.picked.clear();ui.multiArm=false;render(true);return;}
    if (kind==='multi-arm') {ui.multiArm=true;render(true);return;}
    if (kind==='multi-cancel') {ui.multiArm=false;render(true);return;}
    if (kind==='multi-confirm') {const ids=(state().owned||[]).filter(x=>ui.picked.has(String(x.id))&&!num(x.slot)).map(x=>x.id);ui.picked.clear();ui.multiArm=false;if(ids.length)return send(`pet/release-many?ids=${encodeURIComponent(ids.join(','))}`);render(true);return;}
    if (kind==='merge-arm') {const main=(state().owned||[]).find(x=>String(x.id)===ui.star);if(main&&donorsReady(main))ui.merge=true;render(true);return;}
    if (kind==='merge-cancel') {ui.merge=null;render(true);return;}
    if (kind==='star-open') {ui.tab='star';ui.star=[arg,...rest].join(':');ui.merge=null;ui.starDonors=[null,null];render(true);return;}
    if (kind==='star-main') {ui.star=[arg,...rest].join(':');ui.merge=null;ui.starDonors=[null,null];render(true);return;}
    if (kind==='star-donor') {
      const main=(state().owned||[]).find(x=>String(x.id)===ui.star),i=Number(arg),id=rest.join(':');
      const donor=(state().owned||[]).find(x=>String(x.id)===id);
      if(main&&donor&&Number.isInteger(i)&&i>=0&&i<1+num(starRecipe(main)?.otherCount)&&donorFits(main,donor,i)&&!ui.starDonors.some((x,j)=>j!==i&&x===id))ui.starDonors[i]=ui.starDonors[i]===id?null:id;
      ui.merge=null;render(true);return;
    }
    if (kind==='train-open') {ui.tab='train';ui.train=[arg,...rest].join(':');ui.awakenDonor=null;ui.locks.clear();render(true);return;}
    if (kind==='train-pick') {ui.train=[arg,...rest].join(':');ui.awakenDonor=null;ui.locks.clear();render(true);return;}
    if (kind==='lock') {ui.locks.has(arg)?ui.locks.delete(arg):ui.locks.size<num(RULES().maxLocks||2)&&ui.locks.add(arg);render(true);return;}
    if (kind==='awaken-donor') {const id=[arg,...rest].join(':');ui.awakenDonor=ui.awakenDonor===id?null:id;render(true);return;}
    if (kind==='petfuse') return openTab('petfuse',true);
    if (ui.tab==='train'&&ui.train&&['reroll','gear','ungear','awaken'].includes(kind)){
      const base=`pet/${encodeURIComponent(ui.train)}/`;
      if (kind==='reroll') return send(base+`reroll?locks=${encodeURIComponent([...ui.locks].join(','))}`);
      if (kind==='gear') return send(base+`gear/${encodeURIComponent(arg)}`);
      if (kind==='ungear') return send(base+`ungear/${encodeURIComponent(arg)}`);
      if (kind==='awaken'&&ui.awakenDonor){const d=ui.awakenDonor;ui.awakenDonor=null;return send(base+`awaken/${encodeURIComponent(d)}`);}
      return;
    }
    if (kind==='hatch') return send(`hatch/${encodeURIComponent(rest.join(':'))}?slot=${num(arg)}`);
    if (kind==='now'||kind==='collect') return send(`hatch/${kind}?slot=${num(arg)}`);
    if (kind==='merge-confirm'&&ui.merge&&ui.tab==='star'&&ui.star){const main=(state().owned||[]).find(x=>String(x.id)===ui.star);if(main&&donorsReady(main)){ui.merge=null;return send(`pet/${encodeURIComponent(ui.star)}/merge?foods=${encodeURIComponent(chosenDonors(main).map(x=>x.id).join(','))}`);}return;}
    if (!p) return;
    const base=`pet/${encodeURIComponent(p.id)}/`;
    if (kind==='feed') return send(base+`feed?n=${num(arg)}`);
    if (kind==='slot') return send(base+`slot/${num(arg)}`);
    if (kind==='evolve'&&num(p.form)<2) return send(base+'evolve');
    if (kind==='appearance') return send(base+`appearance/${num(arg)}`);
    if (kind==='release-confirm'&&ui.release===String(p.id)) return send(base+'release');
  }
  function init() {
    const root=document.getElementById('pane-pet');
    if (root&&!root.dataset.petBound) {
      root.dataset.petBound='1';
      root.addEventListener('click',e=>{const b=e.target.closest('[data-pet-action],[data-pet-select]');if(!b||b.disabled)return;if(b.hasAttribute('data-pet-select')){const id=b.dataset.petSelect;if(ui.multi){const x=(state().owned||[]).find(p=>String(p.id)===id);if(x&&!num(x.slot)){ui.picked.has(id)?ui.picked.delete(id):ui.picked.add(id);ui.multiArm=false;}render(true);return;}ui.selected=id;ui.release=null;ui.merge=null;render(true);}else action(b.dataset.petAction);});
      root.addEventListener('change',e=>{const key=e.target.dataset.petFilter;if(key){ui[key]=e.target.value;ui.release=null;render(true);}});
      root.addEventListener('error',e=>{if(e.target.tagName==='IMG'){e.target.hidden=true;e.target.parentElement.classList.add('pet-missing');}},true);
    }
    if (!ui.timer) ui.timer=setInterval(tick,1000);
  }
  function render(force = false) {
    init();
    if (typeof S==='undefined'||!S) return;
    if (ui.state!==S.pet) {ui.state=S.pet;ui.received=typeof recvAt==='number'&&recvAt>0?recvAt:performance.now();}
    tick();
    const root=document.getElementById('pane-pet'); if(!root)return;
    root.setAttribute('aria-busy',String(ui.pending));
    if(ui.pending){
      root.querySelectorAll('.pet-btn,.pet-egg-choice').forEach(b=>{b.disabled=true;});
      return;
    }
    const signature=JSON.stringify([S.pet,S.inventory,DATA?.petDefs,ui.tab,ui.selected,ui.element,ui.rarity,ui.sort,ui.egg,ui.codexElement,ui.codexRarity,ui.codexOwned,ui.release,ui.multi,[...ui.picked],ui.multiArm,ui.merge,ui.star,ui.starDonors,ui.train,[...ui.locks],ui.awakenDonor,ui.pending,ui.error]);
    if (!force&&signature===ui.signature)return;
    // A server sync must not steal focus from an open filter or keyboard navigation.
    const focused=root.contains(document.activeElement)?document.activeElement:null;
    if (!force&&focused?.matches('select')) return;
    const focusKey=focused?.dataset.petAction, focusSelect=focused?.dataset.petSelect;
    const scrolls=[...root.querySelectorAll('.pet-detail,.pet-cards,.pet-star-list,.pet-star-main,.pet-body')].map(el=>[el.className,el.scrollTop]);
    ui.signature=signature;
    const next=document.createElement('div');
    next.innerHTML=`<div class="pet-ui"><div class="pet-top"><header class="pet-header"><span class="pet-heading-icon"><img src="img/header/pet-emblem.png" alt=""></span><div><h2>Linh thú</h2><p>Duyên lành đồng hành</p></div><span>${f((state().owned||[]).length)} linh thú</span></header><nav class="pet-tabs" aria-label="Linh thú">${[['stable','Chuồng thú'],['star','Ghép sao'],['train','Bồi dưỡng'],['hatch','Ấp trứng'],['codex','Thú điển']].map(([k,label])=>`<button type="button" data-pet-action="tab:${k}" aria-current="${ui.tab===k?'page':'false'}" class="${ui.tab===k?'active':''}">${label}</button>`).join('')}</nav></div><div class="pet-status" role="${ui.error?'alert':'status'}">${ui.error?esc(ui.error):''}</div><div class="pet-body">${ui.tab==='hatch'?hatch():ui.tab==='codex'?codex():ui.tab==='star'?starTab():ui.tab==='train'?trainTab():stable()}</div></div>`;
    if(ui.tab==='stable')layoutStable(next);
    next.firstElementChild.insertAdjacentHTML('beforeend',noticeMarkup());
    reconcile(root,next);
    scrolls.forEach(([cls,top])=>{const el=root.getElementsByClassName(cls)[0];if(el)el.scrollTop=top;});
    if(focusKey||focusSelect) [...root.querySelectorAll('button')].find(el=>focusKey?el.dataset.petAction===focusKey:el.dataset.petSelect===focusSelect)?.focus({preventScroll:true});
    tick();
  }
  window.initPet=init;
  window.renderPet=render;
})();
