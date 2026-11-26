using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VanDao.Data;

namespace VanDao.Online;

/// <summary>Số liệu DB của một request online (một GameDbContext): số lệnh SQL, tổng thời gian chờ DB,
/// số lần mở kết nối và thời gian mở, lệnh transaction (COMMIT / SAVEPOINT / ROLLBACK TO). OnlineBoundary ghi vào header
/// Server-Timing để tìm chỗ chậm (11/2026).</summary>
public sealed class DbTiming
{
    public int Commands, Opens, TxOps;
    public double CommandMs, OpenMs, TxMs;
    internal long TxStarted;   // Stopwatch timestamp lúc bắt đầu tạo / quay về savepoint (EF không báo thời gian của hai việc này)

    /// <summary>Số lượt đi về DB đã dùng: lệnh SQL + lệnh transaction (BEGIN đi kèm lệnh đầu tiên nên không tính riêng).</summary>
    public int RoundTrips => Commands + TxOps;

    public string Header(params (string Name, double Ms)[] steps) => Header(steps.Select(s => (s.Name, s.Ms, -1)).ToArray());

    /// <summary>Trips ≥ 0: ghi số lượt đi về DB của bước vào desc.</summary>
    public string Header(params (string Name, double Ms, int Trips)[] steps)
    {
        static string F(double ms) => ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var parts = steps.Select(s => s.Trips < 0 ? $"{s.Name};dur={F(s.Ms)}" : $"{s.Name};dur={F(s.Ms)};desc=\"{s.Trips} rt\"").ToList();
        parts.Add($"db;dur={F(CommandMs)};desc=\"{Commands} sql\"");
        if (TxOps > 0) parts.Add($"tx;dur={F(TxMs)};desc=\"{TxOps} tx\"");
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

/// <summary>Đếm COMMIT / ROLLBACK / SAVEPOINT / ROLLBACK TO SAVEPOINT: mỗi lệnh là một lượt đi về DB mà interceptor lệnh không thấy.</summary>
public sealed class DbTimingTransactionInterceptor : DbTransactionInterceptor
{
    static DbTiming? T(TransactionEventData e) => (e.Context as GameDbContext)?.Timing;
    static void End(TransactionEndEventData e) { if (T(e) is { } t) { t.TxOps++; t.TxMs += e.Duration.TotalMilliseconds; } }
    static void Start(TransactionEventData e) { if (T(e) is { } t) t.TxStarted = Stopwatch.GetTimestamp(); }
    static void Done(TransactionEventData e) {
        if (T(e) is { } t) { t.TxOps++; if (t.TxStarted != 0) t.TxMs += Stopwatch.GetElapsedTime(t.TxStarted).TotalMilliseconds; t.TxStarted = 0; }
    }
    public override void TransactionCommitted(DbTransaction tx, TransactionEndEventData e) => End(e);
    public override Task TransactionCommittedAsync(DbTransaction tx, TransactionEndEventData e, CancellationToken ct = default) { End(e); return Task.CompletedTask; }
    public override void TransactionRolledBack(DbTransaction tx, TransactionEndEventData e) => End(e);
    public override Task TransactionRolledBackAsync(DbTransaction tx, TransactionEndEventData e, CancellationToken ct = default) { End(e); return Task.CompletedTask; }
    public override InterceptionResult CreatingSavepoint(DbTransaction tx, TransactionEventData e, InterceptionResult r) { Start(e); return r; }
    public override ValueTask<InterceptionResult> CreatingSavepointAsync(DbTransaction tx, TransactionEventData e, InterceptionResult r, CancellationToken ct = default) { Start(e); return ValueTask.FromResult(r); }
    public override void CreatedSavepoint(DbTransaction tx, TransactionEventData e) => Done(e);
    public override Task CreatedSavepointAsync(DbTransaction tx, TransactionEventData e, CancellationToken ct = default) { Done(e); return Task.CompletedTask; }
    public override InterceptionResult RollingBackToSavepoint(DbTransaction tx, TransactionEventData e, InterceptionResult r) { Start(e); return r; }
    public override ValueTask<InterceptionResult> RollingBackToSavepointAsync(DbTransaction tx, TransactionEventData e, InterceptionResult r, CancellationToken ct = default) { Start(e); return ValueTask.FromResult(r); }
    public override void RolledBackToSavepoint(DbTransaction tx, TransactionEventData e) => Done(e);
    public override Task RolledBackToSavepointAsync(DbTransaction tx, TransactionEventData e, CancellationToken ct = default) { Done(e); return Task.CompletedTask; }
}

/// <summary>Đồng hồ các bước trong OnlineBoundary; có DbTiming thì ghi thêm số lượt đi về DB của từng bước.</summary>
public sealed class StepClock(DbTiming? timing = null)
{
    readonly Stopwatch _sw = Stopwatch.StartNew();
    double _last;
    int _lastTrips;
    public readonly List<(string Name, double Ms, int Trips)> Steps = new();
    public void Mark(string name) {
        var now = _sw.Elapsed.TotalMilliseconds; var trips = timing?.RoundTrips ?? 0;
        Steps.Add((name, now - _last, timing is null ? -1 : trips - _lastTrips)); _last = now; _lastTrips = trips;
    }
    public double Total => _sw.Elapsed.TotalMilliseconds;
}
