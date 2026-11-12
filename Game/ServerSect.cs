using Microsoft.EntityFrameworkCore;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Tông Môn: đọc/ghi bảng Sects, SectMembers, SectApplications và kênh chat tông (ChatMessages.SectId).
/// Chạy được cả SQLite (offline) lẫn Postgres (online). Dữ liệu dùng chung giữa nhiều tài khoản chỉ đổi bằng UPDATE có điều kiện
/// (ExecuteUpdate) để hai người thao tác cùng lúc không ghi đè nhau: người đến sau nhận 0 dòng và được báo lỗi.
/// Tên/cảnh giới người khác đọc qua IgnoreQueryFilters (query filter OwnerId chỉ cho thấy nhân vật của mình), chỉ chiếu vài cột.
/// Không đụng linh thạch / vật phẩm: phần đó ở GameService.Sect.cs.
/// </summary>
public sealed class ServerSect(GameDbContext db)
{
    private sealed record Info(Guid Id, string Name, int Realm, DateTime LastTickAt, bool Blocked);

    private Task<Info?> InfoAsync(Guid id) =>
        db.Players.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == id)
            .Select(p => new Info(p.Id, p.Name, p.Realm, p.LastTickAt, p.Flagged || p.Tainted)).FirstOrDefaultAsync();

    /// <summary>SQLite trả DateTime Kind Unspecified: gắn UTC để JSON có "Z", trình duyệt đổi đúng giờ địa phương.</summary>
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private static string RealmName(int realm) => GameData.Realms[Math.Clamp(realm, 0, GameData.Realms.Count - 1)].Name;

    /// <summary>Dòng thành viên của mình (tạo khi chưa có). Trả bản không theo dõi.</summary>
    private async Task<SectMember> MemberAsync(Guid me, DateTime now)
    {
        var m = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == me);
        if (m is not null) return m;
        m = new SectMember { PlayerId = me, JoinedAt = now };
        db.SectMembers.Add(m);
        await db.SaveChangesAsync();
        db.Entry(m).State = EntityState.Detached;
        return m;
    }

    private async Task<(SectMember M, Sect S)> MineAsync(Guid me, DateTime now)
    {
        var m = await MemberAsync(me, now);
        var s = m.SectId is { } sid ? await db.Sects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid) : null;
        return s is null ? throw new GameException("Chưa gia nhập Tông Môn.") : (m, s);
    }

    private static int RejoinWait(SectMember m, DateTime now) =>
        m.LeftAt is { } left ? Math.Max(0, (int)Math.Ceiling((left.AddMinutes(SectData.RejoinMinutes) - now).TotalSeconds)) : 0;

    private static void RequireFree(SectMember m, DateTime now)
    {
        if (m.SectId is not null) throw new GameException("Đã ở trong một Tông Môn.");
        var wait = RejoinWait(m, now);
        if (wait > 0) throw new GameException($"Vừa rời tông, còn {Math.Ceiling(wait / 3600.0)} giờ nữa mới gia nhập được tông khác.");
    }

    internal async Task SystemAsync(long sectId, string text, DateTime now)
    {
        db.ChatMessages.Add(new ChatMessage { At = now, Kind = ChatRules.KindSystem, SectId = sectId,
            Text = text.Length <= ChatRules.SystemMaxLen ? text : text[..ChatRules.SystemMaxLen] });
        await db.SaveChangesAsync();
    }

    /// <summary>Vào tông bằng UPDATE có điều kiện (chưa ở tông nào). Xóa mọi đơn còn lại của người đó.</summary>
    private async Task<bool> JoinAsync(Guid who, long sectId, string role, DateTime now)
    {
        var n = await db.SectMembers.Where(x => x.PlayerId == who && x.SectId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SectId, sectId).SetProperty(x => x.Role, role).SetProperty(x => x.JoinedAt, now));
        await db.SectApplications.Where(a => a.PlayerId == who).ExecuteDeleteAsync();
        if (n == 0) return false;
        SectBuff.Forget(who);
        return true;
    }

    private async Task<int> RemoveAsync(Guid who, long sectId, DateTime now)
    {
        var n = await db.SectMembers.Where(x => x.PlayerId == who && x.SectId == sectId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SectId, (long?)null).SetProperty(x => x.Role, SectData.Disciple).SetProperty(x => x.LeftAt, now));
        SectBuff.Forget(who);
        return n;
    }

    private async Task AddPointsAsync(Guid me, long sectId, int points)
    {
        await db.Sects.Where(x => x.Id == sectId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Fund, x => x.Fund + points));
    }

    /// <summary>Cộng điểm cống hiến cho người chơi và cùng số điểm vào quỹ tông (dùng cho Boss tông).</summary>
    public async Task AwardAsync(Guid me, long sectId, int points)
    {
        await db.SectMembers.Where(x => x.PlayerId == me && x.SectId == sectId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Contribution, x => x.Contribution + points)
                .SetProperty(x => x.TotalContribution, x => x.TotalContribution + points));
        await AddPointsAsync(me, sectId, points);
    }

    // ---------- Trang ----------

    public async Task<SectPageDto> PageAsync(Guid me, DateTime now)
    {
        var info = await InfoAsync(me) ?? throw new KeyNotFoundException();
        var m = await MemberAsync(me, now);
        var sect = m.SectId is { } sid ? await db.Sects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid) : null;
        if (sect is not null && await LeaderAwayAsync(sect, now)) {
            m = await MemberAsync(me, now);
            sect = await db.Sects.AsNoTracking().FirstAsync(x => x.Id == sect.Id);
        }
        SectBuff.Set(me, sect?.Level ?? 0, now, sect?.BuffDay ?? "");
        var day = SectData.DayOf(now);
        var donatesUsed = m.DonateDay == day ? m.DonateCount : 0;
        var meDto = new SectMeDto(sect is null ? "" : m.Role, m.Contribution, m.TotalContribution, m.CheckinDay == day,
            Math.Max(0, SectData.DonatesPerDay - donatesUsed),
            Enumerable.Range(0, SectData.Donates.Length).Select(t => SectData.DonateCost(t, info.Realm)).ToArray(),
            SectData.Donates.Select(d => d.Points).ToArray(), SectData.CreateCost(info.Realm), RejoinWait(m, now), SectData.MinRealm, info.Realm);
        var levels = SectData.Levels.Select(l => new SectLevelDto(l.Level, l.Cost, l.QiPct)).ToList();
        if (sect is null)
            return new SectPageDto(meDto, null, [], [], await ListAsync(me), [], levels, SectData.MaxElders);

        var members = await MembersAsync(sect.Id, me);
        var leaderName = members.FirstOrDefault(x => x.Role == SectData.Leader)?.Name ?? "";
        var apps = SectData.IsOfficer(m.Role) ? await ApplicationsAsync(sect.Id) : [];
        var week = SectData.WeekOf(now);
        var bought = m.ShopWeek == week ? SectData.ParseBought(m.ShopBought) : new();
        var shop = SectData.Shop.Select(s => new SectShopDto(s.ItemId, s.Price, s.WeeklyLimit, bought.GetValueOrDefault(s.ItemId), s.MinLevel)).ToList();
        var dto = new SectInfoDto(sect.Id, sect.Name, sect.Level, sect.Fund, sect.Notice, sect.AutoAccept, sect.MinRealm, leaderName,
            members.Count, SectData.QiPct(sect.Level), SectData.NextCost(sect.Level));
        return new SectPageDto(meDto, dto, members, apps, [], shop, levels, SectData.MaxElders);
    }

    private async Task<List<SectMemberDto>> MembersAsync(long sectId, Guid me)
    {
        var rows = await db.SectMembers.AsNoTracking().Where(x => x.SectId == sectId)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id,
                (x, p) => new { x.PlayerId, p.Name, p.Realm, x.Role, x.TotalContribution, p.LastTickAt })
            .ToListAsync();
        static int Rank(string r) => r == SectData.Leader ? 0 : r == SectData.Elder ? 1 : 2;
        return rows.OrderBy(r => Rank(r.Role)).ThenByDescending(r => r.TotalContribution).ThenBy(r => r.Name)
            .Select(r => new SectMemberDto(r.PlayerId, r.Name, r.Realm, RealmName(r.Realm), r.Role, r.TotalContribution, Utc(r.LastTickAt), r.PlayerId == me))
            .ToList();
    }

    private async Task<List<SectApplicationDto>> ApplicationsAsync(long sectId)
    {
        var rows = await db.SectApplications.AsNoTracking().Where(a => a.SectId == sectId)
            .Join(db.Players.IgnoreQueryFilters(), a => a.PlayerId, p => p.Id, (a, p) => new { a.Id, a.PlayerId, p.Name, p.Realm, a.At })
            .ToListAsync();
        return rows.OrderBy(r => r.At).Select(r => new SectApplicationDto(r.Id, r.PlayerId, r.Name, r.Realm, RealmName(r.Realm), Utc(r.At))).ToList();
    }

    private async Task<List<SectSummaryDto>> ListAsync(Guid me)
    {
        var sects = await db.Sects.AsNoTracking().ToListAsync();
        if (sects.Count == 0) return [];
        var counts = await db.SectMembers.AsNoTracking().Where(x => x.SectId != null).GroupBy(x => x.SectId!.Value)
            .Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
        var leaderIds = sects.Select(s => s.LeaderId).ToList();
        var names = await db.Players.IgnoreQueryFilters().AsNoTracking().Where(p => leaderIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
        var applied = (await db.SectApplications.AsNoTracking().Where(a => a.PlayerId == me).Select(a => a.SectId).ToListAsync()).ToHashSet();
        return sects.Select(s => new SectSummaryDto(s.Id, s.Name, s.Level, counts.GetValueOrDefault(s.Id), s.MinRealm, s.AutoAccept,
                names.GetValueOrDefault(s.LeaderId, ""), applied.Contains(s.Id), s.Fund))
            .OrderByDescending(s => s.Applied).ThenByDescending(s => s.Level).ThenByDescending(s => s.Fund).ThenByDescending(s => s.Members).ThenBy(s => s.Id)
            .Take(SectData.ListSize).ToList();
    }

    /// <summary>Tông chủ vắng quá 7 ngày (hoặc nhân vật đã mất): chuyển chức cho người còn hoạt động,
    /// ưu tiên Trưởng lão rồi tổng cống hiến. Trả true nếu đã đổi.</summary>
    private async Task<bool> LeaderAwayAsync(Sect sect, DateTime now)
    {
        var cutoff = now.AddDays(-SectData.LeaderAwayDays);
        var leader = await db.Players.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == sect.LeaderId)
            .Select(p => new { p.LastTickAt }).FirstOrDefaultAsync();
        var leaderIn = await db.SectMembers.AnyAsync(x => x.PlayerId == sect.LeaderId && x.SectId == sect.Id);
        if (leader is not null && leaderIn && leader.LastTickAt >= cutoff) return false;
        var next = await db.SectMembers.AsNoTracking().Where(x => x.SectId == sect.Id && x.PlayerId != sect.LeaderId)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => new { x.PlayerId, x.Role, x.TotalContribution, p.LastTickAt, p.Name })
            .Where(x => x.LastTickAt >= cutoff).ToListAsync();
        var pick = next.OrderBy(x => x.Role == SectData.Elder ? 0 : 1).ThenByDescending(x => x.TotalContribution).FirstOrDefault();
        if (pick is null) return false;
        if (await db.Sects.Where(x => x.Id == sect.Id && x.LeaderId == sect.LeaderId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaderId, pick.PlayerId)) == 0) return false;
        await db.SectMembers.Where(x => x.PlayerId == sect.LeaderId && x.SectId == sect.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Role, SectData.Disciple));
        await db.SectMembers.Where(x => x.PlayerId == pick.PlayerId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Role, SectData.Leader));
        await SystemAsync(sect.Id, $"Tông chủ vắng mặt quá {SectData.LeaderAwayDays} ngày, {pick.Name} lên kế nhiệm Tông chủ.", now);
        await db.SaveChangesAsync();
        return true;
    }

    // ---------- Tạo / xin vào / rời ----------

    /// <summary>Tạo tông; người gọi đã kiểm linh thạch và trừ sau khi hàm này xong.</summary>
    public async Task<string> CreateAsync(Guid me, string? rawName, DateTime now)
    {
        var info = await InfoAsync(me) ?? throw new KeyNotFoundException();
        if (info.Realm < SectData.MinRealm) throw new GameException("Cần đạt Trúc Cơ mới lập được Tông Môn.");
        var m = await MemberAsync(me, now);
        RequireFree(m, now);
        var name = SectData.CleanName(rawName) ?? throw new GameException($"Tên tông cần {SectData.NameMin}–{SectData.NameMax} ký tự.");
        var key = SectData.NameKey(name);
        if (await db.Sects.AnyAsync(x => x.NameKey == key)) throw new GameException("Tên tông đã có người dùng.");
        var sect = new Sect { Name = name, NameKey = key, LeaderId = me, CreatedAt = now, MinRealm = SectData.MinRealm };
        db.Sects.Add(sect);
        await db.SaveChangesAsync();
        db.Entry(sect).State = EntityState.Detached;   // các thay đổi sau đều bằng ExecuteUpdate: không giữ bản cũ trong ChangeTracker
        if (!await JoinAsync(me, sect.Id, SectData.Leader, now)) throw new GameException("Đã ở trong một Tông Môn.");
        SectBuff.Set(me, sect.Level, now);
        await SystemAsync(sect.Id, $"{info.Name} khai sơn lập phái, Tông Môn {name} ra đời.", now);
        return name;
    }

    public async Task<string> ApplyAsync(Guid me, long sectId, DateTime now)
    {
        var info = await InfoAsync(me) ?? throw new KeyNotFoundException();
        var m = await MemberAsync(me, now);
        RequireFree(m, now);
        var sect = await db.Sects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sectId) ?? throw new GameException("Tông Môn không còn tồn tại.");
        if (info.Realm < Math.Max(SectData.MinRealm, sect.MinRealm))
            throw new GameException($"{sect.Name} chỉ nhận đệ tử từ {RealmName(Math.Max(SectData.MinRealm, sect.MinRealm))} trở lên.");
        if (await db.SectApplications.AnyAsync(a => a.PlayerId == me && a.SectId == sectId)) throw new GameException("Đã gửi đơn vào tông này, hãy chờ duyệt.");
        if (sect.AutoAccept)
        {
            if (!await JoinAsync(me, sect.Id, SectData.Disciple, now)) throw new GameException("Đã ở trong một Tông Môn.");
            await SystemAsync(sect.Id, $"{info.Name} gia nhập tông môn.", now);
            return $"Đã gia nhập {sect.Name}.";
        }
        if (await db.SectApplications.CountAsync(a => a.PlayerId == me) >= SectData.MaxPendingApps)
            throw new GameException($"Chỉ được chờ duyệt tối đa {SectData.MaxPendingApps} đơn cùng lúc.");
        db.SectApplications.Add(new SectApplication { SectId = sectId, PlayerId = me, At = now });
        await db.SaveChangesAsync();
        return $"Đã gửi đơn xin vào {sect.Name}.";
    }

    public async Task<string> CancelApplyAsync(Guid me, long sectId)
    {
        var n = await db.SectApplications.Where(a => a.PlayerId == me && a.SectId == sectId).ExecuteDeleteAsync();
        return n == 0 ? throw new GameException("Không có đơn nào để rút.") : "Đã rút đơn.";
    }

    public async Task<string> DecideAsync(Guid me, long appId, bool approve, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        if (!SectData.IsOfficer(m.Role)) throw new GameException("Chỉ Tông chủ và Trưởng lão được duyệt đơn.");
        var app = await db.SectApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appId && a.SectId == s.Id)
                  ?? throw new GameException("Đơn này không còn.");
        var who = await InfoAsync(app.PlayerId);
        if (!approve || who is null)
        {
            await db.SectApplications.Where(a => a.Id == appId).ExecuteDeleteAsync();
            return "Đã từ chối đơn.";
        }
        if (!await JoinAsync(app.PlayerId, s.Id, SectData.Disciple, now)) return $"{who.Name} đã vào tông khác.";
        await SystemAsync(s.Id, $"{who.Name} gia nhập tông môn.", now);
        return $"Đã nhận {who.Name} vào tông.";
    }

    public async Task<string> LeaveAsync(Guid me, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        var info = await InfoAsync(me);
        if (m.Role == SectData.Leader)
        {
            if (await db.SectMembers.CountAsync(x => x.SectId == s.Id) > 1)
                throw new GameException("Hãy nhường chức Tông chủ trước khi rời tông.");
            // còn một mình: giải tán, không tính thời gian chờ
            await db.ChatMessages.Where(x => x.SectId == s.Id).ExecuteDeleteAsync();
            await db.SectApplications.Where(a => a.SectId == s.Id).ExecuteDeleteAsync();
            await db.SectMembers.Where(x => x.SectId == s.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.SectId, (long?)null).SetProperty(x => x.Role, SectData.Disciple));
            await db.Sects.Where(x => x.Id == s.Id).ExecuteDeleteAsync();
            SectBuff.Clear();
            return $"Đã giải tán {s.Name}.";
        }
        if (await RemoveAsync(me, s.Id, now) == 0) throw new GameException("Chưa gia nhập Tông Môn.");
        await SystemAsync(s.Id, $"{info?.Name} rời tông môn.", now);
        return $"Đã rời {s.Name}. {SectData.RejoinMinutes} phút nữa mới gia nhập được tông khác.";
    }

    // ---------- Quản lý ----------

    private async Task<(SectMember M, Sect S, SectMember T, Info TInfo)> TargetAsync(Guid me, Guid target, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        if (target == me) throw new GameException("Không thể tự làm việc này với chính mình.");
        var t = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == target && x.SectId == s.Id)
                ?? throw new GameException("Người này không ở trong tông.");
        var ti = await InfoAsync(target) ?? throw new GameException("Người này không ở trong tông.");
        return (m, s, t, ti);
    }

    public async Task<string> KickAsync(Guid me, Guid target, DateTime now)
    {
        var (m, s, t, ti) = await TargetAsync(me, target, now);
        var ok = m.Role == SectData.Leader || (m.Role == SectData.Elder && t.Role == SectData.Disciple);
        if (!ok) throw new GameException("Không đủ quyền trục xuất người này.");
        if (await RemoveAsync(target, s.Id, now) == 0) throw new GameException("Người này không ở trong tông.");
        await SystemAsync(s.Id, $"{ti.Name} bị trục xuất khỏi tông môn.", now);
        return $"Đã trục xuất {ti.Name}.";
    }

    public async Task<string> SetRoleAsync(Guid me, Guid target, string role, DateTime now)
    {
        if (role is not (SectData.Elder or SectData.Disciple)) throw new GameException("Chức vị không hợp lệ.");
        var (m, s, t, ti) = await TargetAsync(me, target, now);
        if (m.Role != SectData.Leader) throw new GameException("Chỉ Tông chủ được bổ nhiệm chức vị.");
        if (t.Role == role) return "Chức vị không đổi.";
        if (role == SectData.Elder && await db.SectMembers.CountAsync(x => x.SectId == s.Id && x.Role == SectData.Elder) >= SectData.MaxElders)
            throw new GameException($"Tông chỉ có tối đa {SectData.MaxElders} Trưởng lão.");
        await db.SectMembers.Where(x => x.PlayerId == target && x.SectId == s.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, role));
        var text = role == SectData.Elder ? $"{ti.Name} được phong Trưởng lão." : $"{ti.Name} thôi giữ chức Trưởng lão.";
        await SystemAsync(s.Id, text, now);
        return text;
    }

    public async Task<string> TransferAsync(Guid me, Guid target, DateTime now)
    {
        var (m, s, t, ti) = await TargetAsync(me, target, now);
        if (m.Role != SectData.Leader) throw new GameException("Chỉ Tông chủ được nhường chức.");
        if (await db.Sects.Where(x => x.Id == s.Id && x.LeaderId == me).ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaderId, target)) == 0)
            throw new GameException("Chức Tông chủ đã đổi, hãy tải lại.");
        var elders = await db.SectMembers.CountAsync(x => x.SectId == s.Id && x.Role == SectData.Elder && x.PlayerId != target);
        var myRole = elders < SectData.MaxElders ? SectData.Elder : SectData.Disciple;
        await db.SectMembers.Where(x => x.PlayerId == me).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, myRole));
        await db.SectMembers.Where(x => x.PlayerId == target).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, SectData.Leader));
        await SystemAsync(s.Id, $"{ti.Name} kế nhiệm Tông chủ.", now);
        return $"Đã nhường chức Tông chủ cho {ti.Name}.";
    }

    public async Task<string> NoticeAsync(Guid me, string? text, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        if (!SectData.IsOfficer(m.Role)) throw new GameException("Chỉ Tông chủ và Trưởng lão được sửa thông báo.");
        var clean = ChatRules.Clean(text);
        if (clean.Length > SectData.NoticeMax) clean = clean[..SectData.NoticeMax];
        await db.Sects.Where(x => x.Id == s.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Notice, clean));
        return "Đã cập nhật thông báo tông.";
    }

    public async Task<string> SettingsAsync(Guid me, bool autoAccept, int minRealm, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        if (m.Role != SectData.Leader) throw new GameException("Chỉ Tông chủ được đổi cách nhận đệ tử.");
        var min = Math.Clamp(minRealm, SectData.MinRealm, GameData.Realms.Count - 1);
        await db.Sects.Where(x => x.Id == s.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.AutoAccept, autoAccept).SetProperty(x => x.MinRealm, min));
        return autoAccept ? $"Tự nhận đệ tử từ {RealmName(min)} trở lên." : $"Duyệt tay, nhận đơn từ {RealmName(min)} trở lên.";
    }

    public async Task<string> UpgradeAsync(Guid me, DateTime now)
    {
        var (m, s) = await MineAsync(me, now);
        if (m.Role != SectData.Leader) throw new GameException("Chỉ Tông chủ được nâng cấp tông.");
        if (SectData.NextCost(s.Level) is not { } cost) throw new GameException("Tông đã đạt cấp tối đa.");
        var lv = s.Level;
        if (await db.Sects.Where(x => x.Id == s.Id && x.Level == lv && x.Fund >= cost)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Level, x => x.Level + 1).SetProperty(x => x.Fund, x => x.Fund - cost)) == 0)
            throw new GameException($"Quỹ tông chưa đủ {cost:N0} để lên cấp {lv + 1}.");
        SectBuff.Clear();
        var text = $"Tông Môn lên cấp {lv + 1}: tốc độ tu luyện +{SectData.QiPct(lv + 1)}%.";
        await SystemAsync(s.Id, text, now);
        return text;
    }

    // ---------- Cống hiến ----------

    public async Task<int> CheckinAsync(Guid me, DateTime now)
    {
        var (_, s) = await MineAsync(me, now);
        var day = SectData.DayOf(now);
        var pts = SectData.CheckinPoints;
        if (await db.SectMembers.Where(x => x.PlayerId == me && x.SectId == s.Id && x.CheckinDay != day)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.CheckinDay, day)
                    .SetProperty(x => x.Contribution, x => x.Contribution + pts).SetProperty(x => x.TotalContribution, x => x.TotalContribution + pts)) == 0)
            throw new GameException("Hôm nay đã điểm danh.");
        await AddPointsAsync(me, s.Id, pts);
        return pts;
    }

    /// <summary>Ghi nhận một lần quyên (đếm lượt trong ngày, cộng cống hiến + quỹ). Linh thạch do người gọi trừ.</summary>
    public async Task<int> DonateAsync(Guid me, int tier, DateTime now)
    {
        if (tier < 0 || tier >= SectData.Donates.Length) throw new GameException("Mức quyên góp không hợp lệ.");
        var (_, s) = await MineAsync(me, now);
        var day = SectData.DayOf(now);
        var pts = SectData.Donates[tier].Points;
        var max = SectData.DonatesPerDay;
        if (await db.SectMembers.Where(x => x.PlayerId == me && x.SectId == s.Id && (x.DonateDay != day || x.DonateCount < max))
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.DonateCount, x => x.DonateDay == day ? x.DonateCount + 1 : 1)
                    .SetProperty(x => x.DonateDay, day)
                    .SetProperty(x => x.Contribution, x => x.Contribution + pts).SetProperty(x => x.TotalContribution, x => x.TotalContribution + pts)) == 0)
            throw new GameException($"Hôm nay đã quyên đủ {max} lần.");
        await AddPointsAsync(me, s.Id, pts);
        return pts;
    }

    /// <summary>Trừ cống hiến và ghi lượt mua tuần. Vật phẩm do người gọi cộng. Trả tổng giá.</summary>
    public async Task<int> BuyAsync(Guid me, string itemId, int n, DateTime now)
    {
        if (!SectData.ShopById.TryGetValue(itemId, out var item)) throw new GameException("Cửa hàng tông không bán món này.");
        if (n < 1) throw new GameException("Số lượng không hợp lệ.");
        var (m, s) = await MineAsync(me, now);
        if (s.Level < item.MinLevel) throw new GameException($"Tông cần đạt cấp {item.MinLevel} để mở món này.");
        var week = SectData.WeekOf(now);
        var bought = m.ShopWeek == week ? SectData.ParseBought(m.ShopBought) : new();
        var had = bought.GetValueOrDefault(itemId);
        if (had + n > item.WeeklyLimit) throw new GameException($"Tuần này chỉ mua được thêm {Math.Max(0, item.WeeklyLimit - had)} món.");
        var price = item.Price * n;
        if (m.Contribution < price) throw new GameException($"Cần {price:N0} cống hiến.");
        bought[itemId] = had + n;
        var text = SectData.FormatBought(bought);
        string oldWeek = m.ShopWeek, oldBought = m.ShopBought;
        if (await db.SectMembers.Where(x => x.PlayerId == me && x.SectId == s.Id && x.Contribution >= price && x.ShopWeek == oldWeek && x.ShopBought == oldBought)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Contribution, x => x.Contribution - price)
                    .SetProperty(x => x.ShopWeek, week).SetProperty(x => x.ShopBought, text)) == 0)
            throw new GameException("Cống hiến vừa thay đổi, hãy thử lại.");
        return price;
    }

    // ---------- Chat tông ----------

    public async Task<ChatPageDto> ChatPageAsync(Guid me, long after, DateTime now)
    {
        var info = await InfoAsync(me) ?? throw new KeyNotFoundException();
        var m = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == me);
        if (m?.SectId is not { } sid) return ChatPageDto.Off;
        var q = db.ChatMessages.AsNoTracking().Where(x => x.SectId == sid);
        var rows = after > 0
            ? await q.Where(x => x.Id > after).OrderBy(x => x.Id).Take(ChatRules.PageSize).ToListAsync()
            : (await q.OrderByDescending(x => x.Id).Take(ChatRules.PageSize).ToListAsync()).AsEnumerable().Reverse().ToList();
        var why = ChatRules.LockReason(info.Realm, info.Blocked);
        var wait = why is null ? ChatRules.CooldownLeft(await LastPostAsync(me), now) : 0;
        var online = await db.SectMembers.AsNoTracking().Where(x => x.SectId == sid)
            .Join(db.Players.IgnoreQueryFilters(), x => x.PlayerId, p => p.Id, (x, p) => p.LastTickAt)
            .CountAsync(t => t > now.AddMinutes(-ChatRules.OnlineMinutes));
        return new ChatPageDto(true, rows.Select(x => new ChatMessageDto(x.Id, Utc(x.At), x.Kind, x.Name, x.Realm,
                x.Kind == ChatRules.KindSystem ? "" : RealmName(x.Realm), x.Text, x.PlayerId == me)).ToList(),
            online, why is null && wait == 0, wait, why, ChatRules.MaxLen);
    }

    private Task<DateTime?> LastPostAsync(Guid me) =>
        db.ChatMessages.Where(x => x.PlayerId == me).MaxAsync(x => (DateTime?)x.At);

    public async Task<ChatPageDto> ChatPostAsync(Guid me, string? text, long after, DateTime now)
    {
        var info = await InfoAsync(me) ?? throw new KeyNotFoundException();
        var m = await db.SectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PlayerId == me);
        if (m?.SectId is not { } sid) throw new GameException("Chưa gia nhập Tông Môn.");
        if (ChatRules.LockReason(info.Realm, info.Blocked) is { } why) throw new GameException(why);
        var clean = ChatRules.Clean(text);
        if (clean.Length == 0) throw new GameException("Chưa nhập nội dung.");
        var wait = ChatRules.CooldownLeft(await LastPostAsync(me), now);
        if (wait > 0) throw new GameException($"Chờ {wait} giây nữa mới truyền âm tiếp được.");
        db.ChatMessages.Add(new ChatMessage { At = now, Kind = ChatRules.KindPlayer, PlayerId = me, Name = info.Name, Realm = info.Realm, Text = clean, SectId = sid });
        await db.SaveChangesAsync();
        return await ChatPageAsync(me, after, now);
    }
}
