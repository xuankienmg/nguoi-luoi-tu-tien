// Chuyển ngôn ngữ trang giới thiệu: Tiếng Việt (gốc, viết sẵn trong HTML) ↔ English.
// Phần tử có data-i18n="khóa" được thay innerHTML theo EN bên dưới; data-i18n-alt thay alt của ảnh.
// Chọn: ?lang=vi|en → lựa chọn đã lưu → ngôn ngữ trình duyệt (không phải tiếng Việt thì English).
(() => {
  const KEY = 'van-dao-site-lang';
  const EN = {
    title: 'Người Lười Tu Tiên — an idle cultivation game on your Windows taskbar',
    desc: 'Người Lười Tu Tiên is an idle xianxia cultivation game that lives on your Windows taskbar: auto-explore, break through realms, brew pills, raise spirit beasts. Free for Windows 10/11 64-bit.',
    h1: 'Cultivate right on your taskbar.',
    lede: 'A small strip sits just above the Windows taskbar. Your cultivator explores, slays demons and gathers cultivation on their own while you work. When you have a moment, open it up to break through, brew pills and raise spirit beasts.',
    dlMain: 'Download',
    dlNote: 'Free. The ZIP contains the game and a self-updating Launcher.',
    stripRealm: 'Qi Refining',
    stripStage: 'Stage 3 · 6 cultivation/s',
    stripTrip: 'Expedition',
    fTitle: 'Idle, but never dull',
    f1t: 'Auto-exploration',
    f1d: 'On every trip your cultivator meets monsters, fights turn by turn and earns cultivation and loot. Cultivation keeps building while your PC is off, up to 2 hours.',
    f2t: 'Realm breakthroughs',
    f2d: 'Qi Refining, Foundation Establishment, Golden Core… up to Body Integration. Gather pills, Boss cores and spirit stones, then gamble on the breakthrough chance. Fail, and you may suffer a qi deviation.',
    f3t: 'Pills and equipment',
    f3d: 'Combine pills 9 → 1, equip swords, robes and rings by rarity, sell spare gear for spirit stones or trade with other players at the Market.',
    f4t: 'Spirit beasts',
    f4d: 'Hatch eggs, feed and evolve them through three forms. Ancient spirit beasts have their own divine abilities: revival, or lightning that paralyzes monsters.',
    f5t: 'Techniques and outfits',
    f5d: 'Skill trees for Fire, Water and Wood. Outfits change with your realm, with idle, meditation and combat animations.',
    f6t: 'Online Heaven Ranking',
    f6d: 'Compare realm, combat power, stages cleared and strongest spirit beast with other players. Fully playable offline.',
    gallery: 'In-game screenshots',
    g1: 'Spirit beast codex', g1alt: 'Beast codex: spirit beast species and their three growth forms',
    g2: 'Void Refining breakthrough', g2alt: 'Void Refining breakthrough, the cultivator meditating inside a formation',
    g3: 'Selling gear by quality', g3alt: 'Inventory and the gear selling counter',
    g4: 'Egg hatching', g4alt: 'Hatching spirit beast eggs',
    g5: 'Inventory and equipment', g5alt: 'Inventory of a Body Integration cultivator wearing the full Crimson Gold set 10/10',
    iTitle: 'Installation',
    i1: '<b>Download the ZIP</b> with the button at the top of the page.',
    i2: '<b>Extract it</b>: right-click the file → <i>Extract All…</i> and pick a dedicated folder such as <code>D:\\Games\\VanDao</code>. Don\'t run it from inside the ZIP and don\'t put it in <code>Program Files</code>.',
    i3: '<b>Open <code>NguoiLuoiTuTienLauncher.exe</code></b>. The Launcher checks for a new version and downloads updates automatically.',
    i4: '<b>Click Play.</b> The game strip appears right above the taskbar. Right-click the strip to change its position, size or language, or hide it to the tray.',
    iAside: 'Progress is saved in <code>%LocalAppData%\\VanDao</code>, so updating never loses your character.',
    ssTitle: 'Windows says “Windows protected your PC”?',
    ssBody: 'The game isn\'t code-signed yet, so Windows SmartScreen warns on the first run. Click <b>More info</b>, then <b>Run anyway</b>. If your antivirus blocks it, only download the game from this page.',
    reqTitle: 'Requirements',
    req1: 'Windows 10 or 11, 64-bit',
    req2: 'Microsoft Edge WebView2 Runtime (built into Windows 11; for Windows 10 get it <a href="https://developer.microsoft.com/microsoft-edge/webview2/">here</a>)',
    req3: 'About 500 MB of free space',
    req4: 'Internet for the Heaven Ranking, Market and updates',
    reqNote: 'No .NET install needed, it\'s bundled.',
    relTitle: 'Latest version',
    relNotesLang: 'Release notes are written in Vietnamese.',
    relAll: 'Earlier updates →',
    // trang Cập nhật (updates.html)
    updatesTitle: 'Updates · Người Lười Tu Tiên',
    updatesDesc: 'What changed in each Người Lười Tu Tiên release, from the latest to the earliest.',
    navHome: 'Home & download',
    upTitle: 'Update log',
    upLede: 'Every release, newest at the top.',
    upIndex: 'Versions',
    upLoading: 'Loading updates…',
    // chữ do site.js / updates.js điền theo bản phát hành
    download: 'Download', released: 'Released {0}.', freeReleased: 'Free. Released {0}. The ZIP contains the game and a self-updating Launcher.',
    upLatest: 'Latest', upEmpty: 'No notes for this version.', upError: 'Couldn\'t load the updates. Please try again later.',
  };
  const VI = {
    download: 'Tải', released: 'Phát hành ngày {0}.', freeReleased: 'Miễn phí. Phát hành ngày {0}. File ZIP gồm game và Launcher tự cập nhật.',
    relNotesLang: '',
    upLatest: 'Mới nhất', upEmpty: 'Bản này không có ghi chú.', upError: 'Không tải được các bản cập nhật. Thử lại sau.',
  };

  const q = new URLSearchParams(location.search).get('lang');
  let saved = null;
  try { saved = localStorage.getItem(KEY); } catch (e) {}
  let lang = q === 'en' || q === 'vi' ? q : saved === 'en' || saved === 'vi' ? saved : /^vi\b/i.test(navigator.language || 'vi') ? 'vi' : 'en';

  const orig = new Map();      // phần tử → nội dung tiếng Việt gốc
  const listeners = [];
  const meta = document.querySelector('meta[name="description"]');
  // trang phụ có tiêu đề / mô tả riêng: <html data-page="updates"> → EN.updatesTitle, EN.updatesDesc
  const page = document.documentElement.dataset.page;
  const titleKey = page ? page + 'Title' : 'title', descKey = page ? page + 'Desc' : 'desc';

  function apply(){
    document.documentElement.lang = lang;
    for (const el of document.querySelectorAll('[data-i18n]')){
      if (!orig.has(el)) orig.set(el, el.innerHTML);
      el.innerHTML = lang === 'en' ? EN[el.dataset.i18n] ?? orig.get(el) : orig.get(el);
    }
    for (const el of document.querySelectorAll('[data-i18n-alt]')){
      const k = 'alt:' + el.dataset.i18nAlt;
      if (!orig.has(k)) orig.set(k, el.alt);
      el.alt = lang === 'en' ? EN[el.dataset.i18nAlt] ?? orig.get(k) : orig.get(k);
    }
    for (const el of document.querySelectorAll('[data-i18n-label]')){
      const k = 'label:' + el.dataset.i18nLabel;
      if (!orig.has(k)) orig.set(k, el.getAttribute('aria-label'));
      el.setAttribute('aria-label', lang === 'en' ? EN[el.dataset.i18nLabel] ?? orig.get(k) : orig.get(k));
    }
    if (!orig.has('title')){ orig.set('title', document.title); orig.set('desc', meta?.content); }
    document.title = lang === 'en' ? EN[titleKey] : orig.get('title');
    if (meta) meta.content = lang === 'en' ? EN[descKey] : orig.get('desc');
    for (const b of document.querySelectorAll('[data-lang]')) b.setAttribute('aria-pressed', String(b.dataset.lang === lang));
    listeners.forEach(f => f(lang));
  }

  window.siteLang = {
    get lang(){ return lang; },
    /** Chữ động cho site.js, {0} {1}… thay bằng tham số. */
    t: (key, ...args) => ((lang === 'en' ? EN : VI)[key] ?? '').replace(/\{(\d)\}/g, (_, i) => args[i]),
    onChange: f => listeners.push(f),
  };

  document.addEventListener('click', e => {
    const b = e.target.closest('[data-lang]');
    if (!b) return;
    e.preventDefault();
    lang = b.dataset.lang;
    try { localStorage.setItem(KEY, lang); } catch (err) {}
    const u = new URL(location.href); u.searchParams.delete('lang'); history.replaceState(null, '', u);
    apply();
  });
  apply();
})();
