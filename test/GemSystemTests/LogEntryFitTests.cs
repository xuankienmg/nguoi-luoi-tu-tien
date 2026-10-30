using VanDao.Data;

namespace GemSystemTests;

public class LogEntryFitTests
{
    [Fact]
    public void ShortLogIsKept()
    {
        Assert.Equal("Thám hiểm ải 1-1", LogEntry.Fit("Thám hiểm ải 1-1"));
        var exact = new string('a', LogEntry.TextMax);
        Assert.Same(exact, LogEntry.Fit(exact));
    }

    [Fact]
    public void LongLogFitsTheOnlineColumn()
    {
        // PostgreSQL online rejects the whole save when a log line passes the varchar limit
        var fitted = LogEntry.Fit(string.Concat(Enumerable.Repeat("Nhặt Linh Thảo · ", 60)));
        Assert.Equal(LogEntry.TextMax, fitted.Length);
        Assert.EndsWith("…", fitted);
    }

    [Fact]
    public void CutNeverSplitsASurrogatePair()
    {
        var text = new string('a', LogEntry.TextMax - 2) + "😀" + "tail";
        var fitted = LogEntry.Fit(text);
        Assert.True(fitted.Length <= LogEntry.TextMax);
        Assert.False(char.IsHighSurrogate(fitted[^2]));
        Assert.EndsWith("…", fitted);
    }
}
