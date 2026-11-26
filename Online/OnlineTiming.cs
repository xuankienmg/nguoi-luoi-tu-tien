using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Số liệu DB của một request online (một GameDbContext): số lệnh SQL, tổng thời gian chờ DB,
/// số lần mở kết nối và thời gian mở. OnlineBoundary ghi vào header Server-Timing để tìm chỗ chậm (11/2026).</summary>
public sealed class DbTiming
{
    public int Commands, Opens;
    public double CommandMs, OpenMs;

    public string Header(params (string Name, double Ms)[] steps)
    {
        static string F(double ms) => ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var parts = steps.Select(s => $"{s.Name};dur={F(s.Ms)}").ToList();
        parts.Add($"db;dur={F(CommandMs)};desc=\"{Commands} sql\"");
        parts.Add($"conn;dur={F(OpenMs)};desc=\"{Opens} open\"");
        return string.Join(", ", parts);
    }
}

/// <summary>Cộng thời gian mọi lệnh SQL vào GameDbContext.Timing.</summary>
public sealed class DbTimingCommandInterceptor : DbCommandInterceptor
{
    static void Add(CommandEventData e, TimeSpan d) {
        if (e.Context is GameDbContext { Timing: { } t }) { t.Commands++; t.CommandMs += d.TotalMilliseconds; }
    }
    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { Add(e, e.Duration); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default) { Add(e, e.Duration); return ValueTask.FromResult(r); }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Add(e, e.Duration); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default) { Add(e, e.Duration); return ValueTask.FromResult(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Add(e, e.Duration); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default) { Add(e, e.Duration); return ValueTask.FromResult(r); }
}

/// <summary>Đo thời gian mở kết nối: lớn (hàng trăm ms) = kết nối không được tái dùng từ pool hoặc DB ở xa.</summary>
public sealed class DbTimingConnectionInterceptor : DbConnectionInterceptor
{
    static void Add(ConnectionEndEventData e) {
        if (e.Context is GameDbContext { Timing: { } t }) { t.Opens++; t.OpenMs += e.Duration.TotalMilliseconds; }
    }
    public override void ConnectionOpened(DbConnection c, ConnectionEndEventData e) => Add(e);
    public override Task ConnectionOpenedAsync(DbConnection c, ConnectionEndEventData e, CancellationToken ct = default) { Add(e); return Task.CompletedTask; }
}

/// <summary>Đồng hồ các bước trong OnlineBoundary.</summary>
public sealed class StepClock
{
    readonly Stopwatch _sw = Stopwatch.StartNew();
    double _last;
    public readonly List<(string Name, double Ms)> Steps = new();
    public void Mark(string name) { var now = _sw.Elapsed.TotalMilliseconds; Steps.Add((name, now - _last)); _last = now; }
    public double Total => _sw.Elapsed.TotalMilliseconds;
}
