using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ExitGames.Client.Photon.StructWrapping;

public class BattleCard : IIdentifyHandler
{
    public Battle Battle => Player.currentBattle;
    public BattleManager Hud => BattleManager.instance;
    public static string[] FlagProperties => new string[] { "bane", "drain", "earth", "flag", "lastword" };

    public int Id => CurrentCard.Id;
    public string Name => CurrentCard.Name;

    public bool IsEvolved { get; protected set; }
    public Card baseCard;
    public Card evolveCard;
    public Card OriginalCard => IsEvolved ? evolveCard : baseCard;
    public Card CurrentCard => GetCurrentCard(OriginalCard);
    public List<KeyValuePair<Func<bool>, Effect>> newEffects = new List<KeyValuePair<Func<bool>, Effect>>();
    public BattleCardBuffController buffController;
    //public BattleCardActionController actionController;

    public Dictionary<string, float> options = new Dictionary<string, float>();

    public int StayFieldTurn
    {
        get => (int)GetIdentifier("stayFieldTurn", 0);
        set => SetIdentifier("stayFieldTurn", value);
    }

    /*public int MaxAttackTimes
    {
        get => (int)GetIdentifier("maxAttackTimes", 0);
        set => SetIdentifier("maxAttackTimes", value);
    }*/

    public int CurrentAttackTimes
    {
        get => (int)GetIdentifier("attackTimes", 0);
        set => SetIdentifier("attackTimes", value);
    }

    public int FusionTimes
    {
        get => (int)GetIdentifier("fusionTimes", 0);
        set => SetIdentifier("fusionTimes", value);
    }

    public int MaxAttackTimes => GetMaxAttackTimes();

    public bool IsAttackFinished => CurrentAttackTimes >= MaxAttackTimes;

    //新增效果索引优化效率
    public BattleCard(Card card) {
        IsEvolved = false;

        baseCard = (card == null) ? null : new Card(card);
        evolveCard = (baseCard?.EvolveCard == null) ? null : new Card(baseCard.EvolveCard);
        
        baseCard?.effects.ForEach(x => x.source = this);
        evolveCard?.effects.ForEach(x => x.source = this);

        buffController = new BattleCardBuffController();
        //actionController = new BattleCardActionController();
    }

    public BattleCard(BattleCard rhs) {
        IsEvolved = rhs.IsEvolved;
        
        baseCard = (rhs.baseCard == null) ? null : new Card(rhs.baseCard);
        evolveCard = (rhs.evolveCard == null) ? null : new Card(rhs.evolveCard);

        baseCard?.effects.ForEach(x => x.source = this);
        evolveCard?.effects.ForEach(x => x.source = this);

        /*newEffects = rhs.newEffects.Select(x => new KeyValuePair<Func<bool>, Effect>
            (x.Key, new Effect(x.Value))).ToList();*/

        //newEffects.ForEach(x => x.Value.source = this);

        buffController = new BattleCardBuffController(rhs.buffController);
        //actionController = new BattleCardActionController(rhs.actionController);

        options = new Dictionary<string, float>(rhs.options);
    }

    public static BattleCard Get(Card baseCard) {
        return (baseCard == null) ? null : new BattleCard(baseCard);
    }

    public static BattleCard Get(int id) {
        return BattleCard.Get(Card.Get(id));
    }

    public bool TryGetIdenfier(string id, out float value)
    {
        value = GetIdentifier(id);
        return value != float.MinValue;
    }

    public float GetIdentifier(string id, float defaultReturn = float.MinValue)
    {
        return options.Get(id, defaultReturn);
    }

    public void SetIdentifier(string id, float num)
    {
        /*if (id.StartsWith("isAttacked"))
            actionController.SetIdentifier(id, num);
        else */
        options.Set(id, num);
    }

    public void AddIdentifier(string id, float num)
    {
        SetIdentifier(id, GetIdentifier(id, 0) + num);
    }

    public Card GetCurrentCard(Card baseCard) {
        if (baseCard == null)
            return null;
            
        var result = new Card(baseCard);
        result.cost = Mathf.Max(result.cost + buffController.CostBuff, 0);
        result.atk = Mathf.Max(result.atk + buffController.AtkBuff, 0);
        result.hpMax = Mathf.Max(result.hpMax + buffController.HpBuff, 0);
        result.hp = Mathf.Max(result.hpMax - buffController.Damage, 0);

        var tempTraits = buffController.TraitBuff;
        var tempKeywords = buffController.KeywordBuff;
        var tempEffects = buffController.EffectBuff;
        result.tempTraits = tempTraits;
        result.tempKeywords = tempKeywords;
        result.tempEffects = tempEffects;
        result.traits.AddRange(tempTraits);
        result.keywords.AddRange(tempKeywords);
        result.effects.AddRange(tempEffects);

        /*var statsEffects = result.effects
                                .Where(x => x.statsList != null)
                                .SelectMany(x => x.statsList)
                                .Distinct()
                                .SelectMany(x => x.effects)
                                .ToList(); 
        result.effects.AddRange(statsEffects);*/

        result.effects.ForEach(x => x.source = this);
        return result;
    }

    public int GetMaxAttackTimes()
    {
        var attackTimesEffects = CurrentCard.effects.Where(x => x.ability == EffectAbility.SetAttackTimes);
        if (attackTimesEffects.Any())
        {
            var maxAttackTimes = (int)attackTimesEffects.Max(x => x.GetIdentifier("value", 1));
            return maxAttackTimes;
        }
        return 1;
    }

    public BattleCard GetCurrentBattleCard(int cost, string situation) {
        if (string.IsNullOrEmpty(situation))
            return this;

        var ability = situation.ToEffectAbility();
        var effect = CurrentCard.effects.Find(x => (x.ability == ability) && (int.Parse(x.abilityOptionDict.Get(situation, "-1")) == cost));
        if (effect == null)
            return this;

        return BattleCard.Get(int.Parse(effect.abilityOptionDict.Get("id", "-1"))) ?? this;
    }

    public string GetConditionDescription() {
        var card = CurrentCard;
        var description = string.Empty;
        var statsList = card.effects
                        .Where(x => x.statsList != null)
                        .SelectMany(x => x.statsList)
                        .Distinct()
                        .ToList();

        /*var leaderInfoKeys = new List<string>();
        var sourceInfoKeys = new List<string>();

        for (int i = 0; i < card.effects.Count; i++) {
            var currentEffect = card.effects[i];
            if (List.IsNullOrEmpty(currentEffect.statsList))
                continue;

            *//*for (int j = 0; j < currentEffect.statsList.Count; j++) {
                var currentCondition = currentEffect.statsList[j];
                if (currentCondition.TryTrimStart("leader.", out var leaderKey))
                    leaderInfoKeys.Add(leaderKey);
                else if (currentCondition.TryTrimStart("source.", out var sourceKey))
                    sourceInfoKeys.Add(sourceKey);
            }*//*
        }
        leaderInfoKeys = leaderInfoKeys.Distinct().ToList();
        sourceInfoKeys = sourceInfoKeys.Distinct().ToList();

        for (int i = 0; i < leaderInfoKeys.Count; i++) {
            var leaderStats = Stats.GetByName(leaderInfoKeys[i]);
            if (leaderStats == null || leaderStats.type == StatsType.Card) 
                continue;
            var num = Hud.CurrentState.GetBelongUnit(this).leader.GetIdentifier(leaderStats.name, 0);
            description += "（當前 " + leaderStats.description + " 為 " + num + "）\n";
        }

        for (int i = 0; i < sourceInfoKeys.Count; i++) {
            description += "（當前 " + sourceInfoKeys[i].ToSourceInfoValue() + " 為 " + GetIdentifier(sourceInfoKeys[i], 0) + "）\n";
        }*/

        foreach(var stats in statsList)
        {
            if (stats == null)
                continue;

            var num = 0f;
            if(stats.type == StatsType.Leader)
            {
                num = Hud.CurrentState.GetBelongUnit(this).leader.GetIdentifier(stats.name, 0);
            }else if(stats.type == StatsType.Card)
            {
                num = GetIdentifier(stats.name, 0);
            }
            description += "（當前 " + stats.description + " 為 " + num + "）\n";
        }

        return description;
    }

    public string GetAdditionalDescription() {
        var description = string.Empty;
        var costBuff = buffController.CostBuff;
        var atkBuff = buffController.AtkBuff;
        var hpBuff = buffController.HpBuff;
        //var originalKeywords = Card.Get(Id).keywords;

        foreach(var keyword in CardDatabase.cardKeywords)
        {
            if (buffController.KeywordBuff.Contains(keyword))
                description += ("[" + keyword.GetKeywordName() + "]").GetDescription() + "\n";
        }

        foreach (var trait in CardDatabase.traits)
        {
            if (buffController.TraitBuff.Contains(trait))
                description += trait.GetTraitName().GetDescription() + "\n";
        }

        var buffDescription = string.Empty;

        foreach(var buff in buffController.buffs) {
            if (!string.IsNullOrEmpty(buff.description))
                buffDescription += buff.description + "\n";
        }

        if(!string.IsNullOrEmpty(buffDescription))
            description += "------\n" + buffDescription;

        /*for (int i = 0; i < newEffects.Count; i++) {
            if (newEffects[i].Value.hudOptionDict.TryGetValue("description", out var effectDesc) && (!string.IsNullOrEmpty(effectDesc)))
                description += effectDesc + "\n";
        }*/

        //return (description.Count(x => x == '\n') == 1) ? string.Empty : description.TrimEnd('\n');

        if (!string.IsNullOrEmpty(description))
            description = atkBuff.ToStringWithSign() + "/" + hpBuff.ToStringWithSign() + "\n" + description;

        return description.TrimEnd('\n');
    }

    public void GetTargetEffectWithTiming(string timing, out Queue<Effect> targetEffectQueue, out Queue<EffectTargetInfo> targetInfoQueue, out Queue<List<int>> selectableTargetQueue) {
        bool isEvolveTiming = timing == "on_this_evolve_with_ep";

        var nowCost = GetUseCost(Hud.CurrentState.myUnit.leader, out var situation);
        var nowBattleCard = GetCurrentBattleCard(nowCost, situation);
        var nowCard = GetCurrentCard(isEvolveTiming ? nowBattleCard.evolveCard : nowBattleCard.OriginalCard);

        targetEffectQueue = new Queue<Effect>();
        targetInfoQueue = new Queue<EffectTargetInfo>();
        selectableTargetQueue = new Queue<List<int>>();

        for (int i = 0; i < nowCard.effects.Count; i++) {
            var currentEffect = nowCard.effects[i];
            if (currentEffect.timing != timing)
                continue;

            currentEffect.invokeUnit = Hud.CurrentState.myUnit;
            if (currentEffect.Condition(Hud.CurrentState)) {
                var info = currentEffect.GetEffectTargetInfo(Hud.CurrentState);

                if ((!List.IsNullOrEmpty(info.mode)) && (info.mode[0] == "index")) {
                    targetEffectQueue.Enqueue(currentEffect);
                    targetInfoQueue.Enqueue(info);
                    selectableTargetQueue.Enqueue(GetCurrentSelectableTarget(info));
                }

                var appendixEffect = currentEffect;
                //TODO
                while (appendixEffect.abilityOptionDict.TryGetValue("appendix", out var trimId)) {
                    var appendixIds = trimId.ToIntList('/');
                    foreach (var appendixId in appendixIds)
                    {
                        appendixEffect = Effect.Get(appendixId);
                        if (appendixEffect == null)
                            break;

                        appendixEffect.invokeUnit = Hud.CurrentState.myUnit;
                        if (appendixEffect.target != "none" && appendixEffect.Condition(Hud.CurrentState))
                        {
                            var appendixInfo = appendixEffect.GetEffectTargetInfo(Hud.CurrentState);

                            if ((!List.IsNullOrEmpty(appendixInfo.mode)) && (appendixInfo.mode[0] == "index"))
                            {
                                targetEffectQueue.Enqueue(appendixEffect);
                                targetInfoQueue.Enqueue(appendixInfo);
                                selectableTargetQueue.Enqueue(GetCurrentSelectableTarget(appendixInfo));
                            }
                        }
                        appendixEffect.invokeUnit = null;
                    }
                }
            }
            currentEffect.invokeTarget = null;
        }
    }

    public List<int> GetCurrentSelectableTarget(EffectTargetInfo currentInfo) {
        List<int> currentSelectableList = new List<int>();

        if (currentInfo.places.Contains(BattlePlaceId.Hand)) {
            var myHand = Hud.CurrentState.myUnit.hand;
            var opHand = Hud.CurrentState.opUnit.hand;

            var myFilteredCards = (currentInfo.unit == "op") ? new List<BattleCard>() :
                currentInfo.filter.FilterCurrentCardList(myHand.cards);
            var opFilteredCards = (currentInfo.unit == "me") ? new List<BattleCard>() :
                currentInfo.filter.FilterCurrentCardList(opHand.cards);

            var myIndex = myFilteredCards.Select(card => myHand.cards.IndexOf(card)).ToList();
            var opIndex = opFilteredCards.Select(card => opHand.cards.IndexOf(card)).ToList();

            /*var myIndex = (currentInfo.unit == "op") ? new List<int>() : 
                Enumerable.Range(0, myHand.Count).Where(x => currentInfo.filter.FilterWithCurrentCard(myHand.cards[x])).ToList();
                    
            var opIndex = (currentInfo.unit == "me") ? new List<int>() : 
                Enumerable.Range(0, opHand.Count).Where(x => opHand.cards[x].IsTargetSelectable() && 
                    currentInfo.filter.FilterWithCurrentCard(opHand.cards[x])).ToList();*/

            myIndex.ForEach(x => currentSelectableList.Add((int)((int)BattlePlaceId.Hand * 10 + x)));
            opIndex.ForEach(x => currentSelectableList.Add((int)(100 + (int)BattlePlaceId.Hand * 10 + x)));
        }

        if (currentInfo.places.Contains(BattlePlaceId.Field)) {
            var myField = Hud.CurrentState.myUnit.field;
            var opField = Hud.CurrentState.opUnit.field;

            var myFilteredCards = (currentInfo.unit == "op") ? new List<BattleCard>() :
                currentInfo.filter.FilterCurrentCardList(myField.cards);
            var opFilteredCards = (currentInfo.unit == "me") ? new List<BattleCard>() :
                currentInfo.filter.FilterCurrentCardList(opField.cards.Where(x => x.IsTargetSelectable()).ToList());

            var myIndex = myFilteredCards.Select(card => myField.cards.IndexOf(card)).ToList();
            var opIndex = opFilteredCards.Select(card => opField.cards.IndexOf(card)).ToList();

            /*var myIndex = (currentInfo.unit == "op") ? new List<int>() : 
                Enumerable.Range(0, myField.Count).Where(x => currentInfo.filter.FilterWithCurrentCard(myField.cards[x])).ToList();
                    
            var opIndex = (currentInfo.unit == "me") ? new List<int>() : 
                Enumerable.Range(0, opField.Count).Where(x => opField.cards[x].IsTargetSelectable() && 
                    currentInfo.filter.FilterWithCurrentCard(opField.cards[x])).ToList();*/

            myIndex.ForEach(x => currentSelectableList.Add((int)BattlePlaceId.Field * 10 + x));
            opIndex.ForEach(x => currentSelectableList.Add(100 + (int)BattlePlaceId.Field * 10 + x));
        }

        if (currentInfo.places.Contains(BattlePlaceId.Leader)) {
            if (currentInfo.unit != "op")
                currentSelectableList.Add((int)BattlePlaceId.Leader * 10);

            if (currentInfo.unit != "me")
                currentSelectableList.Add(100 + (int)BattlePlaceId.Leader * 10);
        }

        if (currentInfo.mode.Contains("other"))
            currentSelectableList.Remove(Hud.CurrentState.GetCardPlaceInfo(this).ToIntCode());

        return currentSelectableList;
    }

    public int GetUseCost(Leader leader, out string situation) {
        var card = CurrentCard;
        var effects = card.effects;

        List<int> GetSecondChoiceCostList(string choiceSituation, out int choiceCost) {
            var choiceList = new List<int>() { -1 };
            for (int i = 0; i < effects.Count; i++) {
                var currentEffect = effects[i];
                currentEffect.invokeUnit = Hud.CurrentState.myUnit;
                if (currentEffect.abilityOptionDict.TryGetValue(choiceSituation, out var choiceCostId) && currentEffect.Condition(Hud.CurrentState))
                    choiceList.Add(int.Parse(choiceCostId));
            }

            choiceCost = choiceList.Where(x => x <= leader.PP).Max();
            return choiceList;
        }

        var choiceList = GetSecondChoiceCostList("choice", out var choiceCost);
        if (choiceCost != -1)
        {
            situation = "choice";
            return choiceCost;
        }

        var enhanceList = GetSecondChoiceCostList("enhance", out var enhanceCost);
        if (enhanceCost != -1) {
            situation = "enhance";
            return enhanceCost;
        }

        var accelList = GetSecondChoiceCostList("accelerate", out var accelCost);
        var crystalList = GetSecondChoiceCostList("crystalize", out var crystalCost);

        // If cost enough or no accel/crystal effect
        if ((card.cost <= leader.PP) || ((accelList.Count <= 1) && (crystalList.Count <= 1))) {
            situation = string.Empty;
            return card.cost;
        }

        // If accel/crystal effect exists but not enough cost => show min cost.
        if ((accelCost == -1) && (crystalCost == -1)) {
            accelCost = accelList.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
            crystalCost = crystalList.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
        }

        bool isAccel = accelCost >= crystalCost;
        situation = isAccel ? "accelerate" : "crystalize";
        return isAccel ? accelCost : crystalCost;
    }

    public bool IsUsable(BattleUnit sourceUnit) {
        var useCost = GetUseCost(sourceUnit.leader, out var situation);
        var useCard = GetCurrentBattleCard(useCost, situation);

        bool isCostEnough = sourceUnit.leader.PP >= useCost;
        bool isFieldFull = useCard.CurrentCard.IsFollower() && sourceUnit.field.IsFull;
        bool isSpellTargetable = true;

        if (useCard.CurrentCard.Type == CardType.Spell) {
            GetTargetEffectWithTiming("on_this_play", out _, out var infoQueue, out var selectableQueue);

            while (infoQueue.Count > 0) {
                isSpellTargetable = infoQueue.Dequeue().num <= selectableQueue.Dequeue().Count;
                if (!isSpellTargetable)
                    break;
            }
        }

        return sourceUnit.isMyTurn && isCostEnough && (!isFieldFull) && isSpellTargetable;
    }

    public int GetEvolveCost() {
        return Mathf.CeilToInt(CurrentCard.GetIdentifier("evolveCost"));
    }

    public bool IsFreeEvolve()
    {
        return CurrentCard.keywords.Contains(CardKeyword.FreeEvolve);
    }

    public bool IsBanEvolve()
    {
        return CurrentCard.keywords.Contains(CardKeyword.BanEvolve);
    }

    public bool IsEvolvable(BattleUnit sourceUnit) {
        bool isUnevolvedFollower = CurrentCard.Type == CardType.Follower;
        //bool isEpUnused = (sourceUnit.isEvolveEnabled) && (!sourceUnit.leader.isEpUsed) && (sourceUnit.leader.EP >= GetEvolveCost());
        bool isEpUnused = (sourceUnit.isEvolveEnabled) && (!sourceUnit.leader.isEpUsed) && (IsFreeEvolve());
        return IsBanEvolve() && isUnevolvedFollower && isEpUnused;
    }

    public bool IsAttackable(BattleUnit sourceUnit) {
        return IsLeaderAttackable(sourceUnit) || IsFollowerAttackable(sourceUnit);
    }
    public bool IsFusionable(BattleUnit sourceUnit)
    {
        var isFusionLegal = CurrentCard.effects.Count(x => x.ability == EffectAbility.Fusion && x.Condition(Hud.CurrentState)) == 1;
        var isFusionChanceLegal = FusionTimes == 0;
        return sourceUnit.isMyTurn && isFusionLegal && isFusionChanceLegal;
    }

    public bool IsLeaderAttackable(BattleUnit sourceUnit) {
        var isAttackChanceLegal = (CurrentAttackTimes < MaxAttackTimes) && (!IsKeywordAvailable(CardKeyword.Freeze));
        var isStayTurnLegal = StayFieldTurn > 0;
        var isKeywordLegal = IsKeywordAvailable(CardKeyword.Storm);

        return sourceUnit.isMyTurn && isAttackChanceLegal && (isStayTurnLegal || isKeywordLegal);
    }

    public bool IsFollowerAttackable(BattleUnit sourceUnit) {
        var isAttackChanceLegal = (CurrentAttackTimes < MaxAttackTimes) && (!IsKeywordAvailable(CardKeyword.Freeze));
        var isStayTurnLegal = StayFieldTurn > 0;
        var isKeywordLegal = IsKeywordAvailable(CardKeyword.Storm) || IsKeywordAvailable(CardKeyword.Rush);

        return sourceUnit.isMyTurn && isAttackChanceLegal && (IsEvolved || isStayTurnLegal || isKeywordLegal);
    }

    public bool IsTargetSelectable() {
        bool isAmbush = IsKeywordAvailable(CardKeyword.Ambush);
        bool isAura = IsKeywordAvailable(CardKeyword.Aura);
        return (!isAmbush) && (!isAura);
    }

    public void RemoveUntilEffect() {
        newEffects.RemoveAll(x => (x.Key != null) && (x.Key.Invoke()));
        buffController.RemoveUntilEffect();    
    }

    // Evolve this follower. You should check IsEvolvable() before calling this if you use EP evolve.
    public void Evolve() {
        //TODO 同步進化前後能力使用次數
        IsEvolved = true;
    }

    public bool IsKeywordAvailable(CardKeyword keyword)
    {
        return CurrentCard.keywords.Contains(keyword);
    }

    public void Leave(BattleCardGraveReason reason, BattleState state = null)
    {
        if (state == null)
            state = Hud.CurrentState;

        var belongPlace = state.GetBelongPlace(this);
        SetIdentifier("graveReason", (float)reason);
        SetIdentifier("graveIsMyTurn", Hud.CurrentState.myUnit.isMyTurn ? 1 : 0);
        SetIdentifier("graveTurn", Hud.CurrentState.currentUnit.turn);
        belongPlace.cards.Remove(this);
        belongPlace.unit.grave.cards.Add(this);
    }

    public void SetKeyword(List<CardKeyword> keywords, string untilCondition, ModifyOption option) {
        if (option == ModifyOption.Add) {
            buffController.AddKeyword(keywords, untilCondition);
        } else if (option == ModifyOption.Remove) {
            baseCard.keywords.RemoveAll(x => keywords.Contains(x));
            evolveCard?.keywords.RemoveAll(x => keywords.Contains(x));
            buffController.buffs?.ForEach(x => x.keywords.RemoveAll(y => keywords.Contains(y)));
        }
    }

    public int TakeDamage(int damage, Effect effect = null, BattleState state = null) {
        /*var damageEffects = CurrentCard.effects.Where(x => x.ability == EffectAbility.SetDamage);
        var addEffects = damageEffects.Where(x => x.abilityOptionDict.ContainsKey("add")).ToList();
        var setEffects = damageEffects.Where(x => x.abilityOptionDict.ContainsKey("set")).ToList();

        addEffects = GetValidTakeEffects(addEffects, effect, state, "damage", damage);
        addEffects.ForEach(x => damage += Parser.ParseEffectExpression(x.abilityOptionDict.Get("add"), effect, state));

        setEffects = GetValidTakeEffects(setEffects, effect, state, "damage", damage);
        if (setEffects.Count > 0)
            damage = setEffects.Select(x => Parser.ParseEffectExpression(x.abilityOptionDict.Get("set"), effect, state)).Min();*/
        return buffController.TakeDamage(damage);
    }

    public int TakeHeal(int heal, Effect effect = null, BattleState state = null) {
        /*var healEffects = CurrentCard.effects.Where(x => x.ability == EffectAbility.SetHeal);
        var addEffects = healEffects.Where(x => x.abilityOptionDict.ContainsKey("add")).ToList();
        var setEffects = healEffects.Where(x => x.abilityOptionDict.ContainsKey("set")).ToList();

        addEffects = GetValidTakeEffects(addEffects, effect, state, "heal", heal);
        addEffects.ForEach(x => heal += Parser.ParseEffectExpression(x.abilityOptionDict.Get("add"), effect, state));

        setEffects = GetValidTakeEffects(setEffects, effect, state, "heal", heal);
        if (setEffects.Count > 0)
            heal = setEffects.Select(x => Parser.ParseEffectExpression(x.abilityOptionDict.Get("set"), effect, state)).Min();*/
        return buffController.TakeHeal(heal);
    }

    public void TakeBuff(CardStatus status, string untilCondition) {

        buffController.TakeBuff(status, untilCondition);
    }

    public int TakeCountdown(int add) {
        if (baseCard.countdown > 0)
            baseCard.countdown = Mathf.Max(baseCard.countdown + add, 0); 
            
        return add;
    }

    public int TakeCharge(int add)
    {
        if (OriginalCard.charge > 0)
            OriginalCard.charge = Mathf.Max(OriginalCard.charge + add, 0);

        return add;
    }

    public void OnTurnStart(BattlePlaceId placeId)
    {
        //CurrentCard.effects.ForEach(x => x.OnTurnStart());
        buffController.OnTurnStart();
        if (placeId == BattlePlaceId.Field)
        {
            StayFieldTurn += 1;
            CurrentAttackTimes = 0;
            FusionTimes = 0;
        }

        foreach (var entry in CardDatabase.cardStats)
        {
            if (entry.duration == "turn" && options.TryGetValue(entry.name, out _))
            {
                SetIdentifier(entry.name, 0);
            }
        }
    }

    /*public void RemoveEffectAuto(Effect effect)
    {
        if (effect.id == 0)
        {
            newEffects.RemoveAll(x => x.Value == effect);
            return;
        }

        baseCard.ClearEffect(effect.id);
        evolveCard.ClearEffect(effect.id);
    }*/


    public void RemoveEffect(string timing = "none", int id = 0, CardKeyword keyword = CardKeyword.None) {
        baseCard.ClearEffects(timing, id, keyword);
        evolveCard.ClearEffects(timing, id, keyword);
        buffController.buffs.ForEach(x => x.ClearEffects(timing, id, keyword));
        //newEffects.RemoveAll(x => (timing == "all") || (timing == x.Value.timing));

        /*if (timing == "all") {    
            foreach (var cardKeyword in CardDatabase.keywords)
                SetKeyword(cardKeyword, ModifyOption.Remove);
        }*/
    }

}
