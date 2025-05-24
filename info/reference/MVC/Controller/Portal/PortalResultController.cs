using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalResultController : IMonoBehaviour
{
    [SerializeField] private PortalResultModel resultModel;
    [SerializeField] private PortalResultView resultView;
    [SerializeField] private PortalSearchController searchController;
    [SerializeField] private List<PageView> resultPageViews;

    public event Action<Card[]> onResultSetPageEvent;

    protected override void Awake()
    {
        base.Awake();
        searchController.onSearchEvent += Search;
    }

    public void SetStorage(List<Card> cardMaster) {
        resultModel.SetStorage(cardMaster);
    }

    public void Search(CardFilter filter) {
        resultModel.Filter(filter.Filter);
        resultModel.Sort(CardDatabase.Sorter, false);
        OnResultSetPage();
    }

    public void SetPortalSelectionsCardCount(int[] counts = null) {
        resultView?.SetPortalResultCardCount(counts);
    }

    public void Select(int index) {
        var panel = Panel.OpenPanel<CardDetailPanel>();
        panel.SetCard(resultModel.Selections[index]);
    } 

    private void OnResultSetPage() {
        for (int i = 0; i < resultPageViews.Count; i++) {
            resultPageViews[i]?.SetPage(resultModel.Page, resultModel.LastPage);
            resultPageViews[i]?.SetStorageCount(resultModel.Count);
        }
        resultView?.SetPortalResult(resultModel.Selections);
        onResultSetPageEvent?.Invoke(resultModel.Selections);
    }

    public void OnResultFirstPage() {
        resultModel.SetPage(0);
        OnResultSetPage();
    }

    public void OnResultLastPage() {
        resultModel.SetPage(resultModel.LastPage);
        OnResultSetPage();
    }

    public void OnResultPrevPage() {
        resultModel.PrevPage();
        OnResultSetPage();
    }

    public void OnResultNextPage() {
        resultModel.NextPage();
        OnResultSetPage();
    }
}
