using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace VanDao.Desktop;

/// <summary>Dùng chung một môi trường WebView2 để dải game và bảng chia sẻ cùng tu sĩ (localStorage).</summary>
internal static class WebHost
{
    private static Task<CoreWebView2Environment>? _env;
    private static string? _onlineSession;
    private static event Action? OnlineSessionChanged;

    public static Task<CoreWebView2Environment> Environment =>
        _env ??= CoreWebView2Environment.CreateAsync(null, Path.Combine(AppPaths.DataDir, "WebView2"));

    /// <summary>Khởi tạo WebView2, nhận tin nhắn {type: ...} từ trang và mở link ngoài bằng trình duyệt.</summary>
    public static async Task InitAsync(WebView2 web, Action<string, JsonElement> onMessage, string? trustedUrl = null)
    {
        await web.EnsureCoreWebView2Async(await Environment);
        var s = web.CoreWebView2.Settings;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        var trusted = trustedUrl is null ? null : new Uri(trustedUrl).GetLeftPart(UriPartial.Authority);
        void SendSession() {
            if(web.IsDisposed || web.CoreWebView2 is null)return;
            if(web.Source is null || !web.Source.GetLeftPart(UriPartial.Authority).Equals(trusted,StringComparison.OrdinalIgnoreCase))return;
            object? session=_onlineSession is null?null:JsonSerializer.Deserialize<JsonElement>(_onlineSession);
            web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="online-session",session}));
        }
        if(trusted is not null){
            await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("""
            window.__nativeSession = new Promise(resolve => {
              window.chrome.webview.addEventListener('message', event => {
                if(event.data?.type !== 'online-session')return;
                const before=!!sessionStorage.getItem('van-dao-online-session'), session=event.data.session;
                if(session)sessionStorage.setItem('van-dao-online-session',JSON.stringify(session));
                else sessionStorage.removeItem('van-dao-online-session');
                if(typeof onlineSession!=='undefined')onlineSession.session=session;
                resolve();
                if(before!==!!session && !location.pathname.includes('online-login'))location.reload();
              });
              window.chrome.webview.postMessage({type:'online-session-request'});
            });
            """);
            OnlineSessionChanged+=SendSession;
            web.Disposed+=(_,_)=>OnlineSessionChanged-=SendSession;
        }
        web.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        };
        web.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if(trusted is not null && Uri.TryCreate(e.Source,UriKind.Absolute,out var source)
                    && source.GetLeftPart(UriPartial.Authority).Equals(trusted,StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("type",out var nativeType)){
                    var type=nativeType.GetString();
                    if(type=="online-session-request"){SendSession();return;}
                    if(type=="online-google"){onMessage(type,root.Clone());return;}
                    if(type is "online-authenticated" or "online-session-update"){
                        if(root.TryGetProperty("session",out var session)){
                            if(session.ValueKind==JsonValueKind.Null)_onlineSession=null;
                            else if(session.TryGetProperty("access_token",out var access)&&access.GetString()?.Length is > 0 and < 8192)
                                _onlineSession=session.GetRawText();
                            OnlineSessionChanged?.Invoke();
                        }
                    }
                }
                if (root.TryGetProperty("type", out var t) && t.GetString()!="online-google") onMessage(t.GetString() ?? "", root.Clone());
            }
            catch (JsonException) { }
        };
    }

    public static Task SyncAsync(WebView2 web) =>
        web.CoreWebView2 is null ? Task.CompletedTask : web.ExecuteScriptAsync("window.sync && window.sync()");
}
