using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.AddressableAssets;
using System.Linq;
using System;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SceneLoader : Singleton<SceneLoader>
{
    public GameObject loadingScreen;
    public Image loadingPanel;
    public Slider loadingSlider;
    public Text loadingIndicator, loadingText;
    public GameObject CornerLoadingObject;

    public static SceneId CurrentSceneId { get; private set; } = SceneId.Title;

    public void StartCornerLoading() {
        loadingScreen.SetActive(true);
        Color c = loadingPanel.color;
        loadingPanel.color = new Color(c.r, c.g, c.b, 0);
        loadingSlider.gameObject.SetActive(false);
        CornerLoadingObject.SetActive(true);
    }    

    public void StopCornerLoading() {
        loadingScreen.SetActive(false);
        Color c = loadingPanel.color;
        loadingPanel.color = new Color(c.r, c.g, c.b, 1);
        loadingSlider.gameObject.SetActive(true);
        CornerLoadingObject.SetActive(false);
    }

    public void ChangeScene(SceneId index, bool network = false) {
        loadingScreen.SetActive(true);
        AudioSystem.instance.StopMusic();
        PhotonNetwork.AutomaticallySyncScene = network;
        
        if (!network) {
            PhotonNetwork.AutomaticallySyncScene = false;

            var coroutine = (GameManager.instance.state == GameState.Init) ? DownloadResourcesCoroutine(index) : ChangeSceneAsync(index);
            StartCoroutine(coroutine);
        } else {
            if (!PhotonNetwork.IsConnected) {
                loadingIndicator?.SetText("連線已中斷");    
                return;
            }   

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel((int)index);
        
            StartCoroutine(ChangeSceneAsyncPhoton(index));
        }
    }
    
    private IEnumerator ChangeSceneAsync(SceneId index) {
        AsyncOperation operation = SceneManager.LoadSceneAsync(((int)index));
        while(!operation.isDone) {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            
            loadingSlider.value = progress;
            loadingText.text = (Mathf.CeilToInt(progress * 10000) / 100).ToString() + "%";

            yield return null;
        }
        CurrentSceneId = index;
        loadingScreen.SetActive(false);
    }

    private IEnumerator ChangeSceneAsyncPhoton(SceneId index) {
        while (PhotonNetwork.LevelLoadingProgress < 1) {
            float progress = PhotonNetwork.LevelLoadingProgress;
            
            loadingSlider.value = progress;
            loadingText.text = (Mathf.CeilToInt(progress * 10000) / 100).ToString() + "%";
            yield return null;
        }
        CurrentSceneId = index;
        loadingScreen.SetActive(false);
    }

    private IEnumerator DownloadResourcesCoroutine(SceneId index) {
        loadingSlider.value = 0;
        loadingText?.SetText(string.Empty);
        AudioSystem.instance.PlayMusic(AudioResources.Main);

        loadingIndicator?.SetText("正在初始化");
        var initHandle = Addressables.InitializeAsync();
        yield return initHandle;
    
        loadingIndicator?.SetText("正在檢測更新");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return checkHandle;
    
        if (!IsAsyncHandleSucceeded(checkHandle, "檢測更新失敗"))
            yield break;
    
        if (checkHandle.Result.Count > 0) {
            loadingIndicator?.SetText("正在獲取目錄");
            var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
            yield return updateHandle;
    
            if (!IsAsyncHandleSucceeded(updateHandle, "獲取更新目錄失敗"))
                yield break;
    
            var updateList = updateHandle.Result;
            var updateCount = updateList.Count;
            Addressables.Release(updateHandle);
    
            for (int i = 0; i < updateCount; i++) {
                IEnumerable<object> keys = new List<object>(updateList[i].Keys);
                var sizeHandle = Addressables.GetDownloadSizeAsync(keys);
                yield return sizeHandle;
    
                if (!IsAsyncHandleSucceeded(sizeHandle, "獲取更新檔案大小失敗"))
                    yield break;
    
                var totalDownloadSize = sizeHandle.Result;
                if (totalDownloadSize > 0) {
                    var downloadHandle  = Addressables.DownloadDependenciesAsync(keys, true);
                    while (!downloadHandle.IsDone) {
                        if (downloadHandle.Status == AsyncOperationStatus.Failed) {
                            RequestManager.OnRequestFail("檔案下載失敗，請重新啟動。\n錯誤：" + downloadHandle.OperationException);
                            loadingScreen.SetActive(false);
                            yield break;
                        }
                        var status = downloadHandle.GetDownloadStatus();
                        float progress = downloadHandle.PercentComplete;
                        loadingSlider.value = Mathf.Clamp01(progress / 100);
    
                        var downloadProgressText = (status.DownloadedBytes / 1000) + " KB / " + (status.TotalBytes / 1000) + " KB";
                        var downloadCountText = (i + 1) + " / " + updateCount;
                        loadingIndicator?.SetText("已下載：" + downloadCountText);
                        loadingText?.SetText(downloadProgressText);
                        yield return null;
                    }
                }
            }
            loadingIndicator?.SetText("更新完畢");
        }
        Addressables.Release(checkHandle);
        GameManager.instance.ChangeState(GameState.Play);
        AudioSystem.instance.StopMusic();
    
        loadingIndicator?.SetText("正在載入場景");
        var operation = SceneManager.LoadSceneAsync((int)index);
        while (!operation.isDone) {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            
            loadingSlider.value = progress;
            loadingText.text = (Mathf.CeilToInt(progress * 10000) / 100).ToString() + "%";

            yield return null;
        }
    
        CurrentSceneId = index;
        loadingScreen.SetActive(false);
    }
    
    private bool IsAsyncHandleSucceeded<T>(AsyncOperationHandle<T> asyncOperationHandle, string errorHeader) {
        if (asyncOperationHandle.Status != AsyncOperationStatus.Succeeded) {
            RequestManager.OnRequestFail(errorHeader + "，請重新啟動。\n錯誤：" + asyncOperationHandle.OperationException);
            loadingScreen.SetActive(false);
            return false;
        }
        return true;
    }
}

public enum SceneId {
    Title = 0,
    Main = 1,
    DeckBuilder = 2,
    Portal = 3,
    Battle = 4,
    Room = 5,
}
