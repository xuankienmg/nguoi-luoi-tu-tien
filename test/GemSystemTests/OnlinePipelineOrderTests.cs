namespace GemSystemTests;

/// <summary>OnlineBoundary đệm body phản hồi rồi lưu vào OnlineRequests.Response (cột text Postgres).
/// Nén gzip/brotli phải bọc NGOÀI boundary; đặt sau thì body nhị phân có byte 0x00 → Postgres 22021, mọi hành động online 503 (bản 0.13.0).</summary>
public class OnlinePipelineOrderTests
{
    // test/GemSystemTests/<file>.cs → gốc repo
    private static string Program([System.Runtime.CompilerServices.CallerFilePath] string self = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(self)!, "..", "..", "Program.cs"));

    [Fact]
    public void ResponseCompressionWrapsOnlineBoundary()
    {
        var src = Program();
        int compress = src.IndexOf("app.UseResponseCompression()", StringComparison.Ordinal), boundary = src.IndexOf("app.UseMiddleware<OnlineBoundary>()", StringComparison.Ordinal);
        Assert.True(compress >= 0 && boundary >= 0, "Không tìm thấy UseResponseCompression / UseMiddleware<OnlineBoundary> trong Program.cs");
        Assert.True(compress < boundary, "app.UseResponseCompression() phải đứng trước app.UseMiddleware<OnlineBoundary>()");
        Assert.Equal(compress, src.LastIndexOf("app.UseResponseCompression()", StringComparison.Ordinal));   // chỉ một chỗ
    }
}
