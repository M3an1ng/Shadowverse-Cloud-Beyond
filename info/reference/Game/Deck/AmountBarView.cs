using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AmountBarView : IMonoBehaviour
{
    [SerializeField] private int maxAmount = 20;
    [SerializeField] private RectTransform backgroundRect, amountRect;
    [SerializeField] private Text indicatorText, amountText;

    private float MaxHeight => backgroundRect.rect.size.y;

    public void SetIndicator(string indicator) {
        indicatorText?.SetText(indicator);
    }

    public void SetMaxAmount(int max) {
        maxAmount = max;
    }

    public void SetAmount(int amount) {
        float percent = Mathf.Clamp(amount * 1f / maxAmount, 0, 1);
        amountText?.SetText(amount.ToString());
        amountRect?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, percent * MaxHeight);
    }
}
