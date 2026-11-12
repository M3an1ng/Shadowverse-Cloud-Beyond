using System;
using System.Collections.Generic;
using System.Text.Json;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Dtos
{
    public sealed record MirageCombatStepDto
    {
        public int StepIndex { get; init; }
        public Guid AttackerInstanceId { get; init; }
        public Guid DefenderInstanceId { get; init; }
        public Guid AttackerOwnerPlayerId { get; init; }
        public List<JsonElement> Events { get; init; } = new();
        public GameStateDto PostStepState { get; init; } = null!;
    }

    public sealed record MiragePermanentGrowthEntry
    {
        public Guid PlayerId { get; init; }
        public Guid OriginInstanceId { get; init; }
        public JsonElement SerializedBuff { get; init; }
    }

    public sealed record MirageCombatOutcomeDto
    {
        public Guid? WinnerPlayerId { get; init; }
        public Guid? LoserPlayerId { get; init; }
        public int LeaderDamage { get; init; }
        public bool IsTie => !WinnerPlayerId.HasValue || LeaderDamage <= 0;
        public List<MiragePermanentGrowthEntry> PermanentGrowthLog { get; init; } = new();
    }

    public sealed record MirageCombatReplayDto
    {
        public Guid CombatId { get; init; }
        public int RoundNumber { get; init; }
        public Guid FirstAttackerPlayerId { get; init; }
        public GameStateDto InitialState { get; init; } = null!;
        public List<MirageCombatStepDto> Steps { get; init; } = new();
        public MirageCombatOutcomeDto Outcome { get; init; } = new();
    }

    public sealed record MirageCommitCombatActionDto
    {
        public Guid CombatId { get; init; }
        public int RoundNumber { get; init; }
        public int StepCount { get; init; }
        public MirageCombatOutcomeDto Outcome { get; init; } = new();
    }
}
