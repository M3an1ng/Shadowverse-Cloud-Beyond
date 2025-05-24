using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageView : IMonoBehaviour
{
    [SerializeField] private Text storageCountText;
    [SerializeField] private Text currentPageText;
    [SerializeField] private Text lastPageText;
    [SerializeField] private IButton prevButton;
    [SerializeField] private IButton nextButton;

    public void SetPage(int page, int lastPage, int firstPage = 0) {
        currentPageText?.SetText((page + 1).ToString());
        lastPageText?.SetText((lastPage + 1).ToString());
        prevButton?.SetInteractable(page > firstPage);
        nextButton?.SetInteractable(page < lastPage);
    }

    public void SetStorageCount(int count) {
        storageCountText?.SetText(count.ToString());
    }

}
