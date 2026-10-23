// File: Server/Dtos/CardDefinitionDto.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos
{
    public record CardDefinitionDto
    {
        [JsonPropertyName("id")]
        public required int CardId { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("type")]
        public required CardType Type { get; init; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; init; } = "Common";

        [JsonPropertyName("card_set")]
        public string CardSet { get; init; } = "Legacy";

        [JsonPropertyName("card_set_definition_id")]
        public int? CardSetDefinitionId { get; init; }

        [JsonPropertyName("card_set_code")]
        public string? CardSetCode { get; init; }

        [JsonPropertyName("card_set_abbr")]
        public string? CardSetAbbr { get; init; }

        [JsonPropertyName("card_set_environment")]
        public List<DeckEnvironment>? CardSetEnvironment { get; init; }

        [JsonPropertyName("class")]
        public string Class { get; init; } = "Neutral";

        [JsonPropertyName("cost")]
        public required int Cost { get; init; }

        [JsonPropertyName("attack")]
        public int? Attack { get; init; }

        [JsonPropertyName("health")]
        public int? Health { get; init; }

        [JsonPropertyName("countdown")]
        public int? Countdown { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("artPreview")]
        public string? ArtPreview { get; init; }

        [JsonPropertyName("artDetail")]
        public string? ArtDetail { get; init; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; init; }

        [JsonPropertyName("updatedBy")]
        public string? UpdatedBy { get; init; }

        [JsonPropertyName("isCollectible")]
        public bool IsCollectible { get; init; } = true;

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; init; } = new List<string>();

        [JsonPropertyName("tribe")]
        public List<string>? Tribe { get; init; }

        [JsonPropertyName("effects")]
        public List<CardEffectDto>? Effects { get; init; }
    }

    /// <summary>
    /// 简单的卡牌效果DTO，用于从JSON数据中读取效果信息
    /// </summary>
    public record CardEffectDto
    {
        [JsonPropertyName("trigger")]
        public string Trigger { get; init; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public int? Value { get; init; }

        [JsonPropertyName("target")]
        public string? Target { get; init; }
    }
}
