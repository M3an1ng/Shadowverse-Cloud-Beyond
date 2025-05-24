using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Buff : IIdentifyHandler
{
    public const int DATA_COL = 10;

    public static Buff Get(int id)
    {
        var buff = DatabaseManager.instance.GetBuffInfo(id);
        return (buff == null) ? null : new Buff(buff);
    }

    public int id;
    public int Id => id;

    public string name;
    public string Name => name;

    public int cost, atk, hp;

    public BattleCard source = null;

    public Effect sourceEffect = null;

    public string timing;

    public int StayTurn
    {
        get => (int)GetIdentifier("stayTurn", 0);
        set => SetIdentifier("stayTurn", value);
    }

    public List<CardTrait> traits = new List<CardTrait>();
    public List<CardKeyword> keywords = new List<CardKeyword>();
    public List<int> effectIds = new List<int>();
    public List<Effect> effects = new List<Effect>();
    public Dictionary<string, string> options = new Dictionary<string, string>();
    //TODO 去掉condition 改为自带能力
    //TODO 持久Buff需求
    public List<ICondition> condDictList { get; private set; } = new List<ICondition>();
    public string description;

    public Buff(string[] _data, int startIndex)
    {
        string[] _slicedData = new string[DATA_COL];
        Array.Copy(_data, startIndex, _slicedData, 0, _slicedData.Length);

        source = null;
        id = int.Parse(_slicedData[0]);
        name = _slicedData[1];
        var status = _slicedData[2].ToIntList('/');
        cost = status[0];
        atk = status[1];
        hp = status[2];

        traits = _slicedData[3].ToIntList('/').Select(x => (CardTrait)x).ToList();
        keywords = _slicedData[4].ToIntList('/').Select(x => (CardKeyword)x).ToList();

        options.ParseOptions(_slicedData[5]);

        timing = _slicedData[6];
        condDictList = condDictList.ParseCondition(_slicedData[7]);
        effectIds = _slicedData[8].ToIntList('/');
        description = _slicedData[9].GetDescription();

        SetEffects(effectIds);
    }

    public Buff(int _cost, int _atk, int _hp, string _timing,
    string _condition_option, string _name = "default")
    {
        source = null;
        cost = _cost;
        atk = _atk;
        hp = _hp;
        name = _name;
        timing = _timing;
        condDictList = condDictList.ParseCondition(_condition_option);
    }

    public Buff(Buff rhs)
    {
        source = null;

        id = rhs.id;
        name = rhs.name;
        cost = rhs.cost;
        atk = rhs.atk;
        hp = rhs.hp;
        traits = rhs.traits.ToList();
        keywords = rhs.keywords.ToList();
        timing = rhs.timing;
        condDictList = rhs.condDictList.Select(x => new ICondition(x.op, x.lhs, x.rhs)).ToList();
        effectIds = new List<int>(rhs.effectIds);
        options = new Dictionary<string, string>(rhs.options);

        description = rhs.description;

        SetEffects(rhs.effectIds);
    }

    public void SetEffects(List<int> effectIds, Func<int, Effect> effectFunc = null)
    {
        effectFunc ??= (x => Effect.Get(x));
        effects = effectIds.Select(effectFunc).Where(x => x != null).ToList();
    }

    public void ClearEffects(string timing = "none", int id = 0, CardKeyword keyword = CardKeyword.None)
    {
        if (timing != "all" && timing != "none")
        {
            effectIds.RemoveAll(x => Effect.Get(x)?.timing == timing);
            effects.RemoveAll(x => x.timing == timing);
        }
        else if (timing == "all")
        {
            effectIds.Clear();
            effects.Clear();
            keywords.Clear();
            return;
        }

        if (id != 0)
        {
            effectIds.RemoveAll(x => x == id); 
            effects.RemoveAll(x => x.id == id);
        }

        if (keyword != CardKeyword.None)
        {
            keywords.RemoveAll(x => x == keyword);
        }
    }

    public bool TryGetIdenfier(string id, out float value) {
        value = GetIdentifier(id);
        return true;
    }
    public float GetIdentifier(string id, float defaultReturn = float.MinValue)
    {
        string trimId;
        if (id.TryTrimStart("source.", out trimId))
            source.GetIdentifier(trimId);

        if (id.TryTrimStart("sourceEffect.", out trimId))
            return sourceEffect.GetIdentifier(trimId);

        return id switch{
            "cost" => cost,
            "atk" => atk,
            "hp" => hp,
            _ => options.TryGetValue(id, out var temp) && float.TryParse(temp, out var value) ? value : defaultReturn,
        };
    }
    public void SetIdentifier(string id, float value)
    {
        string trimId;
        if (id.TryTrimStart("source.", out trimId))
            source?.SetIdentifier(trimId, value);

        if (id.TryTrimStart("sourceEffect.", out trimId))
            sourceEffect?.SetIdentifier(trimId, value);

        options.Set(id, value.ToString());
    }

    public bool Condition(BattleState state)
    {
        foreach (var condOption in condDictList)
        {
            if (!ParseAndOperate(condOption, state))
                return false;
        }
        return true;
    }

    public bool ParseAndOperate(ICondition cond, BattleState state)
    {
        var lhs = Parser.ParseBuffExpression(cond.lhs, this, state);
        var rhs = Parser.ParseBuffExpression(cond.rhs, this, state);
        var result = Operator.Condition(cond.op, lhs, rhs);
        if (!result)
        {
            Debug.Log($"{this.name}执行失败！\n左侧元素{cond.lhs}返回结果为{lhs}，右侧元素{rhs}返回结果为{rhs}");
        }
        return result;
    }

    public void OnTurnStart()
    {
        StayTurn += 1;
    }

    public bool IsEmpty()
    {
        bool isNumberEmpty = cost == 0 && atk == 0 && hp == 0;

        bool isListsEmpty = !traits.Any() && !keywords.Any() && !effectIds.Any() && !effects.Any();

        return isNumberEmpty && isListsEmpty;
    }
}
