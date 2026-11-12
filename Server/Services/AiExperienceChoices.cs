using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiExperienceChoiceOption(int Index, int? CardId, string? Name, AiNativeActionCard? Location);
public sealed record AiExperienceChoice(string Kind, string Seat, int? SourceCardId, int? EffectIndex, int? ExecutionIndex,
    bool Cancelled, AiExperienceChoiceOption[] Selected);

internal static class AiExperienceChoices
{
    internal sealed record Step(string ContextKey, string SelectionKey, AiExperienceChoice Choice);
    internal sealed record Probe(string ContextKey, string Kind, string Seat, int? SourceCardId, int? EffectIndex,
        int? ExecutionIndex, Guid[] Offered, AiExperienceChoiceOption[] Options, int Min, int Max,
        bool IsCancellable, Task<PlayerChoiceResult> Result)
    {
        internal Step? ReadAccepted()
        {
            if (!Result.IsCompletedSuccessfully) return null;
            var accepted = Result.Result;
            if (accepted.WasCancelled)
                return IsCancellable ? new(ContextKey, "cancelled",
                    new(Kind, Seat, SourceCardId, EffectIndex, ExecutionIndex, true, [])) : null;
            if (accepted.ChosenTargetIds == null) return null;
            var selected = accepted.ChosenTargetIds.Select(id => Array.IndexOf(Offered, id)).ToArray();
            if (selected.Length < Min || selected.Length > Max || selected.Any(index => index < 0)
                || selected.Distinct().Count() != selected.Length)
                return null;
            return new(ContextKey, AiExperienceRootTrace.Digest(selected),
                new(Kind, Seat, SourceCardId, EffectIndex, ExecutionIndex, false,
                    selected.Select(index => Options[index]).ToArray()));
        }
    }

    internal static Probe? Capture(GameState game, GameStateDto snapshot, PlayerChoiceRequest request,
        PendingChoiceContext? pending, Ability? source)
    {
        try
        {
            if (!ReferenceEquals(pending?.Request, request) || snapshot.GameId != game.GameId
                || request.SelectableTargetIds.Count > 12 || request.SelectableTargetIds.Distinct().Count() != request.SelectableTargetIds.Count)
                return null;
            var actor = snapshot.Players.Single(player => player.PlayerId == request.RequestingPlayerId);
            var opponent = snapshot.Players.Single(player => player.PlayerId != request.RequestingPlayerId);
            var seat = game.StrategyBenchmarkPlayerId == actor.PlayerId ? "model" : "opponent";
            var sourceCard = request.OptionSourceCard ?? source?.SourceCard;
            var sourceKey = source == null ? null : AiPlannedModes.Fingerprint(source.Config);
            if (source != null && sourceKey == null) return null;
            var kind = request.TargetKey switch { "decision" => "mode", "discover" => "discover", _ => "target" };
            if (kind != "mode" && request.SourceAbilityRuntimeId != Guid.Empty && source == null) return null;
            var invocation = kind == "mode" ? AiDecisionFlowFacts.Identify(game, request) : null;
            if (kind == "mode" && invocation == null) return null;
            var offered = request.SelectableTargetIds.ToArray();
            var options = new List<AiExperienceChoiceOption>(offered.Length);
            var keys = new List<string>(offered.Length);
            for (var index = 0; index < offered.Length; index++)
            {
                var id = offered[index];
                if (kind == "mode")
                {
                    if (!request.DecisionOptions.TryGetValue(id, out var rule)
                        || AiPlannedModes.Fingerprint(rule) is not { } key)
                        return null;
                    options.Add(new(index, null, rule.Name, null));
                    keys.Add(key);
                    continue;
                }

                var location = kind == "discover" ? null : AiExperienceRootTrace.Locate(actor, opponent, id);
                var card = request.SelectableCardInstances.SingleOrDefault(item => item.InstanceId == id);
                if (location == null && card == null) return null;
                options.Add(new(index, location?.CardId ?? card!.CardId, card?.Name, location));
                keys.Add(AiExperienceRootTrace.Digest(new
                {
                    location, OfferedZone = card?.Zone,
                    OfferedSide = card == null ? null : card.OwnerPlayerId == actor.PlayerId ? "own"
                        : card.OwnerPlayerId == opponent.PlayerId ? "enemy" : "unknown",
                    OfferedCard = card == null ? null : AiExperienceRootTrace.Card(card)
                }));
            }

            var context = AiExperienceRootTrace.Digest(new
            {
                Position = AiExperienceRootTrace.SamplePosition(snapshot, actor, opponent, seat),
                request.TargetKey, request.MinSelectionCount, request.MaxSelectionCount, request.IsCancellable,
                Source = sourceCard?.CurrentDefinitionId, sourceKey,
                SourceLocation = sourceCard == null ? null : AiExperienceRootTrace.Locate(actor, opponent, sourceCard.InstanceId),
                Parent = invocation?.ParentFingerprint, invocation?.Effect, invocation?.Execution, Options = keys
            });
            return new(context, kind, seat, sourceCard?.CurrentDefinitionId, invocation?.Effect, invocation?.Execution,
                offered, options.ToArray(), request.MinSelectionCount, request.MaxSelectionCount, request.IsCancellable,
                pending!.Tcs.Task);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            return null;
        }
    }
}
