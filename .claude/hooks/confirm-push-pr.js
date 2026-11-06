// PreToolUse(Bash|PowerShell):
//  - Chặn hẳn mọi `git push` lên nhánh master (kể cả push không ghi nhánh khi đang đứng ở master, --all, --mirror).
//  - Các `git push` khác và `gh pr create`: hỏi xác nhận, TRỪ KHI tin nhắn gần nhất của người dùng
//    yêu cầu tạo PR (vd "tạo PR", "mở pull request", /commit-push-pr) thì cho chạy luôn.
const { execFileSync } = require('child_process');
const fs = require('fs');
const PROTECTED = new Set(['master']);
const PR_ASK = /\bPR\b|pull\s*request|commit-push-pr/i;

/** Đọc transcript (JSONL), lấy tin nhắn thật gần nhất của người dùng (bỏ tool_result và tin hệ thống). */
function userAskedForPr(transcriptPath){
  try {
    const lines = fs.readFileSync(transcriptPath, 'utf8').trimEnd().split('\n');
    for (let i = lines.length - 1; i >= 0; i--){
      let e; try { e = JSON.parse(lines[i]); } catch { continue; }
      if (e.type !== 'user' || e.isMeta || e.message?.role !== 'user') continue;
      const c = e.message.content;
      const text = typeof c === 'string' ? c
        : Array.isArray(c) ? c.filter(b => b?.type === 'text').map(b => b.text).join('\n') : '';
      if (Array.isArray(c) && c.some(b => b?.type === 'tool_result')) continue;   // kết quả công cụ, không phải người dùng gõ
      if (!text.trim()) continue;
      return PR_ASK.test(text.replace(/<system-reminder>[\s\S]*?<\/system-reminder>/g, ''));
    }
  } catch { /* không đọc được transcript: hỏi như cũ */ }
  return false;
}

let raw = '';
process.stdin.on('data', d => raw += d);
process.stdin.on('end', () => {
  let input;
  try { input = JSON.parse(raw); } catch { return; }
  const cmd = input.tool_input?.command || '';
  const cwd = input.cwd || process.cwd();

  const out = (decision, reason) => process.stdout.write(JSON.stringify({
    hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: decision, permissionDecisionReason: reason }
  }));

  // Tách từng lệnh con (; && || | xuống dòng) để xét riêng.
  const parts = cmd.split(/&&|\|\||[;|\n]/);
  let pushSeen = false;
  for (const part of parts) {
    const words = part.trim().split(/\s+/).map(w => w.replace(/^['"]|['"]$/g, ''));
    const g = words.indexOf('git');
    if (g < 0) continue;
    // bỏ qua tùy chọn toàn cục của git (-C dir, -c k=v, --no-pager…)
    let i = g + 1, dir = cwd;
    while (i < words.length && words[i].startsWith('-')) {
      if (words[i] === '-C') { dir = require('path').resolve(cwd, words[i + 1] || ''); i += 2; }
      else if (words[i] === '-c') i += 2;
      else i++;
    }
    if (words[i] !== 'push') continue;
    pushSeen = true;
    const args = words.slice(i + 1);
    if (args.some(a => a === '--all' || a === '--mirror' || a === '--branches'))
      return out('deny', 'Không được push toàn bộ nhánh (--all/--mirror) vì sẽ đẩy cả master.');
    const positional = args.filter(a => a && !a.startsWith('-'));
    const refspecs = positional.slice(1);   // positional[0] là remote
    const target = s => {
      const spec = s.replace(/^\+/, '');
      const dst = spec.includes(':') ? spec.split(':').pop() : spec;
      return dst.replace(/^refs\/heads\//, '');
    };
    let blocked = refspecs.map(target).find(b => PROTECTED.has(b));
    if (!blocked && (refspecs.length === 0 || refspecs.some(r => /^\+?HEAD$/.test(r)))) {
      try {
        const cur = execFileSync('git', ['-C', dir, 'symbolic-ref', '--short', 'HEAD'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
        if (PROTECTED.has(cur)) blocked = cur;
      } catch { /* không xác định được nhánh: để bước hỏi xác nhận xử lý */ }
    }
    if (blocked)
      return out('deny', `Không được push trực tiếp lên nhánh ${blocked}. Hãy push nhánh riêng và tạo PR.`);
  }

  const pr = /(^|[\s;&|(])gh\s+pr\s+create\b/.test(cmd);
  if (!pushSeen && !pr) return;
  if (userAskedForPr(input.transcript_path))
    return out('allow', 'Người dùng đã yêu cầu tạo PR trong tin nhắn gần nhất.');
  out('ask', `Cần bạn xác nhận trước khi ${pr ? 'TẠO PULL REQUEST' : 'ĐẨY CODE LÊN REMOTE (git push)'}. Claude không được tự động làm việc này.`);
});
