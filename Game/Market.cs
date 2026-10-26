using System.Net.Http.Json;
using System.Text.Json;

namespace VanDao.Game;

public record MarketListingDto(long Id, string SellerName, string ItemId, int Qty, long Price, DateTime ExpiresAt, bool Mine);
public record MarketMineDto(long Id, string Role, string ItemId, int Qty, long Price, long Payout, string Status, string? OtherName,
    DateTime CreatedAt, DateTime ExpiresAt, DateTime? ClosedAt);
public record MarketDto(bool Enabled, int MinRealm, double TaxPct, int MaxOpen, int MaxDaily, int ListingDays,
    List<MarketListingDto> Listings, List<MarketMineDto> Mine, string? Blocked, string? Error = null, bool CanSellTickets = false, bool UnlimitedListings = false);

/// <summary>
/// Chợ giao dịch giữa người chơi trên Supabase (docs/market/supabase.sql), dùng chung cấu hình "Leaderboard".
/// Supabase giữ đồ ký gửi và đảm bảo mỗi tin chỉ một người mua được; game chỉ trừ / cộng túi sau khi Supabase xác nhận.
/// </summary>
public sealed class MarketService(LeaderboardOptions opt, ILogger<MarketService> log, IHostEnvironment? env = null, HttpClient? client = null)
{
    /// <summary>Môi trường dev (ASPNETCORE_ENVIRONMENT=Development, có sẵn trong launchSettings khi dotnet run): bỏ qua MarketBlocked.</summary>
    public bool Dev => env?.IsDevelopment() == true;

    // Chỉ để hiển thị và kiểm tra sớm phía game; luật thật nằm ở market_rules() trong SQL — sửa thì sửa cả hai.
    public const int MinRealm = 1;          // Trúc Cơ
    public const double TaxPct = 5;
    public const int MaxOpen = 5, MaxDaily = 10, ListingDays = 3;
    public const long MaxPrice = 2_000_000_000;   // linh thạch lưu kiểu int

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly HttpClient _http = client ?? Client(opt);
    public bool Enabled => opt.Enabled;

    private static HttpClient Client(LeaderboardOptions o)
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!o.Enabled) return h;
        h.BaseAddress = new Uri(o.Url.TrimEnd('/') + "/rest/v1/");
        h.DefaultRequestHeaders.Add("apikey", o.AnonKey);
        h.DefaultRequestHeaders.Add("Authorization", $"Bearer {o.AnonKey}");
        h.DefaultRequestHeaders.Add("X-Game-Version", AppVersion.Current);   // Supabase chặn bản cũ (require_version)
        return h;
    }

    public sealed record OpenRow(long Id, Guid SellerId, string SellerName, string ItemId, int Qty, long Price, DateTime ExpiresAt);
    public sealed record BuyRow(string ItemId, int Qty, long Price, string SellerName, long ListingId = 0, Guid BuyerId = default);
    public sealed record ClaimRow(long Id, string Status, string ItemId, int Qty, long Payout, string? BuyerName);
    public sealed record MineRow(long Id, string Role, string ItemId, int Qty, long Price, long Payout, string Status, string? OtherName,
        DateTime CreatedAt, DateTime ExpiresAt, DateTime? ClosedAt);

    private const string OpenCols = "id,seller_id,seller_name,item_id,qty,price,expires_at";

    public async Task<List<OpenRow>> OpenAsync() =>
        await Get<List<OpenRow>>($"market_open?select={OpenCols}&order=created_at.desc&limit=300") ?? [];

    public async Task<OpenRow?> OpenAsync(long id) =>
        (await Get<List<OpenRow>>($"market_open?select={OpenCols}&id=eq.{id}"))?.FirstOrDefault();

    private async Task<T?> Get<T>(string path)
    {
        try { return await _http.GetFromJsonAsync<T>(path, Json); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Market GET {Path}: {Error}", path, ex.Message);
            throw new GameException("Không kết nối được Chợ. Kiểm tra mạng rồi thử lại.");
        }
    }

    /// <summary>Gọi hàm SQL; lỗi do luật chợ (RAISE EXCEPTION) trả về đúng câu tiếng Việt cho người chơi.</summary>
    public async Task<T> Rpc<T>(string fn, object body)
    {
        HttpResponseMessage res;
        // nội dung đệm sẵn (có Content-Length) thay vì gửi chunked
        try { res = await _http.PostAsync($"rpc/{fn}", new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("Market {Fn}: {Error}", fn, ex.Message);
            throw new GameException("Không kết nối được Chợ. Kiểm tra mạng rồi thử lại.");
        }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                string? msg = null;
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("code", out var c))
                    {
                        if (c.GetString() == "P0001") msg = doc.RootElement.GetProperty("message").GetString();
                        else if (fn == "market_buy_v2" && c.GetString() is "PGRST202" or "42883")
                            msg = "Chợ chưa cập nhật cơ chế mua an toàn. Tạm thời không thể mua, vui lòng thử lại sau.";
                    }
                }
                catch (JsonException) { }
                if (msg is null) log.LogWarning("Market {Fn}: {Status} {Body}", fn, (int)res.StatusCode, text);
                throw new GameException(msg ?? "Chợ đang gặp lỗi, thử lại sau.");
            }
            return JsonSerializer.Deserialize<T>(text, Json)!;
        }
    }
}
