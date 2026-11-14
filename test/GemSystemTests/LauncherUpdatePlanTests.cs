using VanDao.Launcher;

namespace GemSystemTests;

public class LauncherUpdatePlanTests
{
    private static readonly PackAsset Core = new("VanDao_core_v0.14.5.vdpack", "https://x/core", 100);
    private static readonly PackAsset Assets = new("VanDao_assets_0123456789abcdef.vdpack", "https://x/assets", 335);
    private static readonly PackAsset Full = new("full", "https://x/full", 497);
    private const string Hash = "0123456789abcdef";

    [Fact]
    public void Assets_unchanged_downloads_only_core() =>
        Assert.Equal([Core], UpdatePlan.Choose(Core, Assets, Hash, Full, Hash));

    [Fact]
    public void Hash_compare_ignores_case_and_file_whitespace_is_trimmed_by_reader() =>
        Assert.Equal([Core], UpdatePlan.Choose(Core, Assets, Hash, Full, Hash.ToUpperInvariant()));

    [Theory]
    [InlineData(null)]                    // cài từ bản cũ chưa có dấu hash
    [InlineData("ffffffffffffffff")]      // ảnh đã đổi
    public void Missing_or_different_hash_downloads_core_and_assets(string? local) =>
        Assert.Equal([Core, Assets], UpdatePlan.Choose(Core, Assets, Hash, Full, local));

    [Fact]
    public void Release_without_split_packs_falls_back_to_full_zip()
    {
        Assert.Equal([Full], UpdatePlan.Choose(null, null, null, Full, Hash));
        Assert.Equal([Full], UpdatePlan.Choose(Core, null, null, Full, Hash));   // thiếu gói ảnh → không thể chỉ cập nhật core
    }

    [Fact]
    public void No_assets_at_all_gives_empty_plan() =>
        Assert.Empty(UpdatePlan.Choose(null, null, null, null, null));

    [Theory]
    [InlineData("VanDao_assets_0123456789abcdef.vdpack", "0123456789abcdef")]
    [InlineData("VanDao_assets_0123456789ABCDEF.vdpack", "0123456789abcdef")]
    [InlineData("VanDao_assets_0123456789abcdef.zip", null)]    // Launcher đời cũ không được thấy gói này
    [InlineData("VanDao_assets_xyz.vdpack", null)]
    [InlineData("VanDao_assets_0123456789abcde.vdpack", null)]
    [InlineData("VanDao_v0.14.5.zip", null)]
    public void Assets_hash_is_parsed_from_pack_name(string name, string? expected) =>
        Assert.Equal(expected, UpdatePlan.AssetsHashFromName(name));

    [Theory]
    [InlineData("VanDao_core_v0.14.5.vdpack", true)]
    [InlineData("VanDao_core_v0.14.5.zip", false)]
    [InlineData("VanDao_v0.14.5.zip", false)]
    public void Core_pack_name_is_recognised(string name, bool expected) =>
        Assert.Equal(expected, UpdatePlan.IsCorePack(name));

    [Fact]
    public void Local_hash_is_read_from_marker_and_missing_marker_is_null()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vd_plan_" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(UpdatePlan.ReadLocalAssetsHash(dir));
            var marker = Path.Combine(dir, "wwwroot", "img", "assets-hash.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, " 0123456789ABCDEF\r\n");
            Assert.Equal(Hash, UpdatePlan.ReadLocalAssetsHash(dir));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
