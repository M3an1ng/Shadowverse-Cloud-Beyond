using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardInfoButtonView : IMonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private List<IButton> buttons;

    public int ActiveButtonCount => buttons.Count(x => x.gameObject.activeSelf);
    public float RectSize => (ActiveButtonCount == 0) ? 0 : (20 + 30 * ActiveButtonCount);

    protected override void Awake()
    {
        base.Awake();
        Reset();
    }

    public void Reset() => buttons.ForEach(x => x.gameObject.SetActive(false));
    public void SetEvolvable(bool evolvable) => SetButtonActive(0, evolvable);
    public void SetActable(bool actable) => SetButtonActive(1, actable);
    public void SetFusionable(bool fusionable) => SetButtonActive(2, fusionable);
    public void SetCoverable(bool coverable) => SetButtonActive(3, coverable);

    private void SetButtonActive(int index, bool active) {
        if (!index.IsInRange(0, buttons.Count))
            return;

        buttons[index]?.gameObject.SetActive(active);
    }

    public void SetAnchoredPos(Vector2 pos) {
        if (rectTransform == null)
            return;
        rectTransform.anchoredPosition = pos;
    }
}
