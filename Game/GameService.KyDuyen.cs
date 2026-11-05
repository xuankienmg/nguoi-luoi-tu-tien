using VanDao.Data;
namespace VanDao.Game;

/// <summary>Kỳ duyên Thiên Thư: xử lý lựa chọn ở khung giờ đang mở, trả state cho client.</summary>
public partial class GameService
{
    /// <summary>Khung đang mở; admin bật cờ "kyduyen" thì luôn có khung 10 phút đang chạy (để thử ngoài giờ).</summary>
    private static KyDuyen.Slot? KyDuyenSlot(Player p, DateTimeOffset now) =>
        KyDuyen.At(now) ?? (p.Cheat("kyduyen") ? AdminSlot(p, now.ToOffset(KyDuyen.Vn)) : null);
    /// <summary>Cờ admin "kdat=&lt;phút Unix&gt;": lệnh Kích hoạt mở khung đủ 10 phút tính từ lúc bấm.</summary>
    private const string KyDuyenAtFlag = "kdat=";
    /// <summary>Khung admin: khung vừa kích hoạt nếu còn hạn, không thì khối 10 phút đang chạy.</summary>
    private static KyDuyen.Slot AdminSlot(Player p, DateTimeOffset vn)
    {
        var mins = p.Admin.Split(',').Where(f => f.StartsWith(KyDuyenAtFlag)).Select(f => long.TryParse(f[KyDuyenAtFlag.Length..], out var m) ? m : 0).FirstOrDefault();
        var start = DateTimeOffset.FromUnixTimeSeconds(mins * 60).ToOffset(KyDuyen.Vn);
        if (mins <= 0 || vn < start || vn >= start + KyDuyen.Window)
            start = new DateTimeOffset(vn.Year, vn.Month, vn.Day, vn.Hour, vn.Minute / (int)KyDuyen.Window.TotalMinutes * (int)KyDuyen.Window.TotalMinutes, 0, KyDuyen.Vn);
        return KyDuyen.Make(start.Date, start.Hour, start.Minute);
    }

    /// <summary>Cờ admin "kdpick=&lt;id&gt;": lệnh Kích hoạt kỳ duyên chọn sẵn kỳ duyên muốn gặp.</summary>
    private const string KyDuyenPickFlag = "kdpick=";
    /// <summary>Kỳ duyên của khung: theo hạt giống nhân vật + khung, trừ khi admin đã chọn sẵn.</summary>
    private static KyDuyen.Encounter KyDuyenPick(Player p, KyDuyen.Slot slot) =>
        (Player.AdminEnabled ? p.Admin.Split(',').Where(f => f.StartsWith(KyDuyenPickFlag)).Select(f => KyDuyen.Encounters.FirstOrDefault(x => x.Id == f[KyDuyenPickFlag.Length..])).FirstOrDefault(x => x is not null) : null)
        ?? KyDuyen.Pick(p.Id, slot);

    /// <summary>Lời đáp thứ ba của mọi kỳ duyên (choice 2): bỏ đi, không được không mất, khép kỳ duyên khung này.</summary>
    public const int KyDuyenSkip = 2;
    private const string KyDuyenSkipLabel = "Bỏ đi";

    /// <summary>Câu hỏi đang chờ trả lời ở khung này: bước 1 của kỳ duyên, hoặc bước 2 khi KyDuyenKey = "khung#lối bước 1".
    /// First = lối đã chọn ở bước 1 (-1 = đang ở bước 1).</summary>
    private static (string Text, KyDuyen.Choice A, KyDuyen.Choice B, int First) KyDuyenNode(Player p, KyDuyen.Slot slot, KyDuyen.Encounter enc)
    {
        var prefix = slot.Key + "#";
        if (p.KyDuyenKey.StartsWith(prefix) && int.TryParse(p.KyDuyenKey[prefix.Length..], out var first)
            && (first == 0 ? enc.A : first == 1 ? enc.B : null)?.Next is { } step)
            return (step.Text, step.A, step.B, first);
        return (enc.Text, enc.A, enc.B, -1);
    }

    /// <summary>Quái ẩn / Boss của kỳ duyên với chỉ số Boss lang thang theo cảnh giới + tầng người chơi.</summary>
    private static MonsterDef KyDuyenFoe(Player p, KyDuyen.Foe f) =>
        WanderingBoss.Monster(new WanderingBoss.Def(f.Id, f.Name, f.Image, "", 0, 0, ""), p.Realm, p.Stage) with { Tier = f.Boss ? "boss" : "elite" };

    /// <summary>Xử lý kỳ duyên khung đang mở: choice 0 = lối thứ nhất, 1 = lối thứ hai, 2 = bỏ đi. Mỗi khung một lần, phải online.</summary>
    public (string, string) ResolveKyDuyen(Player p, int choice) => ResolveKyDuyenAt(p, choice, EventNow);

    public (string, string) ResolveKyDuyenAt(Player p, int choice, DateTimeOffset? at)
    {
        if (at is not { } now) throw new GameException("Cần kết nối mạng để gặp kỳ duyên.");
        if (EventBlocked(p) is { } why) throw new GameException(why);
        if (!p.StarterChosen) throw new GameException("Chọn khởi đầu trước đã.");
        if (KyDuyenSlot(p, now) is not { } slot) throw new GameException($"Hiện không có kỳ duyên. Khung kế tiếp lúc {KyDuyen.Next(now).Start.ToOffset(KyDuyen.Vn):HH:mm}.");
        if (p.KyDuyenKey == slot.Key) throw new GameException("Đã xử lý kỳ duyên khung giờ này.");
        if (choice is not (0 or 1 or KyDuyenSkip)) throw new GameException("Lựa chọn không hợp lệ.");
        var enc = KyDuyenPick(p, slot);
        if (choice == KyDuyenSkip)
        {
            p.KyDuyenKey = slot.Key;
            return Log(p, $"Kỳ duyên · {enc.Title}: Ngươi lặng lẽ bỏ đi, không dây dưa nhân quả.", "");
        }
        var node = KyDuyenNode(p, slot, enc);
        var c = choice == 0 ? node.A : node.B;
        if (c.Next is not null && node.First < 0)
        {
            p.KyDuyenKey = $"{slot.Key}#{choice}";   // sang câu hỏi thứ hai, cùng khung 10 phút
            return Log(p, $"{enc.Title}: {c.Label}.", "");
        }
        var unit = KyDuyen.StoneUnit(p.Realm);
        var cost = c.Cost * unit;
        if (p.Count("linh_thach") < cost) throw new GameException($"Cần {cost:N0} linh thạch.");
        var win = KyDuyen.Roll(p.Id, slot, choice + 10 * (node.First + 1), c.Chance);   // bước 2 gieo riêng
        var o = win ? c.Win : c.Lose!;
        // ô trống tính theo cả kết quả thắng lẫn thua để báo lỗi hành trang không lộ kết quả gieo
        var outcomes = new[] { c.Win, c.Lose }.OfType<KyDuyen.Outcome>().ToArray();
        var need = outcomes.SelectMany(x => x.Items).Select(x => x.Item).Distinct().Count(i => !IsCurrency(i) && p.Count(i) == 0);   // linh thạch không chiếm ô
        if (BagCap(p) - BagUsed(p) < need) throw new GameException($"Hành trang cần {need} ô trống.");

        p.Add("linh_thach", -cost);
        if (o.Fight is { } foeId)   // quái ẩn / Boss: đánh ngay, thua thì suy yếu và không nhận gì
        {
            var foe = KyDuyen.Foes.First(x => x.Id == foeId);
            var battle = BattleAt(p, p.Area, slot.Start.UtcDateTime, foe: KyDuyenFoe(p, foe));
            if (!battle.Win)
            {
                var until = DateTime.UtcNow.AddMinutes(KyDuyen.FightLoseDebuff);
                if (until > p.DebuffUntil) p.DebuffUntil = until;
                p.KyDuyenKey = slot.Key;
                return Log(p, $"Kỳ duyên · {enc.Title}: {(foe.Boss ? "Boss" : "Quái ẩn")} {foe.Name} xuất hiện! Ngươi bại trận, phải lui về điều tức (suy yếu {KyDuyen.FightLoseDebuff} phút).", "bad");
            }
            o = o with { Text = $"Đánh bại {(foe.Boss ? "Boss" : "quái ẩn")} {foe.Name}! {o.Text}" };
        }
        var pages = KyDuyen.Pages(p);
        var doneBefore = pages.Select(n => n >= KyDuyen.PagesPerBook).ToArray();
        var added = KyDuyen.AddPages(pages, p.Realm, o.Pages);
        KyDuyen.SetPages(p, pages);
        foreach (var (item, qty) in o.Items) p.Add(item, qty);
        if (o.Stones > 0) p.Add("linh_thach", o.Stones * unit);
        p.DaoTam = Math.Clamp(p.DaoTam + o.DaoTam, -KyDuyen.DaoTamMax, KyDuyen.DaoTamMax);
        if (o.DebuffMinutes > 0)
        {
            var until = DateTime.UtcNow.AddMinutes(o.DebuffMinutes);
            if (until > p.DebuffUntil) p.DebuffUntil = until;
        }
        p.KyDuyenKey = slot.Key;

        for (var i = 0; i < pages.Length; i++)
            if (!doneBefore[i] && pages[i] >= KyDuyen.PagesPerBook)
                AddLog(p, $"Ghép đủ {KyDuyen.Books[i].Name}: {KyDuyen.Books[i].Bonus.Text()} vĩnh viễn.", "gold");
        if (!doneBefore.All(d => d) && KyDuyen.EndingFor(p) is { } end)
            AddLog(p, $"Đủ mười quyển Thiên Thư · {end.Name}: {end.Bonus.Text()}.", "gold");

        var gains = new List<string>();
        if (added > 0) gains.Add($"+{added} trang Thiên Thư");
        if (o.Pages > added) gains.Add($"{o.Pages - added} trang thất lạc (chưa mở quyển)");
        if (o.Stones > 0) gains.Add($"+{o.Stones * unit:N0} linh thạch");
        gains.AddRange(o.Items.Select(x => $"+{x.Qty} {GameData.ItemById[x.Item].Name}"));
        if (o.DaoTam != 0) gains.Add($"Đạo Tâm {o.DaoTam:+0;-0}");
        if (o.DebuffMinutes > 0) gains.Add($"suy yếu {o.DebuffMinutes} phút");
        if (cost > 0) gains.Insert(0, $"−{cost:N0} linh thạch");
        return Log(p, $"Kỳ duyên · {enc.Title}: {o.Text}{(gains.Count > 0 ? $" ({string.Join(", ", gains)})" : "")}", win ? "gold" : "bad");
    }

    private static string Hint(KyDuyen.Outcome o, long unit) => string.Join(" · ", new[]
    {
        o.Pages > 0 ? $"+{o.Pages} trang Thiên Thư" : "",
        o.Stones > 0 ? $"+{o.Stones * unit:N0} linh thạch" : "",
        string.Join(", ", o.Items.Select(x => $"+{x.Qty} {GameData.ItemById[x.Item].Name}")),
        o.DaoTam != 0 ? $"Đạo Tâm {o.DaoTam:+0;-0}" : "",
    }.Where(s => s.Length > 0));

    public KyDuyenDto KyDuyenStateFor(Player p) => KyDuyenStateAt(p, EventNow);

    public KyDuyenDto KyDuyenStateAt(Player p, DateTimeOffset? at)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var pages = KyDuyen.Pages(p);
        var unit = KyDuyen.StoneUnit(p.Realm);
        KyDuyenOpenDto? open = null;
        var blocked = EventBlocked(p);
        if (at is not null && blocked is null && p.StarterChosen && KyDuyenSlot(p, now) is { } slot && p.KyDuyenKey != slot.Key)
        {
            var enc = KyDuyenPick(p, slot);
            var node = KyDuyenNode(p, slot, enc);
            open = new(slot.Key, slot.End.ToUnixTimeMilliseconds(), enc.Title, node.Text,
                new[] { node.A, node.B }.Select(c => new KyDuyenChoiceDto(c.Label, (int)Math.Round(c.Chance * 100), c.Cost * unit, Hint(c.Win, unit)))
                    .Append(new KyDuyenChoiceDto(KyDuyenSkipLabel, 100, 0, "")).ToList());   // chỉ số KyDuyenSkip = 2
        }
        var books = KyDuyen.Books.Select((b, i) => new ThienThuBookDto(b.Name, pages[i], KyDuyen.PagesPerBook, p.Realm >= i, b.Bonus.Text(),
            pages[i] >= KyDuyen.PagesPerBook ? b.Lore : null)).ToList();
        var end = KyDuyen.EndingFor(p);
        return new(at is not null, blocked, open, KyDuyen.Next(now).Start.ToUnixTimeMilliseconds(), p.DaoTam, books, end?.Name, end?.Text,
            Player.AdminEnabled ? KyDuyen.Encounters.Select(x => new KyDuyenPickDto(x.Id, x.Title)).ToList() : null);
    }
}
