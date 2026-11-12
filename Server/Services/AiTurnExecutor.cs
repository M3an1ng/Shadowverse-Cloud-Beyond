using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class AiTurnExecutor
{
    private const int ActionObservationDelayMilliseconds = 1000;
    private const int MaxMirageActionsPerRound = 20;
    private readonly IGameService _gameService;
    private readonly IAiService _aiService;
    private readonly ILogger<AiTurnExecutor> _logger;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _serviceProvider;
    private readonly AiLearningModel? _model;
    private readonly bool _valueSearchEnabled;
    private readonly bool _projectCombatEffects;
    private readonly bool _prepareWeaverEvolution;
    private readonly bool _projectLeaderRecovery;
    private readonly bool _projectEvolutionEffects;
    private readonly bool _projectEndTurnRefill;
    private readonly bool _benchmarkAvoidDeckExhaustion;
    private readonly bool _scopeRecoveryReactions;

    public AiTurnExecutor(
        IGameService gameService,
        IAiService aiService,
        ILogger<AiTurnExecutor> logger,
        IMapper mapper,
        IServiceProvider serviceProvider,
        AiLearningModel? model = null,
        IConfiguration? configuration = null)
    {
        _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _model = model;
        _valueSearchEnabled = configuration?.GetValue("AiModel:SearchValueEnabled", false) == true;
        _projectCombatEffects=configuration?.GetValue("AiStrategy:ProjectCombatEffects",true)??true;
        _prepareWeaverEvolution = configuration?.GetValue("AiStrategy:PrepareWeaverEvolution", true) ?? true;
        _projectEvolutionEffects = configuration?.GetValue("AiStrategy:ProjectEvolutionEffects", true) ?? true;
        _projectEndTurnRefill = configuration?.GetValue("AiStrategy:ProjectEndTurnRefill", true) ?? true;
        _benchmarkAvoidDeckExhaustion = configuration?.GetValue("AiStrategy:BenchmarkAvoidDeckExhaustion",true) ?? true;
        _projectLeaderRecovery = configuration?.GetValue("AiStrategy:ProjectLeaderRecovery", true) ?? true;
        _scopeRecoveryReactions = configuration?.GetValue("AiStrategy:ScopeRecoveryReactions", true) ?? true;
    }

    public async Task ExecuteTurnAsync(string gameId, Guid aiPlayerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var game = _gameService.GetGameState(gameId);
        if (game?.Mode == GameMode.Mirage)
        {
            await ExecuteMirageRecruitTurnAsync(gameId, aiPlayerId, game.Mirage!.RoundNumber,
                game.GetPlayerState(aiPlayerId)!.TurnNumber, cancellationToken);
            return;
        }
        if (game == null || game.IsGameOver || game.CurrentPlayerId != aiPlayerId)
        {
            return;
        }

        using var recovery=AiRecoveryPolicy.Use(game,aiPlayerId);
        using var eternal = AiEternalEngine.Use(game,aiPlayerId);
        using var combatComparison = AiCombatThreat.UseLegacy(game.IsLocalAiTraining && game.LegacyCombatThreatPlayerId == aiPlayerId);
        var useLegacyPolicy = game.LegacyAiPlayerId == aiPlayerId;
        var evolutionBenchmark = AiEvolutionEvaluation.For(game);
        var evolutionEvaluation = evolutionBenchmark?.Candidate == aiPlayerId ? evolutionBenchmark : null;
        var projectEvolution = _projectEvolutionEffects && (evolutionBenchmark?.RefillOnly != false || evolutionEvaluation != null);
        var projectRefill = _projectEndTurnRefill && (evolutionBenchmark?.RefillOnly != true || evolutionEvaluation != null);
        var turnNumber = game.GetPlayerState(aiPlayerId)!.TurnNumber;
        var trainingActions = 0;
        var allowStrategy = !useLegacyPolicy && (!game.IsLocalAiTraining || game.StrategyBenchmarkPlayerId == aiPlayerId);
        var planner = allowStrategy ? _serviceProvider.GetService<AiStrategyPlanner>() : null;
        AiStrategyPlanner.TurnPlan? strategy = null;
        var strategyChecked = false;
        var unavailableAbilities = new HashSet<Guid>();
        while (IsCurrentTurn(game, aiPlayerId, turnNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.IsLocalAiTraining && ++trainingActions > 128) throw new InvalidOperationException("训练回合动作过多");
            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var accessor = services.GetRequiredService<IGameContextAccessor>();
            GameStateDto snapshot;
            long decisionRevision;
            List<AbilityAction> abilityActions;
            var evolutionValues = new Dictionary<Guid, (double Evolve, double SuperEvolve)>();
            AiTacticalSearch.EndTurnForecast? endTurn = null;
            AiCombatProjection.Frame? combatRules=null;
            AiTacticalSearch.EndTurnForecast? observedEnding = null;
            AiCombatProjection.Frame? observedCombat = null;
            AiCombatEvaluation.Decision? projectionDecision=null;
            AiStrategyPlanner.HandLossRule[] handLossRules=[];
            AiRuntimeAbilityFacts.Catalog? runtimeRules=null;
            var generatedTokenLines = new List<(Guid Source, AiGeneratedTokenTactics.Recipe Recipe)>();
            var generatedSummonLines = new List<(Guid Source, AiGeneratedTokenTactics.SummonAttack Summon)>();
            using (var context = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken))
            {
                snapshot = GameStateProjection.Create(game, context);
                decisionRevision=game.StateRevision;
                abilityActions = await GetAbilityActionsAsync(context, snapshot, aiPlayerId, unavailableAbilities, useLegacyPolicy);
                if (!useLegacyPolicy && snapshot.Phase == GamePhase.Main)
                {
                    var player = snapshot.Players.First(p => p.PlayerId == aiPlayerId);
                    endTurn = await AiEndTurnForecast.ReadForPolicyAsync(context, player, projectRefill);
                    if(AiCombatEvaluation.Enabled(game,aiPlayerId,_projectCombatEffects))combatRules=AiCombatProjection.Read(context,snapshot,aiPlayerId);
                    if (evolutionEvaluation != null)
                    {
                        observedEnding = endTurn;
                        observedCombat = combatRules;
                        evolutionEvaluation.Observe(turnNumber, player, observedCombat, observedEnding);
                        if (evolutionEvaluation.RefillOnly && !_benchmarkAvoidDeckExhaustion && endTurn.DeckExhaustion is { } shortage)
                        {
                            // The control retains normal refill forecasts. Before deck-loss handling,
                            // combined shortages also made damage ordering unavailable.
                            var canReadDamage = shortage.LaterDraws.Length==0;
                            endTurn=endTurn with {DeckExhaustion=null,LastHandPlayIntoExhaustion=null,
                                Damage=canReadDamage?endTurn.Damage:[],EmptyHandDamage=canReadDamage?endTurn.EmptyHandDamage:[]};
                        }
                    }
                    if (!projectEvolution)
                    {
                        if (endTurn.HandClearEvolutions.Count > 0)
                            endTurn = endTurn with { HandClearEvolutions = [], EmptyHandDamage = [] };
                        combatRules = combatRules?.WithoutEvolutionReplacements();
                    }
                    foreach (var spell in player.Hand.Where(card => card.Type == CardType.Spell))
                    {
                        var actual = game.FindCardInstance(spell.InstanceId);
                        if (game.IsLocalAiTraining && game.LegacyGeneratedTokenPlayerId == aiPlayerId) continue;
                        if (actual == null || actual.IsSilenced || AiGeneratedTokenTactics.HasInterference(context, actual)) continue;
                        var effective = context.AbilityManager.GetEffectiveAbilitiesForCard(actual.InstanceId, context).OfType<Ability>().Select(ability => ability.Config);
                        foreach (var recipe in AiGeneratedTokenTactics.Read(GameDataProvider.Instance, spell.CardId, effective))
                            generatedTokenLines.Add((spell.InstanceId, recipe));
                        var enemy=snapshot.Players.First(p=>p.PlayerId!=aiPlayerId);
                        foreach(var summon in AiGeneratedTokenTactics.ReadSummonLethals(context,player,enemy,actual))
                            generatedSummonLines.Add((spell.InstanceId,summon));
                    }
                    if(!strategyChecked && planner!=null) {
                        handLossRules=await AiPlaySequence.ReadHandLossAsync(context,player);
                        runtimeRules=AiRuntimeAbilityFacts.Read(context,snapshot,aiPlayerId);
                    }
                    if (player.CanEvolve || player.CanSuperEvolve)
                        await EstimateEvolutionEffectsAsync(context, player, evolutionValues);
                }
            }

            var abilityAction = useLegacyPolicy
                ? abilityActions.OrderBy(action => action.Cost > 0).ThenByDescending(action => action.Score).FirstOrDefault()
                : abilityActions.MaxBy(action => action.Score);
            var abilityCandidates = abilityActions.OrderByDescending(action => action.Score)
                .Select(action => action.Candidate).ToArray();

            var stormPlays = new List<AiTacticalSearch.Move>();
            AiTacticalSearch.Plan? localTacticalPlan=null;
            AiTacticalSearch.Plan? verifiedRecovery = null;
            var recoveryBenchmark = AiLeaderRecoveryEvaluation.For(game);
            var recoveryEvaluation = recoveryBenchmark?.Candidate == aiPlayerId ? recoveryBenchmark : null;
            AiLocalTacticalFacts.Facts? localTacticalFacts=null;
            AiTacticalCandidates.Frame? tacticalChoices=null;
            Func<GameStateDto,double>? searchValue=null;
            if(!useLegacyPolicy&&snapshot.Phase==GamePhase.Main)
            {
                var player=snapshot.Players.First(p=>p.PlayerId==aiPlayerId);
                foreach(var card in player.Hand.Where(c=>c.Type==CardType.Follower&&c.Keywords.Contains(CardKeyword.Storm)))
                {
                    var playable=await _gameService.GetCardPlayabilityAsync(gameId,aiPlayerId,card.InstanceId,cancellationToken);
                    var baseMode=playable.PlayModes.FirstOrDefault(mode=>mode.Mode==PlayMode.Base);
                    if(playable.SelectedPlayMode!=PlayMode.Base||!playable.IsPlayable||baseMode is not {IsAvailable:true}||baseMode.EffectiveCost<0)continue;
                    stormPlays.Add(new("PlayCard",new PlayCardActionDto{CardUid=card.InstanceId,PlayMode=PlayMode.Base,PaidCost=baseMode.EffectiveCost},
                        card with{CurrentCost=baseMode.EffectiveCost}));
                }
                var useValueSearch=game.IsLocalAiTraining&&game.ValueSearchComparisonPlayerId.HasValue
                    ?game.ValueSearchComparisonPlayerId==aiPlayerId:_valueSearchEnabled;
                var capturedValue=useValueSearch?_model?.CaptureValueEvaluator(game,aiPlayerId):null;
                if(capturedValue!=null)searchValue=projected=>{
                    Interlocked.Increment(ref game.ValueSearchEvaluations);return capturedValue(projected);
                };
                localTacticalPlan=AiTacticalSearch.FindWithCombat(snapshot,aiPlayerId,cancellationToken,stormPlays,evolutionValues,endTurn,null,searchValue,combatRules);
                if(AiCombatEvaluation.NeedsComparison(game,aiPlayerId)&&combatRules?.RuleCount>0) {
                    AiCombatProjection.Frame? baselineRules=null;
                    if(AiCombatEvaluation.ComparePrevious(game,aiPlayerId)) {
                        using var baselineContext=new GameContext(services,_mapper,accessor,game,aiPlayerId,cancellationToken);
                        baselineRules=AiCombatProjection.ReadPrevious(baselineContext,snapshot,aiPlayerId);
                    }
                    var baseline=AiTacticalSearch.FindWithCombat(snapshot,aiPlayerId,cancellationToken,stormPlays,evolutionValues,endTurn,null,capturedValue,baselineRules);
                    projectionDecision=AiCombatEvaluation.Record(game,aiPlayerId,snapshot,decisionRevision,baseline,localTacticalPlan);
                }
                var recoveryEnabled = _projectLeaderRecovery && (recoveryBenchmark == null || recoveryEvaluation != null);
                if ((recoveryEnabled || recoveryEvaluation != null) && (localTacticalPlan?.Priority ?? 0) == 0
                    && HasVisibleLethalThreat(player, snapshot.Players.Single(p => p.PlayerId != aiPlayerId)))
                {
                    using var recoveryContext = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken);
                    verifiedRecovery = await AiLeaderRecovery.FindAsync(recoveryContext, snapshot, aiPlayerId, _gameService,
                        cancellationToken, recoveryEvaluation, _scopeRecoveryReactions);
                    if (recoveryEnabled && verifiedRecovery != null
                        && !AiTacticalSearch.ClearsHandIntoDeckLoss(player,endTurn,verifiedRecovery.First.Data))
                        localTacticalPlan = verifiedRecovery;
                }
                if(!strategyChecked&&planner?.IsConfigured==true)
                {
                    using var factsContext=new GameContext(services,_mapper,accessor,game,aiPlayerId,cancellationToken);
                    localTacticalFacts=AiLocalTacticalFacts.ReadWithCombat(factsContext,snapshot,aiPlayerId,localTacticalPlan,decisionRevision,combatRules);
                    tacticalChoices=AiTacticalCandidates.ReadWithCombat(factsContext,snapshot,aiPlayerId,localTacticalPlan,decisionRevision,combatRules);
                }
            }

            if(game.StateRevision!=decisionRevision)continue;
            if (!strategyChecked && planner != null && snapshot.Phase == GamePhase.Main)
            {
                strategy = await planner.PlanAsync(game, snapshot, aiPlayerId, cancellationToken,
                    (uid, token) => _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, uid, token),
                    abilityCandidates,
                    (uid, mode, token) => _gameService.GetCardPlayabilityForModeAsync(gameId, aiPlayerId, uid, mode, token),handLossRules,
                    AiEndTurnForecast.ForModel(endTurn,snapshot,aiPlayerId),runtimeRules,localTacticalFacts,tacticalChoices);
                strategyChecked = true;
                if (!IsCurrentTurn(game, aiPlayerId, turnNumber)) return;
                if(game.StateRevision!=decisionRevision)continue;
            }

            PlayCardActionDto? weaverPreparation = null;
            Guid? generatedStormSource = null;
            int? generatedStormToken = null;
            Guid? generatedSummonSource = null;
            var actionType = "EndTurn";
            object actionData = new { };
            var bestScore = 0d;
            var localType = "EndTurn";
            object localData = new { };
            var localBestScore = 0d;

            void ConsiderLocal(string type, object data, double score)
            {
                if (score > localBestScore) (localType, localData, localBestScore) = (type, data, score);
            }
            void Consider(string type, object data, double score, double? localScore = null)
            {
                ConsiderLocal(type, data, localScore ?? score);
                if (score <= bestScore) return;
                (actionType, actionData, bestScore) = (type, data, score);
            }

            if (game.IsLocalAiTraining && useLegacyPolicy)
            {
                (actionType, actionData) = await ChooseLegacyActionAsync(snapshot, aiPlayerId, abilityAction);
            }
            else if (snapshot.Phase == GamePhase.Main)
            {
                var player = snapshot.Players.First(p => p.PlayerId == aiPlayerId);
                var opponent = snapshot.Players.First(p => p.PlayerId != aiPlayerId);
                var defensiveEmergency = HasVisibleLethalThreat(player, opponent);
                var playStrategy = AiPlayCandidates.ForEmergency(strategy,defensiveEmergency,localTacticalPlan?.PreventsVisibleLethal==true||localTacticalPlan?.ProjectsLethal==true);
                var intent = playStrategy?.Focus;
                var localPlan = localTacticalPlan;
                var plan = intent==null?localPlan:AiTacticalSearch.FindWithCombat(snapshot,aiPlayerId,cancellationToken,
                    stormPlays,evolutionValues,endTurn,intent,searchValue,combatRules);
                static double CombatScore(AiTacticalSearch.Plan plan) =>
                    plan.ProjectsLethal ? 1000 : plan.PreventsVisibleLethal ? 850 : plan.Gain * .55;
                if (plan != null)
                    Consider(plan.First.Type, plan.First.Data, CombatScore(plan), double.NegativeInfinity);
                if (localPlan != null)
                    ConsiderLocal(localPlan.First.Type, localPlan.First.Data, CombatScore(localPlan));

                async Task<double> PlayScore(PlayCardActionDto action)
                {
                    var card = player.Hand.First(c => c.InstanceId == action.CardUid);
                    var playable = action.PlayMode.HasValue
                        ? await _gameService.GetCardPlayabilityForModeAsync(gameId,aiPlayerId,card.InstanceId,action.PlayMode.Value,cancellationToken)
                        : await _gameService.GetCardPlayabilityAsync(gameId,aiPlayerId,card.InstanceId,cancellationToken);
                    if (!playable.IsPlayable || (playable.SelectedPlayMode==PlayMode.Base
                        && AiTacticalSearch.LastPlayEntersDeckLoss(player,endTurn,card.InstanceId)))
                        return double.NegativeInfinity;
                    var cost = playable.PlayModes.FirstOrDefault(mode=>mode.Mode==playable.SelectedPlayMode)?.EffectiveCost ?? card.CurrentCost;
                    return 2.5 + cost * .35 - BoostSpendPenalty(player, cost)
                        + (defensiveEmergency ? 0 : AiAbilityScoring.FragileHandPriority(player, card) + AiEternalEngine.PlayBias(player,card,cost));
                }
                var playCard = await _aiService.DecidePlayCardAsync(snapshot, aiPlayerId, playStrategy);
                if (playCard != null)
                {
                    var card = player.Hand.First(c => c.InstanceId == playCard.CardUid);
                    var score = await PlayScore(playCard);
                    Consider("PlayCard", playCard, score + (playStrategy?.PlayBias(card.CardId) ?? 0) * .35,
                        playStrategy == null ? score : double.NegativeInfinity);
                }
                var localPlay = playStrategy == null ? playCard
                    : await _aiService.DecidePlayCardAsync(snapshot, aiPlayerId);
                if (localPlay != null) ConsiderLocal("PlayCard", localPlay, await PlayScore(localPlay));

                foreach (var available in abilityActions)
                {
                    var localScore = available.Score - BoostSpendPenalty(player, available.Cost);
                    var score = localScore + (defensiveEmergency ? 0 : (strategy?.AbilityBias(available.Candidate) ?? 0)
                        + (available.IsFusion ? AiAbilityScoring.MaterialBias(player, available.Materials, strategy) : 0));
                    if (score > bestScore) abilityAction = available;
                    Consider(available.IsFusion ? "RequestFusion" : "RequestAct", available.Data, score, localScore);
                }

                foreach (var (source, recipe) in generatedTokenLines)
                {
                    if (bestScore >= 990 && localBestScore >= 990) break;
                    var spell = player.Hand.FirstOrDefault(card => card.InstanceId == source);
                    if (spell == null || !AiTacticalSearch.CanProjectGeneratedStormLethal(player, opponent, spell.CurrentCost,
                        recipe.SelfDamage, recipe.Count, recipe.TokenCost, recipe.TokenAttack)) continue;
                    // Explicit base mode: automatic Enhance selection must not hide an available base route.
                    var playable = await _gameService.GetCardPlayabilityForModeAsync(gameId, aiPlayerId, source, PlayMode.Base, cancellationToken);
                    var baseMode = playable.PlayModes.FirstOrDefault(mode => mode.Mode == PlayMode.Base);
                    if (playable.IsPlayable && playable.SelectedPlayMode == PlayMode.Base && baseMode != null
                        && AiTacticalSearch.CanProjectGeneratedStormLethal(player, opponent, baseMode.EffectiveCost,
                            recipe.SelfDamage, recipe.Count, recipe.TokenCost, recipe.TokenAttack))
                    {
                        var play = new PlayCardActionDto { CardUid = source, PlayMode = PlayMode.Base, PaidCost=baseMode.EffectiveCost };
                        if (bestScore < 990)
                        {
                            Consider("PlayCard", play, 990);
                            generatedStormSource = source; generatedStormToken = recipe.TokenId;
                        }
                        else ConsiderLocal("PlayCard", play, 990);
                    }
                }
                foreach(var (source,_) in generatedSummonLines)
                {
                    if (bestScore >= 990 && localBestScore >= 990) break;
                    var playable=await _gameService.GetCardPlayabilityForModeAsync(gameId,aiPlayerId,source,PlayMode.Base,cancellationToken);
                    var baseMode=playable.PlayModes.FirstOrDefault(mode=>mode.Mode==PlayMode.Base);
                    if(!playable.IsPlayable||playable.SelectedPlayMode!=PlayMode.Base||baseMode is not {IsAvailable:true}
                        ||baseMode.EffectiveCost<0||baseMode.EffectiveCost>player.CurrentPP)continue;
                    // A legal current-turn finish overrides the long-term desire to retain the last engine.
                    var play = new PlayCardActionDto { CardUid = source, PlayMode = PlayMode.Base, PaidCost=baseMode.EffectiveCost };
                    if (bestScore < 990)
                    {
                        Consider("PlayCard", play, 990);
                        generatedSummonSource = source;
                    }
                    else ConsiderLocal("PlayCard", play, 990);
                }
                if (CanArmBoost(player))
                {
                    if (player.Hand.Any(card => IsBoostedStormLethal(player, opponent, card)))
                        Consider("PerformPpBoost", new { }, 900);
                    else if ((bestScore <= 0 || localBestScore <= 0) && ShouldArmBoost(player, opponent))
                        Consider("PerformPpBoost", new { }, bestScore <= 0 ? .1 : double.NegativeInfinity,
                            localBestScore <= 0 ? .1 : double.NegativeInfinity);
                }
                var prepareLocal = localType == "PerformEvolve" && localBestScore < 850;
                var prepareSelected = actionType == "PerformEvolve" && bestScore < 850;
                if ((prepareLocal || prepareSelected) && !defensiveEmergency
                    && AiWeaverPlan.PreparationEnabled(game, aiPlayerId, _prepareWeaverEvolution)
                    && localTacticalPlan?.ProjectsLethal != true && localTacticalPlan?.PreventsVisibleLethal != true)
                {
                    Guid? weaver;
                    using (var context = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken))
                        weaver = AiWeaverPlan.PrepareBeforeEvolution(context, player);
                    if (weaver is Guid uid)
                    {
                        var playable = await _gameService.GetCardPlayabilityForModeAsync(gameId, aiPlayerId, uid, PlayMode.Base, cancellationToken);
                        var mode = playable.PlayModes.FirstOrDefault(item => item.Mode == PlayMode.Base);
                        if (playable.IsPlayable && playable.SelectedPlayMode == PlayMode.Base
                            && mode is { IsAvailable: true, EffectiveCost: >= 0 and <= 1 }
                            && mode.EffectiveCost <= player.CurrentPP && game.StateRevision == decisionRevision)
                        {
                            var play = new PlayCardActionDto { CardUid = uid, PlayMode = PlayMode.Base, PaidCost = mode.EffectiveCost };
                            if (prepareLocal) (localType, localData, localBestScore) = ("PlayCard", play, 2.5);
                            if (prepareSelected)
                            {
                                (actionType, actionData, bestScore) = ("PlayCard", play, 2.5);
                                weaverPreparation = play;
                            }
                        }
                    }
                }
            }
            var explicitModelSelection = false;
            if(strategy?.TacticalBinding is { } proposed) {
                using var currentContext=new GameContext(services,_mapper,accessor,game,aiPlayerId,cancellationToken);
                var blocked=AiTacticalCandidates.CheckNow(proposed,currentContext,snapshot,aiPlayerId,decisionRevision);
                var sameLocal=AiTacticalCandidates.Same(proposed.Action,localTacticalPlan?.First);
                if(blocked==null && !(sameLocal && localTacticalPlan?.ProjectsLethal==true)
                    && (AiTacticalSearch.ClearsHandIntoDeckLoss(snapshot.Players.Single(player=>player.PlayerId==aiPlayerId),
                        endTurn,proposed.Action.Data)
                        || (proposed.Action.Type=="EndTurn" && actionType!="EndTurn"
                            && AiTacticalSearch.EndsInDeckLoss(snapshot,aiPlayerId,endTurn))))
                    blocked="known-end-turn-deck-exhaustion";
                if(blocked==null&&!sameLocal) {
                    if(localTacticalPlan?.ProjectsLethal==true&&(localTacticalFacts?.CanSkipPlanning==true||localTacticalPlan.First.Type=="EndTurn")
                        ||bestScore>=990&&(generatedStormSource.HasValue||generatedSummonSource.HasValue))blocked="known-current-finish-has-priority";
                    else if(localTacticalPlan?.PreventsVisibleLethal==true)blocked="visible-lethal-defense-has-priority";
                }
                if(blocked==null) {
                    actionType=proposed.Action.Type;actionData=proposed.Action.Data;
                    explicitModelSelection = true;
                    // Explicit choice has a separate proposal record; do not borrow another action's score.
                    bestScore=0;
                    generatedStormSource=null;generatedStormToken=null;generatedSummonSource=null;
                }
                planner?.RecordTacticalSelection(game,aiPlayerId,strategy,blocked);
                strategy=strategy with {NextAction=null,TacticalBinding=null};
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentTurn(game, aiPlayerId, turnNumber))
            {
                return;
            }

            var action = new PlayerActionWrapper(actionType, aiPlayerId, actionData)
            {
                ExpectedTurnNumber = turnNumber,
                CancellationToken = cancellationToken
            };
            AiStrategyPlanner.ActionOption Option(string type, object data, double score)
            {
                var allCards = snapshot.Players.SelectMany(player => player.Hand.Concat(player.Field)
                    .Concat(player.LeaderCard == null ? [] : new[] { player.LeaderCard }));
                int? CardId(Guid uid) => allCards.FirstOrDefault(card => card.InstanceId == uid)?.CardId;
                return data switch
                {
                    PlayCardActionDto play => new(type, CardId(play.CardUid), null, null, score,
                        snapshot.Players.First(player=>player.PlayerId==aiPlayerId).Hand.FindIndex(card=>card.InstanceId==play.CardUid),
                        play.PlayMode?.ToString(), play.PaidCost),
                    AttackActionDto attack => new(type, CardId(attack.AttackerUid), CardId(attack.TargetUid), null, score,
                        FieldIndex:snapshot.Players.Single(player=>player.PlayerId==aiPlayerId).Field.FindIndex(card=>card.InstanceId==attack.AttackerUid),
                        TargetFieldIndex:snapshot.Players.Single(player=>player.PlayerId!=aiPlayerId).Field.FindIndex(card=>card.InstanceId==attack.TargetUid)),
                    EvolveActionDto evolve when Guid.TryParse(evolve.UnitUid, out var uid) => new(type, CardId(uid), null, null, score,FieldIndex:snapshot.Players.Single(player=>player.PlayerId==aiPlayerId).Field.FindIndex(card=>card.InstanceId==uid)),
                    SuperEvolveActionDto super when Guid.TryParse(super.UnitUid, out var uid) => new(type, CardId(uid), null, null, score,FieldIndex:snapshot.Players.Single(player=>player.PlayerId==aiPlayerId).Field.FindIndex(card=>card.InstanceId==uid)),
                    ActActionDto act => new(type, CardId(act.CardUid), null,
                        abilityActions.FirstOrDefault(item => item.RuntimeId == act.AbilityRuntimeId)?.Candidate.Name, score),
                    FusionActionDto fusion => new(type, CardId(fusion.CardUid), null,
                        abilityActions.FirstOrDefault(item => item.RuntimeId == fusion.AbilityRuntimeId)?.Candidate.Name, score),
                    _ => new(type, null, null, null, score)
                };
            }
            var beforeOwn=snapshot.Players.First(player=>player.PlayerId==aiPlayerId);
            var beforeEnemy=snapshot.Players.First(player=>player.PlayerId!=aiPlayerId);
            var comparison = strategy == null ? null : new AiStrategyPlanner.ActionComparisonFact(turnNumber,
                Option(localType, localData, localBestScore), Option(actionType, actionData, bestScore),
                new(beforeOwn.LeaderCard?.CurrentHealth,beforeEnemy.LeaderCard?.CurrentHealth,beforeOwn.CurrentPP,
                    beforeOwn.MaxPP,beforeOwn.CurrentEP,beforeOwn.CurrentSEP,beforeOwn.CanEvolve,beforeOwn.CanSuperEvolve,
                    beforeOwn.IsPpBoostArmed,beforeOwn.PpBoostUsesAvailable,beforeOwn.Hand.Count,beforeOwn.Field.Count,
                    beforeEnemy.Field.Count,AiTacticalSearch.EstimateBoardDamage(beforeEnemy.Field,beforeOwn.Field,beforeOwn.LeaderCard),
                    AiEndTurnForecast.ForMemory(endTurn,snapshot,aiPlayerId)))
            {
                SameSelection = AiCommittedAction.SameSelection(localType, localData, actionType, actionData),
                SelectionPolicy = explicitModelSelection ? "model-first-action" : "plan-guided-ranking"
            };
            AiStrategyPlanner.AttackFact? attackFact = null;
            if (actionData is AttackActionDto attackAction)
            {
                var own = snapshot.Players.First(player => player.PlayerId == aiPlayerId);
                var enemy = snapshot.Players.First(player => player.PlayerId != aiPlayerId);
                var attacker = own.Field.FirstOrDefault(card => card.InstanceId == attackAction.AttackerUid);
                var target = enemy.Field.Concat(enemy.LeaderCard == null ? [] : new[] { enemy.LeaderCard })
                    .FirstOrDefault(card => card.InstanceId == attackAction.TargetUid);
                if (attacker != null && target != null)
                    attackFact = new(turnNumber, attacker.CardId, target.CardId, target.Type == CardType.Leader,
                        attacker.Keywords.Contains(CardKeyword.Storm), attacker.CurrentAttack, target.CurrentAttack,
                        target.CurrentHealth, own.LeaderCard?.CurrentHealth, enemy.LeaderCard?.CurrentHealth,
                        AiTacticalSearch.EstimateBoardDamage(enemy.Field, own.Field,own.LeaderCard),
                        target.Keywords.Select(keyword => keyword.ToString()).ToArray(),
                        AiEndTurnForecast.ForMemory(endTurn,snapshot,aiPlayerId,attacker.InstanceId));
            }
            using var projectionSubmission=AiCombatEvaluation.BeginSubmit(game,aiPlayerId,projectionDecision,actionType,actionData);
            var completion = await SubmitAiActionAsync(game, action, generatedStormSource, cancellationToken,
                actionType is "RequestFusion" or "RequestAct" ? abilityAction : null, planner, comparison, attackFact, generatedStormToken, snapshot);
            if (completion == null) return;
            if (completion.Status == PlayerActionCompletionStatus.Completed)
                evolutionEvaluation?.Commit(game, turnNumber, snapshot, actionType, actionData, observedCombat, observedEnding);
            if (recoveryEvaluation != null && ReferenceEquals(localTacticalPlan, verifiedRecovery)
                && verifiedRecovery != null && completion.Status == PlayerActionCompletionStatus.Completed
                && AiCommittedAction.SameSelection(verifiedRecovery.First.Type, verifiedRecovery.First.Data, actionType, actionData) == true
                && beforeOwn.LeaderCard is { } priorLeader && game.GetPlayerState(aiPlayerId)?.Leader is { } currentLeader)
                recoveryEvaluation.Commit(turnNumber, verifiedRecovery,
                    AiTacticalSearch.EstimateBoardDamage(beforeEnemy.Field, beforeOwn.Field, priorLeader),
                    priorLeader.CurrentHealth, currentLeader.CurrentHealth);
            if (completion.Status == PlayerActionCompletionStatus.Completed && weaverPreparation != null
                && ReferenceEquals(action.ActionData, weaverPreparation))
                AiWeaverPlan.RecordPreparation(game, aiPlayerId);
            if (completion.Status == PlayerActionCompletionStatus.Completed && (generatedStormSource.HasValue
                ||generatedSummonSource.HasValue&&action.ActionData is PlayCardActionDto summonPlay&&summonPlay.CardUid==generatedSummonSource))
                Interlocked.Increment(ref game.GeneratedTokenTacticUses);
            if(completion.Status==PlayerActionCompletionStatus.Completed&&generatedSummonSource.HasValue
                &&action.ActionData is PlayCardActionDto completedSummon&&completedSummon.CardUid==generatedSummonSource)
                Interlocked.Increment(ref AiSummonSourceState.For(game).Uses);
            if (actionType is "RequestFusion" or "RequestAct" && abilityAction != null)
            {
                var used = abilityAction.IsFusion ? abilityAction.Card.FusionsThisTurn : abilityAction.Card.ActsThisTurn;
                if (completion.Status != PlayerActionCompletionStatus.Completed || used == abilityAction.PreviousUses)
                {
                    unavailableAbilities.Add(abilityAction.RuntimeId);
                    if (completion.Status != PlayerActionCompletionStatus.Completed)
                    {
                        _logger.LogDebug("AI ability {Ability} did not complete: {Reason}", abilityAction.RuntimeId, completion.Reason);
                        continue;
                    }
                }
            }
            if (completion.Status == PlayerActionCompletionStatus.Failed)
            {
                throw new InvalidOperationException(completion.Reason);
            }
            if (completion.Status != PlayerActionCompletionStatus.Completed)
            {
                _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Action {ActionType} {Status}: {Reason}",
                    gameId, aiPlayerId, actionType, completion.Status, completion.Reason);
                return;
            }
            if(planner!=null&&strategy?.PlayLine!=null)
            {
                using var afterContext=new GameContext(services,_mapper,accessor,game,aiPlayerId,cancellationToken);
                var after=GameStateProjection.Create(game,afterContext);
                strategy=await planner.AdvanceCommittedPlanAsync(game,after,aiPlayerId,actionType,actionData,
                    (uid,token)=>_gameService.GetCardPlayabilityAsync(gameId,aiPlayerId,uid,token),
                    (uid,mode,token)=>_gameService.GetCardPlayabilityForModeAsync(gameId,aiPlayerId,uid,mode,token),
                    ()=>AiPlaySequence.ReadHandLossAsync(afterContext,after.Players.Single(player=>player.PlayerId==aiPlayerId)),cancellationToken);
            }
            if (actionType == "EndTurn")
            {
                return;
            }

            // 演出等待不再充当动作完成证明；取消会立即停止等待及后续决策。
            if (!game.IsLocalAiTraining) await Task.Delay(ActionObservationDelayMilliseconds, cancellationToken);
        }
    }

    private async Task<PlayerActionCompletion?> SubmitAiActionAsync(GameState game, PlayerActionWrapper action,
        Guid? generatedStormSource, CancellationToken token, AbilityAction? abilityAction, AiStrategyPlanner? planner, AiStrategyPlanner.ActionComparisonFact? comparison = null, AiStrategyPlanner.AttackFact? attackFact = null, int? generatedStormTokenId = null, GameStateDto? observationBefore = null)
    {
        if (generatedStormSource.HasValue)
        {
            game.AiGeneratedStormSource = generatedStormSource;
            game.AiGeneratedStormTokenId = generatedStormTokenId;
        }
        if (abilityAction != null) planner?.SetPendingAbility(game, abilityAction.RuntimeId, abilityAction.Candidate);
        planner?.BeginPlanStep(game,action.PlayerId,action.ActionType,action.ActionData);
        planner?.SetPendingComparison(game, comparison);
        planner?.BindPendingComparison(game,action.ActionData);
        if (action.ActionData is AttackActionDto attack)
            planner?.SetPendingAttack(game, attack.AttackerUid, attack.TargetUid, attackFact);
        try
        {
            if(observationBefore!=null&&planner!=null) {
                using var planScope=_serviceProvider.CreateScope();
                using var planContext=new GameContext(planScope.ServiceProvider,_mapper,planScope.ServiceProvider.GetRequiredService<IGameContextAccessor>(),game,action.PlayerId);
                planner.BeginActionOutcome(game,action,observationBefore,comparison);
            }
            if (await _gameService.EnqueuePlayerActionAsync(game.GameId, action))
            {
                var completion = await action.Completion.WaitAsync(token);
                if (completion.Status == PlayerActionCompletionStatus.Completed && game.IsLocalAiTraining)
                    game.LocalAiTrainingObservation?.Invoke(action.PlayerId, action.ActionType);
                return completion;
            }

            _logger.LogWarning("[AI_EXECUTOR:{GameId}] Action was not admitted.", game.GameId);
            return null;
        }
        finally
        {
            planner?.EndActionOutcome(game,action);
            if (generatedStormSource.HasValue)
            {
                game.AiGeneratedStormSource = null;
                game.AiGeneratedStormTokenId = null;
            }
            if (abilityAction != null) planner?.SetPendingAbility(game, null, null);
            planner?.SetPendingComparison(game, null);
            if (attackFact != null) planner?.SetPendingAttack(game, Guid.Empty, Guid.Empty, null);
        }
    }

    internal static bool HasVisibleLethalThreat(PlayerStateDto player, PlayerStateDto opponent) =>
        AiTacticalSearch.HasVisibleLethalThreat(player, opponent);

    internal static bool ShouldArmBoost(PlayerStateDto player, PlayerStateDto opponent)
    {
        if (!CanArmBoost(player))
            return false;
        var threatened = AiTacticalSearch.EstimateBoardDamage(opponent.Field, player.Field,player.LeaderCard)
            >= (player.LeaderCard?.CurrentHealth ?? 20);
        return player.Hand.Any(card =>
        {
            if (card.Type != CardType.Follower || card.CurrentCost != player.CurrentPP + GameConstants.PlayPoints.PpBoostAmount)
                return false;
            if (IsBoostedStormLethal(player, opponent, card))
                return true;
            if (threatened && (card.Keywords.Contains(CardKeyword.Ward) || card.Keywords.Contains(CardKeyword.Rush)))
                return true;
            // Advance a substantial follower when the remaining PP would otherwise sit idle.
            return player.CurrentPP >= 2 && card.CurrentAttack >= 2
                && card.CurrentAttack + card.CurrentHealth >= Math.Max(6, card.CurrentCost + 2);
        });
    }

    private static bool CanArmBoost(PlayerStateDto player) =>
        player.PpBoostUsesAvailable > 0 && !player.IsPpBoostArmed && player.Field.Count < GameConstants.Field.MaxFieldSize;

    private static bool IsBoostedStormLethal(PlayerStateDto player, PlayerStateDto opponent, CardInstanceDto card) =>
        card.Type == CardType.Follower && card.CurrentCost == player.CurrentPP + GameConstants.PlayPoints.PpBoostAmount
        && card.Keywords.Contains(CardKeyword.Storm) && opponent.LeaderCard is { } leader
        && !leader.Keywords.Contains(CardKeyword.Shield) && card.CurrentAttack >= leader.CurrentHealth
        && AiService.LegalAttackTargets(card with { ZoneStayTurn = 0 }, opponent).Any(target => target.InstanceId == leader.InstanceId);

    private static double BoostSpendPenalty(PlayerStateDto player, int cost) =>
        player.IsPpBoostArmed && cost > player.CurrentPP ? 2 : 0;

    private sealed record AbilityAction(CardInstance Card, Guid RuntimeId, bool IsFusion, int Cost,
        int PreviousUses, object Data, double Score, AiStrategyPlanner.LocalAbilityCandidate Candidate,
        CardInstanceDto[] Materials);

    private async Task<List<AbilityAction>> GetAbilityActionsAsync(
        GameContext context, GameStateDto snapshot, Guid actor, HashSet<Guid> unavailable, bool useLegacyPolicy)
    {
        if (snapshot.Phase != GamePhase.Main) return [];

        var player = context.CurrentGameState.GetPlayerState(actor)!;
        var playerView = snapshot.Players.First(p => p.PlayerId == actor);
        var targetSelection = context.ServiceProvider.GetRequiredService<TargetSelectionService>();
        var model = context.ServiceProvider.GetService<AiLearningModel>();
        var weights = model?.Weights(context.CurrentGameState,actor);
        var candidates = new List<AbilityAction>();
        foreach (var view in playerView.Hand.Concat(playerView.Field))
        {
            var fusion = view.Zone == CardZone.Hand;
            var summaries = fusion ? view.FusionAbilities : view.ActAbilities;
            if (summaries.Count == 0) continue;

            var card = context.CurrentGameState.FindCardInstance(view.InstanceId);
            if (card == null || (fusion ? card.HasUsedFusionThisTurn() : card.HasUsedActThisTurn())) continue;

            var keyword = fusion ? AbilityKeyword.Fusion : AbilityKeyword.Act;
            foreach (var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context))
            {
                if (!ability.HasKeyword(keyword) || unavailable.Contains(ability.RuntimeId)) continue;
                var cost = fusion ? 0 : card.GetActCostForAbility(ability, context);
                if (cost > player.GetSpendablePlayPoints()) continue;

                IGameEvent trigger = fusion
                    ? new FusionInvokedEvent(player, card, triggeredAbilityRuntimeId: ability.RuntimeId)
                    : new ActInvokedEvent(player, card, cost, ability.RuntimeId);
                var abilityContext = context with { SourceCard = card, SourceAbility = ability, TriggeringEvent = trigger };
                if (!await ability.CanTrigger(trigger, abilityContext) || !ability.CheckConditions(abilityContext)) continue;
                var targets = await targetSelection.CheckTargetAvailabilityAsync(ability, abilityContext);
                if (!targets.IsAvailable) continue;
                if (fusion && ability.Targets.Any(t => t.Config.IsEnabled && t.Config.PlayerChoiceOptions?.IsRequired == true)
                    && !targets.HasValidTargets) continue;

                object data = fusion
                    ? new FusionActionDto { CardUid = card.InstanceId, AbilityRuntimeId = ability.RuntimeId }
                    : new ActActionDto { CardUid = card.InstanceId, AbilityRuntimeId = ability.RuntimeId };
                var score = model?.Score(snapshot, actor, fusion ? "fusion" : "act", view with { CurrentCost = cost }, weights: weights) ?? 0;
                CardInstanceDto[] materials = [];
                if (!useLegacyPolicy)
                {
                    var candidatesForTarget = (targets.ValidTargetIds ?? []).Select(id => snapshot.FindCardInstanceById(id.ToString()))
                        .OfType<CardInstanceDto>().ToArray();
                    if (fusion) materials = candidatesForTarget;
                    var effects = ability is Ability configured ? configured.Config.Effects : [];
                    var choiceTargets = ability.Targets.Where(t => t.Config.IsEnabled && t.Config.PlayerChoiceOptions?.IsRequired == true).ToArray();
                    var choiceKey = choiceTargets.Length == 1 ? choiceTargets[0].Key : null;
                    score = AiAbilityScoring.Score(playerView, actor, view, fusion, cost, effects, candidatesForTarget, choiceKey)
                        + .05 * Math.Clamp(score, -8, 8);
                }

                candidates.Add(new AbilityAction(card, ability.RuntimeId, fusion, cost,
                    fusion ? card.FusionsThisTurn : card.ActsThisTurn, data, score,
                    new(card.CurrentDefinition.Id, fusion ? "fusion" : "act", cost, ability.Name, score), materials));
            }
        }

        return candidates;
    }

    private static async Task EstimateEvolutionEffectsAsync(
        GameContext context, PlayerStateDto player, Dictionary<Guid, (double Evolve, double SuperEvolve)> values)
    {
        var owner = context.CurrentGameState.GetPlayerState(player.PlayerId)!;
        var source = TargetInfo.ForPlayer(owner);
        var targetSelection = context.ServiceProvider.GetRequiredService<TargetSelectionService>();
        foreach (var unit in player.Field.Where(c => c.CanEvolve && !c.Keywords.Contains(CardKeyword.BanEvolve)))
        {
            var card = context.CurrentGameState.FindCardInstance(unit.InstanceId);
            if (card == null) continue;
            var normalValue = 0d;
            var superValue = 0d;
            foreach (var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context))
            {
                var evolves = ability.HasKeyword(AbilityKeyword.EvolveEffect);
                var superEvolves = ability.HasKeyword(AbilityKeyword.SuperEvolveEffect);
                if (!evolves && !superEvolves) continue;

                async Task<bool> IsAvailable(IGameEvent trigger)
                {
                    var abilityContext = context with { SourceCard = card, SourceAbility = ability, TriggeringEvent = trigger };
                    if (!await ability.CanTrigger(trigger, abilityContext) || !ability.CheckConditions(abilityContext))
                        return false;
                    return (await targetSelection.CheckTargetAvailabilityAsync(ability, abilityContext)).IsAvailable;
                }

                if (evolves && player.CanEvolve && await IsAvailable(new EvolveEvent(card, source, isManualEvolution: true)))
                    normalValue += 2;
                if (player.CanSuperEvolve)
                {
                    if (evolves && await IsAvailable(new EvolveEvent(card, source, true, true))) superValue += 2;
                    if (superEvolves && await IsAvailable(new SuperEvolveEvent(card, source, true))) superValue += 2;
                }
            }
            if (normalValue > 0 || superValue > 0)
                values[unit.InstanceId] = (Math.Min(normalValue, 4), Math.Min(superValue, 4));
        }
    }

    private async Task<(string Type, object Data)> ChooseLegacyActionAsync(
        GameStateDto state, Guid actor, AbilityAction? ability)
    {
        if (await _aiService.DecidePpBoostAsync(state, actor)) return ("PerformPpBoost", new { });
        if (ability is { IsFusion: true }) return ("RequestFusion", ability.Data);
        if (ability is { Cost: 0 }) return ("RequestAct", ability.Data);
        if (await _aiService.DecidePlayCardAsync(state, actor) is { } play) return ("PlayCard", play);
        if (ability != null) return ("RequestAct", ability.Data);

        var player = state.Players.First(p => p.PlayerId == actor);
        if (player.CanSuperEvolve && player.CurrentSEP >= GameConstants.SuperEvolution.SuperEvolutionPointCost)
        {
            var unit = player.Field.Where(c => c.CanEvolve && !c.Keywords.Contains(CardKeyword.BanEvolve))
                .OrderByDescending(c => c.CanAttack).ThenByDescending(c => c.CurrentAttack).FirstOrDefault();
            if (unit != null) return ("PerformSuperEvolve", new SuperEvolveActionDto { UnitUid = unit.InstanceId.ToString() });
        }
        if (await _aiService.DecideEvolveAsync(state, actor) is { } evolve) return ("PerformEvolve", evolve);
        if (await _aiService.DecideNextAttackAsync(state, actor) is { } attack) return ("PerformAttack", attack);
        return ("EndTurn", new { });
    }

    public async Task ExecuteMirageRecruitTurnAsync(string gameId, Guid aiPlayerId,
        int expectedRoundNumber, int expectedTurnNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var game = _gameService.GetGameState(gameId);
        if (game == null) return;

        for (var actionCount = 0; actionCount < MaxMirageActionsPerRound; actionCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMirageRecruitActive(game, aiPlayerId, expectedRoundNumber, expectedTurnNumber)) return;

            // 双方并行招募时仍在主队列边界捕获投影，避免读取正在被另一玩家修改的集合。
            GameStateDto? snapshot = null;
            var read = new MatchStateRead((state, services) =>
            {
                using var context = new GameContext(services, _mapper,
                    services.GetRequiredService<IGameContextAccessor>(), state, aiPlayerId, cancellationToken);
                snapshot = GameStateProjection.Create(state, context);
                return Task.FromResult(System.Text.Json.JsonSerializer.SerializeToElement(snapshot));
            }) { CancellationToken = cancellationToken };
            if (!await _gameService.EnqueuePlayerActionAsync(gameId, read)) return;
            var readCompletion = await read.Completion.WaitAsync(cancellationToken);
            if (readCompletion.Status == PlayerActionCompletionStatus.Failed)
                throw new InvalidOperationException(readCompletion.Reason);
            if (readCompletion.Status != PlayerActionCompletionStatus.Completed
                || !IsMirageRecruitActive(game, aiPlayerId, expectedRoundNumber, expectedTurnNumber)) return;

            var decision = await _aiService.DecideMirageRecruitActionAsync(snapshot!, aiPlayerId);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMirageRecruitActive(game, aiPlayerId, expectedRoundNumber, expectedTurnNumber)) return;

            var completion = await SubmitMirageActionAsync(gameId, aiPlayerId, expectedTurnNumber, decision, cancellationToken);
            if (completion.Status == PlayerActionCompletionStatus.Failed)
                throw new InvalidOperationException(completion.Reason);
            if (completion.Status == PlayerActionCompletionStatus.Cancelled || decision.ActionType == "MirageSetReady") return;
            if (completion.Status != PlayerActionCompletionStatus.Completed)
            {
                _logger.LogWarning("[MIRAGE_AI:{GameId}:{PlayerId}:{Round}] Action {ActionType} {Status}: {Reason}",
                    gameId, aiPlayerId, expectedRoundNumber, decision.ActionType, completion.Status, completion.Reason);
                break;
            }

            if (!game.IsMirageTournamentSeat)
            {
                await Task.Delay(ActionObservationDelayMilliseconds, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsMirageRecruitActive(game, aiPlayerId, expectedRoundNumber, expectedTurnNumber)) return;
        var readyCompletion = await SubmitMirageActionAsync(gameId, aiPlayerId, expectedTurnNumber,
            new MirageAiActionDecision("MirageSetReady", new { }), cancellationToken);
        if (readyCompletion.Status == PlayerActionCompletionStatus.Failed)
            throw new InvalidOperationException(readyCompletion.Reason);
    }

    private async Task<PlayerActionCompletion> SubmitMirageActionAsync(string gameId, Guid playerId,
        int expectedTurnNumber, MirageAiActionDecision decision, CancellationToken cancellationToken)
    {
        var action = new PlayerActionWrapper(decision.ActionType, playerId, decision.ActionData)
        {
            ExpectedTurnNumber = expectedTurnNumber,
            CancellationToken = cancellationToken
        };
        _logger.LogInformation("[MIRAGE_AI:{GameId}:{PlayerId}] Submitting {ActionType} for turn {Turn}.",
            gameId, playerId, decision.ActionType, expectedTurnNumber);
        await _gameService.EnqueuePlayerActionAsync(gameId, action);
        return await action.Completion.WaitAsync(cancellationToken);
    }

    private bool IsMirageRecruitActive(GameState game, Guid playerId, int roundNumber, int turnNumber)
    {
        if (!ReferenceEquals(_gameService.GetGameState(game.GameId), game)
            || game.IsGameOver || game.Mode != GameMode.Mirage || game.Phase != GamePhase.Recruit
            || game.Mirage == null)
        {
            return false;
        }

        lock (game.Mirage.RoundSyncLock)
        {
            return game.Mirage.RoundNumber == roundNumber
                && game.Mirage.ClosingRecruitRoundNumber != roundNumber
                && !game.Mirage.ReadyPlayerIds.Contains(playerId)
                && game.GetPlayerState(playerId)?.TurnNumber == turnNumber;
        }
    }

    private bool IsCurrentTurn(GameState game, Guid playerId, int turnNumber)
    {
        return ReferenceEquals(_gameService.GetGameState(game.GameId), game)
            && !game.IsGameOver
            && game.CurrentPlayerId == playerId
            && game.GetPlayerState(playerId)!.TurnNumber == turnNumber;
    }
}
