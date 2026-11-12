using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// User-supplied deck role, gated by loaded rules and the actor's own initial deck. Not an effect simulator.
internal static class AiEternalEngine
{
    internal const int Reiss=1474120, Emblem=1474276, Search=1474113;
    private sealed record Context(Guid Actor,bool InDeck);
    private static readonly AsyncLocal<Context?> Current=new();
    private sealed class Lease(Action release):IDisposable {public void Dispose()=>Interlocked.Exchange(ref release,null!)?.Invoke();}
    internal static IDisposable Use(GameState? game,Guid actor)
    {
        var before=Current.Value;
        var deck=game?.GetPlayerState(actor)?.InitialDeckDefinitionIds;
        Current.Value=new(actor,game!=null&&game.LegacyAiPlayerId!=actor&&game.LegacyEternalEnginePlayerId!=actor
            &&deck?.Contains(Reiss)==true&&deck.Contains(Search));
        return new Lease(()=>Current.Value=before);
    }
    private sealed class Cache {internal Guid Revision;internal bool Valid;}
    private static readonly ConditionalWeakTable<IGameDataProvider,Cache> Profiles=new();
    private static string Normalize(string text)=>Regex.Replace(text,"[\\s()]","").ToLowerInvariant();
    private static object? Literal(EffectConfig effect,string key)=>effect.Parameters.FirstOrDefault(p=>p.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).Value is StaticValueConfig literal?literal.Value:null;
    private static string? Text(object? value)=>value is JsonElement {ValueKind:JsonValueKind.String} json?json.GetString():value as string;
    private static int[] Ids(object? value)=>value switch {
        IEnumerable<int> ids=>ids.ToArray(),JsonElement {ValueKind:JsonValueKind.Array} json=>json.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out _)).Select(v=>v.GetInt32()).ToArray(),_=>[]};
    private static bool Positive(object? value)=>value is int i?i>0:value is long l?l>0:value is JsonElement {ValueKind:JsonValueKind.Number} j&&j.TryGetInt32(out var n)&&n>0;
    private static IEnumerable<EffectConfig> Effects(AbilityConfig config,int depth=0)=>depth>8?[]:config.Effects.Where(e=>e.IsEnabled)
        .SelectMany(e=>new[]{e}.Concat(e.ChainedAbilities.Where(a=>a.IsEnabled).SelectMany(a=>Effects(a,depth+1))));
    internal static bool RulesValid()
    {
        var provider=GameDataProvider.Instance;if(provider==null)return false;
        var cache=Profiles.GetValue(provider,_=>new());lock(cache)
        {
            var revision=provider.DataRevision;if(revision!=Guid.Empty&&cache.Revision==revision)return cache.Valid;
            var reiss=provider.GetCardDefinition(Reiss);var emblem=provider.GetCardDefinition(Emblem);
            var grant=provider.GetAbilityConfigs(Reiss)?.Any(a=>a.IsEnabled&&a.Trigger.IsEnabled
                &&a.Trigger.EventTypes.Contains(EventType.PlayCard)&&a.Trigger.RelationType==TriggerRelationType.TargetSelf
                &&a.Conditions.All(c=>!c.IsEnabled)&&Effects(a).Any(e=>e.Type==EffectType.AddEmblem
                    &&Ids(Literal(e,"CardDefinitionId")).Contains(Emblem)&&Text(Literal(e,"PlayerScope"))=="self"));
            var refill=provider.GetAbilityConfigs(Emblem)?.Any(a=>a.IsEnabled&&a.Trigger.IsEnabled
                &&a.Trigger.EventTypes.Contains(EventType.TurnEnd)&&a.Trigger.ValidZones.Contains(CardZone.Emblem)
                &&a.Conditions.Any(c=>c.IsEnabled&&Normalize(c.Expression)=="countcontext.owner.hand==0")
                &&a.Conditions.Any(c=>c.IsEnabled&&Normalize(c.Expression)=="context.triggeringevent.endingplayer.playerid==context.owner.playerid")
                &&Effects(a).Any(e=>e.Type==EffectType.Draw&&Positive(Literal(e,"DrawAmount"))&&Text(Literal(e,"PlayerScope"))=="self"));
            cache.Valid=reiss is {Type:CardType.Follower,Cost:2}&&emblem?.Type==CardType.Crest&&grant==true&&refill==true;
            cache.Revision=revision;return cache.Valid;
        }
    }
    internal static bool Active(PlayerStateDto player)=>Current.Value is {InDeck:true} context&&context.Actor==player.PlayerId&&RulesValid();
    internal static bool Established(PlayerStateDto player)=>player.Emblem.Any(card=>card.CardId==Emblem);
    internal static string[] Principles(IEnumerable<int> deck)=>deck.Contains(Reiss)&&deck.Contains(Search)&&RulesValid()?[
        "本牌组的用户战术定位：#1474120建立#1474276是空手体系的首要资源核心。"+(deck.Contains(1473441)?"#1473441是没有其他有效出牌或确有血量压力时才考虑的辅助牌，健康血量下不为按费拍它而挤掉卸手牌、铺场和资源引擎。":""),
        "没有芮丝纹章且没有其他已核对的后续资源时，不把单纯空手当作成功。保留第一张芮丝或荒诞夜恸的检索入口，避免作为普通弃牌或融合素材耗掉。",
        "调度优先找芮丝和荒诞夜恸。前期可以先压场抢节奏，三费或四费再检索并当回合接芮丝；不固定要求一费检索、二费使用。检索来的芮丝会因使用其他牌而自弃，拿到后核对顺序。斩杀和紧急保命仍优先。"
    ]:[];
    internal static object? ForModel(PlayerStateDto player)=>!Active(player)?null:new {
        coreCardId=Reiss,resourceEmblemId=Emblem,emblemEstablished=Established(player),
        coreHandIndexes=player.Hand.Select((c,i)=>(c,i)).Where(x=>x.c.CardId==Reiss).Select(x=>x.i),
        accessHandIndexes=player.Hand.Select((c,i)=>(c,i)).Where(x=>x.c.CardId==Search).Select(x=>x.i),
        stage=Established(player)?"convert-empty-hand-to-resources":"establish-resource-engine-before-spending-last-access",
        handEconomy="纹章建立后，不为保留高费牌长期放弃空手续航。宿醉可作为有弃牌收益的素材；若打出后已无PP且当回合降费仍用不掉剩余手牌，不把降费当成已兑现收益。低血量评估蝙蝠进化吸血与剩余敌方场攻，夜莺主要用于无其他有效出牌或血量压力。",
        scope="User deck principle and visible own state; not a guarantee of search success or a simulation."};
    internal static List<string>? Mulligan(PlayerStateDto player)
    {
        if(!Active(player))return null;
        var keep=new HashSet<Guid>();
        foreach(var id in new[]{Reiss,Search})if(player.Hand.FirstOrDefault(c=>c.CardId==id) is {} first)keep.Add(first.InstanceId);
        // Keep one early body while looking for access; a held engine already permits a normal cheap curve.
        if(keep.Count==0) {
            var tempo=player.Hand.Where(c=>c.Type==CardType.Follower&&c.Cost<=1).OrderByDescending(c=>c.Attack+c.Health).FirstOrDefault();
            if(tempo!=null)keep.Add(tempo.InstanceId);
        } else foreach(var card in player.Hand.Where(c=>c.Cost<=2&&c.CardId is not (Reiss or Search)))keep.Add(card.InstanceId);
        return player.Hand.Where(c=>!keep.Contains(c.InstanceId)).Select(c=>c.InstanceId.ToString()).ToList();
    }
    internal static double Retention(PlayerStateDto player,CardInstanceDto card)
    {
        if(!Active(player)||Established(player)||player.Hand.FirstOrDefault(c=>c.CardId==card.CardId)?.InstanceId!=card.InstanceId)return 0;
        if(card.CardId==Reiss)return 6; // Unaffordable this turn still carries the next turn's resource engine.
        return card.CardId==Search&&!player.Hand.Any(c=>c.CardId==Reiss)?4.5:0;
    }
    internal static double PlayBias(PlayerStateDto player,CardInstanceDto card,int paidCost)
    {
        if(!Active(player)||Established(player))return 0;
        if(card.CardId!=Reiss&&player.Hand.Any(c=>c.CardId==Reiss&&AiAbilityScoring.FragileHandPriority(player,c)>0))return -8;
        if(card.CardId==Reiss)return 2.5;
        if(card.CardId==Search&&player.Hand.Count==2&&player.Hand.Any(c=>c.CardId==Reiss))return -8;
        if(card.CardId!=Search||player.Hand.Any(c=>c.CardId==Reiss))return 0;
        if(player.CurrentPP>=paidCost+2&&player.Field.Count<GameConstants.Field.MaxFieldSize)return 1.6;
        return player.Hand.Any(c=>c.InstanceId!=card.InstanceId&&c.Type==CardType.Follower&&c.CurrentCost<=player.CurrentPP)?-.8:0;
    }
    internal static double RoleBias(PlayerStateDto player,PlayerStateDto? enemy,CardInstanceDto card,bool hasOtherPlayable)
    {
        if(!AiRecoveryPolicy.Enabled||!Active(player)||card.CardId!=1473441||!hasOtherPlayable||player.LeaderCard is not {} leader||enemy==null)return 0;
        var damage=AiTacticalSearch.EstimateBoardDamage(enemy.Field,player.Field,leader);
        // User-confirmed role: filler or recovery under pressure, rather than an on-curve engine.
        return leader.CurrentHealth>=12&&leader.CurrentHealth-damage>=8?-6:0;
    }
    internal static bool IsSearchBranch(AbilityConfig config)
    {
        const string expected="context.owner.deck.query(((self.cardType) == (\"Follower\"))).query(((self.currentDefinition.class) == (\"Abysscraft\"))).query(((self.currentCost) == (2))).distinct((self.currentDefinition.id)).take(random (2))";
        return config.IsEnabled&&config.Conditions.All(c=>!c.IsEnabled)&&config.Effects.Any(e=>e.IsEnabled&&e.Type==EffectType.Draw
            &&Literal(e,"UseSearchMode") is {} search&&(search is true||search is JsonElement {ValueKind:JsonValueKind.True})
            &&e.Parameters.FirstOrDefault(p=>p.Key.Equals("SearchTargets",StringComparison.OrdinalIgnoreCase)).Value is FormulaValueConfig formula
            &&Normalize(formula.Formula)==Normalize(expected)&&Text(Literal(e,"PlayerScope"))=="self");
    }
    internal static Guid? PreferSearch(PlayerStateDto player,PlayerStateDto enemy,GameState game,PlayerChoiceRequest request,CardInstance? source)
    {
        if(!Active(player)||Established(player)||player.Hand.Any(c=>c.CardId==Reiss)||source?.BaseDefinitionId!=Search
            ||request.TargetKey!="decision"||request.MinSelectionCount!=1||request.MaxSelectionCount!=1
            ||game.GetPlayerState(player.PlayerId)?.Deck.Any(c=>c.CurrentDefinitionId==Reiss&&c.CurrentCost==2&&c.CurrentDefinition?.Class==CardClass.Abysscraft)!=true
            ||AiTacticalSearch.HasVisibleLethalThreat(player,enemy))return null;
        var options=request.SelectableTargetIds.Where(id=>request.DecisionOptions.TryGetValue(id,out var cfg)&&IsSearchBranch(cfg)).ToArray();
        return options.Length==1?options[0]:null;
    }
}
