using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BattleLogMainView : BattleBaseView
{
    [SerializeField] private int maxLogCount = 100;
    [SerializeField] private ScrollRect scrollRect;

    private List<LogInfoView> battleLogs = new List<LogInfoView>();

    public void LogState(BattleState state) {
        var effect = state.currentEffect;
        string log = effect.hudOptionDict.Get("log", string.Empty).TrimEnd("\n");
        var logCard = (effect.ability == EffectAbility.Summon) ? effect.invokeTarget[0] : effect.source;
        var color = (effect.invokeUnit.id == state.myUnit.id) ? Color.cyan : Color.red;

        if (string.IsNullOrEmpty(log))
            return;

        var logArray = log.Split('\n');
        for (int i = 0; i < logArray.Length; i++) {
            if (battleLogs.Count >= maxLogCount) {
                Destroy(battleLogs[0].gameObject);
                battleLogs.RemoveAt(0);
            }

            var obj = Instantiate(SpriteResources.Log, scrollRect.content);
            var logPrefab = obj.GetComponent<LogInfoView>();

            logPrefab.SetEffect(logArray[i], color, effect, () => cardInfoView?.SetCard(logCard.baseCard));
            battleLogs.Add(logPrefab);
        }
    }

    public void AutoScrollToBottom() {
        scrollRect.verticalNormalizedPosition = 0;
    }

}
