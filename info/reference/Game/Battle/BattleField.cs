using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleField : BattlePlace
{
    public BattleField() {
        MaxCount = 5;
    }
    public BattleField(BattleField rhs) : base(rhs) {}

    public List<int> GetAttackableTargetIndex(BattleCard attackSource, BattleUnit sourceUnit) {
        var result = cards.Where(x => (x.CurrentCard.IsFollower()) || (x.CurrentCard.Type == CardType.Leader));
        var attackable = result.Where(x => (!x.IsKeywordAvailable(CardKeyword.Ambush)) && (!x.IsKeywordAvailable(CardKeyword.Pressure)));
        var ward = attackable.Where(x => x.IsKeywordAvailable(CardKeyword.Ward));

        result = ward.Any() ? ward : attackable;
        var index = result.Select(x => cards.IndexOf(x)).ToList();

        if ((attackSource.IsLeaderAttackable(sourceUnit)) && (!ward.Any()))
            index.Add(-1);

        return index;
    }

    public override BattlePlaceId GetPlaceId()
    {
        return BattlePlaceId.Field;
    }
}
public enum BattleCardFieldReason
{
    None = 0,
    Deck = 1,
    Hand = 2,
    Grave = 3,
    Token = 4,
}

