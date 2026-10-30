using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VanDao.Data;

var root=Path.GetFullPath(args.Length>1?args[1]:".");
var command=args.FirstOrDefault()??"test";
if(command=="bind-loopback"){await DockerLocal.BindAsync();return;}
var local=Path.Combine(root,".local");Directory.CreateDirectory(local);
var status=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Path.Combine(local,"supabase-status.json")))!;
string Setting(params string[] keys)=>keys.Select(k=>status.FirstOrDefault(x=>x.Key.Equals(k,StringComparison.OrdinalIgnoreCase)).Value).FirstOrDefault(v=>v.ValueKind==JsonValueKind.String).GetString()??throw new Exception("Local Supabase setting missing.");
var api=Setting("API_URL","api_url").TrimEnd('/');
var dbUrl=new Uri(Setting("DB_URL","db_url"));
if(new Uri(api).Port!=55431||!new Uri(api).IsLoopback||dbUrl.Port!=55432||!dbUrl.IsLoopback)throw new Exception("This tool only targets the isolated local stack on ports 55431/55432.");
var credentials=dbUrl.UserInfo.Split(':',2);
var admin=new NpgsqlConnectionStringBuilder{Host=dbUrl.Host,Port=dbUrl.Port,Database=dbUrl.AbsolutePath.Trim('/'),Username=Uri.UnescapeDataString(credentials[0]),Password=Uri.UnescapeDataString(credentials[1]),IncludeErrorDetail=false};
var anon=Setting("ANON_KEY","anon_key","PUBLISHABLE_KEY");
await using var connection=new NpgsqlConnection(admin.ConnectionString);await connection.OpenAsync();
if(command=="prepare"){
    using var model=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(admin.ConnectionString,VanDao.Online.OnlineSchema.Npgsql).Options);
    // Stacks prepared before EF migrations have tables but no history: adopt them as the baseline.
    var legacy=await new NpgsqlCommand("SELECT to_regclass('game.\"Players\"') IS NOT NULL AND to_regclass('game.\"__EFMigrationsHistory\"') IS NULL",connection).ExecuteScalarAsync();
    if((bool)legacy!)await new NpgsqlCommand($"""
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "StarterChosen" boolean NOT NULL DEFAULT true;
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "ManualProgress" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemVault" text NOT NULL DEFAULT '';
ALTER TABLE game."Players" ADD COLUMN IF NOT EXISTS "GemSlots" text NOT NULL DEFAULT '';
CREATE TABLE game."__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
INSERT INTO game."__EFMigrationsHistory" VALUES ('{model.Database.GetMigrations().First()}','8.0.11');
""",connection).ExecuteNonQueryAsync();
    await model.Database.MigrateAsync();
    await new NpgsqlCommand(VanDao.Online.OnlineSchema.Grants,connection).ExecuteNonQueryAsync();
    var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    await new NpgsqlCommand($"ALTER ROLE game_backend LOGIN PASSWORD '{password}'",connection).ExecuteNonQueryAsync();
    var backend=new NpgsqlConnectionStringBuilder(admin.ConnectionString){Username="game_backend",Password=password};
    var env=new Dictionary<string,string>{["Online__Mode"]="Server",["Online__AuthUrl"]=api,["Online__PublishableKey"]=anon,["ConnectionStrings__Game"]=backend.ConnectionString,["ASPNETCORE_URLS"]="http://127.0.0.1:5188",["ASPNETCORE_ENVIRONMENT"]="Production"};
    File.WriteAllText(Path.Combine(local,"online-server.env.json"),JsonSerializer.Serialize(env));
    Console.WriteLine("Prepared private game schema, restricted backend role and ignored local configuration. No cloud resources changed.");return;
}

var checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
using var http=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:5188"),Timeout=TimeSpan.FromSeconds(30)};
var suffix=Guid.NewGuid().ToString("N");
async Task<(string Token,Guid User)> Register(string name){
    using var response=await http.PostAsJsonAsync("/api/session/signup",new{email=$"{name}-{suffix}@example.com",password="Local-Test-"+suffix});
    var json=await response.Content.ReadFromJsonAsync<JsonElement>();Check(response.IsSuccessStatusCode&&json.TryGetProperty("access_token",out _),"local Auth signup");
    return(json.GetProperty("access_token").GetString()!,json.GetProperty("user").GetProperty("id").GetGuid());
}
async Task<(int Status,JsonElement Json,bool Replay)> Call(string method,string path,string? token=null,Guid? key=null,object? body=null){
    using var request=new HttpRequestMessage(new HttpMethod(method),path);
    if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
    if(key!=null)request.Headers.Add("Idempotency-Key",key.ToString());
    if(method=="POST")request.Content=JsonContent.Create(body??new{});
    using var response=await http.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
    JsonElement json;try{json=JsonSerializer.Deserialize<JsonElement>(text);}catch{throw new Exception($"Non-JSON response {response.StatusCode} from {path}");}
    return((int)response.StatusCode,json,response.Headers.Contains("X-Idempotent-Replay"));
}
JsonElement State(JsonElement json)=>json.TryGetProperty("state",out var value)?value:json;
int Count(JsonElement inventory,string item)=>inventory.TryGetProperty(item,out var value)?value.GetInt32():0;
if(command=="resume"){
    var saved=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(local,"online-test-session.json")));
    var response=await Call("GET","/api/players/"+saved.GetProperty("player").GetGuid(),saved.GetProperty("token").GetString());
    Check(response.Status==200&&State(response.Json).GetProperty("name").GetString()=="Alice Verified","state survives backend restart");
    Console.WriteLine("PASS: authenticated character persisted across backend restart.");return;
}
if(command=="unavailable"){
    var saved=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(local,"online-test-session.json")));
    var response=await Call("GET","http://127.0.0.1:5189/api/players/"+saved.GetProperty("player").GetGuid(),saved.GetProperty("token").GetString());
    Check(response.Status==503&&!response.Json.TryGetProperty("state",out _),"proxy must not fall back to offline save when backend is unavailable");
    Console.WriteLine("PASS: backend outage fails closed; desktop proxy never substitutes local character data.");return;
}
Check((await Call("GET","/api/players/current")).Status==401,"unauthenticated read rejected");
Check((await Call("GET","/api/players/current","forged-token")).Status==401,"forged token rejected");
var a=await Register("alice");var b=await Register("bob");
var jwtParts=a.Token.Split('.');
var forgedPayload=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(jwtParts[1].Replace('-','+').Replace('_','/').PadRight((jwtParts[1].Length+3)/4*4,'='))))!;
forgedPayload["sub"]=JsonSerializer.SerializeToElement(b.User);
var altered=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(forgedPayload)).TrimEnd('=').Replace('+','-').Replace('/','_');
Check((await Call("GET","/api/players/current",jwtParts[0]+"."+altered+"."+jwtParts[2])).Status==401,"JWT subject tampering rejected by Supabase Auth");
Check((await Call("POST","/api/players",b.Token,Guid.NewGuid(),new{name="Forged owner",ownerId=a.User})).Status==400,"client cannot assign character ownership");
var created=await Call("POST","/api/players",a.Token,Guid.NewGuid(),new{name="Alice local"});Check(created.Status==200,"create owned character");
var aid=State(created.Json).GetProperty("id").GetGuid();
var current=await Call("GET","/api/players/current",a.Token);Check(State(current.Json).GetProperty("id").GetGuid()==aid,"current only selects caller's character");
Check((await Call("GET",$"/api/players/{aid}",b.Token)).Status==404,"another account cannot read character");
Check((await Call("POST",$"/api/players/{aid}/channel",b.Token,Guid.NewGuid())).Status==404,"another account cannot mutate character");
Check((await Call("POST","/api/players",a.Token,Guid.NewGuid(),new{name="duplicate"})).Status==409,"one character per account");
Check((await Call("POST",$"/api/players/{aid}/channel",a.Token)).Status==400,"mutation requires idempotency key");
Check((await Call("POST",$"/api/players/{aid}/channel",a.Token,Guid.NewGuid(),new{qi=999999,inventory=new{linh_thach=999999}})).Status==400,"forged save upload rejected");
Check((await Call("POST",$"/api/players/{aid}/admin/stones?n=999999",a.Token,Guid.NewGuid())).Status==403,"online admin rejected");
Check((await Call("POST",$"/api/players/{aid}/market/list/tu_khi_dan?qty=1&price=1",a.Token,Guid.NewGuid())).Status==503,"legacy non-atomic market disabled");
var idem=Guid.NewGuid();var first=await Call("POST",$"/api/players/{aid}/channel",a.Token,idem);Check(first.Status==200,"server-computed channel action");
var repeated=await Call("POST",$"/api/players/{aid}/channel",a.Token,idem);Check(repeated.Status==200&&repeated.Replay&&first.Json.GetRawText()==repeated.Json.GetRawText(),"identical retry replays exact response");
Check((await Call("POST",$"/api/players/{aid}/name?name=Changed",a.Token,idem)).Status==409,"same key with different action rejected");
Check((await Call("POST",$"/api/players/{aid}/channel",a.Token,Guid.NewGuid())).Status==400,"channel cooldown enforced by server");
var parallelKey=Guid.NewGuid();var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Call("POST",$"/api/players/{aid}/name?name=Alice%20Verified",a.Token,parallelKey)));
Check(results.All(x=>x.Status==200)&&results.Count(x=>!x.Replay)==1,"parallel duplicate requests commit once");
var bad=await Call("POST",$"/api/players/{aid}/brew/chan_tien_dan?n=1",a.Token,Guid.NewGuid());Check(bad.Status==400,"invalid action cannot mint rewards");
// Trusted fixture setup in the isolated database, never a public/admin client endpoint.
await using(var seed=new NpgsqlCommand("UPDATE game.\"Inventory\" SET \"Count\"=@amount WHERE \"PlayerId\"=@id AND \"ItemId\"='linh_thach'; UPDATE game.\"Players\" SET \"ExploreReadyAt\"=now()+interval '1 hour' WHERE \"Id\"=@id",connection)){
    seed.Parameters.AddWithValue("amount",VanDao.Game.GameData.SpinTicketPrice);seed.Parameters.AddWithValue("id",aid);await seed.ExecuteNonQueryAsync();
}
var spend=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Call("POST",$"/api/players/{aid}/spin/buy?n=1",a.Token,Guid.NewGuid())));
Check(spend.Count(x=>x.Status==200)==1&&spend.Count(x=>x.Status==400)==1,"parallel distinct purchases cannot double-spend one balance");
var afterSpend=State((await Call("GET",$"/api/players/{aid}",a.Token)).Json).GetProperty("inventory");
Check(Count(afterSpend,VanDao.Game.GameData.SpinTicket)==1&&Count(afterSpend,"linh_thach")==0,"server transaction debits one ticket exactly once");
var fakePath=Path.Combine(local,"fake-client-"+suffix+".db");
using(var fake=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source="+fakePath).Options)){
    fake.Database.EnsureCreated();var tampered=new Player{Id=aid,Name="Forged local",Realm=9,Qi=1e30};tampered.Add("linh_thach",2000000000);fake.Players.Add(tampered);await fake.SaveChangesAsync();
}
var trusted=State((await Call("GET",$"/api/players/{aid}",a.Token)).Json);
Check(trusted.GetProperty("realm").GetInt32()==0&&trusted.GetProperty("name").GetString()=="Alice Verified"&&Count(trusted.GetProperty("inventory"),"linh_thach")==0,"tampered local SQLite has no effect on authoritative online state");
await using(var cmd=new NpgsqlCommand("SELECT \"OwnerId\",\"Name\" FROM game.\"Players\" WHERE \"Id\"=@id",connection)){
    cmd.Parameters.AddWithValue("id",aid);await using var reader=await cmd.ExecuteReaderAsync();Check(await reader.ReadAsync()&&reader.GetGuid(0)==a.User&&reader.GetString(1)=="Alice Verified","authoritative state stored in Supabase Postgres");
}
foreach(var role in new[]{"anon","authenticated"}){
    await using var tx=await connection.BeginTransactionAsync();
    await new NpgsqlCommand("SET LOCAL ROLE "+role,connection,tx).ExecuteNonQueryAsync();
    try{await new NpgsqlCommand("UPDATE game.\"Players\" SET \"Qi\"=999999",connection,tx).ExecuteNonQueryAsync();Check(false,"direct client write must be denied");}
    catch(PostgresException e){Check(e.SqlState=="42501","RLS/grants deny direct client writes");}await tx.RollbackAsync();
}
Check((await Call("GET","/healthz")).Status==200,"health check reports a reachable database");
// Account flows: reset e-mail, reset link → new password, confirmation resend.
var aliceEmail=$"alice-{suffix}@example.com";
var recovered=await Call("POST","/api/session/recover",body:new{email=aliceEmail});
var unknownRecover=await Call("POST","/api/session/recover",body:new{email=$"nobody-{suffix}@example.com"});
Check(recovered.Status==200&&unknownRecover.Status==200&&recovered.Json.GetRawText()==unknownRecover.Json.GetRawText(),"password reset answers the same for unknown e-mails");
using(var mail=new HttpClient{Timeout=TimeSpan.FromSeconds(10)}){
    JsonElement found=default;
    for(var i=0;i<20&&!(found.ValueKind==JsonValueKind.Object&&found.GetProperty("messages_count").GetInt32()>0);i++){
        await Task.Delay(500);found=await mail.GetFromJsonAsync<JsonElement>("http://127.0.0.1:55434/api/v1/search?query="+Uri.EscapeDataString("to:"+aliceEmail));
    }
    Check(found.GetProperty("messages_count").GetInt32()>0,"reset e-mail delivered to the local mail server");
    var html=await mail.GetFromJsonAsync<JsonElement>("http://127.0.0.1:55434/api/v1/message/"+found.GetProperty("messages")[0].GetProperty("ID").GetString());
    Check(html.GetProperty("HTML").GetString()!.Contains("online-reset.html"),"reset e-mail links to the game's reset page");
}
var secret=Setting("SECRET_KEY","SERVICE_ROLE_KEY","service_role_key");
using(var adminHttp=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false})){
    using var linkRequest=new HttpRequestMessage(HttpMethod.Post,api+"/auth/v1/admin/generate_link"){Content=JsonContent.Create(new{type="recovery",email=aliceEmail,redirect_to="http://127.0.0.1:5188/online-reset.html"})};
    linkRequest.Headers.Add("apikey",secret);
    using var linkResponse=await adminHttp.SendAsync(linkRequest);
    var link=(await linkResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("action_link").GetString()!;
    using var verify=await adminHttp.GetAsync(link);
    var landing=verify.Headers.Location!;
    Check(landing.AbsoluteUri.StartsWith("http://127.0.0.1:5188/online-reset.html#")&&landing.Fragment.Contains("type=recovery"),"reset link lands on the game's reset page with a recovery token");
    var recoveryToken=System.Web.HttpUtility.ParseQueryString(landing.Fragment.TrimStart('#'))["access_token"]!;
    Check((await Call("POST","/api/session/password",recoveryToken,body:new{password="short"})).Status==400,"short new password rejected");
    Check((await Call("POST","/api/session/password",recoveryToken,body:new{password="Reset-Pass-"+suffix})).Status==200,"recovery token sets a new password");
}
Check((await Call("POST","/api/session/signin",body:new{email=aliceEmail,password="Reset-Pass-"+suffix})).Status==200,"sign in with the new password");
Check((await Call("POST","/api/session/signin",body:new{email=aliceEmail,password="Local-Test-"+suffix})).Status==400,"old password no longer works");
Check((await Call("POST","/api/session/resend",body:new{email=aliceEmail})).Status==200,"confirmation resend accepted");
// Last on purpose: it spends this minute's sign-in budget for 127.0.0.1 (wait a minute before re-running).
var limited=0;
for(var i=0;i<25;i++){using var attempt=await http.PostAsJsonAsync("/api/session/signin",new{email=$"limit-{suffix}@example.com",password="Wrong-Password-1"});if((int)attempt.StatusCode==429)limited++;}
Check(limited>0,"sign-in attempts are rate limited per IP");
File.WriteAllText(Path.Combine(local,"online-test-session.json"),JsonSerializer.Serialize(new{token=a.Token,player=aid,user=a.User}));
Console.WriteLine($"PASS: {checks} real local Supabase Auth/PostgreSQL checks (ownership, forged data, atomic actions, replay, concurrency, grants, health and auth rate limit).");
