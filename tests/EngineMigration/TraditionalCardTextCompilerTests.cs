using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class TraditionalCardTextCompilerTests
{
    public static void Run()
    {
        CardDefinition[] cards =
        [
            new() { Id = 7, Name = "范例" },
            new() { Id = 8, Name = "一张從者，示例" },
            new() { Id = 9, Name = "一张從者，示例", IsMirageCard = true }
        ];
        var catalog = Proxy<IGameDataProvider>.Make((method, args) => method.Name switch
        {
            "get_Cards" => cards,
            "GetCardDefinition" => cards.SingleOrDefault(card => card.Id == (int)args![0]!),
            _ => null
        });
        var controller = new DslController(NullLogger<DslController>.Instance, catalog);

        var traditional = Compile("〖入場曲〗指定1張敵方戰場上的從者卡。\n給予其8點傷害。\n〖威懾〗");
        var simplified = Compile("〖入场曲〗指定1张敌方战场上的从者卡。给予其8点伤害。\n〖威慑〗");
        var legacy = Compile("【入场曲】选择1个敌方随从，造成8点伤害。\n【威慑】");
        var expected = Config(simplified);
        foreach (var result in new[] { traditional, legacy })
        {
            var actual = Config(result);
            foreach (var property in new[] { "trigger", "effects", "targets" })
                Check(actual.GetProperty(property).GetRawText() == expected.GetProperty(property).GetRawText(), property);
            Check(result.DetectedKeywords.SequenceEqual(new[] { nameof(CardKeyword.Pressure) }), "innate keyword");
        }
        Check(expected.GetProperty("targets")[0].GetProperty("playerChoiceOptions").GetProperty("minCount").GetInt32() == 1, "choice count");

        var draw = Config(Compile("〖入场曲〗由自己的牌堆中抽取2张卡片。使敌方由其牌堆中抽取1张卡片。"));
        Check(Param(draw, 0, "PlayerScope").GetString() == "Self", "self draw");
        Check(Param(draw, 1, "PlayerScope").GetString() == "Opponent", "opponent draw");
        Check(Param(draw, 0, "DrawAmount").GetInt32() == 2, "draw count");

        var area = Config(Compile("〖入場曲〗給予敵方戰場上全部的從者卡6點傷害。使自己戰場上全部的其他從者卡+2/+4。"));
        Check(Effect(area, 0) == "Damage" && Effect(area, 1) == "ModifyStatus", "ordered effects");
        Check(area.GetProperty("targets")[1].GetProperty("query").GetProperty("sourcePoolExpression").GetString()!
            .Contains("self.instanceId != context.sourceCard.instanceId"), "other followers exclude source");
        var random = Config(Compile("随机给予2张敌方战场上的从者卡5点伤害。"));
        Check(random.GetProperty("targets")[0].GetProperty("query").GetProperty("sourcePoolExpression").GetString()!
            .EndsWith(".take(random 2)"), "random count");
        Check(random.GetProperty("targets")[0].GetProperty("playerChoiceOptions").ValueKind == JsonValueKind.Null, "random has no choice");
        var vanish = Config(Compile("〖進化時〗指定1張敵方戰場上的從者卡。使其消失。"));
        Check(Effect(vanish, 0) == "Vanish", "vanish is distinct from destroy");

        var act = Config(Compile("〖策動_２〗\n破壞這張卡片。從自己的牌堆抽取1張卡片。"));
        Check(act.GetProperty("abilityKeywords")[0].GetProperty("value").GetInt32() == 2, "act cost");
        Check(Effect(act, 0) == "Destroy" && Effect(act, 1) == "Draw", "act effects");
        foreach (var text in new[] {
            "消费1【启动】使这张卡片的倒数回合数-1。",
            "消費１〖策動〗\n使這張卡片的倒數回合數－１。",
            "费用1【啟動】使本卡牌倒计时-1。"
        })
        {
            var result = Compile(text, cardType: "Amulet");
            var countdown = Config(result);
            var keywords = AbilityConfigJson.ReadKeywords(result.Abilities.Single().JsonConfig);
            Check(keywords.Count == 1 && keywords[0].Keyword == AbilityKeyword.Act && keywords[0].Value == 1, "paid Act keyword");
            Check(countdown.GetProperty("trigger").GetProperty("eventTypes")[0].GetString() == "Act", "paid Act trigger");
            Check(Effect(countdown, 0) == "SetCountdown" && Param(countdown, 0, "ChangeAmount").GetInt32() == -1, "signed countdown change");
            var selector = countdown.GetProperty("effects")[0].GetProperty("targetSelector")[0].GetString();
            Check(selector == "target1" && countdown.GetProperty("targets")[0].GetProperty("query").GetProperty("sourcePoolExpression").GetString()
                == "context.sourceCard", "countdown affects the source card");
        }
        var innateKeywords = new[] { "Bane", "Drain", "Shield", "Aura", "Ambush", "Pressure" };
        foreach (var text in new[] {
            "【毁灭】【虹吸】【屏障】【灵气】【潜伏】【威慑】",
            "〖必殺〗〖吸血〗〖障壁〗〖光紋〗〖潛行〗〖威懾〗"
        })
        {
            var result = Compile(text);
            Check(result.FullyCompiled && result.DetectedKeywords.SequenceEqual(innateKeywords), "same innate keywords in both editions");
        }
        foreach (var text in new[] { "【吟唱_3】", "〖倒數 ３〗" })
        {
            var result = Compile(text + "\n消费1【启动】使这张卡片的倒数回合数-1。", cardType: "Amulet");
            Check(result.FullyCompiled && result.InitialCountdown == 3 && result.DetectedKeywords.SequenceEqual(new[] { "Countdown" }),
                "initial countdown is independent of activation");
        }
        Check(Condition(Config(Compile("【入场曲】【连击_3】指定1张敌方战场上的从者卡。给予其3点伤害。")))
            == "context.owner.combo >= 2", "top-level Fanfare combo checks before play");
        Check(Condition(Config(Compile("〖連擊〗３抽1张牌。", cardType: "Spell"))) == "context.owner.combo >= 2",
            "top-level spell combo checks before play");
        Check(Condition(Config(Compile("【入场曲】【连击_1】抽1张牌。"))) == "context.owner.combo >= 0", "first play counts itself");
        Check(Condition(Config(Compile("〖攻擊時〗〖連擊_３〗抽1张牌。"))) == "context.owner.combo >= 3",
            "other triggers check the current combo");
        var chainConfig = Config(Compile("〖入場曲〗抽1张牌。\n〖連擊_３〗指定1張敵方戰場上的從者卡。給予其3點傷害。抽2张牌。,【连击_5】抽3张牌。"));
        var chain = chainConfig.GetProperty("effects")[0].GetProperty("chainedAbilities");
        Check(chainConfig.GetProperty("conditions").GetArrayLength() == 0 && chain.GetArrayLength() == 2,
            "combo branches don't gate the first effect");
        Check(Condition(chain[0]) == "context.owner.combo >= 3" && Condition(chain[1]) == "context.owner.combo >= 5",
            "each chained combo checks the post-play count");
        Check(chain[0].GetProperty("trigger").GetProperty("eventTypes")[0].GetString() == "EffectCompleted"
            && Effect(chain[0], 0) == "Damage" && Effect(chain[0], 1) == "Draw"
            && chain[0].GetProperty("effects")[0].GetProperty("targetSelector")[0].GetString() == "target1",
            "chained effects and their targets stay inside the combo branch");
        var unknownChain = Compile("【入场曲】抽1张牌。【连击_3】改为抽2张牌。抽3张牌。");
        Check(!unknownChain.FullyCompiled && !unknownChain.Abilities.Single().IsActive, "unsupported replacement stays inactive");
        var turn = Config(Compile("敵方的回合結束時，給予敵方的主戰者2點傷害。"));
        Check(turn.GetProperty("conditions")[0].GetProperty("expression").GetString() ==
            "context.triggeringEvent.EndingPlayer.playerId == context.opponent.playerId", "opponent turn condition");
        var summon = Config(Compile("〖入場曲〗召喚1張『一张從者，示例』到自己的戰場上。", cardId: 7));
        Check(Effect(summon, 0) == "Summon" && Param(summon, 0, "CardDefinitionId")[0].GetInt32() == 8, "exact catalog name and scope");
        Check(!Compile("〖入場曲〗召喚1張『一张從者，示例』到自己的戰場上。").FullyCompiled, "ambiguous catalog match");
        Check(!Compile("〖入场曲〗指定1个〖模式〗并发动该能力。\n（1）由自己的牌堆中抽取1张卡片。").FullyCompiled,
            "mode option cannot become a normal draw");
        var partial = Compile("〖入場曲〗由自己的牌堆中抽取1張卡片。若為進化後，使其消失。");
        Check(!partial.FullyCompiled && partial.Abilities.Count == 1 && !partial.Abilities[0].IsActive, "unknown conditional stays inactive");
        Check(!Compile("〖入場曲〗「由自己的牌堆中抽取1張卡片。").FullyCompiled, "unclosed quotation stays unrecognized");

        var multilinePaidAct = Compile("消费1【启动】\n抽1张牌。\n抽2张牌。", cardType: "Amulet");
        var multilineActConfig = Config(multilinePaidAct);
        var multilineActKeywords = AbilityConfigJson.ReadKeywords(multilinePaidAct.Abilities.Single().JsonConfig);
        Check(multilineActKeywords.Count == 1 && multilineActKeywords[0].Keyword == AbilityKeyword.Act && multilineActKeywords[0].Value == 1,
            "multiline paid Act stays a single paid ability");
        Check(Effect(multilineActConfig, 0) == "Draw" && Param(multilineActConfig, 0, "DrawAmount").GetInt32() == 1
            && Effect(multilineActConfig, 1) == "Draw" && Param(multilineActConfig, 1, "DrawAmount").GetInt32() == 2,
            "multiline paid Act preserves all continuation effects");

        var cornerBracketTokens = Config(Compile("〖入場曲〗召喚1個「一张從者，示例」到戰場上。增加2張「一张從者，示例」到手牌中。", cardId: 7));
        Check(Effect(cornerBracketTokens, 0) == "Summon" && Param(cornerBracketTokens, 0, "SummonCount").GetInt32() == 1
            && Effect(cornerBracketTokens, 1) == "GetToken" && Param(cornerBracketTokens, 1, "Amount").GetInt32() == 2,
            "corner-bracket token names and implicit self destination compile");

        DslController.DslGenerateFromCardResponse Compile(string description, int? cardId = null, string cardType = "Follower")
        {
            var result = controller.GenerateFromCard(new() { CardId = cardId, CardType = cardType, Name = "范例", Description = description });
            return (DslController.DslGenerateFromCardResponse)((OkObjectResult)result.Result!).Value!;
        }
    }

    private static JsonElement Config(DslController.DslGenerateFromCardResponse result)
    {
        Check(result.FullyCompiled, $"Unrecognized: {string.Join(" / ", result.UnrecognizedClauses)}");
        return JsonSerializer.Deserialize<JsonElement>(result.Abilities.Single().JsonConfig);
    }

    private static string? Effect(JsonElement config, int i) => config.GetProperty("effects")[i].GetProperty("type").GetString();

    private static string? Condition(JsonElement config) => config.GetProperty("conditions")[0].GetProperty("expression").GetString();

    private static JsonElement Param(JsonElement config, int i, string name) =>
        config.GetProperty("effects")[i].GetProperty("parameters").GetProperty(name).GetProperty("value");

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
