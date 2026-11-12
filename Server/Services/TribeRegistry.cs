using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class TribeRegistry : ITribeRegistry
    {
        private sealed record Snapshot(
            IReadOnlyDictionary<string, TribeDefinition> DefinitionsByLookup,
            IReadOnlyDictionary<string, TribeDefinition> DefinitionsByName,
            IReadOnlyCollection<string> StandardKnownNormalTribes,
            IReadOnlyCollection<string> MirageKnownNormalTribes)
        {
            public static Snapshot Empty { get; } = new(
                new Dictionary<string, TribeDefinition>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, TribeDefinition>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ILogger<TribeRegistry> _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private Snapshot _snapshot = Snapshot.Empty;
        private volatile bool _isInitialized;

        public TribeRegistry(
            IServiceScopeFactory scopeFactory,
            IGameDataProvider gameDataProvider,
            ILogger<TribeRegistry> logger)
        {
            _scopeFactory = scopeFactory;
            _gameDataProvider = gameDataProvider;
            _logger = logger;
        }

        public string Normalize(string? raw)
        {
            EnsureInitialized();

            var trimmed = raw?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot.DefinitionsByLookup.TryGetValue(trimmed, out var definition)
                ? definition.Name
                : trimmed;
        }

        public IReadOnlyList<string> NormalizeMany(IEnumerable<string>? rawValues)
        {
            EnsureInitialized();
            return CanonicalizeMany(rawValues, Volatile.Read(ref _snapshot));
        }

        public IReadOnlyCollection<string> GetKnownNormalTribes(GameMode mode = GameMode.Standard)
        {
            EnsureInitialized();
            var snapshot = Volatile.Read(ref _snapshot);
            return mode == GameMode.Mirage
                ? snapshot.MirageKnownNormalTribes
                : snapshot.StandardKnownNormalTribes;
        }

        public bool TryGetDefinition(string? raw, out TribeDefinition definition)
        {
            EnsureInitialized();

            var trimmed = raw?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                definition = null!;
                return false;
            }

            return Volatile.Read(ref _snapshot).DefinitionsByLookup.TryGetValue(trimmed, out definition!);
        }

        public async Task<IReadOnlyList<string>> NormalizeAndRegisterAsync(
            IEnumerable<string>? rawValues,
            CancellationToken cancellationToken = default,
            bool isMirageScope = false)
        {
            await RefreshIfNeededAsync(cancellationToken);

            var normalizedValues = CanonicalizeMany(rawValues, Volatile.Read(ref _snapshot));
            if (normalizedValues.Count == 0 || isMirageScope)
            {
                return normalizedValues;
            }

            var snapshot = Volatile.Read(ref _snapshot);
            var missing = normalizedValues
                .Where(name => !snapshot.DefinitionsByName.ContainsKey(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missing.Count == 0)
            {
                return normalizedValues;
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var changed = false;

            foreach (var tribeName in missing)
            {
                var definition = BuildDefaultDefinition(tribeName);
                var existing = await dbContext.TribeDefinitions.FindAsync([definition.Name], cancellationToken);
                if (existing != null)
                {
                    continue;
                }

                dbContext.TribeDefinitions.Add(definition);
                changed = true;
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await RefreshAsync(cancellationToken);
            }

            return CanonicalizeMany(normalizedValues, Volatile.Read(ref _snapshot));
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var storedDefinitions = await dbContext.TribeDefinitions
                    .AsNoTracking()
                    .Where(definition => definition.IsEnabled)
                    .ToListAsync(cancellationToken);

                var byName = new Dictionary<string, TribeDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var definition in storedDefinitions)
                {
                    var normalizedName = NormalizeStoredValue(definition.Name);
                    if (string.IsNullOrWhiteSpace(normalizedName))
                    {
                        continue;
                    }

                    definition.Name = normalizedName;
                    definition.Aliases = NormalizeAliasList(definition.Aliases);
                    byName[definition.Name] = definition;
                }

                if (!byName.ContainsKey(GameConstants.Tribes.MatchAll))
                {
                    byName[GameConstants.Tribes.MatchAll] = BuildMatchAllDefinition();
                }

                var standardKnownNormalTribes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var mirageKnownNormalTribes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var definition in byName.Values)
                {
                    if (definition.Behavior == TribeBehavior.Normal)
                    {
                        standardKnownNormalTribes.Add(definition.Name);
                    }
                }

                foreach (var card in _gameDataProvider.Cards)
                {
                    if (card == null || card.IsMirageCard) continue;
                    foreach (var tribe in TrimDistinct(card.Tribe))
                    {
                        if (!byName.TryGetValue(tribe, out var definition))
                        {
                            definition = BuildDefaultDefinition(tribe);
                            byName[tribe] = definition;
                        }

                        if (definition.Behavior == TribeBehavior.Normal)
                        {
                            standardKnownNormalTribes.Add(definition.Name);
                        }
                    }
                }

                var mirageCards = (_gameDataProvider as IEnvironmentScopedGameDataProvider)?.GetMirageCardDefinitions()
                    ?? Array.Empty<CardDefinition>();
                foreach (var card in mirageCards)
                {
                    if (card == null || !card.IsMirageCard) continue;
                    foreach (var tribe in TrimDistinct(card.Tribe))
                    {
                        if (byName.TryGetValue(tribe, out var definition))
                        {
                            if (definition.Behavior == TribeBehavior.Normal)
                            {
                                mirageKnownNormalTribes.Add(definition.Name);
                            }
                        }
                        else
                        {
                            mirageKnownNormalTribes.Add(tribe);
                        }
                    }
                }

                var byLookup = new Dictionary<string, TribeDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var definition in byName.Values)
                {
                    byLookup[definition.Name] = definition;
                    foreach (var alias in definition.Aliases)
                    {
                        byLookup[alias] = definition;
                    }
                }

                Volatile.Write(
                    ref _snapshot,
                    new Snapshot(
                        byLookup,
                        byName,
                        standardKnownNormalTribes.ToList(),
                        mirageKnownNormalTribes.ToList()));
                _isInitialized = true;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            RefreshAsync().GetAwaiter().GetResult();
        }

        private Task RefreshIfNeededAsync(CancellationToken cancellationToken)
        {
            return _isInitialized ? Task.CompletedTask : RefreshAsync(cancellationToken);
        }

        private static IReadOnlyList<string> CanonicalizeMany(IEnumerable<string>? rawValues, Snapshot snapshot)
        {
            var normalized = new List<string>();
            foreach (var rawValue in TrimDistinct(rawValues))
            {
                var canonical = snapshot.DefinitionsByLookup.TryGetValue(rawValue, out var definition)
                    ? definition.Name
                    : rawValue;

                if (normalized.Any(existing => string.Equals(existing, canonical, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                normalized.Add(canonical);
            }

            return normalized;
        }

        private static IReadOnlyList<string> TrimDistinct(IEnumerable<string>? rawValues)
        {
            if (rawValues == null)
            {
                return Array.Empty<string>();
            }

            var normalized = new List<string>();
            foreach (var rawValue in rawValues)
            {
                var trimmed = NormalizeStoredValue(rawValue);
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (normalized.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                normalized.Add(trimmed);
            }

            return normalized;
        }

        private static TribeDefinition BuildDefaultDefinition(string name)
        {
            if (string.Equals(name, GameConstants.Tribes.MatchAll, StringComparison.OrdinalIgnoreCase))
            {
                return BuildMatchAllDefinition();
            }

            return new TribeDefinition
            {
                Name = name,
                Behavior = TribeBehavior.Normal,
                IsEnabled = true,
                IsReserved = false
            };
        }

        private static TribeDefinition BuildMatchAllDefinition()
        {
            return new TribeDefinition
            {
                Name = GameConstants.Tribes.MatchAll,
                Behavior = TribeBehavior.MatchAllKnownTribes,
                IsEnabled = true,
                IsReserved = true
            };
        }

        private static string NormalizeStoredValue(string? raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }

        private static List<string> NormalizeAliasList(IEnumerable<string>? aliases)
        {
            if (aliases == null)
            {
                return new List<string>();
            }

            return aliases
                .Select(NormalizeStoredValue)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
