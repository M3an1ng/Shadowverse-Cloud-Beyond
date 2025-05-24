using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckBuilderManager : Manager<DeckBuilderManager>
{
    [SerializeField] private DeckBuilderController deckBuilderController;

    protected override void Start() {
        AudioSystem.instance.PlayMusic(AudioResources.Main);
        
        deckBuilderController.SetInitDeck(Player.currentDeck);
    }
}
