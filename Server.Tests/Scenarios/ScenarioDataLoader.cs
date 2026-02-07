using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public sealed class ScenarioDataSet
    {
        public IReadOnlyDictionary<int, CardDefinition> CardDefinitions { get; }
        public IReadOnlyDictionary<int, List<AbilityConfig>> AbilityConfigsByCard { get; }
        public TestGameDataProvider DataProvider { get; }

        public ScenarioDataSet(
            IReadOnlyDictionary<int, CardDefinition> cardDefinitions,
            IReadOnlyDictionary<int, List<AbilityConfig>> abilityConfigsByCard,
            TestGameDataProvider dataProvider)
        {
            CardDefinitions = cardDefinitions;
            AbilityConfigsByCard = abilityConfigsByCard;
            DataProvider = dataProvider;
        }
    }

    public static class ScenarioDataLoader
    {
        private static readonly Lazy<ScenarioDataSet> Cached = new(LoadInternal);

        public static ScenarioDataSet Load() => Cached.Value;

        private static ScenarioDataSet LoadInternal()
        {
            var dataRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
            var dataRoots = new[]
            {
                Path.Combine(dataRoot, "神眷的棋盘"),
                dataRoot
            };

            var files = dataRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "card_data_*.json"))
                .ToList();
            var legacyCardDataPath = Path.Combine(dataRoot, "card_data.json");
            if (File.Exists(legacyCardDataPath))
            {
                files.Add(legacyCardDataPath);
            }
            files = files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path)
                .ToList();
            if (files.Count == 0)
            {
                throw new FileNotFoundException($"No card_data_*.json files found in {string.Join(", ", dataRoots)}.");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            jsonOptions.Converters.Add(new LenientEffectKeywordConverter());

            var cardDefinitions = new Dictionary<int, CardDefinition>();
            var abilityConfigsByCard = new Dictionary<int, List<AbilityConfig>>();
            long nextAbilityId = 1;

            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<List<CardDataEntry>>(json, jsonOptions) ?? new List<CardDataEntry>();

                foreach (var entry in entries)
                {
                    if (!cardDefinitions.ContainsKey(entry.Id))
                    {
                        cardDefinitions[entry.Id] = BuildCardDefinition(entry);
                    }

                    if (entry.Abilities != null && entry.Abilities.Count > 0)
                    {
                        if (!abilityConfigsByCard.TryGetValue(entry.Id, out var list))
                        {
                            list = new List<AbilityConfig>();
                            abilityConfigsByCard[entry.Id] = list;
                        }

                        foreach (var ability in entry.Abilities)
                        {
                            ability.CardId = entry.Id;
                            ability.Id = nextAbilityId++;
                            list.Add(ability);
                        }
                    }
                }
            }

            EnsureLeaderDefinition(cardDefinitions);

            var provider = new TestGameDataProvider(cardDefinitions, abilityConfigsByCard);
            GameDataProvider.SetInstanceForTesting(provider);
            return new ScenarioDataSet(cardDefinitions, abilityConfigsByCard, provider);
        }

        private static CardDefinition BuildCardDefinition(CardDataEntry entry)
        {
            var cardType = Enum.TryParse<CardType>(entry.Type ?? string.Empty, true, out var parsedType)
                ? parsedType
                : CardType.Follower;
            var rarity = Enum.TryParse<CardRarity>(entry.Rarity ?? string.Empty, true, out var parsedRarity)
                ? parsedRarity
                : CardRarity.Bronze;
            var cardClass = Enum.TryParse<CardClass>(entry.Class ?? string.Empty, true, out var parsedClass)
                ? parsedClass
                : CardClass.Neutral;

            return new CardDefinition
            {
                Id = entry.Id,
                Name = entry.Name ?? string.Empty,
                Cost = entry.Cost,
                Type = cardType,
                Rarity = rarity,
                CardSet = entry.CardSet ?? "特殊",
                Attack = entry.Attack,
                Health = entry.Health,
                Countdown = entry.Countdown,
                Description = entry.Description ?? string.Empty,
                Keywords = entry.Keywords ?? new List<string>(),
                Tribe = entry.Tribe ?? new List<string>(),
                Class = cardClass,
                AbilityIds = new List<long>()
            };
        }

        private static void EnsureLeaderDefinition(Dictionary<int, CardDefinition> cardDefinitions)
        {
            const int leaderId = 0;
            if (cardDefinitions.ContainsKey(leaderId))
            {
                return;
            }

            cardDefinitions[leaderId] = new CardDefinition
            {
                Id = leaderId,
                Name = "Leader",
                Cost = 0,
                Type = CardType.Leader,
                Rarity = CardRarity.Special,
                CardSet = "System",
                Attack = 0,
                Health = 20,
                Description = string.Empty,
                Keywords = new List<string>(),
                Tribe = new List<string>(),
                Class = CardClass.Neutral,
                AbilityIds = new List<long>()
            };
        }

        private sealed class CardDataEntry
        {
            [JsonPropertyName("id")]
            public int Id { get; init; }

            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("card_set")]
            public string? CardSet { get; init; }

            [JsonPropertyName("class")]
            public string? Class { get; init; }

            [JsonPropertyName("rarity")]
            public string? Rarity { get; init; }

            [JsonPropertyName("type")]
            public string? Type { get; init; }

            [JsonPropertyName("cost")]
            public int Cost { get; init; }

            [JsonPropertyName("attack")]
            public int? Attack { get; init; }

            [JsonPropertyName("health")]
            public int? Health { get; init; }

            [JsonPropertyName("description")]
            public string? Description { get; init; }

            [JsonPropertyName("keywords")]
            public List<string>? Keywords { get; init; }

            [JsonPropertyName("tribe")]
            public List<string>? Tribe { get; init; }

            [JsonPropertyName("abilities")]
            public List<AbilityConfig>? Abilities { get; init; }

            [JsonPropertyName("countdown")]
            public int? Countdown { get; init; }
        }
    }

    public sealed class TestGameDataProvider : IGameDataProvider
    {
        private readonly Dictionary<int, CardDefinition> _cardDefinitions;
        private readonly Dictionary<int, List<AbilityConfig>> _abilityConfigsByCard;
        private readonly Dictionary<long, AbilityConfig> _abilityConfigsById;
        private readonly Dictionary<string, BuffDefinition> _buffDefinitions = new();
        private Guid _dataRevision = Guid.NewGuid();

        public bool IsInitialized { get; private set; } = true;
        public Guid DataRevision => _dataRevision;

        public TestGameDataProvider(
            IReadOnlyDictionary<int, CardDefinition> cardDefinitions,
            IReadOnlyDictionary<int, List<AbilityConfig>> abilityConfigsByCard)
        {
            _cardDefinitions = cardDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            _abilityConfigsByCard = abilityConfigsByCard.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(cfg => cfg).ToList());

            _abilityConfigsById = new Dictionary<long, AbilityConfig>();
            foreach (var list in _abilityConfigsByCard.Values)
            {
                foreach (var config in list)
                {
                    _abilityConfigsById[config.Id] = config;
                }
            }
        }

        public Task InitializeAsync()
        {
            IsInitialized = true;
            _dataRevision = Guid.NewGuid();
            return Task.CompletedTask;
        }

        public CardDefinition? GetCardDefinition(int cardId)
        {
            return _cardDefinitions.TryGetValue(cardId, out var definition) ? definition : null;
        }

        public IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions()
        {
            return _cardDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IReadOnlyCollection<CardDefinition> Cards => _cardDefinitions.Values.ToList();

        public List<AbilityConfig> GetAbilityConfigs(int cardId)
        {
            return _abilityConfigsByCard.TryGetValue(cardId, out var configs)
                ? configs.Select(cfg => cfg).ToList()
                : new List<AbilityConfig>();
        }

        public AbilityConfig? GetAbilityConfig(long abilityId)
        {
            return _abilityConfigsById.TryGetValue(abilityId, out var config) ? config : null;
        }

        public IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs()
        {
            return _abilityConfigsByCard.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(cfg => cfg).ToList());
        }

        public BuffDefinition? GetBuffDefinition(string buffId)
        {
            return _buffDefinitions.TryGetValue(buffId, out var definition) ? definition : null;
        }

        public Task RefreshCacheAsync()
        {
            IsInitialized = true;
            _dataRevision = Guid.NewGuid();
            return Task.CompletedTask;
        }

        public void ClearCache()
        {
            _cardDefinitions.Clear();
            _abilityConfigsByCard.Clear();
            _abilityConfigsById.Clear();
            _buffDefinitions.Clear();
            IsInitialized = false;
            _dataRevision = Guid.Empty;
        }

        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                CardDefinitionCount = _cardDefinitions.Count,
                AbilityConfigCount = _abilityConfigsById.Count,
                BuffDefinitionCount = _buffDefinitions.Count,
                LastInitialized = DateTime.UtcNow,
                InitializationDuration = TimeSpan.Zero,
                MemoryUsageBytes = 0,
                CacheHitCount = 0,
                CacheMissCount = 0
            };
        }
    }
}
