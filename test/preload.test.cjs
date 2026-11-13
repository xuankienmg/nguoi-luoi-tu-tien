const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot', 'js', 'preload.js'), 'utf8');

/** Môi trường giả: DOM tối thiểu + bộ hẹn giờ do test điều khiển (không chờ thật). */
function load(search = '') {
  const timers = [];
  const fake = () => ({ style: {}, textContent: '', classList: { added: [], add(c) { this.added.push(c); } } });
  const bar = fake(), msg = fake(), pct = fake();
  const box = { removed: false, classList: { added: [], add(c) { this.added.push(c); } }, remove() { this.removed = true; },
    querySelector: s => ({ '.pl-bar i': bar, '.pl-msg': msg, '.pl-pct': pct })[s] };
  const loaded = [];
  const context = {
    document: { getElementById: id => (id === 'preload' && !search.includes('none') ? box : null) },
    location: { search },
    Image: class { set src(v) { loaded.push(v); setImmediate(() => this.onload?.()); } },
    setTimeout: (fn, ms) => { const t = { fn, ms, off: false }; timers.push(t); return t; },
    setInterval: () => ({}), clearInterval: () => {}, Date,
  };
  context.window = context;
  vm.runInContext(source + '\nthis.Preload = Preload;', vm.createContext(context));
  return { Preload: context.Preload, box, bar, msg, pct, timers, loaded };
}

test('step chỉ tiến, không lùi, và ghi nhãn', () => {
  const { Preload, bar, msg, pct } = load();
  Preload.step('Đang tải dữ liệu game…', 35);
  assert.equal(bar.style.width, '35%');
  assert.equal(msg.textContent, 'Đang tải dữ liệu game…');
  Preload.step('', 20);
  assert.equal(bar.style.width, '35%');
  Preload.step('', 500);
  assert.equal(pct.textContent, '99%');                     // chỉ done() mới tới 100%
});

test('done tắt màn preload đúng một lần và bỏ phần tử', () => {
  const { Preload, box, bar, timers } = load();
  Preload.done(); Preload.done(); Preload.hide();
  assert.equal(bar.style.width, '100%');
  for (let i = 0; i < timers.length; i++) if (timers[i].ms < 45000 && !timers[i].off) { timers[i].off = true; timers[i].fn(); }   // chờ tối thiểu rồi hiệu ứng mờ (timer mới thêm cũng chạy)
  assert.deepEqual(box.classList.added, ['pl-done']);
  assert.ok(box.removed);
});

test('có chốt chặn 45 giây để không che màn hình mãi', () => {
  const { timers } = load();
  assert.ok(timers.some(t => t.ms === 45000));
});

test('dải taskbar (mode=strip) không có màn preload', () => {
  const { Preload, box } = load('?mode=strip');
  assert.ok(box.removed);
  Preload.step('x', 50); Preload.done();                      // không lỗi
});

test('images chờ hết hoặc quá hạn, bỏ qua giá trị rỗng và trùng', async () => {
  const { Preload, loaded } = load();
  await Preload.images(['a.png', null, 'a.png', false, 'b.png']);
  assert.deepEqual(loaded, ['a.png', 'b.png']);
});
