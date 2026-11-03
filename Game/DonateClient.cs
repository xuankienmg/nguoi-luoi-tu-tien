using System.Net.Http.Json;

namespace VanDao.Game;

/// <summary>Bản offline: nhờ backend online "tiêu" code donate (POST /api/donate/redeem) rồi tự cộng vào save trên máy.</summary>
public sealed class DonateClient(HttpClient http)
{
    private sealed record Reply(int Amount);

    public async Task<int> RedeemAsync(string code, Guid player)
    {
        HttpResponseMessage r;
        try { r = await http.PostAsJsonAsync("api/donate/redeem", new { code, player }); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new GameException("Không kết nối được máy chủ để kiểm tra code."); }
        using (r)
        {
            if ((int)r.StatusCode == 429) throw new GameException("Nhập code quá nhanh. Hãy chờ một phút rồi thử lại.");
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) throw new GameException("Code không đúng hoặc đã được dùng.");
            if (!r.IsSuccessStatusCode) throw new GameException("Máy chủ chưa xử lý được code, hãy thử lại sau.");
            Reply? reply;
            try { reply = await r.Content.ReadFromJsonAsync<Reply>(); }
            catch (Exception e) when (e is System.Text.Json.JsonException or NotSupportedException) { throw new GameException("Máy chủ trả lời không hợp lệ. Hãy thử lại sau hoặc báo admin kèm code."); }
            return reply?.Amount is > 0 and var n ? n : throw new GameException("Code không đúng hoặc đã được dùng.");
        }
    }
}
