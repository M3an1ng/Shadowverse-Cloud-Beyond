using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainManager : Manager<MainManager>
{
    protected override void Start() {
        AudioSystem.instance.PlayMusic(AudioResources.Main);
    }

    public void BackToTitleScene() {
        SceneLoader.instance.ChangeScene(SceneId.Title);
    }

    public void GoToPortalScene() {
        SceneLoader.instance.ChangeScene(SceneId.Portal);
    }
}
