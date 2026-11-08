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
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class AiTurnExecutor
{
    private const int ActionObservationDelayMilliseconds = 1000;
    private readonly IGameService _gameService;
    private readonly IAiService _aiService;
    private readonly ILogger<AiTurnExecutor> _logger;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _serviceProvider;

    public AiTurnExecutor(
        IGameService gameService,
        IAiService aiService,
        ILogger<AiTurnExecutor> logger,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task ExecuteTurnAsync(string gameId, Guid aiPlayerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var game = _gameService.GetGameState(gameId);
        if (game == null || game.IsGameOver || game.CurrentPlayerId != aiPlayerId)
        {
            return;
        }

        var useLegacyPolicy = game.LegacyAiPlayerId == aiPlayerId;
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
            List<AbilityAction> abilityActions;
            var evolutionValues = new Dictionary<Guid, (double Evolve, double SuperEvolve)>();
            AiTacticalSearch.EndTurnForecast? endTurn = null;
            AiStrategyPlanner.HandLossRule[] handLossRules=[];
            using (var context = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken))
            {
                snapshot = GameStateProjection.Create(game, context);
                abilityActions = await GetAbilityActionsAsync(context, snapshot, aiPlayerId, unavailableAbilities, useLegacyPolicy);
                if (!useLegacyPolicy && snapshot.Phase == GamePhase.Main)
                {
                    var player = snapshot.Players.First(p => p.PlayerId == aiPlayerId);
                    endTurn = await AiEndTurnForecast.ReadAsync(context, player);
                    if(!strategyChecked && planner!=null)handLossRules=await AiPlaySequence.ReadHandLossAsync(context,player);
                    if (player.CanEvolve || player.CanSuperEvolve)
                        await EstimateEvolutionEffectsAsync(context, player, evolutionValues);
                }
            }

            var abilityAction = useLegacyPolicy
                ? abilityActions.OrderBy(action => action.Cost > 0).ThenByDescending(action => action.Score).FirstOrDefault()
                : abilityActions.MaxBy(action => action.Score);
            var abilityCandidates = abilityActions.OrderByDescending(action => action.Score)
                .Select(action => action.Candidate).ToArray();

            if (!strategyChecked && planner != null && snapshot.Phase == GamePhase.Main)
            {
                strategy = await planner.PlanAsync(game, snapshot, aiPlayerId, cancellationToken,
                    (uid, token) => _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, uid, token),
                    abilityCandidates,
                    (uid, mode, token) => _gameService.GetCardPlayabilityForModeAsync(gameId, aiPlayerId, uid, mode, token),handLossRules);
                strategyChecked = true;
                if (!IsCurrentTurn(game, aiPlayerId, turnNumber)) return;
            }

            Guid? generatedStormSource = null;
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
                var playStrategy = defensiveEmergency ? null : strategy;
                var stormPlays = new List<AiTacticalSearch.Move>();
                foreach (var card in player.Hand.Where(c => c.Type == CardType.Follower && c.Keywords.Contains(CardKeyword.Storm)))
                {
                    var playable = await _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, card.InstanceId, cancellationToken);
                    var baseMode = playable.PlayModes.FirstOrDefault(m => m.Mode == PlayMode.Base);
                    if (playable.SelectedPlayMode != PlayMode.Base || !playable.IsPlayable || baseMode == null)
                        continue;
                    stormPlays.Add(new AiTacticalSearch.Move("PlayCard",
                        new PlayCardActionDto { CardUid = card.InstanceId, PlayMode = PlayMode.Base, PaidCost=baseMode.EffectiveCost },
                        card with { CurrentCost = baseMode.EffectiveCost }));
                }

                var intent = playStrategy?.Focus;
                var plan = AiTacticalSearch.FindWithIntent(snapshot, aiPlayerId, cancellationToken, stormPlays, evolutionValues, endTurn, intent);
                var localPlan = intent == null ? plan
                    : AiTacticalSearch.FindWithEndTurn(snapshot, aiPlayerId, cancellationToken, stormPlays, evolutionValues, endTurn);
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
                    if (!playable.IsPlayable) return double.NegativeInfinity;
                    var cost = playable.PlayModes.FirstOrDefault(mode=>mode.Mode==playable.SelectedPlayMode)?.EffectiveCost ?? card.CurrentCost;
                    return 2.5 + cost * .35 - BoostSpendPenalty(player, cost)
                        + (defensiveEmergency ? 0 : AiAbilityScoring.FragileHandPriority(player, card));
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

                if (planner?.IsConfigured == true && bestScore < 990
                    && player.Hand.FirstOrDefault(card => card.CardId == AiStrategyPlanner.StarCallId) is { } starCall
                    && AiStrategyPlanner.GetStarCallStormToken() is { Attack: > 0 } ghost
                    && AiTacticalSearch.CanProjectGeneratedStormLethal(player, opponent, starCall.CurrentCost,
                        selfDamage: 3, count: 3, ghost.Cost, ghost.Attack.Value))
                {
                    var playable = await _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, starCall.InstanceId, cancellationToken);
                    var baseMode = playable.PlayModes.FirstOrDefault(mode => mode.Mode == PlayMode.Base);
                    if (playable.IsPlayable && playable.SelectedPlayMode == PlayMode.Base && baseMode != null
                        && AiTacticalSearch.CanProjectGeneratedStormLethal(player, opponent, baseMode.EffectiveCost,
                            selfDamage: 3, count: 3, ghost.Cost, ghost.Attack.Value))
                    {
                        Consider("PlayCard", new PlayCardActionDto { CardUid = starCall.InstanceId, PlayMode = PlayMode.Base, PaidCost=baseMode.EffectiveCost }, 990);
                        generatedStormSource = starCall.InstanceId;
                    }
                }
                if (CanArmBoost(player))
                {
                    if (player.Hand.Any(card => IsBoostedStormLethal(player, opponent, card)))
                        Consider("PerformPpBoost", new { }, 900);
                    else if ((bestScore <= 0 || localBestScore <= 0) && ShouldArmBoost(player, opponent))
                        Consider("PerformPpBoost", new { }, bestScore <= 0 ? .1 : double.NegativeInfinity,
                            localBestScore <= 0 ? .1 : double.NegativeInfinity);
                }
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
                    AttackActionDto attack => new(type, CardId(attack.AttackerUid), CardId(attack.TargetUid), null, score),
                    EvolveActionDto evolve when Guid.TryParse(evolve.UnitUid, out var uid) => new(type, CardId(uid), null, null, score),
                    SuperEvolveActionDto super when Guid.TryParse(super.UnitUid, out var uid) => new(type, CardId(uid), null, null, score),
                    ActActionDto act => new(type, CardId(act.CardUid), null,
                        abilityActions.FirstOrDefault(item => item.RuntimeId == act.AbilityRuntimeId)?.Candidate.Name, score),
                    FusionActionDto fusion => new(type, CardId(fusion.CardUid), null,
                        abilityActions.FirstOrDefault(item => item.RuntimeId == fusion.AbilityRuntimeId)?.Candidate.Name, score),
                    _ => new(type, null, null, null, score)
                };
            }
            var comparison = strategy == null ? null : new AiStrategyPlanner.ActionComparisonFact(turnNumber,
                Option(localType, localData, localBestScore), Option(actionType, actionData, bestScore));
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
                        AiTacticalSearch.EstimateBoardDamage(enemy.Field, own.Field),
                        target.Keywords.Select(keyword => keyword.ToString()).ToArray());
            }
            var completion = await SubmitAiActionAsync(game, action, generatedStormSource, cancellationToken,
                actionType is "RequestFusion" or "RequestAct" ? abilityAction : null, planner, comparison, attackFact);
            if (completion == null) return;
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
            if (actionType == "EndTurn")
            {
                return;
            }

            // 演出等待不再充当动作完成证明；取消会立即停止等待及后续决策。
            if (!game.IsLocalAiTraining) await Task.Delay(ActionObservationDelayMilliseconds, cancellationToken);
        }
    }

    private async Task<PlayerActionCompletion?> SubmitAiActionAsync(GameState game, PlayerActionWrapper action,
        Guid? generatedStormSource, CancellationToken token, AbilityAction? abilityAction, AiStrategyPlanner? planner, AiStrategyPlanner.ActionComparisonFact? comparison = null, AiStrategyPlanner.AttackFact? attackFact = null)
    {
        if (generatedStormSource.HasValue) game.AiGeneratedStormSource = generatedStormSource;
        if (abilityAction != null) planner?.SetPendingAbility(game, abilityAction.RuntimeId, abilityAction.Candidate);
        planner?.SetPendingComparison(game, comparison);
        if (action.ActionData is AttackActionDto attack)
            planner?.SetPendingAttack(game, attack.AttackerUid, attack.TargetUid, attackFact);
        try
        {
            if (await _gameService.EnqueuePlayerActionAsync(game.GameId, action))
                return await action.Completion.WaitAsync(token);

            _logger.LogWarning("[AI_EXECUTOR:{GameId}] Action was not admitted.", game.GameId);
            return null;
        }
        finally
        {
            if (generatedStormSource.HasValue) game.AiGeneratedStormSource = null;
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
        var threatened = AiTacticalSearch.EstimateBoardDamage(opponent.Field, player.Field)
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
        var weights = model?.Weights(player.InitialDeckDefinitionIds);
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

    private bool IsCurrentTurn(GameState game, Guid playerId, int turnNumber)
    {
        return ReferenceEquals(_gameService.GetGameState(game.GameId), game)
            && !game.IsGameOver
            && game.CurrentPlayerId == playerId
            && game.GetPlayerState(playerId)!.TurnNumber == turnNumber;
    }
}
