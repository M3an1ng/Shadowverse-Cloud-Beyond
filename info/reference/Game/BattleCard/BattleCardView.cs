using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleCardView : IMonoBehaviour
{
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] private BattleCardFollowerView normalView, evolveView;
    [SerializeField] private BattleCardAmuletView amuletView;
    [SerializeField] private BattleCardEffectView effectView;

    private BattleCard currentCard;

    public void SetBattleCard(BattleCard card) {
        currentCard = card;
        gameObject.SetActive(card != null);
        effectView?.SetBattleCard(card);
        if (card == null)
            return;

        normalView?.gameObject.SetActive(card.CurrentCard.Type == CardType.Follower);
        evolveView?.gameObject.SetActive(card.CurrentCard.Type == CardType.Evolved);
        amuletView?.gameObject.SetActive(card.CurrentCard.Type == CardType.Amulet);

        normalView?.SetBattleCard(card);
        evolveView?.SetBattleCard(card);
        amuletView?.SetBattleCard(card);
    }

    public void SetTransparent(bool transparent) {
        if (!transparent) {
            SetBattleCard(currentCard);
            return;
        }
        
        normalView?.gameObject.SetActive(!transparent);
        evolveView?.gameObject.SetActive(!transparent);
        amuletView?.gameObject.SetActive(!transparent);
        effectView?.SetBattleCard(null);
    }

    public void SetOutlineColor(Color color) {
        normalView?.SetOutlineColor(color);
        evolveView?.SetOutlineColor(color);
        amuletView?.SetOutlineColor(color);
    }

    public void SetTargeting(bool isTargeting) {
        effectView?.SetTargeting(isTargeting);
    }

    public void SetDamage(int damage, Color color, Action callback) {
        effectView?.SetDamage(damage, color, callback);
    }

    public void SetLeaveField(string type, Action callback) {
        SetTransparent(true);
        effectView?.SetLeaveField(type, callback);
    }
}
