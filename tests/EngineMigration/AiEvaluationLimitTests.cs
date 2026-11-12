using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Services;

internal static class AiEvaluationLimitTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static void Run()
    {
        // Invoke production Start, not a duplicated range expression. A stopped application
        // cancels at the worker's first instruction, before any model, deck, network or fitting access.
        using var stopped=new CancellationTokenSource();stopped.Cancel();
        var life=Proxy<IHostApplicationLifetime>.Make((m,a)=>m.ReturnType==typeof(CancellationToken)?stopped.Token:null);
        LocalAiTrainingService Service()=>new(null!,null!,life,NullLogger<LocalAiTrainingService>.Instance,null!);
        foreach(var games in new[]{4,8,12,1000}){
            var service=Service();
            Check(service.Start(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),games,1,CompareStrategy:true)),"valid LLM request rejected at actual Start: "+games);
            service.WaitAsync().GetAwaiter().GetResult();
            Check(service.Status.Planned==games&&service.Status.CompareStrategy&&!service.Status.Running&&service.Status.Completed==0,"accepted run size changed or a game unexpectedly ran");
        }
        foreach(var games in new[]{0,1,3,5,1001}){
            var rejected=false;
            try{Service().Start(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),games,1,CompareStrategy:true));}
            catch(ArgumentOutOfRangeException e){rejected=e.ParamName=="request";}
            Check(rejected,"invalid LLM size accepted: "+games);
        }
        var normal=Service();var tooSmall=false;
        try{normal.Start(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),4,1));}catch(ArgumentOutOfRangeException){tooSmall=true;}
        Check(tooSmall,"normal training minimum unintentionally changed");
        Check(normal.Start(Guid.NewGuid(),new(Guid.NewGuid(),Guid.NewGuid(),10,1)),"normal 10 game request rejected");normal.WaitAsync().GetAwaiter().GetResult();
        Console.WriteLine("PASS production training Start: LLM 4/8/12/1000 accepted, 0/1/3/5/1001 rejected, normal 10 unchanged; games/provider calls/fitting=0");
    }
}
