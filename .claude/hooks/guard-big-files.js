// PreToolUse(Read): chặn đọc cả file GameService.cs / GameData.cs (mỗi file ~160-185 KB, tốn context).
// Cho phép khi có `limit` (đọc theo đoạn: Grep tìm dòng, rồi Read với offset/limit) và limit <= MAX_LIMIT.
const path = require('path');
const BIG = new Set(['gameservice.cs', 'gamedata.cs']);
const MAX_LIMIT = 400;

let raw = '';
process.stdin.on('data', d => raw += d);
process.stdin.on('end', () => {
  let input;
  try { input = JSON.parse(raw); } catch { return; }
  const file = input.tool_input?.file_path;
  if (typeof file !== 'string' || !BIG.has(path.basename(file).toLowerCase())) return;
  const limit = Number(input.tool_input?.limit);
  if (Number.isFinite(limit) && limit > 0 && limit <= MAX_LIMIT) return;
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason:
        `Không đọc cả ${path.basename(file)} (file rất lớn). Dùng Grep tìm đúng dòng, rồi Read với offset và limit <= ${MAX_LIMIT} (theo CLAUDE.md).`
    }
  }));
});
