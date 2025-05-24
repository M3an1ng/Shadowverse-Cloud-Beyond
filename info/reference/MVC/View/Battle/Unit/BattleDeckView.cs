using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleDeckView : BattleBaseView
{
    [SerializeField] private List<Image> deckImages;

    public void SetDeck(BattleDeck deck) {
        deckImages[0]?.SetSprite(SpriteResources.DeathCard);
        deckImages[1].gameObject.SetActive(deck.Count > 0);
        
        for (int i = 2; i < deckImages.Count; i++) {
            deckImages[i].gameObject.SetActive((i - 1) < deck.Count / (deck.MaxCount / 5));
        }
    }
}
