# Xích Diễm Thiên v2

227 frames, 30 FPS, 7566.667 ms. Atlas: 16 columns × 15 rows, cells 256×256; 13 trailing cells unused. Fixed impact anchor [128,151]. Frames 0–17 charge, 18–41 flight, 42–226 Explosion. Impact at 1400 ms.

Art source: existing fireball (`img/fx_hoa_cau_anim.png`) and Sprite Fusion MCP Explosion source, MIT. No new image model or paid generation. Settings: fire=1, earth=.15, chunks=3, seed=17. Alpha glow approximates the original additive light buffer. Original full-resolution frames and reproducible export scripts: output/fireball-explosion-v1/.

The runtime uses a wider draw envelope for Explosion so the core stays visible; distant sparks can leave the viewport. Only xich_diem_thien is overridden. Combat keeps logical damage at progress .45, maps 1.4 seconds of charge/flight before it and 185/30 seconds of explosion after it, then plays companion/combo/counter phases.
