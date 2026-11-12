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

public sealed record LocalTrainingRequest(Guid PlayerDeckId, Guid OpponentDeckId, int Games=20, int Parallel=2, bool Continuous=false, bool CompareLegacy=false, int BenchmarkSeed=18431, bool CompareStrategy=false, bool StrategyChoicesOnly=false, bool CompareLocalBaseline=false, int? StrategyTurnBudgetMs=null,bool CompareModelInputs=false,bool CompareCombatThreat=false,bool CompareValueSearch=false,bool CompareGeneratedTokens=false,bool CompareEternalEngine=false,bool CompareSummonSources=false,bool CompareCombatProjection=false)
{
    internal int MinimumGames => CompareStrategyExperience ? 8 : CompareStrategy || CompareWeaverEvolution ? 4 : 10;
    internal bool HasValidRunSize => Games>=MinimumGames && Games<=1000 && Parallel is >=1 and <=16
        && (!CompareStrategy || Games%4==0);
    public bool ComparePreviousCombatProjection {get;init;}
    public bool CompareRecoveryPolicy {get;init;}
    public bool CompareWeaverEvolution { get; init; }
    public bool CompareStrategyExperience { get; init; }
    internal bool HasValidExperienceComparison => !CompareStrategyExperience || (CompareStrategy && Games % 8 == 0
        && Parallel <= 2 && !Continuous && !ComparePreviousCombatProjection && !(this with { CompareStrategy = false }).IsBenchmark);
    internal bool HasValidWeaverComparison => !CompareWeaverEvolution || (Games % 4 == 0 && Parallel <= 2
        && !Continuous && !ComparePreviousCombatProjection && !StrategyChoicesOnly && StrategyTurnBudgetMs == null
        && !(this with { CompareWeaverEvolution = false }).IsBenchmark);
    internal bool IsBenchmark => CompareWeaverEvolution || CompareLegacy || CompareStrategy || CompareLocalBaseline || CompareModelInputs || CompareCombatThreat || CompareValueSearch || CompareGeneratedTokens || CompareEternalEngine || CompareSummonSources || CompareCombatProjection || CompareRecoveryPolicy || CandidateArtifact != null;
    [System.Text.Json.Serialization.JsonIgnore] internal AiModelArtifact? CandidateArtifact {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] internal AiModelArtifact? IncumbentArtifact {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] internal IReadOnlyDictionary<Guid,string>? ExpectedDecks {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] internal AiDefinitionStamp? ExpectedRules {get;init;}
}
public sealed record StrategyMatchResult(int Episode, int ShuffleSeed, Guid DeckId, string DeckName, bool First,
    string Result, long ElapsedMs, AiStrategyPlanner.DecisionMetrics Turn, AiStrategyPlanner.DecisionMetrics Choice,long ValueSearchEvaluations=0,long GeneratedTokenTacticUses=0,AiEternalMatchFacts[]? EternalEngine=null,long GeneratedSummonTacticUses=0,AiCombatProjectionMatchFacts? CombatProjection=null,int DifferentCompletedActions=0,int DifferentCommittedOptions=0,AiBenchmarkOpening? Opening=null,int WeaverPreparations=0,bool? WithStrategyExperience=null,int ExperienceGames=0,AiCompletedActionFact[]? ExperienceOutcomeExamples=null,AiBenchmarkOpening? AfterMulligan=null, AiLeaderRecoveryMatchFacts? LeaderRecovery=null)
{
    public AiEvolutionMatchFacts? EvolutionEffects { get; init; }
}

public sealed record LocalTrainingStatus(bool Running=false,int Completed=0,int Failed=0,int Planned=0,int Samples=0,string Message="尚未开始训练",bool Promoted=false,double? Before=null,double? After=null,int Parallel=1,int Active=0,bool Continuous=false,int Round=1,int TotalCompleted=0,int Wins=0,int Losses=0,int Draws=0,string PlayerDeckName="",string OpponentDeckName="",Guid PlayerDeckId=default,Guid OpponentDeckId=default,int CandidateFirstGames=0,int CandidateFirstWins=0,int? BenchmarkSeed=null,int StrategyGames=0,int StrategyPlans=0,bool CompareStrategy=false,AiStrategyPlanner.StrategyReport? LatestStrategy=null,
    AiStrategyPlanner.DecisionMetrics? TurnDecisions=null, AiStrategyPlanner.DecisionMetrics? OptionDecisions=null, bool StrategyChoicesOnly=false, StrategyMatchResult[]? StrategyMatches=null, bool CompareLocalBaseline=false, int? StrategyTurnBudgetMs=null,bool CompareModelInputs=false,AiValueValidation[]? ModelValidations=null,AiModelEvaluation? ModelEvaluation=null,bool ValidatingModel=false,string? CandidateVersion=null,bool CompareCombatThreat=false,bool CompareValueSearch=false,long ValueSearchEvaluations=0,AiTrainingCoverage? TrainingPositions=null,bool SampleReplaySaved=false,bool CompareGeneratedTokens=false,long GeneratedTokenTacticUses=0,bool CompareEternalEngine=false,bool CompareSummonSources=false,long GeneratedSummonTacticUses=0,bool CompareCombatProjection=false,long CombatProjectionCommittedChanges=0,bool CompareRecoveryPolicy=false,bool CompareWeaverEvolution=false,bool CompareStrategyExperience=false)
{
    public AiStrategyExperienceBenchmark? StrategyExperienceBenchmark => CompareStrategyExperience && BenchmarkSeed is int historySeed
        ? AiStrategyExperienceBenchmark.Calculate(StrategyMatches ?? [], Planned, historySeed, PlayerDeckId, OpponentDeckId)
        : null;
    public AiStrategyBenchmarkSummary? StrategyBenchmark => (CompareStrategy || CompareWeaverEvolution) && !CompareStrategyExperience && BenchmarkSeed is int seed
        ? AiStrategyBenchmarkSummary.Calculate(StrategyMatches ?? [], Planned, seed, PlayerDeckId, OpponentDeckId)
        : null;
}
public sealed class LocalAiTrainingService(IServiceScopeFactory scopes,AiLearningModel model,IHostApplicationLifetime lifetime,ILogger<LocalAiTrainingService> logger,AiStrategyPlanner strategy,IConfiguration? configuration=null)
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
            if(!request.HasValidRunSize || !request.HasValidWeaverComparison || !request.HasValidExperienceComparison)throw new ArgumentOutOfRangeException(nameof(request));
            if(run is {IsCompleted:false})return false;
            cancellation?.Dispose();cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            status=new(true,0,0,request.Games,0,"正在运行本地自对战",Parallel:request.Parallel,Continuous:request.Continuous,PlayerDeckId:request.PlayerDeckId,OpponentDeckId:request.OpponentDeckId,BenchmarkSeed:request.IsBenchmark?request.BenchmarkSeed:null,CompareStrategy:request.CompareStrategy,StrategyChoicesOnly:request.CompareStrategy && request.StrategyChoicesOnly,CompareLocalBaseline:request.CompareLocalBaseline,StrategyTurnBudgetMs:request.StrategyTurnBudgetMs,CompareModelInputs:request.CompareModelInputs,CompareCombatThreat:request.CompareCombatThreat,CompareValueSearch:request.CompareValueSearch,CompareGeneratedTokens:request.CompareGeneratedTokens,CompareEternalEngine:request.CompareEternalEngine,CompareSummonSources:request.CompareSummonSources,CompareCombatProjection:request.CompareCombatProjection,CompareRecoveryPolicy:request.CompareRecoveryPolicy,CompareWeaverEvolution:request.CompareWeaverEvolution,CompareStrategyExperience:request.CompareStrategyExperience);
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
                Set(s=>s with{Round=s.Round+1,Completed=0,Failed=0,Samples=0,ModelValidations=null,ModelEvaluation=null,CandidateVersion=null,Promoted=false,Before=null,After=null,TrainingPositions=null,SampleReplaySaved=false,Message="正在开始下一轮连续训练"});
                await Task.Delay(500,token);
            } while(true);
        } catch(OperationCanceledException){Set(s=>s with{Message="训练已停止"});}
        finally {Set(s=>s with{Running=false,Active=0});}
    }
    async Task<bool> Train(Guid owner,LocalTrainingRequest request,CancellationToken token) {
        var samples=new List<AiTrainingSample>();var deckNames=new Dictionary<string,string>();
        var expectedDecks = request.ExpectedDecks == null ? new Dictionary<Guid,string>() : new Dictionary<Guid,string>(request.ExpectedDecks);
        try {
            var incumbent = request.IncumbentArtifact ?? model.Current ?? throw new InvalidOperationException("请先迁移模型");
            var rules = request.ExpectedRules ?? AiDefinitionRules.Read(GameDataProvider.Instance);
            if (!rules.Available) throw new InvalidOperationException("卡库规则指纹尚未就绪，未开始训练");
            bool SameRules() {
                var now = AiDefinitionRules.Read(GameDataProvider.Instance);
                return now.Available && now.Fingerprint == rules.Fingerprint && now.EngineFingerprint == rules.EngineFingerprint;
            }
            IReadOnlyDictionary<string, AiStrategyPlanner.BenchmarkExperience>? benchmark = null;
            var benchmarkFormats = new Dictionary<Guid, (CardClass, CardClass?, DeckEnvironment, DeckRule)>();
            if (request.CompareStrategy)
            {
                EnsureStrategyAvailable(request);
                using var preparation = scopes.CreateScope();
                var decks = preparation.ServiceProvider.GetRequiredService<IDeckService>();
                var firstDeck = await decks.GetAsync(new(owner, null), request.PlayerDeckId, token)
                    ?? throw new InvalidOperationException("训练卡组不存在");
                var secondDeck = await decks.GetAsync(new(owner, null), request.OpponentDeckId, token)
                    ?? throw new InvalidOperationException("对手卡组不存在");
                foreach (var deck in new[] { firstDeck, secondDeck })
                {
                    expectedDecks[deck.DeckId] = AiLearningModel.DeckKey(deck.Cards.SelectMany(card => Enumerable.Repeat(card.CardDefinitionId, card.Count)));
                    benchmarkFormats[deck.DeckId] = (deck.Class, deck.SecondaryClass, deck.Environment, deck.Rule);
                }
                Set(s => s with { Message = "正在准备本轮评测的经验和牌组攻略" });
                benchmark = await strategy.PrepareBenchmarkAsync(firstDeck, secondDeck, token, request.CompareStrategyExperience);
            }
            await Parallel.ForEachAsync(Enumerable.Range(0,request.Games),new ParallelOptions{MaxDegreeOfParallelism=request.Parallel,CancellationToken=token},async (episode,workerToken) => {
                token.ThrowIfCancellationRequested();
                EnsureStrategyAvailable(request);
                if (!SameRules() || !ReferenceEquals(model.Current, incumbent))
                    throw new InvalidOperationException("训练期间卡库或当前模型发生变化，已停止本轮并保留原模型");
                using var scope=scopes.CreateScope();var sp=scope.ServiceProvider;
                var decks=sp.GetRequiredService<IDeckService>();var games=sp.GetRequiredService<IGameService>();var matches=sp.GetRequiredService<IMatchService>();
                var a=await decks.GetAsync(new(owner,null),request.PlayerDeckId,token)??throw new InvalidOperationException("训练卡组不存在");
                var b=await decks.GetAsync(new(owner,null),request.OpponentDeckId,token)??throw new InvalidOperationException("对手卡组不存在");
                Set(s=>s with{PlayerDeckName=a.Name,OpponentDeckName=b.Name});
                lock(sync) foreach(var d in new[]{a,b}) {
                    var key = AiLearningModel.DeckKey(d.Cards.SelectMany(c=>Enumerable.Repeat(c.CardDefinitionId,c.Count)));
                    if ((expectedDecks.TryGetValue(d.DeckId, out var expected) && expected != key)
                        || (benchmarkFormats.TryGetValue(d.DeckId, out var format)
                            && format != (d.Class, d.SecondaryClass, d.Environment, d.Rule)))
                        throw new InvalidOperationException("训练牌组发生变化，已停止本轮并保留原模型");
                    expectedDecks[d.DeckId] = key; deckNames[key] = d.Name;
                }
                // Each four-game block gives each policy the same deck and first-player combinations.
                var (matchSlot, withExperience) = request.CompareStrategyExperience
                    ? AiStrategyExperienceBenchmark.Schedule(episode) : (episode, false);
                var shuffleSeed = unchecked(request.BenchmarkSeed + matchSlot / 4);
                var hostUsesA = request.IsBenchmark ? matchSlot / 4 % 2 == 0 : matchSlot % 2 == 0;
                var aGoesFirst = matchSlot % 4 < 2;
                var created = matches.Create(Guid.NewGuid(), false, devMatchConfig: new()
                {
                    IgnoreDeckFormatConsistency = true,
                    IsLocalAiTraining = true,
                    BenchmarkHostGoesFirst = request.IsBenchmark ? hostUsesA == aGoesFirst : null,
                    BenchmarkShuffleSeed = request.IsBenchmark ? shuffleSeed : null
                });
                var room=created.Room??throw new InvalidOperationException(created.Message);
                var matchTimer = Stopwatch.StartNew();
                Set(s=>s with{Active=s.Active+1});
                try {
                    var joined=matches.TryJoin(room.RoomId,room.JoinCode,Guid.NewGuid());var guest=joined.Room?.GuestPlayerId??throw new InvalidOperationException(joined.Message);
                    MatchDeckSelection Selection(DeckDetailDto d)=>new(){UserId=owner,DeckId=d.DeckId,DeckClass=d.Class,SecondaryClass=d.SecondaryClass,Environment=d.Environment,Rule=d.Rule};
                    var first=hostUsesA?a:b;var second=hostUsesA?b:a;
                    var playerSeat=hostUsesA?room.HostPlayerId:guest;
                    var candidateSeat=matchSlot%2==0?playerSeat:(playerSeat==room.HostPlayerId?guest:room.HostPlayerId);
                    if(!matches.SubmitDeckSelection(room.RoomId,room.HostPlayerId,Selection(first)).Success||!matches.SubmitDeckSelection(room.RoomId,guest,Selection(second)).Success)throw new InvalidOperationException("训练卡组选择失败");
                    matches.MarkReady(room.RoomId,room.HostPlayerId);matches.MarkReady(room.RoomId,guest);
                    var started=matches.Start(room.RoomId,room.HostPlayerId);var id=started.Room?.ActiveGameId??throw new InvalidOperationException(started.Message);
                    var game=await games.GetOrCreateGameStateAsync(id,room.HostPlayerId,guest,false,sp)??throw new InvalidOperationException("训练对局初始化失败，请检查卡组完整性");
                    var opening = (request.CompareStrategy || request.CompareWeaverEvolution) ? AiBenchmarkOpening.Capture(game, playerSeat) : null;
                    using var comparison = request.CandidateArtifact == null ? null
                        : model.BeginComparison(game, candidateSeat, request.CandidateArtifact, incumbent);
                    if (request.CompareWeaverEvolution) AiWeaverPlan.BindBenchmark(game, candidateSeat);
                    if(request.CompareRecoveryPolicy)AiRecoveryPolicy.For(game).DisabledPlayer=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareLegacy)game.LegacyAiPlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareModelInputs)game.LegacyModelInputsPlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareCombatThreat)game.LegacyCombatThreatPlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareValueSearch)game.ValueSearchComparisonPlayerId=candidateSeat;
                    if(request.CompareCombatProjection){if(request.ComparePreviousCombatProjection)AiCombatEvaluation.StartPrevious(game,candidateSeat);else AiCombatEvaluation.Start(game,candidateSeat);}
                    if(request.CompareSummonSources)AiSummonSourceState.For(game).DisabledPlayer=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareGeneratedTokens)game.LegacyGeneratedTokenPlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    if(request.CompareEternalEngine)game.LegacyEternalEnginePlayerId=candidateSeat==room.HostPlayerId?guest:room.HostPlayerId;
                    var engineObservation=request.CompareEternalEngine||request.CompareRecoveryPolicy?new AiEternalEvaluation(game,candidateSeat):null;
                    var experienceGames = 0;
                    if (request.CompareStrategy)
                    {
                        if (configuration?.GetValue("AiStrategy:MeasureLeaderRecovery", false) == true)
                            AiLeaderRecoveryEvaluation.Bind(game, candidateSeat);
                        var measureRefill = configuration?.GetValue("AiStrategy:MeasureEndTurnRefill", false) == true;
                        if (measureRefill || configuration?.GetValue("AiStrategy:MeasureEvolutionEffects", false) == true)
                            AiEvolutionEvaluation.Bind(game, candidateSeat, measureRefill);
                        game.StrategyBenchmarkPlayerId = candidateSeat;
                        game.StrategyChoicesOnly = request.StrategyChoicesOnly;
                        game.StrategyTurnBudgetMs = request.StrategyTurnBudgetMs;
                        experienceGames = strategy.BindBenchmarkGame(game, candidateSeat, benchmark!, request.CompareStrategyExperience ? withExperience : null);
                        game.StrategyBenchmarkDeckName = candidateSeat == room.HostPlayerId ? first.Name : second.Name;
                    }
                    var mapper=sp.GetRequiredService<IMapper>();var accessor=sp.GetRequiredService<IGameContextAccessor>();
                    GameStateDto Snapshot(Guid actor){using var context=new GameContext(sp,mapper,accessor,game,actor,token);return GameStateProjection.Create(game,context);}
                    var positions = request.IsBenchmark ? null : new AiTrainingPositions(episode, request.BenchmarkSeed, game.Player1.PlayerId, game.Player2.PlayerId);
                    using var observationLease = positions?.Attach(game, Snapshot);
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
                        engineObservation?.AfterMulligan(player.PlayerId);
                    }
                    var afterMulligan = opening != null ? AiBenchmarkOpening.CaptureAfterMulligan(game, playerSeat) : null;
                    for(var turn=0;turn<100&&!game.IsGameOver;turn++) {
                        deadline.Token.ThrowIfCancellationRequested();var actor=game.CurrentPlayerId??throw new InvalidOperationException("训练缺少当前玩家");
                        if(positions!=null)positions.Record(actor,false,AiFeatures.Value(Snapshot(actor),actor));
                        EnsureStrategyAvailable(request);
                        var observedTurn=game.GetPlayerState(actor)!.TurnNumber;
                        await sp.GetRequiredService<AiTurnExecutor>().ExecuteTurnAsync(id,actor,deadline.Token);
                        engineObservation?.AfterOwnTurn(game,actor,observedTurn);
                        EnsureStrategyAvailable(request);
                        if (request.CompareStrategy && actor == candidateSeat && strategy.GetReport(game) is { } report)
                            Set(s => s with { LatestStrategy = report });
                    }
                    if(!game.IsGameOver)throw new InvalidOperationException("训练对局超过回合上限");
                    lock(sync) {
                        var playerDeckKeys = new[] { game.Player1, game.Player2 }.ToDictionary(player => player.PlayerId,
                            player => AiLearningModel.DeckKey(player.InitialDeckDefinitionIds));
                        if (positions != null)
                        {
                            samples.AddRange(positions.Samples(playerDeckKeys, game.WinnerId));
                            status = status with { TrainingPositions = (status.TrainingPositions ?? new()).Merge(positions.Coverage) };
                        }
                        var scoredSeat=request.IsBenchmark?candidateSeat:playerSeat;
                        var draw=game.WinnerId==null||game.WinnerId==Guid.Empty;
                        status=status with{TotalCompleted=status.TotalCompleted+1,Wins=status.Wins+(!draw&&game.WinnerId==scoredSeat?1:0),Losses=status.Losses+(!draw&&game.WinnerId!=scoredSeat?1:0),Draws=status.Draws+(draw?1:0)};
                        if (request.CompareStrategy || request.CompareLocalBaseline || request.CompareModelInputs || request.CompareCombatThreat || request.CompareValueSearch || request.CompareGeneratedTokens || request.CompareEternalEngine || request.CompareSummonSources || request.CompareCombatProjection || request.CompareRecoveryPolicy || request.CompareWeaverEvolution || request.CandidateArtifact != null)
                        {
                            var plans = strategy.CountPlans(game);
                            var metrics = strategy.GetDecisionMetrics(game);
                            var differences = request.CompareStrategy ? strategy.CountDecisionDifferences(game) : (Actions: 0, Options: 0);
                            var candidateDeck = matchSlot % 2 == 0 ? a : b;
                            var matchResult = new StrategyMatchResult(episode + 1,
                                shuffleSeed, candidateDeck.DeckId, candidateDeck.Name,
                                game.FirstPlayerId == candidateSeat, draw ? "draw" : game.WinnerId == candidateSeat ? "win" : "loss",
                                matchTimer.ElapsedMilliseconds, metrics.Turn, metrics.Choice, Interlocked.Read(ref game.ValueSearchEvaluations),Interlocked.Read(ref game.GeneratedTokenTacticUses),engineObservation?.Snapshot(),Interlocked.Read(ref AiSummonSourceState.For(game).Uses),AiCombatEvaluation.Snapshot(game),
                                differences.Actions, differences.Options, opening, AiWeaverPlan.PreparationCount(game),
                                request.CompareStrategyExperience ? withExperience : null, experienceGames,
                                request.CompareStrategyExperience ? strategy.ExperienceOutcomeExamples(game) : null, afterMulligan,
                                AiLeaderRecoveryEvaluation.For(game)?.Snapshot())
                            { EvolutionEffects = AiEvolutionEvaluation.For(game)?.Snapshot(game) };
                            status = status with
                            {
                                StrategyMatches = [.. status.StrategyMatches ?? [], matchResult],
                                ValueSearchEvaluations = status.ValueSearchEvaluations + matchResult.ValueSearchEvaluations,
                                GeneratedTokenTacticUses = status.GeneratedTokenTacticUses + matchResult.GeneratedTokenTacticUses,
                                GeneratedSummonTacticUses = status.GeneratedSummonTacticUses + matchResult.GeneratedSummonTacticUses,
                                CombatProjectionCommittedChanges=status.CombatProjectionCommittedChanges+(matchResult.CombatProjection?.CommittedChangedSteps??0),
                                StrategyGames = status.StrategyGames + (plans > 0 || metrics.Choice.Accepted > 0 ? 1 : 0),
                                StrategyPlans = status.StrategyPlans + plans,
                                TurnDecisions = (status.TurnDecisions ?? new()).Merge(metrics.Turn),
                                OptionDecisions = (status.OptionDecisions ?? new()).Merge(metrics.Choice)
                            };
                        }
                        if (request.IsBenchmark && game.FirstPlayerId == candidateSeat)
                            status = status with { CandidateFirstGames = status.CandidateFirstGames + 1,
                                CandidateFirstWins = status.CandidateFirstWins + (game.WinnerId == candidateSeat ? 1 : 0) };
                        status=status with{Completed=status.Completed+1,Samples=samples.Count,Message=request.CandidateArtifact != null ? $"候选模型实战验证 {status.Completed+1}/{request.Games} 局" : $"自对战完成 {status.Completed+1}/{request.Games} 局"};
                    }
                } catch(OperationCanceledException) when(!token.IsCancellationRequested) {Set(s=>s with{Failed=s.Failed+1,Message="一局训练超时，继续下一局"});}
                  catch(Exception e) when(e is not OperationCanceledException and not StrategyUnavailableException) {logger.LogWarning(e,"Local AI training episode failed");Set(s=>s with{Failed=s.Failed+1,Message=e.Message});}
                finally {matches.Close(room.RoomId,"本地AI训练结束");Set(s=>s with{Active=Math.Max(0,s.Active-1)});}
            });
            token.ThrowIfCancellationRequested();
            if (request.CandidateArtifact != null) return true;
            if (!SameRules() || !ReferenceEquals(model.Current, incumbent))
                throw new InvalidOperationException("训练期间卡库或当前模型发生变化，未替换模型");
            if(request.CompareStrategy)
            {
                Set(s => s with { Message = request.CompareStrategyExperience ? "大模型历史经验对照结束" : "大模型对照评测结束" });
                return true;
            }
            if (request.CompareWeaverEvolution)
            {
                Set(s => s with { Message = "织法进化顺序对照结束" });
                return true;
            }
            if(request.CompareRecoveryPolicy){
                Set(s=>s with{Message=$"手牌与回血策略对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平；相同冻结模型，不调用大模型、不拟合权重"});return true;
            }
            if(request.CompareCombatProjection) {
                Set(s=>s with{Message=$"{(request.ComparePreviousCombatProjection?"新旧有限攻击前预测对照":"攻击前预测对照")}：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，确认执行不同首步 {s.CombatProjectionCommittedChanges} 次；不训练或替换权重"});return true;
            }
            if(request.CompareSummonSources) {
                Set(s=>s with{Message=$"召唤斩杀出牌对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，实际执行源卡 {s.GeneratedSummonTacticUses} 次"});return true;
            }
            if(request.CompareEternalEngine) {
                if(!(Status.StrategyMatches??[]).Any(match=>match.EternalEngine?.Length>0))throw new InvalidOperationException("对照未包含具备芮丝与夜恸的有效永火牌组");
                Set(s=>s with{Message=$"芮丝资源核心对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，失败 {s.Failed} 局"});return true;
            }
            if(request.CompareGeneratedTokens) {
                Set(s=>s with{Message=$"本地生成疾驰路线对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，实际执行路线 {s.GeneratedTokenTacticUses} 次"});return true;
            }
            if(request.CompareValueSearch) {
                if(Status.ValueSearchEvaluations == 0)throw new InvalidOperationException("未执行局面模型搜索，无法作为有效对照");
                Set(s=>s with{Message=$"本地模型搜索对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，局面评估 {s.ValueSearchEvaluations} 次"});return true;
            }
            if(request.CompareCombatThreat) {
                Set(s=>s with{Message=$"基础交战威胁对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，失败 {s.Failed} 局"});return true;
            }
            if(request.CompareModelInputs)
            {
                Set(s=>s with{Message=$"本地模型输入对照：{s.Wins} 胜 {s.Losses} 负 {s.Draws} 平，失败 {s.Failed} 局"});
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
            await Fit(owner,request,incumbent,rules,expectedDecks,samples,deckNames,token);
            return true;
        } catch(StrategyUnavailableException e){Set(s=>s with{Message=e.Message});return false;}
          catch(OperationCanceledException){Set(s=>s with{Message="训练已停止"});return false;}
          catch(Exception e){logger.LogError(e,"Local AI training failed");Set(s=>s with{Message="训练失败："+e.Message});return false;}
    }
    async Task Fit(Guid owner, LocalTrainingRequest request, AiModelArtifact artifact, AiDefinitionStamp rules,
        Dictionary<Guid,string> deckIdentities, List<AiTrainingSample> samples, Dictionary<string,string> deckNames, CancellationToken token) {
        if (configuration?.GetValue<bool>("LocalAiTraining:SaveReplay") == true)
        {
            var replay = AiTrainingReplayStore.Create(artifact, rules, samples, Status.TrainingPositions ?? new());
            await AiTrainingReplayStore.SaveAsync(model.ModelPath + ".training-samples.json", replay, token);
            Set(status => status with {SampleReplaySaved=true});
        }
        var result = AiValueTraining.Fit(artifact, samples, deckNames.Keys.ToHashSet(), token);
        Set(s => s with { ModelValidations = result.Validations.Select(item => item with { Scope = item.Scope == "global" ? "通用判断" : deckNames.GetValueOrDefault(item.Scope, "牌组专项") }).ToArray() });
        if (result.Candidate == null) {
            Set(s => s with { Message = "独立验证未改善或有效对局不足，保留原模型" }); return;
        }
        var accepted = result.Validations.Where(item => item.Accepted).ToArray();
        await model.SaveTrialAsync(result.Candidate, token);
        var pending = AiModelStrengthGate.Evaluate(result.Candidate.Version, artifact.Version, [], 0,
            deckIdentities.Keys.ToHashSet()) with { Message = "候选已保存，尚未完成实战验证" };
        await model.SaveEvaluationAsync(pending, token);
        var trained = Status with { Before = accepted.Average(item => item.Before), After = accepted.Average(item => item.After),
            CandidateVersion = result.Candidate.Version, ModelEvaluation = pending };
        Set(_ => trained with { Completed=0,Failed=0,Samples=0,Planned=AiModelStrengthGate.Games,Parallel=1,
            Active=0,Wins=0,Losses=0,Draws=0,TotalCompleted=0,CandidateFirstGames=0,CandidateFirstWins=0,
            StrategyMatches=[],TurnDecisions=null,OptionDecisions=null,StrategyGames=0,StrategyPlans=0,TrainingPositions=null,SampleReplaySaved=false,
            ValidatingModel=true,Message="正在验证候选模型：交换牌组与先后手，对战旧模型" });
        AiModelEvaluation? evaluation = null; var promoted = false;
        try {
            var gateRequest = request with { Games=AiModelStrengthGate.Games,Parallel=1,Continuous=false,
                CompareLegacy=false,CompareStrategy=false,CompareLocalBaseline=false,CompareModelInputs=false,CompareCombatThreat=false,CompareValueSearch=false,CompareGeneratedTokens=false,CompareEternalEngine=false,CompareSummonSources=false,CompareCombatProjection=false,CompareRecoveryPolicy=false,
                StrategyChoicesOnly=false,StrategyTurnBudgetMs=null,
                BenchmarkSeed=unchecked(request.BenchmarkSeed+97301+trained.Round*97),
                CandidateArtifact=result.Candidate,IncumbentArtifact=artifact,ExpectedDecks=deckIdentities,ExpectedRules=rules };
            var complete = await Train(owner, gateRequest, token);
            token.ThrowIfCancellationRequested();
            var gateStatus=Status;
            evaluation=AiModelStrengthGate.Evaluate(result.Candidate.Version,artifact.Version,
                gateStatus.StrategyMatches??[],gateStatus.Failed,deckIdentities.Keys.ToHashSet());
            var now=AiDefinitionRules.Read(GameDataProvider.Instance);
            if (!complete || !ReferenceEquals(model.Current,artifact) || !now.Available
                || now.Fingerprint!=rules.Fingerprint || now.EngineFingerprint!=rules.EngineFingerprint)
                evaluation=evaluation with { Accepted=false,Message="实战验证未完成或规则/模型已改变，保留旧模型" };
            await model.SaveEvaluationAsync(evaluation,token);
            if(evaluation.Accepted) { await model.SaveCandidateAsync(result.Candidate,token);promoted=true; }
        } finally {
            Set(_=>trained with { ModelEvaluation=evaluation??pending,ValidatingModel=false,Promoted=promoted,
                Message=promoted ? "候选通过独立误差验证与本组实战验证，已替换模型"
                    : (evaluation?.Message??"实战验证未完成，保留旧模型")+"；学习候选和验证记录已保存" });
        }
    }
}
