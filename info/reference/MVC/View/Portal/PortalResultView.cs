using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalResultView : IMonoBehaviour
{
    [SerializeField] List<CardView> cardViews;

    public void SetPortalResult(Card[] cards) {
        for (int i = 0; i < cardViews.Count; i++) {
            var card = i < cards.Length ? cards[i] : null;
            cardViews[i].SetCard(card);
        }
    }

    public void SetPortalResultCardCount(int[] counts) {
        for (int i = 0; i < cardViews.Count; i++) {
            int count = ((counts != null) && (i < counts.Length)) ? counts[i] : -1;
            cardViews[i].SetCount(count);
        }
    }

}
