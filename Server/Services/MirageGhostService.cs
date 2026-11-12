using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record MirageGhostSnapshot
    {
        public Guid GhostId { get; init; } = Guid.NewGuid();
        public int RoundNumber { get; init; }
        public int TavernTier { get; init; }
        public Guid SourcePlayerId { get; init; }
        public string AuthorDisplayName { get; init; } = string.Empty;
        public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
        public JsonElement SerializedSideSnapshot { get; init; }
    }

    public interface IMirageGhostService
    {
        MirageGhostSnapshot RecordGhostSnapshot(int roundNumber, MirageCombatSideSnapshot sideSnapshot);
        MirageGhostSnapshot RecordPlayerGhost(GameState gameState, Guid playerId, int? roundNumberOverride = null);
        IReadOnlyList<MirageGhostSnapshot> GetGhostsForRound(int roundNumber);
        MirageGhostSnapshot? GetGhostById(Guid ghostId);
        MirageCombatSideSnapshot? TryGetGhostCombatSide(
            int roundNumber,
            PlayerState targetOpponentPlayer,
            Guid? excludeSourcePlayerId = null,
            Guid? specificGhostId = null);
        MirageCombatSideSnapshot GetOrCreateGhostCombatSide(
            GameState mainGameState,
            PlayerState targetOpponentPlayer,
            int roundNumber,
            Guid? excludeSourcePlayerId = null,
            Guid? specificGhostId = null);
        void ClearAllGhosts();
    }

    public sealed class MirageGhostService : IMirageGhostService
    {
        private const int MaxGhostsPerRound = 64;

        private readonly IMirageCombatSnapshotBuilder _snapshotBuilder;
        private readonly IMirageShopService _shopService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<int, List<MirageGhostSnapshot>> _ghostsByRound = new();
        private readonly ConcurrentDictionary<Guid, MirageGhostSnapshot> _ghostsById = new();
        private readonly object _syncLock = new();

        public MirageGhostService(
            IMirageCombatSnapshotBuilder snapshotBuilder,
            IMirageShopService shopService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
            _shopService = shopService ?? throw new ArgumentNullException(nameof(shopService));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public MirageGhostSnapshot RecordGhostSnapshot(int roundNumber, MirageCombatSideSnapshot sideSnapshot)
        {
            if (sideSnapshot == null) throw new ArgumentNullException(nameof(sideSnapshot));
            if (!IsSnapshotValidForMirage(sideSnapshot))
            {
                throw new InvalidOperationException("Cannot record Mirage ghost snapshot containing non-Mirage card definitions.");
            }

            var normalizedRound = Math.Max(1, roundNumber);
            var serialized = JsonSerializer.SerializeToElement(sideSnapshot, ReplayExecutionState.Json);
            var ghost = new MirageGhostSnapshot
            {
                GhostId = Guid.NewGuid(),
                RoundNumber = normalizedRound,
                TavernTier = Math.Max(1, sideSnapshot.TavernTier),
                SourcePlayerId = sideSnapshot.PlayerId,
                AuthorDisplayName = sideSnapshot.Name ?? string.Empty,
                CapturedAtUtc = DateTime.UtcNow,
                SerializedSideSnapshot = serialized
            };

            lock (_syncLock)
            {
                var list = _ghostsByRound.GetOrAdd(normalizedRound, static _ => new List<MirageGhostSnapshot>());
                list.Add(ghost);
                while (list.Count > MaxGhostsPerRound)
                {
                    var evicted = list[0];
                    list.RemoveAt(0);
                    _ghostsById.TryRemove(evicted.GhostId, out _);
                }
                _ghostsById[ghost.GhostId] = ghost;
            }

            return ghost;
        }

        public MirageGhostSnapshot RecordPlayerGhost(GameState gameState, Guid playerId, int? roundNumberOverride = null)
        {
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));
            var sideSnapshot = _snapshotBuilder.CaptureSideSnapshot(gameState, playerId);
            var roundNumber = roundNumberOverride ?? gameState.Mirage?.RoundNumber ?? 1;
            return RecordGhostSnapshot(roundNumber, sideSnapshot);
        }

        public IReadOnlyList<MirageGhostSnapshot> GetGhostsForRound(int roundNumber)
        {
            var normalizedRound = Math.Max(1, roundNumber);
            lock (_syncLock)
            {
                if (_ghostsByRound.TryGetValue(normalizedRound, out var list))
                {
                    return list.ToList();
                }
                return Array.Empty<MirageGhostSnapshot>();
            }
        }

        public MirageGhostSnapshot? GetGhostById(Guid ghostId)
        {
            if (ghostId == Guid.Empty) return null;
            return _ghostsById.TryGetValue(ghostId, out var ghost) ? ghost : null;
        }

        public MirageCombatSideSnapshot? TryGetGhostCombatSide(
            int roundNumber,
            PlayerState targetOpponentPlayer,
            Guid? excludeSourcePlayerId = null,
            Guid? specificGhostId = null)
        {
            if (targetOpponentPlayer == null) throw new ArgumentNullException(nameof(targetOpponentPlayer));

            MirageGhostSnapshot? selected = null;
            if (specificGhostId.HasValue && specificGhostId.Value != Guid.Empty)
            {
                selected = GetGhostById(specificGhostId.Value);
            }
            else
            {
                var normalizedRound = Math.Max(1, roundNumber);
                var candidates = GetGhostsForRound(normalizedRound);
                if (candidates.Count > 0)
                {
                    // Auto-matching must strictly exclude the player's own ghost snapshots;
                    // if no other player's ghost exists for this round, return null so cold-start runs.
                    var filtered = excludeSourcePlayerId.HasValue
                        ? candidates.Where(g => g.SourcePlayerId != excludeSourcePlayerId.Value).ToList()
                        : candidates.ToList();
                    if (filtered.Count > 0)
                    {
                        selected = filtered[Random.Shared.Next(filtered.Count)];
                    }
                }
            }

            if (selected == null)
            {
                return null;
            }

            return DeserializeAndIsolateGhostSnapshot(selected, targetOpponentPlayer);
        }

        private static MirageCombatSideSnapshot? DeserializeAndIsolateGhostSnapshot(
            MirageGhostSnapshot selected,
            PlayerState targetOpponentPlayer)
        {
            var initial = selected.SerializedSideSnapshot.Deserialize<MirageCombatSideSnapshot>(ReplayExecutionState.Json);
            if (initial == null || !IsSnapshotValidForMirage(initial))
            {
                return null;
            }

            // Build a complete internal ID remap table for the Ghost side so that even if the ghost
            // originated from the same player or match, its card/ability/buff IDs are 100% disjoint
            // from any live side before entering PopulateSandboxState.
            var ghostIdMap = new Dictionary<Guid, Guid>();
            void MapId(Guid id)
            {
                if (id != Guid.Empty && !ghostIdMap.ContainsKey(id))
                {
                    ghostIdMap[id] = Guid.NewGuid();
                }
            }

            MapId(initial.LeaderOriginalInstanceId);
            foreach (var card in initial.Cards)
            {
                MapId(card.OriginalInstanceId);
                if (card.RuntimeState?.OriginInstanceId is { } originId)
                {
                    MapId(originId);
                }
                foreach (var fusedId in card.FusedCardIds)
                {
                    MapId(fusedId);
                }
            }

            foreach (var ability in initial.Abilities)
            {
                MapId(ability.OriginalRuntimeId);
                if (ability.OriginalSourceCardId.HasValue) MapId(ability.OriginalSourceCardId.Value);
                if (ability.OriginalSourceBuffId.HasValue) MapId(ability.OriginalSourceBuffId.Value);
                if (ability.ResumeState.TryGetProperty("summaryRootAbilityId", out var rootProp)
                    && rootProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(rootProp.GetString(), out var rootId))
                {
                    MapId(rootId);
                }
            }

            foreach (var buff in initial.Buffs)
            {
                MapId(buff.OriginalBuffId);
                if (buff.SerializedBuff.TryGetProperty("sourceAbilityId", out var saProp)
                    && saProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(saProp.GetString(), out var saId))
                {
                    MapId(saId);
                }
                if (buff.SerializedBuff.TryGetProperty("summaryRootAbilityId", out var srProp)
                    && srProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(srProp.GetString(), out var srId))
                {
                    MapId(srId);
                }
                if (buff.SerializedBuff.TryGetProperty("sourceCardInstanceId", out var scProp)
                    && scProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(scProp.GetString(), out var scId))
                {
                    MapId(scId);
                }
            }

            MirageCombatSideSnapshot isolated = initial;
            if (ghostIdMap.Count > 0)
            {
                var rawNode = JsonNode.Parse(selected.SerializedSideSnapshot.GetRawText());
                if (rawNode != null)
                {
                    var remappedNode = ReplayExecutionState.Remap(rawNode, ghostIdMap);
                    var remappedDeserialized = remappedNode.Deserialize<MirageCombatSideSnapshot>(ReplayExecutionState.Json);
                    if (remappedDeserialized != null)
                    {
                        isolated = remappedDeserialized;
                    }
                }
            }

            var displayName = !string.IsNullOrWhiteSpace(selected.AuthorDisplayName)
                ? $"{selected.AuthorDisplayName} (幽灵镜像)"
                : (string.IsNullOrWhiteSpace(targetOpponentPlayer.DisplayName) ? "幽灵镜像" : targetOpponentPlayer.DisplayName);

            return isolated with
            {
                PlayerId = targetOpponentPlayer.PlayerId,
                Name = displayName,
                IsAI = targetOpponentPlayer.IsAI,
                Cards = isolated.Cards
                    .Select(c => c with
                    {
                        RuntimeState = c.RuntimeState with { OriginInstanceId = null }
                    })
                    .ToList()
            };
        }

        public MirageCombatSideSnapshot GetOrCreateGhostCombatSide(
            GameState mainGameState,
            PlayerState targetOpponentPlayer,
            int roundNumber,
            Guid? excludeSourcePlayerId = null,
            Guid? specificGhostId = null)
        {
            var fromPool = TryGetGhostCombatSide(roundNumber, targetOpponentPlayer, excludeSourcePlayerId, specificGhostId);
            if (fromPool != null)
            {
                return fromPool;
            }

            var baseSide = _snapshotBuilder.CaptureSideSnapshot(mainGameState, targetOpponentPlayer.PlayerId);
            if (baseSide.ZoneCardIds.TryGetValue("Field", out var fieldIds) && fieldIds.Count > 0)
            {
                return baseSide with
                {
                    Cards = baseSide.Cards
                        .Select(c => c with { RuntimeState = c.RuntimeState with { OriginInstanceId = null } })
                        .ToList()
                };
            }

            // Cold-start fallback: construct an isolated temporary GameState and create followers via
            // ICardFactory.CreateAndRegisterNewCard so their native definition abilities and counters
            // are registered and captured by CaptureSideSnapshot.
            var rules = mainGameState.Mirage?.Rules ?? new MirageRuleConfig();
            var targetTier = Math.Clamp(1 + (roundNumber - 1) / 2, 1, rules.MaxTavernTier);
            var followerCount = Math.Clamp(roundNumber, 1, 5);
            var pool = _shopService.GetPoolUpToTier(targetTier, rules.MaxTavernTier);
            if (pool.Count == 0)
            {
                return baseSide;
            }

            using var coldScope = _serviceScopeFactory.CreateScope();
            var sp = coldScope.ServiceProvider;
            var accessor = sp.GetRequiredService<IGameContextAccessor>();
            using var suspend = accessor.SuspendAmbient();
            using var guard = SandboxExecutionGuard.Enter();

            var tempAbilityManager = sp.GetRequiredService<AbilityManager>();
            var tempBuffManager = sp.GetRequiredService<BuffManager>();
            var tempAuraManager = ActivatorUtilities.CreateInstance<AuraManager>(sp);
            var cardFactory = sp.GetRequiredService<ICardFactory>();

            var dummyPlayer = new PlayerState(Guid.NewGuid())
            {
                DisplayName = "Dummy",
                IsAI = true
            };
            var tempOpponent = new PlayerState(targetOpponentPlayer.PlayerId)
            {
                DisplayName = string.IsNullOrWhiteSpace(targetOpponentPlayer.DisplayName) ? "幽灵镜像" : targetOpponentPlayer.DisplayName,
                Class = targetOpponentPlayer.Class,
                Environment = targetOpponentPlayer.Environment,
                IsAI = targetOpponentPlayer.IsAI,
                TurnNumber = Math.Max(1, roundNumber),
                Mirage = new MiragePlayerState
                {
                    TavernTier = targetTier
                }
            };

            var tempGame = new GameState($"mirage-cold-ghost-{Guid.NewGuid():N}", dummyPlayer, tempOpponent, tempOpponent.PlayerId)
            {
                Status = GameStatus.InProgress,
                Phase = GamePhase.Recruit,
                Mode = GameMode.Mirage,
                IsCombatSandbox = true,
                Mirage = new MirageMatchState
                {
                    RoundNumber = Math.Max(1, roundNumber)
                },
                AbilityManager = tempAbilityManager,
                BuffManager = tempBuffManager,
                AuraManager = tempAuraManager
            };

            dummyPlayer.Leader = cardFactory.CreateAndRegisterNewCard(
                tempGame,
                ShadowverseBeyond.Server.Domain.Constants.GameConstants.Cards.MirageLeaderCardId,
                dummyPlayer.PlayerId,
                CardZone.Field);
            tempOpponent.Leader = cardFactory.CreateAndRegisterNewCard(
                tempGame,
                targetOpponentPlayer.Leader?.BaseDefinitionId ?? ShadowverseBeyond.Server.Domain.Constants.GameConstants.Cards.MirageLeaderCardId,
                tempOpponent.PlayerId,
                CardZone.Field);

            for (int i = 0; i < followerCount; i++)
            {
                var entry = pool[Random.Shared.Next(pool.Count)];
                var follower = cardFactory.CreateAndRegisterNewCard(tempGame, entry.CardDefinitionId, tempOpponent.PlayerId, CardZone.Field);
                follower.IsMirageRecruited = true;
                follower.CostOverride = 0;
                follower.OriginInstanceId = null;
            }

            var capturedColdSide = _snapshotBuilder.CaptureSideSnapshot(tempGame, tempOpponent.PlayerId);
            return capturedColdSide with
            {
                Cards = capturedColdSide.Cards
                    .Select(c => c with
                    {
                        RuntimeState = c.RuntimeState with { OriginInstanceId = null }
                    })
                    .ToList()
            };
        }

        public void ClearAllGhosts()
        {
            lock (_syncLock)
            {
                _ghostsByRound.Clear();
                _ghostsById.Clear();
            }
        }

        private static bool IsSnapshotValidForMirage(MirageCombatSideSnapshot snapshot)
        {
            if (snapshot?.Cards == null || snapshot.Cards.Count == 0)
            {
                return false;
            }

            var provider = GameDataProvider.Instance;
            if (provider == null || !provider.IsInitialized)
            {
                return true;
            }

            foreach (var card in snapshot.Cards)
            {
                var baseDef = provider.GetCardDefinition(card.BaseDefinitionId);
                if (baseDef == null || !baseDef.IsMirageCard)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
