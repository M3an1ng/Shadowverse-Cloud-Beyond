using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Panel : IMonoBehaviour
{
    [SerializeField] protected Image background;
    [SerializeField] protected IButton ESCButton;

    public event Action onCloseEvent;

    public static T OpenPanel<T>() where T : Panel {
        string name = typeof(T).Name.TrimEnd("Panel");
        GameObject canvas = GameObject.Find("Canvas");
        GameObject prefab = ResourceManager.instance.GetPanel(name);
        if (prefab == null)
            return null;

        GameObject obj = Instantiate(prefab, canvas.transform);
        obj.transform.SetAsLastSibling();
        T panel = obj.GetComponentInChildren<T>();
        return panel;
    }

    public static Panel OpenPanel(string panelName) {
        panelName = panelName.TrimEnd("Panel");
        GameObject canvas = GameObject.Find("Canvas");
        GameObject prefab = ResourceManager.instance.GetPanel(panelName);
        if (prefab == null)
            return null;

        GameObject obj = Instantiate(prefab, canvas.transform);
        obj.transform.SetAsLastSibling();
        Panel panel = obj.GetComponentInChildren<Panel>();
        return panel;
    }

    public virtual void ClosePanel() {
        onCloseEvent?.Invoke();
        
        if (background != null) {
            Destroy(background.gameObject);
        }
        Destroy(gameObject);
    }

    public void SetActive(bool active) {
        var panel = background?.gameObject ?? gameObject;
        panel.SetActive(active);
    }

    public void SetBackgroundColor(Color color) {
        background.color = color;
    }

}
