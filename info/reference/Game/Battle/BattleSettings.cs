using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleSettings 
{
    public bool isLocal = false;
    public CardZone zone = CardZone.Cloud;
    public GameFormat format = GameFormat.Rotation;
    public int leaderHp = 20;
    public int evolveStart = 4;
    public string masterName;
    public string clientName;
    public int seed = 0;

    public BattleSettings(CardZone zoneId, GameFormat formatId, bool local = false, int hp = 20, int evolveStartTurn = 4) {
        isLocal = local;
        zone = zoneId;
        format = formatId;
        leaderHp = hp;
        evolveStart = evolveStartTurn;
    }

    public BattleSettings(BattleSettings rhs) {
        isLocal = rhs.isLocal;
        zone = rhs.zone;
        format = rhs.format;
        leaderHp = rhs.leaderHp;
        evolveStart = rhs.evolveStart;
        masterName = rhs.masterName;
        clientName = rhs.clientName;
        seed = rhs.seed;
    }
}
