using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PortalManager : Manager<PortalManager>
{
    [SerializeField] private PortalSearchController searchController;
    [SerializeField] private PortalResultController resultController;
    [SerializeField] private ScrollRect scrollRect;

    protected override void Start() {
        AudioSystem.instance.PlayMusic(AudioResources.Main);

        resultController.SetStorage(CardDatabase.CardMaster);
        searchController.Search();
    }

    public void OpenNewsPanel() {
        Panel.OpenPanel<NewsPanel>();
    }

    public void BackToMainScene() {
        SceneLoader.instance.ChangeScene(SceneId.Main);
    }

    public void GoToTop() {
        scrollRect.verticalNormalizedPosition = 1;
    }
}
