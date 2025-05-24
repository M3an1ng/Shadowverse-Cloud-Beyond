using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionSelectView : IMonoBehaviour
{
    private bool[] isSelected;
    [SerializeField] private List<IButton> optionButtons;

    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < optionButtons.Count; i++) {
            int copy = i;
            optionButtons[i]?.onPointerEnterEvent.AddListener(() => OnPointerEnter(copy));
            optionButtons[i]?.onPointerExitEvent.AddListener(() => OnPointerExit(copy));
        }
        isSelected = new bool[optionButtons.Count];
    }

    public void Clear() {
        foreach (var button in optionButtons) 
            button?.SetColor(Color.white);
    }

    public void SetSelectState(bool[] isSelected) {
        this.isSelected = isSelected;
        for (int i = 0; i < optionButtons.Count; i++) {
            optionButtons[i]?.SetColor(((i < isSelected.Length) && isSelected[i]) ? ColorHelper.chosen : Color.black);
        }
    }

    private void OnPointerEnter(int index) {
        optionButtons[index]?.SetColor(ColorHelper.chosen);
    }

    private void OnPointerExit(int index) {
        if (isSelected[index])
            return;

        optionButtons[index]?.SetColor(Color.black);
    }
}
