using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum EffectPhase
{
    Before = -1,
    On = 0,
    After = 1
}

public class Effect : IIdentifyHandler
{
    public const int DATA_COL = 10;
    public static Effect Get(int id) {
        var effect = DatabaseManager.instance.GetEffectInfo(id);
        return (effect == null) ? null : new Effect(effect);
    }

    public static Effect Get(string name)
    {
        var effect = DatabaseManager.instance.GetEffectInfo(name);
        return (effect == null) ? null : new Effect(effect);
    }

    public static Effect None => new Effect("none", "none", null, null, EffectAbility.None, null);
    public static Battle Battle => Player.currentBattle;

    public BattleCard source = null;
    public Effect sourceEffect = null;
    public Buff sourceBuff = null;
    public BattlePlace sourcePlace = null;
    public List<BattleCard> invokeTarget = null;
    public BattleUnit invokeUnit = null;
    public EffectPhase phase;
    public int level;
    public float priority;

    public int id;
    public int Id => id;
    public string name { get; private set; }
    public string Name => name;
    public string timing { get; private set; }
    public string target { get; private set; }
    public BattlePlaceId place { get; private set; }
    public List<CardKeyword> keywords { get; private set; } = new List<CardKeyword>();
    public List<Stats> statsList { get; private set; }
    public List<ICondition> condDictList { get; private set; } = new List<ICondition>();
    public EffectAbility ability { get; private set; }
    public Dictionary<string, string> abilityOptionDict { get; private set; } = new Dictionary<string, string>();
    //TODO 优化LOG相关逻辑 *hide
    public Dictionary<string, string> hudOptionDict { get; private set; } = new Dictionary<string, string>();
    public Dictionary<string, string> outputOptionDict { get; private set; } = new Dictionary<string, string>();


    /*public int MaxUseTimes {
        get => (int)GetIdentifier("maxUseTimes");
        set => SetIdentifier("maxUseTimes", value); 
    }*/

    /*public int UseTimes
    {
        get => (int)GetIdentifier("useTimes",0);
        set => SetIdentifier("useTimes", value);
    }
    public int TurnUseTimes
    {
        get => (int)GetIdentifier("turnUseTimes",0);
        set => SetIdentifier("turnUseTimes", value);
    }*/

    public Effect(string[] _data, int startIndex) {
        string[] _slicedData = new string[DATA_COL];
        Array.Copy(_data, startIndex, _slicedData, 0, _slicedData.Length);
        source = null;
        id = int.Parse(_slicedData[0]);
        name = _slicedData[1];
        timing = _slicedData[2];
        place = Enum.Parse<BattlePlaceId>(_slicedData[3], true);
        target = _slicedData[4];
        keywords = _slicedData[5].ToIntList('/').Select(x => (CardKeyword)x).ToList();
        statsList = (_slicedData[6] == "none") ? null : _slicedData[6].Split('/').Select(x => Stats.GetByName(x)).ToList();
        condDictList = condDictList.ParseCondition(_slicedData[7]);
        ability = _slicedData[8].ToEffectAbility();
        abilityOptionDict.ParseOptions(_slicedData[9]);
        hudOptionDict = new Dictionary<string, string>();
    }

    public Effect(string _timing, string _target,
        List<Stats> _stats, List<ICondition> _condition,
        EffectAbility _ability, Dictionary<string, string> _ability_option,
        string _name = "default") {

        source = null;

        name = _name;
        timing = _timing;
        target = _target;
        statsList = _stats;
        if (_condition == null) {
            condDictList = new List<ICondition>();
        } else {
            condDictList = _condition;
        }
        ability = _ability;
        abilityOptionDict = _ability_option ?? new Dictionary<string, string>();
        hudOptionDict = new Dictionary<string, string>();
    }

    public Effect(int[] data) {
        source = null;
        timing = "none";
        target = "none";
        statsList = null;
        condDictList = new List<ICondition>();
        ability = (EffectAbility)data[0];
        abilityOptionDict = Parse(ability, data.SubArray(1));
        hudOptionDict = new Dictionary<string, string>();
    }

    public Effect(Effect rhs) {
        id = rhs.id;
        name = rhs.name;
        timing = rhs.timing;
        place = rhs.place;
        target = rhs.target;
        keywords = rhs.keywords;
        statsList = rhs.statsList;
        ability = rhs.ability;
        condDictList = rhs.condDictList.Select(x => new ICondition(x.op, x.lhs, x.rhs)).ToList();
        abilityOptionDict = new Dictionary<string, string>(rhs.abilityOptionDict);
        outputOptionDict = new Dictionary<string, string>(rhs.outputOptionDict);
        hudOptionDict = new Dictionary<string, string>(rhs.hudOptionDict);

        source = rhs.source;
        sourceEffect = rhs.sourceEffect;
        sourceBuff = rhs.sourceBuff;
        invokeUnit = rhs.invokeUnit;
        invokeTarget = rhs.invokeTarget?.ToList();
    }

    /*public Effect(Stats stats)
    {
        source = null;
        timing = stats.timing;
        target = "none";
        statsList = null;
        condDictList = stats.condDictList;
        ability = EffectAbility.SetValue;
        var tempOption = new Dictionary<string, string>(stats.options);
        tempOption.Add("id", stats.id.ToString());
        abilityOptionDict = tempOption;
        hudOptionDict = new Dictionary<string, string>();
    }*/

    public float GetPriority(int level, EffectPhase phase)
    {
        var priority = sourceEffect.priority;
        for(int i = 0; i < level; i++)
        {
            priority *= 10;
        }
        priority += (int)phase;
        for (int i = 0; i < level; i++)
        {
            priority /= 10;
        }
        return priority;
    }

    public bool TryGetIdenfier(string id, out float value)
    {
        value = GetIdentifier(id);
        return value != float.MinValue;
    }

    public float GetIdentifier(string id, float defaultReturn = float.MinValue)
    {
        var trimId = string.Empty;

        if (id.TryTrimStart("source.", out trimId)) {
            return trimId switch {
                "isMe"  => (invokeUnit.GetBelongPlace(source) != null) ? 1 : 0,
                "isOp" => (invokeUnit.GetBelongPlace(source) != null) ? 0 : 1,
                //"where" => (float)(invokeUnit.GetBelongPlace(source)?.PlaceId ?? BattlePlaceId.None),
                _ => source.GetIdentifier(trimId, invokeUnit, defaultReturn),
            };
        }

        if (id.Contains("?"))
        {
            var parts = id.Split('?', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (abilityOptionDict.TryGetValue(parts[0].Trim(), out var value))
                {
                    return value.Equals(parts[1].Trim()) ? 1 : 0;
                }else
                    return defaultReturn;
            }
        }

        return id switch {
            "isMe" => (invokeUnit.id == Battle.CurrentState.myUnit.id) ? 1 : 0,
            "isOp" => (invokeUnit.id == Battle.CurrentState.myUnit.id) ? 0 : 1,
            _ => outputOptionDict.TryGetValue(id, out var temp) && float.TryParse(temp, out var value) ? value : defaultReturn,
        };
    }

    public float GetSourceEffectIdentifier(string id, BattleUnit unit, float defaultReturn = float.MinValue) {
        if(id.TryTrimStart("target", out id))
        {
            var all = invokeTarget;
            var trimId = id;
            if (trimId.StartsWith("["))
            {
                trimId = id.Split('.')[0];
                var filter = BattleCardFilter.Parse(trimId);
                all = filter.FilterCurrentCardList(all);
                trimId = id.TrimStart(trimId);
            }

            trimId = trimId.TrimStart(".");
            return all.GetIdentifier(trimId, unit, defaultReturn);
        }
        return GetIdentifier(id, defaultReturn);
    }

    public void SetIdentifier(string id, float value)
    {
        var trimId = string.Empty;

        if (id.TryTrimStart("source.", out trimId))
            source?.SetIdentifier(trimId, value);

        if (id.TryTrimStart("target.", out id))
        {
            var all = invokeTarget;
            trimId = id;
            if (trimId.StartsWith("["))
            {
                trimId = id.Split('.')[0];
                var filter = BattleCardFilter.Parse(trimId);
                all = filter.FilterCurrentCardList(all);
                trimId = id.TrimStart(trimId);
            }

            trimId = trimId.TrimStart(".");
            all.ForEach(x => x.SetIdentifier(trimId, value));
        }
        /*else if (id.TryTrimStart("target.", out trimId))
        {
            var all = invokeTarget;
            var me = all.Where(x => Battle.CurrentState.GetBelongUnit(x).id == invokeUnit.id).ToList();
            var op = all.Where(x => Battle.CurrentState.GetBelongUnit(x).id == Battle.CurrentState.GetRhsUnitById(invokeUnit.id).id).ToList();

            var prefix = trimId.Split('.')[0];
            var trimPrefix = string.Empty;
            var cards = all;

            if (prefix.TryTrimStart("me", out trimPrefix))
                cards = me;
            else if (prefix.TryTrimStart("op", out trimPrefix))
                cards = op;
            else
                trimPrefix = prefix.TrimStart("all");

            if (trimPrefix.StartsWith("["))
            {
                var filter = BattleCardFilter.Parse(trimPrefix);
                cards = cards.Where(filter.FilterWithCurrentCard).ToList();
            }

            trimId = trimId.TrimStart(prefix).TrimStart('.');
            cards.ForEach(x => x.SetIdentifier(trimId, value));
        }*/

        outputOptionDict.Set(id, value.ToString());
    }

    public EffectTargetInfo GetEffectTargetInfo(BattleState state) {
        return EffectTargetInfo.Parse(this, state);
    }

    public Dictionary<string, string> Parse(EffectAbility action, int[] data) {
        return EffectParseHandler.GetParseFunc(action).Invoke(data);
    }

    public bool Condition(BattleState state) {
        foreach(var condOption in condDictList)
        {
            if (!ParseAndOperate(condOption, state))
                return false;
        }
        return true;
    }

    public bool ParseAndOperate(ICondition cond, BattleState state)
    {
        var lhs = Parser.ParseEffectExpression(cond.lhs, this, state);
        var rhs = Parser.ParseEffectExpression(cond.rhs, this, state);
        var result = Operator.Condition(cond.op, lhs, rhs);
        if (!result)
        {
            Debug.Log($"{this.name}执行失败！\n左侧元素{cond.lhs}返回结果为{lhs}，右侧元素{rhs}返回结果为{rhs}");
        }
        return result;
    }
    
    public bool Apply(BattleState state = null) {
        
        state.currentEffect = this;

        var result = true;
        var repeat = Parser.ParseEffectExpression(abilityOptionDict.Get("repeat", "1"), this, state);
        var abilityFunc = EffectAbilityHandler.GetAbilityFunc(ability);
        outputOptionDict = new Dictionary<string, string>(abilityOptionDict);

        for (int i = 0; i < repeat; i++) {
            if (!EffectAbilityHandler.Preprocess(this, state)) {
                result = false;
                continue;
            }
            foreach (var keyword in keywords)
            {
                EffectAbilityHandler.EnqueueEffect("on_this_" + keyword.ToString().ToLower(), new List<BattleCard>() { source }, state);
                EffectAbilityHandler.OnPhaseChange("on_" + keyword.ToString().ToLower(), state);
            }
            result &= abilityFunc.Invoke(this, state);
        }

        if (result)
            EffectAbilityHandler.Postprocess(this, state);
        
        return result;
    }

    public bool ImmediateApply(BattleState state)
    {
        var currentEffect = state.currentEffect;
        var result = Apply(state);
        state.currentEffect = currentEffect;
        return result;
    }

    public void SetInvokeTarget(BattleState state) {
        var rhsUnit = state.GetRhsUnitById(invokeUnit.id);
        var info = GetEffectTargetInfo(state);

        if (info.unit == "none")
            return;

        if (info.unit == "self") {
            invokeTarget = new List<BattleCard>() { source };
            return;
        } 

        if (info.unit.TryTrimStart("sourceEffect.", out var trimUnit)) {
            invokeTarget = info.GetSourceEffectTarget(this, state);
            return;
        }
        
        invokeTarget = info.GetTarget(this, state);

    }

    public Func<bool> GetCheckCondition(string checkTiming, BattleState state) {
        return checkTiming switch {
            "turn_end"      => () => state.currentEffect.ability == EffectAbility.TurnEnd,
            "me_turn_end"   => () => (state.currentEffect.ability == EffectAbility.TurnEnd) && (invokeUnit.isDone),
            "op_turn_end"   => () => (state.currentEffect.ability == EffectAbility.TurnEnd) && (state.GetRhsUnitById(invokeUnit.id).isDone),
            _               => null,
        };
    }
    /*public void OnTurnStart()
    {
        TurnUseTimes = 0;
    }*/
}