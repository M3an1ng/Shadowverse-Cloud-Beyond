using System;
using System.Collections.Generic;
using System.Linq;

public enum StatsType
{
    Leader,
    Card
}

public class Stats : IIdentifyHandler
{
    public const int DATA_COL = 7;

    public static Stats Get(int id)
    {
        return DatabaseManager.instance.GetStatsInfo(id);
    }

    public static Stats GetByName(string name)
    {
        return DatabaseManager.instance.GetStatsInfo(name);
    }

    public int id;
    public int Id => id;

    public string name;
    public string Name => name;

    public StatsType type;

    public string duration;

    public List<int> effectIds = new List<int>();

    public List<Effect> effects = new List<Effect>();

    public int defaultValue;

    public string description;

    public Stats(string[] _data, int startIndex)
    {
        string[] _slicedData = new string[DATA_COL];
        Array.Copy(_data, startIndex, _slicedData, 0, _slicedData.Length);
        id = int.Parse(_slicedData[0]);
        name = _slicedData[1];
        type = Enum.Parse<StatsType>(_slicedData[2], true);
        duration = _slicedData[3];
        defaultValue = int.Parse(_slicedData[4]);
        effectIds = _slicedData[5].ToIntList('/');
        description = _slicedData[6].GetDescription();
    }

    public Stats(Stats rhs)
    {
        id = rhs.id;
        name = rhs.name;
        type = rhs.type;
        duration = rhs.duration;
        defaultValue = rhs.defaultValue;
        description = rhs.description;
        effectIds = new List<int>(rhs.effectIds);

        SetEffects(rhs.effectIds);
    }
    public void SetEffects(List<int> effectIds, Func<int, Effect> effectFunc = null)
    {
        effectFunc ??= (x => Effect.Get(x));
        effects = effectIds.Select(effectFunc).Where(x => x != null).ToList();
    }

    public bool TryGetIdenfier(string id, out float value) {
        value = GetIdentifier(id);
        return true;
    }

    public float GetIdentifier(string id, float defaultReturn = float.MinValue)
    {
        return defaultReturn;
    }

    public void SetIdentifier(string id, float value)
    {

    }

}
