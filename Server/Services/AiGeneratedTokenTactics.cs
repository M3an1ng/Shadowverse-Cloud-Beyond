using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// A small verified configuration shape, not a description/name parser or an arbitrary ability simulator.
internal static class AiGeneratedTokenTactics
{
    internal sealed record Recipe(int SourceId, int TokenId, int Count, int SelfDamage, int TokenCost, int TokenAttack);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static bool Plain(AbilityConfig config) => config.IsEnabled && config.Conditions.All(c => !c.IsEnabled)
        && config.PreConditions.All(c => !c.IsEnabled) && config.Properties.Count == 0 && config.Auras.All(a => !a.IsEnabled)
        && config.CounterRegistrations.Count == 0 && config.PlayerCounterRegistrations.Count == 0
        && config.AbilityKeywords.Count == 0;
    private static bool Simple(EffectConfig effect) => effect.IsEnabled && effect.RepeatCountValue == null
        && effect.RepeatCondition == null && effect.ChainedAbilities.Count == 0 && effect.Outputs?.Count is not > 0;
    private static object? Literal(EffectConfig effect, string key) =>
        effect.Parameters.GetValueOrDefault(key) is StaticValueConfig value ? value.Value : null;
    private static int? Integer(object? value)
    {
        if (value is JsonElement json) return json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var number) ? number : null;
        return value switch { int i => i, long l when l is >= int.MinValue and <= int.MaxValue => (int)l, _ => null };
    }
    private static string? Text(object? value) => value is JsonElement json && json.ValueKind == JsonValueKind.String
        ? json.GetString() : value as string;
    private static int? SingleId(object? value)
    {
        if (value is JsonElement json && json.ValueKind == JsonValueKind.Array)
            return json.GetArrayLength() == 1 ? Integer(json[0]) : null;
        if (value is IEnumerable<int> ids) { var list = ids.Take(2).ToArray(); return list.Length == 1 ? list[0] : null; }
        return null;
    }
    internal static int? Token(AbilityConfig config)
    {
        if (!Plain(config) || config.Targets.Any(t => t.Value.IsEnabled)) return null;
        var effects = config.Effects.Where(e => e.IsEnabled).ToArray();
        if (effects.Length != 1) return null;
        var effect = effects[0];
        if (effect.Type != EffectType.GetToken || !Simple(effect) || effect.RepeatCount != 0
            || effect.Parameters.Keys.Any(k => !new[] {"CardDefinitionId", "PlayerScope", "CardSourceMode", "Amount"}.Contains(k, StringComparer.OrdinalIgnoreCase))
            || Text(Literal(effect, "PlayerScope")) != "self" || Text(Literal(effect, "CardSourceMode")) != "definition"
            || Integer(Literal(effect, "Amount")) != 1) return null;
        return SingleId(Literal(effect, "CardDefinitionId")) is > 0 and var id ? id : null;
    }
    // Decision preflight can happen before earlier parent effects settle. Only accept a final,
    // unrepeated Choose, optionally preceded by a discard from our own hand (never a field unit).
    internal static bool SimpleSummonDecision(AbilityConfig config)
    {
        var trigger=config.Trigger;
        if(!config.IsEnabled||config.Type!=AbilityType.Triggered||!trigger.IsEnabled||trigger.CountLimit!=null
            ||trigger.LimitSameNameOncePerTransaction||trigger.RelationType!=TriggerRelationType.TargetSelf
            ||trigger.Timing!=TriggerTiming.On||trigger.EventTypes.Count!=1||trigger.EventTypes[0]!=EventType.PlayCard
            ||config.Conditions.Any(c=>c.IsEnabled)||config.PreConditions.Any(c=>c.IsEnabled)
            ||config.Properties.Count>0||config.Auras.Any(a=>a.IsEnabled)||config.CounterRegistrations.Count>0
            ||config.PlayerCounterRegistrations.Count>0||config.AlternatePlay!=null||config.ActivateCost!=null
            ||config.AbilityKeywords.Any(k=>k.Keyword is not (AbilityKeyword.Choose or AbilityKeyword.Reanimate)))return false;
        var effects=config.Effects.Where(e=>e.IsEnabled).OrderBy(e=>e.ExecutionOrder).ToArray();
        if(effects.Length is <1 or >2||effects.Any(e=>!Simple(e)||e.RepeatCount!=0))return false;
        var choose=effects[^1];
        if(choose.Type!=EffectType.Choose||choose.TargetSelector.Count>0||Integer(Literal(choose,"SelectionCount"))!=1
            ||choose.Parameters.Keys.Any(k=>!new[]{"AbilityConfigs","SelectionCount"}.Contains(k,StringComparer.OrdinalIgnoreCase)))return false;
        var targets=config.Targets.Values.Where(t=>t.IsEnabled).ToArray();
        if(effects.Length==1)return targets.Length==0;
        const string ownHand="((context.owner.hand.query((self.type == \"Follower\" || self.type == \"Spell\" || self.type == \"Amulet\"))) ~ (context.sourceCard))";
        string Normalize(string s)=>System.Text.RegularExpressions.Regex.Replace(s,"[\\s()]","").ToLowerInvariant();
        return effects[0].Type==EffectType.Discard&&effects[0].Parameters.Count==0&&effects[0].TargetSelector.Count==0
            &&targets.Length==1&&targets[0].Query.Operations.Count==0
            &&Normalize(targets[0].Query.SourcePoolExpression)==Normalize(ownHand)
            &&targets[0].PlayerChoiceOptions is {MinCount:1,MaxCount:1};
    }
    internal sealed record SummonAttack(int Slots,long Damage,bool CanEvolve=false,bool FreeEvolve=false);
    internal static SummonAttack? ReadSummonAttack(IGameDataProvider provider,AbilityConfig config)
    {
        if(!Plain(config)||config.Targets.Any(t=>t.Value.IsEnabled)||config.AlternatePlay!=null||config.ActivateCost!=null)return null;
        var effects=config.Effects.Where(e=>e.IsEnabled).ToArray();
        if(effects.Length is <1 or >5)return null;
        var slots=0;long damage=0;var canEvolve=false;var freeEvolve=false;
        foreach(var effect in effects)
        {
            if(effect.Type!=EffectType.Summon||!Simple(effect)||effect.RepeatCount!=0||effect.TargetSelector.Count!=0
                ||effect.Parameters.Keys.Any(k=>!new[]{"CardDefinitionId","SummonCount","PlayerScope","CardSourceMode"}.Contains(k,StringComparer.OrdinalIgnoreCase))
                ||Text(Literal(effect,"PlayerScope"))!="self"||Text(Literal(effect,"CardSourceMode"))!="definition"
                ||Integer(Literal(effect,"SummonCount")) is not int count||count is <1 or >5)return null;
            var raw=Literal(effect,"CardDefinitionId");int[] ids;
            if(raw is JsonElement {ValueKind:JsonValueKind.Array} json && json.GetArrayLength()<=5) {
                if(json.EnumerateArray().Any(v=>Integer(v) is not >0))return null;
                ids=json.EnumerateArray().Select(v=>Integer(v)!.Value).ToArray();
            } else if(raw is int[] list && list.Length<=5)ids=list;
            else if(raw is List<int> values && values.Count<=5)ids=values.ToArray();
            else return null;
            if(ids.Length==0||ids.Any(id=>id<=0))return null;
            foreach(var id in ids)
            {
                if(provider.GetCardDefinition(id) is not {Type:CardType.Follower,Attack:>=0,Health:>0} card)return null;
                var rules=provider.GetAbilityConfigs(id);
                if(rules==null||rules.Any(a=>a.IsEnabled&&(a.Auras.Any(v=>v.IsEnabled)||a.Type!=AbilityType.Triggered
                    ||a.Trigger.EventTypes.Any(t=>t is not (EventType.TurnEnd or EventType.LeaveField)))))return null;
                slots+=count;if(slots>5)return null;
                if(card.Keywords?.Contains(nameof(CardKeyword.Storm))==true
                    &&!card.Keywords.Any(k=>k is nameof(CardKeyword.Shackle) or nameof(CardKeyword.CannotAttackLeader)))
                {
                    damage+=(long)(card.Attack??0)*count;
                    if(!card.Keywords.Contains(nameof(CardKeyword.BanEvolve))){canEvolve=true;freeEvolve|=card.Keywords.Contains(nameof(CardKeyword.FreeEvolve));}
                }
            }
        }
        return damage>0||canEvolve?new(slots,damage,canEvolve,freeEvolve):null;
    }
    private static Ability? SummonSource(GameContext context,ShadowverseBeyond.Server.Domain.Entities.CardInstance source)
    {
        if(source.CardType!=CardType.Spell||source.IsSilenced||context.BuffManager.GetBuffsForCard(source.InstanceId).Count>0)return null;
        var abilities=context.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).OfType<Ability>()
            .Where(a=>a.Config.IsEnabled).ToArray();
        return abilities.Length==1&&SimpleSummonDecision(abilities[0].Config)?abilities[0]:null;
    }
    private static bool SummonScene(GameContext context,ShadowverseBeyond.Server.Dtos.PlayerStateDto player,
        ShadowverseBeyond.Server.Dtos.PlayerStateDto enemy,ShadowverseBeyond.Server.Domain.Entities.CardInstance source)
    {
        var game=context.CurrentGameState;
        return game.CurrentPlayerId==player.PlayerId&&game.Phase==GamePhase.Main&&!game.IsGameOver
            &&game.LegacyAiPlayerId!=player.PlayerId&&source.OwnerPlayerId==player.PlayerId
            &&enemy.Field.Count==0&&enemy.LeaderCard is {CurrentHealth:>0} leader&&!leader.Keywords.Contains(CardKeyword.Shield)
            &&player.LeaderCard is {CurrentHealth:>0}
            &&context.BuffManager.GetBuffsForCard(leader.InstanceId).Count==0
            &&!HasInterference(context,source);
    }
    private static bool SummonCanFinish(GameContext context,ShadowverseBeyond.Server.Dtos.PlayerStateDto player,
        ShadowverseBeyond.Server.Dtos.PlayerStateDto enemy,SummonAttack summon)
    {
        var actual=context.CurrentGameState.GetPlayerState(player.PlayerId);var leader=enemy.LeaderCard;
        if(actual==null||leader==null||actual.FieldOccupancyCount+summon.Slots>GameConstants.Field.MaxFieldSize)return false;
        var ready=player.Field.Where(c=>c.Type==CardType.Follower&&c.CanAttack&&c.CurrentAttack>=0&&c.CurrentHealth>0
            &&AiService.LegalAttackTargets(c,enemy).Any(target=>target.InstanceId==leader.InstanceId)).ToArray();
        if(ready.Any(c=>context.BuffManager.GetBuffsForCard(c.InstanceId).Count>0))return false;
        var current=ready.Sum(c=>(long)c.CurrentAttack*Math.Clamp(c.MaxAttacksPerTurn-c.AttacksThisTurn,0,5));
        long Bonus(bool free)=>Math.Max(player.IsEvolutionUnlocked&&player.CanEvolve
                &&(player.CurrentEP>=GameConstants.Evolution.EvolutionPointCost||free)?2:0,
            player.IsSuperEvolutionUnlocked&&player.CanSuperEvolve
                &&player.CurrentSEP>=GameConstants.SuperEvolution.SuperEvolutionPointCost?3:0);
        var boardBonus=ready.Where(c=>c.CanEvolve&&!c.IsEvolved&&!c.IsSuperEvolved&&!c.Keywords.Contains(CardKeyword.BanEvolve))
            .Select(c=>Bonus(c.Keywords.Contains(CardKeyword.FreeEvolve))*Math.Clamp(c.MaxAttacksPerTurn-c.AttacksThisTurn,0,5)).DefaultIfEmpty().Max();
        // Current face damage plus exactly one shared manual evolution, on a field body OR a new Storm.
        return current+summon.Damage+Math.Max(boardBonus,summon.CanEvolve?Bonus(summon.FreeEvolve):0)>=leader.CurrentHealth;
    }
    internal static SummonAttack[] ReadSummonLethals(GameContext context,ShadowverseBeyond.Server.Dtos.PlayerStateDto player,
        ShadowverseBeyond.Server.Dtos.PlayerStateDto enemy,ShadowverseBeyond.Server.Domain.Entities.CardInstance source)
    {
        if(AiSummonSourceState.For(context.CurrentGameState).DisabledPlayer==player.PlayerId||source.CurrentZone!=CardZone.Hand||!SummonScene(context,player,enemy,source)||SummonSource(context,source) is not {} ability)return [];
        var effects=ability.Config.Effects.Where(e=>e.IsEnabled).OrderBy(e=>e.ExecutionOrder).ToArray();
        if(effects.Length==2&&!player.Hand.Any(c=>c.InstanceId!=source.InstanceId&&c.Type is CardType.Follower or CardType.Spell or CardType.Amulet))return [];
        return Branches(effects[^1]).Select(config=>ReadSummonAttack(GameDataProvider.Instance,config))
            .Where(summon=>summon!=null&&SummonCanFinish(context,player,enemy,summon)).Cast<SummonAttack>().ToArray();
    }
    internal static Guid? PreferSummonLethal(ShadowverseBeyond.Server.Domain.Entities.GameState game,
        ShadowverseBeyond.Server.Dtos.PlayerStateDto player,ShadowverseBeyond.Server.Dtos.PlayerStateDto enemy,
        ShadowverseBeyond.Server.Domain.Abilities.Targets.PlayerChoiceRequest request,
        ShadowverseBeyond.Server.Domain.Entities.CardInstance? source)
    {
        var context=GameServiceProvider.CurrentGameContext;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game)||source==null
            ||request.TargetKey!="decision"||request.RequestingPlayerId!=player.PlayerId
            ||request.MinSelectionCount!=1||request.MaxSelectionCount!=1
            ||request.SelectableTargetIds.Count is <1 or >12||request.SelectableTargetIds.Any(id=>id==Guid.Empty)
            ||request.SelectableTargetIds.Distinct().Count()!=request.SelectableTargetIds.Count
            ||!SummonScene(context,player,enemy,source)||SummonSource(context,source) is not {} ability
            ||ability.RuntimeId!=request.SourceAbilityRuntimeId)return null;
        foreach(var id in request.SelectableTargetIds)
            if(request.DecisionOptions.TryGetValue(id,out var config)&&ReadSummonAttack(GameDataProvider.Instance,config) is {} summon
                &&SummonCanFinish(context,player,enemy,summon))return id;
        return null;
    }
    internal static AbilityConfig[] Branches(EffectConfig choose)
    {
        var value = Literal(choose, "AbilityConfigs");
        if (value is IEnumerable<AbilityConfig> configs) { var rows=configs.Take(13).ToArray();return rows.Length<=12?rows:[]; }
        if (value is not JsonElement { ValueKind: JsonValueKind.Array } json || json.GetArrayLength() > 12) return [];
        try { return JsonSerializer.Deserialize<AbilityConfig[]>(json.GetRawText(), Json) ?? []; }
        catch (JsonException) { return []; }
    }
    internal static bool HasInterference(GameContext context, ShadowverseBeyond.Server.Domain.Entities.CardInstance source,
        Func<ShadowverseBeyond.Server.Domain.Entities.CardInstance, AbilityConfig, bool>? recognizedResponse = null,
        Action<ShadowverseBeyond.Server.Domain.Entities.CardInstance, AbilityConfig>? onBlock = null)
    {
        // These are only visible cards plus our own hand. Never inspect the opponent's hidden cards.
        var game=context.CurrentGameState;
        var cards=game.AllField.Concat(game.AllEmblem).Concat(game.AllLeaders).Concat(game.GetPlayerState(source.OwnerPlayerId)!.Hand);
        var blocked = false;
        foreach(var card in cards.Where(card => card.InstanceId!=source.InstanceId && !card.IsSilenced))
            foreach(var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).OfType<Ability>())
            {
                var config=ability.Config;var trigger=config.Trigger;
                if(!config.IsEnabled || !trigger.IsEnabled || !trigger.IsValidZone(card.CurrentZone))continue;
                if (config.Type == AbilityType.Triggered && !config.Auras.Any(aura => aura.IsEnabled))
                {
                    if (trigger.EventTypes.All(type => type is EventType.TurnEnd or EventType.TurnStart or EventType.LeaveField)) continue;
                    if (card.CurrentZone == CardZone.Hand && trigger.RelationType == TriggerRelationType.TargetSelf
                        && trigger.EventTypes.All(type => type == EventType.PlayCard)) continue;
                    if (recognizedResponse?.Invoke(card, config) == true) continue;
                }
                // Unknown play, movement, damage and combat responses may change token costs, hand or lethal.
                if (onBlock == null) return true;
                onBlock(card, config);
                blocked = true;
            }
        return blocked;
    }
    internal static Recipe[] Read(IGameDataProvider provider, int sourceId, IEnumerable<AbilityConfig>? runtime = null)
    {
        if (provider.GetCardDefinition(sourceId)?.Type != CardType.Spell) return [];
        var configs = (runtime ?? provider.GetAbilityConfigs(sourceId) ?? []).Where(a => a.IsEnabled).ToArray();
        if (configs.Length != 1) return [];
        var config = configs[0]; var trigger = config.Trigger;
        if (!Plain(config) || config.Type != AbilityType.Triggered || !trigger.IsEnabled
            || trigger.RelationType != TriggerRelationType.TargetSelf || trigger.Timing != TriggerTiming.On
            || trigger.EventTypes.Count != 1 || trigger.EventTypes[0] != EventType.PlayCard
            || !trigger.ValidZones.Contains(CardZone.Hand) || trigger.CountLimit != null
            || trigger.LimitSameNameOncePerTransaction) return [];
        var effects = config.Effects.Where(e => e.IsEnabled).OrderBy(e => e.ExecutionOrder).ToArray();
        if (effects.Length is < 1 or > 2 || effects.Any(e => !Simple(e))) return [];
        var damage = 0;
        if (effects.Length == 2)
        {
            var hit = effects[0]; var targets = config.Targets.Where(t => t.Value.IsEnabled).ToArray();
            if (hit.Type != EffectType.Damage || hit.RepeatCount != 0 || targets.Length != 1
                || hit.TargetSelector.Count != 0 || targets[0].Value.PlayerChoiceOptions != null
                || targets[0].Value.GetEffectiveTargetHandlingStrategy() != ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetHandlingStrategy.DoNotIntercept
                || targets[0].Value.Query.Operations.Count != 0
                || targets[0].Value.Query.SourcePoolExpression.Trim() != "context.owner.leader"
                || hit.Parameters.Keys.Any(k => k is not ("DamageAmount" or "DistributionMode"))
                || Text(Literal(hit, "DistributionMode")) != "perTarget"
                || Integer(Literal(hit, "DamageAmount")) is not int amount || amount is < 0 or > 1000) return [];
            damage = amount;
        }
        else if (config.Targets.Any(t => t.Value.IsEnabled)) return [];
        var choose = effects[^1];
        if (choose.Type != EffectType.Choose || choose.RepeatCount is < 0 or > 4
            || Integer(Literal(choose, "SelectionCount")) != 1
            || choose.Parameters.Keys.Any(k => !new[] {"AbilityConfigs", "SelectionCount"}.Contains(k, StringComparer.OrdinalIgnoreCase))) return [];
        var result = new List<Recipe>();
        foreach (var branch in Branches(choose))
        {
            if (Token(branch) is not int id || provider.GetCardDefinition(id) is not { Type: CardType.Follower, Attack: > 0, Cost: >= 0 } token
                || token.Keywords?.Contains(nameof(CardKeyword.Storm)) != true
                || token.Keywords.Any(k => k is nameof(CardKeyword.CannotAttackLeader) or nameof(CardKeyword.Shackle))) continue;
            // Fanfare, damage/attack listeners and auras would invalidate this cheap generated-attack estimate.
            var tokenRules=provider.GetAbilityConfigs(id);
            if (tokenRules == null || tokenRules.Any(a => a.IsEnabled && (a.Auras.Any(v => v.IsEnabled)
                || a.Type != AbilityType.Triggered || a.Trigger.EventTypes.Any(t => t is not (EventType.TurnEnd or EventType.LeaveField))))) continue;
            result.Add(new(sourceId, id, choose.RepeatCount + 1, damage, token.Cost, token.Attack!.Value));
        }
        return result.ToArray();
    }
}
