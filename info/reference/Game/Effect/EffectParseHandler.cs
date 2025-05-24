using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public static class EffectParseHandler
{
    public static Func<int[], Dictionary<string, string>> GetParseFunc(EffectAbility ability) {
        return ability switch {
            EffectAbility.SetResult => SetResult,
            EffectAbility.KeepCard  => KeepCard,
            EffectAbility.Play      => Use,
            EffectAbility.Attack    => Attack,
            EffectAbility.Evolve    => Evolve,
            EffectAbility.Draw      => Draw,
            EffectAbility.Summon    => Summon,
            EffectAbility.SetPP     => SetAddValue,
            EffectAbility.SetEP     => SetAddValue,
            EffectAbility.SetGrave  => SetAddValue,
            _ => ((data) => new Dictionary<string, string>()),
        };
    }

    public static Dictionary<string, string> SetResult(int[] data) {
        var dict = new Dictionary<string, string>();
        var result = (BattleResultState)data[0] switch {
            BattleResultState.Win => "win",
            BattleResultState.Lose => "lose",
            _ => "none",
        };
        dict.Set("result", result);
        if (data.Length > 1)
            dict.Set("reason", data[1].ToString());
        
        return dict;
    }

    public static Dictionary<string, string> KeepCard(int[] data) {
        var dict = new Dictionary<string, string>();
        var str = string.Empty;

        for (int i = 0; i < data.Length; i++) {
            str += data[i].ToString() + ((i == data.Length - 1) ? string.Empty : "/");
        }
        dict.Set("change", str);
        return dict;
    }

    public static Dictionary<string, string> Use(int[] data) {
        var dict = new Dictionary<string, string>();
        dict.Set("index", data[0].ToString());
        
        if (data.Length > 1)
            dict.Set("target", data.SubArray(1).Select(x => x.ToString()).ConcatToString("/"));

        return dict;
    }

    public static Dictionary<string, string> Attack(int[] data) {
        var dict = new Dictionary<string, string>();
        dict.Set("source", data[0].ToString());
        dict.Set("target", data[1].ToString());
        return dict;
    }

    public static Dictionary<string, string> Evolve(int[] data) {
        var dict = new Dictionary<string, string>();
        dict.Set("index", data[0].ToString());

        if (data.Length > 1)
            dict.Set("target", data.SubArray(1).Select(x => x.ToString()).ConcatToString("/"));

        return dict;
    }

    public static Dictionary<string, string> Draw(int[] data) {
        var dict = new Dictionary<string, string>();
        dict.Set("count", data[0].ToString());
        return dict;
    }

    public static Dictionary<string, string> Summon(int[] data) {
        var dict = new Dictionary<string, string>();
        if (data.Length == 0)
            return dict;

        dict.Set("who", data[0].ToString());
        return dict;
    }

    public static Dictionary<string, string> SetAddValue(int[] data)
    {
        var dict = new Dictionary<string, string>();
        dict.Set("add", data[0].ToString());
        return dict;
    }
}
