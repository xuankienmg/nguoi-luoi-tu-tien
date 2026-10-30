using System.Security.Cryptography;
using System.Text;

namespace VanDao.Desktop;

/// <summary>Keeps the online session between launches, encrypted for the current Windows user (DPAPI).</summary>
public sealed class OnlineSessionStore(string path)
{
    public static OnlineSessionStore Default { get; } = new(Path.Combine(AppPaths.DataDir, "online-session.bin"));

    public string? Load()
    {
        try { return File.Exists(path) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)) : null; }
        catch (CryptographicException) { Clear(); return null; }   // another Windows user or a damaged file: sign in again
        catch (IOException) { return null; }
    }

    public void Save(string json)
    {
        try { File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser)); }
        catch (IOException) { }   // failing to remember the login must not block the game
    }

    public void Clear()
    {
        try { File.Delete(path); } catch (IOException) { }
    }
}
