using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class BattleStartView : BattleBaseView
{
    [SerializeField] private Image background;
    [SerializeField] private BattleOrderView myOrderView, opOrderView;

    public override void Init()
    {
        base.Init();

        int id = Random.Range(1, 9);
        AudioSystem.instance.PlayMusic(AudioResources.GetThemeBattleClip(id));
        background?.SetSprite(SpriteResources.GetThemeBackgroundSprite(id));
    }

    public void ShowOrderInfo(Action callback = null) {
        gameObject.SetActive(true);
        myOrderView?.ShowOrderInfo(callback);
        opOrderView?.ShowOrderInfo(null);
    }
}
