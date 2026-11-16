# Xích Diễm Thiên integration

Approved scope: replace Xích Diễm Thiên with the accepted fireball → Sprite Fusion Explosion and extend combat so it can finish. Keep damage and other skill effects.

- [x] Package the existing source into a GPU-sized 4096×3840 atlas, 227 frames at 30 FPS, retaining alpha and MIT attribution.
- [x] Add a dedicated spec to the shared preview/combat renderer and preserve the .45 logical impact marker.
- [x] Extend server BattleTiming and client exploration/arena schedules only on turns using the replacement. Insert companions and combo after the complete main animation.
- [x] Verify all 227 frames through the real browser canvas, source syntax, related client regressions and server timing tests.

Rulings: use supplied existing art rather than image generation. Keep exploration speed modifiers, which still scale the complete trip as before. The new default turn takes 227/30+.25 seconds; the first 227/30 seconds cover the full skill. Rendering intentionally permits remote sparks to leave the viewport rather than shrinking the core to fit the entire particle envelope.

Validation: headless Edge accepted the atlas and sampled all 227 frames; runtime-review.png visually inspected. .NET timing/regression selection: 20 passed. Existing build warnings: WindowsBase version conflict and nullable references outside this change. Testhost required unsandboxed execution; isolated artifacts avoid the running game executable lock.
