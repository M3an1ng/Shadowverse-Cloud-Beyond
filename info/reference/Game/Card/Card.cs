using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class Card : IIdentifyHandler
{
    public const int DATA_COL = 7;
    public const int DESC_COL = 2;
    public static string[] StatusNames => new string[] { "cost", "atk", "hp" };
    public static Card Get(int id) => DatabaseManager.instance.GetCardInfo(id);
    public static GameObject Prefab => ResourceManager.instance.GetPrefab("Card");
    public static int GetBaseId(int uid) => ((CardType)(uid % 1000 / 100) != CardType.Evolved) ? uid : (uid - uid % 1000 + 100 + uid % 100);
    public static int GetEvolveId(int uid) => ((CardType)(uid % 1000 / 100) != CardType.Follower) ? 0 : (uid - uid % 1000 + 900 + uid % 100);

    public int id;
    public int Id => id;
    public int GroupId => id / (int)1e8;
    public int ZoneId => id % (int)1e8 / (int)1e7;
    public int PackId => id % (int)1e8 / (int)1e5;
    public int CraftId => id % (int)1e5 / 10000;
    public int RarityId => id % 10000 / 1000;
    public int TypeId => id % 1000 / 100;
    public int EvolveTypeId => (int)EvolveType;
    public int SerialNum => id % 100;

    public int NameId { get; protected set; }
    public int ArtworkId { get; protected set; }
    public int Limit { get; protected set; }

    public CardGroup Group => (CardGroup)GroupId;
    public CardZone Zone => (CardZone)ZoneId;
    public CardPack Pack => (CardPack)PackId;
    public CardCraft Craft => (CardCraft)CraftId;
    public CardType Type => (CardType)TypeId;
    public CardType EvolveType => Type == CardType.Evolved ? CardType.Follower : CardType.Evolved;
    public CardRarity Rarity => (CardRarity)RarityId;

    public string name;
    public string Name => name;

    public int cost, atk, hp, hpMax, countdown, charge;
    public List<CardTrait> traits = new List<CardTrait>();
    public List<CardKeyword> keywords = new List<CardKeyword>();
    public List<int> tokenIds = new List<int>();
    //public List<int> effectIds = new List<int>();
    public List<string> effectNames = new List<string>();
    public List<Effect> effects = new List<Effect>();
    public List<CardTrait> tempTraits = new List<CardTrait>();
    public List<CardKeyword> tempKeywords = new List<CardKeyword>();
    public List<Effect> tempEffects = new List<Effect>();

    public Dictionary<string, string> options = new Dictionary<string, string>();
    public string description;

    public Task<Texture2D> Artwork => Addressables.LoadAssetAsync<Texture2D>(ArtworkId.ToString()).Task;
    public Card BaseCard => Card.Get(Card.GetBaseId(id));
    public Card EvolveCard => Card.Get(Card.GetEvolveId(id));


    public static Card GetLeaderCard(int craft) => Card.Get(100000000 + craft * 10000);

    public Card(string[] _data, int startIndex) {
        string[] _slicedData = new string[DATA_COL];
        Array.Copy(_data, startIndex, _slicedData, 0, _slicedData.Length);

        id = int.Parse(_slicedData[0]);
        //Debug.Log(id);
        name = _slicedData[1];
        var status = _slicedData[2].ToIntList('/');
        cost = status[0];
        atk = status[1];
        hp = status[2];
        hpMax = hp;
        traits = _slicedData[3].ToIntList('/').Select(x => (CardTrait)x).ToList();
        keywords = _slicedData[4].ToIntList('/').Select(x => (CardKeyword)x).ToList();

        options.ParseOptions(_slicedData[5]);
        InitExtraPorperty();

        effectNames = (string.IsNullOrEmpty(_slicedData[6]) || _slicedData[6] == "none") ? new List<string>() : _slicedData[6].Split('/').ToList();
        //effectIds = _slicedData[6].ToIntList('/');
    }

    private void InitExtraPorperty() {
        int ArtId = int.Parse(options.Get("artId", id.ToString()));
        ArtworkId = (Type == CardType.Evolved) ? Card.GetBaseId(ArtId) : ArtId;

        NameId = int.Parse(options.Get("nameId", id.ToString()));
        Limit = int.Parse(options.Get("limit", "3"));
        countdown = int.Parse(options.Get("countdown", "-1"));
        charge = int.Parse(options.Get("charge", traits.Contains(CardTrait.Earth) ? "1" : "-1"));
    }

    public Card(Card rhs) {
        id = rhs.id;
        name = rhs.name;
        cost = rhs.cost;
        atk = rhs.atk;
        hp = rhs.hp;
        hpMax = rhs.hpMax;
        traits = rhs.traits.ToList();
        keywords = rhs.keywords.ToList();
        effectNames = new List<string>(rhs.effectNames);
        //effects = new List<Effect>(rhs.effects);
        //effectIds = new List<int>(rhs.effectIds);
        tokenIds = new List<int>(rhs.tokenIds);
        options = new Dictionary<string, string>(rhs.options);

        description = rhs.description;

        ArtworkId = rhs.ArtworkId;
        NameId = rhs.NameId;
        Limit = rhs.Limit;
        countdown = rhs.countdown;
        charge = rhs.charge;

        SetEffects(rhs.effects);
    }

    public void SetEffects(List<int> effectIds, Func<int, Effect> effectFunc = null) {
        effectFunc ??= (x => Effect.Get(x));
        effects = effectIds.Select(effectFunc).Where(x => x != null).ToList();
    }

    public void SetEffects(List<string> effectNames, Func<string, Effect> effectFunc = null)
    {
        effectFunc ??= (x => Effect.Get(x));
        effects = effectNames.Select(effectFunc).Where(x => x != null).ToList();
        if (Type == CardType.Leader)
            SetLeaderStats();
    }

    public void SetEffects(List<Effect> oldEffects)
    {
        var newEffects = new List<Effect>(oldEffects.Count);
        for (int i = 0; i < oldEffects.Count; i++)
        {
            newEffects.Add(new Effect(oldEffects[i]));
        }
        effects = newEffects;
    }

    public void SetLeaderStats()
    {
        foreach(var stats in DatabaseManager.instance.statsMaster)
        {
            effects.AddRange(stats.effects.Select(x => new Effect(x)).ToList());
        }
    }

    public void ClearEffects(string timing = "none", int id = 0, CardKeyword keyword = CardKeyword.None) {
        if (timing != "all" && timing != "none")
        {
            //effectIds.RemoveAll(x => Effect.Get(x)?.timing == timing);
            effects.RemoveAll(x => x.timing == timing);
        }else if(timing == "all"){
            //effectIds.Clear();
            effects.Clear();
            keywords.Clear();
            return;
        }

        if (id != 0)
        {
            //effectIds.RemoveAll(x => x == id);
            effects.RemoveAll(x => x.id == id);
        }

        if (keyword != CardKeyword.None)
        {
            keywords.RemoveAll(x => x == keyword);
        }
    }

    public void SetDescription(string[] _data, Dictionary<int, Card> cardInfoDict) {
        //name = _data[0];
        description = _data[1].GetInitDescription(out tokenIds, cardInfoDict, (Type == CardType.Leader) ? string.Empty : "（沒有卡片能力記敘）");
    }

    public float GetIdentifier(string id, float defaultReturn = float.MinValue) 
    {
        string trimId;

        if (id.TryTrimStart("filter", out trimId))
        {
            if (trimId.StartsWith("["))
                return BattleCardFilter.Parse(trimId).Filter(this) ? 1 : 0;
            else
                return GetIdentifier(trimId.TrimStart("."), defaultReturn);
        }

        if (id.TryTrimStart("trait", out trimId)) {
            while (trimId.TryTrimParentheses(out var traitId)) {
                var checkTrait = traitId.Split('|').Select(x => (CardTrait)int.Parse(x));
                Debug.Log(traitId);
                Debug.Log("checkTrait: " + checkTrait.Select(x => x.ToString()).ConcatToString(" "));
                if (!checkTrait.Any(traits.Contains))
                    return 0;

                trimId = trimId.TrimStart("[" + traitId + "]");
            }
            return 1;
        }

        if (id.TryTrimStart("keyword", out trimId)) {
            while (trimId.TryTrimParentheses(out var keywordId)) {
                var checkKeyword = keywordId.Split('|').Select(x => (CardKeyword)int.Parse(x));
                if (!checkKeyword.Any(keywords.Contains))
                    return 0;

                trimId = trimId.TrimStart("[" + keywordId + "]");
            }
            return 1;
        }

        if (id.TryTrimStart("type", out trimId)) {
            if (trimId.TryTrimParentheses(out var typeId)) {
                var checkType = typeId.Split('|').Select(x => (CardType)int.Parse(x));
                return checkType.Contains(Type) ? 1 : 0;
            }
        }

        return id switch {
            "uid" => this.id,
            "id" => NameId,
            "group" => GroupId,
            "zone" => ZoneId,
            "pack" => PackId,
            "craft" => CraftId,
            "type" => TypeId,
            "rarity" => RarityId,
            "serial" => SerialNum,

            "cost" => cost,
            "atk" => atk,
            "hp" => hp,
            "hpMax" => hpMax,
            "countdown" => countdown,
            "evolveCost" => float.Parse(options.Get("evolveCost", "1")),
            _ => options.TryGetValue(id, out var temp) && float.TryParse(temp, out var value) ? value : defaultReturn,
        };
    }

    public bool TryGetIdenfier(string id, out float value)
    {
        value = GetIdentifier(id);
        return value != float.MinValue;
    }

    public void SetIdentifier(string id, float value)
    {
        options.Set(id, value.ToString());
    }

    public bool IsFollower() => (Type == CardType.Follower) || (Type == CardType.Evolved);
    public bool IsFormat(GameFormat format) {
        int newPackId = GameManager.versionData.NewPackIds[ZoneId];
        return format switch {
            GameFormat.Rotation => (PackId == ZoneId * 100) ||
                (PackId % 100).IsWithin(newPackId - 4, newPackId),
            _ => true,
        };
    }
}

public struct CardStatus 
{
    public int cost, atk, hp;

    public CardStatus(int cost, int atk, int hp) {
        this.cost = cost;
        this.atk = atk;
        this.hp = hp;
    }
}
