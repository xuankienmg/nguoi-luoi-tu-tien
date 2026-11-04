using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Desktop;
using VanDao.Game;
using VanDao.Online;

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
#if ONLINE_HEADLESS
        var webOnly = true;
        var playOnline = false;
#else
        var webOnly = args.Contains("--web");
        var playOnline = !webOnly && WindowSettings.Load().PlayOnline;   // người chơi chọn "Chơi online" trong menu
#endif
        var app = BuildServer(args.Where(a => a != "--web").ToArray(), webOnly, playOnline);
        if (app.Services.GetRequiredService<OnlineOptions>().IsServer) webOnly = true;

        if (webOnly)
        {
            app.Run();
            return;
        }

#if !ONLINE_HEADLESS
        app.StartAsync().GetAwaiter().GetResult();
        var url = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameWindow(url, app.Services.GetRequiredService<OnlineOptions>()));

        app.StopAsync().GetAwaiter().GetResult();
#endif
    }

    private static WebApplication BuildServer(string[] args, bool webOnly, bool playOnline)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            // để vandao.db và wwwroot luôn nằm cạnh file chạy, dù khởi động từ đâu
            ContentRootPath = AppContext.BaseDirectory,
        });
        var online = builder.Configuration.GetSection("Online").Get<OnlineOptions>() ?? new();
        // cấu hình Online:Mode đặt tay (dev, máy chủ) luôn thắng lựa chọn trong menu.
        // Development bỏ qua lựa chọn "Chơi online": chạy offline (vandao_dev.db, có admin), muốn thử online thì đặt Online:Mode.
        if (playOnline && !builder.Environment.IsDevelopment() && online.Mode.Equals("Offline", StringComparison.OrdinalIgnoreCase) && online.DefaultBackendUrl.Length > 0)
        {
            online.Mode = "Client";
            online.BackendUrl = online.DefaultBackendUrl;
        }
        online.Validate(builder.Configuration);
        builder.Services.AddSingleton(online);
        if (online.IsClient) {
            builder.WebHost.UseUrls($"http://127.0.0.1:{online.LocalPort}");
            var clientApp = builder.Build();
            Player.AdminEnabled = false;
            OnlineGateway.ConfigureClient(clientApp, online);
            return clientApp;
        }
        if (online.IsServer) {
            builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 8192);
            builder.Services.AddSingleton<SupabaseIdentity>();
            builder.Services.AddHostedService<OnlineRequestCleanup>();
            OnlineGateway.AddAuthRateLimit(builder.Services);
            if (online.TrustProxy) builder.Services.Configure<ForwardedHeadersOptions>(o => {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownNetworks.Clear(); o.KnownProxies.Clear();   // the hosting platform's proxy address is not fixed
            });
            builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
            if (string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://127.0.0.1:5188");
        }

        // Chế độ cửa sổ: chỉ nghe trên máy này, cổng ngẫu nhiên để không đụng app khác
        if (!webOnly && !online.IsServer) builder.WebHost.UseUrls("http://127.0.0.1:0");

        // DB nằm trong %LocalAppData%\VanDao để không mất khi build lại hay xóa bin/
        // chạy Development dùng file riêng vandao_dev.db, không đụng save của bản phát hành.
        // Server online không có save local: không tạo thư mục (container chạy user không có quyền ghi).
        var dbFile = builder.Environment.IsDevelopment() ? "vandao_dev.db" : "vandao.db";
        builder.Services.AddDbContext<GameDbContext>(o => {
            if (online.IsServer) o.UseNpgsql(builder.Configuration.GetConnectionString("Game"), OnlineSchema.Npgsql);
            else o.UseSqlite(builder.Configuration.GetConnectionString("Game") ?? $"Data Source={Path.Combine(AppPaths.DataDir, dbFile)}");
        });
        builder.Services.AddScoped<GameService>();
        // bảng xếp hạng online (Supabase): cấu hình mục "Leaderboard" trong appsettings.json, bỏ trống = tắt
        builder.Services.AddSingleton(online.IsServer ? new LeaderboardOptions() : builder.Configuration.GetSection("Leaderboard").Get<LeaderboardOptions>() ?? new LeaderboardOptions());
        builder.Services.AddSingleton<LeaderboardService>();
        if (online.IsServer) {   // online Server: bảng xếp hạng nằm trong schema game, ghi cùng transaction nhân vật
            builder.Services.AddSingleton<ServerLeaderboardCache>();
            builder.Services.AddSingleton<ServerLogCache>();   // nhật ký: chỉ đọc lại khi có dòng mới
            // cây nhân vật trong RAM: đồng bộ gần như chỉ còn ghi (Online:PlayerCache=false để tắt)
            if (online.PlayerCache) builder.Services.AddSingleton(new PlayerCache());
            builder.Services.AddScoped<ServerLeaderboard>();
            builder.Services.AddScoped<ServerMarket>();   // Chợ: tin rao trong schema game, cùng transaction hành trang
            builder.Services.AddScoped<ServerArena>();    // Lôi đài: backend tự đánh trận và đổi hạng trong một transaction
            builder.Services.AddScoped<DonateStore>();     // code donate: bảng game.DonateCodes
        }
        // Chế độ Client đã return sớm ở trên (proxy thẳng /api/... về backend), nên DonateClient chỉ đăng ký khi chính process này chạy logic game (offline)
        if (!online.IsServer && online.DefaultBackendUrl.Length > 0)   // bản offline nhập code donate qua backend online
            builder.Services.AddSingleton(new DonateClient(new HttpClient { BaseAddress = new Uri(online.DefaultBackendUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(15) }));
        builder.Services.AddSingleton(sp => new VersionService(sp.GetRequiredService<LeaderboardOptions>(), sp.GetRequiredService<ILogger<VersionService>>(),
            sp.GetRequiredService<IHostEnvironment>()) { UseOwnClock = online.IsServer });   // bắt cập nhật: đọc app_config trên Supabase · máy chủ online: giờ sự kiện theo đồng hồ máy chủ
        builder.Services.AddSingleton<MarketService>();   // Chợ giao dịch: dùng chung project Supabase
        builder.Services.AddSingleton<ArenaService>();    // Lôi đài PK: dùng chung project Supabase

        var app = builder.Build();
        Player.AdminEnabled = !online.IsServer && app.Environment.IsDevelopment();

        if (!online.IsServer) using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Database.EnsureCreated();
            SchemaUpgrade.Apply(db);
            SaveGuard.Check(db, app.Environment.IsDevelopment());   // chống quay về bản cũ: file dữ liệu nhớ bản cao nhất đã mở nó
        }
        if (online.IsServer) {
            if (online.TrustProxy) app.UseForwardedHeaders();
            app.UseRateLimiter();
            app.UseExceptionHandler(errorApp => errorApp.Run(async ctx => {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "Máy chủ online tạm thời không khả dụng. Không dùng save local thay thế." });
            }));
            app.UseMiddleware<OnlineBoundary>();
        }
        OnlineGateway.MapSession(app, online);

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
            GameData.Brews, GameData.HerbDrops, GameData.Skins, GameData.SpinWeights, GameData.SpinTicketPrice, EquipmentEffects.Sets, PetData.Definitions, PetData.PetSpinWeights, GameData.PetSpinTicketPrice, GemData.UpgradeChances));

        api.MapPost("/players", async (CreatePlayerRequest? req, GameService game) =>
        {
            try {
                var p = await game.CreateAsync(req?.Name);
                return Results.Ok(await game.BuildStateAsync(p));
            } catch (GameException e) { return Results.BadRequest(new { error = e.Message }); }
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
        api.MapPost("/players/{id:guid}/equip/{itemId}", (Guid id, string itemId, string? slot, int? copy, GameService game) =>
            Run(id, game, p => game.Equip(p, itemId, slot, copy ?? 0)));

        api.MapPost("/players/{id:guid}/use/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.UseItem(p, itemId, n ?? 1)));

        api.MapPost("/players/{id:guid}/drop/{itemId}", (Guid id, string itemId, int? n, int? copy, GameService game) =>
            Run(id, game, p => game.Drop(p, itemId, n ?? 1, copy ?? 0)));

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

        api.MapPost("/players/{id:guid}/starter", (Guid id, string element, string egg, string? name, GameService game) =>
            Run(id, game, p => { var r = game.ChooseStarter(p, element, egg, name); if (name is not null) game.PushLeaderboard(p); return r; }));

        api.MapPost("/players/{id:guid}/skillslot/{slot:int}", (Guid id, int slot, string? skill, GameService game) =>
            Run(id, game, p => game.SetSkillSlot(p, slot, skill)));

        api.MapPost("/players/{id:guid}/sell/{itemId}", (Guid id, string itemId, int? n, int? copy, GameService game) =>
            Run(id, game, p => game.Sell(p, itemId, n ?? 1, copy ?? 0)));

        api.MapPost("/players/{id:guid}/sell-bulk", (Guid id, int max, bool? gear, string? items, bool? pick, GameService game) =>
            Run(id, game, p => game.SellBulk(p, max, gear ?? false, items, pick ?? false)));

        api.MapPost("/players/{id:guid}/unequip/{slot}", (Guid id, string slot, GameService game) =>
            Run(id, game, p => game.Unequip(p, slot)));

        api.MapPost("/players/{id:guid}/advance", (Guid id, GameService game) =>
            Run(id, game, p => game.Advance(p)));

        // luyện khí: ghép 9 trang bị cùng phẩm → 1 phẩm cao hơn (items = danh sách id, lặp lại khi lấy nhiều món cùng id)
        api.MapPost("/players/{id:guid}/fuse", (Guid id, string? items, GameService game) =>
            RunCraft(id, game, p => game.Fuse(p, items)));
        api.MapPost("/players/{id:guid}/fuse/quick", (Guid id, GameService game) =>
            RunCraft(id, game, p => game.QuickFuseGear(p)));

        // code quà tặng (nhập ở menu Cài đặt)
        api.MapPost("/players/{id:guid}/redeem", (Guid id, string? code, GameService game) =>
            RunAsync(id, game, async p => await game.RedeemCodeAsync(p, code)));

        // Tài Xỉu: đổi linh thạch lấy vé (n = số vé, 0 = tối đa) · quay vòng trang bị 10 phẩm (n = số lượt, tối đa 10)
        api.MapPost("/players/{id:guid}/spin/buy", (Guid id, int? n, string? mode, GameService game) =>
            Run(id, game, p => game.BuySpinTicket(p, n ?? 1, mode == "pet")));
        api.MapPost("/players/{id:guid}/spin", (Guid id, int? n, string? mode, GameService game) =>
            RunCraft(id, game, p => mode == "pet" ? game.PetSpin(p, n ?? 1) : game.Spin(p, n ?? 1)));

        // sự kiện quay tích lũy Thiên Cơ: i = chỉ số mốc, bỏ trống / -1 = nhận mọi mốc đã đạt
        api.MapPost("/players/{id:guid}/event/claim", (Guid id, int? i, GameService game) => Run(id, game, p => game.ClaimEvent(p, i ?? -1)));
        // kỳ duyên Thiên Thư: c = 0 (lối thứ nhất) / 1 (lối thứ hai), mỗi khung giờ một lần
        api.MapPost("/players/{id:guid}/kyduyen", (Guid id, int? c, GameService game) => Run(id, game, p => game.ResolveKyDuyen(p, c ?? -1)));
        api.MapPost("/players/{id:guid}/pet/{petId:guid}/feed", (Guid id, Guid petId, int? n, GameService game) => Run(id, game, p => game.Feed(p, petId, n ?? 1)));
        // quà đăng nhập hằng ngày: nhận ô hôm nay trong vòng 7 ngày
        api.MapPost("/players/{id:guid}/login/claim", (Guid id, GameService game) => Run(id, game, p => game.ClaimDailyLogin(p)));
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
        api.MapPost("/players/{id:guid}/petgear/quick-fuse", (Guid id, GameService game) => RunCraft(id, game, p => game.QuickFusePetGear(p)));
        api.MapPost("/players/{id:guid}/hatch/now", (Guid id, int? slot, GameService game) => Run(id, game, p => game.HatchNow(p, slot ?? 0)));
        api.MapPost("/players/{id:guid}/hatch/collect", (Guid id, int? slot, GameService game) => Run(id, game, p => game.CollectHatch(p, slot ?? 0)));
        api.MapPost("/players/{id:guid}/hatch/{eggId}", (Guid id, string eggId, int? slot, GameService game) => Run(id, game, p => game.Hatch(p, eggId, slot ?? 0)));

        // đan dược: hợp thành 9 → 1 (n = số lần, 0 = tối đa)
        api.MapPost("/players/{id:guid}/synth/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.Synthesize(p, itemId, n ?? 1)));

        // luyện đan theo đan phương (linh dược + đan + linh thạch → 1 viên), n = số lần, 0 = tối đa
        api.MapPost("/players/{id:guid}/brew/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            RunCraft(id, game, p => game.Brew(p, itemId, n ?? 1)));

        // Linh Tinh: khảm nạm vào ô trang bị · tháo khảm · mở lỗ khảm
        api.MapPost("/players/{id:guid}/gem/socket", (Guid id, string slot, int socketIndex, string itemId, GameService game) =>
            Run(id, game, p => game.SocketGem(p, slot, socketIndex, itemId)));
        api.MapPost("/players/{id:guid}/gem/unsocket", (Guid id, string slot, int socketIndex, GameService game) =>
            Run(id, game, p => game.UnsocketGem(p, slot, socketIndex)));
        api.MapPost("/players/{id:guid}/gem/unlock", (Guid id, string slot, GameService game) =>
            Run(id, game, p => game.UnlockSocket(p, slot)));
        api.MapPost("/players/{id:guid}/gem/craft/{recipeId}", (Guid id, string recipeId, int? n, GameService game) =>
            RunCraft(id, game, p => game.CraftGemRecipe(p, recipeId, n ?? 1)));
        api.MapPost("/players/{id:guid}/gem/fuse-free", (Guid id, string? items, int? n, GameService game) =>
            RunCraft(id, game, p => game.CraftGemFromItems(p, items, n ?? 1)));
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

        // code donate (chỉ tài khoản trong Online:AdminUsers; OnlineBoundary đã chặn 403 mọi /admin/ của tài khoản khác)
        api.MapPost("/players/{id:guid}/admin/donate-codes", async (Guid id, long vnd, string? note, GameDbContext db, IServiceProvider sp) =>
        {
            if (!Player.AdminEnabled || sp.GetService<DonateStore>() is not { } store || db.RequestOwnerId is not { } admin)
                return Results.Json(new { error = "Chỉ tài khoản admin online mới tạo được code donate." }, statusCode: StatusCodes.Status403Forbidden);
            try { return Results.Ok(await store.CreateAsync(vnd, note, admin)); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        api.MapGet("/players/{id:guid}/admin/donate-codes", async (Guid id, IServiceProvider sp) =>
            Player.AdminEnabled && sp.GetService<DonateStore>() is { } store
                ? Results.Ok(await store.ListAsync())
                : Results.Json(new { error = "Chỉ tài khoản admin online mới xem được code donate." }, statusCode: StatusCodes.Status403Forbidden));

        // bản offline đổi code donate qua backend: không đăng nhập, không transaction của OnlineBoundary (ngoài /api/players)
        if (app.Services.GetRequiredService<OnlineOptions>().IsServer)
            api.MapPost("/donate/redeem", async (DonateRedeemRequest body, DonateStore store) =>
                DonateStore.IsDonateCode(body.Code) && await store.ClaimAsync(body.Code, body.Player, null, offline: true) is { } amount
                    ? Results.Ok(new { amount })
                    : Results.NotFound(new { error = "Code không đúng hoặc đã được dùng." }))
               .RequireRateLimiting(OnlineGateway.DonateLimit);

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
        // Tạp Hóa bà Dĩm trong Chợ (GameData.Shop): mua bằng linh thạch, không qua Chợ người chơi
        api.MapPost("/players/{id:guid}/shop/{shopId}/{itemId}", (Guid id, string shopId, string itemId, int? n, GameService game) =>
            Run(id, game, p => game.ShopBuy(p, shopId, itemId, n ?? 1)));
        api.MapPost("/players/{id:guid}/vip/{itemId}", (Guid id, string itemId, int? n, GameService game) =>
            Run(id, game, p => game.VipBuy(p, itemId, n ?? 1)));
        api.MapPost("/players/{id:guid}/speed2", (Guid id, bool on, GameService game) =>
            Run(id, game, p => game.SetSpeed2(p, on)));
        api.MapPost("/players/{id:guid}/market/buy/{listing:long}", (Guid id, long listing, GameService game) =>
            RunAsync(id, game, async p => await game.MarketBuyAsync(p, listing)));
        api.MapPost("/players/{id:guid}/market/cancel/{listing:long}", (Guid id, long listing, GameService game) =>
            RunAsync(id, game, p => game.MarketCancelAsync(p, listing)));
        // nhận linh thạch đã bán / đồ trả về: giao diện gọi định kỳ
        // online Server: hỏi trước bằng một EXISTS, không có gì để nhận thì khỏi nạp / lưu nhân vật (client chỉ dùng phản hồi có message)
        api.MapPost("/players/{id:guid}/market/claim", async (Guid id, GameService game) =>
            await game.MarketClaimPendingAsync(id) ? await RunAsync(id, game, p => game.MarketClaimAsync(p)) : Results.Ok(new { }));

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
                var (p, (msg, kind, fight)) = await game.ActAsync(id, p => game.ArenaFightAsync(p, target));
                return Results.Ok(new ArenaFightResponse(await game.BuildStateAsync(p), msg, kind, fight));
            }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "Không tìm thấy tu sĩ." }); }
            catch (GameException ex) { return Results.BadRequest(new { error = ex.Message }); }
            finally { MutationGate.Release(); }
        });

        // bấm vào một người trên bảng xếp hạng: trang bị đang mặc + chỉ số
        api.MapGet("/players/{id:guid}/leaderboard/{other:guid}", async (Guid id, Guid other, GameService game) =>
        {
            try { return await game.LeaderboardProfileAsync(await game.LoadAsync(id), other) is { } pf ? Results.Ok(pf) : Results.NotFound(new { error = "Tu sĩ này không còn trên bảng xếp hạng." }); }
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
            var (p, (msg, kind)) = await game.ActAsync(id, action);
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
            var (p, (msg, kind, craft)) = await game.ActAsync(id, p => Task.FromResult(action(p)));
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
