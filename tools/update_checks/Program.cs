using VanDao.Launcher;
using System.Diagnostics;
if(args.Contains("--child")){await Task.Delay(350);return;}
void Check(bool ok){if(!ok)throw new Exception("Invalid update request behavior");}
Check(LauncherUpdateRequest.FromArgs([]) is null);
Check(LauncherUpdateRequest.FromArgs(["--update-game"]) is null);
var path=Path.GetFullPath("folder with spaces");
var req=LauncherUpdateRequest.FromArgs(["--update-game","--game-path",path,"--wait-pid","123"]);
Check(req?.GamePath==path&&req.WaitPid==123);
Check(LauncherUpdateRequest.FromArgs(["--update-game","--game-path",path,"--wait-pid","-1"]) is null);
Check(LauncherUpdateRequest.FromArgs(["--game-path",path,"--wait-pid","123"]) is null);
Check(LauncherUpdateRequest.FromArgs(["--update-game","--game-path","relative","--wait-pid","123"]) is null);
Console.WriteLine("PASS launcher update argument validation and paths with spaces.");

await new LauncherUpdateRequest(path,int.MaxValue).WaitForGameExitAsync(CancellationToken.None);
var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true};
start.ArgumentList.Add(typeof(LauncherUpdateRequest).Assembly.Location);start.ArgumentList.Add("--child");
using(var child=Process.Start(start)!) {
    var pending=new LauncherUpdateRequest(path,child.Id).WaitForGameExitAsync(CancellationToken.None);
    Check(!pending.IsCompleted);
    await pending;Check(child.HasExited);
}
using(var child=Process.Start(start)!) {
    using var timeout=new CancellationTokenSource(10);
    var timedOut=false;
    try{await new LauncherUpdateRequest(path,child.Id).WaitForGameExitAsync(timeout.Token);}
    catch(OperationCanceledException){timedOut=true;}
    Check(timedOut);await child.WaitForExitAsync();
}
Console.WriteLine("PASS waits for running game, tolerates exited game, and stops waiting on timeout.");
