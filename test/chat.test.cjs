const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync('wwwroot/js/chat.js', 'utf8');
const pure = source.slice(source.indexOf('/* chat-pure:start */'), source.indexOf('/* chat-pure:end */'));
const ctx = vm.createContext({});
vm.runInContext(pure + ';globalThis.F={chatMerge,chatBadge,chatUnread,chatPollDelay,CHAT_KEEP}', ctx);
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
