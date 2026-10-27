using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace VanDao.Desktop;

/// <summary>Full-size sign-in dialog for the taskbar strip; tokens are shared in process memory.</summary>
internal sealed class OnlineLoginWindow : Form
{
    private readonly CancellationTokenSource _closed = new();
    private bool _googlePending;
    public OnlineLoginWindow(string url)
    {
        Text="Vấn Đạo · Đăng nhập online";StartPosition=FormStartPosition.CenterScreen;
        Size=new Size(480,720);MinimumSize=new Size(400,580);
        var web=new WebView2{Dock=DockStyle.Fill};Controls.Add(web);
        Load+=async(_,_)=>{
            await WebHost.InitAsync(web,(type,body)=>{
                if(type=="online-authenticated"){DialogResult=DialogResult.OK;Close();}
                if(type=="online-google" && body.TryGetProperty("url",out var target))
                    _ = GoogleAsync(web,target.GetString());
            },url);
            web.Source=new Uri(url.TrimEnd('/')+"/online-login.html?native=1");
        };
        FormClosed+=(_,_)=>_closed.Cancel();
    }

    private async Task GoogleAsync(WebView2 web,string? target)
    {
        if(_googlePending)return;
        _googlePending=true;
        try {
            // The authorize origin is obtained from our own backend, never from external web content.
            using var http=new HttpClient();
            var configUrl=new Uri(web.Source!,"/api/session/google");
            using var config=JsonDocument.Parse(await http.GetStringAsync(configUrl,_closed.Token));
            var expected=new Uri(config.RootElement.GetProperty("authorizeUrl").GetString()!);
            if(!Uri.TryCreate(target,UriKind.Absolute,out var authorize)
                || authorize.GetLeftPart(UriPartial.Path)!=expected.GetLeftPart(UriPartial.Path))
                throw new InvalidOperationException("Địa chỉ đăng nhập không hợp lệ.");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            using var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:5190/");listener.Start();
            var state=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var redirect="http://127.0.0.1:5190/?state="+state;
            var launch=authorize.AbsoluteUri+"&redirect_to="+Uri.EscapeDataString(redirect);
            Process.Start(new ProcessStartInfo(launch){UseShellExecute=true});
            while(true){
                var callback=await listener.GetContextAsync().WaitAsync(timeout.Token);
                callback.Response.Headers["Cache-Control"]="no-store";
                if(callback.Request.QueryString["state"]!=state){callback.Response.StatusCode=400;callback.Response.Close();continue;}
                var code=callback.Request.QueryString["code"];
                var bytes=System.Text.Encoding.UTF8.GetBytes("Bạn có thể đóng tab này và quay lại Vấn Đạo.");
                callback.Response.ContentType="text/plain; charset=utf-8";
                await callback.Response.OutputStream.WriteAsync(bytes);callback.Response.Close();
                if(string.IsNullOrWhiteSpace(code))throw new InvalidOperationException("Đăng nhập Google bị hủy hoặc không thành công.");
                await web.ExecuteScriptAsync("window.completeGoogleCode("+JsonSerializer.Serialize(code)+")");
                break;
            }
        } catch(Exception e) when(e is HttpRequestException or HttpListenerException or OperationCanceledException or InvalidOperationException or JsonException){
            if(!web.IsDisposed)await web.ExecuteScriptAsync("window.googleLoginError("+JsonSerializer.Serialize(e is OperationCanceledException?"Phiên đăng nhập Google đã hết thời gian.":e.Message)+")");
        } finally {_googlePending=false;}
    }
}
