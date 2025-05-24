using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckListView : IMonoBehaviour
{
    [SerializeField] private IButton topicDeckListButton;
    [SerializeField] private List<DeckView> deckViews;

    public void ToggleTopicButton(bool isOn) {
        topicDeckListButton?.SetColor(isOn ? ColorHelper.chosen : Color.black);
    }

    public void SetDecks(Deck[] decks) {
        for (int i = 0; i < deckViews.Count; i++) {
            deckViews[i]?.SetDeck(i < decks.Length ? decks[i] : null);
        }
    }
}
