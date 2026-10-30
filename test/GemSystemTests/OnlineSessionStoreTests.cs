using VanDao.Desktop;

namespace GemSystemTests;

public class OnlineSessionStoreTests
{
    [Fact]
    public void SessionRoundTripsEncryptedAndClears()
    {
        var path = Path.Combine(Path.GetTempPath(), "vandao-session-" + Guid.NewGuid().ToString("N") + ".bin");
        var store = new OnlineSessionStore(path);
        const string session = """{"access_token":"a.b.c","refresh_token":"r-secret"}""";
        try {
            Assert.Null(store.Load());
            store.Save(session);
            Assert.DoesNotContain("r-secret", File.ReadAllText(path));   // not stored in clear text
            Assert.Equal(session, store.Load());
            store.Clear();
            Assert.False(File.Exists(path));
            Assert.Null(store.Load());
        } finally { File.Delete(path); }
    }

    [Fact]
    public void HandoffSessionIsReadOnlyOnce()
    {
        // Launcher sign-in without "remember": the next game start gets the session, later starts do not
        var path = Path.Combine(Path.GetTempPath(), "vandao-session-" + Guid.NewGuid().ToString("N"), "once.bin");
        var store = new OnlineSessionStore(path);
        try {
            store.Save("""{"access_token":"a.b.c"}""");
            Assert.Equal("""{"access_token":"a.b.c"}""", store.Take());
            Assert.False(File.Exists(path));
            Assert.Null(store.Take());
        } finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void CorruptFileIsDiscarded()
    {
        var path = Path.Combine(Path.GetTempPath(), "vandao-session-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        try {
            Assert.Null(new OnlineSessionStore(path).Load());
            Assert.False(File.Exists(path));
        } finally { File.Delete(path); }
    }
}
