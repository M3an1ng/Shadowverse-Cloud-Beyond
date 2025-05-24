using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleCardActionController
{
    public int StayFieldTurn {
        get => (int)GetIdentifier("stayFieldTurn");
        set => SetIdentifier("stayFieldTurn", value);
    }

    public int MaxAttackTimes {
        get => (int)GetIdentifier("maxAttackTimes");
        set => SetIdentifier("maxAttackTimes", value); 
    }


    public int CurrentAttackTimes {
        get => Mathf.Clamp((int)GetIdentifier("attackTimes"), 0, MaxAttackTimes);
        set => SetIdentifier("attackTimes", Mathf.Clamp(value, 0, MaxAttackTimes));
    }

    public int FusionTimes
    {
        get => (int)GetIdentifier("fusionTimes");
        set => SetIdentifier("fusionTimes", value);
    }


    public bool IsAttackFinished => CurrentAttackTimes >= MaxAttackTimes;

    public Dictionary<string, float> options = new Dictionary<string, float>();

    public BattleCardActionController() {
        MaxAttackTimes = 1;
        CurrentAttackTimes = 0;
    }

    public BattleCardActionController(BattleCardActionController rhs) {
        options = new Dictionary<string, float>(rhs.options);
    }

    public float GetIdentifier(string id) 
    {
        return id switch {
            "isAttackFinished" => IsAttackFinished ? 1 : 0,
            _ => options.Get(id, 0),
        };
    }

    public void SetIdentifier(string id, float num) {
        switch (id) {
            default:
                options.Set(id, num);
                return;
        }
    }

    public void AddIdentifier(string id, float num) {
        SetIdentifier(id, GetIdentifier(id) + num);
    }

    public void OnTurnStartInField() {
        StayFieldTurn += 1;
        CurrentAttackTimes = 0;
        FusionTimes = 0;
    }

    public bool IsKeywordAvailable(CardKeyword keyword) {
        return GetIdentifier(keyword.GetKeywordEnglishName()) > 0;
    }
}
