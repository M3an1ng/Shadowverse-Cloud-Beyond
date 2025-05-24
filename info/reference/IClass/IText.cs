using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class IText : IMonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform Rect => Text?.rectTransform;
    public TextMeshProUGUI Text { get; protected set; }
    public Color Color => Text.color;
    public Vector2 PreferredSize => GetPreferredSize();
    [SerializeField] public UnityEvent<string> onPointerClickEvent = new UnityEvent<string>();
    [SerializeField] public UnityEvent<string> onPointerEnterEvent = new UnityEvent<string>();
    [SerializeField] public UnityEvent<string> onPointerExitEvent = new UnityEvent<string>();

    protected override void Awake()
    {
        base.Awake();
        Text = gameObject.GetComponent<TextMeshProUGUI>(); 
    }

    private void OnDestroy() {
        onPointerClickEvent?.RemoveAllListeners();
        onPointerEnterEvent?.RemoveAllListeners();
        onPointerExitEvent?.RemoveAllListeners();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnterEvent?.Invoke(Text.text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExitEvent?.Invoke(Text.text);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onPointerClickEvent?.Invoke(Text.text);
    }

    public Vector2 GetPreferredSize() {
        return (Text == null) ? Vector2.zero : new Vector2(Text.preferredWidth, Text.preferredHeight);
    }

    public void SetText(string text) {
        if (Text == null)
            return;

        this.Text.text = text;
    }

    public void SetColor(Color32 color) {
        if (Text == null)
            return;

        Text.color = color;
    }

    public void SetSize(Vector2 size) {
        if (Rect == null)
            return;

        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    public void SetSizeAuto(RectTransform.Axis axisToChange) {
        if (axisToChange == RectTransform.Axis.Horizontal) {
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PreferredSize.x);
            return;
        }

        if (axisToChange == RectTransform.Axis.Vertical) {
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PreferredSize.y);
            return;
        }
    }

    public void SetFontSize(int size) {
        if (Text == null)
            return;

        Text.fontSize = size;
    }

}
