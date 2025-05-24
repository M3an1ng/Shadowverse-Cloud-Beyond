using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardFilter
{
    public virtual string[] GetSetStringType() => new string[] { "name", "description" };
    public virtual string[] GetSetIntType() => new string[] { "format", "zone" };
    public virtual string[] GetSelectIntType() => new string[] { 
        "uid", "id", "craft", "pack", "type", "rarity", "trait", "keyword",
        "cost", "atk", "hp", "countdown" };
    public virtual string[] GetSetBoolType() => new string[] { "token" };

    public int format, zone;
    public string name, description;
    public List<int> uidList, idList, craftList, packList, typeList, rarityList, traitList, keywordList,
        costList, atkList, hpList, countdownList;
    public bool isWithToken;

    /// <summary>
    /// Create a filter to search card from database.
    /// </summary>
    /// <param name="formatId">Format id. Set -1 if don't filter format</param>
    public CardFilter(int formatId) {
        format = formatId;
        zone = -1;
        name = description = string.Empty;
        uidList = new List<int>();
        idList = new List<int>();
        craftList = new List<int>();
        packList = new List<int>();
        typeList = new List<int>();
        rarityList = new List<int>();
        traitList = new List<int>();
        keywordList = new List<int>();
        costList = new List<int>();
        atkList = new List<int>();
        hpList = new List<int>();
        countdownList = new List<int>();
        isWithToken = false;
    }

    public static CardFilter Parse(string options, Func<string, string, string> transformFunc = null) {
        var filter = new CardFilter(-1);
        if (string.IsNullOrEmpty(options) || (options == "none"))
            return filter;

        transformFunc ??= ((x, y) => y);

        while (options.TryTrimParentheses(out string trimOptions)) {
            var split = trimOptions.Split(':');
            var type = split[0];
            var items = split[1].Split('|');
            for (int i = 0; i < items.Length; i++) {
                filter.SetParam(type, transformFunc.Invoke(type, items[i]));
            }
            options = options.TrimStart("[" + trimOptions + "]");
        }
        return filter;
    }

    public virtual void SetParam(string which, string param) {
        if (GetSetBoolType().Contains(which))
            SetBool(which, bool.Parse(param));
        else if (GetSetIntType().Contains(which))
            SetInt(which, int.Parse(param));
        else if (GetSelectIntType().Contains(which))
            SelectInt(which, int.Parse(param));
        else
            SetString(which, param);
    }

    public virtual void SetString(string which, string input) {
        switch (which) {
            default:
                return;
            case "name": 
                name = input;
                return;
            case "description":
                description = input;
                return;
        }
    }

    public virtual void SetInt(string which, int item) {
        switch (which) {
            default:
                return;
            case "format":
                format = item;
                return;
            case "zone":
                zone = item;
                return;
        }
    }

    public virtual void SetBool(string which, bool item) {
        switch (which) {
            default:
                return;
            case "token":
                isWithToken = item;
                return;
        }
    }

    public virtual void SelectInt(string which, int item) {
        var list = which switch {
            "uid"       => uidList,
            "id"        => idList,
            "craft"     => craftList,
            "pack"      => packList,
            "type"      => typeList,
            "rarity"    => rarityList,
            "trait"     => traitList,
            "keyword"   => keywordList,
            "cost"      => costList,
            "atk"       => atkList,
            "hp"        => hpList,
            "countdown" => countdownList, 
            _ => null,
        };

        if (item == -1) {
            list?.Clear();
            return;
        }

        if (Card.StatusNames.Contains(which) && (item < 0)) {
            if (list.Count <= 0)
                return;

            var below = Enumerable.Range(0, list.Max() + 1);
            var above = Enumerable.Range(list.Min(), 10 - list.Min() + 1);
            var result = item switch {
                -2 => below,
                -3 => above,
                _ => list,
            };
            list.Clear();
            list.AddRange(result);
            return;
        }

        list?.Fluctuate(item);
    }

    public bool Filter(Card card) {
        return FormatFilter(card) && ZoneFilter(card)
            && UIDFilter(card) && IDFilter(card) && NameFilter(card)
            && CostFilter(card) && AtkFilter(card) && HpFilter(card) && CountdownFilter(card)
            && CraftFilter(card) && PackFilter(card) && TypeFilter(card) 
            && RarityFilter(card) && TraitFilter(card) && KeywordFilter(card) 
            && DescriptionFilter(card) && TokenFilter(card);
    }


    public virtual bool FormatFilter(Card card) => (format == -1) || card.IsFormat((GameFormat)format);
    public virtual bool ZoneFilter(Card card) => (zone == -1) || (card.ZoneId == zone) || (card.PackId == 0);
    public virtual bool NameFilter(Card card) => string.IsNullOrEmpty(name) || card.name.Contains(name);
    public virtual bool UIDFilter(Card card) => List.IsNullOrEmpty(uidList) || uidList.Contains(card.id);
    public virtual bool IDFilter(Card card) => List.IsNullOrEmpty(idList) || idList.Contains(Card.GetBaseId(card.NameId)) || idList.Contains(Card.GetEvolveId(card.NameId));
    public virtual bool CraftFilter(Card card) => List.IsNullOrEmpty(craftList) || craftList.Contains(card.CraftId);
    public virtual bool PackFilter(Card card) => List.IsNullOrEmpty(packList) || packList.Contains(card.PackId);
    public virtual bool TypeFilter(Card card) => (card.Type != CardType.Leader) && (card.Type != CardType.Evolved) && (List.IsNullOrEmpty(typeList) || typeList.Contains(card.TypeId));
    public virtual bool RarityFilter(Card card) => List.IsNullOrEmpty(rarityList) || rarityList.Contains(card.RarityId);
    public virtual bool TraitFilter(Card card) => List.IsNullOrEmpty(traitList) || card.traits.Contains(CardTrait.All) || traitList.Select(x => (CardTrait)x).Intersect(card.traits).Any();
    public virtual bool KeywordFilter(Card card) {
        if (List.IsNullOrEmpty(keywordList)) 
            return true;

        var keywords = keywordList.Select(x => (CardKeyword)x).ToList();
        var allKeywords = card.BaseCard.keywords.Concat(card.BaseCard.effects.SelectMany(effect => effect.keywords));
        var evolveCard = card.EvolveCard;
        if (evolveCard != null)
            allKeywords = allKeywords.Concat(evolveCard.keywords).Concat(evolveCard.effects.SelectMany(effect => effect.keywords));
        return keywords.Intersect(allKeywords.Distinct()).Any();
    }
    public virtual bool DescriptionFilter(Card card) => string.IsNullOrEmpty(description) || card.description.Contains(description) || card.EvolveCard.description.Contains(description);
    public virtual bool TokenFilter(Card card) => (card.Group == CardGroup.Normal) || (isWithToken && (card.Group == CardGroup.Token));

    public virtual bool CostFilter(Card card) => List.IsNullOrEmpty(costList) || costList.Contains(Mathf.Min(card.cost, 10));
    public virtual bool AtkFilter(Card card) => List.IsNullOrEmpty(atkList) || atkList.Contains(Mathf.Min(card.atk, 10));
    public virtual bool HpFilter(Card card) => List.IsNullOrEmpty(hpList) || hpList.Contains(Mathf.Min(card.hp, 10));
    public virtual bool CountdownFilter(Card card) => List.IsNullOrEmpty(countdownList) || countdownList.Contains(Mathf.Min(card.countdown, 10));
}

public class BattleCardFilter
{

    public List<KeyValuePair<string, List<string>>> filterItems = new List<KeyValuePair<string, List<string>>>();
    //public Dictionary<string, List<string>> filterItems = new Dictionary<string, List<string>>();


    //TODO 增加所属玩家判断
    public static BattleCardFilter Parse(string options, Func<string, string, string> transformFunc = null, BattleUnit unit = null)
    {
        var filter = new BattleCardFilter();
        if (string.IsNullOrEmpty(options) || (options == "none"))
            return filter;

        transformFunc ??= ((x, y) => y);

        while (options.StartsWith("[") && options.TryTrimParentheses(out string trimOptions))
        {
            var split = trimOptions.Split(':');
            var type = split[0];
            var items = split[1].Split('|');
            var itemList = new List<string>();
            for (int i = 0; i < items.Length; i++)
            {
                var itemCond = string.Empty;
                foreach (var cond in Operator.sortedCondDict)
                {
                    if (items[i].TryTrimStart(cond, out items[i]))
                    {
                        itemCond = cond;
                        break;
                    }
                }
                itemList.Add(string.Concat(itemCond, transformFunc.Invoke(type, items[i])));
            }
            filter.filterItems.Add(new KeyValuePair<string, List<string>>(type, itemList));
            //filter.filterItems.Add(type, itemList);
            options = options.TrimStart("[" + trimOptions + "]");
        }
        return filter;
    }

    public virtual Func<List<BattleCard>, string, List<BattleCard>> GetBattleCardListFilter(string type)
    {
        Func<List<BattleCard>, string, List<BattleCard>> func = type switch
        {
            "first" => FirstFilter,
            "last" => LastFilter,
            "random" => RandomFilter,
            "costOrder" => (list, item) => GeneralSortFilter(list, item, card => card.CurrentCard.cost),
            "atkOrder" => (list, item) => GeneralSortFilter(list, item, card => card.CurrentCard.atk),
            "hpOrder" => (list, item) => GeneralSortFilter(list, item, card => card.CurrentCard.hp),
            "distinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.CurrentCard.NameId),
            "costDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.CurrentCard.cost),
            "atkDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.CurrentCard.atk),
            "hpDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.CurrentCard.hp),
            _ => (list, item) => list,
        };
        return func;
    }

    public virtual Func<List<Card>, string, List<Card>> GetCardListFilter(string type)
    {
        Func<List<Card>, string, List<Card>> func = type switch
        {
            "first" => FirstFilter,
            "last" => LastFilter,
            "random" => RandomFilter,
            "costOrder" => (list, item) => GeneralSortFilter(list, item, card => card.cost),
            "atkOrder" => (list, item) => GeneralSortFilter(list, item, card => card.atk),
            "hpOrder" => (list, item) => GeneralSortFilter(list, item, card => card.hp),
            "distinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.NameId),
            "costDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.cost),
            "atkDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.atk),
            "hpDistinct" => (list, item) => GeneralDistinctFilter(list, item, card => card.hp),
            _ => (list, item) => list,
        };
        return func;
    }

    public virtual Func<BattleCard, string, bool> GetBattleCardFilter(string type)
    {
        Func<BattleCard, string, bool> GetFilter()
        {
            var cardFilter = GetCardFilter(type);
            return cardFilter != null ? (card, item) => cardFilter(card.CurrentCard, item) : null;
        }

        Func<BattleCard, string, bool> func = type switch
        {
            "isAttacked" => AttackedFilter,
            "originCost" => OriginCostFilter,
            "originAtk" => OriginAtkFilter,
            "originHp" => OriginHpFilter,
            _ => GetFilter(),
            /*_ => (card, item) => {
                var cardFilter = GetCardFilter(type);
                return cardFilter != null ? cardFilter(card.CurrentCard, item) : true;
            },*/

        };
        return func;
    }

    public virtual Func<Card, string, bool> GetCardFilter(string type)
    {
        Func<Card, string, bool> func = type switch
        {
            "format" => FormatFilter,
            "zone" => ZoneFilter,
            "name" => NameFilter,
            "description" => DescriptionFilter,
            "uid" => UIDFilter,
            "id" => IDFilter,
            "group" => GroupFilter,
            "craft" => CraftFilter,
            "pack" => PackFilter,
            "type" => TypeFilter,
            "rarity" => RarityFilter,
            "trait" => TraitFilter,
            "keyword" => KeywordFilter,
            "battleKeyword" => BattleKeywordFilter,
            "cost" => CostFilter,
            "atk" => AtkFilter,
            "hp" => HpFilter,
            "countdown" => CountdownFilter,
            _ => null,
        };
        return func;
    }

    public bool FilterWithCurrentCard(BattleCard battleCard)
    {
        if (DefaultFilter(battleCard))
        {
            foreach (var filterItem in filterItems)
            {
                var filterResult = false;
                var filter = GetBattleCardFilter(filterItem.Key);
                if (filter != null)
                {
                    foreach (var item in filterItem.Value)
                    {
                        if (filter(battleCard, item))
                        {
                            filterResult = true;
                            break;
                        }
                    }
                    if (!filterResult) return false;
                }
            }
            return true;
        }
        return false;
    }

    public bool Filter(Card card)
    {
        if (DefaultFilter(card))
        {
            foreach (var filterItem in filterItems)
            {
                var filterResult = false;
                var filter = GetCardFilter(filterItem.Key);
                if (filter != null)
                {
                    foreach (var item in filterItem.Value)
                    {
                        if (filter(card, item))
                        {
                            filterResult = true;
                            break;
                        }
                    }
                    if (!filterResult) return false;
                }
            }
            return true;
        }
        return false;
    }

    public List<Card> FilterList(List<Card> cardList)
    {
        var resultList = cardList.Where(card => Filter(card)).ToList();
        foreach (var filterItem in filterItems)
        {
            var filter = GetCardListFilter(filterItem.Key);
            if (filter != null)
            {
                foreach (var item in filterItem.Value)
                {
                    resultList = filter(resultList, item);
                }
            }
        }
        return resultList;
    }


    public List<BattleCard> FilterCurrentCardList(List<BattleCard> battleCardList)
    {
        var resultList = battleCardList.Where(card => FilterWithCurrentCard(card)).ToList();

        foreach (var filterItem in filterItems)
        {
            var filter = GetBattleCardListFilter(filterItem.Key);
            if (filter != null)
            {
                foreach (var item in filterItem.Value)
                {
                    resultList = filter(resultList, item);
                }
            }
        }
        return resultList;
    }


    public bool DefaultFilter() => true;

    public bool DefaultFilter(Card card) => true;
    public bool DefaultFilter(BattleCard battleCard) => true;

    public bool GeneralFilter(int value, string item)
    {
        string op = "=";

        foreach (var condOp in Operator.sortedCondDict)
        {
            if (item.TryTrimStart(condOp, out item))
            {
                op = condOp;
                break;
            }
        }
        if (!int.TryParse(item, out int number))
        {
            return true;
        }

        return Operator.Condition(op, value, number);
    }

    //TODO 考虑增加Effect相关筛选？
    public bool FormatFilter(Card card, string item)
    {
        if (int.TryParse(item, out var format))
            return card.IsFormat((GameFormat)format);
        else
            return true;
    }
    public bool ZoneFilter(Card card, string item)
    {
        if (int.TryParse(item, out var zone))
            return (card.ZoneId == zone) || (card.PackId == 0);
        else
            return true;
    }
    public bool NameFilter(Card card, string item) => string.IsNullOrEmpty(item) || card.name.Contains(item);
    public bool UIDFilter(Card card, string item)
    {
        if (int.TryParse(item, out var uid))
            return card.id == uid;
        else
            return true;
    }
    public bool IDFilter(Card card, string item)
    {
        return GeneralFilter(Card.GetBaseId(card.id), item) || GeneralFilter(Card.GetEvolveId(card.id), item) || GeneralFilter(Card.GetBaseId(card.NameId), item) || GeneralFilter(Card.GetEvolveId(card.NameId), item);
    }
    public bool GroupFilter(Card card, string item) => GeneralFilter(card.GroupId, item);
    public bool CraftFilter(Card card, string item) => GeneralFilter(card.CraftId, item);
    public bool PackFilter(Card card, string item) => GeneralFilter(card.PackId, item);
    public bool TypeFilter(Card card, string item) => GeneralFilter(card.TypeId, item);
    public bool RarityFilter(Card card, string item) => GeneralFilter(card.RarityId, item);
    public bool TraitFilter(Card card, string item)
    {
        var allTraits = card.tempTraits.Concat(card.BaseCard.traits);
        var evolveCard = card.EvolveCard;
        if (evolveCard != null)
        {
            allTraits = allTraits.Concat(card.EvolveCard.traits);
        }
        allTraits = allTraits.Distinct();

        if (item.TryTrimStart("!", out item))
        {
            if (int.TryParse(item, out var number))
                return !allTraits.Contains(CardTrait.All) && allTraits.All(trait => !GeneralFilter((int)trait, item));
            else
                return true;
        }
        else
        {
            if (int.TryParse(item, out var number))
                return allTraits.Contains(CardTrait.All) || allTraits.Any(trait => GeneralFilter((int)trait, item));
            else
                return true;
        }
    }
    public bool KeywordFilter(Card card, string item)
    {
        var allKeywords = card.tempKeywords.Concat(card.tempEffects.SelectMany(effect => effect.keywords)).Concat(card.BaseCard.keywords).Concat(card.BaseCard.effects.SelectMany(effect => effect.keywords));
        var evolveCard = card.EvolveCard;
        if (evolveCard != null)
            allKeywords = allKeywords.Concat(evolveCard.keywords).Concat(evolveCard.effects.SelectMany(effect => effect.keywords));
        allKeywords = allKeywords.Distinct();

        if (item.TryTrimStart("!", out item))
        {
            if (int.TryParse(item, out var number))
                return allKeywords.All(keyword => !GeneralFilter((int)keyword, item));
            else
                return true;
        }
        else
        {
            if (int.TryParse(item, out var number))
                return allKeywords.Any(keyword => GeneralFilter((int)keyword, item));
            else
                return true;
        }
    }
    public bool BattleKeywordFilter(Card card, string item)
    {
        var allKeywords = card.keywords.Concat(card.effects.SelectMany(effect => effect.keywords)).Distinct();

        if (item.TryTrimStart("!", out item))
        {
            if (int.TryParse(item, out var number))
                return allKeywords.All(keyword => !GeneralFilter((int)keyword, item));
            else
                return true;
        }
        else
        {
            if (int.TryParse(item, out var number))
                return allKeywords.Any(keyword => GeneralFilter((int)keyword, item));
            else
                return true;
        }
    }
    public bool DescriptionFilter(Card card, string item) => card.BaseCard.description.Contains(item) || (card.EvolveCard != null && card.EvolveCard.description.Contains(item));
    public bool CostFilter(Card card, string item) => GeneralFilter(card.cost, item);
    public bool AtkFilter(Card card, string item) => GeneralFilter(card.atk, item);
    public bool HpFilter(Card card, string item) => GeneralFilter(card.hp, item);
    public bool CountdownFilter(Card card, string item) => GeneralFilter(card.countdown, item);

    //BattleCard Filter
    public bool OriginCostFilter(BattleCard card, string item) => GeneralFilter(card.OriginalCard.cost, item);
    public bool OriginAtkFilter(BattleCard card, string item) => GeneralFilter(card.OriginalCard.atk, item);
    public bool OriginHpFilter(BattleCard card, string item) => GeneralFilter(card.OriginalCard.hp, item);
    public bool CustomFilter(BattleCard card, string type, string item) => GeneralFilter((int)card.GetIdentifier(type, 0), item);

    public bool AttackedFilter(BattleCard card, string item)
    {
        if (int.TryParse(item, out var isAttackFinished))
            return isAttackFinished == 1 ? card.IsAttackFinished : !card.IsAttackFinished;
        else
            return true;
    }

    //List Filter
    public List<T> GeneralSortFilter<T>(List<T> cards, string item, Func<T, int> attribute)
    {
        if (cards != null && cards.Count > 0 && int.TryParse(item, out var sort))
        {
            if (sort == 1)
                return cards.OrderBy(attribute).ToList();
            else if(sort == -1)
                return cards.OrderByDescending(attribute).ToList();
        }
        return cards;
    }

    /*public List<T> GeneralLimitFilter<T>(List<T> cards, string item, Func<T, int> attribute)
    {
        List<T> resultCards = new List<T>();
        if (cards != null && cards.Count > 0 && int.TryParse(item, out var limit))
        {
            int limitValue = (limit == 1) ? int.MinValue : int.MaxValue;
            foreach (var card in cards)
            {
                var cardValue = attribute(card);
                if ((limit == 1 && cardValue > limitValue) || (limit == -1 && cardValue < limitValue))
                {
                    limitValue = cardValue;
                    resultCards.Clear();
                    resultCards.Add(card);
                }
                else if (cardValue == limitValue)
                {
                    resultCards.Add(card);
                }
            }
        }
        return resultCards;
    }*/

    public List<T> GeneralDistinctFilter<T, TKey>(List<T> cards, string item, Func<T, TKey> attribute)
    {
        List<T> resultCards = new List<T>();
        HashSet<TKey> temp = new HashSet<TKey>();

        if (cards != null && cards.Count > 0 && int.TryParse(item, out var distinct) && distinct >= 0)
        {
            if (distinct == 0)
            {
                foreach (var card in cards)
                {
                    if (!temp.Contains(attribute(card)))
                    {
                        temp.Add(attribute(card));
                        resultCards.Add(card);
                    }
                }
            }
            else
                return resultCards.RandomAttribute(distinct, attribute);
        }
        return cards;
    }

    public List<T> RandomFilter<T>(List<T> cards, string item)
    {
        List<T> resultCards = new List<T>();
        if (cards != null && cards.Count > 0 && int.TryParse(item, out var count))
        {
            resultCards = cards.Random(count, false);
        }
        return resultCards;
    }
    public List<T> FirstFilter<T>(List<T> cards, string item)
    {
        List<T> resultCards = new List<T>();
        if (cards != null && cards.Count > 0 && int.TryParse(item, out var count))
        {
            resultCards = cards.Take(count).ToList();
        }
        return resultCards;
    }

    public List<T> LastFilter<T>(List<T> cards, string item)
    {
        List<T> resultCards = new List<T>();
        if (cards != null && cards.Count > 0 && int.TryParse(item, out var count))
        {
            resultCards = cards.TakeLast(count).ToList();
        }
        return resultCards;
    }

}
