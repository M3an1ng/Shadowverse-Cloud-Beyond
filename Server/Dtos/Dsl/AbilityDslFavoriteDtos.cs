using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Dsl;

public record AbilityDslFavoriteDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public string? ExpectedType { get; init; }
    public List<string> Tags { get; init; } = new();
    public int UsageCount { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateAbilityDslFavoriteRequest
{
    public Guid UserId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public string? ExpectedType { get; init; }
    public List<string>? Tags { get; init; }
}

public record UpdateAbilityDslFavoriteRequest
{
    public Guid UserId { get; init; }
    public string? Label { get; init; }
    public List<string>? Tags { get; init; }
    public string? ExpectedType { get; init; }
}

public record DslFavoriteSearchResultDto
{
    public int Total { get; init; }
    public int Page { get; init; }
    public int Limit { get; init; }
    public List<AbilityDslFavoriteDto> Items { get; init; } = new();
}
