/* Gold and jade navigation art with SVG fallbacks for utility tabs. */
const CONCEPT_ICONS={
 req:'<path d="M14 17c2 3 6 3 8 0m-5-3-4 3-3 3h20l-3-3-4-3m-8 1 3-5h4l3 5"/><circle cx="20" cy="7" r="3"/><path d="M9 25h22M13 29h14"/>',
 inv:'<path d="M14 7h12l-3 6c7 4 9 9 7 15-4 4-16 4-20 0-2-6 0-11 7-15zM15 13h10M18 18v9m4-9v9"/>',
 craft:'<path d="M15 6h10l-2 7c1 4 8 5 7 12-1 8-19 8-20 0-1-7 6-8 7-12zM15 7h10M17 13h6M15 25c3 3 7 3 10 0"/>',
 map:'<path d="m6 9 9-4 10 4 9-4v25l-9 4-10-4-9 4zM15 5v25m10-21v25M9 19l4-4m5-1 4 5m6 3 3-4"/>',
 skill:'<path d="M6 7c6-3 10-2 14 1 4-3 8-4 14-1v24c-6-3-10-2-14 1-4-3-8-4-14-1zM20 8v24M10 12l6 1m-6 5 6 1m8-6 6-1m-6 7 6-1"/>',
 pet:'<ellipse cx="11" cy="11" rx="3" ry="4"/><ellipse cx="19" cy="7" rx="3" ry="4"/><ellipse cx="27" cy="11" rx="3" ry="4"/><ellipse cx="33" cy="18" rx="2.5" ry="3.5"/><path d="M12 26c0-5 4-11 8-11s9 7 9 12c0 5-5 5-9 3-4 2-8 1-8-4z"/>',
 market:'<path d="M7 17h26L29 7H11zM10 17v15h20V17M5 32h30M17 32V23h7v9M16 7l-2 10m10-10 2 10"/>',
 admin:'<path d="m20 5 4 5 6 1 1 6 4 4-4 5-1 6-6 1-4 4-4-4-6-1-1-6-4-5 4-4 1-6 6-1z"/><circle cx="20" cy="21" r="5"/>',
 vault:'<polygon points="20,6 34,14 29,33 11,33 6,14" fill="none" stroke="currentColor" stroke-width="2"/><polygon points="20,6 26,14 20,33 14,14" fill="none" stroke="currentColor" stroke-width="1.5"/><line x1="6" y1="14" x2="34" y2="14" stroke="currentColor" stroke-width="1.5"/>',
 kyduyen:'<path d="M9 6h20a3 3 0 0 1 3 3v23H12a3 3 0 0 1-3-3zM9 29a3 3 0 0 1 3-3h20"/><path d="m20 10 1.6 3.4 3.4 1.6-3.4 1.6L20 20l-1.6-3.4-3.4-1.6 3.4-1.6z"/>'
};
const CONCEPT_ART={req:'tu_luyen',inv:'hanh_trang',craft:'luyen_che',map:'ban_do',pet:'linh_thu',market:'phuong_thi'};
function conceptIcon(key){if(CONCEPT_ART[key])return `<img class="nav-icon nav-art" src="img/ui/action-v2/${CONCEPT_ART[key]}.png" alt="" aria-hidden="true">`;if(key==='skill')return `<img class="nav-icon nav-cut" src="img/header/nav-skill.png" alt="" aria-hidden="true">`;return `<svg class="nav-icon" viewBox="0 0 40 40" aria-hidden="true">${CONCEPT_ICONS[key]||CONCEPT_ICONS.pet}</svg>`;}
document.querySelectorAll('.book .tabs>[data-tab]').forEach(button=>{
 const label=button.textContent.trim();button.innerHTML=conceptIcon(button.dataset.tab)+`<span>${label}</span>`;
});
document.querySelectorAll('.book .tabs>[data-group]').forEach(button=>{button.innerHTML=conceptIcon('req')+`<span>${button.textContent.trim()}</span>`;});   // nhóm Tu luyện dùng biểu tượng Đột phá
const petButton=document.getElementById('btnPet');if(petButton)petButton.innerHTML=conceptIcon('pet');
// Simple gold outlines keep secondary navigation readable at small sizes.
const SUBTAB_OUTLINES={
 dot_pha:'<path d="M12 3c2 4-2 5 1 8 1-2 2-3 3-4 3 4 4 7 2 10a7 7 0 0 1-12 0c-2-3 0-7 3-9-1 4 1 5 2 6 2-4-1-6 1-11Z"/>',
 cong_phap:'<path d="M3 5c3-1 6-1 9 1 3-2 6-2 9-1v15c-3-1-6-1-9 1-3-2-6-2-9-1ZM12 6v15M6 9l3 1m-3 3 3 1m6-4 3-1m-3 5 3-1"/>',
 ky_duyen:'<path d="M5 3h13a2 2 0 0 1 2 2v16H7a2 2 0 0 1-2-2ZM5 19c0-2 2-3 4-3h11"/><path d="m12 6 1.5 3.5L17 11l-3.5 1.5L12 16l-1.5-3.5L7 11l3.5-1.5Z"/>',
 luyen_khi:'<path d="m5 3 6 3-3 5-6-3Zm4 6 9 11M3 15h18l-4 3v3H7v-3Z"/>',
 dan_duoc:'<path d="M9 3h6v4c0 2 5 4 5 9a8 8 0 0 1-16 0c0-5 5-7 5-9ZM8 3h8M6 14h12"/><circle cx="10" cy="17" r="1.5"/><circle cx="15" cy="18" r="1"/>',
 kham_tinh_thach:'<path d="m7 3-4 6 9 12 9-12-4-6ZM3 9h18M7 3l5 18 5-18M7 3h10"/>',
 ban:'<path d="M9 3h6l-2 5c5 2 8 6 7 10-1 4-15 4-16 0-1-4 2-8 7-10ZM8 8h8"/><path d="M14 12h-3a2 2 0 0 0 0 4h2a2 2 0 0 1 0 4h-3m2-9v10"/>',
 chuong_thu:'<path d="m2 11 10-8 10 8M5 9v12h14V9M9 21v-7h6v7"/>',
 ghep_sao:'<path d="m12 2 2 5 5 .5-4 3 1 5-4-2.5L8 16l1-5-4-3 5-.5ZM4 17v5m-2-2 2 2 2-2m14-3v5m-2-2 2 2 2-2"/>',
 boi_duong:'<path d="M3 12h18c-1 7-3 9-9 9s-8-2-9-9ZM5 9c0-3 3-5 6-4-1 3-3 5-6 4Zm10 0c-1-3 1-6 5-6 0 4-2 6-5 6Z"/><circle cx="12" cy="9" r="2"/>',
 ap_trung:'<path d="M20 15c0 5-4 7-8 7s-8-2-8-7S8 2 12 2s8 8 8 13ZM5 12l4 2 3-3 3 3 4-2"/>',
 thu_dien:'<path d="M5 3h14v18H7a2 2 0 0 1-2-2ZM5 18h14"/><ellipse cx="9" cy="8" rx="1" ry="1.5"/><ellipse cx="13" cy="7" rx="1" ry="1.5"/><ellipse cx="16" cy="9" rx="1" ry="1.5"/><path d="M9 13c0-2 2-4 3-4s3 2 3 4c0 2-2 1-3 1s-3 1-3-1Z"/>',
 tap_hoa_vip:'<path d="m4 5 4 3 4-5 4 5 4-3-2 8H6ZM5 16h14v5H5ZM9 16v5m6-5v5"/>',
 tap_hoa:'<path d="M3 10 6 3h12l3 7ZM5 10v11h14V10M2 21h20M9 21v-7h6v7M9 3l-1 7m7-7 1 7"/>',
 mua:'<path d="M3 10h18l-3 11H6ZM7 10l3-7m7 7-3-7M12 12v6m-3-3 3 3 3-3"/>',
 cua_toi:'<path d="M5 3h14v18H7a2 2 0 0 1-2-2ZM5 18h14"/><circle cx="12" cy="8" r="2"/><path d="M8 15c0-5 8-5 8 0Z"/>'
};
function subtabIcon(key){return `<svg class="subtab-art" viewBox="0 0 24 24" aria-hidden="true">${SUBTAB_OUTLINES[key]||''}</svg>`;}
const CULTIVATION_SUBTAB_ART={req:'dot_pha',skill:'cong_phap',kyduyen:'ky_duyen'};
document.querySelectorAll('.subtabs[data-group="tuluyen"]>[data-tab]').forEach(button=>{
 const art=CULTIVATION_SUBTAB_ART[button.dataset.tab];if(!art)return;
 const label=button.textContent.trim();
 button.innerHTML=subtabIcon(art)+`<span>${label}</span>`;
});
const CRAFT_SUBTAB_ART={fuse:'luyen_khi',craft:'dan_duoc',vault:'kham_tinh_thach',sell:'ban'};
document.querySelectorAll('.subtabs[data-group="craft"]>[data-lc]').forEach(button=>{
 const art=CRAFT_SUBTAB_ART[button.dataset.lc];if(!art)return;
 const label=button.textContent.trim();
 button.innerHTML=subtabIcon(art)+`<span>${label}</span>`;
});
const PET_SUBTAB_ART={stable:'chuong_thu',star:'ghep_sao',train:'boi_duong',hatch:'ap_trung',codex:'thu_dien'};
document.querySelectorAll('.subtabs[data-group="pet"]>[data-pet-action]').forEach(button=>{
 const art=PET_SUBTAB_ART[button.dataset.petAction.split(':')[1]];if(!art)return;
 const label=button.textContent.trim();
 button.innerHTML=subtabIcon(art)+`<span>${label}</span>`;
});
const MARKET_SUBTAB_ART={vip:'tap_hoa_vip',shop:'tap_hoa',buy:'mua',sell:'ban',mine:'cua_toi'};
document.querySelectorAll('.subtabs[data-group="market"]>[data-mk-view]').forEach(button=>{
 const art=MARKET_SUBTAB_ART[button.dataset.mkView];if(!art)return;
 const label=button.textContent.trim();
 button.innerHTML=subtabIcon(art)+`<span>${label}</span>`;
});
function renderConceptNavigation(){
 const balance=(id,key)=>{const node=document.getElementById(id);if(node)node.textContent=S?fmt(S.inventory?.[key]||0):'—';};
 balance('navStones','linh_thach');balance('navPremium','thuong_pham_linh_thach');
}
document.getElementById('navMarket')?.addEventListener('click',()=>openTab('market'));
