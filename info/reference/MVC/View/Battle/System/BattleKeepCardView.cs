using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BattleKeepCardView : BattleBaseView
{
    [SerializeField] private float waitSeconds = 2;
    [SerializeField] private float leavePosY, leaveThresholdY;
    [SerializeField] private float keepPosY, keepThresholdY;
    [SerializeField] private Timer timer;
    [SerializeField] private Text orderText;
    [SerializeField] private Text indicatorText;
    [SerializeField] private IButton keepButton;
    [SerializeField] private List<CardView> cardViews;

    private Vector2[] initPos = new Vector2[3];

    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < cardViews.Count; i++) {
            int copy = i;
            cardViews[i].draggable.onBeginDragEvent.SetListener(r => OnBeginDrag(r, copy));
            cardViews[i].draggable.onEndDragEvent.SetListener(r => OnEndDrag(r, copy));
            cardViews[i].SetCallback(() => cardInfoView?.SetCard(cardViews[copy].CurrentCard));
        }
    }

    public void SetActive(bool active) {
        gameObject.SetActive(active);
    }

    public void ShowKeepInfo() {
        SetActive(true);
        orderText?.SetText(Battle.CurrentState.myUnit.IsFirstText);
        for (int i = 0; i < cardViews.Count; i++) {
            cardViews[i].SetCard(Battle.CurrentState.myUnit.hand.cards[i].CurrentCard, int.MinValue);
        }
        timer?.SetTimer(60);
        timer.onDoneEvent += OnConfirmKeep;
    }

    public void OnConfirmKeep(float leftSeconds) {
        timer.onDoneEvent -= OnConfirmKeep;
        timer.gameObject.SetActive(false);

        keepButton?.gameObject.SetActive(false);
        indicatorText?.SetText("等待對手交換完成");
        var change = Enumerable.Range(0, cardViews.Count)
            .Where(x => cardViews[x].rectTransform.anchoredPosition.y == leavePosY)
            .ToArray();

        var data = (new int[] { (int)EffectAbility.KeepCard }).Concat(change).ToArray();
        Battle.PlayerAction(data, true);
    }

    public void ShowKeepResult(List<BattleCard> newHandCards, Action callback = null) {
        for (int i = 0; i < cardViews.Count; i++) {
            var x = cardViews[i].rectTransform.anchoredPosition.x;
            cardViews[i].SetCard(newHandCards[i].CurrentCard);
            cardViews[i].rectTransform.anchoredPosition = new Vector2(x, keepPosY);
        }
        StartCoroutine(WaitForSeconds(waitSeconds, callback));
    }

    private void OnBeginDrag(RectTransform rectTransform, int index) {
        cardInfoView?.SetCard(null);
        initPos[index] = rectTransform.anchoredPosition;
    }

    private void OnEndDrag(RectTransform rectTransform, int index) {
        var x = initPos[index].x;
        if (initPos[index].y <= keepThresholdY) {
            var y = (rectTransform.anchoredPosition.y >= leaveThresholdY) ? leavePosY : initPos[index].y;
            rectTransform.anchoredPosition = new Vector2(x, y);
        } else if (initPos[index].y >= leaveThresholdY) {    
            var y = (rectTransform.anchoredPosition.y <= keepThresholdY) ? keepPosY : initPos[index].y;
            rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }

}
