// Chuyển ngôn ngữ trang giới thiệu: Tiếng Việt (gốc, viết sẵn trong HTML) ↔ English.
// Phần tử có data-i18n="khóa" được thay innerHTML theo EN bên dưới; data-i18n-alt thay alt của ảnh; data-i18n-label thay aria-label.
// Chọn: ?lang=vi|en → lựa chọn đã lưu → ngôn ngữ trình duyệt (không phải tiếng Việt thì English).
(() => {
  const KEY = 'van-dao-site-lang';
  const EN = {
    title: 'Người Lười Tu Tiên — an idle cultivation game on your Windows taskbar',
    desc: 'Người Lười Tu Tiên is an idle xianxia cultivation game that lives on your Windows taskbar: auto-explore, break through realms, brew pills, raise spirit beasts. Free for Windows 10/11 64-bit.',
    navMain: 'Main navigation', navFeatures: 'Features', navRealms: 'Realms', navInstall: 'Install', navUpdates: 'Updates', navFoot: 'Footer links',
    kicker: 'Idle cultivation game · Free for Windows',
    h1: 'Cultivate right on your taskbar.',
    lede: 'A small strip sits just above the Windows taskbar. Your cultivator explores, slays demons and gathers cultivation on their own while you work. When you have a moment, open it up to break through, brew pills and raise spirit beasts.',
    dlMain: 'Download',
    dlNote: 'Free. The ZIP contains the game and a self-updating Launcher.',
    wikiLink: 'Gear &amp; spirit beast wiki',
    stripRealm: 'Qi Refining',
    stripStage: 'Stage 3 · 6 cultivation/s',
    stripTrip: 'Expedition',
    statsLabel: 'At a glance',
    s1: 'realms, from Qi Refining to True Immortal',
    s2: 'map chapters, each with a Boss lair',
    s3: 'spirit beast species, three growth forms',
    s4n: '2 hrs', s4: 'of cultivation kept while your PC is off',
    fKicker: 'Gameplay',
    fTitle: 'Idle, but never dull.',
    fLede: 'Your cultivator handles the repetitive part. The decisions — when to break through, which beast to raise, which set to wear — are left to you.',
    p1k: '01 · Realm breakthroughs',
    p1t: 'Gamble on the odds, survive the tribulation.',
    p1d: 'Gather pills, Boss cores and spirit stones, then try your luck. Every realm unlocks its own art and formation. Fail, and you may suffer a qi deviation.',
    p1c1: 'Qi Gathering Pill', p1c2: 'Boss cores', p1c3: 'Spirit stones',
    p2k: '02 · Spirit beasts',
    p2t: 'Hatch, raise, fight side by side.',
    p2d: 'Eggs drop from Bosses or the Spirit Beast spin. Feed them and evolve them through three forms: Hatchling, Adult, Divine Beast. Ancient spirit beasts have their own divine abilities: revival, or lightning that paralyzes monsters.',
    p2c1: 'Fire', p2c2: 'Ice', p2c3: 'Wood', p2c4: 'Earth', p2c5: 'Ancient',
    p2tabs: 'Spirit beast screenshots',
    p3k: '03 · Pills &amp; equipment',
    p3t: 'Complete the set, sell the rest at the Market.',
    p3d: 'Combine pills 9 → 1, equip swords, robes and rings by rarity, collect sets for bonus effects. Sell spare gear for spirit stones or list it at the Market to trade with other players.',
    p3c1: '10 equipment slots', p3c2: 'Set bonuses', p3c3: 'Player market',
    p3tabs: 'Equipment screenshots',
    f1t: 'Auto-exploration',
    f1d: 'Every trip meets monsters, fights turn by turn and earns cultivation and loot. Cultivation keeps building while your PC is off, up to 2 hours.',
    f5t: 'Techniques &amp; outfits',
    f5d: 'Activate one technique as your basic attack. Outfits change with your realm, with idle, meditation and combat animations.',
    f6t: 'Online Heaven Ranking',
    f6d: 'Compare realm, combat power, stages cleared and strongest spirit beast with other players. Fully playable offline.',
    g1: 'Beast codex', g1alt: 'Beast codex: spirit beast species and their three growth forms',
    g2alt: 'Void Refining breakthrough, the cultivator meditating inside a formation',
    g3: 'Selling gear', g3alt: 'Inventory and the gear selling counter',
    g4: 'Egg hatching', g4alt: 'Hatching spirit beast eggs',
    g5: 'Inventory', g5alt: 'Inventory of a Body Integration cultivator wearing the full Crimson Gold set 10/10',
    rKicker: 'The path of cultivation',
    rTitle: 'Ten realms, one step from ascension.',
    rHint: 'Pick a realm to see the details (descriptions in Vietnamese)',
    rStages: 'Stages', rChapters: 'Map chapters', rLairs: 'Boss lairs',
    bKicker: 'Maps &amp; Bosses',
    bTitle: 'At the end of every chapter, a demon waits.',
    bLede: 'Bosses drop cores for breakthroughs, spirit beast eggs and rare gear. Their crit and combo resistance rises chapter by chapter — clear Normal, then try Hard, Nightmare and Hell.',
    d1: 'Normal', d2: 'Hard', d3: 'Nightmare', d4: 'Hell',
    b1: 'Chapter 6 · Đoạn Hồn Nhai', b2: 'Chapter 8 · Táng Tiên Cốc',
    cName: 'Chapter', cRealm: 'Realm', cLair: 'Boss lair',
    bMore: 'See every chapter’s drops in the Wiki →',
    iKicker: 'Installation',
    iTitle: 'Four steps, under two minutes.',
    i1t: 'Download the ZIP', i1: 'Use the Download button at the top of the page.',
    i2t: 'Extract to its own folder', i2: 'Right-click → <i>Extract All…</i>, e.g. <code>D:\\Games\\VanDao</code>. Don\'t run it from inside the ZIP or put it in <code>Program Files</code>.',
    i3t: 'Open the Launcher', i3: 'Run <code>NguoiLuoiTuTienLauncher.exe</code>. It checks for and downloads new versions.',
    i4t: 'Click Play', i4: 'The game strip appears above the taskbar. Right-click it to change position, size or language, or hide it to the tray.',
    iAside: 'Progress is saved in <code>%LocalAppData%\\VanDao</code> — updating never loses your character.',
    ssTitle: 'Windows says “Windows protected your PC”?',
    ssBody: 'The game isn\'t code-signed yet, so Windows SmartScreen warns on the first run. Click <b>More info</b>, then <b>Run anyway</b>. If your antivirus blocks it, only download the game from this page.',
    reqTitle: 'Requirements',
    req1: 'Windows 10 or 11, 64-bit · about 500 MB free',
    req2: 'Microsoft Edge WebView2 Runtime (built into Windows 11; for Windows 10 get it <a href="https://developer.microsoft.com/microsoft-edge/webview2/">here</a>)',
    req4: 'Internet for the Heaven Ranking, Market and updates · .NET is bundled',
    relTitle: 'Latest version',
    relNotesLang: 'Release notes are written in Vietnamese.',
    relAll: 'Earlier updates →',
    ctaTitle: 'Start cultivating today.',
    ctaLede: 'Free. No need to open the game window — just let it cultivate.',
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
