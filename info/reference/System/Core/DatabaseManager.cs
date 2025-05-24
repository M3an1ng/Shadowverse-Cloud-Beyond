using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class DatabaseManager : Singleton<DatabaseManager>
{
    public const int LoadingGoal = 4;
    private ResourceManager RM => ResourceManager.instance;

    public List<Card> cardMaster = new List<Card>();
    public List<Stats> statsMaster = new List<Stats>();
    public Dictionary<int, Card> cardInfoDict = new Dictionary<int, Card>();
    public Dictionary<int, Effect> effectInfoDict = new Dictionary<int, Effect>();
    public Dictionary<string, Effect> effectNameInfoDict = new Dictionary<string, Effect>();
    public Dictionary<int, Buff> buffInfoDict = new Dictionary<int, Buff>();
    public Dictionary<int, Stats> statsInfoDict = new Dictionary<int, Stats>();
    public Dictionary<string, Stats> statsNameInfoDict = new Dictionary<string, Stats>();
    public Dictionary<CardTrait, string> traitNameDict = new Dictionary<CardTrait, string>();
    public Dictionary<CardKeyword, string> keywordNameDict = new Dictionary<CardKeyword, string>();
    public Dictionary<CardKeyword, string> keywordEnglishNameDict = new Dictionary<CardKeyword, string>();
    public Dictionary<CardKeyword, string> keywordInfoDict = new Dictionary<CardKeyword, string>();

    public bool IsReady => loadingProgress == LoadingGoal;
    private int loadingProgress = 0;

    public void Init() {
        RM.LoadTraitInfo((x) => {
            traitNameDict = x.ToDictionary(entry => (CardTrait)entry.Key, entry => entry.Value[0]);
            loadingProgress += 1;
        });

        RM.LoadKeywordInfo((x) => {
            keywordNameDict = x.ToDictionary(entry => (CardKeyword)entry.Key, entry => entry.Value[0]);
            keywordEnglishNameDict = x.ToDictionary(entry => (CardKeyword)entry.Key, entry => entry.Value[1]);
            keywordInfoDict = x.ToDictionary(entry => (CardKeyword)entry.Key, entry => entry.Value[2].GetDescription("-"));
            loadingProgress += 1;
        });

        RM.LoadStatsInfo((x) => {
            statsInfoDict = x;
            statsMaster = statsInfoDict.Select(entry => entry.Value).ToList();
            loadingProgress += 1;
        });

        RM.LoadCardInfo((x) => { 
            cardInfoDict = x; 
            cardMaster = cardInfoDict.Select(entry => entry.Value).ToList();
            loadingProgress += 1;
        }, (y) => effectInfoDict = y, (z) => buffInfoDict = z);
    }

    public bool VerifyData(out string error) {
        if (!IsReady) {
            error = "正在初始化資料庫 (" + loadingProgress + "/" + LoadingGoal + ")\n請稍候再試";
            return false;
        }
        /*if (keywordInfoDict.Count != GameManager.versionData.keywordCount) {
            error = "獲取關鍵字說明資料失敗 (" + keywordInfoDict.Count + "/" + GameManager.versionData.keywordCount  + ")";
            return false;
        }
        if (traitNameDict.Count != GameManager.versionData.traitCount) {
            error = "獲取類型資料失敗 (" + traitNameDict.Count + "/" + GameManager.versionData.traitCount  + ")";
            return false;
        }
        if (cardInfoDict.Count != GameManager.versionData.cardCount) {
            error = "獲取卡片資料失敗 (" + cardInfoDict.Count + "/" + GameManager.versionData.cardCount + ")";
            return false;
        }*/
        error = string.Empty;
        return true;
    }

    public Card GetCardInfo(int id) {
        if (id == 0)
            return null;

        return cardInfoDict.Get(id);
    }

    public Effect GetEffectInfo(int id) {
        if (id == 0)
            return null;

        return effectInfoDict.Get(id);
    }

    public Effect GetEffectInfo(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "none")
            return null;

        return effectNameInfoDict.Get(name.TrimEnd('\n', '\t', '\r')); 
    }

    public Buff GetBuffInfo(int id)
    {
        if (id == 0)
            return null;

        return buffInfoDict.Get(id);
    }

    public Stats GetStatsInfo(int id)
    {
        if (id == 0)
            return null;

        return statsInfoDict.Get(id);
    }

    public Stats GetStatsInfo(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "none")
            return null;

        return statsNameInfoDict.Get(name);
    }

    public string GetTraitName(CardTrait trait) {
        return traitNameDict.Get(trait, "-");
    }

    public string GetKeywordName(CardKeyword keyword) {
        return keywordNameDict.Get(keyword, "-");
    }

    public string GetKeywordEnglishName(CardKeyword keyword) {
        return keywordEnglishNameDict.Get(keyword, "-");
    }

    public string GetKeywordInfo(CardKeyword keyword) {
        return keywordInfoDict.Get(keyword, string.Empty);
    }
    
}
