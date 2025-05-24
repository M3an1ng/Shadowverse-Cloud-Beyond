using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleTurnView : BattleBaseView
{
    [SerializeField] private float fadeSeconds = 0.5f;
    [SerializeField] private float waitSeconds = 2;
    [SerializeField] private Image background;
    [SerializeField] private Text whosTurnText;
    [SerializeField] private Text descriptionText;

    public void ShowTurnInfo(string whosTurn, string description, Action callback = null) {
        gameObject.SetActive(true);
        StartCoroutine(ShowTurnCoroutine(whosTurn, description, waitSeconds, callback));
    }

    public void ShowBattleResult(string whosTurn, string description, Action callback = null) {
        gameObject.SetActive(true);
        StartCoroutine(ShowTurnCoroutine(whosTurn, description, waitSeconds + 2, callback));
    }

    private IEnumerator ShowTurnCoroutine(string whosTurn, string description, float waitTime, Action callback) {
        float currentTime = 0, finishTime = fadeSeconds, percent = 0;

        background?.SetColor(Color.clear);
        whosTurnText?.SetText(string.Empty);
        descriptionText?.SetText(string.Empty);

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            background?.SetColor(Color.Lerp(Color.clear, ColorHelper.black192, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        background?.SetColor(ColorHelper.black192);
        whosTurnText?.SetText(whosTurn);
        descriptionText?.SetText(description);

        yield return new WaitForSeconds(waitTime);

        whosTurnText?.SetText(string.Empty);
        descriptionText?.SetText(string.Empty);

        currentTime = 0;
        finishTime = fadeSeconds;

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            background?.SetColor(Color.Lerp(ColorHelper.black192, Color.clear, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
        callback?.Invoke();
    }
}
