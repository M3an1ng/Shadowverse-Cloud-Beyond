using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class KeywordView : IMonoBehaviour
{

    [SerializeField] private Dropdown dropdown;
    [SerializeField] private IText descriptionText;

    public override void Init()
    {
        base.Init();
        InitDropdownOptions();
    }

    private void InitDropdownOptions() {
        var keywordNames = new List<string>();
        for (int id = 0; id < GameManager.versionData.keywordCount; id++) {
            var keyword = (CardKeyword)id;
            keywordNames.Add(keyword.GetKeywordName());
        }
        dropdown?.ClearOptions();
        dropdown?.AddOptions(keywordNames);
        dropdown.value = 0;
    }

    public void OnSelectKeyword(int keywordId) {
        var keyword = (CardKeyword)keywordId;
        descriptionText?.SetText(keyword.GetKeywordInfo());
    }

}
