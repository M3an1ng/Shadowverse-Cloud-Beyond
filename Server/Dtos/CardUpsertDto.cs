using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// 用于创建/更新卡牌定义的输入 DTO
    /// </summary>
    public record CardUpsertDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; init; } = "Bronze";

        [JsonPropertyName("card_set")]
        public string CardSet { get; init; } = "Legacy";

        [JsonPropertyName("card_set_definition_id")]
        public int? CardSetDefinitionId { get; init; }

        [JsonPropertyName("card_pack_definition_id")]
        public int? CardPackDefinitionId { get; init; }

        [JsonPropertyName("card_pack_code")]
        public string? CardPackCode { get; init; }

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

        [JsonPropertyName("isCollectible")]
        public bool? IsCollectible { get; init; }

        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; init; }

        [JsonPropertyName("tribe")]
        public List<string>? Tribe { get; init; }

        [JsonPropertyName("abilityIds")]
        public List<long>? AbilityIds { get; init; }
    }
}
