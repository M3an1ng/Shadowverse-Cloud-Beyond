using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

internal static class AiPlayCandidates
{
    internal sealed record Candidate(CardInstanceDto Original, CardInstanceDto View, int HandIndex,
        PlayMode Mode, int Cost, bool NeedsTarget, Guid? AbilityRuntimeId, int PlayedCardId)
    {
        internal PlayCardActionDto Action() => new() { CardUid=Original.InstanceId, PlayMode=Mode,
            PaidCost=Cost, AbilityRuntimeId=AbilityRuntimeId };
    }

    internal static Candidate? Select(IReadOnlyList<Candidate> candidates, AiStrategyPlanner.TurnPlan? plan,
        string positionKey, Func<Candidate,double> score)
    {
        var advised = plan?.PlayBinding is { } binding && binding.PositionKey == positionKey
            ? candidates.FirstOrDefault(candidate => candidate.Original.InstanceId==binding.InstanceId
                && candidate.Original.CardId==binding.Advice.CardId && candidate.Mode.ToString()==binding.Advice.Mode
                && candidate.Cost==binding.Advice.Cost) : null;
        if (advised!=null && plan!.Play!.Intent=="use") return advised;
        return candidates.OrderByDescending(candidate => score(candidate)
            - (ReferenceEquals(candidate,advised) && plan!.Play!.Intent=="hold" ? .85 : 0)).FirstOrDefault();
    }

    internal static async Task<List<Candidate>> Collect(PlayerStateDto player,
        Func<Guid,CancellationToken,Task<PlayabilityResultDto>> checkDefault,
        Func<Guid,PlayMode,CancellationToken,Task<PlayabilityResultDto>>? checkMode,
        CancellationToken token, ILogger? logger = null)
    {
        var result = new List<Candidate>();
        for (var index=0;index<player.Hand.Count;index++)
        {
            token.ThrowIfCancellationRequested();
            var original = player.Hand[index];
            try
            {
                var initial = await checkDefault(original.InstanceId, token);
                var selected = initial.SelectedPlayMode ?? PlayMode.Base;
                var modes = initial.PlayModes.Where(mode=>mode.IsAvailable).Select(mode=>mode.Mode).Distinct().ToArray();
                if (modes.Length==0 && initial.IsPlayable) modes=[selected];
                foreach (var mode in modes)
                {
                    var verified = mode==selected ? initial : checkMode==null ? null : await checkMode(original.InstanceId,mode,token);
                    if (verified?.IsPlayable!=true || (verified.SelectedPlayMode ?? PlayMode.Base)!=mode) continue;
                    var availability = verified.PlayModes.FirstOrDefault(entry=>entry.Mode==mode);
                    var cost = availability?.EffectiveCost ?? original.CurrentCost;
                    if (cost<0) continue;
                    var playedId = verified.PlayedCardDefinitionId ?? original.CardId;
                    var view = original with { CurrentCost=cost };
                    if (mode is PlayMode.Accelerate or PlayMode.Crystalize && playedId!=original.CardId
                        && GameDataProvider.Instance.GetCardDefinition(playedId) is { } definition)
                    {
                        var keywords = (definition.Keywords ?? []).Select(value=>Enum.TryParse<CardKeyword>(value,true,out var keyword)
                            ? (CardKeyword?)keyword : null).OfType<CardKeyword>().ToList();
                        // Alternate-play commit resets the original card's buffs/counters before rebinding.
                        view = new CardInstanceDto { InstanceId=original.InstanceId, OwnerPlayerId=original.OwnerPlayerId,
                            Zone=original.Zone, CardId=playedId, Cost=definition.Cost, CurrentCost=cost,
                            Name=definition.Name, Description=definition.Description, Type=definition.Type,
                            Class=definition.Class, Tribe=definition.Tribe ?? [], Attack=definition.Attack ?? 0,
                            Health=definition.Health ?? 0, CurrentAttack=definition.Attack ?? 0,
                            CurrentHealth=definition.Health ?? 0, MaxHealth=definition.Health ?? 0,
                            CurrentCountdown=definition.Countdown, Keywords=keywords };
                    }
                    result.Add(new(original,view,index,mode,cost,verified.RequiresTargetSelection,
                        mode==PlayMode.Base?null:verified.SelectedAbilityRuntimeId,playedId));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex,"AI skipped a card after playability check failed");
            }
        }
        return result;
    }
}
