using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleOrderView : BattleBaseView
{
    [SerializeField] private int id;
    [SerializeField] private float removeSeconds = 0.5f;
    [SerializeField] private float waitSeconds = 1f;
    [SerializeField] private Vector2 flipBound;
    [SerializeField] private Image background, coin;
    [SerializeField] private Image leader;
    [SerializeField] private Text order;
    [SerializeField] private Text description;
    
    public void ShowOrderInfo(Action callback) {
        var unit = (id == 0) ? Battle.CurrentState.myUnit : Battle.CurrentState.opUnit;
        gameObject.SetActive(true);
        StartCoroutine(FlipCoin(unit, callback));
    }

    private IEnumerator FlipCoin(BattleUnit unit, Action callback) {
        float currentTime = 0, finishTime = removeSeconds, percent = 0;

        leader?.SetSprite(SpriteResources.GetLeaderProfileSprite(unit.leader.CraftId));

        // Flip X.
        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            if (percent < 0.5f)
                coin.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(0, flipBound.x, percent * 2), 0);
            else
                coin.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(flipBound.x, 0, percent * 2 - 1), 0);

            currentTime += Time.deltaTime;
            yield return null;
        }

        // Flip Y.
        currentTime = 0;
        finishTime = removeSeconds;

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            if (percent < 0.5f)
                coin.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(0, flipBound.y, percent * 2));
            else
                coin.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(flipBound.y, 0, percent * 2 - 1));;

            currentTime += Time.deltaTime;
            yield return null;
        }

        // Flip XY.
        currentTime = 0;
        finishTime = removeSeconds / 2;
        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            coin.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, flipBound, percent);
            currentTime += Time.deltaTime;
            yield return null;
        }

        coin.rectTransform.anchoredPosition = flipBound;

        yield return new WaitForSeconds(removeSeconds);

        coin.gameObject.SetActive(false);
        
        order?.SetText(unit.IsFirstText);

        if (id == 0)
            description?.SetText("您為" + unit.IsFirstText);

        yield return new WaitForSeconds(waitSeconds);

        callback?.Invoke();

        StartCoroutine(RemoveBackground());
    }

    private IEnumerator RemoveBackground() {
        float currentTime = 0, finishTime = waitSeconds, percent = 0;
        var size = background.rectTransform.rect.size.y;
        var targetPos = new Vector2(0, size * (id * 2 - 1));

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            background.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, targetPos, percent);
            currentTime += Time.deltaTime;
            yield return null;
        }
        
        gameObject.SetActive(false);
    }

}
