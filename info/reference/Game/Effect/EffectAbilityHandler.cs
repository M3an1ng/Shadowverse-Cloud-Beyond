using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon.StructWrapping;
using UnityEngine;
using Random = UnityEngine.Random;

public static class EffectAbilityHandler 
{
    public static Battle Battle => Player.currentBattle;
    public static BattleManager Hud => BattleManager.instance;

    public static Func<Effect, BattleState, bool> GetAbilityFunc(EffectAbility ability) {
        //TODO 提取公共部分进行预处理 输入 输出
        return ability switch {
            EffectAbility.SetResult     => SetResult,
            EffectAbility.KeepCard      => KeepCard,
            EffectAbility.TurnStart     => OnTurnStart,
            EffectAbility.TurnEnd       => OnTurnEnd,
            EffectAbility.Play          => Play,
            EffectAbility.Attack        => Attack,
            EffectAbility.Evolve        => Evolve,
            EffectAbility.Fusion        => Fusion,
            EffectAbility.ChangeFlag    => ChangeFlag,

            EffectAbility.Random        => RandomEffect,

            EffectAbility.SetKeyword    => SetKeyword,
            EffectAbility.Draw          => Draw,
            EffectAbility.Summon        => Summon,
            EffectAbility.Damage        => Damage,
            EffectAbility.Heal          => Heal,
            EffectAbility.Destroy       => Destroy,
            EffectAbility.Vanish        => Vanish,
            EffectAbility.Return        => Return,
            EffectAbility.Transform     => Transform,
            EffectAbility.SetStatus     => SetStatus,
            EffectAbility.RemoveBuff    => RemoveBuff,

            EffectAbility.GetToken      => GetToken,
            EffectAbility.SpellBoost    => SpellBoost,
            EffectAbility.SetCost       => SetCost,
            EffectAbility.Ramp          => Ramp,
            EffectAbility.AddEffect     => AddEffect,
            EffectAbility.RemoveEffect  => RemoveEffect,
            EffectAbility.SetGrave      => SetGrave,
            EffectAbility.SetCountdown  => SetCountdown,
            EffectAbility.AddDeck       => AddDeck,
            EffectAbility.Hybrid        => Hybrid,

            EffectAbility.Bury          => Bury,
            EffectAbility.Reanimate     => Reanimate,
            EffectAbility.Discard       => Discard,
            EffectAbility.Travel        => Travel,
            EffectAbility.Reveal        => Reveal,
            EffectAbility.SetPP         => SetPP,
            EffectAbility.SetEP         => SetEP,
            EffectAbility.SetCharge     => SetCharge,
            EffectAbility.Choose        => Choose,
            EffectAbility.AddBuff       => AddBuff,
            EffectAbility.SetValue      => SetValue,

            _ => (e, s) => true,
        };
    }

    /// <summary>
    /// Check if cards have effects with correct timing and condition.
    /// Enqueue these effects with corresponding invoke unit.
    /// </summary>
    public static List<Effect> EnqueueEffect(string timing, List<BattleCard> battleCards, BattleState state, bool enqueueToBattle = true) {
        var list = new List<Effect>();
        var units = battleCards.Select(state.GetBelongUnit).ToList();
        var cards = battleCards.Select(x => x.CurrentCard).ToList();

        var baseTiming = timing.TrimStart("on_");
        var beforeTiming = "before_" + baseTiming;
        var afterTiming = "after_" + baseTiming;
        var otherTiming = "on_other_" + baseTiming;
        var beforeOtherTiming = "before_other_" + baseTiming;
        var afterOtherTiming = "after_other_" + baseTiming;
        /*var phase = EffectPhase.On;
        if (timing.StartsWith(EffectPhase.Before.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            phase = EffectPhase.Before;
        }else if (timing.StartsWith(EffectPhase.After.ToString(), StringComparison.OrdinalIgnoreCase)){
            phase = EffectPhase.After;
        }
        var otherTiming = phase + "_other_" + timing.TrimStart(phase + "_");*/

        for (int i = 0; i < cards.Count; i++) {
            if (cards[i] == null)
                continue;

            var effects = cards[i].effects;
            var place = units[i].GetBelongPlace(battleCards[i]);

            for (int j = 0; j < effects.Count; j++) {
                if (effects[j].place != BattlePlaceId.None && effects[j].place != place.PlaceId)
                    continue;

                var phase = EffectPhase.On;

                bool isOnTiming = (effects[j].timing == timing);
                bool isBeforeTiming = (effects[j].timing == beforeTiming);
                bool isAfterTiming = (effects[j].timing == afterTiming);
                bool isOnOtherTiming = (effects[j].timing == otherTiming) && (state.currentEffect.source != battleCards[i]);
                bool isBeforeOtherTiming = (effects[j].timing == beforeOtherTiming) && (state.currentEffect.source != battleCards[i]);
                bool isAfterOtherTiming = (effects[j].timing == afterOtherTiming) && (state.currentEffect.source != battleCards[i]);

                if (isOnTiming || isOnOtherTiming)
                    phase = EffectPhase.On;
                else if (isBeforeTiming || isBeforeOtherTiming)
                    phase = EffectPhase.Before;
                else if (isAfterTiming || isAfterOtherTiming)
                    phase = EffectPhase.After;
                else
                    continue;

                var tmpSourceEffect = effects[j].sourceEffect;
                var tmpInvokeUnit = effects[j].invokeUnit;

                effects[j].sourceEffect = (state.currentEffect == null) ? null : new Effect(state.currentEffect);
                effects[j].invokeUnit = units[i];
 
                if (phase.Equals(EffectPhase.After) || effects[j].Condition(state)) {
                    effects[j].sourcePlace = timing.EndsWith("this_play") ? null : place;
                    list.Add(effects[j]);

                    if (enqueueToBattle)
                        if (phase == EffectPhase.Before)
                            effects[j].ImmediateApply(state);
                        else
                            Battle.EnqueueEffect(effects[j], phase);
                } else {
                    effects[j].sourceEffect = tmpSourceEffect;
                    effects[j].invokeUnit = tmpInvokeUnit;
                }
            }
        }
        for (int n = 0; n < battleCards.Count; n++)
        {
            var buffs = battleCards[n].buffController.buffs;
            for (int m = 0; m < buffs.Count; m++)
            {
                var phase = EffectPhase.On;

                bool isOnTiming = (buffs[m].timing == timing);
                bool isBeforeTiming = (buffs[m].timing == beforeTiming);
                bool isAfterTiming = (buffs[m].timing == afterTiming);
                bool isOnOtherTiming = (buffs[m].timing == otherTiming) && (state.currentEffect.source != battleCards[n]);
                bool isBeforeOtherTiming = (buffs[m].timing == beforeOtherTiming) && (state.currentEffect.source != battleCards[n]);
                bool isAfterOtherTiming = (buffs[m].timing == afterOtherTiming) && (state.currentEffect.source != battleCards[n]);

                if (isOnTiming || isOnOtherTiming)
                    phase = EffectPhase.On;
                else if (isBeforeTiming || isBeforeOtherTiming)
                    phase = EffectPhase.Before;
                else if (isAfterTiming || isAfterOtherTiming)
                    phase = EffectPhase.After;
                else
                    continue;

                if (phase.Equals(EffectPhase.After) || buffs[m].Condition(state))
                {
                    var removeBuffEffect = new Effect("none", "self", null, null, EffectAbility.RemoveBuff, null)
                    {
                        source = battleCards[n],
                        sourceBuff = buffs[m],
                        sourceEffect = (state.currentEffect == null) ? null : new Effect(state.currentEffect),
                        invokeUnit = units[n],
                    };
                    list.Add(removeBuffEffect);
                    if (enqueueToBattle)
                        if (phase == EffectPhase.Before)
                            removeBuffEffect.ImmediateApply(state);
                        else
                            Battle.EnqueueEffect(removeBuffEffect, phase);
                }
            }
        }
        return list;                                                     
    }

    /// <summary>
    /// Check if cards have effects with correct timing and condition.
    /// Enqueue these effects with corresponding invoke unit.
    /// </summary>
    public static List<Effect> ListEffect(EffectAbility ability, List<BattleCard> battleCards, BattleState state)
    {
        var list = new List<Effect>();
        var units = battleCards.Select(state.GetBelongUnit).ToList();
        var cards = battleCards.Select(x => x.CurrentCard).ToList();

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null)
                continue;

            var effects = cards[i].effects;
            var place = units[i].GetBelongPlace(battleCards[i]);

            for (int j = 0; j < effects.Count; j++)
            {
                if (effects[j].ability != ability)
                    continue;

                var tmpSourceEffect = effects[j].sourceEffect;
                var tmpInvokeUnit = effects[j].invokeUnit;

                //effects[j].sourceEffect = (state.currentEffect == null) ? null : new Effect(state.currentEffect);
                effects[j].sourceEffect = state.currentEffect;
                effects[j].invokeUnit = units[i];


                if (effects[j].Condition(state))
                {
                    effects[j].sourcePlace = effects[j].timing.EndsWith("this_play") ? null : place;
                    list.Add(effects[j]);
                }
                else
                {
                    effects[j].sourceEffect = tmpSourceEffect;
                    effects[j].invokeUnit = tmpInvokeUnit;
                }
            }
        }
        return list;
    }

    /// <summary>
    /// Check hand, leader, territory, field, deck cards, <br/>
    /// and enqueue effects with correct timing and condition.
    /// </summary>
    public static List<Effect> OnPhaseChange(string timing, BattleState state, bool enqueueToBattle = true) {
        var lhsUnit = state.currentUnit;
        var rhsUnit = state.GetRhsUnitById(lhsUnit.id);
        var cards = lhsUnit.hand.cards.Concat(rhsUnit.hand.cards);
        
        var result = cards.Concat(lhsUnit.leader.cards).Concat(rhsUnit.leader.cards)
            .Concat(lhsUnit.field.cards).Concat(rhsUnit.field.cards)
            .Concat(lhsUnit.deck.cards).Concat(rhsUnit.deck.cards).ToList();

        return EnqueueEffect(timing, result, state, enqueueToBattle);
    }

    public static void OnStateChange(BattleState state)
    {
        var newLhsUnit = state.currentUnit;
        var newRhsUnit = state.GetRhsUnitById(newLhsUnit.id);
        var oldLhsUnit = Hud.CurrentState.currentUnit;
        var oldRhsUnit = Hud.CurrentState.GetRhsUnitById(oldLhsUnit.id);
        Hud.SetState(state);
        List<string> flagList = new() { "isVenge", "isAwake", "isEager"}; // 添加所有需要检测的状态名称

        foreach (var flag in flagList)
        {
            ApplyFlagChange(state, oldLhsUnit, newLhsUnit, flag);
            ApplyFlagChange(state, oldRhsUnit, newRhsUnit, flag);
        }
    }

    private static void ApplyFlagChange(BattleState state, BattleUnit oldUnit, BattleUnit newUnit, string flag)
    {
        var newStateValue = newUnit.GetIdentifier(flag);
        var oldStateValue = oldUnit.GetIdentifier(flag);

        if (newStateValue != oldStateValue)
        {
            Effect flagChange = new Effect("none", "none", null, null,
                EffectAbility.ChangeFlag, new Dictionary<string, string>() { { "name", flag.TrimStart("is").ToLower() }, { "value", newStateValue.ToString() } })
            {
                invokeUnit = newUnit,
            };
            flagChange.ImmediateApply(state);
        }
    }

    public static bool Preprocess(this Effect effect, BattleState state) {
        string trimId;
        var unit = effect.invokeUnit;
        var timing = effect.timing;
        var beforeTiming = "before_" + timing.TrimStart("on_");

        state.currentEffect = effect;
        effect.SetInvokeTarget(state);

        // Check if still in place.
        if ((effect.sourcePlace != null) && (state.GetBelongPlace(effect.source) != effect.sourcePlace))
            return false;

        /*//Check Before Timing
        if(!timing.StartsWith("before") && !timing.StartsWith("none"))
        {
            var beforeEffects = OnPhaseChange(beforeTiming, state, false);
            foreach(var beforeEffect in beforeEffects)
            {
                beforeEffect.Apply();
                state.currentEffect = effect;
            }
        }*/

        if (bool.Parse(effect.abilityOptionDict.Get("break", "false")))
            return false;
            
        // Enhance.
        if (effect.abilityOptionDict.TryGetValue("enhance", out trimId)) {
            var enhance = Parser.UpdateEffectExpression("enhance", effect, state);
            var useCost = int.Parse(effect.sourceEffect.abilityOptionDict.Get("useCost", "-1"));
            var useSituation = effect.sourceEffect.abilityOptionDict.Get("useSituation", string.Empty);

            if ((useSituation != "enhance") || (useCost < enhance))
                return false;
        }

        // Earth
        if (effect.abilityOptionDict.TryGetValue("earth", out trimId)) {
            var earth = Parser.UpdateEffectExpression("earth", effect, state);
            var filter = BattleCardFilter.Parse("[trait:3][type:3]");
            var cards = filter.FilterCurrentCardList(unit.field.cards);

            if (unit.field.cards.Sum(x => x.CurrentCard.charge) < earth)
                return false;

            var destroyList = new List<BattleCard>();
            var earthList = new List<BattleCard>();

            foreach (var card in cards)
            {
                if (card.CurrentCard.charge < earth)
                {
                    destroyList.Add(card);
                    earth -= card.CurrentCard.charge;
                }
                else
                    earthList.Add(card);
                    break;
            }

            if (destroyList.Count > 0)
            {
                Effect destroyEffect = new Effect("none", "none", null, null, EffectAbility.Destroy, new Dictionary<string, string>()
            {
                {"situation", "system"},
            })
                {
                    source = effect.source,
                    sourceEffect = effect,
                    invokeUnit = effect.invokeUnit,
                    invokeTarget = destroyList,
                };

                destroyEffect.Apply(state);
                state.currentEffect = effect;
            }

            if (earth != 0)
            {
                Effect chargeEffect = new Effect(new int[] { (int)EffectAbility.SetCharge, -earth })
                {
                    source = effect.source,
                    sourceEffect = effect,
                    invokeUnit = unit,
                    invokeTarget = earthList,
                };
                chargeEffect.Apply(state);
                state.currentEffect = effect;
            }

            /*if (unit.field.cards.Count(filter.FilterWithCurrentCard) < earth)
                return false;*/

            /*Effect earthEffect = new Effect("none", "me_field_" + earth.ToString() + "_first_[trait:3][type:3]",
                null, null, EffectAbility.Destroy, null)
            {
                source = effect.source,
                sourceEffect = effect,
                invokeUnit = effect.invokeUnit,
            };*/

            /*EnqueueEffect("on_this_earth", new List<BattleCard>() { effect.source }, state);
            OnPhaseChange("on_earth", state);*/
        }

        // Necromance
        if (effect.abilityOptionDict.TryGetValue("necromance", out trimId)) {
            var necromance = Parser.UpdateEffectExpression("necromance", effect, state);
            if (unit.grave.GraveCount < necromance)
                return false;

            Effect necromanceEffect = new Effect(new int[] { (int)EffectAbility.SetGrave, -necromance })
            {
                source = effect.source,
                sourceEffect = effect,
                invokeUnit = unit,
            };
            necromanceEffect.Apply(state);
            state.currentEffect = effect;

            //unit.grave.GraveCount -= necromance;

            /*EnqueueEffect("on_this_necromance", new List<BattleCard>() { effect.source }, state);
            OnPhaseChange("on_necromance", state);*/
        }

        // Necromance
        if (effect.abilityOptionDict.TryGetValue("intromezzo", out trimId))
        {
            var intromezzo = Parser.UpdateEffectExpression("intromezzo", effect, state);
            unit.isIntro = !unit.isIntro;
            if (unit.GetIdentifier("isIntro") == intromezzo)
                return false;

            /*EnqueueEffect("on_this_intromezzo", new List<BattleCard>() { effect.source }, state);
            OnPhaseChange("on_intromezzo", state);*/
        }

        return true;
    }

    public static bool Postprocess(this Effect effect, BattleState state) {
        //effect.TurnUseTimes += 1;
        //effect.UseTimes += 1;
        if (bool.Parse(effect.abilityOptionDict.Get("autoRemove", "false")))
            effect.source.RemoveEffect("none", effect.id);

        if (effect.abilityOptionDict.TryGetValue("appendix", out var trimId)) {
            var appendixIds = trimId.ToIntList('/');
            foreach (var appendixId in appendixIds)
            {
                var appendix = Effect.Get(appendixId);

                if (appendix == null)
                    continue;

                state.currentEffect = effect;

                appendix.source = effect.source;
                appendix.sourceEffect = effect;
                appendix.invokeUnit = effect.invokeUnit;

                if (!appendix.Condition(state))
                    continue;

                appendix.Apply(state);
            }
        }

        state.currentEffect = effect;
        //state.RemoveUntilEffect();

        return true;
    }


    public static BattleCardGraveReason HandleLeave(BattleCard battleCard, BattleCardGraveReason reason, BattleState state)
    {
        var initReason = reason;
        var effectList = ListEffect(EffectAbility.SetLeave, new List<BattleCard> { battleCard }, state);
        //var effectList = EnqueueEffect("on_this_before_leave", tempList, state, false).Concat(OnPhaseChange("on_before_leave", state, false));

        foreach (var effect in effectList)
        {
            var reasonStr = effect.abilityOptionDict.Get("reason", null);
            if (reasonStr != null && int.TryParse(reasonStr, out var setReason) && effect.Condition(state))
            {
                reason = (BattleCardGraveReason)setReason;
                effect.sourceEffect.abilityOptionDict["reason"] = reason.ToString();
            }
        }

        state.currentEffect.abilityOptionDict["reason"] = initReason.ToString();

        return reason;
    }

    public static int HandleDamage(BattleCard battleCard, int damage, BattleState state)
    {
        var initDamage = damage;
        var sourceEffect = state.currentEffect;
        var effectList = ListEffect(EffectAbility.SetDamage, new List<BattleCard> { battleCard }, state);
        //var effectList = EnqueueEffect("on_this_before_damage", new List<BattleCard> { battleCard }, state, false, false).Concat(OnPhaseChange("on_before_damage", state, false, false));

        /*var isCardInField = state.GetCardPlaceInfo(battleCard).place == BattlePlaceId.Field;
        var isShieldAvailable = battleCard.IsKeywordAvailable(CardKeyword.Ambush);
        if (isShieldAvailable && isCardInField)
        {
            var lostAmbushEffect = new Effect("none", "self", null, null,
                EffectAbility.SetKeyword, new Dictionary<string, string>() {
                { "mode", "remove"},
                { "keyword", "11" }
            })
            {
                source = battleCard,
                sourceEffect = sourceEffect,
                invokeUnit = sourceEffect.invokeUnit,
            };
            lostAmbushEffect.ImmediateApply(state);
            return 0;
        }*/

        //TODO 全部换成output字典
        foreach (var effect in effectList)
        {
            var add = effect.abilityOptionDict.Get("add", null);

            if (add != null && effect.Condition(state))
            {
                var addValue = Parser.ParseEffectExpression(add, effect, state);
                damage += addValue;
                effect.sourceEffect.abilityOptionDict["damage"] = damage.ToString();
            }
        }

        foreach (var effect in effectList)
        {
            var set = effect.abilityOptionDict.Get("set", null);

            if (set != null && effect.Condition(state))
            {
                var setValue = Parser.ParseEffectExpression(set, effect, state);
                damage = setValue;
                effect.sourceEffect.abilityOptionDict["damage"] = damage.ToString();
            }
        }

        state.currentEffect.abilityOptionDict["damage"] = initDamage.ToString();

        return damage;
    }

    public static int HandleHeal(BattleCard battleCard, int heal, BattleState state)
    {
        var initHeal = heal;
        var effectList = ListEffect(EffectAbility.SetHeal, new List<BattleCard> { battleCard }, state);
        //var effectList = EnqueueEffect("on_this_before_damage", new List<BattleCard> { battleCard }, state, false, false).Concat(OnPhaseChange("on_before_damage", state, false, false));

        foreach (var effect in effectList)
        {
            var add = effect.abilityOptionDict.Get("add", null);

            if (add != null && effect.Condition(state))
            {
                var addValue = Parser.ParseEffectExpression(add, effect, state);
                heal += addValue;
                effect.sourceEffect.abilityOptionDict["heal"] = heal.ToString();
            }
        }

        foreach (var effect in effectList)
        {
            var set = effect.abilityOptionDict.Get("set", null);

            if (set != null && effect.Condition(state))
            {
                var setValue = Parser.ParseEffectExpression(set, effect, state);
                heal = setValue;
                effect.sourceEffect.abilityOptionDict["heal"] = heal.ToString();
            }
        }

        state.currentEffect.abilityOptionDict["heal"] = initHeal.ToString();

        return heal;
    }

    public static bool SetResult(this Effect effect, BattleState state) {
        if (state == null)
            return false;

        // Get Master's Result State.
        string who = effect.abilityOptionDict.Get("who", "me");
        string result = effect.abilityOptionDict.Get("result", "none");
        var unit = (who == "me") ? effect.invokeUnit : state.GetRhsUnitById(effect.invokeUnit.id);
        var resultState = result switch {
            "win" => unit.IsMasterUnit ? BattleResultState.Win : BattleResultState.Lose,
            "lose" => unit.IsMasterUnit ? BattleResultState.Lose : BattleResultState.Win,
            _ => BattleResultState.None,
        };  
        state.result.masterState = resultState;
        Hud.SetState(state);
        return true;
    }

    public static bool KeepCard(this Effect effect, BattleState state) {
        string indexList = effect.abilityOptionDict.Get("change", "none");
        var changeList = indexList.ToIntList('/');
        
        if ((state == null) || (changeList == null))
            return false;

        // Check if change index OK.
        var unit = effect.invokeUnit;
        bool isMyUnit = state.myUnit.id == unit.id;
        for (int i = 0; i < changeList.Count; i++) {
            if (changeList[i] >= unit.hand.Count)
                return false;
        }
        
        // Replace hand with new cards.
        var changeCards = changeList.Select(x => unit.hand.cards[x]).ToList();
        var newCards = unit.deck.cards.Take(changeList.Count).ToList();
        for (int i = 0; i < changeList.Count; i++) {
            unit.hand.cards[changeList[i]] = newCards[i];
        }

        // Remove new cards from deck and Shuffle.
        if (changeCards.Count > 0) {
            unit.deck.cards.RemoveRange(0, Mathf.Min(newCards.Count, unit.deck.Count));
            unit.deck.cards.AddRange(changeCards);
            unit.deck.cards.Shuffle();
        }
        effect.invokeTarget = newCards;
        unit.isDone = true;

        // Set UI.
        string log = (isMyUnit ? "我方" : "對方") + "交換了 " + newCards.Count + " 張手牌";
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        // Handle next action.
        if (state.settings.isLocal && (state.myUnit.id == unit.id))
            Battle.PlayerAction(new int[] { (int)EffectAbility.KeepCard }, false);
        
        if (state.myUnit.isDone && state.opUnit.isDone) {
            Effect turnStart = new Effect(new int[] { (int)EffectAbility.TurnStart })
            {
                source = state.currentUnit.leader.leaderCard,
                invokeUnit = state.currentUnit
            };
            Battle.EnqueueEffect(turnStart);
        }
        return true;
    }

    public static bool OnTurnStart(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        bool isMyUnit = state.myUnit.id == unit.id;

        state.myUnit.isDone = state.opUnit.isDone = false;

        // Add turn and Recover pp (max 10)
        unit.turn += 1;
        unit.leader.PPMax += 1;
        unit.leader.PP = unit.leader.PPMax;

        // Clear data.
        unit.targetQueue.Clear();
        unit.leader.ClearTurnIdentifier();
        
        // If specific turn comes, give player EP.
        var first = unit.isFirst ? 1 : 0;
        if (unit.turn - first == state.settings.evolveStart) {
            unit.leader.EPMax = unit.isFirst ? 2 : 3;
            unit.leader.EP = unit.leader.EPMax;
            effect.hudOptionDict.Set("ep", "true");
        }

        // Set UI
        string log = (isMyUnit ? "YOUR" : "ENEMY") + " TURN (" + unit.turn + ")";
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        // On Turn Start In Field.
        unit.hand.cards.ForEach(x => x.OnTurnStart(BattlePlaceId.Hand));
        unit.leader.cards.ForEach(x => x.OnTurnStart(BattlePlaceId.Leader));
        unit.field.cards.ForEach(x => x.OnTurnStart(BattlePlaceId.Field));
        unit.deck.cards.ForEach(x => x.OnTurnStart(BattlePlaceId.Deck));
        Effect countdownEffect = new Effect("none", "none", null, null, 
            EffectAbility.SetCountdown, new Dictionary<string, string>()
            {
                {"situation", "system"},
                {"add", "-1"}
            })
        {
            source = unit.leader.leaderCard,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = unit.field.cards.Where(x => (x.CurrentCard.Type == CardType.Amulet) &&
                (x.CurrentCard.countdown > 0)).ToList(),
        };
        countdownEffect.Apply(state);

        // Set current effect to this after apply countdown.
        state.currentEffect = effect;

        // If turn over 40, lose.
        if (unit.turn > 40) {
            Effect lose = new Effect(new int[] { (int)EffectAbility.SetResult, (int)BattleResultState.Lose, (int)BattleLoseReason.TurnOverMax })
            {
                source = unit.leader.leaderCard,
                invokeUnit = unit
            };
            lose.Apply(state);
            return true;
        }

        // On turn start.
        OnPhaseChange("on_turn_start", state);

        // Draw cards.
        int drawCount = ((!unit.isFirst) && (unit.turn == 1)) ? 2 : 1;
        Effect draw = new Effect(new int[] { (int)EffectAbility.Draw, drawCount })
        {
            source = unit.leader.leaderCard,
            sourceEffect = effect,
            invokeUnit = unit
        };
        Battle.EnqueueEffect(draw);
        return true;
    }

    public static bool OnTurnEnd(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        bool isMyUnit = state.myUnit.id == unit.id;

        unit.isDone = true;

        // Set UI
        string log = "回合結束";
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        // On turn end.
        OnPhaseChange("on_turn_end", state);
        
        // Check if next turn is mine (Add turn effect)
        int addTurn = (int)unit.leader.GetIdentifier("addTurn");
        if (addTurn > 0) {
            unit.leader.SetIdentifier("addTurn", addTurn - 1);
        } else {
            state.IsMasterTurn = !state.IsMasterTurn;
        }

        // Change turn.
        Effect turnStart = new Effect(new int[] { (int)EffectAbility.TurnStart })
        {
            source = state.currentUnit.leader.leaderCard,
            invokeUnit = state.currentUnit
        };
        Battle.EnqueueEffect(turnStart);
        return true;
    }

    public static bool Play(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        var isMyUnit = unit.id == state.myUnit.id;

        int index = int.Parse(effect.abilityOptionDict.Get("index", "0"));
        if (!index.IsInRange(0, unit.hand.Count))
            return false;

        var useCard = unit.hand.cards[index];
        if (isMyUnit && (!useCard.IsUsable(unit)))
            return false;

        // Record selected target.
        var targetList = effect.abilityOptionDict.Get("target", string.Empty).ToIntList('/').Select(x => BattleCardPlaceInfo.Parse(x).GetBattleCard(state));
        state.currentUnit.targetQueue = new Queue<BattleCard>(targetList);

        // Use cost
        var cost = useCard.GetUseCost(unit.leader, out var situation);
        effect.abilityOptionDict.Set("useCost", cost.ToString());
        effect.abilityOptionDict.Set("useSituation", situation);
        effect.outputOptionDict.Set("useCost", cost.ToString());
        effect.outputOptionDict.Set("useSituation", situation);

        useCard = useCard.GetCurrentBattleCard(cost, situation);

        Effect setPP = new Effect(new int[] { (int)EffectAbility.SetPP, -cost })
        {
            source = useCard,
            sourceEffect = effect,
            invokeUnit = unit,
        };
        setPP.Apply(state);
        state.currentEffect = effect;

        unit.hand.cards.Remove(unit.hand.cards[index]);
        unit.grave.usedCards.Add(useCard.baseCard);

        effect.source = useCard;
        effect.invokeTarget = new List<BattleCard>() { useCard };

        // Follower and Amulet goes to field.
        if (useCard.CurrentCard.IsFollower() || (useCard.CurrentCard.Type == CardType.Amulet)) {
            var abilityOptionDict = new Dictionary<string, string>() { { "where", "hand" },{ "situation", "system" } };
            Effect summon = new Effect("none", "none", null, null, 
                EffectAbility.Summon, abilityOptionDict) 
            {
                source = useCard,
                sourceEffect = effect,
                invokeUnit = unit,
                invokeTarget = new List<BattleCard>() { useCard },
            };
            Battle.EnqueueEffect(summon);
        }

        // Use effect.
        EnqueueEffect("on_this_play", effect.invokeTarget, state);

        // Spell goes to grave and boost.
        /*if (useCard.CurrentCard.Type == CardType.Spell) {
            unit.grave.GraveCount += 1;
            unit.grave.cards.Add(useCard);

            Effect boost = new Effect("none", "me_hand_0_all+other_[keyword:108]", null, null, 
                EffectAbility.SpellBoost, new Dictionary<string, string>(){ 
                    { "hide", "true"}, 
                    { "add",  "1"   }, 
                })
            {
                source = useCard,
                sourceEffect = effect,
                invokeUnit = unit,
                invokeTarget = unit.hand.cards,
            };
            boost.Apply(state);
            state.currentEffect = effect;
        }*/

        // Add combo.
        //unit.leader.AddIdentifier("combo", 1);

        var card = useCard.CurrentCard;

        effect.hudOptionDict.Set("log", "使用 " + card.name);
        effect.hudOptionDict.Set("status", cost + "/" + card.atk + "/" + card.hp);
        Hud.SetState(state);

        OnPhaseChange("on_play", state);

        return true;
    }

    public static bool Attack(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        var targetUnit = state.GetRhsUnitById(unit.id);

        bool isMyUnit = state.myUnit.id == unit.id;

        var source = int.Parse(effect.abilityOptionDict.Get("source", "-1"));
        var target = int.Parse(effect.abilityOptionDict.Get("target", "-1"));

        bool isSourceLeader = source == -1;
        bool isTargetLeader = target == -1;

        var sourceCard = isSourceLeader ? unit.leader.leaderCard : unit.field.cards[source];
        var targetCard = isTargetLeader ? targetUnit.leader.leaderCard : targetUnit.field.cards[target];

        effect.source = sourceCard;
        effect.invokeTarget = new List<BattleCard>() { targetCard };

        // Consume attack chance.
        sourceCard.CurrentAttackTimes += 1;

        effect.hudOptionDict.Set("log", sourceCard.CurrentCard.name + " 攻擊 " + targetCard.CurrentCard.name);
        effect.hudOptionDict.Set("source", source.ToString());
        effect.hudOptionDict.Set("target", target.ToString());
        Hud.SetState(state);

        // OnAttack and OnFight
        //TODO 增加immediate设置
        var onThisAttack = EnqueueEffect("on_this_attack", new List<BattleCard>() { sourceCard }, state, false);
        var onThisFight = isTargetLeader ? new List<Effect>() : EnqueueEffect("on_this_fight", new List<BattleCard>() { sourceCard, targetCard }, state, false);
        var onAttack = OnPhaseChange("on_attack", state, false);
        var onFight = isTargetLeader ? new List<Effect>() : OnPhaseChange("on_fight", state, false);

        var effectList = onThisAttack.Concat(onThisFight).Concat(onAttack).Concat(onFight).ToList();
        effectList.ForEach(x => x.Apply(state));

        // Check if they are still in field after above effects.
        var isSourceInField = isSourceLeader || unit.field.cards.Contains(sourceCard);
        var isTargetInField = isTargetLeader || targetUnit.field.cards.Contains(targetCard); 

        if ((!isSourceInField) || (!isTargetInField))
            return false;

        // Give damage.
        Effect attackEffect = new Effect("none", "none", null, null,
            EffectAbility.Damage, new Dictionary<string, string>() { 
                { "situation", "attack" },
                { "damage", sourceCard.CurrentCard.atk.ToString() },
            }) 
        {
            source = sourceCard,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = new List<BattleCard>(){ targetCard },
        };
        
        Effect defenseEffect = new Effect("none", "none", null, null,
            EffectAbility.Damage, new Dictionary<string, string>() { 
                { "situation", "defense" },
                { "damage", targetCard.CurrentCard.atk.ToString() },
            }) 
        {
            source = targetCard,
            sourceEffect = effect,
            invokeUnit = targetUnit,
            invokeTarget = new List<BattleCard>(){ sourceCard },
        };

        Effect destroyTargetEffect = new Effect("none", "none", null, null, EffectAbility.Destroy, null)
        {
            source = sourceCard,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = new List<BattleCard>() { targetCard },
        };

        Effect destroySourceEffect = new Effect("none", "none", null, null, EffectAbility.Destroy, null)
        {
            source = targetCard,
            sourceEffect = effect,
            invokeUnit = targetUnit,
            invokeTarget = new List<BattleCard>() { sourceCard },
        };

        attackEffect.Apply(state);

        if (!isTargetLeader)
            defenseEffect.Apply(state);

        if ((!isTargetLeader) && (sourceCard.IsKeywordAvailable(CardKeyword.Bane)))
            destroyTargetEffect.Apply(state);

        if ((!isSourceLeader) && (targetCard.IsKeywordAvailable(CardKeyword.Bane)))
            destroySourceEffect.Apply(state);

        return true;
    }

    public static bool Evolve(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        bool isMyUnit = state.myUnit.id == unit.id;

        // Index and card is used when players use EP to evolve follower
        int index = int.Parse(effect.abilityOptionDict.Get("index", "-1"));
        var card = index.IsInRange(0, unit.field.Count) ? unit.field.cards[index] : null;

        if (card != null) {
            var targetInfoList = effect.abilityOptionDict.Get("target", string.Empty).ToIntList('/');
            var targetList = targetInfoList.Select(x => BattleCardPlaceInfo.Parse(x).GetBattleCard(state));
            state.currentUnit.targetQueue = new Queue<BattleCard>(targetList);

            effect.source = card;
            effect.invokeTarget = new List<BattleCard>() { card };

            if (card.IsEvolvable(unit)) {
                if (!card.IsFreeEvolve())
                {
                    Effect setEP = new Effect(new int[] { (int)EffectAbility.SetEP, -1 })
                    {
                        source = card,
                        sourceEffect = effect,
                        invokeUnit = unit,
                    };
                    setEP.ImmediateApply(state);
                }
                unit.leader.isEpUsed = true;
            }
        }

        effect.invokeTarget.ForEach(x => x.Evolve());

        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => x.CurrentCard.name + " 進化").ConcatToString());
        effect.hudOptionDict.Set("index", index.ToString());
        Hud.SetState(state);

        if (card != null) {
            EnqueueEffect("on_this_evolve_with_ep", effect.invokeTarget, state);
            OnPhaseChange("on_evolve_with_ep", state);
        }

        EnqueueEffect("on_this_evolve", effect.invokeTarget, state);
        OnPhaseChange("on_evolve", state);

        return true;
    }

    public static bool Fusion(this Effect effect, BattleState state)
    {
        var unit = effect.invokeUnit;
        bool isMyUnit = state.myUnit.id == unit.id;

        // Index and card is used when players use EP to evolve follower
        int index = int.Parse(effect.abilityOptionDict.Get("index", "-1"));
        var card = index.IsInRange(0, unit.hand.Count) ? unit.hand.cards[index] : null;

        if (card != null)
        {
            var targetInfoList = effect.abilityOptionDict.Get("target", string.Empty).ToIntList('/');
            var targetList = targetInfoList.Select(x => BattleCardPlaceInfo.Parse(x).GetBattleCard(state));
            state.currentUnit.targetQueue = new Queue<BattleCard>(targetList);

            effect.source = card;
            effect.invokeTarget = new List<BattleCard>() { card };

            /*if (card.IsFusionable(unit))
            {
                
            }*/
            card.FusionTimes += 1;
        }

        effect.invokeTarget.ForEach(x => x.Leave(BattleCardGraveReason.Fusion));

        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => x.CurrentCard.name + " 被融合").ConcatToString());
        effect.hudOptionDict.Set("index", index.ToString());
        Hud.SetState(state);

        EnqueueEffect("on_this_fusion", effect.invokeTarget, state);
        OnPhaseChange("on_fusion", state);

        return true;
    }

    public static bool ChangeFlag(this Effect effect, BattleState state)
    {
        var name = effect.abilityOptionDict.Get("name", "none");

        OnPhaseChange("on_" + name + "_change", state);
        return true;
    }

    public static bool RandomEffect(this Effect effect, BattleState state) {
        //var pdf = effect.abilityOptionDict.Get("pdf", "none").ToIntList('/');
        var pdf = Parser.UpdateEffectExpressions("pdf", effect, state) ?? new List<int>();
        var randomEffects = effect.abilityOptionDict.Get("effect", "none").ToIntList('/').Select(Effect.Get).ToList();
        var count = Parser.UpdateEffectExpression("count", effect, state, 1);

        if (List.IsNullOrEmpty(pdf) || randomEffects.Contains(null) || (pdf.Count != randomEffects.Count))
            return false;

        var result = new List<Effect>();

        for (int i = 0; i < count; i++) {
            var sum = pdf.Sum();
            var rng = Random.Range(0f, sum);

            for (int j = 0; j < pdf.Count; j++) {
                if (rng < pdf[j]) {
                    randomEffects[j].source = effect.source;
                    randomEffects[j].sourceEffect = effect;
                    randomEffects[j].invokeUnit = effect.invokeUnit;
                    result.Add(randomEffects[j]);

                    pdf.RemoveAt(j);
                    randomEffects.RemoveAt(j);
                    break;
                }
                rng -= pdf[j];
            }
        }  

        result.ForEach(x => x.Apply(state));
        return true;
    }


    public static bool SetKeyword(this Effect effect, BattleState state) {
        var mode = effect.abilityOptionDict.Get("mode", "add");
        var modifyOption = (mode == "add") ? ModifyOption.Add : ModifyOption.Remove;
        var until = effect.abilityOptionDict.Get("until", "none");
        var keywordIds = effect.abilityOptionDict.Get("keyword", "none").ToIntList('/');
        var keywords = keywordIds.Select(x => (CardKeyword)x).ToList();
        var keywordNames = keywords.Select( x => x.GetKeywordName()).ToList();
        var keywordEnglishNames = keywords.Select(x => x.GetKeywordEnglishName()).ToList();

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            effect.invokeTarget[i].SetKeyword(keywords, until, modifyOption);
        }

        var modifyLog = (modifyOption == ModifyOption.Add) ? "獲得" : "失去";
        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => x.CurrentCard.name + " " + modifyLog + " " + string.Join("/", keywordNames) + " 效果").ConcatToString());
        Hud.SetState(state);

        foreach (var keywordEnglishName in keywordEnglishNames)
        {
            EnqueueEffect("on_this_" + keywordEnglishName + "_" + mode, effect.invokeTarget, state);
            OnPhaseChange("on_" + keywordEnglishName + "_" + mode, state);
        }
        return true;
    }

    public static bool Draw(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;

        var who = effect.abilityOptionDict.Get("who", "me");
        var drawUnit = (who == "me") ? unit : state.GetRhsUnitById(unit.id);
        //var count = effect.abilityOptionDict.Get("count", "1");
        var drawCount = Parser.UpdateEffectExpression("count", effect, state, 1);
        var filterOptions = effect.abilityOptionDict.Get("filter", "none");
        var inGraveCards = new List<BattleCard>();

        bool isMyUnit = who == "me";
        bool isMyCard = drawUnit.id == state.myUnit.id;

        effect.hudOptionDict.Set("who", isMyUnit ? "me" : "op");

        if (filterOptions == "none") {
            var total = drawUnit.Draw(drawCount, out effect.invokeTarget, out inGraveCards);

            drawUnit.grave.GraveCount += inGraveCards.Count;

            string log = (isMyUnit ? string.Empty : "使對手") + "抽取 " + drawCount + " 張卡片\n";       
            if (inGraveCards.Count > 0) {
                if (isMyCard)
                    inGraveCards.ForEach(x => log += x.CurrentCard.name + " 爆牌進入墓地\n");
                else
                    log += inGraveCards.Count + " 張卡片爆牌進入墓地\n";
            }

            effect.hudOptionDict.Set("log", log);
            effect.hudOptionDict.Set("count", drawCount.ToString());
            Hud.SetState(state);

            if (total.Count < drawCount) {
                int result = (int)drawUnit.leader.GetIdentifier("deckOutResult", 0);            
                var resultState = (result == 0) ? BattleResultState.Lose : BattleResultState.Win;
                Effect resultEffect = new Effect(new int[] { (int)EffectAbility.SetResult, (int)resultState, (int)BattleLoseReason.Deckout })
                {
                    source = drawUnit.leader.leaderCard,
                    sourceEffect = effect,
                    invokeUnit = drawUnit
                };
                resultEffect.Apply(state);
                return true;
            }
        } else {
            var filter = BattleCardFilter.Parse(filterOptions);
            var total = drawUnit.Draw(filter, out effect.invokeTarget, out inGraveCards);

            drawUnit.grave.GraveCount += inGraveCards.Count;

            string log = (isMyUnit ? string.Empty : "使對手") + "檢索 " + total.Count + " 張卡片\n";       

            if (inGraveCards.Count > 0) {
                if (isMyCard)
                    inGraveCards.ForEach(x => log += x.CurrentCard.name + " 爆牌進入墓地\n");
                else
                    log += inGraveCards.Count + " 張卡片爆牌進入墓地\n";
            }

            effect.hudOptionDict.Set("log", log);
            effect.hudOptionDict.Set("count", drawCount.ToString());
            Hud.SetState(state);
        }
        EnqueueEffect("on_this_draw", effect.invokeTarget, state);
        EnqueueEffect("on_this_draw_discard", inGraveCards, state);
        OnPhaseChange("on_draw", state);
        return true;
    }

    public static bool Summon(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        var rhsUnit = state.GetRhsUnitById(unit.id);

        var who = effect.abilityOptionDict.Get("who", "me");
        var summonUnit = (who == "me") ? unit : rhsUnit;
        var field = effect.abilityOptionDict.Get("field", "me");
        var fieldUnit = (field == "me") ? unit : rhsUnit;
        //TODO 考虑换成数字，与标识符统一
        var where = effect.abilityOptionDict.Get("where", "token");
        if (!Enum.TryParse(typeof(BattlePlaceId), where, true, out var wherePlace))
            return false;
        //var id = effect.abilityOptionDict.Get("id", "none").ToIntList('/');
        var id = Parser.UpdateEffectExpressions("count", effect, state) ?? new List<int>();
        //var count = effect.abilityOptionDict.Get("count", "1").Split('/').Select(x => Parser.ParseEffectExpression(x, effect, state)).ToList();
        var count = Parser.UpdateEffectExpressions("count", effect, state) ?? new List<int> {1};
        var availableCount = fieldUnit.field.AvailableCount;

        if (availableCount <= 0)
            return false;

        switch (wherePlace)
        {
            default:
                break;

            case BattlePlaceId.Token:
                effect.invokeTarget = new List<BattleCard>();
                id.Select((x, i) => Enumerable.Repeat(x, count[i]).Select(BattleCard.Get)).ToList()
                    .ForEach(effect.invokeTarget.AddRange);
                break;

            case BattlePlaceId.Grave:
                var grave = summonUnit.grave;
                var graveTargetInfo = effect.GetEffectTargetInfo(state);
                var gravePoolId = effect.abilityOptionDict.Get("pool", "all");
                var gravePool = gravePoolId switch
                {
                    "distinct_destroy_card" => grave.DistinctDestroyedCards,
                    "distinct_destroy_follower" => grave.DistinctDestroyedFollowers,
                    "distinct_destroy_amulet" => grave.DistinctDestroyedAmulets,
                    _ => grave.DestroyedCards,
                };
                effect.invokeTarget = graveTargetInfo.filter.FilterCurrentCardList(gravePool.Select(BattleCard.Get).ToList()).Random(graveTargetInfo.num, false);
                break;
        }

        var target = effect.invokeTarget.Take(availableCount).ToList();
        if (target.Count == 0)
            return false;

        var whereNumber = (float)(int)wherePlace;
        target.ForEach(x => x.SetIdentifier("fieldReason", (float)(int)wherePlace));

        for (int i = 0; i < Mathf.Min(availableCount, target.Count); i++) {
            effect.invokeTarget = new List<BattleCard>() { target[i] };

            switch (wherePlace) {
                default:
                    break;
                case BattlePlaceId.Hand:
                    target.ForEach(x => x.buffController.ClearCostBuff());
                    summonUnit.hand.cards.RemoveRange(effect.invokeTarget);
                    break;
                case BattlePlaceId.Deck:
                    target.ForEach(x => x.buffController.ClearCostBuff());
                    summonUnit.deck.cards.RemoveRange(effect.invokeTarget);
                    break;
            }

            fieldUnit.field.cards.AddRange(effect.invokeTarget);

            /*if (target[i].CurrentCard.IsFollower())
                fieldUnit.leader.AddIdentifier("rally", 1);*/
            //TODO 蓄积

            EnqueueEffect("on_this_summon", effect.invokeTarget, state);
            OnPhaseChange("on_summon", state);
        }


        effect.invokeTarget = target;
        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => x.CurrentCard.name + " 進入戰場").ConcatToString());
        Hud.SetState(state);

        return true;
    }

    public static bool Damage(this Effect effect, BattleState state) {
        var situation = effect.abilityOptionDict.Get("situation", "none");
        var type = effect.abilityOptionDict.Get("type", "none");
        var damage = Parser.UpdateEffectExpression("damage", effect, state);

        // Remove ambush.        
        var isSourceInField = state.GetCardPlaceInfo(effect.source).place == BattlePlaceId.Field;
        var isAmbushAvailable = effect.source.IsKeywordAvailable(CardKeyword.Ambush);
        if (isSourceInField && isAmbushAvailable) {
            var lostAmbushEffect = new Effect("none", "self", null, null,
                EffectAbility.SetKeyword, new Dictionary<string, string>() {
                { "mode", "remove"},
                { "keyword", "5"    }
            })
            {
                source = effect.source,
                sourceEffect = effect, 
                invokeUnit = effect.invokeUnit,   
            };
            lostAmbushEffect.Apply(state);
        }

        // Set current effect to this after apply lostAmbush
        state.currentEffect = effect;

        // Take damage and Set UI.
        List<int> damageAllList = new List<int>();
        List<int> myIndexList = new List<int>();
        List<int> myDamageList = new List<int>();
        List<int> opIndexList = new List<int>();
        List<int> opDamageList = new List<int>();

        switch (type)
        {
            default:
                damageAllList = effect.invokeTarget.Select(x => x.TakeDamage(HandleDamage(x, damage, state))).ToList();
                break;
            case "takeTurn":
                for (int i = 0; i < effect.invokeTarget.Count; i++)
                {
                    var target = effect.invokeTarget[i];
                    var targetDamage = (i == effect.invokeTarget.Count - 1) ? damage : Mathf.Min(damage, target.CurrentCard.hp);
                    var finalDamage = HandleDamage(effect.invokeTarget[i], targetDamage, state);

                    effect.invokeTarget[i].TakeDamage(finalDamage);
                    damageAllList.Add(finalDamage);
                    damage -= targetDamage;

                    if (damage <= 0)
                    {
                        effect.invokeTarget = effect.invokeTarget.Take(i + 1).ToList();
                        break;
                    }
                }
                break;
        }

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            var belongUnit = state.GetBelongUnit(effect.invokeTarget[i]);
            var indexList = (belongUnit.id == state.myUnit.id) ? myIndexList : opIndexList;
            var damageList = (belongUnit.id == state.myUnit.id) ? myDamageList : opDamageList;
            var index = (effect.invokeTarget[i].CurrentCard.Type == CardType.Leader) ? -1 :
                belongUnit.field.cards.IndexOf(effect.invokeTarget[i]);

            indexList.Add(index);
            damageList.Add(damageAllList[i]);
        }

        //TODO 可以给TakeDamage注册事件 Wrath
        if (effect.invokeUnit.isMyTurn)
        {
            var indexList = (effect.invokeUnit.id == state.myUnit.id) ? myIndexList : opIndexList;
            var damageList = (effect.invokeUnit.id == state.myUnit.id) ? myDamageList : opDamageList;
            var leaderIndex = indexList.IndexOf(1);

            if (leaderIndex != -1)
                effect.invokeUnit.leader.AddIdentifier("give_me_leader_damage_times", 1);
        }

        effect.hudOptionDict.Set("log", effect.invokeTarget.Select((x, i) => effect.source.CurrentCard.name + " 給予 " + x.CurrentCard.name + " " + damageAllList[i] + " 點傷害").ConcatToString());
        effect.hudOptionDict.Set("situation", situation);
        effect.hudOptionDict.Set("myIndex", myIndexList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("myDamage", myDamageList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opIndex", opIndexList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opDamage", opDamageList.Select(x => x.ToString()).ConcatToString("/"));
        OnStateChange(state);

        // Drain.
        if ((situation == "attack") && (effect.source.IsKeywordAvailable(CardKeyword.Drain))) {
            Effect healEffect = new Effect("none", "none", null, null, 
                EffectAbility.Heal, new Dictionary<string, string>() { 
                    { "situation", "attack" },
                    { "heal", damageAllList.Sum().ToString() },
                })
            {
                source = effect.source,
                sourceEffect = effect,
                invokeUnit = effect.invokeUnit,
                invokeTarget = new List<BattleCard>() { state.GetBelongUnit(effect.source).leader.leaderCard },
            };
            healEffect.Apply(state);
        }

        state.currentEffect = effect;

        // If hp <= 0, destroy.
        var destroyCount = 0;
        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            if (effect.invokeTarget[i].CurrentCard.hp <= 0) {
                destroyCount++;
                Effect destroyEffect = new Effect("none", "none", null, null,
                    EffectAbility.Destroy, new Dictionary<string, string> { 
                        { "situation", situation }
                    })
                {
                    source = effect.invokeTarget[i],
                    sourceEffect = effect,
                    invokeUnit = effect.invokeUnit,
                    invokeTarget = new List<BattleCard>() { effect.invokeTarget[i] },
                };

                // Attack or defense need to wait op, so only enqueue.
                if ((situation == "attack") || (situation == "defense"))
                    Battle.EnqueueEffect(destroyEffect);
                else
                    destroyEffect.Apply(state);
            }
        }
        effect.outputOptionDict.Set("destroyCount", destroyCount.ToString());

        state.currentEffect = effect;

        EnqueueEffect("on_this_damage", effect.invokeTarget, state);
        OnPhaseChange("on_damage", state);

        return true;
    }

    public static bool Heal(this Effect effect, BattleState state) {
        var situation = effect.abilityOptionDict.Get("situation", "none");
        var heal = Parser.UpdateEffectExpression("heal", effect, state);

        // Take damage and Set UI.
        List<int> healAllList = effect.invokeTarget.Select(x => x.TakeHeal(HandleHeal(x, heal, state))).ToList();
        List<int> myIndexList = new List<int>();
        List<int> myHealList = new List<int>();
        List<int> opIndexList = new List<int>();
        List<int> opHealList = new List<int>();

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            var belongUnit = state.GetBelongUnit(effect.invokeTarget[i]);
            var indexList = (belongUnit.id == state.myUnit.id) ? myIndexList : opIndexList;
            var healList = (belongUnit.id == state.myUnit.id) ? myHealList : opHealList;
            var index = (effect.invokeTarget[i].CurrentCard.Type == CardType.Leader) ? -1 :
                belongUnit.field.cards.IndexOf(effect.invokeTarget[i]);

            indexList.Add(index);
            healList.Add(healAllList[i]);
        }

        effect.hudOptionDict.Set("log", effect.invokeTarget.Select((x, i) => effect.source.CurrentCard.name + " 回復 " + x.CurrentCard.name + " " + healAllList[i] + " 點生命值").ConcatToString());
        effect.hudOptionDict.Set("situation", situation);
        effect.hudOptionDict.Set("myIndex", myIndexList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("myHeal", myHealList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opIndex", opIndexList.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opHeal", opHealList.Select(x => x.ToString()).ConcatToString("/"));
        OnStateChange(state);

        EnqueueEffect("on_this_heal", effect.invokeTarget, state);
        OnPhaseChange("on_heal", state);

        return true;
    }

    public static bool Destroy(this Effect effect, BattleState state) {
        string log = string.Empty;
        var situation = effect.abilityOptionDict.Get("situation", string.Empty);
        var allTargetInfos = effect.invokeTarget.Select(state.GetCardPlaceInfo).ToList();
        var target = effect.invokeTarget.Where((x, i) => (allTargetInfos[i].place == BattlePlaceId.Leader) || (allTargetInfos[i].place == BattlePlaceId.Field))
                    .Where(x => (!string.IsNullOrEmpty(situation)) || (!x.IsKeywordAvailable(CardKeyword.DestroyResist))).ToList();
        var targetInfos = allTargetInfos.Where(x => (x.place == BattlePlaceId.Leader) || (x.place == BattlePlaceId.Field)).ToList();
        
        var destroyedTarget = new List<BattleCard>();
        var vanishedTarget = new List<BattleCard>();

        var myIndex = new List<int>();
        var opIndex = new List<int>();

        var myValue = new List<string>();
        var opValue = new List<string>();

        //target.ForEach(x => state.GetBelongUnit(x).field.cards.Remove(x));

        for (int i = 0; i < target.Count; i++) {
            var belongUnit = (targetInfos[i].unitId == 0) ? state.myUnit : state.opUnit;
            var card = target[i].CurrentCard;
            var reason = BattleCardGraveReason.Destroy;

            effect.invokeTarget = new List<BattleCard>() { target[i] };

            // Leader destroy, lose.
            if (card.Type == CardType.Leader) {
                Effect resultEffect = new Effect(new int[] { (int)EffectAbility.SetResult, (int)BattleResultState.Lose, (int)BattleLoseReason.LeaderDie })
                {
                    source = belongUnit.leader.leaderCard,
                    invokeUnit = belongUnit,
                };
                resultEffect.Apply(state);
                return true;                
            }

            // Special handle: Vanish when destroyed.
            reason = HandleLeave(target[i], reason, state);
            var indexList = (targetInfos[i].unitId == 0) ? myIndex : opIndex;
            var valueList = (targetInfos[i].unitId == 0) ? myValue : opValue;

            // Remove and grave++;
            target[i].Leave(reason, state);
            if(reason == BattleCardGraveReason.Destroy) 
                destroyedTarget.Add(target[i]);

            indexList.Add(targetInfos[i].index);
            valueList.Add(reason.ToString());

            log += card.name + reason switch
            {
                BattleCardGraveReason.Destroy => "被破壞",
                BattleCardGraveReason.Vanish => "消失",
                BattleCardGraveReason.Return => "返回手牌",
                _ => string.Empty
            } + "\n";

            EnqueueEffect("on_this_leavefield", effect.invokeTarget, state);
            OnPhaseChange("on_leavefield", state);

            EnqueueEffect("on_this_" + reason.ToString().ToLower(), effect.invokeTarget, state);
            OnPhaseChange("on_" + reason.ToString().ToLower(), state);
        }

        for (int i = 0; i < destroyedTarget.Count; i++) {
            var belongUnit = state.GetBelongUnit(destroyedTarget[i]);

            belongUnit.grave.GraveCount += 1;

            switch (destroyedTarget[i].CurrentCard.Type) {
                default:
                    break;
                case CardType.Follower:
                case CardType.Evolved:
                    belongUnit.leader.AddIdentifier("destroyedFollowerCount", 1);
                    break;
                case CardType.Amulet:
                    belongUnit.leader.AddIdentifier("destroyedAmuletCount", 1);
                    break;
            }
        }

        effect.invokeTarget = destroyedTarget;
        
        effect.hudOptionDict.Set("myIndex", myIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("myValue", myValue.ConcatToString("/"));
        effect.hudOptionDict.Set("opIndex", opIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opValue", opValue.ConcatToString("/"));
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        return true;
    }

    public static bool Vanish(this Effect effect, BattleState state) {
        string log = string.Empty;
        var target = effect.invokeTarget.Where(x => state.GetBelongUnit(x).field.Contains(x))
            .Where(x => !x.IsKeywordAvailable(CardKeyword.VanishResist)).ToList();
        var targetInfos = target.Select(x => state.GetCardPlaceInfo(x)).ToList();

        var myIndex = new List<int>();
        var opIndex = new List<int>();

        var myValue = new List<string>();
        var opValue = new List<string>();

        //target.ForEach(x => state.GetBelongUnit(x).field.cards.Remove(x));

        for (int i = 0; i < target.Count; i++) {
            var belongUnit = (targetInfos[i].unitId == 0) ? state.myUnit : state.opUnit;
            var card = target[i].CurrentCard;
            var reason = BattleCardGraveReason.Vanish;

            effect.invokeTarget = new List<BattleCard>() { target[i] };

            // Leader vanish, lose.
            if (card.Type == CardType.Leader) {
                Effect resultEffect = new Effect(new int[] { (int)EffectAbility.SetResult, (int)BattleResultState.Lose, (int)BattleLoseReason.LeaderDie })
                {
                    source = belongUnit.leader.leaderCard,
                    invokeUnit = belongUnit,
                };
                resultEffect.Apply(state);
                return true;                
            }

            // Special handle
            reason = HandleLeave(target[i], reason, state);
            var indexList = (targetInfos[i].unitId == 0) ? myIndex : opIndex;
            var valueList = (targetInfos[i].unitId == 0) ? myValue : opValue;

            // Remove and grave++;
            target[i].Leave(reason, state);

            indexList.Add(targetInfos[i].index);
            valueList.Add(reason.ToString());

            log += card.name + reason switch
            {
                BattleCardGraveReason.Destroy => "被破壞",
                BattleCardGraveReason.Vanish => "消失",
                BattleCardGraveReason.Return => "返回手牌",
                _ => string.Empty
            } + "\n";

            EnqueueEffect("on_this_leavefield", effect.invokeTarget, state);
            OnPhaseChange("on_leavefield", state);

            EnqueueEffect("on_this_" + reason.ToString(), effect.invokeTarget, state);
            OnPhaseChange("on_" + reason.ToString(), state);
        }

        effect.invokeTarget = target;

        effect.hudOptionDict.Set("myIndex", myIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("myValue", myValue.ConcatToString("/"));
        effect.hudOptionDict.Set("opIndex", opIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opValue", opValue.ConcatToString("/"));
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        return true;
    }

    public static bool Return(this Effect effect, BattleState state) {
        string log = string.Empty;
        var target = effect.invokeTarget.Where(x => state.GetBelongUnit(x).field.Contains(x))
            .OrderBy(x => state.GetCardPlaceInfo(x).unitId * 10 + state.GetCardPlaceInfo(x).index).ToList();

        var targetInfos = target.Select(x => state.GetCardPlaceInfo(x)).ToList();

        var vanishedTarget = new List<BattleCard>();

        var myReturn = new List<BattleCard>();
        var opReturn = new List<BattleCard>();

        var myIndex = new List<int>();
        var opIndex = new List<int>();

        var myValue = new List<string>();
        var opValue = new List<string>();

        //target.ForEach(x => state.GetBelongUnit(x).field.cards.Remove(x));

        for (int i = 0; i < target.Count; i++) {
            var belongUnit = (targetInfos[i].unitId == 0) ? state.myUnit : state.opUnit;
            var isMyUnit = effect.invokeUnit.id == belongUnit.id;
            var returnList = isMyUnit ? myReturn : opReturn;
            var card = target[i].CurrentCard;
            var reason = BattleCardGraveReason.Return;

            effect.invokeTarget = new List<BattleCard>() { target[i] };

            // Special handle
            reason = HandleLeave(target[i], reason, state);
            var indexList = (targetInfos[i].unitId == 0) ? myIndex : opIndex;
            var valueList = (targetInfos[i].unitId == 0) ? myValue : opValue;

            // Remove and grave++;
            target[i].Leave(reason, state);
            if(reason == BattleCardGraveReason.Return)
                returnList.Add(target[i]);

            indexList.Add(targetInfos[i].index);
            valueList.Add(reason.ToString());

            log += card.name + reason switch
            {
                BattleCardGraveReason.Destroy => "被破壞",
                BattleCardGraveReason.Vanish => "消失",
                BattleCardGraveReason.Return => "返回手牌",
                _ => string.Empty
            } + "\n";

            EnqueueEffect("on_this_leavefield", effect.invokeTarget, state);
            OnPhaseChange("on_leavefield", state);

            EnqueueEffect("on_this_" + reason.ToString(), effect.invokeTarget, state);
            OnPhaseChange("on_" + reason.ToString(), state);
        }

        effect.invokeTarget = target;

        //TODO 转移到BattleCard的leave方法中
        // Get token.
        Effect myToken = new Effect("none", "none", null, null, 
            EffectAbility.GetToken, new Dictionary<string, string>() { 
                { "id", myReturn.Select(x => x.baseCard.id.ToString()).ConcatToString("/") },
                { "count", Enumerable.Repeat("1", myReturn.Count).ConcatToString("/") },
            })
        {
            invokeUnit = effect.invokeUnit,
            source = effect.source,
            sourceEffect = effect,
            invokeTarget = new List<BattleCard>(),
        };

        Effect opToken = new Effect("none", "none", null, null, 
            EffectAbility.GetToken, new Dictionary<string, string>() { 
                { "id", opReturn.Select(x => x.baseCard.id.ToString()).ConcatToString("/") },
                { "count", Enumerable.Repeat("1", opReturn.Count).ConcatToString("/") },
            })
        {
            invokeUnit = state.GetRhsUnitById(effect.invokeUnit.id),
            source = effect.source,
            sourceEffect = effect,
            invokeTarget = new List<BattleCard>(),
        };

        if (myReturn.Count > 0)
            myToken.Apply(state);

        if (opReturn.Count > 0)
            opToken.Apply(state);

        state.currentEffect = effect;

        for (int i = 0; i < myReturn.Count; i++)
        {
            effect.invokeTarget = new List<BattleCard>() { myReturn[i] };

            EnqueueEffect("on_this_leavefield", effect.invokeTarget, state);
            OnPhaseChange("on_leavefield", state);
        }

        for (int i = 0; i < myToken.invokeTarget.Count; i++)
        {
            effect.invokeTarget = new List<BattleCard>() { myToken.invokeTarget[i] };

            EnqueueEffect("on_this_return", effect.invokeTarget, state);
            OnPhaseChange("on_return", state);
        }
        for (int i = 0; i < opReturn.Count; i++)
        {
            effect.invokeTarget = new List<BattleCard>() { opReturn[i] };

            EnqueueEffect("on_this_leavefield", effect.invokeTarget, state);
            OnPhaseChange("on_leavefield", state);
        }

        for (int i = 0; i < opToken.invokeTarget.Count; i++)
        {
            effect.invokeTarget = (i < opToken.invokeTarget.Count) ? new List<BattleCard>() { opToken.invokeTarget[i] } : new List<BattleCard>();

            EnqueueEffect("on_this_return", effect.invokeTarget, state);
            OnPhaseChange("on_return", state);
        }

        effect.invokeTarget = myToken.invokeTarget.Concat(opToken.invokeTarget).ToList();
        effect.hudOptionDict.Set("myIndex", myIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("myValue", myValue.ConcatToString("/"));
        effect.hudOptionDict.Set("opIndex", opIndex.Select(x => x.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("opValue", opValue.ConcatToString("/"));
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);
        
        return true;
    }

    public static bool Transform(this Effect effect, BattleState state)
    {
        //var id = int.Parse(effect.abilityOptionDict.Get("id", "0"));
        var id = Parser.UpdateEffectExpression("id", effect, state);
        var card = BattleCard.Get(id);

        if (card == null)
            return false;

        effect.invokeTarget.RemoveAll(x => state.GetBelongUnit(x) == null);

        for (int i = 0; i < effect.invokeTarget.Count; i++)
        {
            var target = effect.invokeTarget[i];
            var place = state.GetBelongUnit(target).GetBelongPlace(target);
            var index = place.cards.IndexOf(target);

            var newCard = BattleCard.Get(id);
            place.cards[index] = newCard;
            target = newCard;
        }

        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => "使 " + x.CurrentCard.name + " 變身為 " + card.CurrentCard.name).ConcatToString("\n"));
        Hud.SetState(state);

        OnPhaseChange("on_transform", state);

        return true;
    }

    public static bool SetStatus(this Effect effect, BattleState state) {
        var mode = effect.abilityOptionDict.Get("mode", "add");
        if (!Enum.TryParse(typeof(ModifyOption), mode, true, out var modifyOption))
            return false;
        var until = effect.abilityOptionDict.Get("until", "none");
        var hpValue = effect.abilityOptionDict.Get("hp", null);
        var hpMaxValue = effect.abilityOptionDict.Get("hpMax", null);
        var atkValue = effect.abilityOptionDict.Get("atk", null);
        var costValue = effect.abilityOptionDict.Get("cost", null);

        var destroyEffectList = new List<Effect>();
        string log = string.Empty;

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            int costBuff = 0, atkBuff = 0, hpBuff = 0, hpMaxBuff = 0;
            if ((ModifyOption)modifyOption == ModifyOption.Add)
            {
                costBuff = costValue != null ? Parser.UpdateEffectExpression("cost", effect, state) : 0;
                atkBuff = atkValue != null ? Parser.UpdateEffectExpression("atk", effect, state) : 0;
                hpBuff = hpValue != null ? Parser.UpdateEffectExpression("hp", effect, state) : 0;
                hpMaxBuff = hpMaxValue != null ? Parser.UpdateEffectExpression("hpMaxBuff", effect, state) : 0;
                if (costBuff != 0) 
                    log += effect.invokeTarget[i].CurrentCard.name + "消費 " + costBuff.ToStringWithSign();
                if (costBuff != 0)
                    log += effect.invokeTarget[i].CurrentCard.name + "生命值上限 " + costBuff.ToStringWithSign();
                if (atkBuff!=0 || hpBuff != 0)
                    log += effect.invokeTarget[i].CurrentCard.name + " 獲得 +" + atkBuff + "/+" + hpBuff + " 效果\n";
            }
            else if((ModifyOption)modifyOption == ModifyOption.Set)
            {
                costBuff = costValue != null ? (Parser.UpdateEffectExpression("cost", effect, state) - effect.invokeTarget[i].CurrentCard.cost) : 0;
                atkBuff = atkValue != null ? (Parser.UpdateEffectExpression("atk", effect, state) - effect.invokeTarget[i].CurrentCard.atk): 0;
                hpBuff = hpValue != null ? (Parser.UpdateEffectExpression("hp", effect, state) - effect.invokeTarget[i].CurrentCard.hp) : 0;
                hpMaxBuff = hpMaxValue != null ? (Parser.UpdateEffectExpression("hpMax", effect, state) - effect.invokeTarget[i].CurrentCard.hpMax) : 0;
                if (costBuff != 0) 
                    log += effect.invokeTarget[i].CurrentCard.name + "消費轉變為 " + costBuff;
                if (costBuff != 0)
                    log += effect.invokeTarget[i].CurrentCard.name + "生命值上限轉變為 " + costBuff.ToStringWithSign();
                if (atkBuff != 0 || hpBuff != 0)
                    log += effect.invokeTarget[i].CurrentCard.name + " 轉變為 +" + atkBuff + "/+" + hpBuff + "\n";
            }

            effect.invokeTarget[i].TakeBuff(new CardStatus(costBuff, atkBuff, hpBuff+hpMaxBuff), until);
            if(hpMaxBuff != 0) 
                    effect.invokeTarget[i].TakeDamage(hpMaxBuff);

            if (effect.invokeTarget[i].CurrentCard.hp <= 0)
            {
                var destroyEffect = new Effect("none", "self", null, null, EffectAbility.Destroy, new Dictionary<string, string>() { { "situation", "debuff" } })
                {
                    source = effect.invokeTarget[i],
                    sourceEffect = effect,
                    invokeUnit = effect.invokeUnit,
                };
                destroyEffectList.Add(destroyEffect);
            };

            if (atkBuff > 0 || hpBuff > 0)
            {
                EnqueueEffect("on_this_buff", effect.invokeTarget, state);
                OnPhaseChange("on_buff", state);
            }
            if (atkBuff < 0 || hpBuff < 0)
            {
                EnqueueEffect("on_this_debuff", effect.invokeTarget, state);
                OnPhaseChange("on_debuff", state);
            }
            if(costBuff != 0)
            {
                EnqueueEffect("on_this_set_cost", effect.invokeTarget, state);
                OnPhaseChange("on_set_cost", state);
            }
        }

        effect.hudOptionDict.Set("log", log);
        OnStateChange(state);

        destroyEffectList.ForEach(x => x.Apply(state));
        state.currentEffect = effect;

        return true;
    }

    public static bool Debuff(this Effect effect, BattleState state)
    {
        var until = effect.abilityOptionDict.Get("until", "none");
        /*var add = effect.abilityOptionDict.Get("add", "0/0").Split('/')
            .Select(x => Parser.ParseEffectExpression(x, effect, state)).ToList();*/

        var add = Parser.UpdateEffectExpressions("add", effect, state) ?? new List<int> {0,0};

        if (List.IsNullOrEmpty(add))
            return false;

        int atk = add[0], hp = add[1];
        var destroyEffectList = new List<Effect>();
        string log = string.Empty;

        for (int i = 0; i < effect.invokeTarget.Count; i++)
        {
            effect.invokeTarget[i].TakeBuff(new CardStatus(0, atk, hp), until);
            log += effect.invokeTarget[i].CurrentCard.name + " 獲得 " + atk + "/" + hp + " 效果\n";

            if (effect.invokeTarget[i].CurrentCard.hp <= 0)
            {
                var destroyEffect = new Effect("none", "self", null, null, EffectAbility.Destroy, null)
                {
                    source = effect.invokeTarget[i],
                    sourceEffect = effect,
                    invokeUnit = effect.invokeUnit,
                };
                destroyEffectList.Add(destroyEffect);
            };
        }

        effect.hudOptionDict.Set("log", log);
        OnStateChange(state);

        destroyEffectList.ForEach(x => x.Apply(state));
        state.currentEffect = effect;

        EnqueueEffect("on_this_debuff", effect.invokeTarget, state);
        OnPhaseChange("on_debuff", state);

        return true;
    }

    public static bool AddBuff(this Effect effect, BattleState state)
    {
        var id = int.Parse(effect.abilityOptionDict.Get("id", "0"));
        var isDuplicate = bool.Parse(effect.abilityOptionDict.Get("isDuplicate", "true"));

        var buff = Buff.Get(id);
        if (buff == null)
            return false;

        foreach (var target in effect.invokeTarget)
        {

            if (isDuplicate && target.buffController.buffs.Exists(x => x.id == id))
                continue;

            var addBuff = Buff.Get(id);
            addBuff.source = target;
            addBuff.sourceEffect = effect;
            target.buffController.buffs.Add(addBuff);
        }

        //effect.invokeTarget.ForEach(x => x.buffController.buffs.Add(addBuff));
        effect.hudOptionDict.Set("log", effect.source.CurrentCard.name + " 賦予目標效果");
        Hud.SetState(state);

        if (buff.atk > 0 || buff.hp > 0)
        {
            EnqueueEffect("on_this_buff", effect.invokeTarget, state);
            OnPhaseChange("on_buff", state);
        }

        if (buff.atk < 0 || buff.hp < 0)
        {
            EnqueueEffect("on_this_debuff", effect.invokeTarget, state);
            OnPhaseChange("on_debuff", state);
        }

        EnqueueEffect("on_this_add_effect", effect.invokeTarget, state);
        OnPhaseChange("on_add_effect", state);

        return true;
    }

    public static bool RemoveBuff(this Effect effect, BattleState state)
    {
        var buff = effect.sourceBuff;
        effect.invokeTarget.ForEach(x => x.buffController.buffs.Remove(buff));

        /*effect.hudOptionDict.Set("log", effect.source.CurrentCard.name + " 使 目標失去能力");
        Hud.SetState(state);*/

        EnqueueEffect("on_this_remove_buff", effect.invokeTarget, state);
        OnPhaseChange("on_remove_buff", state);

        return true;
    }

    public static bool GetToken(this Effect effect, BattleState state) {
        var who = effect.abilityOptionDict.Get("who", "me");

        var tokenUnit = (who == "me") ? effect.invokeUnit : state.GetRhsUnitById(effect.invokeUnit.id);
        var isMyUnit = tokenUnit.id == state.myUnit.id;

        var hide = bool.Parse(effect.abilityOptionDict.Get("hide", "false")) && (!isMyUnit);
        var tokenIds = Parser.UpdateEffectExpressions("count", effect, state) ?? new List<int>();
        //var tokenIds = effect.abilityOptionDict.Get("id", string.Empty).ToIntList('/');
        //var tokenCountExpr = effect.abilityOptionDict.Get("count", "1").Split('/');
        //var tokenCounts = tokenCountExpr.Select(x => Parser.ParseEffectExpression(x, effect, state)).ToList(); 
        var tokenCounts = Parser.UpdateEffectExpressions("count", effect, state)?? new List<int> {1};

        List<BattleCard> tokens = new List<BattleCard>();
        List<BattleCard> inHand = new List<BattleCard>();
        List<BattleCard> inGrave = new List<BattleCard>();

        var availableCount = tokenUnit.hand.AvailableCount;

        if (List.IsNullOrEmpty(tokenIds))
        {
            tokens = effect.invokeTarget.ToList();
        }
        else
        {
            for (int i = 0; i < tokenIds.Count; i++)
                tokens.AddRange(Enumerable.Repeat(tokenIds[i], tokenCounts[i]).Select(BattleCard.Get));
        }

        if (tokens.Count > availableCount) {
            inHand = tokens.GetRange(0, availableCount);
            inGrave = tokens.GetRange(availableCount, tokens.Count - availableCount);
        } else {
            inHand = tokens.GetRange(0, tokens.Count);
            inGrave = new List<BattleCard>();
        }

        effect.invokeTarget = inHand;
        inGrave.ForEach(x => x.SetIdentifier("graveReason", (float)BattleCardGraveReason.DrawTooMuch));

        tokenUnit.hand.cards.AddRange(inHand);
        tokenUnit.grave.cards.AddRange(inGrave);

        tokenUnit.grave.GraveCount += inGrave.Count;

        string logUnit = (tokenUnit.id == effect.invokeUnit.id) ? "我方" : "對方";
        string log = hide ? ("增加 " + inHand.Count + " 張卡片到" + logUnit + "手牌中") : 
            inHand.Select(x => "增加 " + x.CurrentCard.name + " 到" + logUnit + "手牌中").ConcatToString();
            
        if (inGrave.Count > 0) {
            if (isMyUnit)
                inGrave.ForEach(x => log += x.CurrentCard.name + " 爆牌進入墓地\n");
            else
                log += inGrave.Count + " 張卡片爆牌進入墓地\n";
        }

        effect.hudOptionDict.Set("log", log);
        effect.hudOptionDict.Set("who", isMyUnit ? "me" : "op");
        effect.hudOptionDict.Set("token", tokens.Select(x => x.Id.ToString()).ConcatToString("/"));
        effect.hudOptionDict.Set("hide", (hide && (!isMyUnit)).ToString());
        Hud.SetState(state);

        EnqueueEffect("on_this_get_token", effect.invokeTarget, state);
        OnPhaseChange("on_get_token", state);

        return true;
    }

    public static bool SpellBoost(this Effect effect, BattleState state) {
        var hide = bool.Parse(effect.abilityOptionDict.Get("hide", "false"));
        var add = Parser.UpdateEffectExpression("add", effect, state, 1);
        //var mult = Parser.ParseEffectExpression(effect.abilityOptionDict.Get("mult", "1"), effect, state);

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            var boost = effect.invokeTarget[i].GetIdentifier("boost", 0);
            effect.invokeTarget[i].SetIdentifier("boost", boost + add);
        }

        string targetName = (effect.invokeTarget.Count == 1) ? effect.invokeTarget[0].CurrentCard.name : string.Empty;
        //string log = (mult == 1) ? string.Empty : ("發動 " + mult + " 倍魔力增幅\n");
        string log = "發動 " + add + " 次魔力增幅";

        effect.hudOptionDict.Set("log", hide ? string.Empty : log);
        Hud.SetState(state);

        for (int i = 0; i < add; i++) {
            EnqueueEffect("on_this_boost", effect.invokeTarget, state);
            OnPhaseChange("on_boost", state);
        }
        
        return true;
    }

    public static bool SetCost(this Effect effect, BattleState state) {
        var hide = bool.Parse(effect.abilityOptionDict.Get("hide", "true"));
        var until = effect.abilityOptionDict.Get("until", "none");
        var log = string.Empty;

        if (effect.abilityOptionDict.TryGetValue("add", out var addValue)) {
            var add = Parser.UpdateEffectExpression("add", effect, state);
            effect.invokeTarget.ForEach(x => x.TakeBuff(new CardStatus(add, 0, 0), until));
            log = "消費 " + add.ToStringWithSign();
        } else if (effect.abilityOptionDict.TryGetValue("set", out var setValue)) {
            var set = Parser.UpdateEffectExpression("set", effect, state);
            effect.invokeTarget.ForEach(x => x.TakeBuff(new CardStatus(set - x.CurrentCard.cost, 0, 0), until));
            log = "消費轉變為 " + set;
        }

        effect.hudOptionDict.Set("log", hide ? string.Empty : log);
        Hud.SetState(state);

        EnqueueEffect("on_this_set_cost", effect.invokeTarget, state);
        OnPhaseChange("on_set_cost", state);

        return true;
    }

    public static bool Ramp(this Effect effect, BattleState state) {
        var who = effect.abilityOptionDict.Get("who", "me");
        var add = Parser.UpdateEffectExpression("add", effect, state);

        var rampUnit = (who == "me") ? effect.invokeUnit : state.GetRhsUnitById(effect.invokeUnit.id);

        rampUnit.leader.AddIdentifier("ppMax", add);

        effect.hudOptionDict.Set("log", ((who == "me") ? "我方" : "對方") + "PP最大值 " + add.ToStringWithSign());
        Hud.SetState(state);

        EnqueueEffect("on_this_ramp", rampUnit.leader.cards, state);
        OnPhaseChange("on_ramp", state);

        return true;
    }

    public static bool AddEffect(this Effect effect, BattleState state) {
        //TODO改为简化版本
        var id = effect.abilityOptionDict.Get("id", "none").ToIntList('/');
        var isDuplicate = bool.Parse(effect.abilityOptionDict.Get("isDuplicate", "true"));
        var until = effect.abilityOptionDict.Get("until", "none");
        var effects = id.Select(x => Effect.Get(x)).ToList();

        foreach(var target in effect.invokeTarget)
        {
            target.buffController.AddEffect(effects, until);
        }

        //effect.invokeTarget.ForEach(x => x.buffController.buffs.Add(addBuff));
        effect.hudOptionDict.Set("log", effect.source.CurrentCard.name + " 賦予目標效果");
        Hud.SetState(state);
        EnqueueEffect("on_this_add_effect", effect.invokeTarget, state);
        OnPhaseChange("on_add_effect", state);

        return true;
    }

    public static bool RemoveEffect(this Effect effect, BattleState state) {
        var id = int.Parse(effect.abilityOptionDict.Get("id", "0"));
        var timing = effect.abilityOptionDict.Get("timing", "none");
        var keywordId = int.Parse(effect.abilityOptionDict.Get("keyword", "0"));
        var keyword = (CardKeyword)keywordId;
        effect.invokeTarget.ForEach(x => x.RemoveEffect(timing, id, keyword));

        effect.hudOptionDict.Set("log", effect.source.CurrentCard.name + " 使 目標失去能力");
        Hud.SetState(state);

        EnqueueEffect("on_this_remove_effect", effect.invokeTarget, state);
        OnPhaseChange("on_remove_effect", state);

        return true;
    }

    public static bool SetGrave(this Effect effect, BattleState state) {
        var add = Parser.UpdateEffectExpression("add", effect, state);

        effect.invokeTarget = new List<BattleCard>() { effect.invokeUnit.leader.leaderCard };
        effect.invokeUnit.grave.GraveCount += add;

        effect.hudOptionDict.Set("log", "墓場" + add.ToStringWithSign());
        Hud.SetState(state);

        EnqueueEffect("on_this_set_grave", effect.invokeTarget, state);
        OnPhaseChange("on_set_grave", state);
        
        return true;
    }

    public static bool SetCountdown(this Effect effect, BattleState state) {
        var situation = effect.abilityOptionDict.Get("situation", "none");
        var add = Parser.UpdateEffectExpression("add", effect, state);

        effect.invokeTarget.ForEach(x => x.TakeCountdown(add));

        var destroyedList = effect.invokeTarget.Where(x => x.CurrentCard.countdown == 0).ToList();
        var notDestoryedList = effect.invokeTarget.Where(x => x.CurrentCard.countdown != 0).ToList();

        if (situation != "system") {
            EnqueueEffect("on_this_set_countdown", notDestoryedList, state);
            OnPhaseChange("on_set_countdown", state);
        }

        if (destroyedList.Count > 0) {
            Effect destroyEffect = new Effect("none", "none", null, null, EffectAbility.Destroy, new Dictionary<string, string>() { { "situation", "countdown" } })
            {
                source = effect.source,
                sourceEffect = effect,
                invokeUnit = effect.invokeUnit,
                invokeTarget = destroyedList,
            };
            Battle.EnqueueEffect(destroyEffect);
        }
        
        var log = effect.invokeTarget.Select(x => x.CurrentCard.name + " 倒數 " + add.ToStringWithSign()).ConcatToString();
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);
        
        return true;
    }

    public static bool AddDeck(this Effect effect, BattleState state) {
        var who = effect.abilityOptionDict.Get("who", "me");
        var hide = bool.Parse(effect.abilityOptionDict.Get("hide", "false")) && (effect.invokeUnit.id != state.myUnit.id);
        var tokenIdExpr = effect.abilityOptionDict.Get("id", string.Empty);
        var tokenIds = string.IsNullOrEmpty(tokenIdExpr) ? effect.invokeTarget.Select(x => x.baseCard.id).ToList() : tokenIdExpr.ToIntList('/');
        var tokenCountExpr = effect.abilityOptionDict.Get("count", string.Empty);
        //var tokenCounts = string.IsNullOrEmpty(tokenCountExpr) ? Enumerable.Repeat(1, effect.invokeTarget.Count).ToList() : tokenCountExpr.Split('/').Select(x => Parser.ParseEffectExpression(x, effect, state)).ToList();
        var tokenCounts = string.IsNullOrEmpty(tokenCountExpr) ? Enumerable.Repeat(1, effect.invokeTarget.Count).ToList() : Parser.UpdateEffectExpressions("count", effect, state);

        var tokenUnit = (who == "me") ? effect.invokeUnit : state.GetRhsUnitById(effect.invokeUnit.id);
        var isMyUnit = tokenUnit.id == state.myUnit.id;

        effect.invokeTarget = new List<BattleCard>();

        if (tokenIds.Select(BattleCard.Get).Contains(null))
            return false;

        string log = string.Empty;
        for (int i = 0; i < tokenIds.Count; i++) {
            effect.invokeTarget.AddRange(Enumerable.Repeat(tokenIds[i], tokenCounts[i]).Select(BattleCard.Get));
            log += "增加 " + tokenCounts[i] + " 張 " + (hide ? "卡片" : BattleCard.Get(tokenIds[i]).CurrentCard.name) + 
                " 到" + ((tokenUnit.id == effect.invokeUnit.id) ? "我方" : "對方") + "牌堆中\n";
        }

        tokenUnit.deck.cards.AddRange(effect.invokeTarget);
        tokenUnit.deck.cards.Shuffle();

        effect.hudOptionDict.Set("log", log);
        effect.hudOptionDict.Set("who", isMyUnit ? "me" : "op");
        effect.hudOptionDict.Set("hide", hide ? "true" : "false");
        effect.hudOptionDict.Set("token", effect.invokeTarget.Select(x => x.Id.ToString()).ConcatToString("/"));
        Hud.SetState(state);

        EnqueueEffect("on_this_add_deck", effect.invokeTarget, state);
        OnPhaseChange("on_add_deck", state);

        return true;
    }

    public static bool Hybrid(this Effect effect, BattleState state) {
        //var result = int.Parse(effect.abilityOptionDict.Get("id", "0"));
        var result = Parser.UpdateEffectExpression("id", effect, state);

        if ((BattleCard.Get(result) == null) || (effect.invokeTarget.Count == 0))
            return false;

        effect.invokeTarget.Insert(0, effect.source);

        if (effect.invokeTarget.Exists(x => state.GetCardPlaceInfo(x).place != BattlePlaceId.Field))
            return false;

        effect.invokeTarget.ForEach(x => state.GetBelongUnit(x).field.cards.Remove(x));

        EnqueueEffect("on_this_hybrid", effect.invokeTarget, state);
        OnPhaseChange("on_hybrid", state);

        string log = effect.invokeTarget.Select(x => x.CurrentCard.name).ConcatToString(" 與 ") + " 合體";
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        Effect summonEffect = new Effect("none", "none", null, null, 
            EffectAbility.Summon, new Dictionary<string, string>()
            {
                { "id", result.ToString() },
                { "count", "1" },
            })
        {
            source = effect.source,
            sourceEffect = effect,
            invokeUnit = effect.invokeUnit,
        };

        summonEffect.Apply(state);

        return true;
    }

    public static bool Bury(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        if (effect.invokeTarget.Count == 0)
            return false;

        if (unit.field.AvailableCount < effect.invokeTarget.Count)
            return false;

        for (int i = 0; i < effect.invokeTarget.Count; i++) {
            var info = state.GetCardPlaceInfo(effect.invokeTarget[i]);
            var infoUnit = (info.unitId == 0) ? state.myUnit : state.opUnit;
            if ((infoUnit.id != unit.id) || (info.place != BattlePlaceId.Hand))
                return false;
        }

        effect.invokeTarget.ForEach(x => x.RemoveEffect("all"));
    
        Effect summonEffect = new Effect("none", "none", null, null,
            EffectAbility.Summon, new Dictionary<string, string>() 
            {
                {"where", "hand"}
            })
        {
            source = effect.source,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = effect.invokeTarget,
        };

        summonEffect.Apply(state);
        state.currentEffect = effect;

        effect.hudOptionDict.Set("log", effect.source.CurrentCard.name + " 發動 " + effect.invokeTarget.Count + " 次葬送");
        Hud.SetState(state);

        Effect destroyEffect = new Effect("none", "none", null, null, EffectAbility.Destroy, null)
        {
            source = effect.source,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = effect.invokeTarget,
        };

        destroyEffect.Apply(state);
        state.currentEffect = effect;

        EnqueueEffect("on_this_bury", effect.invokeTarget, state);
        OnPhaseChange("on_bury", state);

        return true;
    }

    public static bool Reanimate(this Effect effect, BattleState state) {
        var who = effect.abilityOptionDict.Get("who", "me");
        var cost = Parser.UpdateEffectExpression("cost", effect, state, -1);
        var filter = BattleCardFilter.Parse(effect.abilityOptionDict.Get("filter", "none"));

        var unit = (who == "me") ? effect.invokeUnit : state.GetRhsUnitById(effect.invokeUnit.id);
        var pool = unit.grave.DestroyedFollowers;
        int maxCost = (pool.Count == 0) ? -1 : pool.Max(x => x.cost);

        effect.invokeTarget = new List<BattleCard>();
        for (int poolCost = Mathf.Min(maxCost, cost); poolCost >= 0; poolCost--) {
            var result = filter.FilterList(pool.Where(x => x.cost == poolCost).ToList()).Random();
            if (result != null) {
                effect.invokeTarget = new List<BattleCard>() { BattleCard.Get(result.id) };
                break;
            }
        }

        effect.hudOptionDict.Set("log", ((who == "me") ? "我方" : "對方") + "發動 亡者召還 " + cost);
        Hud.SetState(state);
        
        Effect summonEffect = new Effect("none", "none", null, null, 
            EffectAbility.Summon, new Dictionary<string, string>() 
            {
                { "where", "grave" },
            })
        {
            source = effect.source,
            sourceEffect = effect,
            invokeUnit = unit,
            invokeTarget = effect.invokeTarget.ToList(),
        };

        summonEffect.Apply(state);
        state.currentEffect = effect;

        effect.invokeTarget.RemoveAll(x => state.GetCardPlaceInfo(x).place != BattlePlaceId.Field);

        EnqueueEffect("on_this_reanimate", effect.invokeTarget, state);
        OnPhaseChange("on_reanimate", state);

        return true;
    }

    public static bool Discard(this Effect effect, BattleState state) {
        var unit = effect.invokeUnit;
        var isMyUnit = unit.id == state.myUnit.id;
        var typeId = effect.abilityOptionDict.Get("type", "destroy");
        var type = typeId.ToEffectAbility();
        var typeLog = type switch {
            EffectAbility.Destroy   => "被捨棄",
            EffectAbility.Vanish    => "在手牌中消失",
            _ => "離開手牌",
        };
        
        var target = effect.invokeTarget.Where(unit.hand.Contains).ToList();
        target.ForEach(x => unit.hand.cards.Remove(x));

        for (int i = 0; i < target.Count; i++) {
            var card = target[i].CurrentCard;

            effect.invokeTarget = new List<BattleCard>() { target[i] };

            // Remove and grave++;
            target[i].SetIdentifier("graveReason", (float)BattleCardGraveReason.Discard);
            target[i].SetIdentifier("graveIsMyTurn", unit.isMyTurn ? 1 : 0);
            target[i].SetIdentifier("graveTurn", unit.turn);
            unit.grave.cards.Add(target[i]);

            EnqueueEffect("on_this_discard_" + type, effect.invokeTarget, state);
        }

        effect.invokeTarget = target;
        unit.grave.GraveCount += (type == EffectAbility.Destroy) ? target.Count : 0;

        OnPhaseChange("on_discard_" + type, state);

        string log = isMyUnit ? effect.invokeTarget.Select(x => x.CurrentCard.name + " " + typeLog).ConcatToString("\n")
            : (effect.invokeTarget.Count + " 張卡片" + typeLog);

        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        return true;
    }

    public static bool Travel(this Effect effect, BattleState state) {
        var isMyUnit = effect.invokeUnit.id == state.myUnit.id;
        var filter = BattleCardFilter.Parse(effect.abilityOptionDict.Get("filter", string.Empty), (x, y) => Parser.ParseEffectExpression(y, effect, state).ToString());
        var count = Parser.UpdateEffectExpression("count", effect, state, 1);
        var tokenIds = filter.FilterList(CardDatabase.CardMaster).Random(count, false).Select(x => x.id).ToList();

        if (tokenIds.Count == 0)
            return false;

        Effect tokenEffect = new Effect("none", "none", null, null, EffectAbility.GetToken, new Dictionary<string, string>()
        {
            { "hide", "true" },
        })
        {
            source = effect.source,
            sourceEffect = effect,
            invokeUnit = effect.invokeUnit,
            invokeTarget = tokenIds.Select(BattleCard.Get).ToList(),
        };

        tokenEffect.Apply(state);
        state.currentEffect = effect;

        effect.invokeTarget = tokenEffect.invokeTarget.ToList();
        EnqueueEffect("on_this_travel", effect.invokeTarget, state);
        OnPhaseChange("on_travel", state);

        effect.hudOptionDict.Set("log", string.Empty);
        Hud.SetState(state);

        return true;
    }

    public static bool Reveal(this Effect effect, BattleState state)
    {
        effect.hudOptionDict.Set("log", effect.invokeTarget.Select(x => "公開 " + x.CurrentCard.name).ConcatToString("\n"));
        Hud.SetState(state);

        EnqueueEffect("on_this_reveal", effect.invokeTarget, state);
        OnPhaseChange("on_reveal", state);
        return true;
    }

    public static bool SetPP(this Effect effect, BattleState state)
    {
        var add = Parser.UpdateEffectExpression("add", effect, state);

        effect.invokeTarget = new List<BattleCard>() { effect.invokeUnit.leader.leaderCard };
        effect.invokeUnit.leader.PP += add;

        var log = string.Empty;
        if(add > 0)
            log = "回復自己的PP " + add + " 點";
        else if(add < 0)
            //log = "消耗自己的PP " + -add + " 點";

        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        EnqueueEffect("on_this_set_pp", effect.invokeTarget, state);
        OnPhaseChange("on_set_pp", state);

        return true;
    }

    public static bool SetEP(this Effect effect, BattleState state)
    {
        var add = Parser.UpdateEffectExpression("add", effect, state);

        effect.invokeTarget = new List<BattleCard>() { effect.invokeUnit.leader.leaderCard };
        effect.invokeUnit.leader.EP += add;

        var log = string.Empty;
        if (add > 0)
            log = "回復自己的EP " + add + " 點";
        else if (add < 0)
            //log = "消耗自己的PP " + -add + " 點";

            effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        EnqueueEffect("on_this_set_ep", effect.invokeTarget, state);
        OnPhaseChange("on_set_ep", state);

        return true;
    }

    public static bool Choose(this Effect effect, BattleState state)
    {
        OnPhaseChange("on_choose", state);
        return true;
    }

    public static bool SetValue(this Effect effect, BattleState state)
    {
        var id = effect.abilityOptionDict.Get("id", "none");
        var hide = bool.Parse(effect.abilityOptionDict.Get("hide", "true"));
        var num = Parser.UpdateEffectExpression("value", effect, state);
        /*var mode = ModifyOption.None;
        if (effect.abilityOptionDict.TryGetValue("add", out _))
            mode = ModifyOption.Add;
        else if(effect.abilityOptionDict.TryGetValue("set", out _))
            mode = ModifyOption.Set;*/

        /*if (id == 0 || mode.Equals(ModifyOption.None))
            return false;

        var stats = Stats.Get(id);

        if (mode.Equals(ModifyOption.Add))
        {
            if (stats.type == StatsType.Leader)
                effect.invokeUnit.leader.AddIdentifier(stats.name, Parser.UpdateEffectExpression("add", effect, state));
            else if (stats.type == StatsType.Card)
                effect.source.AddIdentifier(stats.name, Parser.UpdateEffectExpression("add", effect, state));
        }
        else if (mode.Equals(ModifyOption.Set))
        {
            if (stats.type == StatsType.Leader)
                effect.invokeUnit.leader.SetIdentifier(stats.name, Parser.UpdateEffectExpression("set", effect, state));
            else if (stats.type == StatsType.Card)
                effect.source.SetIdentifier(stats.name, Parser.UpdateEffectExpression("set", effect, state));
        }*/

        Identifier.SetIdentifier(id, num, effect, state);

        effect.hudOptionDict.Set("log", hide ? string.Empty : effect.source.CurrentCard.name + " 設定狀態");
        Hud.SetState(state);
        return true;
    }

    public static bool SetCharge(this Effect effect, BattleState state)
    {
        var situation = effect.abilityOptionDict.Get("situation", "none");
        var add = Parser.UpdateEffectExpression("add", effect, state);

        effect.invokeTarget.ForEach(x => x.TakeCharge(add));

        var log = effect.invokeTarget.Select(x => x.CurrentCard.name + " 蓄积 " + add.ToStringWithSign()).ConcatToString();
        effect.hudOptionDict.Set("log", log);
        Hud.SetState(state);

        return true;
    }
}
