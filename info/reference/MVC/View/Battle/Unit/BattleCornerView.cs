using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleCornerView : BattleBaseView
{
    [SerializeField] private Text craftText;
    [SerializeField] private Text graveText, deckText, handText;

    public void SetUnit(BattleUnit unit) {
        var leader = unit.leader;
        craftText?.SetText(leader.Craft.GetCraftName());
        graveText?.SetText(unit.grave.GraveCount.ToString());
        deckText?.SetText(unit.deck.Count.ToString());
        handText?.SetText(unit.hand.Count.ToString());
    }

}
