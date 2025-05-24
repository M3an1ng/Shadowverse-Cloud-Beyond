using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleResult 
{
    public BattleResultState masterState = BattleResultState.None;
    public BattleResult() {}
    public BattleResult(BattleResult rhs) {
        masterState = rhs.masterState;
    }
}

public enum BattleResultState {
    None = 0,
    Win = 1,
    Lose = 2,
    Draw = 3,
}

public enum BattleLoseReason {
    Retire = 0,
    LeaderDie = 1,
    Deckout = 2,
    TurnOverMax = 3,
}
