using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleEvolveAnimView : BattleBaseView
{
    [SerializeField] private float particleMagnifyScale = 10;
    [SerializeField] private float particleMoveTime;
    [SerializeField] private float normalRotateTime;
    [SerializeField] private float evolveRotateTime;
    [SerializeField] private float exhibitRotateTime;

    [SerializeField] private Image evolveBackground, particleImage;
    [SerializeField] private CardView evolveCardView;

    [SerializeField] private List<BattleCardView> myCardViews;
    [SerializeField] private List<BattleCardView> opCardViews;

    public void EvolveWithEP(BattleCardPlaceInfo info, BattleCard evolvedCard, List<BattleCard> fieldCards, Action callback) {
        StartCoroutine(EvolveWithEPAnim(info, evolvedCard, fieldCards, callback));
    }

    private IEnumerator EvolveWithEPAnim(BattleCardPlaceInfo info, BattleCard evolvedCard, List<BattleCard> fieldCards, Action callback) {
        float currentTime = 0, finishTime = particleMoveTime, percent = 0;
        var startPoint = GetStartPoint(info, fieldCards.Count);
        var endPoint = new Vector3(-200, 70);
        var cardViews = (info.unitId == 0) ? myCardViews : opCardViews;

        // Anim start.
        evolveBackground.gameObject?.SetActive(true);
        cardViews[info.index].SetTransparent(true);
        particleImage.gameObject.SetActive(true);
        particleImage.rectTransform.localScale = Vector3.one;
        
        // Particle go
        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            evolveBackground?.SetColor(Color.Lerp(Color.clear, ColorHelper.black192, percent));
            particleImage.rectTransform.anchoredPosition = Vector3.Lerp(startPoint, endPoint, percent);
            particleImage.SetColor(Color.Lerp(ColorHelper.gray192, Color.black, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        evolveBackground?.SetColor(ColorHelper.black192);
        particleImage.rectTransform.anchoredPosition = endPoint;
        particleImage.rectTransform.localScale = Vector3.one * particleMagnifyScale;

        // Show card.
        evolveCardView.rectTransform.localScale = 0.7f * Vector3.one;
        evolveCardView.rectTransform.anchoredPosition = new Vector2(-200, 60);
        evolveCardView.SetCard(fieldCards[info.index].OriginalCard);

        yield return new WaitForSeconds(0.3f);

        // Normal Rotate.
        currentTime = 0;    
        finishTime = normalRotateTime;

        var normalRotate = new Vector3(0, 90, 0);
        var evolveRotate = new Vector3(0, 165, 0);
        var exhibitRotate = new Vector3(0, 185, 0);

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            evolveCardView.rectTransform.localRotation = Quaternion.Euler(Vector3.Lerp(Vector3.zero, normalRotate, percent));
            particleImage.SetColor(Color.Lerp(Color.black, ColorHelper.gray192, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        // Evolve.
        evolveCardView.SetCard(evolvedCard.OriginalCard);
        evolveCardView.SetFrameRotation(new Vector3(0, 180, 0));

        particleImage.rectTransform.localScale = Vector3.one * particleMagnifyScale;

        // Evolve Rotate.
        currentTime = 0;    
        finishTime = evolveRotateTime;

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            evolveCardView.rectTransform.localRotation = Quaternion.Euler(Vector3.Lerp(normalRotate, evolveRotate, percent));
            particleImage.SetColor(Color.Lerp(ColorHelper.gray192, Color.black, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        // Exhibit Rotate.
        currentTime = 0;
        finishTime = exhibitRotateTime;

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            evolveCardView.rectTransform.localRotation = Quaternion.Euler(Vector3.Lerp(evolveRotate, exhibitRotate, percent));
            currentTime += Time.deltaTime;
            yield return null;
        }

        // Hide card.
        evolveCardView.SetCard(null);
        evolveCardView.SetFrameRotation(Vector3.zero);
        evolveCardView.rectTransform.localRotation = Quaternion.Euler(Vector3.zero);

        particleImage.rectTransform.localScale = Vector3.one;
        particleImage.SetColor(ColorHelper.gray192);

        // Go back.
        currentTime = 0;    
        finishTime = particleMoveTime;

        while (currentTime < finishTime) {
            percent = currentTime / finishTime;
            evolveBackground?.SetColor(Color.Lerp(ColorHelper.black192, Color.clear, percent));
            particleImage.rectTransform.anchoredPosition = Vector3.Lerp(endPoint, startPoint, percent);
            currentTime += Time.deltaTime;
            yield return null;
        }

        // Anim end.
        evolveBackground?.gameObject.SetActive(false);
        evolveBackground?.SetColor(Color.clear);
        particleImage.gameObject.SetActive(false);

        callback?.Invoke();
    }

    private Vector3 GetStartPoint(BattleCardPlaceInfo info, int fieldCount) {
        int q = (fieldCount - 1) / 2;
        int r = (fieldCount - 1) % 2;
        float y = (info.unitId == 0) ? -45 : 70;
        float x = (-10) + ((-50 * r) + (100 * (info.index - q))) * ((info.unitId == 0) ? 1 : -1);

        return new Vector3(x, y);
    }
}
