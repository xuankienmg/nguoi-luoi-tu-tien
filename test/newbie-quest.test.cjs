const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');

function load() {
  const context = vm.createContext({window: {}, document: {}, S: null, MODE: 'full', ITEM: {linh_thach: {name: 'Linh thạch'}}, fmt: String});
  vm.runInContext(fs.readFileSync('wwwroot/js/newbie-quest.js', 'utf8'), context);
  return context;
}
const quest = (over = {}) => ({step: 2, total: 14, id: 'equip', title: 'Khoác giáp cầm binh', hint: 'Mở Hành trang', tab: 'inv',
  progress: 0, need: 1, canClaim: false, rewards: [{item: 'linh_thach', qty: 500}], ...over});

test('card shows step, go button and no claim while not done', () => {
  const html = load().window.newbieQuestHtml(quest(), 'full');
  assert.match(html, /3\/14/);
  assert.match(html, /data-nq="go"/);
  assert.doesNotMatch(html, /data-nq="claim"/);
  assert.match(html, /500 <span>Linh thạch<\/span>/);
});

test('card shows claim instead of go when done', () => {
  const html = load().window.newbieQuestHtml(quest({progress: 1, canClaim: true}), 'full');
  assert.match(html, /data-nq="claim"/);
  assert.doesNotMatch(html, /data-nq="go"/);
});

test('no go button when quest has no tab; strip mode is one line', () => {
  const ctx = load();
  assert.doesNotMatch(ctx.window.newbieQuestHtml(quest({tab: null}), 'full'), /data-nq="go"/);
  assert.match(ctx.window.newbieQuestHtml(quest(), 'strip'), /data-nq="open"/);
});

test('titles are escaped', () => {
  assert.doesNotMatch(load().window.newbieQuestHtml(quest({title: '<b>x</b>'}), 'full'), /<b>x<\/b>/);
});

test('card is hidden when chain done or starter dialog open', () => {
  const ctx = load();
  assert.equal(ctx.window.newbieQuestVisible(null), false);
  assert.equal(ctx.window.newbieQuestVisible({newbieQuest: quest(), starterChoice: {required: true}}), false);
  assert.equal(ctx.window.newbieQuestVisible({newbieQuest: quest(), starterChoice: {required: false}}), true);
});

test('wired into page, render loop and server', () => {
  assert.match(fs.readFileSync('wwwroot/index.html', 'utf8'), /js\/newbie-quest\.js/);
  assert.match(fs.readFileSync('wwwroot/index.html', 'utf8'), /css\/newbie-quest\.css/);
  assert.match(fs.readFileSync('wwwroot/js/core.js', 'utf8'), /renderNewbieQuest\(\)/);
  assert.match(fs.readFileSync('Program.cs', 'utf8'), /\/newbie-quest\/claim/);
});
