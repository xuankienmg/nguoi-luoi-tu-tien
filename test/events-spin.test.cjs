const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
test('event wheel becomes visible and refreshes when reopened while already mounted', () => {
  const nav = {dataset: {}, innerHTML: ''};
  const home = {};
  const spin = {parentElement: home, hidden: true};
  const body = {dataset: {}, classList: {toggle() {}}, replaceChildren(node) { node.parentElement = this; }};
  const pane = {querySelector(sel) { return sel === '.ev-list' ? nav : sel === '.ev-body' ? body : {}; }, addEventListener() {}};
  let renders = 0;
  const context = vm.createContext({
    $: id => id === 'pane-spin' ? spin : id === 'pane-event' ? pane : null,
    S: {inventory: {}}, PANE_RENDER: {}, paneDirty: new Set(), window: {},
    setInterval() {}, fmt: String, renderSpin() { renders++; }
  });
  vm.runInContext(fs.readFileSync('wwwroot/js/events.js', 'utf8'), context);
  context.window.selectEvent('spin');
  context.PANE_RENDER.event();
  assert.equal(spin.parentElement, body);
  assert.equal(spin.hidden, false);
  spin.hidden = true; // openTab hides every pane except pane-event
  context.PANE_RENDER.event();
  assert.equal(spin.hidden, false);
  assert.equal(renders, 2);
});
test('daily login claim has exactly one server endpoint', () => {
  const source = fs.readFileSync('Program.cs', 'utf8');
  assert.equal((source.match(/api\.MapPost\("\/players\/\{id:guid\}\/login\/claim"/g) || []).length, 1);
});