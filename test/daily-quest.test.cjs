const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');

function load() {
  const context = vm.createContext({window: {}});
  vm.runInContext(fs.readFileSync('wwwroot/js/daily-quest.js', 'utf8'), context);
  return context.window;
}
const helpers = (over = {}) => ({cell: r => `<cell ${r.item}×${r.qty}>`, fmt: String, clock: () => '01:00:00',
  warn: (online, blocked) => !online ? '<p class="ev-warn">offline</p>' : blocked ? `<p class="ev-warn">${blocked}</p>` : '', busy: false, ...over});
const chest = (points, claimed = false) => ({points, claimed, rewards: [{item: 'linh_thach', qty: 10}]});
const quest = (over = {}) => ({online: true, points: 45, maxPoints: 115, weekPoints: 160, blocked: null, resetAt: 0, weekResetAt: 0,
  tasks: [{id: 'kill', title: 'Hạ quái khi thám hiểm', tab: 'map', progress: 20, need: 50, points: 15},
          {id: 'boss', title: 'Hạ Boss chương', tab: 'map', progress: 1, need: 1, points: 15}],
  chests: [chest(20, true), chest(40), chest(60), chest(80), chest(100)],
  weekChests: [chest(150), chest(300), chest(450), chest(600)], ...over});

test('locked state shows unlock hint', () => {
  const html = load().dailyQuestHtml(null, helpers());
  assert.match(html, /Hoàn thành nhiệm vụ tân thủ/);
});

test('tasks show progress, go button only when unfinished', () => {
  const html = load().dailyQuestHtml(quest(), helpers());
  assert.match(html, /20\/50/);
  assert.match(html, /data-dq-go="map"/);
  assert.equal((html.match(/data-dq-go=/g) || []).length, 1);   // việc Boss đã xong: không có nút Đi tới
  assert.match(html, /Xong/);
});

test('claim buttons only for reached, unclaimed chests', () => {
  const html = load().dailyQuestHtml(quest(), helpers());
  assert.doesNotMatch(html, /data-dq-claim="0"/);   // đã nhận
  assert.match(html, /data-dq-claim="1"/);          // 45 ≥ 40
  assert.doesNotMatch(html, /data-dq-claim="2"/);   // 45 < 60
  assert.match(html, /data-wq-claim="0"/);          // tuần 160 ≥ 150
  assert.doesNotMatch(html, /data-wq-claim="1"/);
  assert.match(html, /còn 15/);
});

test('ready count and disabled when offline or blocked', () => {
  const w = load();
  assert.equal(w.dailyQuestReady(quest()), 2);
  assert.equal(w.dailyQuestReady(quest({online: false})), 0);
  assert.equal(w.dailyQuestReady(quest({blocked: 'x'})), 0);
  assert.equal(w.dailyQuestReady(null), 0);
  const html = w.dailyQuestHtml(quest({online: false}), helpers());
  assert.match(html, /offline/);
  assert.match(html, /data-dq-claim="1" disabled/);
});
