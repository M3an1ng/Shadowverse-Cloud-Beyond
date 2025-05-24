using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardInfoEffectView : IMonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Text atkText, hpText;
    [SerializeField] private IText descriptionText;

    public float RectSize => gameObject.activeSelf ? rectTransform.rect.size.y : 0;

    public void SetBattleCard(BattleCard card) {
        var additionalDescription = card?.GetAdditionalDescription();
        var isAdditionalDescriptionEmpty = string.IsNullOrEmpty(additionalDescription);

        gameObject.SetActive(!isAdditionalDescriptionEmpty);

        if (isAdditionalDescriptionEmpty)
            return;

        var split = additionalDescription.Split(new char[] { '\n' }, 2);
        var buff = split[0].Split(new char[] { '/' }, 2);

        SetBuffValueText(buff[0], buff[1]);
        if(split.Length > 1)SetEffectDescriptionText(split[1]);
    }

    public void SetBuffValueText(string atkBuff, string hpBuff) {
        atkText?.SetText(atkBuff);
        hpText?.SetText(hpBuff);
    }

    public void SetEffectDescriptionText(string text) {
        descriptionText?.SetText(text);
        descriptionText?.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, GetDescriptionTextPreferredSize());
    }

    public float GetDescriptionTextPreferredSize() {
        return descriptionText?.PreferredSize.y ?? 0;
    }

    public void SetAnchoredPos(Vector2 pos) {
        if (rectTransform == null)
            return;
            
        rectTransform.anchoredPosition = pos;
    }
}
