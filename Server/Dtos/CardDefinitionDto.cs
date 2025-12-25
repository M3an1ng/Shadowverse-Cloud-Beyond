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
        public string CardSet { get; init; } = "Basic";

        [JsonPropertyName("class")]
        public string Class { get; init; } = "Neutral";

        [JsonPropertyName("cost")]
        public required int Cost { get; init; }

        [JsonPropertyName("attack")]
        public int? Attack { get; init; }

        [JsonPropertyName("health")]
        public int? Health { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("can_evolve")]
        public bool? CanEvolve { get; init; }

        [JsonPropertyName("evolve_id")]
        public int? EvolveId { get; init; }

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