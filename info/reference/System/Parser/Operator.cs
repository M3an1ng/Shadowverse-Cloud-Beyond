using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class Operator {
    public static Dictionary<string, Func<float, float, bool>> condDict { get; } = new Dictionary<string, Func<float, float, bool>>() {
        {"<", LessThan},
        {">", GreaterThan},
        {"=", Equal},
        {"<=", LessThanOrEqual},
        {">=", GreaterThanOrEqual},
        {"!=", NotEqual},
        {"!", NotEqual},
    };

    public static List<string> sortedCondDict = Operator.condDict.Keys.OrderByDescending(op => op.Length).ToList();

    public static bool Condition(string op, float lhs, float rhs) {
        return condDict.ContainsKey(op) ? condDict.Get(op).Invoke(lhs, rhs) : false;
    }

    public static bool Condition(string op, float current, float max, KeyValuePair<DataType, object> data) {
        var type = data.Key;
        var value = data.Value;

        switch (type) {
            default:
                return false;
            case DataType.Int:
                return Condition(op, current, (int)value);
            case DataType.Float:
                return Condition(op, current, (float)value);
            case DataType.Fraction:
                var frac = (KeyValuePair<int, int>)value;
                return Condition(op, current, max * frac.Key / frac.Value);
        }
    }

    public static bool LessThan(float lhs, float rhs) {
        return lhs < rhs;
    }

    public static bool GreaterThan(float lhs, float rhs) {
        return lhs > rhs;
    }

    public static bool Equal(float lhs, float rhs) {
        return lhs == rhs;
    }


    public static bool LessThanOrEqual(float lhs, float rhs) {
        return LessThan(lhs, rhs) || Equal(lhs, rhs);
    }

    public static bool GreaterThanOrEqual(float lhs, float rhs) {
        return GreaterThan(lhs, rhs) || Equal(lhs, rhs);
    }


    public static bool NotEqual(float lhs, float rhs) {
        return lhs != rhs;
    }

    public static Dictionary<string, Func<float, float, float>> opDict { get; } = new Dictionary<string, Func<float, float, float>>() {
        {"+", Add},  {"-", Sub},  {"*", Mult},  {"/", Div},  {"^", Pow},  {"%", Mod},  
        {"><", Mathf.Min},     {"<>", Mathf.Max},      {"[SET]", Set},  { "~", Random.Range }
    };

    public static float Operate(string op, float lhs, float rhs) {
        return opDict.ContainsKey(op) ? opDict.Get(op).Invoke(lhs, rhs) : 0;
    }

    public static float Add(float lhs, float rhs) {
        return lhs + rhs;
    }
    public static float Sub(float lhs, float rhs) {
        return lhs - rhs;
    }
    public static float Mult(float lhs, float rhs) {
        return lhs * rhs;
    }
    public static float Div(float lhs, float rhs) {
        return lhs / rhs;
    }
    public static float Pow(float lhs, float rhs) {
        return Mathf.Pow(lhs, rhs);
    }
    public static float Mod(float lhs, float rhs) {
        return (int)lhs % (int)rhs;
    }
    public static float Set(float lhs, float rhs) {
        return rhs;
    }

    public static Dictionary<string, Func<bool, bool, bool>> logDict { get; } = new Dictionary<string, Func<bool, bool, bool>>() {
        { "&", And}, { "|", Or}, {"!", Not}
    };

    public static bool Logic(string op, bool lhs, bool rhs)
    {
        return logDict.ContainsKey(op) ? logDict.Get(op).Invoke(lhs, rhs) : false;
    }

    public static bool And(bool lhs, bool rhs)
    {
        return lhs && rhs;
    }

    public static bool Or(bool lhs, bool rhs)
    {
        return lhs || rhs;
    }

    public static bool Not(bool lhs, bool rhs)
    {
        return !rhs;
    }

}

public enum DataType {
    Null,
    Text,
    Condition,
    Operator,
    Int,
    Float,
    Fraction,
}

public class ICondition 
{
    public string op, lhs, rhs;
    public ICondition(string op, string lhs, string rhs) {
        this.op = op;
        this.lhs = lhs;
        this.rhs = rhs;
    }
}

public enum ModifyOption {
    None,
    Clear,
    Set,
    Add,
    Remove,
}