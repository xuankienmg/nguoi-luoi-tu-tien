using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanDao.Data;
using VanDao.Game;

var checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
using var db=new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=:memory:").Options);
GameService Service(MarketHandler handler)=>new(db,market:new MarketService(
    new LeaderboardOptions{Url="https://market.invalid",AnonKey="test-only"},NullLogger<MarketService>.Instance,
    client:new HttpClient(handler){BaseAddress=new Uri("https://market.invalid/rest/v1/")}));
Player Buyer(){var p=new Player{Id=Guid.NewGuid(),Name="Test buyer",Realm=1,LbSecret=new string('x',32)};p.Add("linh_thach",5000);return p;}
for(var run=0;run<10;run++){
    using var handler=new MarketHandler();var game=Service(handler);
    var buyers=Enumerable.Range(0,32).Select(_=>Buyer()).ToArray();
    var results=await Task.WhenAll(buyers.Select(async p=>{try{await game.MarketBuyAsync(p,42);return true;}catch(GameException){return false;}}));
    Check(results.Count(x=>x)==1,"more than one concurrent buyer succeeded");
    Check(buyers.Sum(p=>p.Count("tu_khi_dan"))==1,"listing granted more than once");
    Check(buyers.Sum(p=>p.Count("linh_thach"))==32*5000-100,"incorrect total debit");
    for(var i=0;i<buyers.Length;i++)Check(buyers[i].Count("linh_thach")==5000-(results[i]?100:0),"loser paid money");
    var winner=buyers[Array.FindIndex(results,x=>x)];
    try{await game.MarketBuyAsync(winner,42);throw new Exception("replay accepted");}catch(GameException){checks++;}
    Check(winner.Count("tu_khi_dan")==1&&winner.Count("linh_thach")==4900,"replay changed inventory");
}
foreach(var mode in new[]{"empty","duplicate","wrong-listing","wrong-buyer","wrong-item","wrong-qty","wrong-price","old-response","null-list","null-row","outdated-server"}){
    using var handler=new MarketHandler{Mode=mode};var game=Service(handler);var p=Buyer();
    try{await game.MarketBuyAsync(p,42);throw new Exception("bad receipt accepted: "+mode);}catch(GameException){checks++;}
    Check(p.Count("linh_thach")==5000&&p.Count("tu_khi_dan")==0,"bad receipt changed inventory: "+mode);
    Check(handler.LegacyCalls==0,"fell back to unsafe legacy purchase");
}
Console.WriteLine($"PASS: {checks} checks; ten races of 32 buyers, replay, wrong/empty/null/duplicate receipts, outdated server and unchanged loser balances.");

sealed class MarketHandler:HttpMessageHandler
{
    private int sold;
    public string Mode {get;init;}="ok";
    public int LegacyCalls;
    private static HttpResponseMessage Json(object? data,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")};
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){
        if(request.Method==HttpMethod.Get)return Json(new[]{new{id=42,seller_id=Guid.Parse("11111111-1111-1111-1111-111111111111"),seller_name="Seller",item_id="tu_khi_dan",qty=1,price=100,expires_at=DateTime.UtcNow.AddDays(1)}});
        if(!request.RequestUri!.AbsolutePath.EndsWith("/market_buy_v2")){Interlocked.Increment(ref LegacyCalls);throw new Exception("unexpected RPC");}
        using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var buyer=body.RootElement.GetProperty("p_id").GetGuid();
        await Task.Yield();
        if(Mode=="outdated-server")return Json(new{code="PGRST202",message="function not found"},HttpStatusCode.NotFound);
        if(Interlocked.CompareExchange(ref sold,1,0)!=0)return Json(new{code="P0001",message="Món này đã có người mua, đã hủy hoặc hết hạn."},HttpStatusCode.BadRequest);
        var receipt=new{listing_id=Mode=="wrong-listing"?43:42,buyer_id=Mode=="wrong-buyer"?Guid.NewGuid():buyer,item_id=Mode=="wrong-item"?"truc_co_dan":"tu_khi_dan",qty=Mode=="wrong-qty"?2:1,price=Mode=="wrong-price"?1:100,seller_name="Seller"};
        return Mode switch{
            "empty"=>Json(Array.Empty<object>()),"duplicate"=>Json(new[]{receipt,receipt}),
            "null-list"=>Json(null),"null-row"=>Json(new object?[]{null}),
            "old-response"=>Json(new[]{new{item_id="tu_khi_dan",qty=1,price=100,seller_name="Seller"}}),
            _=>Json(new[]{receipt})};
    }
}
