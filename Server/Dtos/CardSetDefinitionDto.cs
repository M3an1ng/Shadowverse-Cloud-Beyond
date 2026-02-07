using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// Card set definition data transfer object.
/// </summary>
public class CardSetDefinitionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public int Region { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("set_code")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public List<DeckEnvironment> Environments { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    public static CardSetDefinitionDto FromEntity(CardSetDefinition definition)
    {
        return new CardSetDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Abbreviation = definition.Abbreviation,
            Region = definition.Region,
            Sequence = definition.Sequence,
            SetCode = definition.SetCode,
            Environments = definition.Environments ?? new List<DeckEnvironment>(),
            CreatedAt = definition.CreatedAt
        };
    }
}
