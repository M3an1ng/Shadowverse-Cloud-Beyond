using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions;

/// <summary>
/// Evaluates whether a finished match is official.
/// The officiality semantics MUST stay aligned with coin reward eligibility.
/// </summary>
public interface IOfficialMatchEvaluator
{
    Task<OfficialMatchEvaluation> EvaluateAsync(
        GameState gameState,
        GameEndEvent gameEndEvent,
        CancellationToken cancellationToken);
}

public sealed record OfficialMatchEvaluation(
    bool IsOfficial,
    string? NonOfficialReason);
