const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const skills = [
  'thuy_nguyet', 'van_thuy_tien', 'thuy_lao', 'thuong_hai', 'thien_ha',
  'hu_hai', 'hon_nguyen_thuy', 'thai_so_thuy', 'thuy_do_kiep_active', 'thuy_chan_tien_active',
];

test('all ten Water casts keep their atlas and impact timing across realms', () => {
  const scope = vm.createContext({});
  for (const file of ['water-wood-fx-data', 'fire-fx'])
    vm.runInContext(fs.readFileSync(`wwwroot/js/${file}.js`, 'utf8'), scope);

  for (const [realm, id] of skills.entries()) {
    const spec = scope.fireFxSpec(id, realm);
    assert.equal(spec.element, 'thuy', id);
    assert.equal(scope.fireFxSpec(id, 9).src, spec.src, id);
    assert(scope.fireFxOwnsStage(id, realm));
    const folder = {hu_hai:'cuu-u-thuy-xa-v6',thuong_hai:'tham-hai-tuyen-qua-v1',thien_ha:'van-thuy-thien-tien-v1'}[id] ?? id;
    const metadata = JSON.parse(fs.readFileSync(`wwwroot/img/fx/water/v1/${folder}/sheet.json`, 'utf8'));
    assert.equal(metadata.skillId, id);
    assert.equal(spec.src, `img/fx/water/v1/${folder}/${metadata.sheet}`);
    for (const key of ['frameWidth', 'frameHeight', 'frameCount', 'columns', 'fps', 'durationMs', 'span', 'fitTop'])
      assert.equal(spec[key], metadata[key], `${id}: ${key}`);
    assert.equal(spec.authoredImpactMs, metadata.impactMs);
    assert.equal(spec.anchorX, metadata.anchor[0]);
    assert.equal(spec.anchorY, metadata.anchor[1]);
    assert.equal(spec.stageAnchor, metadata.anchorKind);
    assert(metadata.decodedRgbaBytes <= 16 * 1024 * 1024);
    if (id === 'hu_hai') {
      assert.match(metadata.sourceDemo, /cuu-u-thuy-xa-demo-v6$/);
      assert.equal(scope.fireFxPlan(id, .45, realm).frame, 43);
      assert.equal(metadata.domeImpactFrame, 68);
      assert.equal(spec.reducedFrame, metadata.reducedFrame);
    }
    if (id === 'thien_ha') {
      assert.match(metadata.sourceDemo, /van-thuy-thien-tien-demo-v1$/);
      assert.equal(scope.fireFxPlan(id, .45, realm).frame, 46);
      assert.equal(spec.reducedFrame, 42);
    }
    if (id === 'thuong_hai') {
      assert.match(metadata.sourceDemo, /tham-hai-tuyen-qua-demo-v1$/);
      assert.equal(scope.fireFxPlan(id, .45, realm).frame, 51);
      assert.equal(spec.reducedFrame, metadata.reducedFrame);
      const pullStart = .45 * metadata.pullStartMs / metadata.impactMs;
      assert.equal(scope.waterVortexPull(pullStart,48), null);
      const motion = scope.waterVortexPull((pullStart+.45)/2,48);
      assert(Math.hypot(motion.x,motion.y)>1);
      assert.equal(scope.waterVortexPull(.45,48), null);
    }

    const battle = {casts:[{turn:0,skill:id,dmg:0,ultimate:true}],extra:[true]};
    const timing = scope.battleSkillTiming(battle, 0);
    const impact = metadata.impactMs / metadata.durationMs * 2;
    assert.equal(timing.authoredDuration, 2);
    assert(Math.abs(timing.progress(impact).s - .45) < 1e-8);
    for (let frame = 0; frame < spec.frameCount; frame++) {
      const phase = timing.progress(2 * (frame + .5) / spec.frameCount);
      assert.equal(scope.fireFxPlan(id, phase.s, realm).frame, frame);
      assert(!scope.battleMonsterActionReady(battle, {name:'fight',i:0,...phase}));
    }
    assert.equal(scope.fireFxPlan(id, .75, realm), null);
    assert(timing.progress(2.01).rep);
    assert(scope.battleMonsterActionReady(battle, {name:'fight',i:0,...timing.progress(2.39)}));
    assert.equal(scope.battleSkillTiming({...battle,pvp:{idle:[true]}}, 0).authoredDuration, 0);
  }

  assert(!scope.fireFxOwnsStage('thuy_cau', 9));
  assert(!scope.battleCastIsUltimate({skill:'thuy_cau'}));
});
