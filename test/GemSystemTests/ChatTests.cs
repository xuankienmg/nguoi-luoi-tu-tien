using Microsoft.EntityFrameworkCore;
using VanDao.Data;
using VanDao.Game;

namespace GemSystemTests;

public class ChatRulesTests
{
    [Theory]
    [InlineData("  chào   đạo   hữu  ", "chào đạo hữu")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("x\u0000y​z", "xyz")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void CleanTrimsCollapsesAndStripsControl(string? input, string expected) =>
        Assert.Equal(expected, ChatRules.Clean(input));

    [Fact]
    public void CleanCutsAtMaxLenWithoutSplittingSurrogates()
    {
        Assert.Equal(ChatRules.MaxLen, ChatRules.Clean(new string('a', 300)).Length);
        var emoji = new string('a', ChatRules.MaxLen - 1) + "😀";   // cặp surrogate vắt qua mốc 120
        Assert.Equal(new string('a', ChatRules.MaxLen - 1), ChatRules.Clean(emoji));
    }

    [Fact]
    public void LockReasonBlocksLowRealmAndFlagged()
    {
        Assert.NotNull(ChatRules.LockReason(0, false));
        Assert.Null(ChatRules.LockReason(ChatRules.MinRealm, false));
        Assert.NotNull(ChatRules.LockReason(5, true));
    }

    [Fact]
    public void CooldownCountsUpWholeSeconds()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, ChatRules.CooldownLeft(null, now));
        Assert.Equal(3, ChatRules.CooldownLeft(now, now));
        Assert.Equal(1, ChatRules.CooldownLeft(now.AddSeconds(-2.5), now));
        Assert.Equal(0, ChatRules.CooldownLeft(now.AddSeconds(-3), now));
    }
}
