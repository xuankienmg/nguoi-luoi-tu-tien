using System.Collections.Concurrent;

namespace VanDao.Game;

/// <summary>
/// Nhật ký hiển thị của từng nhân vật (chế độ Server, đăng ký singleton). Mỗi lần đồng bộ chỉ hỏi database id dòng mới nhất;
/// chưa có dòng mới thì dùng lại danh sách cũ thay vì đọc lại 40 dòng chữ (mỗi lần đọc database đều tính vào Egress của Supabase).
/// </summary>
public sealed class ServerLogCache
{
    private readonly ConcurrentDictionary<Guid, (long Top, LogDto[] Logs)> _byPlayer = new();

    public bool TryGet(Guid player, long top, out LogDto[] logs)
    {
        if (_byPlayer.TryGetValue(player, out var hit) && hit.Top == top) { logs = hit.Logs; return true; }
        logs = [];
        return false;
    }

    public void Set(Guid player, long top, IEnumerable<LogDto> logs) => _byPlayer[player] = (top, logs.ToArray());
}
