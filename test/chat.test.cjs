const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/chat.js', 'utf8');
const pure = source.slice(source.indexOf('/* chat-pure:start */'), source.indexOf('/* chat-pure:end */'));
const ctx = vm.createContext({});
vm.runInContext(pure + ';globalThis.F={chatMerge,chatFresh,chatBadge,chatUnread,chatPollDelay,CHAT_KEEP,CHAT_OVERLAP}', ctx);
const F = ctx.F;
const msg = (id, extra = {}) => ({id, kind: 'player', mine: false, text: 't' + id, ...extra});

test('merge appends only newer ids and keeps the last CHAT_KEEP', () => {
  let list = F.chatMerge([], [msg(1), msg(2)]);
  list = F.chatMerge(list, [msg(2), msg(3)]);
  assert.deepEqual(list.map(m => m.id), [1, 2, 3]);
  const many = Array.from({length: F.CHAT_KEEP + 20}, (_, i) => msg(i + 10));
  const kept = F.chatMerge(list, many);
  assert.equal(kept.length, F.CHAT_KEEP);
  assert.equal(kept[kept.length - 1].id, F.CHAT_KEEP + 29);
});

test('unread skips system and own messages', () => {
  assert.equal(F.chatUnread([msg(1), msg(2, {kind: 'system'}), msg(3, {mine: true}), msg(4)]), 2);
});

test('badge text is empty, digit or 9+', () => {
  assert.equal(F.chatBadge(0), '');
  assert.equal(F.chatBadge(7), '7');
  assert.equal(F.chatBadge(12), '9+');
});

test('poll delay depends on visibility, panel and failures', () => {
  assert.equal(F.chatPollDelay({hidden: true, watching: true, fails: 0}), null);
  assert.equal(F.chatPollDelay({hidden: false, watching: true, fails: 0}), 4000);
  assert.equal(F.chatPollDelay({hidden: false, watching: false, fails: 0}), 20000);
  assert.equal(F.chatPollDelay({hidden: false, watching: true, fails: 1}), 8000);
  assert.equal(F.chatPollDelay({hidden: false, watching: true, fails: 5}), 16000);
});

test('merge inserts a late lower id in order and ignores duplicates', () => {
  let list = F.chatMerge([], [msg(99), msg(101)]);
  list = F.chatMerge(list, [msg(100), msg(101), msg(99)]);
  assert.deepEqual(list.map(m => m.id), [99, 100, 101]);
});

test('merge still keeps only the last CHAT_KEEP after late inserts', () => {
  const base = Array.from({length: F.CHAT_KEEP}, (_, i) => msg(i * 2 + 10));
  const out = F.chatMerge(base, [msg(11), msg(500)]);
  assert.equal(out.length, F.CHAT_KEEP);
  assert.equal(out[out.length - 1].id, 500);
  assert.deepEqual(out.map(m => m.id), [...out.map(m => m.id)].sort((a, b) => a - b));
});

test('fresh returns only unseen ids sorted by id', () => {
  const list = [msg(1), msg(3)];
  assert.deepEqual(F.chatFresh(list, [msg(4), msg(3), msg(2), msg(1)]).map(m => m.id), [2, 4]);
  assert.deepEqual(F.chatFresh(list, [msg(1), msg(3)]), []);
});

test('overlap is 20 ids', () => { assert.equal(F.CHAT_OVERLAP, 20); });
