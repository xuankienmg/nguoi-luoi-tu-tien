using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Desktop;
using VanDao.Game;

namespace VanDao;

public static class Program
{
    /// <summary>
    /// Mặc định: chạy server nội bộ và mở cửa sổ game nổi (kiểu picture-in-picture).
    /// Thêm tham số --web để chỉ chạy server và chơi trên trình duyệt như trước.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var webOnly = args.Contains("--web");
        var app = BuildServer(args.Where(a => a != "--web").ToArray(), webOnly);

        if (webOnly)
        {
            app.Run();
            return;
        }

        app.StartAsync().GetAwaiter().GetResult();
        var url = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameWindow(url));

        app.StopAsync().GetAwaiter().GetResult();
    }

    private static WebApplication BuildServer(string[] args, bool webOnly)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            // để vandao.db và wwwroot luôn nằm cạnh file chạy, dù khởi động từ đâu
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Chế độ cửa sổ: chỉ nghe trên máy này, cổng ngẫu nhiên để không đụng app khác
        if (!webOnly) builder.WebHost.UseUrls("http://127.0.0.1:0");

        // DB nằm trong %LocalAppData%\VanDao để không mất khi build lại hay xóa bin/
        // chạy Development dùng file riêng vandao_dev.db, không đụng save của bản phát hành
        var dbPath = Path.Combine(AppPaths.DataDir, builder.Environment.IsDevelopment() ? "vandao_dev.db" : "vandao.db");
        builder.Services.AddDbContext<GameDbContext>(o =>
            o.UseSqlite(builder.Configuration.GetConnectionString("Game") ?? $"Data Source={dbPath}"));
        builder.Services.AddScoped<GameService>();
        // bảng xếp hạng online (Supabase): cấu hình mục "Leaderboard" trong appsettings.json, bỏ trống = tắt
        builder.Services.AddSingleton(builder.Configuration.GetSection("Leaderboard").Get<LeaderboardOptions>() ?? new LeaderboardOptions());
        builder.Services.AddSingleton<LeaderboardService>();
        builder.Services.AddSingleton<VersionService>();  // bắt cập nhật: đọc app_config trên Supabase
        builder.Services.AddSingleton<MarketService>();   // Chợ giao dịch: dùng chung project Supabase
        builder.Services.AddSingleton<ArenaService>();    // Lôi đài PK: dùng chung project Supabase

        var app = builder.Build();
        Player.AdminEnabled = app.Environment.IsDevelopment();   // admin mode chỉ có khi chạy dev

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Database.EnsureCreated();
            SchemaUpgrade.Apply(db);
            SaveGuard.Check(db, app.Environment.IsDevelopment());   // chống quay về bản cũ: file dữ liệu nhớ bản cao nhất đã mở nó
        }

        // bản game thấp hơn min_version (app_config trên Supabase): từ chối mọi thao tác với nhân vật, giao diện hiện màn hình cập nhật
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api/players"))
            {
                var v = SaveGuard.Apply(await ctx.RequestServices.GetRequiredService<VersionService>().PeekAsync());
                if (v.Blocked)
                {
                    ctx.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                    await ctx.Response.WriteAsJsonAsync(new { error = SaveGuard.NewerSave is null ? $"Phiên bản {v.Current} đã cũ, hãy tải bản {v.Latest} để tiếp tục." : v.Message, update = v });
                    return;
                }
            }
            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        MapApi(app);
        return app;
    }

    private static void MapApi(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/data", () => new GameDataDto(
            GameData.AllItems,
            GameData.Slots,
            GameData.ExtraStats,
            GameData.Realms.Select((r, i) => new RealmInfoDto(i, r.Id, r.Name, r.Desc, r.Stages, r.Aura, r.Lotus, r.Sash, r.Hero, r.Resources, r.Cn, r.BagSlots, r.EquipSlots,
                r.Boss.Name, r.Boss.Image, GameData.ChapterDropping(r.BossItem)?.BossLair ?? "", GameData.ChapterDropping(r.BossItem)?.KeyItem ?? "", r.BossItem,
                r.HeroStand, r.HeroMed, r.HeroCast, r.Halo)),
            GameData.Synths, SkillData.Elements, SkillData.Skills, GameData.PillChain, GameData.Areas,
            GameData.LockedRealms, GameData.FuseChance,
            GameData.Chapters.Select(c => new ChapterInfoDto(c.Index, c.Name, c.Realm, c.Enabled, c.Boss.Name, c.Boss.Image, c.BossLair, c.KeyItem, c.BossItem,
                GameData.BreakthroughBossItem(c.Index, 1), GameData.BreakthroughBossItem(c.Index, 2))),
            GameData.Brews, GameData.HerbDrops, GameData.Skins, GameData.SpinWeights, GameData.SpinTicketPrice, EquipmentEffects.Sets, PetData.Definitions, PetData.PetSpinWeights, GameData.PetSpinTicketPrice));

        api.MapPost("/players", async (CreatePlayerRequest? req, GameService game) =>
        {
            var p = await game.CreateAsync(req?.Name);
            return Results.Ok(await game.BuildStateAsync(p));
        });

        api.MapGet("/players/current", async (GameService game) =>
        {
            await MutationGate.WaitAsync();
            try {
            var p = await game.CurrentAsync();
            await game.SaveAsync(p);
            return Results.Ok(new ActionResponse(await game.BuildStateAsync(p)));
            } finally { MutationGate.Release(); }
        });

        api.MapGet("/players/{id:guid}", (Guid id, GameService game) =>
            Run(id, game, _ => (null, null)));

        api.MapPost("/players/{id:guid}/channel", (Guid id, GameService game) =>
            Run(id, game, p => (game.Channel(p), null)));

        api.MapPost("/players/{id:guid}/equip-best", (Guid id, GameService game) =>
            Run(id, game, p => game.EquipBest(p)));
        api.MapPost("/players/{id:guid}/equip/{itemId}", (Guid id, string itemId, string? slot, GameService game) =>
            Run(id, game, p => game.Equip(p, itemId, slot)));

        api.MapPost("/players/{id:guid}/use/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.UseItem(p, itemId, n ?? 1)));

        api.MapPost("/players/{id:guid}/drop/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            Run(id, game, p => game.Drop(p, itemId, n ?? 1)));

        api.MapPost("/players/{id:guid}/skill/{skillId}", (Guid id, string skillId, GameService game) =>
            Run(id, game, p => game.UpgradeSkill(p, skillId)));

        // trang phục: skin = id trong GameData.Skins, rỗng = theo cảnh giới
        api.MapPost("/players/{id:guid}/skin", (Guid id, string? skin, GameService game) =>
            Run(id, game, p => game.SetSkin(p, skin)));
        api.MapPost("/players/{id:guid}/gender", (Guid id, string? gender, GameService game) =>
            Run(id, game, p => game.SetGender(p, gender)));

        // kích hoạt công pháp một hệ: đòn thường theo hệ đó, bị động hệ khác còn 50%
        api.MapPost("/players/{id:guid}/manual/{element}", (Guid id, string element, GameService game) =>
            Run(id, game, p => game.ActivateManual(p, element)));

        api.MapPost("/players/{id:guid}/skillslot/{slot:int}", (Guid id, int slot, string? skill, GameService game) =>
            Run(id, game, p => game.SetSkillSlot(p, slot, skill)));

        api.MapPost("/players/{id:guid}/sell/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            Run(id, game, p => game.Sell(p, itemId, n ?? 1)));

        api.MapPost("/players/{id:guid}/sell-bulk", (Guid id, int max, bool? gear, string? items, bool? pick, GameService game) =>
            Run(id, game, p => game.SellBulk(p, max, gear ?? false, items, pick ?? false)));

        api.MapPost("/players/{id:guid}/unequip/{slot}", (Guid id, string slot, GameService game) =>
            Run(id, game, p => game.Unequip(p, slot)));

        api.MapPost("/players/{id:guid}/advance", (Guid id, GameService game) =>
            Run(id, game, p => game.Advance(p)));

        // luyện khí: ghép 9 trang bị cùng phẩm → 1 phẩm cao hơn (items = danh sách id, lặp lại khi lấy nhiều món cùng id)
        api.MapPost("/players/{id:guid}/fuse", (Guid id, string? items, GameService game) =>
            RunCraft(id, game, p => game.Fuse(p, items)));

        // code quà tặng (nhập ở menu Cài đặt)
        api.MapPost("/players/{id:guid}/redeem", (Guid id, string? code, GameService game) =>
            Run(id, game, p => game.RedeemCode(p, code)));

        // Tài Xỉu: đổi linh thạch lấy vé (n = số vé, 0 = tối đa) · quay vòng trang bị 10 phẩm (n = số lượt, tối đa 10)
        api.MapPost("/players/{id:guid}/spin/buy", (Guid id, int? n, string? mode, GameService game) =>
            Run(id, game, p => game.BuySpinTicket(p, n ?? 1, mode == "pet")));
        api.MapPost("/players/{id:guid}/spin", (Guid id, int? n, string? mode, GameService game) =>
            RunCraft(id, game, p => mode == "pet" ? game.PetSpin(p, n ?? 1) : game.Spin(p, n ?? 1)));

        // sự kiện quay tích lũy Thiên Cơ: i = chỉ số mốc, bỏ trống / -1 = nhận mọi mốc đã đạt
        api.MapPost("/players/{id:guid}/event/claim", (Guid id, int? i, GameService game) => Run(id, game, p => game.ClaimEvent(p, i ?? -1)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/feed", (Guid id, Guid petId, int? n, GameService game) => Run(id, game, p => game.Feed(p, petId, n ?? 1)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/evolve", (Guid id, Guid petId, GameService game) => Run(id, game, p => game.Evolve(p, petId)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/appearance/{form:int}", (Guid id, Guid petId, int form, GameService game) => Run(id, game, p => game.SetPetAppearance(p, petId, form)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/slot/{slot:int}", (Guid id, Guid petId, int slot, GameService game) => Run(id, game, p => game.SetPetSlot(p, petId, slot)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/release", (Guid id, Guid petId, GameService game) => Run(id, game, p => game.Release(p, petId)));
        api.MapPost("/players/{id:guid}/pet/release-many", (Guid id, string? ids, GameService game) => Run(id, game, p => game.ReleaseMany(p, ids)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/merge-all", (Guid id, Guid petId, GameService game) => Run(id, game, p => game.MergeAll(p, petId)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/merge", (Guid id, Guid petId, string? foods, GameService game) => Run(id, game, p => game.MergePets(p, petId, foods)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/merge/{foodId:guid}", (Guid id, Guid petId, Guid foodId, GameService game) => Run(id, game, p => game.MergePet(p, petId, foodId)));
        // bồi dưỡng linh thú: tẩy luyện / bồi nguyên tư chất · trang bị linh thú · thức tỉnh huyết mạch
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/reroll", (Guid id, Guid petId, string? locks, GameService game) => Run(id, game, p => game.RerollAptitude(p, petId, locks)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/gear/{itemId}", (Guid id, Guid petId, string itemId, GameService game) => Run(id, game, p => game.EquipPetGear(p, petId, itemId)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/ungear/{slot}", (Guid id, Guid petId, string slot, GameService game) => Run(id, game, p => game.UnequipPetGear(p, petId, slot)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/awaken/{donorId:guid}", (Guid id, Guid petId, Guid donorId, GameService game) => Run(id, game, p => game.AwakenPet(p, petId, donorId)));
        api.MapPost("/players/{id:guid}/petgear/fuse/{itemId}", (Guid id, string itemId, int? n, GameService game) => RunCraft(id, game, p => game.FusePetGear(p, itemId, n ?? 1)));
        api.MapPost("/players/{id:guid}/hatch/now", (Guid id, int? slot, GameService game) => Run(id, game, p => game.HatchNow(p, slot ?? 0)));
        api.MapPost("/players/{id:guid}/hatch/collect", (Guid id, int? slot, GameService game) => Run(id, game, p => game.CollectHatch(p, slot ?? 0)));
        api.MapPost("/players/{id:guid}/hatch/{eggId}", (Guid id, string eggId, int? slot, GameService game) => Run(id, game, p => game.Hatch(p, eggId, slot ?? 0)));

        // đan dược: hợp thành 9 → 1 (n = số lần, 0 = tối đa)
        api.MapPost("/players/{id:guid}/synth/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.Synthesize(p, itemId, n ?? 1)));

        // luyện đan theo đan phương (linh dược + đan + linh thạch → 1 viên), n = số lần, 0 = tối đa
        api.MapPost("/players/{id:guid}/brew/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.Brew(p, itemId, n ?? 1)));

        // Cổ Phù & Tiên Châu: khảm nạm vào ô trang bị · tháo khảm · mở lỗ khảm
        api.MapPost("/players/{id:guid}/gem/socket", (Guid id, string slot, int socketIndex, string itemId, GameService game) =>
            Run(id, game, p => game.SocketGem(p, slot, socketIndex, itemId)));
        api.MapPost("/players/{id:guid}/gem/unsocket", (Guid id, string slot, int socketIndex, GameService game) =>
            Run(id, game, p => game.UnsocketGem(p, slot, socketIndex)));
        api.MapPost("/players/{id:guid}/gem/unlock", (Guid id, string slot, GameService game) =>
            Run(id, game, p => game.UnlockSocket(p, slot)));
        api.MapPost("/players/{id:guid}/gem/craft/{recipeId}", (Guid id, string recipeId, int? n, GameService game) =>
            RunCraft(id, game, p => game.CraftGemRecipe(p, recipeId, n ?? 1)));
        api.MapPost("/players/{id:guid}/gem/quick-fuse", (Guid id, GameService game) =>
            RunCraft(id, game, p => game.QuickFuseGems(p)));

        // bản đồ: chọn ải 1..15 của chương đang đánh, 16 = Boss (cần lệnh Boss)
        api.MapPost("/players/{id:guid}/area/{area:int}", (Guid id, int area, GameService game) =>
            Run(id, game, p => game.SelectArea(p, area)));

        // bản đồ: chọn chương đã mở (0-based)
        api.MapGet("/players/{id:guid}/map/{chapter:int}", async (Guid id, int chapter, GameService game) => {
            try { return Results.Ok(await game.PreviewMapAsync(id, chapter)); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        api.MapPost("/players/{id:guid}/map/{chapter:int}/area/{area:int}", (Guid id, int chapter, int area, GameService game) =>
            Run(id, game, p => game.SelectMapArea(p, chapter, area)));
        api.MapPost("/players/{id:guid}/map/{chapter:int}", (Guid id, int chapter, GameService game) =>
            Run(id, game, p => game.SelectMap(p, chapter)));

        // bản đồ: chọn mức độ khó (0 Thường · 1 Khó · 2 Ác mộng)
        api.MapPost("/players/{id:guid}/diff/{d:int}", (Guid id, int d, GameService game) =>
            Run(id, game, p => game.SelectDiff(p, d)));

        // admin mode: lệnh gian lận để thử game
        api.MapPost("/players/{id:guid}/admin/{action}", (Guid id, string action, string? arg, int? n, GameService game) =>
            Player.AdminEnabled ? (action == "clearflag"
                ? RunAsync(id, game, p => game.AdminClearDataFlagAsync(p, arg))
                : Run(id, game, p => game.AdminAction(p, action, arg, n ?? 1)))
                : Task.FromResult<IResult>(Results.Json(new { error = "Admin mode chỉ có ở môi trường phát triển." }, statusCode: StatusCodes.Status403Forbidden)));

        // môi trường: giao diện chỉ bật admin mode khi dev
        api.MapGet("/env", () => new { dev = Player.AdminEnabled });

        // bảng xếp hạng online: by = realm (cảnh giới) | power (lực chiến)
        api.MapGet("/players/{id:guid}/leaderboard", async (Guid id, string? by, GameService game) =>
        {
            try { return Results.Ok(await game.LeaderboardAsync(await game.LoadAsync(id), by)); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
        });

        // phiên bản: ok · update · required · unknown (giao diện gọi khi mở game và định kỳ)
        api.MapGet("/version", async (VersionService v, bool? force) => SaveGuard.Apply(await v.GetAsync(force ?? false)));

        // Chợ giao dịch giữa người chơi (Supabase)
        api.MapGet("/players/{id:guid}/market", async (Guid id, GameService game) =>
        {
            try { return Results.Ok(await game.MarketAsync(await game.LoadAsync(id))); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
        });
        api.MapPost("/players/{id:guid}/market/list/{itemId}", (Guid id, string itemId, int? qty, long? price, GameService game) =>
            RunAsync(id, game, async p => await game.MarketListAsync(p, itemId, qty ?? 1, price ?? 0)));
        api.MapPost("/players/{id:guid}/market/buy/{listing:long}", (Guid id, long listing, GameService game) =>
            RunAsync(id, game, async p => await game.MarketBuyAsync(p, listing)));
        api.MapPost("/players/{id:guid}/market/cancel/{listing:long}", (Guid id, long listing, GameService game) =>
            RunAsync(id, game, p => game.MarketCancelAsync(p, listing)));
        // nhận linh thạch đã bán / đồ trả về: giao diện gọi định kỳ
        api.MapPost("/players/{id:guid}/market/claim", (Guid id, GameService game) =>
            RunAsync(id, game, p => game.MarketClaimAsync(p)));

        // Lôi đài (PK bất đồng bộ trên Supabase): bậc thang hạng, thắng thì đổi vị trí với đối thủ
        api.MapGet("/players/{id:guid}/arena", async (Guid id, GameService game) =>
        {
            try { return Results.Ok(await game.ArenaAsync(await game.LoadAsync(id))); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
        });
        api.MapPost("/players/{id:guid}/arena/fight/{target:guid}", async (Guid id, Guid target, GameService game) =>
        {
            await MutationGate.WaitAsync();
            try
            {
                var p = await game.LoadAsync(id);
                var (msg, kind, fight) = await game.ArenaFightAsync(p, target);
                await game.SaveAsync(p);
                return Results.Ok(new ArenaFightResponse(await game.BuildStateAsync(p), msg, kind, fight));
            }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
            finally { MutationGate.Release(); }
        });

        // bấm vào một người trên bảng xếp hạng: trang bị đang mặc + chỉ số
        api.MapGet("/players/{id:guid}/leaderboard/{other:guid}", async (Guid id, Guid other, GameService game, LeaderboardService lb) =>
        {
            try { return await lb.ProfileAsync(await game.LoadAsync(id), other) is { } pf ? Results.Ok(pf) : Results.NotFound(new { error = "Tu sĩ này không còn trên bảng xếp hạng." }); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // đổi đạo hiệu (tên hiện trên bảng xếp hạng), gửi lên bảng ngay
        api.MapPost("/players/{id:guid}/name", (Guid id, string? name, GameService game) =>
            Run(id, game, p => { var r = game.Rename(p, name); game.PushLeaderboard(p); return r; }));

        api.MapPost("/players/{id:guid}/reset", async (Guid id, GameService game) =>
        {
            await MutationGate.WaitAsync();
            try
            {
                var p = await game.LoadAsync(id);
                var fresh = await game.ResetAsync(p);
                return Results.Ok(new ActionResponse(await game.BuildStateAsync(fresh)));
            }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
            finally { MutationGate.Release(); }
        });
    }

    // Như Run nhưng hành động bất đồng bộ (gọi Supabase); lỗi thì không lưu gì
    private static async Task<IResult> RunAsync(Guid id, GameService game, Func<Player, Task<(string? msg, string? kind)>> action)
    {
        await MutationGate.WaitAsync();
        try
        {
            var p = await game.LoadAsync(id);
            var (msg, kind) = await action(p);
            await game.SaveAsync(p);
            return Results.Ok(new ActionResponse(await game.BuildStateAsync(p), msg, kind));
        }
        catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
        catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        finally { MutationGate.Release(); }
    }

    // Nạp người chơi → thực hiện hành động → lưu DB → trả trạng thái mới
    private static Task<IResult> Run(Guid id, GameService game, Func<Player, (string? msg, string? kind)> action) =>
        RunCraft(id, game, p => { var (m, k) = action(p); return (m, k, null); });

    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    // Như Run, kèm kết quả luyện chế (thành phẩm + nguyên liệu) để giao diện hiện thông báo
    private static async Task<IResult> RunCraft(Guid id, GameService game, Func<Player, (string? msg, string? kind, CraftResultDto? craft)> action)
    {
        await MutationGate.WaitAsync();
        try
        {
            var p = await game.LoadAsync(id);
            var (msg, kind, craft) = action(p);
            await game.SaveAsync(p);
            return Results.Ok(new ActionResponse(await game.BuildStateAsync(p), msg, kind, craft));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "Không tìm thấy tu sĩ." });
        }
        catch (GameException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        finally { MutationGate.Release(); }
    }
}
