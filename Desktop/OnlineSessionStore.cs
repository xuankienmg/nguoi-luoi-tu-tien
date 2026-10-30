using System.Security.Cryptography;
using System.Text;

namespace VanDao.Desktop;

/// <summary>Keeps the online session between launches, encrypted for the current Windows user (DPAPI).
/// The Launcher links this file: it signs in and hands the session to the game through these files.</summary>
public sealed class OnlineSessionStore(string path)
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VanDao");

    /// <summary>"Ghi nhớ đăng nhập": kept until sign-out or until Auth rejects the refresh token.</summary>
    public static OnlineSessionStore Default { get; } = new(Path.Combine(Dir, "online-session.bin"));

    /// <summary>Launcher sign-in without "remember": the game reads it once and deletes it.</summary>
    public static OnlineSessionStore Handoff { get; } = new(Path.Combine(Dir, "online-session-once.bin"));

    public string? Load()
    {
        try { return File.Exists(path) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)) : null; }
        catch (CryptographicException) { Clear(); return null; }   // another Windows user or a damaged file: sign in again
        catch (IOException) { return null; }
    }

    /// <summary>Load then delete, so a one-time session is never reused by a later launch.</summary>
    public string? Take()
    {
        var json = Load();
        Clear();
        return json;
    }

    public void Save(string json)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));
        }
        catch (IOException) { }   // failing to remember the login must not block the game
    }

    public void Clear()
    {
        try { File.Delete(path); } catch (IOException) { }
    }
}
