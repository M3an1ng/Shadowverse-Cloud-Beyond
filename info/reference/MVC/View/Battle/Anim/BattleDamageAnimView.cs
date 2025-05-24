using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleDamageAnimView : BattleBaseView
{
    [SerializeField] private BattleLeaderView myLeaderView, opLeaderView;
    [SerializeField] private List<BattleCardView> myCardViews, opCardViews;
    [SerializeField] private BattleHandView myHandView, opHandView;

    public void ShowDamage(BattleCardPlaceInfo info, int damage, Action callback) {
        var leaderView = (info.unitId == 0) ? myLeaderView : opLeaderView;
        var fieldView = (info.unitId == 0) ? myCardViews : opCardViews;
        var handView = (info.unitId == 0) ? myHandView : opHandView;

        switch (info.place) {
            case BattlePlaceId.Leader:
                handView.SetHandMode(false);
                leaderView.SetDamage(damage, Color.red, callback);
                break;
            case BattlePlaceId.Field:
                if (!info.index.IsInRange(0, fieldView.Count))
                    break;

                fieldView[info.index].SetDamage(damage, Color.red, callback);
                break;
        }
    }

    public void ShowHeal(BattleCardPlaceInfo info, int heal, Action callback) {
        var leaderView = (info.unitId == 0) ? myLeaderView : opLeaderView;
        var fieldView = (info.unitId == 0) ? myCardViews : opCardViews;
        var handView = (info.unitId == 0) ? myHandView : opHandView;

        switch (info.place) {
            case BattlePlaceId.Leader:
                handView.SetHandMode(false);
                leaderView.SetDamage(heal, ColorHelper.green192, callback);
                break;
            case BattlePlaceId.Field:
                if (!info.index.IsInRange(0, fieldView.Count))
                    break;

                fieldView[info.index].SetDamage(heal, ColorHelper.green192, callback);
                break;
        }
    }

    public void ShowLeaveField(BattleCardPlaceInfo info, string type, Action callback) {
        var leaderView = (info.unitId == 0) ? myLeaderView : opLeaderView;
        var fieldView = (info.unitId == 0) ? myCardViews : opCardViews;

        switch (info.place) {
            case BattlePlaceId.Field:
                if (!info.index.IsInRange(0, fieldView.Count))
                    break;

                fieldView[info.index].SetLeaveField(type, callback);
                break;
        }
    }

}
