using System.Text;
using VanDao.Online;
using Xunit;

namespace GemSystemTests;

/// <summary>Phản hồi lưu để phát lại (Idempotency-Key): nén khi ghi, đọc được cả dòng cũ chưa nén.</summary>
public class OnlineRequestBodyTests
{
    [Fact]
    public void PackedResponseRoundTripsExactly()
    {
        var json = "{\"state\":{\"name\":\"Tu sĩ Ánh Trăng\",\"logs\":[" + string.Join(',', Enumerable.Range(0, 2000).Select(i => $"\"Đánh quái {i} · nhận linh thạch\"")) + "]}}";
        var stored = OnlineRequestBody.Pack(Encoding.UTF8.GetBytes(json));
        Assert.StartsWith("gz1:", stored);
        Assert.True(stored.Length < json.Length / 3, $"không nhỏ đi: {stored.Length} / {json.Length}");
        Assert.Equal(json, OnlineRequestBody.Unpack(stored));
    }

    [Fact]
    public void LegacyPlainRowsReplayUnchanged()
    {
        const string old = "{\"message\":\"Đã đột phá\",\"kind\":\"gold\"}";
        Assert.Equal(old, OnlineRequestBody.Unpack(old));
        Assert.Equal("", OnlineRequestBody.Unpack(OnlineRequestBody.Pack([])));
    }
}
