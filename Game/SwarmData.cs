namespace VanDao.Game;

/// <summary>Yêu Triều: thưởng Linh Thú Đan khi kết thúc ván.</summary>
public static class SwarmData
{
    public const string Reward = "linh_thu_dan";
    public const int DailyEntries = 3;
    public const int DailyPills = 5_000;
    public static int PillsForEntry(int entry) =>
        DailyPills / DailyEntries + (entry <= DailyPills % DailyEntries ? 1 : 0);
    public const int MaxRunSeconds = 600;   // mỗi ván tối đa 10 phút
    public const int MaxKillsPerSecond = 20;
    public const int WaveSeconds = 30;
    /// <summary>Đợt sớm nhất mỗi loại quái xuất hiện. Khóa khớp id trong swarm-core.js; "boss" gộp mọi Boss.</summary>
    public static readonly IReadOnlyDictionary<string, int> Monsters = new Dictionary<string, int>
    {
        ["normal"] = 1, ["swift"] = 2, ["armored"] = 3,
        ["archer"] = 4, ["charger"] = 5, ["bomber"] = 6, ["boss"] = 3,
        ["tree"] = 4, ["bat"] = 5,   // quái đợt sự kiện: Thụ yêu, Dơi yêu
    };

    /// <summary>Tổng số Boss tối đa đã xuất hiện tới đợt này (khớp isBossWave / bossCountForWave trong swarm-core.js).</summary>
    public static int MaxBosses(int wave)
    {
        var total = 0;
        for (var w = 3; w <= wave; w++)
            if (w == 3 || (w >= 6 && w % 2 == 0)) total += w >= 18 ? 3 : w >= 14 ? 2 : 1;
        return total;
    }

    /// <summary>Kiểm tra số liệu ván (vượt thời gian sống / đợt chưa tới).</summary>
    public static void ValidateRun(IReadOnlyDictionary<string, int> kills, int seconds)
    {
        seconds = Math.Clamp(seconds, 0, MaxRunSeconds);
        var wave = 1 + seconds / WaveSeconds;
        long total = 0;
        foreach (var (id, n) in kills)
        {
            if (n < 0 || !Monsters.TryGetValue(id, out var minWave)) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            if (n == 0) continue;
            if (wave < minWave) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            if (id == "boss" && n > MaxBosses(wave)) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
            total += n;
        }
        if (total > (long)seconds * MaxKillsPerSecond + 50) throw new GameException("Dữ liệu Yêu Triều không hợp lệ.");
    }
}
