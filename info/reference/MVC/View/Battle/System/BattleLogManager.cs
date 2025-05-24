using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleLogManager : Manager<BattleLogManager>
{
    public static string[] title => new string[] { "戰場紀錄", "已使用的卡片", "被破壞的從者", "對戰資訊" };

    [SerializeField] private Image background;
    [SerializeField] private OptionSelectModel selectModel;
    [SerializeField] private Text titleText;
    [SerializeField] private BattleLogMainView mainView;
    [SerializeField] private BattleLogUseView useView;
    [SerializeField] private BattleLogDestroyView destroyView;
    [SerializeField] private BattleLogInfoView infoView;


    public void SetActive(bool active) {
        background.gameObject.SetActive(active);
        if (!active)
            return;

        useView?.SetWho(true);
        destroyView?.SetWho(true);
        infoView?.SetWho(true);

        selectModel.Select(0);
        OnLogSetPage();
    }

    public void Log(BattleState state) {
        if (state.currentEffect.ability == EffectAbility.None)
            return;

        mainView?.LogState(state);
        useView?.LogUse(state);
        destroyView?.LogDestroy(state);
        infoView?.LogInfo(state);
    }

    private void OnLogSetPage() {
        titleText?.SetText(title[selectModel.Cursor[0]]);
        mainView?.AutoScrollToBottom();
    }

    public void OnLogPrevPage() {
        selectModel.Select((selectModel.Cursor[0] + title.Length - 1) % title.Length);
        OnLogSetPage();
    }

    public void OnLogNextPage() {
        selectModel.Select((selectModel.Cursor[0] + 1) % title.Length);
        OnLogSetPage();
    }
}
