using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleHand : BattlePlace
{
    public BattleHand() {
        MaxCount = 9;
    }
    public BattleHand(BattleHand rhs) {
        MaxCount = rhs.MaxCount;
        cards = rhs.cards.Select(x => (x == null) ? null : new BattleCard(x)).ToList();
    }

    public override BattlePlaceId GetPlaceId()
    {
        return BattlePlaceId.Hand;
    }
}
