using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class Identifier {

    public static float GetIdentifier(string id, Effect effect, BattleState state) {
        var trimId = string.Empty;
        var lhsUnit = effect.invokeUnit;
        var rhsUnit = state.GetRhsUnitById(lhsUnit.id);

        if (id.TryTrimStart("enhance", out trimId)) {
            var enhance = Parser.ParseEffectExpression(trimId.TrimParentheses(), effect, state);
            return lhsUnit.leader.PP >= enhance ? 1 : 0;
        }

        if (id.TryTrimStart("earth", out trimId)) {
            var earth = Parser.ParseEffectExpression(trimId.TrimParentheses(), effect, state);
            var filter = BattleCardFilter.Parse("[trait:3][type:3]");
            return (lhsUnit.field.cards.Count(filter.FilterWithCurrentCard) >= earth) ? 1 : 0;
        }

        if (id.TryTrimStart("necromance", out trimId)) {
            var necromance = Parser.ParseEffectExpression(trimId.TrimParentheses(), effect, state);
            return (lhsUnit.grave.GraveCount >= necromance) ? 1 : 0;
        }
        
        if (id.TryTrimStart("sourceEffect.", out trimId))
            return effect.sourceEffect?.GetSourceEffectIdentifier(trimId, lhsUnit, 0) ?? 0;

        if (id.TryTrimStart("effect.", out trimId))
            return effect.GetIdentifier(trimId, 0);

        if (id.TryTrimStart("sourceBuff.", out trimId))
            return effect.sourceBuff?.GetIdentifier(trimId, 0) ?? 0;

        if (id.TryTrimStart("me.", out trimId))
            return lhsUnit.GetIdentifier(trimId, 0);

        if (id.TryTrimStart("op.", out trimId))
            return rhsUnit.GetIdentifier(trimId, 0);

        return GetNumIdentifier(id);
    }

    public static float GetIdentifier(string id, Buff buff, BattleState state)
    {
        var trimId = string.Empty;
        var lhsUnit = state.GetBelongUnit(buff.source);
        var rhsUnit = state.GetRhsUnitById(lhsUnit.id);

        if (id.TryTrimStart("sourceEffect.", out trimId))
            return buff.sourceEffect.GetSourceEffectIdentifier(trimId, lhsUnit, 0);

        if (id.TryTrimStart("buff.", out trimId))
            return buff.GetIdentifier(trimId, 0);

        if (id.TryTrimStart("me.", out trimId))
            return lhsUnit.GetIdentifier(trimId, 0);

        if (id.TryTrimStart("op.", out trimId))
            return rhsUnit.GetIdentifier(trimId, 0);

        return GetNumIdentifier(id);
    }

    public static float GetNumIdentifier(string id) {
        string trimId;
        float num = 0;

        if (id.TryTrimStart("random", out trimId)) {
            if (trimId == string.Empty)
                return Random.Range(0, 100);

            int startIndex = trimId.IndexOf('[');
            int middleIndex = trimId.IndexOf('~');
            int endIndex = trimId.IndexOf(']');
            string startExpr = trimId.Substring(startIndex + 1, middleIndex - startIndex - 1);
            string endExpr = trimId.Substring(middleIndex + 1, endIndex - middleIndex - 1);
            int startRange = int.Parse(startExpr);
            int endRange = int.Parse(endExpr);

            return Random.Range(startRange, endRange + 1);
        }

        return float.TryParse(id, out num) ? num : 0;
    }

    public static float GetIdentifier(this BattleCard card, string id, BattleUnit unit, float defaultReturn = float.MinValue)
    {
        string trimId;
        var resultCard = card.CurrentCard;
        if (id.TryTrimStart("filter", out trimId))
        {
            if (trimId.StartsWith("["))
                return BattleCardFilter.Parse(trimId).FilterWithCurrentCard(card) ? 1 : 0;
            else
                return card.GetIdentifier(trimId.TrimStart("."), defaultReturn);
        }

        if (id.TryTrimStart("base.", out trimId))
            resultCard = card.baseCard;
        if (id.TryTrimStart("evolve.", out trimId))
            resultCard = card.evolveCard;
        if (id.TryTrimStart("origin.", out trimId))
            resultCard = card.OriginalCard;
        if (id.TryTrimStart("current.", out trimId))
            resultCard = card.CurrentCard;

        return id switch
        {
            "isMe" => (unit.GetBelongPlace(card) != null) ? 1 : 0,
            "isOp" => (unit.GetBelongPlace(card) != null) ? 0 : 1,
            "where" => (float)(unit.GetBelongPlace(card)?.PlaceId ?? BattlePlaceId.None),
            "isAttacked" => card.IsAttackFinished ? 1 : 0,
            _ => card.TryGetIdenfier(id, out var value) ? value : resultCard.GetIdentifier(trimId, defaultReturn),
        };
    }

    public static float GetIdentifier(this List<BattleCard> cards, string id, BattleUnit unit, float defaultReturn = float.MinValue)
    {
        if (id.TryTrimStart("list", out id))
        {
            var trimId = id;
            if (trimId.StartsWith("["))
            {
                trimId = id.Split('.')[0];
                var filter = BattleCardFilter.Parse(trimId);
                cards = filter.FilterCurrentCardList(cards);
                trimId = id.TrimStart(trimId);
            }
            trimId =  trimId.TrimStart('.');
            return cards.GetIdentifier(trimId, unit, defaultReturn);
        }

        if (id.TryTrimStart("card", out id))
        {
            var trimId = id;
            if (trimId.TryTrimParentheses(out var indexString))
                trimId = trimId.TrimStart("[" + indexString + "]");
            trimId = trimId.TrimStart('.');
            int.TryParse(indexString, out var index);
            return cards.ElementAtOrDefault(index)?.GetIdentifier(trimId, unit, defaultReturn) ?? -1;
        }

        return id switch
        {
            "count" => cards.Count,
            _ => defaultReturn,
        };
    }

    public static void SetIdentifier(string id, float num, Effect effect, BattleState state)
    {
        var trimId = string.Empty;
        var lhsUnit = effect.invokeUnit;
        var rhsUnit = state.GetRhsUnitById(lhsUnit.id);
        //var num = Parser.ParseEffectExpression(value, effect, state);

        if (id.TryTrimStart("sourceEffect.", out trimId))
            effect.sourceEffect?.SetIdentifier(trimId, num);

        else if (id.TryTrimStart("effect.", out trimId))
            effect.SetIdentifier(trimId, num);

        if (id.TryTrimStart("sourceBuff.", out trimId))
            effect.sourceBuff?.SetIdentifier(trimId, 0);

        else if (id.TryTrimStart("me.", out trimId))
            lhsUnit.SetIdentifier(trimId, num);

        else if (id.TryTrimStart("op.", out trimId))
            rhsUnit.SetIdentifier(trimId, num);
    }
}
