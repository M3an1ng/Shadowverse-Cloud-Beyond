using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// Match the engine's committed event, not an action request or an unrelated triggered effect.
internal static class AiCommittedAction
{
    // Compare actual instance selections, including identical-looking abilities on the same card.
    internal static bool? SameSelection(string localType, object localData, string selectedType, object selectedData)
    {
        if (localType != selectedType) return false;
        return (localType, localData, selectedData) switch
        {
            ("PlayCard", PlayCardActionDto a, PlayCardActionDto b) => a.CardUid == b.CardUid
                && a.PlayMode == b.PlayMode && a.PaidCost == b.PaidCost && a.AbilityRuntimeId == b.AbilityRuntimeId
                && (a.TargetUids ?? []).SequenceEqual(b.TargetUids ?? []),
            ("PerformAttack", AttackActionDto a, AttackActionDto b) =>
                a.AttackerUid == b.AttackerUid && a.TargetUid == b.TargetUid,
            ("PerformEvolve", EvolveActionDto a, EvolveActionDto b) => a.UnitUid == b.UnitUid,
            ("PerformSuperEvolve", SuperEvolveActionDto a, SuperEvolveActionDto b) => a.UnitUid == b.UnitUid,
            ("RequestAct", ActActionDto a, ActActionDto b) => a.CardUid == b.CardUid && a.AbilityRuntimeId == b.AbilityRuntimeId,
            ("RequestFusion", FusionActionDto a, FusionActionDto b) => a.CardUid == b.CardUid && a.AbilityRuntimeId == b.AbilityRuntimeId,
            ("EndTurn" or "PerformPpBoost", _, _) => true,
            _ => null
        };
    }

    internal static bool Matches(AiStrategyPlanner.ActionOption selected, object? data, Guid player,
        IGameEvent gameEvent, Guid? pendingAbility)
    {
        bool Card(int? id) => !selected.CardId.HasValue || selected.CardId == id;
        return (selected.Kind, gameEvent) switch
        {
            ("PlayCard", PlayCardEvent play) => play.Source?.Player?.PlayerId == player
                && (data is PlayCardActionDto action
                    ? play.PlayedCard?.InstanceId == action.CardUid
                        && (!action.PlayMode.HasValue || play.PlayMode == action.PlayMode)
                        && (!action.PaidCost.HasValue || play.PaidCost == action.PaidCost)
                    : Card(play.PlayedCard?.BaseDefinitionId)
                        && (selected.PlayMode == null || play.PlayMode.ToString() == selected.PlayMode)
                        && (!selected.PaidCost.HasValue || play.PaidCost == selected.PaidCost)),
            ("PerformAttack", AttackEvent attack) => attack.Attacker?.OwnerPlayerId == player
                && (data is AttackActionDto action
                    ? attack.Attacker.InstanceId == action.AttackerUid && attack.TargetCard?.InstanceId == action.TargetUid
                    : Card(attack.Attacker.BaseDefinitionId) && (!selected.TargetCardId.HasValue
                        || attack.TargetCard?.BaseDefinitionId == selected.TargetCardId)),
            ("PerformEvolve", EvolveEvent evolve) => evolve.IsManualEvolution && !evolve.IsSuperEvolution
                && evolve.EvolvedCard?.OwnerPlayerId == player
                && (data is EvolveActionDto action ? Guid.TryParse(action.UnitUid, out var uid)
                    && evolve.EvolvedCard.InstanceId == uid : Card(evolve.EvolvedCard.BaseDefinitionId)),
            ("PerformSuperEvolve", SuperEvolveEvent evolve) => evolve.IsManualEvolution
                && evolve.SuperEvolvedCard?.OwnerPlayerId == player
                && (data is SuperEvolveActionDto action ? Guid.TryParse(action.UnitUid, out var uid)
                    && evolve.SuperEvolvedCard.InstanceId == uid : Card(evolve.SuperEvolvedCard.BaseDefinitionId)),
            ("RequestAct", ActInvokedEvent act) => act.ActPlayer?.PlayerId == player
                && (data is ActActionDto action ? act.ActCard?.InstanceId == action.CardUid
                    && (!action.AbilityRuntimeId.HasValue || act.TriggeredAbilityRuntimeId == action.AbilityRuntimeId)
                    : Card(act.ActCard?.BaseDefinitionId) && (!pendingAbility.HasValue || act.TriggeredAbilityRuntimeId == pendingAbility)),
            ("RequestFusion", FusionInvokedEvent fusion) => fusion.FusionPlayer?.PlayerId == player
                && (data is FusionActionDto action ? fusion.FusionCard?.InstanceId == action.CardUid
                    && (!action.AbilityRuntimeId.HasValue || fusion.TriggeredAbilityRuntimeId == action.AbilityRuntimeId)
                    : Card(fusion.FusionCard?.BaseDefinitionId) && (!pendingAbility.HasValue || fusion.TriggeredAbilityRuntimeId == pendingAbility)),
            ("EndTurn", TurnEndEvent ending) => ending.EndingPlayer?.PlayerId == player,
            ("PerformPpBoost", PpBoostStateUpdatedEvent boost) => boost.Source?.Player?.PlayerId == player,
            _ => false
        };
    }
}
