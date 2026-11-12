using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiExperienceRootMove(string Kind, string Seat, int Turn, AiNativeActionCard? Source,
    AiNativeActionCard[] Targets, string? PlayMode, int? PaidCost, long? AbilityDefinitionId);

public sealed record AiExperienceRootComparison(int HistoryEpisode, int ControlEpisode, int MatchedRoots,
    string Status, AiExperienceRootMove? History = null, AiExperienceRootMove? Control = null)
{
    public int? ChoiceIndex { get; init; }
    public AiExperienceChoice? HistoryChoice { get; init; }
    public AiExperienceChoice? ControlChoice { get; init; }
}

internal sealed class AiExperienceRootTrace
{
    internal const int Limit = 96;
    private static readonly ConditionalWeakTable<GameState, AiExperienceRootTrace> Traces = new();
    private readonly List<Step?> steps = [];
    private bool limited;

    private readonly object choiceGate = new();
    private List<AiExperienceChoices.Probe?>? choices;
    private bool choicesLimited;
    private const int ChoiceLimit = 8;

    internal sealed record Step(string Position, string MoveKey, AiExperienceRootMove Move)
    {
        internal AiExperienceChoices.Step?[] Choices { get; init; } = [];
        internal bool ChoicesLimited { get; init; }
    }

    internal static void Attach(GameState game) => Traces.GetValue(game, _ => new());
    internal static AiExperienceRootTrace? For(GameState game) => Traces.TryGetValue(game, out var trace) ? trace : null;

    internal Step? Capture(GameState game, GameStateDto snapshot, PlayerActionWrapper action)
    {
        if (steps.Count >= Limit) return null;
        try
        {
            if (snapshot.CurrentPlayerId != action.PlayerId || snapshot.GameId != game.GameId) return null;
            var actor = snapshot.Players.Single(player => player.PlayerId == action.PlayerId);
            var opponent = snapshot.Players.Single(player => player.PlayerId != action.PlayerId);
            var seat = game.StrategyBenchmarkPlayerId == action.PlayerId ? "model" : "opponent";

            Guid? sourceId;
            Guid[] targetIds = [];
            Guid? abilityId = null;
            string? mode = null;
            int? paid = null;
            switch (action.ActionType, action.ActionData)
            {
                case ("PlayCard", PlayCardActionDto play):
                    sourceId = play.CardUid;
                    targetIds = (play.TargetUids ?? []).ToArray();
                    abilityId = play.AbilityRuntimeId;
                    mode = play.PlayMode?.ToString();
                    paid = play.PaidCost;
                    break;
                case ("PerformAttack", AttackActionDto attack):
                    sourceId = attack.AttackerUid;
                    targetIds = [attack.TargetUid];
                    break;
                case ("PerformEvolve", EvolveActionDto evolve) when Guid.TryParse(evolve.UnitUid, out var unit):
                    sourceId = unit;
                    break;
                case ("PerformSuperEvolve", SuperEvolveActionDto evolve) when Guid.TryParse(evolve.UnitUid, out var superUnit):
                    sourceId = superUnit;
                    break;
                case ("RequestAct", ActActionDto act):
                    sourceId = act.CardUid;
                    abilityId = act.AbilityRuntimeId;
                    break;
                case ("RequestFusion", FusionActionDto fusion):
                    sourceId = fusion.CardUid;
                    abilityId = fusion.AbilityRuntimeId;
                    break;
                case ("EndTurn" or "PerformPpBoost", _):
                    sourceId = null;
                    break;
                default:
                    return null;
            }

            var source = sourceId is Guid id ? Locate(actor, opponent, id) : null;
            var targets = targetIds.Select(id => Locate(actor, opponent, id)).OfType<AiNativeActionCard>().ToArray();
            if ((sourceId.HasValue && source is not { Side: "own" }) || targets.Length != targetIds.Length) return null;
            long? ability = null;
            if (abilityId is Guid runtimeId)
            {
                if (game.AbilityManager.GetAbilityByRuntimeId(runtimeId) is not Ability selected
                    || selected.SourceCard?.InstanceId != sourceId || selected.Config.Id <= 0) return null;
                ability = selected.Config.Id;
            }

            var move = new AiExperienceRootMove(action.ActionType, seat, actor.TurnNumber, source, targets, mode, paid, ability);
            return new(SamplePosition(snapshot, actor, opponent, seat),
                Digest(move), move);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            // Diagnostics must not affect the game or skip an unreadable root.
            return null;
        }
    }

    internal void BeginChoices()
    {
        lock (choiceGate)
        {
            choices = [];
            choicesLimited = false;
        }
    }

    internal void CaptureChoice(GameState game, GameStateDto snapshot, PlayerChoiceRequest request,
        PendingChoiceContext? pending, Ability? source)
    {
        lock (choiceGate)
        {
            if (choices == null) return;
            if (choices.Count < ChoiceLimit)
                choices.Add(AiExperienceChoices.Capture(game, snapshot, request, pending, source));
            else choicesLimited = true;
        }
    }

    internal void EndChoices()
    {
        lock (choiceGate) choices = null;
    }

    internal void Record(Step? step)
    {
        lock (choiceGate)
        {
            if (steps.Count < Limit)
                steps.Add(step == null || choices == null ? step : step with
                {
                    Choices = choices.Select(choice => choice?.ReadAccepted()).ToArray(),
                    ChoicesLimited = choicesLimited
                });
            else limited = true;
            choices = null;
        }
    }

    internal static AiExperienceRootComparison Compare(StrategyMatchResult history, StrategyMatchResult control)
    {
        AiExperienceRootComparison Result(int prefix, string status, AiExperienceRootMove? a = null, AiExperienceRootMove? b = null) =>
            new(history.Episode, control.Episode, prefix, status, a, b);

        if (history.ExperienceRoots is not { } first || control.ExperienceRoots is not { } second
            || first.steps.Count == 0 || second.steps.Count == 0)
            return Result(0, "unobserved");
        var shared = Math.Min(first.steps.Count, second.steps.Count);
        for (var index = 0; index < shared; index++)
        {
            if (first.steps[index] is not { } a || second.steps[index] is not { } b)
                return Result(index, "unobserved");
            if (a.Position != b.Position)
                return Result(index, "position-difference");
            if (a.MoveKey != b.MoveKey)
                return Result(index, "root-action-difference", a.Move, b.Move);

            var compared = Math.Min(a.Choices.Length, b.Choices.Length);
            for (var choice = 0; choice < compared; choice++)
            {
                if (a.Choices[choice] is not { } left || b.Choices[choice] is not { } right)
                    return Result(index, "choice-unobserved", a.Move, b.Move) with { ChoiceIndex = choice };
                if (left.ContextKey != right.ContextKey)
                    return Result(index, "choice-context-difference", a.Move, b.Move) with { ChoiceIndex = choice };
                if (left.SelectionKey != right.SelectionKey)
                    return Result(index, "subchoice-difference", a.Move, b.Move) with
                    {
                        ChoiceIndex = choice, HistoryChoice = left.Choice, ControlChoice = right.Choice
                    };
            }
            if (a.ChoicesLimited || b.ChoicesLimited)
                return Result(index, "choice-limit", a.Move, b.Move);
            if (a.Choices.Length != b.Choices.Length)
                return Result(index, "choice-length-difference", a.Move, b.Move);
        }
        if (first.limited || second.limited)
            return Result(shared, "trace-limit");
        return Result(shared, first.steps.Count == second.steps.Count ? "same-recorded-roots" : "different-length");
    }

    internal static AiNativeActionCard? Locate(PlayerStateDto actor, PlayerStateDto opponent, Guid uid)
    {
        foreach (var (player, side) in new[] { (actor, "own"), (opponent, "enemy") })
        {
            if (player.LeaderCard is { } leader && (leader.InstanceId == uid || player.PlayerId == uid))
                return new(side, "leader", 0, leader.CardId, leader.CurrentCost);
            foreach (var (cards, zone) in new[] { (player.Field, "field"), (player.Emblem, "emblem"), (side == "own" ? player.Hand : [], "hand") })
            {
                var index = cards.FindIndex(card => card.InstanceId == uid);
                if (index >= 0) return new(side, zone, index, cards[index].CardId, cards[index].CurrentCost);
            }
        }
        return null;
    }

    internal static string SamplePosition(GameStateDto snapshot, PlayerStateDto actor, PlayerStateDto opponent, string seat) =>
        Digest(new { snapshot.Phase, Active = snapshot.CurrentPlayerId == actor.PlayerId ? "own" : "enemy",
            seat, Actor = Position(actor, true), Opponent = Position(opponent, false) });

    // Fingerprints remain in the evaluator and describe only the sampled fields.
    private static object Position(PlayerStateDto player, bool includeHand) => new
    {
        player.TurnNumber, player.CurrentPP, player.MaxPP, player.CurrentEP, player.CurrentSEP,
        player.IsEvolutionUnlocked, player.IsSuperEvolutionUnlocked, player.CanEvolve, player.CanSuperEvolve,
        player.IsPpBoostArmed, player.PpBoostUsesAvailable, player.PlayedCardCountThisTurn, player.BloodPool,
        player.IsIntromezzoActive, player.DeckCount, player.GraveyardCount, HandCount = player.Hand.Count,
        Counters = player.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
        Leader = player.LeaderCard == null ? null : Card(player.LeaderCard),
        Field = player.Field.Select(Card).ToArray(),
        Emblems = player.Emblem.Select(Card).ToArray(),
        Hand = includeHand ? player.Hand.Select(Card).ToArray() : null
    };

    internal static object Card(CardInstanceDto card) => new
    {
        card.CardId, card.CurrentCost, card.CurrentAttack, card.CurrentHealth, card.MaxHealth,
        card.CurrentCountdown, card.Earthsigil, card.Evidence, card.CanAttack, card.CanEvolve, card.IsEvolved,
        card.IsSuperEvolved, card.ActsThisTurn, card.FusionsThisTurn,
        Keywords = card.Keywords.Order().ToArray(),
        Counters = card.Counters.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
        Buffs = card.BuffSummaries.Select(buff => Digest(new
        {
            buff.SourceCardDefinitionId, buff.SourceAbilityName,
            Numeric = buff.NumericEffects.Select(effect => Digest(new
            {
                effect.Field, effect.Layer, effect.Value, effect.RemoveAfterUse, effect.Duration
            })).Order(StringComparer.Ordinal).ToArray(),
            Granted = buff.GrantedAbilities.Select(Digest).Order(StringComparer.Ordinal).ToArray()
        })).Order(StringComparer.Ordinal).ToArray()
    };

    internal static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
