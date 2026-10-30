using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanDao.Desktop;

/// <summary>Vị trí neo của dải game so với thanh taskbar (giống "Screen Layout" của Task Bar Hero).</summary>
public enum StripDock { Auto, BottomLeft, BottomCenter, BottomRight, TopLeft, TopCenter, TopRight, Manual }

/// <summary>Cài đặt cửa sổ, lưu ở %LocalAppData%\VanDao\window.json.</summary>
public class WindowSettings
{
    public StripDock Dock { get; set; } = StripDock.Auto;

    /// <summary>Toạ độ khi người dùng tự kéo (Dock = Manual).</summary>
    public int? X { get; set; }
    public int? Y { get; set; }

    /// <summary>Màn hình đặt dải game (DeviceName). null = màn hình chính.</summary>
    public string? Screen { get; set; }

    /// <summary>Tỉ lệ kích thước dải game, 1 = 420×300 (bằng 150% của bản cũ). Tuỳ chọn: 75% · 100% · 125% · 150% · ×2 · ×3 · ×4.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Phiên bản thang kích thước: null = bản cũ (1 = 280×200) → đưa về 100% mới khi nạp.</summary>
    public int? ScaleVersion { get; set; }

    /// <summary>Luôn hiển thị trên các cửa sổ khác.</summary>
    public bool Pinned { get; set; } = true;

    public double Opacity { get; set; } = 1.0;

    /// <summary>Ngôn ngữ giao diện: "vi" (mặc định) hoặc "en".</summary>
    public string Language { get; set; } = "vi";

    /// <summary>Chế độ chơi: "Offline" (save trên máy, mặc định) hoặc "Online" (tài khoản, lưu trên máy chủ). Đổi có hiệu lực khi mở lại game.</summary>
    public string PlayMode { get; set; } = "Offline";
    [JsonIgnore] public bool PlayOnline => PlayMode == "Online";

    [JsonIgnore] public bool En => Language == "en";

    /// <summary>Chọn chữ theo ngôn ngữ đang dùng.</summary>
    public string L(string vi, string en) => En ? en : vi;

    /// <summary>Kích thước riêng cho tab Hành trang (rộng hơn để chứa nhân vật + túi).</summary>
    public int? InvW { get; set; }
    public int? InvH { get; set; }

    private static string FilePath => Path.Combine(AppPaths.DataDir, "window.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FilePath), Json) ?? new();
                if (s.ScaleVersion is null) { s.Scale = 1.0; s.ScaleVersion = 2; }   // thang cũ: 150% cũ thành 100% mới
                return s;
            }
        }
        catch { /* file hỏng hoặc từ bản cũ thì dùng mặc định */ }
        return new() { ScaleVersion = 2 };
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); }
        catch { /* không lưu được cũng không chặn game */ }
    }
}
