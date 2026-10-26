using System.Diagnostics;

namespace VanDao.Launcher;

/// <summary>Explicit handoff from a running game; ordinary launcher starts remain manual.</summary>
public sealed record LauncherUpdateRequest(string GamePath, int WaitPid)
{
    public async Task WaitForGameExitAsync(CancellationToken cancellationToken)
    {
        Process previous;
        try { previous = Process.GetProcessById(WaitPid); }
        catch (ArgumentException) { return; } // Game has already exited.
        using (previous) await previous.WaitForExitAsync(cancellationToken);
    }

    public static LauncherUpdateRequest? FromArgs(string[] args)
    {
        string? Value(string key)
        {
            var i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        var path = Value("--game-path");
        return args.Contains("--update-game") && !string.IsNullOrWhiteSpace(path)
            && Path.IsPathFullyQualified(path) && int.TryParse(Value("--wait-pid"), out var pid) && pid > 0
            ? new(Path.GetFullPath(path), pid) : null;
    }
}
