# Địa Ngục 1 monster artwork

New Chân Tiên-tier monsters for Địa Ngục 1, inspired by characters of the classic novel *Tây Du Ký* (Journey to the West). Only the novel's characters and motifs are referenced; designs must not imitate any specific film, TV series, game or illustration (e.g. Black Myth: Wukong, the 1986 TV series).

Two atlases, each 3 columns × 2 rows, transparent background. Save the generated originals here as `boss-atlas.png` and `monster-atlas.png`, then extract from the repository root on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/hell_map_art/extract.ps1 -BossAtlas tools/hell_map_art/boss-atlas.png -MonsterAtlas tools/hell_map_art/monster-atlas.png
```

The script refuses atlases with an opaque corner, crops each cell to its visible pixels and fits it into the four runtime variants used by `map.js` and `battle-fx.js`: `x.png` 320×221, `x_hd.png` 240×240, `x_48.png`, `x_24.png` in `wwwroot/img/hell/`. It does not repaint the art. Existing monster files are never touched.

| Cell | File | Character | Used as |
|---|---|---|---|
| Boss 1 | `boss_thoi_phan_quan` | Thôi Phán Quan | Boss ch.1 Quỷ Môn Quan |
| Boss 2 | `boss_hac_hung_tinh` | Hắc Hùng Tinh | Boss ch.2 Hắc Thạch Nguyên |
| Boss 3 | `boss_thanh_anh_dai_vuong` | Thánh Anh Đại Vương (Hồng Hài Nhi) | Boss ch.3 Dung Nham Hà |
| Boss 4 | `boss_thiet_phien_cong_chua` | Thiết Phiến Công Chúa | Boss ch.7 Huyết Diệm Uyên |
| Boss 5 | `boss_nguu_ma_vuong` | Ngưu Ma Vương | Boss ch.8 Nham Ma Điện |
| Boss 6 | `boss_diem_la_vuong` | Diêm La Vương | Boss ch.10 Diêm La Thiên Cung |
| Monster 1 | `quai_nguu_dau` | Ngưu Đầu | normal |
| Monster 2 | `quai_ma_dien` | Mã Diện | normal |
| Monster 3 | `quai_hac_vo_thuong` | Hắc Vô Thường | normal |
| Monster 4 | `quai_bach_vo_thuong` | Bạch Vô Thường | normal |
| Monster 5 | `quai_hoa_diem_tieu_yeu` | Hỏa Diệm Sơn Tiểu Yêu | normal |
| Monster 6 | `tinh_bach_cot_tinh` | Bạch Cốt Tinh | elite |

## Style reference

Match the existing monsters (`wwwroot/img/boss_am_ma_hd.png`, `wwwroot/img/tinh_cot_long_v4_hd.png`): hand-painted pixel art with crisp dark outlines, full body, three-quarter view **facing left** (toward the player), strong readable silhouette at 48 px, transparent background, no ground shadow.

## Generation prompts

Use an image generator with a transparent-background option. If the generator cannot produce real alpha, run a background-removal edit afterwards (see `tools/final_realm_art/README.md` for the wording used there).

### Bosses (`boss-atlas.png`)

Create ONE game sprite ATLAS, exactly 3 columns by 2 rows, landscape 1536x1024 canvas, six equal 512x512 square cells, no gaps, no grid lines, no borders, no text or labels. TRUE TRANSPARENT background everywhere, no floor, no shadow, no scenery. Each cell contains one full-body boss character centered with 10% margin, never crossing into another cell, three-quarter view FACING LEFT. Style: high quality hand-painted pixel art for a Vietnamese xianxia idle RPG, crisp dark outlines, rich infernal lighting, readable silhouette at 48 pixels, imposing late-game boss presence. Characters are inspired by the classic Chinese novel Journey to the West, original designs, not copied from any film, TV series or video game. ROW 1: (1) Judge Cui of the underworld, stern bearded official in black and crimson judge robes and winged official hat, holding a giant brush and the ledger of life and death, ghostly green flames; (2) Black Bear Demon, huge black bear spirit warrior in dark iron armor with a black-tasseled spear, smoldering embers; (3) Red Boy the Holy Infant King, fierce child demon king with red skin and topknots, golden fire-tipped spear, swirling Samadhi true fire around him. ROW 2: (4) Princess Iron Fan, regal demon princess in crimson and gold armor-robes wielding a giant palm-leaf banana fan, fire and wind swirling; (5) Bull Demon King, colossal bull-headed demon lord with massive horns, black and molten-gold armor, iron cudgel, lava glow; (6) Yama King of hell, towering underworld emperor with dark jade-and-gold imperial robes, mian crown with bead curtains, judgment tablet, hellfire aura. Exactly six characters, one per cell, in this order.

### Monsters (`monster-atlas.png`)

Create ONE game sprite ATLAS, exactly 3 columns by 2 rows, landscape 1536x1024 canvas, six equal 512x512 square cells, no gaps, no grid lines, no borders, no text or labels. TRUE TRANSPARENT background everywhere, no floor, no shadow, no scenery. Each cell contains one full-body monster centered with 12% margin, never crossing into another cell, three-quarter view FACING LEFT. Style: high quality hand-painted pixel art for a Vietnamese xianxia idle RPG, crisp dark outlines, infernal lava and ghost-fire lighting, readable silhouette at 48 pixels. Inspired by underworld creatures of the classic novel Journey to the West, original designs, not copied from any film, TV series or video game. ROW 1: (1) Ox-Head hell guard, muscular ox-headed underworld soldier with iron trident and chains; (2) Horse-Face hell guard, lean horse-faced underworld soldier with halberd and shackles; (3) Black Impermanence, tall pale-faced ghost envoy in black robes and tall black hat, holding shackles and a soul-catching tablet. ROW 2: (4) White Impermanence, tall ghost envoy in white robes and tall white hat with long red tongue, holding a mourning staff; (5) Flaming Mountain imp, small horned fire demon with cracked lava skin and a burning club; (6) White Bone Demon, elegant skeletal sorceress with bone crown and floating bone shards, pale purple ghost fire, elite enemy. Exactly six monsters, one per cell, in this order.
