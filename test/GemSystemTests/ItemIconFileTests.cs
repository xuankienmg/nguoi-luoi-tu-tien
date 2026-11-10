using VanDao.Game;

namespace GemSystemTests;

/// <summary>Mọi ảnh vật phẩm / trang phục mà server gửi cho client phải có file trong wwwroot
/// (11/2026: commit dọn ảnh xóa nhầm dt_chau.png, chien_y_dan.png → bản 0.13.0 hiện ô vỡ ảnh trong Hành trang).</summary>
public class ItemIconFileTests
{
    private static string? Root()
    {
        foreach (var d in new[] { Path.Combine(AppContext.BaseDirectory, "wwwroot"), Path.Combine("..", "..", "..", "..", "..", "wwwroot") })
            if (Directory.Exists(Path.Combine(d, "img"))) return d;
        return null;
    }

    [Fact]
    public void EveryItemIconAndSkinImageExists()
    {
        var root = Root();
        Assert.NotNull(root);
        // AllItems = cả trang bị sinh theo phẩm/cấp (icon ghép img/<base>.png), giống danh sách gửi client
        var refs = GameData.AllItems.Where(i => !string.IsNullOrEmpty(i.Icon)).Select(i => (i.Id, Path: i.Icon!))
            .Concat(GameData.Skins.SelectMany(s => new[] { s.Hero, s.HeroStand, s.HeroMed, s.HeroCast }
                .Where(p => !string.IsNullOrEmpty(p)).Select(p => (Id: "skin:" + s.Id, Path: p))));
        var missing = refs.Where(r => !File.Exists(Path.Combine(root!, r.Path.Split('?')[0])))
            .Select(r => $"{r.Id} → {r.Path}").Distinct().ToList();
        Assert.True(missing.Count == 0, "Thiếu ảnh:\n" + string.Join("\n", missing));
    }
}
