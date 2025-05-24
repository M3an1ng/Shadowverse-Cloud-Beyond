using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardView : IMonoBehaviour
{
    public Card CurrentCard { get; private set; }
    public RectTransform rectTransform { get; private set; }
    [SerializeField] public IDraggable draggable;

    [SerializeField] private Text nameText;
    [SerializeField] private IButton cardFrameButton;
    [SerializeField] private RawImage artworkRawImage;
    [SerializeField] private Image gemImage;
    [SerializeField] private List<Image> costImages = new List<Image>();
    [SerializeField] private List<Image> atkImages = new List<Image>();
    [SerializeField] private List<Image> hpImages = new List<Image>();
    [SerializeField] private List<Image> outlines = new List<Image>();
    [SerializeField] private GameObject countObject, countLimitObject;
    [SerializeField] private Text countText;
    [SerializeField] private GameObject tagObject;
    [SerializeField] private Text costTag, atkTag, hpTag;

    protected override void Awake()
    {
        base.Awake();
        rectTransform = gameObject.GetComponent<RectTransform>();
    }

    public async void SetCard(Card card, int count = -1, bool tag = false) {
        CurrentCard = card;
        gameObject.SetActive(card != null);
        if (card == null)
            return;
        
        SetName(card.name);
        SetFrame(card.TypeId, card.RarityId);
        SetOutlineType(card.Type);
        SetGem(card.CraftId);
        SetStatus("cost", card.cost);
        SetStatus("atk", card.atk);
        SetStatus("hp", card.hp);
        SetCount(count);
        SetTag(tag ? card : null);
        SetArtwork(await card.Artwork ?? SpriteResources.DefaultSleeve.texture, card.Type);
    }

    public void SetBattleCard(BattleCard card) {
        SetCard(card?.CurrentCard, int.MinValue, true);
    }

    public void SetName(string name) {
        nameText?.SetText(name);
        nameText?.SetFontSize(Mathf.Min(16, 26 - name.Length));
    }

    public void SetFrame(int type, int rarity) {
        cardFrameButton?.SetSprite(SpriteResources.GetCardFrameSprite(type, rarity));

        SetActiveAll(atkImages, CurrentCard.IsFollower());
        SetActiveAll(hpImages, CurrentCard.IsFollower());
    }

    public void SetFrameRotation(Vector3 euler) {
        cardFrameButton?.SetRotation(euler);
    }

    public void SetArtwork(Texture2D artwork, CardType type = CardType.Follower) {
        // var euler = new Vector3(0, type == CardType.Evolved ? 180 : 0, 0);
        // artworkRawImage.rectTransform.rotation = Quaternion.Euler(euler);
        artworkRawImage.SetTexture(artwork ?? SpriteResources.DefaultSleeve?.texture);
    }

    public void SetGem(int craft) {
        gemImage?.SetSprite(SpriteResources.GetCardGemSprite(craft));
    }

    public void SetOutlineType(CardType type) {
        var id = type switch {
            CardType.Follower   => 0,
            CardType.Evolved    => 0,
            CardType.Spell      => 1,
            CardType.Amulet     => 2,
            CardType.Territory  => 2,
            _ => -1,
        };
        outlines.ForEach(x => x.gameObject.SetActive(false));
        
        if (id == -1)
            return;

        outlines[id].gameObject.SetActive(true);
    }

    public void SetStatus(string status, int num) {
        var images = status switch {
            "cost" => costImages,
            "atk" => atkImages,
            "hp" => hpImages,
            _ => null,
        };

        int ten = num % 100 / 10;
        int one = num % 10;

        images[0]?.SetColor(((ten > 0) || (num < 0)) ? Color.clear : Color.white);
        images[1]?.SetColor(((ten <= 0)|| (num < 0)) ? Color.clear : Color.white);
        images[2]?.SetColor(((ten <= 0)|| (num < 0)) ? Color.clear : Color.white);

        images[0]?.SetSprite(SpriteResources.GetCardCostSprite(one));
        images[1]?.SetSprite(SpriteResources.GetCardCostSprite(ten));
        images[2]?.SetSprite(SpriteResources.GetCardCostSprite(one));
    }

    private void SetActiveAll(List<Image> images, bool active) {
        foreach (var img in images)
            img?.gameObject.SetActive(active);
    }

    public void SetCallback(Action callback, string which = "onClick") {
        cardFrameButton?.onPointerClickEvent.SetListener(callback.Invoke);
    }

    public Color SetOutlineColor(Color color) {
        var oldColor = outlines[0].color;
        outlines.ForEach(x => x.SetColor(color));
        return oldColor;
    }

    public void SetCount(int count) {
        draggable?.SetEnable(!count.IsWithin(-3, 0));
        countObject?.SetActive(count >= 0);
        countLimitObject?.SetActive(count == 0);
        countText?.SetText(count.ToString());
        cardFrameButton?.SetInteractable(count != 0, true);
    }

    public void SetTag(Card card) {
        bool active = (card != null) && (card.IsFollower());
        tagObject?.SetActive(active);
        if (!active)
            return;

        costTag?.SetText(card.hp.ToString());
        atkTag?.SetText(card.atk.ToString());
        hpTag?.SetText(card.hp.ToString());
    }
}
