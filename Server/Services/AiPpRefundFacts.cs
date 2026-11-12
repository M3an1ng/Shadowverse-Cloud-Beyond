using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Conditional budget hypotheses, never PP granted to the engine. Continuation needs committed evidence.
// Only current own hand is read. Unknown branches/conditions never become spendable PP.
internal static class AiPpRefundFacts
{
    internal sealed record Refund(int HandIndex,int CardId,int BaseCost,int PaidBlood,int OwnDamage,int RestoreUpTo,int PpAfterPayment,int PpIfResolved,bool BloodAvailable,bool SurvivesKnownSelfDamage)
    {public string? DestroyTargetKey{get;init;}public long? AbilityDefinitionId{get;init;}}
    internal sealed record Catalog(bool Available,string Scope,Refund[] Options,string? Reason=null);
    private static object? Literal(EffectConfig effect,string key)=>effect.Parameters.GetValueOrDefault(key) is StaticValueConfig s?s.Value:null;
    private static int? Number(object? value)=>value is int i?i:value is JsonElement {ValueKind:JsonValueKind.Number} j&&j.TryGetInt32(out var n)?n:null;
    private static string? Text(object? value)=>value is string s?s:value is JsonElement {ValueKind:JsonValueKind.String} j?j.GetString():null;
    private static bool Once(EffectConfig effect)=>effect.IsEnabled&&effect.RepeatCount==0&&effect.RepeatCountValue==null&&effect.RepeatCondition==null;
    internal static bool TryReadOwnDamageRefund(EffectConfig fee, out int blood, out int damage, out int refund)
    {
        blood = damage = refund = 0;
        if (!Once(fee) || fee.Type != EffectType.Sacrifice || Text(Literal(fee, "PlayerScope")) != "self"
            || Number(Literal(fee, "Amount")) is not int cost || cost is < 0 or > 100
            || fee.Parameters.Keys.Any(key => key is not ("Amount" or "PlayerScope"))
            || fee.Outputs?.Count is > 0 || fee.ChainedAbilities.Count != 1) return false;
        var child = fee.ChainedAbilities[0];
        var effects = child.Effects.Where(effect => effect.IsEnabled).ToArray();
        if (!child.IsEnabled || child.Type != AbilityType.Triggered || child.Conditions.Any(c => c.IsEnabled)
            || child.PreConditions.Any(c => c.IsEnabled) || child.Auras.Any(a => a.IsEnabled) || child.Properties.Count > 0
            || child.AbilityKeywords.Count > 0 || child.CounterRegistrations.Count > 0 || child.PlayerCounterRegistrations.Count > 0
            || effects.Length != 2 || effects.Any(effect => !Once(effect) || effect.ChainedAbilities.Count > 0 || effect.Outputs?.Count is > 0))
            return false;
        var selfHit = effects[0];
        var restore = effects[1];
        if (selfHit.Type != EffectType.Damage || AiTargetScoring.ReadAmount(selfHit) is not double hit || hit < 0 || hit > 1000
            || restore.Type != EffectType.SetPP || Text(Literal(restore, "PlayerScope")) != "self"
            || Number(Literal(restore, "ModifyAmount")) is not int pp || pp is <= 0 or > 10
            || selfHit.Parameters.Keys.Any(key => key is not ("DamageAmount" or "DistributionMode"))
            || restore.Parameters.Keys.Any(key => key is not ("ModifyAmount" or "PlayerScope"))) return false;
        var targets = child.Targets.Where(pair => pair.Value.IsEnabled).ToArray();
        if (targets.Length != 1 || targets[0].Value.Query.Operations.Count > 0 || targets[0].Value.PlayerChoiceOptions != null
            || !targets[0].Value.Query.SourcePoolExpression.Equals("context.owner.leader", StringComparison.OrdinalIgnoreCase)
            || selfHit.TargetSelector.Any(key => key != targets[0].Key) || restore.TargetSelector.Count > 0) return false;
        (blood, damage, refund) = (cost, (int)hit, pp);
        return true;
    }

    internal static Refund[] Read(GameContext context,PlayerStateDto player)
    {
        if(context.CurrentGameState.CurrentPlayerId!=player.PlayerId||context.CurrentGameState.IsGameOver)return [];
        var result=new List<Refund>();var owner=context.CurrentGameState.GetPlayerState(player.PlayerId)!;
        foreach(var (view,index) in player.Hand.Select((c,i)=>(c,i))){
            var card=owner.Hand.FirstOrDefault(c=>c.InstanceId==view.InstanceId);
            if(card==null||card.IsSilenced||card.CurrentDefinitionId!=view.CardId||card.CurrentCost!=view.CurrentCost||card.CurrentCost>player.CurrentPP)continue;
            var abilities=context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).ToArray();
            if(abilities.Any(a=>a is not Ability))continue;
            foreach(var a in abilities.OfType<Ability>().Where(a=>a.Config.IsEnabled&&a.TriggerConfig.IsEnabled&&a.TriggerConfig.IsValidZone(CardZone.Hand))){
                var config=a.Config;
                if(config.Type!=AbilityType.Triggered||!config.HasKeyword(AbilityKeyword.Fanfare)||config.ActivateCost!=null||config.AlternatePlay!=null
                    ||!a.TriggerConfig.EventTypes.SequenceEqual(new[]{EventType.PlayCard})||a.TriggerConfig.RelationType!=TriggerRelationType.TargetSelf
                    ||a.TriggerConfig.Timing!=TriggerTiming.On||a.TriggerConfig.CountLimit!=null||a.TriggerConfig.LimitSameNameOncePerTransaction
                    ||config.Conditions.Any(c=>c.IsEnabled)||config.PreConditions.Any(c=>c.IsEnabled)||config.Auras.Any(a=>a.IsEnabled)||config.Properties.Count>0)continue;
                foreach (var fee in config.Effects)
                {
                    if (!TryReadOwnDamageRefund(fee, out var blood, out var hit, out var amount)) continue;
                    var pp=player.CurrentPP-view.CurrentCost;
                    var rootEffects=config.Effects.Where(e=>e.IsEnabled).ToArray();
                    var rootTargets=config.Targets.Where(t=>t.Value.IsEnabled).ToArray();
                    var removalKey=rootEffects.Length==2&&ReferenceEquals(rootEffects[1],fee)&&rootEffects[0].Type==EffectType.Destroy
                        &&Once(rootEffects[0])&&rootEffects[0].ChainedAbilities.Count==0&&rootEffects[0].Parameters.Count==0
                        &&rootEffects[0].Outputs?.Count is not >0&&rootTargets.Length==1
                        &&rootTargets[0].Value.PlayerChoiceOptions is {IsRequired:true,MinCount:1,MaxCount:1}
                        &&rootEffects[0].TargetSelector.All(k=>k==rootTargets[0].Key||k=="*")
                        &&AiRuntimeAbilityFacts.SafeConfiguration(config)?rootTargets[0].Key:null;
                    result.Add(new(index,view.CardId,view.CurrentCost,blood,hit,amount,pp,Math.Min(player.MaxPP,pp+amount),player.BloodPool>=blood,player.LeaderCard?.CurrentHealth>hit)
                        {DestroyTargetKey=removalKey,AbilityDefinitionId=removalKey==null?null:config.Id});
                }
            }
        }
        return result.ToArray();
    }
    internal static Catalog ForGame(GameState game,GameStateDto snapshot,Guid actor)
    {
        var context=GameServiceProvider.CurrentGameContext;
        var own=snapshot.Players.FirstOrDefault(p=>p.PlayerId==actor);
        var available=context!=null&&ReferenceEquals(context.CurrentGameState,game)&&own!=null;
        var revision=game.StateRevision;Refund[] options=[];
        try{if(available)options=Read(context!,own!);if(game.StateRevision!=revision){available=false;options=[];}}
        catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){available=false;options=[];}
        return new(available,"Conditional own-hand Fanfare Sacrifice -> self Damage -> SetPP rule. Possible refunds are not spendable PP. destroyTargetKey identifies the narrow direct Destroy + paid self-Damage + SetPP route that can be recorded conditionally in plays with an explicit enemy-field target; continuation requires actual committed payment/refund/damage/removal and fresh legality. Payment-only lists are not exhaustive.",options);
    }
}
