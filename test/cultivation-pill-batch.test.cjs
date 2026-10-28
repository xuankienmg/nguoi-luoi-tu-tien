const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('wwwroot/js/inventory.js', 'utf8');
const start = source.indexOf("pane.addEventListener('contextmenu', e => {");
const handler = source.slice(start, source.indexOf('\n  // kéo thả', start));

test('Ctrl + right click sends one batch request; regular click still uses one', () => {
  for (const ctrl of [false, true]) {
    const calls = [];
    const cell = { dataset: { k: 'pill' } };
    const context = vm.createContext({
      pane: { addEventListener: (_, fn) => { context.handler = fn; } },
      ITEM: { pill: { useQiSeconds: 1800 } },
      hideTip() {}, act: path => calls.push(path), invDo: action => calls.push(action),
      canUse: () => true,
    });
    vm.runInContext(handler, context);
    context.handler({ ctrlKey: ctrl, preventDefault() {}, target: {
      closest: selector => selector.startsWith('.iv-cell') ? cell : null,
    }});
    assert.deepEqual(calls, ctrl ? ['use/pill?n=0'] : ['use']);
  }
});
