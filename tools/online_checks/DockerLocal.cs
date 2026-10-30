using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

/// <summary>Docker Desktop ignores the network's default bind option for explicit CLI bindings.
/// Recreate only this project's two published containers with their original config and volumes.</summary>
static class DockerLocal
{
    public static async Task BindAsync()
    {
        var handler=new SocketsHttpHandler{ConnectCallback=async(_,ct)=>{
            var pipe=new NamedPipeClientStream(".","dockerDesktopLinuxEngine",PipeDirection.InOut,PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ct);return pipe;
        }};
        using var http=new HttpClient(handler){BaseAddress=new Uri("http://docker/v1.47/"),Timeout=TimeSpan.FromSeconds(60)};
        async Task<JsonNode?> Request(string method,string path,JsonNode? body=null){
            using var req=new HttpRequestMessage(new HttpMethod(method),path);
            if(body!=null)req.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
            using var res=await http.SendAsync(req);res.EnsureSuccessStatusCode();var text=await res.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(text)?null:JsonNode.Parse(text);
        }
        foreach(var name in new[]{"supabase_db_vandao-online-local","supabase_kong_vandao-online-local","supabase_studio_vandao-online-local"}){
            var original=(await Request("GET","containers/"+name+"/json"))!;
            var host=original["HostConfig"]!.DeepClone();
            var ports=host["PortBindings"]!.AsObject();
            if(ports.All(p=>p.Value!.AsArray().All(v=>v!["HostIp"]!.GetValue<string>()=="127.0.0.1")))continue;
            byte[]? runtime=null;
            if(name.Contains("kong")){
                using var archive=await http.GetAsync("containers/"+name+"/archive?path=/home/kong");
                archive.EnsureSuccessStatusCode();runtime=await archive.Content.ReadAsByteArrayAsync();
            }
            foreach(var p in ports)foreach(var binding in p.Value!.AsArray())binding!["HostIp"]="127.0.0.1";
            var config=original["Config"]!.DeepClone().AsObject();config["HostConfig"]=host;
            var networks=new JsonObject();
            foreach(var network in original["NetworkSettings"]!["Networks"]!.AsObject())
                networks[network.Key]=new JsonObject{["Aliases"]=network.Value!["Aliases"]?.DeepClone()};
            config["NetworkingConfig"]=new JsonObject{["EndpointsConfig"]=networks};
            var backup=name+"-port-backup";
            await Request("POST","containers/"+name+"/stop?t=20");
            await Request("POST","containers/"+name+"/rename?name="+backup);
            var created=await Request("POST","containers/create?name="+name,config);
            if(runtime!=null){
                using var archive=new ByteArrayContent(runtime);
                using var copied=await http.PutAsync("containers/"+created!["Id"]!.GetValue<string>()+"/archive?path=/home",archive);
                copied.EnsureSuccessStatusCode();
            }
            await Request("POST","containers/"+created!["Id"]!.GetValue<string>()+"/start");
            await Request("DELETE","containers/"+backup); // no volume deletion; data volume is reused by the replacement
        }
        Console.WriteLine("Local database, API and Studio ports now explicitly bind to 127.0.0.1; original data volumes retained.");
    }
}
