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
        var unavailableAbilities = new HashSet<Guid>();
        while (IsCurrentTurn(game, aiPlayerId, turnNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.IsLocalAiTraining && ++trainingActions > 128) throw new InvalidOperationException("训练回合动作过多");
            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var accessor = services.GetRequiredService<IGameContextAccessor>();
            GameStateDto snapshot;
            AbilityAction? abilityAction;
            var evolutionValues = new Dictionary<Guid, (double Evolve, double SuperEvolve)>();
            using (var context = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken))
            {
                snapshot = GameStateProjection.Create(game, context);
                abilityAction = await ChooseAbilityActionAsync(context, snapshot, aiPlayerId, unavailableAbilities, useLegacyPolicy);
                if (!useLegacyPolicy && snapshot.Phase == GamePhase.Main)
                {
                    var player = snapshot.Players.First(p => p.PlayerId == aiPlayerId);
                    if (player.CanEvolve || player.CanSuperEvolve)
                        await EstimateEvolutionEffectsAsync(context, player, evolutionValues);
                }
            }

            var actionType = "EndTurn";
            object actionData = new { };
            var bestScore = 0d;

            void Consider(string type, object data, double score)
            {
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
                var stormPlays = new List<AiTacticalSearch.Move>();
                foreach (var card in player.Hand.Where(c => c.Type == CardType.Follower && c.Keywords.Contains(CardKeyword.Storm)))
                {
                    var playable = await _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, card.InstanceId, cancellationToken);
                    var baseMode = playable.PlayModes.FirstOrDefault(m => m.Mode == PlayMode.Base);
                    if (playable.SelectedPlayMode != PlayMode.Base || !playable.IsPlayable || baseMode == null)
                        continue;
                    stormPlays.Add(new AiTacticalSearch.Move("PlayCard",
                        new PlayCardActionDto { CardUid = card.InstanceId, PlayMode = PlayMode.Base },
                        card with { CurrentCost = baseMode.EffectiveCost }));
                }

                var plan = AiTacticalSearch.Find(snapshot, aiPlayerId, cancellationToken, stormPlays, evolutionValues);
                if (plan != null)
                {
                    var combatScore = plan.ProjectsLethal ? 1000 : plan.Gain * .55;
                    Consider(plan.First.Type, plan.First.Data, combatScore);
                }

                if (await _aiService.DecidePlayCardAsync(snapshot, aiPlayerId) is { } playCard)
                {
                    var card = player.Hand.First(c => c.InstanceId == playCard.CardUid);
                    var cost = card.CurrentCost;
                    if (player.IsPpBoostArmed)
                    {
                        var playability = await _gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, card.InstanceId, cancellationToken);
                        cost = playability.PlayModes.FirstOrDefault(mode => mode.Mode == playability.SelectedPlayMode)?.EffectiveCost ?? cost;
                    }
                    var score = 2.5 + cost * .35 - BoostSpendPenalty(player, cost);
                    Consider("PlayCard", playCard, score);
                }

                if (abilityAction != null)
                {
                    var score = abilityAction.Score - BoostSpendPenalty(player, abilityAction.Cost);
                    Consider(abilityAction.IsFusion ? "RequestFusion" : "RequestAct", abilityAction.Data, score);
                }

                var opponent = snapshot.Players.First(p => p.PlayerId != aiPlayerId);
                if (CanArmBoost(player))
                {
                    if (player.Hand.Any(card => IsBoostedStormLethal(player, opponent, card)))
                        Consider("PerformPpBoost", new { }, 900);
                    else if (bestScore <= 0 && ShouldArmBoost(player, opponent))
                        Consider("PerformPpBoost", new { }, .1);
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
            if (!await _gameService.EnqueuePlayerActionAsync(gameId, action))
            {
                _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Action {ActionType} was not admitted.",
                    gameId, aiPlayerId, actionType);
                return;
            }

            var completion = await action.Completion.WaitAsync(cancellationToken);
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

    private sealed record AbilityAction(CardInstance Card, Guid RuntimeId, bool IsFusion, int Cost, int PreviousUses, object Data, double Score);

    private async Task<AbilityAction?> ChooseAbilityActionAsync(
        GameContext context, GameStateDto snapshot, Guid actor, HashSet<Guid> unavailable, bool useLegacyPolicy)
    {
        if (snapshot.Phase != GamePhase.Main) return null;

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
                if (!useLegacyPolicy)
                {
                    var candidatesForTarget = (targets.ValidTargetIds ?? []).Select(id => snapshot.FindCardInstanceById(id.ToString()))
                        .OfType<CardInstanceDto>().ToArray();
                    var effects = ability is Ability configured ? configured.Config.Effects : [];
                    var choiceTargets = ability.Targets.Where(t => t.Config.IsEnabled && t.Config.PlayerChoiceOptions?.IsRequired == true).ToArray();
                    var choiceKey = choiceTargets.Length == 1 ? choiceTargets[0].Key : null;
                    score = AiAbilityScoring.Score(playerView, actor, view, fusion, cost, effects, candidatesForTarget, choiceKey)
                        + .05 * Math.Clamp(score, -8, 8);
                }

                candidates.Add(new AbilityAction(card, ability.RuntimeId, fusion, cost,
                    fusion ? card.FusionsThisTurn : card.ActsThisTurn, data, score));
            }
        }

        return useLegacyPolicy
            ? candidates.OrderBy(action => action.Cost > 0).ThenByDescending(action => action.Score).FirstOrDefault()
            : candidates.MaxBy(action => action.Score);
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
