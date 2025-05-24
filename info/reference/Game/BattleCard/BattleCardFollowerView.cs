using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BattleCardFollowerView : BattleBaseView
{
    [SerializeField] private Text atkText, hpText;
    [SerializeField] private Outline atkOutline, hpOutline;
    [SerializeField] private IButton cardFrameButton;
    [SerializeField] private RawImage artworkRawImage;
    [SerializeField] private Image flagImage, outlineImage;

    private Dictionary<string, bool> flagResultDict = new Dictionary<string, bool>();
    private Coroutine flagCoroutine = null;

    private void OnEnable() {
        flagCoroutine = StartCoroutine(ShowFlagIcon());
    }

    private void OnDisable() {
        StopCoroutine(flagCoroutine);
    }

    public async void SetBattleCard(BattleCard battleCard) {
        if (battleCard == null)
            return;

        var card = battleCard.CurrentCard;
        
        atkText?.SetText(card.atk.ToString());
        atkText?.SetColor(ColorHelper.GetAtkHpTextColor(card.atk, card.atk, battleCard.OriginalCard.atk));
        atkOutline?.SetColor(ColorHelper.GetAtkHpOutlineColor(card.atk, card.atk, battleCard.OriginalCard.atk));

        hpText?.SetText(card.hp.ToString());
        hpText?.SetColor(ColorHelper.GetAtkHpTextColor(card.hp, card.hpMax, battleCard.OriginalCard.hp));
        hpOutline?.SetColor(ColorHelper.GetAtkHpOutlineColor(card.hp, card.hpMax, battleCard.OriginalCard.hp));

        cardFrameButton?.SetSprite(SpriteResources.GetBattleCardFrameSprite(card.TypeId, card.RarityId));

        SetFlagIcon(battleCard);
        SetOutline(battleCard);
        SetArtwork(await card.Artwork, card.Type);
    }

    private void SetArtwork(Texture2D artwork, CardType type) {
        var euler = new Vector3(0, type == CardType.Evolved ? 180 : 0, 0);
        artworkRawImage.rectTransform.rotation = Quaternion.Euler(euler);
        artworkRawImage.SetTexture(artwork ?? SpriteResources.DefaultSleeve?.texture);
    }

    private void SetFlagIcon(BattleCard card) {
        var currentCard = card.CurrentCard;
        var effects = currentCard.effects;

        flagResultDict.Set("lastword", effects.Exists(x => x.timing == "on_this_destroy"));
        flagResultDict.Set("flag", effects.Exists(x => ((x.timing.TryTrimStart("on_", out var trimTiming)) && (!trimTiming.StartsWith("this_")))
            || (x.timing == "on_this_attack") || (x.timing == "on_this_defense") || (x.timing == "on_this_evolve")));

        flagResultDict.Set("bane", card.IsKeywordAvailable(CardKeyword.Bane));
        flagResultDict.Set("drain", card.IsKeywordAvailable(CardKeyword.Drain));
        flagResultDict.Set("earth", currentCard.traits.Contains(CardTrait.Earth));
    }

    public void SetOutline(BattleCard battleCard) {
        var unit = Hud.CurrentState.GetBelongUnit(battleCard);
        if (unit == null) 
            return;

        var isLeaderAttackable = battleCard.IsLeaderAttackable(unit);
        var isFollowerAttackable = battleCard.IsFollowerAttackable(unit);
        
        if (isLeaderAttackable) {
            SetOutlineColor(ColorHelper.storm);
        } else if (isFollowerAttackable) {
            SetOutlineColor(ColorHelper.rush);
        } else {
            SetOutlineColor(Color.clear);
        }
    }

    public void SetOutlineColor(Color color) {
        outlineImage?.SetColor(color);
    }

    private IEnumerator ShowFlagIcon() {
        int index = 0;
        while (true) {
            var trueCount = flagResultDict.Count(entry => entry.Value);
            flagImage?.SetColor((trueCount == 0) ? Color.clear : Color.white);
            if (trueCount == 1)
                flagImage?.SetSprite(SpriteResources.GetCardIcon(flagResultDict.First(x => x.Value).Key));
                        
            if (trueCount < 2) {
                yield return null;
                continue;
            }
            for (int i = 0; i < BattleCard.FlagProperties.Length; i++) {
                var circularIndex = (index + i) % BattleCard.FlagProperties.Length;
                var key = BattleCard.FlagProperties[circularIndex];
                if (flagResultDict.Get(key, false)) {
                    index = circularIndex;
                    flagImage?.SetSprite(SpriteResources.GetCardIcon(key));
                    break;
                }
            }
            float currentTime = 0, finishTime = 3, percent = 0;
            while (currentTime < finishTime) {
                percent = currentTime / finishTime;
                var color = (percent <= 0.5f) ? Color.Lerp(new Color(1, 1, 1, 0), Color.white, percent * 2) :
                    Color.Lerp(Color.white, new Color(1, 1, 1, 0), percent * 2 - 1);

                flagImage?.SetColor(color);
                currentTime += Time.deltaTime;
                yield return null;
            }
            index++;
            yield return null;
        }
    }
}
