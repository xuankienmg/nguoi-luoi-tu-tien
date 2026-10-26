# Final realm artwork

Created using the built-in imagegen tool, 2026-10-26. Original sources are preserved as `gear-atlas.png` and `skill-atlas.png`. Equipment uses the background-removed `gear-atlas-transparent.png`; the dark original is retained for provenance. Runtime icons are 128 x 128 PNG files under `wwwroot/img/final_realms/gear` and `wwwroot/img/final_realms/skills`.

Re-extract from the repository root on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/final_realm_art/extract.ps1 -GearAtlas tools/final_realm_art/gear-atlas-transparent.png -SkillAtlas tools/final_realm_art/skill-atlas.png
```

The script only extracts and fits the atlas cells, preserving source alpha and adding transparent padding. It does not repaint the art. The gear atlas has five columns and four rows; skill atlas has four columns and seven rows. Original dimensions may differ from the requested dimensions; extraction uses the actual dimensions. Equipment backgrounds must be transparent; the skill atlas retains its original dark card backgrounds. Add `-GearOnly` to update equipment without regenerating skill icons.

Validate transparency and wiki synchronization:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/final_realm_checks/transparency.ps1
```

### Background removal edit prompt

Built-in imagegen edit of `gear-atlas.png`, with `transparent_background: true`:

Edit this exact game gear atlas: REMOVE ONLY the dark navy/black background and replace it with genuinely transparent alpha, including dark background holes inside chains, rings and the armillary spheres. Preserve every equipment object, all colors, fine details, delicate gold and silver chains, purple lightning and golden glow. Preserve the exact canvas aspect ratio and exact five columns by four rows grid, object ordering and object positions. Twenty items total. No rearranging, additions, text, checkerboard texture, colored background or black matte. The intended result is the same art sheet as transparent cutouts so it can be extracted into individual inventory icons. Retain semi-transparent light effects with clean non-black edges.

Cleanup edit, also using built-in imagegen with transparent background:

Clean up this transparent game equipment atlas. Keep the exact same five-column, four-row atlas layout, same 20 equipment objects and positions. Remove ALL stray red, hot-pink, neon blue and yellow speckled pixels, stray detached fragments and colored matte fringes around the cutouts. These are background-removal artifacts, not equipment details. In particular there must be NO red dots above/around the white gold robe or boots. Crisp, clean natural antialiased alpha edges on every object. Preserve the purple/silver equipment of the top two rows and white/jade/gold equipment of bottom two rows. Retain only restrained natural violet glow directly along purple swords/gems, and natural gold reflections on gold metal. All empty areas and spaces inside rings/chains must be transparent. Preserve the canvas proportions, exact object center positions, object silhouettes, grid and ordering. Do not add or move objects. No black or colored background, no colored noise, no checkerboard texture.

## Generation prompts

### Gear

Create ONE production game inventory ICON ATLAS, exactly 5 columns by 4 rows, square 1024x1024 canvas, equal 204.8 pixel square cells, no gaps. Each cell has a solid very dark navy background #10121c, NO borders, NO text, NO labels. Each object is centered within its cell with 18% margin, fully isolated and never crosses cells. High quality hand painted pixel edged Vietnamese xianxia RPG inventory art, strong clear silhouettes at 64px, luminous metal and gems. Row 1 violet silver thunder tribulation equipment: sword, celestial orb in armillary rings, ornate crown, flowing robe, pair of armored bracers. Row 2 SAME violet silver equipment set continuation: flowing trousers, pair of boots, pendant necklace, paper magic talisman, gemstone ring. Row 3 ivory jade warm gold primordial immortal equipment: sword, celestial orb in armillary rings, ornate crown, flowing robe, pair of armored bracers. Row 4 SAME ivory jade gold equipment set continuation: flowing trousers, pair of boots, pendant necklace, paper magic talisman, gemstone ring. IMPORTANT exactly twenty icons, exactly five per row in the specified order; no people, hands, watermark, interface, floor or scenery. The objects should be large and readable yet stay inside 64% of each cell.

### Skills

One professional xianxia RPG SPELL ICON ATLAS, exactly 4 columns and 7 rows of equal rectangular cells, no gaps, no grid lines, no text or labels. Canvas portrait 1024x1792 (each cell square 256x256), perfectly regular full bleed grid. Each square cell has solid very dark navy background #10121c, a centered magical symbol fully contained with 18% margin. Hand-painted fantasy pixel-edged game art, rich bright jewels and spell glow, iconic readability at 64px. Columns in EVERY row: 1 tribulation active spell with violet lightning accents; 2 tribulation passive protective sigil with violet silver frame; 3 primordial active spell with ivory warm gold accents; 4 primordial passive protective sigil with jade gold frame. Seven rows in this exact order: ROW1 FIRE: blazing flame eruption, fiery protective body sigil, white-gold purifying flame lotus, immortal flame heart. ROW2 WATER: tsunami wave, protective water ring, celestial river spiral, infinite water pearl. ROW3 WOOD: giant green sacred tree strike, leaf life sigil, emerald tree of heaven strike, green longevity heart. ROW4 EARTH: jagged mountain impact, golden stone body shield, world mountain strike, primordial earth mandala. ROW5 LIGHTNING: violet thunderbolt storm, electric body sigil, whitegold lightning judgment, electric primordial heart. ROW6 ICE: ice prison spikes, blue frost shield crystal, eternal ice flower strike, pure ice heart. ROW7 WIND: teal tornado blades, feather shield sigil, celestial wind crescent slash, jade wind spiral sigil. Exactly twenty eight distinct icons, four per row, seven rows. No people, numbers, written runes, ornament outside cells, UI, borders or scenery. Maintain consistent centered scale.
