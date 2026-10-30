/* Reference-cropped navigation icons with SVG fallbacks for utility tabs. */
const CONCEPT_ICONS={
 req:'<path d="M14 17c2 3 6 3 8 0m-5-3-4 3-3 3h20l-3-3-4-3m-8 1 3-5h4l3 5"/><circle cx="20" cy="7" r="3"/><path d="M9 25h22M13 29h14"/>',
 inv:'<path d="M14 7h12l-3 6c7 4 9 9 7 15-4 4-16 4-20 0-2-6 0-11 7-15zM15 13h10M18 18v9m4-9v9"/>',
 craft:'<path d="M15 6h10l-2 7c1 4 8 5 7 12-1 8-19 8-20 0-1-7 6-8 7-12zM15 7h10M17 13h6M15 25c3 3 7 3 10 0"/>',
 map:'<path d="m6 9 9-4 10 4 9-4v25l-9 4-10-4-9 4zM15 5v25m10-21v25M9 19l4-4m5-1 4 5m6 3 3-4"/>',
 skill:'<path d="M6 7c6-3 10-2 14 1 4-3 8-4 14-1v24c-6-3-10-2-14 1-4-3-8-4-14-1zM20 8v24M10 12l6 1m-6 5 6 1m8-6 6-1m-6 7 6-1"/>',
 pet:'<ellipse cx="11" cy="11" rx="3" ry="4"/><ellipse cx="19" cy="7" rx="3" ry="4"/><ellipse cx="27" cy="11" rx="3" ry="4"/><ellipse cx="33" cy="18" rx="2.5" ry="3.5"/><path d="M12 26c0-5 4-11 8-11s9 7 9 12c0 5-5 5-9 3-4 2-8 1-8-4z"/>',
 market:'<path d="M7 17h26L29 7H11zM10 17v15h20V17M5 32h30M17 32V23h7v9M16 7l-2 10m10-10 2 10"/>',
 admin:'<path d="m20 5 4 5 6 1 1 6 4 4-4 5-1 6-6 1-4 4-4-4-6-1-1-6-4-5 4-4 1-6 6-1z"/><circle cx="20" cy="21" r="5"/>',
 vault:'<polygon points="20,6 34,14 29,33 11,33 6,14" fill="none" stroke="currentColor" stroke-width="2"/><polygon points="20,6 26,14 20,33 14,14" fill="none" stroke="currentColor" stroke-width="1.5"/><line x1="6" y1="14" x2="34" y2="14" stroke="currentColor" stroke-width="1.5"/>'
};
function conceptIcon(key){if(['req','inv','craft','map','skill','pet'].includes(key))return `<img class="nav-icon nav-cut" src="img/header/nav-${key}.png" alt="" aria-hidden="true">`;return `<svg class="nav-icon" viewBox="0 0 40 40" aria-hidden="true">${CONCEPT_ICONS[key]||CONCEPT_ICONS.pet}</svg>`;}
document.querySelectorAll('.book .tabs>[data-tab]').forEach(button=>{
 const label=button.textContent.trim();button.innerHTML=conceptIcon(button.dataset.tab)+`<span>${label}</span>`;
});
const petButton=document.getElementById('btnPet');if(petButton)petButton.innerHTML=conceptIcon('pet');
function renderConceptNavigation(){
 const balance=(id,key)=>{const node=document.getElementById(id);if(node)node.textContent=S?fmt(S.inventory?.[key]||0):'—';};
 balance('navStones','linh_thach');balance('navPremium','thuong_pham_linh_thach');
}
document.getElementById('navMarket')?.addEventListener('click',()=>openTab('market'));
