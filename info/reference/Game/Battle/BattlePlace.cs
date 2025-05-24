using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattlePlace
{
    public BattlePlaceId PlaceId => GetPlaceId();
    public BattleUnit unit;
    public List<BattleCard> cards = new List<BattleCard>();
    public int MaxCount;
    public int Count => cards.Count;
    public int AvailableCount => MaxCount - Count;
    public bool IsFull => Count >= MaxCount;
    public Dictionary<string, float> options = new Dictionary<string, float>();

    public BattlePlace() {}
    public BattlePlace(List<BattleCard> battleCards) {
        cards = battleCards;
        MaxCount = cards.Count;
    }

    public BattlePlace(BattlePlace rhs) {
        cards = rhs.cards.Select(x => (x == null) ? null : new BattleCard(x)).ToList();
        MaxCount = rhs.MaxCount;
        options = new Dictionary<string, float>(rhs.options);
    }

    public virtual BattlePlaceId GetPlaceId() {
        return BattlePlaceId.None;
    }

    public virtual bool Contains(BattleCard battleCard) {
        return cards.Contains(battleCard);
    }

    public virtual float GetIdentifier(string id, float defaultReturn = float.MinValue) {
        var resultCards = cards;

        if (id.TryTrimStart("list", out id))
        {
            var trimId = id;
            if (trimId.StartsWith("["))
            {
                trimId = id.Split('.')[0];
                var filter = BattleCardFilter.Parse(trimId);
                resultCards = filter.FilterCurrentCardList(cards);
                trimId = id.TrimStart(trimId);
            }
            trimId = trimId.TrimStart('.');
            return resultCards.GetIdentifier(trimId, unit, defaultReturn);
        }

        /*if (trimId.TryTrimStart("first.", out trimId))
            return filterCards.FirstOrDefault()?.GetIdentifier(trimId) ?? 0;*/

        return GetPlaceIdentifier(id, defaultReturn);
    }

    public virtual float GetPlaceIdentifier(string id, float defaultReturn)
    {
        return id switch
        {
            "count" => Count,
            "maxCount" => MaxCount,
            "availableCount" => AvailableCount,
            "isFull" => IsFull ? 1 : 0,
            _ => options.Get(id, defaultReturn),
        };
    }

    public virtual void SetIdentifier(string id, float num) {
        var resultCards = cards;

        if (id.TryTrimStart("list", out id))
        {
            var trimId = id;
            if (trimId.StartsWith("["))
            {
                trimId = id.Split('.')[0];
                var filter = BattleCardFilter.Parse(trimId);
                resultCards = filter.FilterCurrentCardList(cards);
                trimId = id.TrimStart(trimId);
            }
            trimId = trimId.TrimStart('.');
            resultCards.ForEach(x => x.SetIdentifier(trimId, num));
        }
        options.Set(id, num);
    }

    public virtual void AddIdentifier(string id, float num) {
        SetIdentifier(id, GetIdentifier(id) + num);
    }

    public virtual void OnTurnStart()
    {
        cards.ForEach(x => x.OnTurnStart(PlaceId));
    }
}

