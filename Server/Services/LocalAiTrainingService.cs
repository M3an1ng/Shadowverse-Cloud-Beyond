using System.Diagnostics;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services.Abstractions;
namespace ShadowverseBeyond.Server.Services;

public sealed record LocalTrainingRequest(Guid PlayerDeckId, Guid OpponentDeckId, int Games=20, int Parallel=2, bool Continuous=false, bool CompareLegacy=false, int BenchmarkSeed=18431, bool CompareStrategy=false, bool StrategyChoicesOnly=false, bool CompareLocalBaseline=false, int? StrategyTurnBudgetMs=null)
{
    internal bool IsBenchmark => CompareLegacy || CompareStrategy || CompareLocalBaseline;
}
public sealed record StrategyMatchResult(int Episode, int ShuffleSeed, Guid DeckId, string DeckName, bool First,
    string Result, long ElapsedMs, AiStrategyPlanner.DecisionMetrics Turn, AiStrategyPlanner.DecisionMetrics Choice);

public sealed record LocalTrainingStatus(bool Running=false,int Completed=0,int Failed=0,int Planned=0,int Samples=0,string Message="尚未开始训练",bool Promoted=false,double? Before=null,double? After=null,int Parallel=1,int Active=0,bool Continuous=false,int Round=1,int TotalCompleted=0,int Wins=0,int Losses=0,int Draws=0,string PlayerDeckName="",string OpponentDeckName="",Guid PlayerDeckId=default,Guid OpponentDeckId=default,int CandidateFirstGames=0,int CandidateFirstWins=0,int? BenchmarkSeed=null,int StrategyGames=0,int StrategyPlans=0,bool CompareStrategy=false,AiStrategyPlanner.StrategyReport? LatestStrategy=null,
    AiStrategyPlanner.DecisionMetrics? TurnDecisions=null, AiStrategyPlanner.DecisionMetrics? OptionDecisions=null, bool StrategyChoicesOnly=false, StrategyMatchResult[]? StrategyMatches=null, bool CompareLocalBaseline=false, int? StrategyTurnBudgetMs=null);
public sealed class LocalAiTrainingService(IServiceScopeFactory scopes,AiLearningModel model,IHostApplicationLifetime lifetime,ILogger<LocalAiTrainingService> logger,AiStrategyPlanner strategy)
{
    private sealed class StrategyUnavailableException(string message) : Exception(message);
    void EnsureStrategyAvailable(LocalTrainingRequest request)
    {
        if (request.CompareStrategy && !strategy.CanRequest)
            throw new StrategyUnavailableException("大模型评测中断：" + strategy.ProviderStatus.Message);
    }
    readonly object sync=new(); CancellationTokenSource? cancellation;Task? run;
    LocalTrainingStatus status=new();
    public LocalTrainingStatus Status {get {lock(sync)return status;}}
    void Set(Func<LocalTrainingStatus,LocalTrainingStatus> change){lock(sync)status=change(status);}
    public bool Start(Guid owner,LocalTrainingRequest request) {
        lock(sync) {
            if(request.Parallel is < 1 or > 16 || request.Games is < 10 or > 1000)throw new ArgumentOutOfRangeException(nameof(request));
            if(run is {IsCompleted:false})return false;
            cancellation?.Dispose();cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            status=new(true,0,0,request.Games,0,"正在运行本地自对战",Parallel:request.Parallel,Continuous:request.Continuous,PlayerDeckId:request.PlayerDeckId,OpponentDeckId:request.OpponentDeckId,BenchmarkSeed:request.IsBenchmark?request.BenchmarkSeed:null,CompareStrategy:request.CompareStrategy,StrategyChoicesOnly:request.CompareStrategy && request.StrategyChoicesOnly,CompareLocalBaseline:request.CompareLocalBaseline,StrategyTurnBudgetMs:request.StrategyTurnBudgetMs);
            run=Task.Run(()=>RunContinuous(owner,request,cancellation.Token));return true;
        }
    }
    public void Stop(){lock(sync)cancellation?.Cancel();}
    public async Task WaitAsync(){Task? task;lock(sync)task=run;if(task!=null)await task;}
    async Task RunContinuous(Guid owner,LocalTrainingRequest request,CancellationToken token) {
        try {
            do {
                token.ThrowIfCancellationRequested();
                if(!await Train(owner,request,token))break;
                if(!request.Continuous || request.IsBenchmark)break;
                token.ThrowIfCancellationRequested();
                Set(s=>s with{Round=s.Round+1,Completed=0,Failed=0,Samples=0,Message="正在开始下一轮连续训练"});
                await Task.Delay(500,token);
            } while(true);
        } catch(OperationCanceledException){Set(s=>s with{Message="训练已停止"});}
        finally {Set(s=>s with{Running=false,Active=0});}
    }
    sealed record Sample(int Episode,double[] Features,double Target);
    async Task<bool> Train(Guid owner,LocalTrainingRequest request,CancellationToken token) {
        var samples=new List<Sample>();var deckKeys=new HashSet<string>();
        try {
            await Parallel.ForEachAsync(Enumerable.Range(0,request.Games),new ParallelOptions{MaxDegreeOfParallelism=request.Parallel,CancellationToken=token},async (episode,workerToken) => {
                token.ThrowIfCancellationRequested();
                EnsureStrategyAvailable(request);
                using var scope=scopes.CreateScope();var sp=scope.ServiceProvider;
                var decks=sp.GetRequiredService<IDeckService>();var games=sp.GetRequiredService<IGameService>();var matches=sp.GetRequiredService<IMatchService>();
                var a=await decks.GetAsync(new(owner,null),request.PlayerDeckId,token)??throw new InvalidOperationException("训练卡组不存在");
                var b=await decks.GetAsync(new(owner,null),request.OpponentDeckId,token)??throw new InvalidOperationException("对手卡组不存在");
                Set(s=>s with{PlayerDeckName=a.Name,OpponentDeckName=b.Name});
                lock(sync) foreach(var d in new[]{a,b})deckKeys.Add(AiLearningModel.DeckKey(d.Cards.SelectMany(c=>Enumerable.Repeat(c.CardDefinitionId,c.Count))));
                // Each four-game block gives each policy the same deck and first-player combinations.
                var hostUsesA = request.IsBenchmark ? episode / 4 % 2 == 0 : episode % 2 == 0;
                var aGoesFirst = episode % 4 < 2;
                var created = matches.Create(Guid.NewGuid(), false, devMatchConfig: new()
                {
                    IgnoreDeckFormatConsistency = true,
                    IsLocalAiTraining = true,
                    BenchmarkHostGoesFirst = request.IsBenchmark ? hostUsesA == aGoesFirst : null,
                    BenchmarkShuffleSeed = request.IsBenchmark ? unchecked(request.BenchmarkSeed + episode / 4) : null
                });
                var room=created.Room??throw new InvalidOperationException(created.Message);
                var matchTimer = Stopwatch.StartNew();
                var observations=new List<(Guid player,double[] x)>();
                Set(s=>s with{Active=s.Active+1});
                try {
                    var joined=matches.TryJoin(room.RoomId,room.JoinCode,Guid.NewGuid());var guest=joined.Room?.GuestPlayerId??throw new InvalidOperationException(joined.Message);
                    MatchDeckSelection Selection(DeckDetailDto d)=>new(){UserId=owner,DeckId=d.DeckId,DeckClass=d.Class,SecondaryClass=d.SecondaryClass,Environment=d.Environment,Rule=d.Rule};
                    var first=hostUsesA?a:b;var second=hostUsesA?b:a;
                    var playerSeat=hostUsesA?room.HostPlayerId:guest;
                    var candidateSeat=episode%2==0?playerSeat:(playerSeat==room.HostPlayerId?guest:room.HostPlayerId);
                    if(!matches.SubmitDeckSelection(room.RoomId,room.HostPlayerId,Selection(first)).Success||!matches.SubmitDeckSelection(room.RoomId,guest,Selection(second)).Success)throw new InvalidOperationException("训练卡组选择失败");
                    matches.MarkReady(room.RoomId,room.HostPlayerId);matches.MarkReady(room.RoomId,guest);
                    var started=matches.Start(room.RoomId,room.HostPlayerId);var id=started.Room?.ActiveGameId??throw new InvalidOperationException(started.Message);
                    var game=await games.GetOrCreateGameStateAsync(id,room.HostPlayerId,guest,false,sp)??throw new InvalidOperationException("训练对局初始化失败，请检查卡组完整性");
                    if(request.CompareLegacy)game.LegacyAiPlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if (request.CompareStrategy)
                    {
                        game.StrategyBenchmarkPlayerId = candidateSeat;
                        game.StrategyChoicesOnly = request.StrategyChoicesOnly;
                        game.StrategyTurnBudgetMs = request.StrategyTurnBudgetMs;
                        var opponent = candidateSeat == room.HostPlayerId ? game.Player2 : game.Player1;
                        _ = strategy.WarmDeckAsync(game, candidateSeat, opponent.Class.ToString());
                        game.StrategyBenchmarkDeckName = candidateSeat == room.HostPlayerId ? first.Name : second.Name;
                    }
                    var mapper=sp.GetRequiredService<IMapper>();var accessor=sp.GetRequiredService<IGameContextAccessor>();
                    GameStateDto Snapshot(Guid actor){using var context=new GameContext(sp,mapper,accessor,game,actor,token);return GameStateProjection.Create(game,context);}
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(3));
                    async Task Submit(Guid actor,string kind,object data) {
                        var action=new PlayerActionWrapper(kind,actor,data){CancellationToken=deadline.Token};
                        if(!await games.EnqueuePlayerActionAsync(id,action))throw new InvalidOperationException("训练动作未接收");
                        var result=await action.Completion.WaitAsync(deadline.Token);if(result.Status!=PlayerActionCompletionStatus.Completed)throw new InvalidOperationException(result.Reason);
                    }
                    var ai=sp.GetRequiredService<IAiService>();
                    foreach(var player in new[]{game.Player1,game.Player2}) {
                        var replace=await ai.DecideMulliganAsync(Snapshot(player.PlayerId),player.PlayerId);
                        await Submit(player.PlayerId,"SubmitMulligan",replace.Select(Guid.Parse).ToList());
                    }
                    for(var turn=0;turn<100&&!game.IsGameOver;turn++) {
                        deadline.Token.ThrowIfCancellationRequested();var actor=game.CurrentPlayerId??throw new InvalidOperationException("训练缺少当前玩家");
                        if(!request.IsBenchmark){var state=Snapshot(actor);observations.Add((actor,AiFeatures.Value(state,actor)));}
                        EnsureStrategyAvailable(request);
                        await sp.GetRequiredService<AiTurnExecutor>().ExecuteTurnAsync(id,actor,deadline.Token);
                        EnsureStrategyAvailable(request);
                        if (request.CompareStrategy && actor == candidateSeat && strategy.GetReport(game) is { } report)
                            Set(s => s with { LatestStrategy = report });
                    }
                    if(!game.IsGameOver)throw new InvalidOperationException("训练对局超过回合上限");
                    lock(sync) {
                        var retained=Math.Min(32,observations.Count);
                        for(var i=0;i<retained;i++){var observation=observations[i*observations.Count/retained];samples.Add(new(episode,observation.x,game.WinnerId==null||game.WinnerId==Guid.Empty?0:game.WinnerId==observation.player?1:-1));}
                        var scoredSeat=request.IsBenchmark?candidateSeat:playerSeat;
                        var draw=game.WinnerId==null||game.WinnerId==Guid.Empty;
                        status=status with{TotalCompleted=status.TotalCompleted+1,Wins=status.Wins+(!draw&&game.WinnerId==scoredSeat?1:0),Losses=status.Losses+(!draw&&game.WinnerId!=scoredSeat?1:0),Draws=status.Draws+(draw?1:0)};
                        if (request.CompareStrategy || request.CompareLocalBaseline)
                        {
                            var plans = strategy.CountPlans(game);
                            var metrics = strategy.GetDecisionMetrics(game);
                            var candidateDeck = episode % 2 == 0 ? a : b;
                            var matchResult = new StrategyMatchResult(episode + 1,
                                unchecked(request.BenchmarkSeed + episode / 4), candidateDeck.DeckId, candidateDeck.Name,
                                game.FirstPlayerId == candidateSeat, draw ? "draw" : game.WinnerId == candidateSeat ? "win" : "loss",
                                matchTimer.ElapsedMilliseconds, metrics.Turn, metrics.Choice);
                            status = status with
                            {
                                StrategyMatches = [.. status.StrategyMatches ?? [], matchResult],
                                StrategyGames = status.StrategyGames + (plans > 0 || metrics.Choice.Accepted > 0 ? 1 : 0),
                                StrategyPlans = status.StrategyPlans + plans,
                                TurnDecisions = (status.TurnDecisions ?? new()).Merge(metrics.Turn),
                                OptionDecisions = (status.OptionDecisions ?? new()).Merge(metrics.Choice)
                            };
                        }
                        if (request.IsBenchmark && game.FirstPlayerId == candidateSeat)
                            status = status with { CandidateFirstGames = status.CandidateFirstGames + 1,
                                CandidateFirstWins = status.CandidateFirstWins + (game.WinnerId == candidateSeat ? 1 : 0) };
                        status=status with{Completed=status.Completed+1,Samples=samples.Count,Message=$"自对战完成 {status.Completed+1}/{request.Games} 局"};
                    }
                } catch(OperationCanceledException) when(!token.IsCancellationRequested) {Set(s=>s with{Failed=s.Failed+1,Message="一局训练超时，继续下一局"});}
                  catch(Exception e) when(e is not OperationCanceledException and not StrategyUnavailableException) {logger.LogWarning(e,"Local AI training episode failed");Set(s=>s with{Failed=s.Failed+1,Message=e.Message});}
                finally {matches.Close(room.RoomId,"本地AI训练结束");Set(s=>s with{Active=Math.Max(0,s.Active-1)});}
            });
            token.ThrowIfCancellationRequested();
            if(request.CompareStrategy)
            {
                Set(s=>s with { Message = $"LLM vs local: {s.Wins}W {s.Losses}L {s.Draws}D, model advice in {s.StrategyGames}/{s.Completed} completed games" });
                return true;
            }
            if(request.CompareLocalBaseline)
            {
                Set(s=>s with{Message=$"本地策略基线：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，失败 {s.Failed} 局"});
                return true;
            }
            if(request.CompareLegacy)
            {
                Set(s=>s with{Message=$"新策略对旧策略：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，失败 {s.Failed} 局"});
                return true;
            }
            Set(s=>s with{Message="正在训练并检查独立验证集"});
            if(samples.Count==0)throw new InvalidOperationException("本轮没有有效对局，连续训练已停止，请检查卡组或日志");
            await Fit(samples,deckKeys,token);
            return true;
        } catch(StrategyUnavailableException e){Set(s=>s with{Message=e.Message});return false;}
          catch(OperationCanceledException){Set(s=>s with{Message="训练已停止"});return false;}
          catch(Exception e){logger.LogError(e,"Local AI training failed");Set(s=>s with{Message="训练失败："+e.Message});return false;}
    }
    async Task Fit(List<Sample> samples,HashSet<string> deckKeys,CancellationToken token) {
        var artifact=model.Current??throw new InvalidOperationException("请先迁移模型");
        var previous=artifact.Weights.ValueModel is string id?artifact.Models[id]:throw new InvalidOperationException("缺少局面网络");
        if(previous.FeatureVersion!=3)throw new InvalidOperationException("本地训练需要第三版局面特征");
        var training=samples.Where(s=>s.Episode%5!=0).ToArray();var validation=samples.Where(s=>s.Episode%5==0).ToArray();
        if(training.Select(s=>s.Episode).Distinct().Count()<4||validation.Select(s=>s.Episode).Distinct().Count()<2) {Set(s=>s with{Message="有效对局不足，请至少完成10局后再训练"});return;}
        var net=new AiNetwork{Format=1,FeatureVersion=3,SnapshotId=previous.SnapshotId,Updates=previous.Updates,W1=(double[])previous.W1.Clone(),W2=(double[])previous.W2.Clone(),B1=(double[])previous.B1.Clone(),B2=previous.B2};
        double Loss(AiNetwork n)=>validation.Average(s=>Math.Pow(n.Predict(s.Features)-s.Target,2));
        var before=Loss(previous);
        for(var epoch=0;epoch<3;epoch++)foreach(var sample in training.OrderBy(s=>AiFeatures.Hash(s.Episode+":"+s.Features[1]))) {
            token.ThrowIfCancellationRequested();var x=sample.Features;var h=new double[128];var output=net.B2;
            for(var j=0;j<128;j++){var sum=net.B1[j];for(var i=0;i<512;i++)sum+=net.W1[j*512+i]*x[i];h[j]=Math.Tanh(sum);output+=h[j]*net.W2[j];}
            var prediction=Math.Tanh(output);var delta=Math.Clamp((prediction-sample.Target)*(1-prediction*prediction),-1,1);const double rate=.002;
            for(var j=0;j<128;j++){var grad=delta*net.W2[j]*(1-h[j]*h[j]);net.W2[j]-=rate*delta*h[j];net.B1[j]-=rate*grad;for(var i=0;i<512;i++)net.W1[j*512+i]-=rate*(grad*x[i]+.0001*net.W1[j*512+i]);}
            net.B2-=rate*delta;net.Updates++;
        }
        var after=Loss(net);
        if(!double.IsFinite(after)||after>=before-1e-5){Set(s=>s with{Before=before,After=after,Message="验证集未改善，保留原模型"});return;}
        var key="native-value-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        AiWeights WithModel(AiWeights w)=>new(){Board=w.Board,Face=w.Face,Survival=w.Survival,Cards=w.Cards,Resource=w.Resource,Tempo=w.Tempo,Utility=w.Utility,ValueModel=key,ValueTeacher=w.ValueTeacher,ActionPolicy=w.ActionPolicy};
        var next=new AiModelArtifact{Version=key,Weights=WithModel(artifact.Weights),Specialists=artifact.Specialists.ToDictionary(e=>e.Key,e=>deckKeys.Contains(e.Key)?WithModel(e.Value):e.Value),Models=new(artifact.Models)};next.Models[key]=net;
        var referenced=next.Specialists.Values.Prepend(next.Weights).SelectMany(w=>new[]{w.ValueModel,w.ValueTeacher,w.ActionPolicy}).Where(k=>k!=null).ToHashSet();
        next.Models=next.Models.Where(e=>referenced.Contains(e.Key)).ToDictionary(e=>e.Key,e=>e.Value);
        await model.SaveCandidateAsync(next,token);Set(s=>s with{Promoted=true,Before=before,After=after,Message="训练完成，验证集改善，模型已保存，可导出上传"});
    }
}
