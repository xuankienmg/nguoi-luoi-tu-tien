namespace VanDao.Game;

/// <summary>Yêu Triều (mini game client): thưởng Linh Thú Đan theo số quái hạ gục; vào tối đa 2 lần mỗi ngày.</summary>
public static class SwarmData
{
    public const string Reward = "linh_thu_dan";
    public const int DailyEntries = 5;   // mỗi ngày vào Yêu Triều tối đa 2 lần, mỗi lần nhận thưởng một lần
    public const int PointsPerPill = 10;     // cân bằng 11/2026: 10 điểm quái = 1 Linh Thú Đan
    public const int MaxRunSeconds = 600;   // mỗi ván tối đa 10 phút
    public const int MaxKillsPerSecond = 20;
    public const int WaveSeconds = 30;
    /// <summary>Điểm mỗi quái hạ gục (quái mạnh hơn → nhiều hơn) và đợt sớm nhất xuất hiện. Khóa khớp id trong swarm-core.js; "boss" gộp mọi Boss.</summary>
    public static readonly IReadOnlyDictionary<string, (int Points, int MinWave)> Monsters = new Dictionary<string, (int, int)>
    {
        ["normal"] = (1, 1), ["swift"] = (1, 2), ["armored"] = (3, 3),
        ["archer"] = (2, 4), ["charger"] = (2, 5), ["bomber"] = (2, 6), ["boss"] = (25, 3),
        ["tree"] = (3, 4), ["bat"] = (1, 5),   // quái đợt sự kiện: Thụ yêu, Dơi yêu
    };

    /// <summary>Tổng số Boss tối đa đã xuất hiện tới đợt này (khớp isBossWave / bossCountForWave trong swarm-core.js).</summary>
    public static int MaxBosses(int wave)
    {
        var total = 0;
        for (var w = 3; w <= wave; w++)
            if (w <= 12 ? w % 3 == 0 : w % 2 == 0) total += w >= 18 ? 3 : w >= 14 ? 2 : 1;
        return total;
    }

    /// <summary>Số Linh Thú Đan tối đa cho kết quả này; ném lỗi nếu số liệu vô lý (vượt thời gian sống / đợt chưa tới).</summary>
    public static int Pills(IReadOnlyDictionary<string, int> kills, int seconds)
    {
        seconds = Math.Clamp(seconds, 0, MaxRunSeconds);
        var wave = 1 + seconds / WaveSeconds;
        long total = 0, points = 0;
        foreach (var (id, n) in kills)
        {
            if (n < 0 || !Monsters.TryGetValue(id, out var m)) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            if (n == 0) continue;
            if (wave < m.MinWave) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            if (id == "boss" && n > MaxBosses(wave)) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            total += n; points += (long)n * m.Points;
        }
        if (total > (long)seconds * MaxKillsPerSecond + 50) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
        return (int)(points / PointsPerPill);   // không giới hạn số đan mỗi lần (chỉ giới hạn 2 lần/ngày)
    }
}
