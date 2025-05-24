using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class IContainer : IMonoBehaviour, IDropHandler
{
    private RectTransform rectTransform;
    public UnityEvent onDropEvent = new UnityEvent();

    protected override void Awake()
    {
        base.Awake();
        rectTransform = gameObject.GetComponent<RectTransform>();    
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null) {
            eventData.pointerDrag.GetComponent<RectTransform>().anchoredPosition = rectTransform.anchoredPosition;
            onDropEvent?.Invoke();
        }
    }
}
