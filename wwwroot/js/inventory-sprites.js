/* Inventory idle sheets use per-costume timing and synchronized cosmetic layers. */
const INVENTORY_SPRITE_SHEETS = {
  "img/hero_trucco.png": {
    src: "img/skin/inventory-male/bich-van-dao-bao-nam-idle-64-v1.png",
    width: 3072, height: 4096, fps: 16, bodyHeight: 456,
    weaponFx: {kind:'sword',layer:'img/skin/inventory-male/bich-van-dao-bao-nam-sword-fx-64-v1.png'},
    frames: Array.from({length:64},(_,i)=>[i%8*384+8,Math.floor(i/8)*512+16,368,468])
  },
  "img/hero_luyenkhi_nu.png": {
    src: "img/skin/inventory-female/luyen-khi-nu-idle-64-wind-v2.png",
    width: 3072, height: 4096, fps: 16, bodyHeight: 456,
    weaponFx: {kind:'sword',start:[134,191],tip:[61,282]},
    // Fixed crop and scale preserve the wind motion and the boot baseline.
    frames: Array.from({length:64},(_,i)=>[i%8*384+8,Math.floor(i/8)*512+16,368,468])
  },
  "img/hero_trucco_nu.png": {
    "src": "img/skin/inventory-female/truc-co-nu-idle.png",
    "width": 1254,
    "height": 1254,
    "frames": [
      [
        30,
        9,
        252,
        307
      ],
      [
        343,
        9,
        254,
        307
      ],
      [
        657,
        9,
        258,
        307
      ],
      [
        971,
        9,
        258,
        306
      ],
      [
        30,
        320,
        268,
        308
      ],
      [
        343,
        320,
        269,
        308
      ],
      [
        656,
        320,
        271,
        308
      ],
      [
        970,
        320,
        267,
        308
      ],
      [
        30,
        634,
        247,
        308
      ],
      [
        344,
        634,
        247,
        308
      ],
      [
        657,
        634,
        248,
        308
      ],
      [
        970,
        634,
        247,
        308
      ],
      [
        30,
        951,
        250,
        303
      ],
      [
        343,
        951,
        251,
        303
      ],
      [
        657,
        951,
        256,
        303
      ],
      [
        971,
        951,
        250,
        303
      ]
    ]
  },
  "img/hero_kimdan_nu.png": {
    src: "img/skin/inventory-female/kim-dan-nu-idle-64-v1.png",
    width: 3072, height: 4096, fps: 16, bodyHeight: 336,
    weaponFx: {kind:'sword',layer:'img/skin/inventory-female/kim-dan-nu-sword-fx-64-v1.png'},
    // Both layers share one fixed crop, scale, timing and ground anchor.
    frames: Array.from({length:64},(_,i)=>[i%8*384+8,Math.floor(i/8)*512+72,368,344])
  },
  "img/hero_nguyenanh_nu.png": {
    "src": "img/skin/inventory-female/nguyen-anh-nu-idle.png",
    "width": 1254,
    "height": 1254,
    "frames": [
      [
        17,
        8,
        278,
        307
      ],
      [
        330,
        7,
        287,
        309
      ],
      [
        640,
        8,
        295,
        308
      ],
      [
        958,
        7,
        278,
        309
      ],
      [
        19,
        321,
        274,
        308
      ],
      [
        330,
        321,
        296,
        308
      ],
      [
        644,
        321,
        289,
        308
      ],
      [
        956,
        321,
        276,
        308
      ],
      [
        18,
        636,
        286,
        307
      ],
      [
        327,
        636,
        291,
        307
      ],
      [
        638,
        636,
        287,
        307
      ],
      [
        958,
        636,
        279,
        307
      ],
      [
        16,
        951,
        283,
        303
      ],
      [
        329,
        951,
        283,
        303
      ],
      [
        644,
        951,
        280,
        303
      ],
      [
        957,
        951,
        278,
        303
      ]
    ]
  },
  "img/hero_hoathan_nu.png": {
    "src": "img/skin/inventory-female/hoa-than-nu-idle.png",
    "width": 1254,
    "height": 1254,
    "frames": [
      [
        4,
        4,
        305,
        318
      ],
      [
        315,
        4,
        306,
        318
      ],
      [
        629,
        4,
        308,
        318
      ],
      [
        942,
        4,
        306,
        318
      ],
      [
        5,
        322,
        293,
        315
      ],
      [
        317,
        322,
        300,
        315
      ],
      [
        630,
        322,
        306,
        315
      ],
      [
        945,
        322,
        299,
        315
      ],
      [
        4,
        637,
        294,
        313
      ],
      [
        317,
        637,
        294,
        313
      ],
      [
        630,
        637,
        295,
        313
      ],
      [
        944,
        637,
        294,
        313
      ],
      [
        4,
        950,
        293,
        304
      ],
      [
        317,
        950,
        294,
        304
      ],
      [
        631,
        950,
        293,
        304
      ],
      [
        945,
        950,
        294,
        304
      ]
    ]
  },
  "img/hero_luyenhu_nu.png": {
    "src": "img/skin/inventory-female/luyen-hu-nu-idle.png",
    "width": 1254,
    "height": 1254,
    "frames": [
      [
        19,
        15,
        295,
        325
      ],
      [
        314,
        15,
        294,
        325
      ],
      [
        627,
        15,
        290,
        325
      ],
      [
        941,
        15,
        289,
        324
      ],
      [
        20,
        342,
        294,
        317
      ],
      [
        314,
        342,
        302,
        317
      ],
      [
        627,
        342,
        297,
        317
      ],
      [
        941,
        342,
        290,
        317
      ],
      [
        20,
        666,
        293,
        307
      ],
      [
        314,
        666,
        291,
        307
      ],
      [
        627,
        666,
        290,
        307
      ],
      [
        941,
        666,
        290,
        307
      ],
      [
        28,
        976,
        282,
        276
      ],
      [
        321,
        976,
        279,
        276
      ],
      [
        631,
        976,
        281,
        276
      ],
      [
        947,
        976,
        280,
        276
      ]
    ]
  },
  "img/hero_hopthe_nu.png": {
    "src": "img/skin/inventory-female/hop-the-nu-idle.png",
    "width": 1254,
    "height": 1254,
    "frames": [
      [
        21,
        2,
        273,
        314
      ],
      [
        334,
        2,
        276,
        314
      ],
      [
        647,
        2,
        274,
        314
      ],
      [
        961,
        2,
        273,
        314
      ],
      [
        21,
        319,
        272,
        310
      ],
      [
        334,
        319,
        276,
        310
      ],
      [
        647,
        319,
        272,
        310
      ],
      [
        961,
        319,
        270,
        310
      ],
      [
        21,
        636,
        276,
        307
      ],
      [
        334,
        636,
        276,
        307
      ],
      [
        647,
        636,
        275,
        307
      ],
      [
        961,
        636,
        272,
        307
      ],
      [
        21,
        951,
        272,
        303
      ],
      [
        334,
        951,
        272,
        303
      ],
      [
        648,
        951,
        274,
        303
      ],
      [
        961,
        951,
        272,
        303
      ]
    ]
  }
};

/** Draw only inventory poses. Return false while loading so the existing
 * static/legacy avatar remains visible. Coordinates are logical 320x320. */
function drawInventorySprite(g, hero, time, reducedMotion, layerSrc){
  const sheet = INVENTORY_SPRITE_SHEETS[hero];
  if (!sheet) return false;
  const image = skImg(layerSrc || sheet.src);
  if (!image || image.naturalWidth !== sheet.width || image.naturalHeight !== sheet.height) return false;
  const seconds = Number.isFinite(time) ? Math.max(0,time) : 0;
  const index = reducedMotion ? 0 : Math.floor(seconds * (sheet.fps || 4)) % sheet.frames.length;
  const [sx,sy,sw,sh] = sheet.frames[index];
  // Normalize visible art rather than cell gutters. This keeps the boots on
  // the same baseline despite uneven padding and scale in the authored sheet.
  const scale = Math.min(278/(sheet.bodyHeight || sh),296/sw);
  const width = sw*scale, height = sh*scale;
  g.save();
  try {
    g.imageSmoothingEnabled = true;
    g.imageSmoothingQuality = 'high';
    g.drawImage(image,sx,sy,sw,sh,160-width/2,306-height,width,height);
  } finally { g.restore(); }
  return true;
}
