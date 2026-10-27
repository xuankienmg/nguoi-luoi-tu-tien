'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const fmt = n => Number(n || 0).toLocaleString('vi-VN');
  const norm = s => String(s ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g,'d').replace(/Đ/g,'D').toLowerCase();
  const tabs = {
    gear:['Trang bị','Chọn cấp, phẩm và ô trang bị để so sánh chỉ số gốc, hiệu ứng riêng và bộ trang bị.'],
    pets:['Linh thú','Khám phá các loài linh thú, thiên phú, kỹ năng và ba hình thái tiến hóa.'],
    gems:['Chú linh','Tra cứu 25 Cổ Phù & Tiên Châu (Sơ cấp · Cao cấp · Đỉnh cấp), công thức Lò Hợp Vạn Năng, quy tắc mở lỗ khảm 10 ô trang bị và tỉ lệ rơi tại các ải / Boss.'],
    map:['Bản đồ & các ải','Tra cứu từng đợt quái, Boss và linh thạch ở cả ba mức độ khó.'],
    alchemy:['Đan dược','Đan phương cho một viên: nguyên liệu, phí linh thạch và công dụng.'],
    materials:['Vật phẩm & linh thảo','Tìm công dụng, nơi thu thập linh thảo, rương và các vật phẩm khác.'],
    realms:['Cảnh giới','Các tầng tu hành, ô trang bị, hành trang và điều kiện đột phá.']
  };
  let data, items, tab='gear', query='', filters={}, page=1, currentId='';
  const size=24;
  const SO_IDS = new Set(['stone_viem_so','stone_bang_so','stone_loi_so','stone_kim_so','stone_tho_so','gem_ruby_cui','gem_sapphire_cui','gem_topaz_cui','gem_emerald_cui','gem_amethyst_cui']);
  const CAO_IDS = new Set(['stone_viem_cao','stone_bang_cao','stone_loi_cao','stone_kim_cao','stone_tho_cao','gem_ruby_xin','gem_sapphire_xin','gem_topaz_xin','gem_emerald_xin','gem_amethyst_xin','stone_socket']);
  const DINH_IDS = new Set(['stone_liet_loi','stone_huyen_bang','stone_am_duong','gem_thien_phu']);
  const CUI_TO_XIN = {gem_ruby_cui:'gem_ruby_xin',gem_sapphire_cui:'gem_sapphire_xin',gem_topaz_cui:'gem_topaz_xin',gem_emerald_cui:'gem_emerald_xin',gem_amethyst_cui:'gem_amethyst_xin'};
  const isChuLinh = it => Boolean(it && (SO_IDS.has(it.id) || CAO_IDS.has(it.id) || DINH_IDS.has(it.id)));
  const gemTier = id => DINH_IDS.has(id) ? 'dinh' : CAO_IDS.has(id) ? 'cao' : 'so';
  const gemTierName = t => ({so:'Sơ Cấp',cao:'Cao Cấp',dinh:'Đỉnh Cấp'})[t] || t;
  const gemCatName = c => ({stone:'Luyện Cổ Phù & Biến Dị',gem:'Thăng cấp Tiên Châu',transcend:'Thăng Hoa (Cổ Phù + Tiên Châu)',herb:'Ngưng luyện từ Linh Thảo'})[c] || c;
  const gemPowerScore = id => id === 'gem_thien_phu' ? 14 : (['stone_liet_loi','stone_huyen_bang','stone_am_duong','stone_socket'].includes(id) ? 10 : (['gem_ruby_xin','gem_sapphire_xin','gem_topaz_xin','gem_emerald_xin','gem_amethyst_xin'].includes(id) ? 8 : (['stone_viem_cao','stone_bang_cao','stone_loi_cao','stone_kim_cao','stone_tho_cao'].includes(id) ? 6 : (['gem_ruby_cui','gem_sapphire_cui','gem_topaz_cui','gem_emerald_cui','gem_amethyst_cui'].includes(id) ? 3 : 2))));
  const qualityName = q => q==='ancient' ? 'Thượng Cổ' : data.qualityNames[data.qualityKeys.indexOf(q)] || q;
  const color = q => q==='ancient' ? '#e9aaf4' : data.qualityColors[data.qualityKeys.indexOf(q)] || '#dfc487';
  const realmName = r => data.realms[r]?.name || 'Chưa xác định';
  const roleName = r => ({atk:'Tấn công',def:'Phòng thủ',hp:'Sinh lực',qi:'Tu luyện',crit:'Bạo kích'})[r] || r;
  function img(path, cls='icon', rarity='common', alt='') {
    return path ? `<img class="${cls}" src="img/wiki/${esc(path.replace(/^img\//,''))}" alt="${esc(alt)}" loading="lazy" style="--quality:${color(rarity)}">` : '<span class="icon" aria-hidden="true">◇</span>';
  }
  const badge = q => `<span class="badge" style="--quality:${color(q)}">${esc(qualityName(q))}</span>`;
  const stats = it => {
    const pills = [['atk','Công'],['hp','Máu'],['def','Thủ'],['qiPct','Tu vi %'],['speedPct','Thân pháp %'],['critPct','Bạo kích %']].filter(([k])=>it[k]).map(([k,n])=>`<span>${n} ${fmt(it[k])}</span>`).concat((it.effects||[]).map(e=>`<span>${esc(e.name)}: ${esc(e.text)}</span>`));
    if (!pills.length && it?.id === 'stone_socket') pills.push('<span>Khai khiếu: Mở +1 Lỗ Khảm (tối đa 3 lỗ/ô)</span>');
    return pills.length ? '<div class="stats">'+pills.join('')+'</div>' : '';
  };
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
    if(tab==='gems') {
      html=select('view','Chế độ xem',[['','Vật phẩm Chú Linh (25)'],['recipes','Công thức Lò Hợp (25)'],['guide','Cẩm nang Khảm Nạm & Lò Hợp Động']]);
      if(filters.view!=='guide') {
        if(filters.view==='recipes') html+=select('gemCat','Nhóm công thức',[['','Mọi nhóm Lò Hợp'],['stone','Luyện Cổ Phù & Biến Dị'],['gem','Thăng cấp Tiên Châu'],['transcend','Thăng Hoa (Cổ Phù + Tiên Châu)'],['herb','Ngưng luyện từ Linh Thảo']]);
        html+=select('gemKind','Phân loại',[['','Mọi loại (Cổ Phù & Tiên Châu)'],['Tiên Châu','Tiên Châu (11)'],['Cổ Phù','Cổ Phù (14)']])+select('gemTier','Cấp bậc Kho',[['','Mọi cấp bậc Kho'],['so','Kho Sơ Cấp (10)'],['cao','Kho Cao Cấp (11)'],['dinh','Kho Đỉnh Cấp (4)']])+select('rarity','Phẩm chất',[['','Mọi phẩm'],...['uncommon','rare','mythic','divine'].map(q=>[q,qualityName(q)])]);
      }
    }
    if(tab==='map') html=select('chapter','Chương',data.chapters.map(c=>[c.index,`${c.index+1}. ${c.name}${c.enabled?'':' · Chưa mở'}`]),'0')+select('difficulty','Độ khó',[['0','Thường'],['1','Khó'],['2','Ác mộng']],'0');
    if(tab==='materials') html=select('type','Loại vật phẩm',[['','Mọi loại'],...Array.from(new Set(data.items.filter(i=>!i.slot).map(i=>i.type))).sort().map(t=>[t,typeName(t)])]);
    $('selects').innerHTML=html;
  }
  function typeName(t) { return ({herb:'Linh thảo',chest:'Rương',egg:'Thú đản',pill:'Đan dược',elixir:'Đan đặc biệt',stone:'Linh thạch & Cổ phù',gem:'Tiên châu',ticket:'Vé quay',key:'Chìa khóa',boss:'Nguyên liệu Boss',manual:'Công pháp',skin:'Trang phục',material:'Nguyên liệu'})[t] || data.items.find(i=>i.type===t)?.kind || t; }
  function route() {
    const p=new URLSearchParams(location.hash.slice(1));
    tab=tabs[p.get('tab')] ? p.get('tab') : 'gear';
    query=p.get('q') || ''; filters={};
    for(const k of ['realm','rarity','slot','element','chapter','difficulty','type','view','gemKind','gemTier','gemCat']) if(p.has(k)) filters[k]=p.get(k);
    page=Math.max(1,Number(p.get('page'))||1); currentId=p.get('id')||'';
    if(!data)return;
    $('search').value=query;
    const idx=Object.keys(tabs).indexOf(tab)+1;
    $('sectionNumber').textContent=`TÀNG THƯ / ${String(idx).padStart(2,'0')}`;
    $('sectionTitle').textContent=tabs[tab][0]; $('sectionDesc').textContent=tabs[tab][1];
    $('nav').innerHTML=Object.entries(tabs).map(([id,[name]],i)=>`<a href="#tab=${id}"${id===tab?' aria-current="page"':''}><span>${String(i+1).padStart(2,'0')}</span>${name}</a>`).join('');
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
    return `<button type="button" class="card" data-item="${esc(it.id)}"><div class="card-top">${img(it.icon,'icon',it.rarity)}${badge(it.rarity)}</div><strong>${esc(it.name)}</strong><small>${esc(it.kind||typeName(it.type))}${it.slot?` · Cấp ${it.itemLevel || 1} · ${esc(realmName(it.minRealm))}`:''}</small>${it.slot?stats(it):`<p class="desc">${esc(it.use||it.special||it.desc)}</p>`}</button>`;
  }
  function gemCard(it) {
    const tier = gemTier(it.id);
    const score = gemPowerScore(it.id);
    return `<button type="button" class="card" data-item="${esc(it.id)}"><div class="card-top">${img(it.icon,'icon',it.rarity)}<div class="badge-group">${badge(it.rarity)}<span class="badge">${esc(gemTierName(tier))}</span></div></div><strong>${esc(it.name)}</strong><small>${esc(it.kind)} · Kho ${esc(gemTierName(tier))} · Điểm linh lực: ${score}</small>${stats(it)}<p class="desc">${esc(it.desc||it.special)}</p></button>`;
  }
  function gemRecipeCard(r) {
    const out = items[r.resultItem] || {id:r.resultItem, name:r.resultItem, rarity:'common'};
    const chancePct = Math.round((r.chance ?? 1) * 100);
    const inText = Object.entries(r.inputs || {}).map(([k,n]) => `${items[k]?.name || k} ×${fmt(n)}`).join(' + ');
    return `<button type="button" class="card" data-item="${esc(out.id)}"><div class="card-top">${img(out.icon,'icon',out.rarity)}<div class="badge-group">${badge(out.rarity)}<span class="badge">${chancePct}%</span></div></div><strong>${esc(r.name)}</strong><small>${esc(gemCatName(r.category))} · ${fmt(r.stones)} linh thạch</small><div class="stats"><span>Nguyên liệu: ${esc(inText)}</span></div><p class="desc">${esc(r.desc)}</p></button>`;
  }
  function renderGemReferenceSections(openAll = false) {
    const openAttr = openAll ? ' open' : '';
    const herbMap = data.herbToGem || {};
    const herbGroups = [
      ['gem_ruby_cui', 'Hệ Hỏa → Xích Diễm Tiên Châu · Thô (+100 Công)'],
      ['gem_sapphire_cui', 'Hệ Thủy / Băng → Huyền Băng Tiên Châu · Thô (+80 Thủ)'],
      ['gem_topaz_cui', 'Hệ Thổ / Kim → Thái Dương Tiên Châu · Thô (+600 Máu)'],
      ['gem_emerald_cui', 'Hệ Mộc / Phong → Thanh Mộc Tiên Châu · Thô (+4% Tu luyện · +4 Thân pháp)'],
      ['gem_amethyst_cui', 'Hệ Lôi / Hồn → Tử Lôi Tiên Châu · Thô (+4% Bạo kích · +15 Nộ)']
    ];
    const bossThemeRows = [
      ['Chương 1, 6', 'gem_ruby_xin', 'stone_viem_cao'],
      ['Chương 2, 7', 'gem_sapphire_xin', 'stone_bang_cao'],
      ['Chương 3, 8', 'gem_topaz_xin', 'stone_tho_cao'],
      ['Chương 4, 9', 'gem_amethyst_xin', 'stone_loi_cao'],
      ['Chương 5, 10', 'gem_emerald_xin', 'stone_kim_cao']
    ];
    const sections = [
      `<details class="area"${openAttr}>
        <summary><span>1. Cơ chế Vị Trí Chú Linh (10 Ô Trang Bị) & Mở Lỗ Khảm</span><small> TỐI ĐA 30 LỖ KHẢM · BẢO TOÀN 100% KHI THAY ĐỒ</small></summary>
        <div class="area-content">
          <p class="detail-copy">Cổ Phù và Tiên Châu được chú linh trực tiếp vào <b>10 vị trí trang bị</b> (${data.slots.map(s=>esc(s.name)).join(', ')}), hoàn toàn độc lập với món đồ đang mặc. Khi thay đổi hoặc tháo trang bị, toàn bộ Cổ Phù & Tiên Châu đã khảm vẫn giữ nguyên 100% hiệu lực. Mọi vật phẩm Chú Linh được cất giữ trong <b>Bảo Khố 3 Cấp Bậc (Sơ Cấp · Cao Cấp · Đỉnh Cấp)</b>, không chiếm ô hành trang.</p>
          <div class="wiki-table-wrap">
            <table class="wiki-table">
              <thead><tr><th>Cảnh giới / Điều kiện</th><th>Số lỗ mở mặc định (mỗi ô)</th><th>Cách mở sớm tối đa 3 lỗ</th></tr></thead>
              <tbody>
                <tr><td><b>Luyện Khí & Trúc Cơ</b></td><td>1 lỗ / ô (Tổng 10 lỗ)</td><td>Dùng ${ingredient('stone_socket',1)} để đục mở sớm Lỗ 2 và Lỗ 3 cho từng ô bất kỳ.</td></tr>
                <tr><td><b>Kim Đan & Nguyên Anh</b></td><td>2 lỗ / ô (Tổng 20 lỗ)</td><td>Dùng ${ingredient('stone_socket',1)} để đục mở sớm Lỗ 3 cho từng ô bất kỳ.</td></tr>
                <tr><td><b>Hóa Thần trở lên</b></td><td>3 lỗ / ô (Tổng 30 lỗ tối đa)</td><td>Đã tự động khai mở toàn bộ 3 lỗ trên cả 10 vị trí trang bị.</td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </details>`,
      `<details class="area"${openAttr}>
        <summary><span>2. Nguồn Rơi Cổ Phù & Tiên Châu (Ải Thường & Boss 100%)</span><small>ẢI THƯỜNG: 38% ĐÁ · 12% NGỌC · BOSS: 100%</small></summary>
        <div class="area-content">
          <p class="detail-copy"><b>Ải thường (Ải 1–15 mỗi chương):</b> Mỗi chuyến vượt ải thành công gieo độc lập 2 nhóm rơi: <b>38% rơi Cổ Phù</b> (Ở mức Thường: 100% là 5 loại Cổ Phù Sơ; ở mức Khó & Ác mộng: 80% Cổ Phù Sơ số lượng ×2–3 và 20% Cổ Phù Cao) và <b>12% rơi Tiên Châu</b> (90% là 5 loại Tiên Châu Thô, 10% là 5 loại Tiên Châu Hoàn Mỹ).</p>
          <p class="detail-copy"><b>Ải Boss (Ải 16 mỗi chương):</b> Hạ Boss chắc chắn <b>100%</b> rơi <b>1 Tiên Châu Hoàn Mỹ</b> và <b>1–4 Cổ Phù Cao Cấp</b> (Thường: 1–2 viên, Khó: 2–3 viên, Ác mộng: 3–4 viên) theo chu kỳ Ngũ Hành của chương:</p>
          <div class="wiki-table-wrap">
            <table class="wiki-table">
              <thead><tr><th>Chương áp dụng</th><th>Tiên Châu Hoàn Mỹ (100% · 1 viên)</th><th>Cổ Phù Cao Cấp (100% · 1–4 viên)</th></tr></thead>
              <tbody>
                ${bossThemeRows.map(([ch,g,s])=>`<tr><td><b>${ch}</b></td><td>${ingredient(g,1)}</td><td>${ingredient(s,undefined)}</td></tr>`).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </details>`,
      `<details class="area"${openAttr}>
        <summary><span>3. Lò Hợp Vạn Năng 5 Ô (Horadric Cube) & Điểm Linh Lực</span><small>7 TẦNG DUNG HỢP TỰ DO · BẢO LƯU KHI THẤT BẠI</small></summary>
        <div class="area-content">
          <p class="detail-copy">Ngoài <b>25 công thức cố định</b> và nút <b>Hợp Nhanh 1 Chạm</b> (tự động nâng cấp 100% thành công: 3 Cổ Phù Sơ → 1 Cao với 200 linh thạch, 3 Tiên Châu Thô → 1 Hoàn Mỹ với 400 linh thạch), Lò Hợp cho phép đặt tự do từ <b>1 đến 5 ô nguyên liệu bất kỳ</b> (mỗi viên tính 1 ô, riêng Linh Thảo tính 10 cây = 1 ô). Khi thất bại luôn <b>bảo lưu 50% nguyên liệu chính</b> hoàn về Kho.</p>
          <p class="muted">Điểm Linh Lực (Power Score) mỗi ô: Linh thảo = <b>1</b> · Cổ Phù Sơ = <b>2</b> · Tiên Châu Thô = <b>3</b> · Cổ Phù Cao = <b>6</b> · Tiên Châu Hoàn Mỹ = <b>8</b> · Cổ Phù Biến Dị / Khai Khiếu = <b>10</b> · Cổ Phù Tiên Châu = <b>14</b>.</p>
          <div class="wiki-table-wrap">
            <table class="wiki-table">
              <thead><tr><th>Tổ hợp đưa vào Lò Hợp (1–5 ô)</th><th>Kết quả đầu ra</th><th>Chi phí Linh Thạch</th><th>Tỉ lệ thành công</th></tr></thead>
              <tbody>
                <tr><td><b>Linh Thảo thuần (1 / 2 / 3 ô · 10 cây/ô)</b></td><td>1 ô: 1 Tiên Châu Thô · 2 ô: 1 Tiên Châu Thô · <b>3 ô: ×2 Tiên Châu Thô</b></td><td>500 / 600 / 750</td><td>85% / 90% / 85%</td></tr>
                <tr><td><b>Linh Thảo thuần (4 / 5 ô · 10 cây/ô)</b></td><td>Đột phá thẳng lên <b>1 × Tiên Châu Hoàn Mỹ</b> theo hệ thảo dược</td><td>900 / 1.000</td><td>75% / 85%</td></tr>
                <tr><td><b>Cổ Phù Sơ thuần (2 / 3 / 4 / 5 ô)</b></td><td>2–4 ô: 1 × Cổ Phù Cao · <b>5 ô cùng/khác hệ: ×2 Cổ Phù Cao</b> (Riêng 5 viên đủ Ngũ Hành ra ${ingredient('stone_socket',1)})</td><td>150 → 400</td><td>60% → 90% (Ngũ hành: 100%)</td></tr>
                <tr><td><b>Cổ Phù Cao thuần (2 / 3 / 4 / 5 ô)</b></td><td>2–4 ô: 1 × Cổ Phù Biến Dị (${ingredient('stone_liet_loi')} ${ingredient('stone_huyen_bang')} ${ingredient('stone_am_duong')}) · <b>5 ô: 1 × ${ingredient('gem_thien_phu')}</b></td><td>500 → 2.000</td><td>70% → 90% (5 ô: 80%)</td></tr>
                <tr><td><b>Tiên Châu Thô thuần (2 / 3 / 4 / 5 ô)</b></td><td>2–4 ô: 1 × Tiên Châu Hoàn Mỹ · <b>5 ô: ×2 Tiên Châu Hoàn Mỹ</b></td><td>300 → 650</td><td>55% → 90%</td></tr>
                <tr><td><b>Bảo vật Cao Giai thuần (2 / 3 / 4 / 5 ô)</b></td><td>Tiên Châu Hoàn Mỹ / Biến Dị / Khai Khiếu: 2–4 ô ra <b>1 × ${ingredient('gem_thien_phu')}</b> · <b>5 ô ra ×2 ${ingredient('gem_thien_phu')}</b></td><td>3.000 → 5.000</td><td>68% / 80% / 90% / 92%</td></tr>
                <tr><td><b>Hỗn Hợp Vạn Năng (Phối trộn tự do 2–5 ô)</b></td><td>Điểm ≥ 14 (hoặc ≥ 11 kèm đồ Cao Giai) → ${ingredient('gem_thien_phu',1)}; Điểm 6–13 → Tiên Châu Hoàn Mỹ / Cổ Phù Biến Dị; Điểm &lt; 6 → Cổ Phù Cao / Tiên Châu Thô</td><td>350 → 2.500</td><td>67% → 90%</td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </details>`,
      `<details class="area"${openAttr}>
        <summary><span>4. Bảng Ngũ Hành 25 Linh Thảo → Ngưng Luyện Tiên Châu</span><small>10 CÂY = 1 Ô LÒ HỢP · 5 HỆ NGŨ HÀNH</small></summary>
        <div class="area-content">
          <p class="detail-copy">Mọi loại linh thảo thu thập từ các chương bản đồ đều có thể đưa vào Lò Hợp (mỗi ô 10 cây) để ngưng luyện thành Tiên Châu tương ứng:</p>
          <div class="wiki-table-wrap">
            <table class="wiki-table">
              <thead><tr><th>Tiên Châu Thô & Hoàn Mỹ tương ứng</th><th>Danh sách Linh Thảo cùng hệ (Nhấp để xem nơi thu thập)</th></tr></thead>
              <tbody>
                ${herbGroups.map(([cuiId, label]) => {
                  const xinId = CUI_TO_XIN[cuiId];
                  const matchingHerbs = Object.entries(herbMap).filter(([,v]) => v === cuiId).map(([k]) => k);
                  return `<tr><td><div>${ingredient(cuiId,1)} ${ingredient(xinId,1)}</div><small class="muted">${esc(label)}</small></td><td><div class="drop-list" style="margin-top:0">${matchingHerbs.map(h => ingredient(h, 10)).join('')}</div></td></tr>`;
                }).join('')}
              </tbody>
            </table>
          </div>
        </div>
      </details>`
    ];
    const visible = openAll && query ? sections.filter(s => match(s)) : sections;
    return visible.length ? `<div class="gem-sections">${visible.join('')}</div>` : '<p class="empty">Không tìm thấy mục cẩm nang phù hợp.<br>Thử từ khóa khác.</p>';
  }
  function renderGems() {
    const allGemItems = data.items.filter(isChuLinh);
    const recipes = data.gemRecipes || [];
    if (filters.view === 'guide') {
      const html = renderGemReferenceSections(true);
      const matchedCount = (html.match(/<details class="area"/g) || []).length;
      $('resultCount').textContent = `${matchedCount} chuyên mục cẩm nang · ${allGemItems.length} vật phẩm · ${recipes.length} công thức cố định`;
      $('results').innerHTML = html;
      return;
    }
    if (filters.view === 'recipes') {
      const filteredRecipes = recipes.filter(r => {
        const out = items[r.resultItem];
        const text = [r.name, r.desc, out?.name, gemCatName(r.category), ...Object.keys(r.inputs||{}).map(k=>items[k]?.name||k)].join(' ');
        if (!match(text)) return false;
        if (filters.gemCat && r.category !== filters.gemCat) return false;
        if (filters.gemKind && out?.kind !== filters.gemKind) return false;
        if (filters.gemTier && gemTier(r.resultItem) !== filters.gemTier) return false;
        if (filters.rarity && out?.rarity !== filters.rarity) return false;
        return true;
      });
      $('resultCount').textContent = `${fmt(filteredRecipes.length)} công thức Lò Hợp${query?' cho “'+query+'”':''}`;
      $('results').innerHTML = (filteredRecipes.length ? '<div class="cards">' + filteredRecipes.map(gemRecipeCard).join('') + '</div>' : '<p class="empty">Không tìm thấy công thức phù hợp.<br>Thử tên khác hoặc bỏ bớt bộ lọc.</p>') + renderGemReferenceSections(false);
      return;
    }
    const filteredItems = allGemItems.filter(i => {
      const text = [i.name, i.desc, i.kind, i.special, gemTierName(gemTier(i.id)), ...(i.effects||[]).map(e=>e.name+' '+e.text)].join(' ');
      if (!match(text)) return false;
      if (filters.gemKind && i.kind !== filters.gemKind) return false;
      if (filters.gemTier && gemTier(i.id) !== filters.gemTier) return false;
      if (filters.rarity && i.rarity !== filters.rarity) return false;
      return true;
    });
    $('resultCount').textContent = `${fmt(filteredItems.length)} kết quả${query?' cho “'+query+'”':''}`;
    $('results').innerHTML = (filteredItems.length ? '<div class="cards">' + filteredItems.map(gemCard).join('') + '</div>' : '<p class="empty">Không tìm thấy Cổ Phù hoặc Tiên Châu phù hợp.<br>Thử tên khác hoặc bỏ bớt bộ lọc.</p>') + renderGemReferenceSections(false);
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
    if(tab==='gems')return renderGems();
    if(tab==='alchemy')return paginate(data.brews.filter(b=>match([items[b.out]?.name,items[b.out]?.use,...Object.keys(b.in).map(k=>items[k]?.name)].join(' '))),b=>{const it=items[b.out];return `<button type="button" class="card" data-item="${esc(it.id)}"><div class="card-top">${img(it.icon,'icon',it.rarity)}${badge(it.rarity)}</div><strong>${esc(it.name)}</strong><small>${Object.keys(b.in).filter(k=>k!=='linh_thach').length} loại nguyên liệu · ${fmt(b.in.linh_thach)} linh thạch</small><p class="desc">${esc(it.use||it.desc)}</p></button>`;});
    if(tab==='materials')return paginate(data.items.filter(i=>!i.slot&&match(i.name+' '+i.desc+' '+(i.use||'')+' '+(i.special||''))&&(!filters.type||i.type===filters.type)),card);
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
      html=`<div class="detail-head">${img(it.icon,'icon',it.rarity)}<div><div class="badge-group">${badge(it.rarity)}${isChuLinh(it)?`<span class="badge">Kho ${esc(gemTierName(gemTier(it.id)))}</span><span class="badge">Linh lực ${gemPowerScore(it.id)}</span>`:''}</div><h2 id="detailTitle">${esc(it.name)}</h2><p class="muted">${esc(it.kind||typeName(it.type))}${it.slot?` · Cấp ${it.itemLevel} · ${esc(realmName(it.minRealm))}`:''}</p></div></div><p class="detail-copy">${esc(it.desc)}</p>${it.use?`<p class="detail-copy">${esc(it.use)}</p>`:''}${stats(it)}`;
      if(it.special)html+=block('Thuộc tính',`<p class="detail-copy">${esc(it.special)}</p>`);
      if(it.effects?.length)html+=block(isChuLinh(it)?'Hiệu ứng Chú Linh':'Hiệu ứng trang bị',it.effects.map(e=>`<p class="effect"><b>${esc(e.name)}</b> · ${esc(e.text)}</p>`).join(''));
      if(isChuLinh(it)){
        html+=block('Quy tắc Chú Linh & Bảo Khố',`<p class="detail-copy">${it.id==='stone_socket'?'Dùng để đục mở sớm Lỗ khảm thứ 2 hoặc thứ 3 cho bất kỳ vị trí trang bị nào (mỗi ô tối đa 3 lỗ).':'Khảm vào bất kỳ lỗ mở nào trên 10 vị trí trang bị. Khi thay trang bị mới hoặc tháo ra, thuộc tính giữ nguyên 100% và hoàn toàn miễn phí.'} Lưu trữ trong Kho <b>${esc(gemTierName(gemTier(it.id)))}</b>, không chiếm ô hành trang.</p>`);
        const craftFrom=(data.gemRecipes||[]).filter(r=>r.resultItem===id);
        if(craftFrom.length)html+=block('Công thức Lò Hợp tạo thành',craftFrom.map(r=>`<div style="margin-bottom:12px"><p class="detail-copy"><b>${esc(r.name)}</b> · Tỉ lệ <b>${Math.round((r.chance??1)*100)}%</b> · Phí <b>${fmt(r.stones)}</b> linh thạch</p><div class="drop-list">${Object.entries(r.inputs||{}).map(([k,n])=>ingredient(k,n)).join('')}</div></div>`).join(''));
        const matchingHerbs=Object.entries(data.herbToGem||{}).filter(([,v])=>v===id).map(([k])=>k);
        if(matchingHerbs.length)html+=block('Linh thảo cùng hệ ngưng luyện (10 cây / ô)',`<div class="drop-list">${matchingHerbs.map(h=>ingredient(h,10)).join('')}</div><p class="muted">Đặt 1–3 ô linh thảo nhận Tiên Châu Thô (85–90%); đặt 4–5 ô đột phá thẳng lên Tiên Châu Hoàn Mỹ (75–85%).</p>`);
        const usedInGem=(data.gemRecipes||[]).filter(r=>Object.hasOwn(r.inputs||{},id));
        if(usedInGem.length)html+=block('Dùng trong Lò Hợp để luyện',usedInGem.map(r=>`<div style="margin-bottom:10px"><p class="detail-copy"><b>${esc(r.name)}</b> · Cần <b>×${fmt(r.inputs[id])}</b> · Tỉ lệ <b>${Math.round((r.chance??1)*100)}%</b> · Phí <b>${fmt(r.stones)}</b> linh thạch</p><div class="drop-list">${ingredient(r.resultItem,r.resultCount||1)}</div></div>`).join('')+'<p class="muted">Ngoài ra có thể phối trộn tự do 2–5 ô trong Lò Hợp Vạn Năng để thăng cấp hoặc luyện Cổ Phù Tiên Châu.</p>');
        if(SO_IDS.has(id)&&it.type==='stone')html+=block('Nguồn nhận · Ải thường',`<p class="detail-copy">Rơi tại mọi ải thường (Ải 1–15): nhóm Cổ Phù có tỉ lệ <b>38%</b> mỗi chuyến (Thường: chia đều 5 hệ 20%/hệ; Khó & Ác mộng: 16%/hệ, số lượng tăng theo độ khó).</p>`);
        if(CAO_IDS.has(id)&&it.type==='stone'&&id!=='stone_socket')html+=block('Nguồn nhận · Ải Khó / Ác mộng',`<p class="detail-copy">Rơi ở ải thường (Ải 1–15) mức Khó & Ác mộng (chiếm 20% trong nhóm Cổ Phù 38%), hạ Boss 100% theo chương, hoặc hợp từ 3 Cổ Phù Sơ cùng hệ.</p>`);
        if(SO_IDS.has(id)&&it.type==='gem')html+=block('Nguồn nhận · Ải thường',`<p class="detail-copy">Rơi tại mọi ải thường (Ải 1–15): nhóm Tiên Châu có tỉ lệ <b>12%</b> mỗi chuyến (90% là Tiên Châu Thô, chia đều 5 hệ), hoặc ngưng luyện từ 10 Linh Thảo cùng hệ.</p>`);
        if(CAO_IDS.has(id)&&it.type==='gem')html+=block('Nguồn nhận · Ải thường & Boss',`<p class="detail-copy">Rơi 100% khi hạ Boss chương tương ứng, rơi hiếm tại mọi ải thường (chiếm 10% trong nhóm Tiên Châu 12%), hoặc thăng hoa trong Lò Hợp.</p>`);
      }
      const set=data.sets.find(s=>s.id===it.setId||s.name===it.set);
      if(set)html+=block('Bộ '+esc(set.name),set.milestones.map(m=>`<p class="effect"><b>${m.count} món:</b> ${m.bonuses.map(b=>esc(b.text)).join(' · ')}</p>`).join(''));
      const b=data.brews.find(b=>b.out===id);
      if(b)html+=block('Đan phương · 1 viên',`<div class="drop-list">${Object.entries(b.in).map(([k,n])=>ingredient(k,n)).join('')}</div><p class="muted">Cảnh giới tối thiểu: ${esc(realmName(it.minRealm))}. Luôn thành công khi đủ nguyên liệu và chỗ trong hành trang.</p>`);
      const recipes=data.brews.filter(b=>Object.hasOwn(b.in,id));
      if(recipes.length)html+=block('Dùng để luyện',`<div class="drop-list">${recipes.map(b=>ingredient(b.out,b.in[id])).join('')}</div><p class="muted">Số lượng hiển thị là lượng nguyên liệu này cần cho mỗi viên.</p>`);
      const herbGem=data.herbToGem?.[id];
      if(herbGem)html+=block('Ngưng luyện Tiên Châu (Lò Hợp)',`<div class="drop-list">${ingredient(herbGem,1)}${CUI_TO_XIN[herbGem]?ingredient(CUI_TO_XIN[herbGem],1):''}</div><p class="muted">Mỗi ô Lò Hợp cần 10 cây. Đặt 1–3 ô luyện ra Tiên Châu Thô (85–90%); đặt 4–5 ô đột phá thẳng lên Tiên Châu Hoàn Mỹ (75–85%).</p>`);
      const boss=bossSources(id);
      if(boss)html+=block('Nguồn nhận · Hạ Boss',boss+'<p class="muted">Mỗi lần hạ Boss gieo tỉ lệ một lần; số lượng ngẫu nhiên trong khoảng.</p>');
      const herbs=data.herbDrops.filter(h=>h.item===id);
      if(herbs.length)html+=block('Thu thập linh thảo',herbs.map(h=>`<p class="detail-copy">Chương ${h.chapters.map(c=>c+1).join(', ')} · từ ải ${h.minArea} · ${h.min}–${h.max} cây · tỉ lệ gốc ${fmt(h.chance*100)}%.</p>`).join('')+'<p class="muted">Ở Khó / Ác mộng: linh thảo của chương cuối, cộng linh thảo bổ sung của đúng chương đó (số lượng nhân hệ số thưởng mức khó). Tỉ lệ thực tế còn phụ thuộc ải.</p>');
      if(it.sell&&!isChuLinh(it))html+=block('Giá bán',`<p class="detail-copy">${fmt(it.sell)} linh thạch.</p>`);
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
  $('selects').addEventListener('change',e=>{if(!e.target.dataset.filter)return;filters[e.target.dataset.filter]=e.target.value;page=1;setFilters();save();render();});
  window.addEventListener('hashchange',route);
  async function load() {
    try {
      const response=await fetch('data/wiki.json');if(!response.ok)throw new Error(response.status);
      data=await response.json();items=Object.fromEntries(data.items.map(i=>[i.id,i]));
      // Drop tables reference gear families; show their first quality variant for lookup.
      for(const it of data.items)if(it.base&&!items[it.base])items[it.base]=it;
      const gemCount=data.items.filter(isChuLinh).length;
      $('summary').innerHTML=`<span><b>${fmt(data.items.filter(i=>i.slot).length)}</b>trang bị</span><span><b>${data.pets.length}</b>linh thú</span><span><b>${fmt(gemCount)}</b>chú linh</span><span><b>${data.chapters.length}</b>chương</span><span><b>${data.brews.length}</b>đan phương</span>`;
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
