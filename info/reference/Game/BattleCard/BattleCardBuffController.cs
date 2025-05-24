using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleCardBuffController
{
    private int costBuff, atkBuff, hpBuff, damage;
    public List<Buff> buffs;
    public List<KeyValuePair<Func<bool>, CardStatus>> tmpBuff = new List<KeyValuePair<Func<bool>, CardStatus>>();
    public Dictionary<string, float> options = new Dictionary<string, float>();

    //TODO 改为缓存
    public List<CardTrait> TraitBuff => buffs.SelectMany(buff => buff.traits).Distinct().ToList();
    public List<CardKeyword> KeywordBuff => buffs.SelectMany(buff => buff.keywords).Distinct().ToList();
    public List<Effect> EffectBuff => buffs.SelectMany(buff => buff.effects).ToList();


    public int CostBuff => costBuff + buffs.Sum(x => x.cost);
    public int AtkBuff => atkBuff + buffs.Sum(x => x.atk);
    public int HpBuff => hpBuff + buffs.Sum(x => x.hp);
    public int Damage {
        get => damage;
        set => damage = value;
    }

    public BattleCardBuffController() {
        costBuff = atkBuff = hpBuff = damage = 0;
        buffs = new List<Buff>();
        tmpBuff = new List<KeyValuePair<Func<bool>, CardStatus>>();
        options = new Dictionary<string, float>();
    }

    public BattleCardBuffController(BattleCardBuffController rhs) {
        costBuff = rhs.costBuff;
        atkBuff = rhs.atkBuff;
        hpBuff = rhs.hpBuff;
        damage = rhs.damage;
        buffs = rhs.buffs;
        tmpBuff = rhs.tmpBuff.ToList();
        options = new Dictionary<string, float>(rhs.options);
    }

    public float GetIdentifier(string id) {
        return id switch {
            "isBuffed" => ((AtkBuff > 0) || (HpBuff > 0)) ? 1 : 0,
            _ => options.Get(id, 0),
        };
    }

    public void RemoveUntilEffect() {
        tmpBuff.RemoveAll(x => x.Key.Invoke());
    }

    public int TakeDamage(int dmg) {
        damage += dmg;
        return dmg;
    }

    public int TakeHeal(int heal) {
        var realHeal = Mathf.Min(heal, damage);
        damage -= realHeal;
        return realHeal;
    }

    /*public void TakeBuff(CardStatus status, Func<bool> untilCondition) {
        if (untilCondition == null) {
            costBuff += status.cost;
            atkBuff += status.atk;
            hpBuff += status.hp;
            return;
        }
        tmpBuff.Add(new KeyValuePair<Func<bool>, CardStatus>(untilCondition, status));
    }*/

    public void TakeBuff(CardStatus status, string untilCondition)
    {
        //TODO增加来源显示
        var tuple = GetCheckCondition(untilCondition);
        var buff = new Buff(status.cost, status.atk, status.hp, tuple.timing, tuple.condition)
        {
        };
        buffs.Add(buff);
    }

    public void AddKeyword(List<CardKeyword> keywords, string untilCondition)
    {
        var tuple = GetCheckCondition(untilCondition);
        var buff = new Buff(0, 0, 0, tuple.timing, tuple.condition)
        {
            keywords = keywords
        };
        buffs.Add(buff);
    }

    public void AddEffect(List<Effect> effects, string untilCondition)
    {
        var tuple = GetCheckCondition(untilCondition);
        var buff = new Buff(0, 0, 0, tuple.timing, tuple.condition)
        {
            effects = effects
        };
        buffs.Add(buff);
    }

    public void OnTurnStart()
    {
        buffs.ForEach(x => x.OnTurnStart());
    }

    public void ClearCostBuff() {
        costBuff = 0;
        buffs.ForEach(x => x.cost = 0);
        buffs.RemoveAll(x => x.IsEmpty());
        tmpBuff.RemoveAll(x => x.Value.cost != 0);
    }

    public (string timing, string condition) GetCheckCondition(string untilCondition)
    {
        return untilCondition switch
        {
            "turn_end" => ("on_turn_end", "none"),
            "me_turn_end" => ("on_turn_end", "me.isMyTurn=1"),
            "op_turn_end" => ("on_turn_end", "me.isMyTurn=0"),
            _ => ("none", "none"),
        };
    }

}
