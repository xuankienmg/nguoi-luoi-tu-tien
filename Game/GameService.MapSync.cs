using System.Security.Cryptography;
using System.Text.Json;
using VanDao.Data;

namespace VanDao.Game;

/// <summary>
/// Đồng bộ nhẹ (11/2026): danh sách ải của bản đồ (~55 KB thô, ~45% trạng thái sau nén) hầu như không đổi giữa các lần
/// đồng bộ khi treo máy. Mỗi trạng thái gửi kèm mã nội dung (MapDto.AreasKey); client gửi lại mã đang giữ qua header
/// X-Map-Key, trùng thì Areas để trống (AreasOmitted) và client dùng lại bản cũ. Client cũ không gửi header → nhận đủ như trước.
/// </summary>
public partial class GameService
{
    public const string MapKeyHeader = "X-Map-Key";
    /// <summary>Mã danh sách ải client đang giữ (header X-Map-Key của request hiện tại), rỗng = cần gửi đủ.</summary>
    public string ClientMapKey { get; set; } = "";

    private static readonly JsonSerializerOptions MapKeyJson = new(JsonSerializerDefaults.Web);

    public static string AreasKey(List<AreaDto> areas) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(areas, MapKeyJson)), 0, 12);

    public MapDto MapStateFor(Player p)
    {
        var map = MapState(p);
        var key = AreasKey(map.Areas);
        return ClientMapKey == key ? map with { Areas = [], AreasKey = key, AreasOmitted = true } : map with { AreasKey = key };
    }
}
