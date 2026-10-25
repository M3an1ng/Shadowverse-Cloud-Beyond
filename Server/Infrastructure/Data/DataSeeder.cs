using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities; // Added ShadowverseBeyond prefix
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder; // Needed for WebApplication extension method input
using System;                   // Needed for Exception
using Microsoft.Extensions.Hosting;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Infrastructure.Data // Added ShadowverseBeyond prefix
{
    public static class DataSeeder
    {
        private const int AbilityIndexWidth = 3;
        private static readonly string[] PreferredArtExtensions = { ".webp", ".png", ".jpg", ".jpeg" };
        private static readonly HashSet<string> SupportedArtExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".webp",
            ".png",
            ".jpg",
            ".jpeg"
        };

        private record CardPackSeedDto
        {
            [JsonPropertyName("id")]
            public int? Id { get; init; }

            [JsonPropertyName("name")]
            public string Name { get; init; } = string.Empty;

            [JsonPropertyName("abbreviation")]
            public string Abbreviation { get; init; } = string.Empty;

            [JsonPropertyName("region")]
            public int Region { get; init; }

            [JsonPropertyName("sequence")]
            public int Sequence { get; init; }

            [JsonPropertyName("environment")]
            public string Environment { get; init; } = "Open";

            [JsonPropertyName("environments")]
            public List<string> Environments { get; init; } = new();

            [JsonPropertyName("description")]
            public string Description { get; init; } = string.Empty;

            [JsonPropertyName("price")]
            public int Price { get; init; }

            [JsonPropertyName("cardCount")]
            public int CardCount { get; init; } = 5;

            [JsonPropertyName("isActive")]
            public bool IsActive { get; init; } = true;

            [JsonPropertyName("aliases")]
            public List<string> Aliases { get; init; } = new();
        }

        private record CardDataSeedDto
        {
            [JsonPropertyName("id")]
            public int CardId { get; init; }

            [JsonPropertyName("name")]
            public string Name { get; init; } = string.Empty;

            [JsonPropertyName("type")]
            public CardType Type { get; init; }

            [JsonPropertyName("rarity")]
            public string Rarity { get; init; } = "Common";

            [JsonPropertyName("card_set")]
            public string CardSet { get; init; } = "Legacy";

            [JsonPropertyName("class")]
            public string Class { get; init; } = "Neutral";

            [JsonPropertyName("cost")]
            public int Cost { get; init; }

            [JsonPropertyName("attack")]
            public int? Attack { get; init; }

            [JsonPropertyName("health")]
            public int? Health { get; init; }

            [JsonPropertyName("countdown")]
            public int? Countdown { get; init; }

            [JsonPropertyName("description")]
            public string? Description { get; init; }

            [JsonPropertyName("isCollectible")]
            public bool IsCollectible { get; init; } = true;

            [JsonPropertyName("keywords")]
            public List<string> Keywords { get; init; } = new List<string>();

            [JsonPropertyName("tribe")]
            public List<string>? Tribe { get; init; }

            [JsonPropertyName("abilities")]
            public List<JsonElement>? Abilities { get; init; }
        }

        private static string NormalizePackKey(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static async Task<List<CardPackSeedDto>> LoadCardPackSeedsAsync(string dataDirectory, JsonSerializerOptions jsonOptions, ILogger logger)
        {
            var seedPath = Path.Combine(dataDirectory, "card_pack_definitions.json");
            if (!File.Exists(seedPath))
            {
                logger.LogWarning("Card pack seed file not found at {SeedPath}.", seedPath);
                return new List<CardPackSeedDto>();
            }

            try
            {
                var jsonData = await File.ReadAllTextAsync(seedPath);
                var seeds = JsonSerializer.Deserialize<List<CardPackSeedDto>>(jsonData, jsonOptions);
                return seeds ?? throw new JsonException($"Card pack seed data must be an array: {seedPath}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read card pack seed file at {SeedPath}.", seedPath);
                throw;
            }
        }

        private static CardSetDefinition BuildCardSetEntity(CardPackSeedDto seed)
        {
            var abbreviation = string.IsNullOrWhiteSpace(seed.Abbreviation) ? string.Empty : seed.Abbreviation.Trim();
            var environments = ResolveSeedEnvironments(seed);

            return new CardSetDefinition
            {
                Name = seed.Name.Trim(),
                Abbreviation = abbreviation,
                Region = seed.Region,
                Sequence = seed.Sequence,
                Environments = environments,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static CardPackDefinition BuildCardPackEntity(CardPackSeedDto seed, CardSetDefinition setDefinition)
        {
            var abbreviation = string.IsNullOrWhiteSpace(seed.Abbreviation) ? "UNK" : seed.Abbreviation.Trim();
            var description = string.IsNullOrWhiteSpace(seed.Description) ? seed.Name : seed.Description;

            return new CardPackDefinition
            {
                Id = seed.Id ?? 0,
                Name = seed.Name.Trim(),
                Abbreviation = abbreviation,
                Region = seed.Region,
                Sequence = seed.Sequence,
                Description = description,
                Price = seed.Price,
                CardCount = seed.CardCount,
                IsActive = seed.IsActive,
                CreatedAt = DateTime.UtcNow,
                CardSetDefinitionId = setDefinition.Id,
                CardSetDefinition = setDefinition
            };
        }

        private static List<DeckEnvironment> ResolveSeedEnvironments(CardPackSeedDto seed)
        {
            var rawValues = new List<string>();
            if (seed.Environments != null && seed.Environments.Count > 0)
            {
                rawValues.AddRange(seed.Environments);
            }
            else if (!string.IsNullOrWhiteSpace(seed.Environment))
            {
                rawValues.Add(seed.Environment);
            }

            var resolved = new List<DeckEnvironment>();
            foreach (var raw in rawValues)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (Enum.TryParse<DeckEnvironment>(raw, true, out var parsed))
                {
                    resolved.Add(parsed);
                }
            }

            if (resolved.Count == 0)
            {
                resolved.Add(DeckEnvironment.Open);
            }

            return resolved;
        }

        private static Dictionary<string, CardSetDefinition> BuildSetLookup(
            IEnumerable<CardSetDefinition> definitions,
            IEnumerable<CardPackSeedDto> seeds)
        {
            var lookup = new Dictionary<string, CardSetDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(definition.Name))
                {
                    lookup[NormalizePackKey(definition.Name)] = definition;
                }
            }

            foreach (var seed in seeds)
            {
                if (string.IsNullOrWhiteSpace(seed.Name)) continue;
                var key = NormalizePackKey(seed.Name);
                if (!lookup.TryGetValue(key, out var definition)) continue;

                foreach (var alias in seed.Aliases ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(alias)) continue;
                    lookup[NormalizePackKey(alias)] = definition;
                }
            }

            return lookup;
        }

        private static CardSetDefinition? ResolveSetDefinition(
            string? cardSet,
            Dictionary<string, CardSetDefinition> setLookup)
        {
            if (string.IsNullOrWhiteSpace(cardSet))
            {
                return null;
            }

            var key = NormalizePackKey(cardSet);
            return setLookup.TryGetValue(key, out var definition) ? definition : null;
        }

        private static async Task<List<CardSetDefinition>> EnsureCardSetDefinitionsAsync(
            ApplicationDbContext context,
            List<CardPackSeedDto> packSeeds,
            ILogger logger)
        {
            var existingDefinitions = await context.CardSetDefinitions.ToListAsync();
            if (existingDefinitions.Count == 0)
            {
                var newDefinitions = packSeeds
                    .Where(seed => !string.IsNullOrWhiteSpace(seed.Name))
                    .Select(BuildCardSetEntity)
                    .ToList();

                if (newDefinitions.Count > 0)
                {
                    context.CardSetDefinitions.AddRange(newDefinitions);
                    await context.SaveChangesAsync();
                    existingDefinitions.AddRange(newDefinitions);
                    logger.LogInformation("Seeded {Count} card set definitions.", newDefinitions.Count);
                }
            }
            else if (packSeeds.Count > 0)
            {
                var existingNames = new HashSet<string>(
                    existingDefinitions.Select(d => d.Name),
                    StringComparer.OrdinalIgnoreCase);
                var missingSeeds = packSeeds.Where(seed => !existingNames.Contains(seed.Name)).ToList();
                if (missingSeeds.Count > 0)
                {
                    var newDefinitions = missingSeeds
                        .Where(seed => !string.IsNullOrWhiteSpace(seed.Name))
                        .Select(BuildCardSetEntity)
                        .ToList();
                    context.CardSetDefinitions.AddRange(newDefinitions);
                    await context.SaveChangesAsync();
                    existingDefinitions.AddRange(newDefinitions);
                    logger.LogInformation("Added {Count} missing card set definitions.", newDefinitions.Count);
                }
            }

            return existingDefinitions;
        }

        private static async Task<List<CardPackDefinition>> EnsureCardPackDefinitionsAsync(
            ApplicationDbContext context,
            List<CardPackSeedDto> packSeeds,
            Dictionary<string, CardSetDefinition> setLookup,
            ILogger logger)
        {
            var existingDefinitions = await context.CardPackDefinitions
                .Include(pack => pack.CardSetDefinition)
                .ToListAsync();

            CardSetDefinition ResolveOrCreateSet(CardPackSeedDto seed)
            {
                var key = NormalizePackKey(seed.Name);
                if (setLookup.TryGetValue(key, out var existingSet))
                {
                    return existingSet;
                }

                var created = BuildCardSetEntity(seed);
                context.CardSetDefinitions.Add(created);
                setLookup[key] = created;
                return created;
            }
            if (existingDefinitions.Count == 0)
            {
                var newDefinitions = packSeeds
                    .Where(seed => !string.IsNullOrWhiteSpace(seed.Name))
                    .Select(seed => BuildCardPackEntity(seed, ResolveOrCreateSet(seed)))
                    .ToList();

                if (newDefinitions.Count > 0)
                {
                    context.CardPackDefinitions.AddRange(newDefinitions);
                    await context.SaveChangesAsync();
                    existingDefinitions.AddRange(newDefinitions);
                    logger.LogInformation("Seeded {Count} card pack definitions.", newDefinitions.Count);
                }
            }
            else if (packSeeds.Count > 0)
            {
                var existingNames = new HashSet<string>(
                    existingDefinitions.Select(d => d.Name),
                    StringComparer.OrdinalIgnoreCase);
                var missingSeeds = packSeeds.Where(seed => !existingNames.Contains(seed.Name)).ToList();
                if (missingSeeds.Count > 0)
                {
                    var newDefinitions = missingSeeds
                        .Where(seed => !string.IsNullOrWhiteSpace(seed.Name))
                        .Select(seed => BuildCardPackEntity(seed, ResolveOrCreateSet(seed)))
                        .ToList();
                    context.CardPackDefinitions.AddRange(newDefinitions);
                    await context.SaveChangesAsync();
                    existingDefinitions.AddRange(newDefinitions);
                    logger.LogInformation("Added {Count} missing card pack definitions.", newDefinitions.Count);
                }
            }

            var updated = false;
            foreach (var pack in existingDefinitions)
            {
                if (pack.CardSetDefinitionId != 0 && pack.CardSetDefinition != null)
                {
                    continue;
                }

                var key = NormalizePackKey(pack.Name);
                if (setLookup.TryGetValue(key, out var setDefinition))
                {
                    pack.CardSetDefinitionId = setDefinition.Id;
                    pack.CardSetDefinition = setDefinition;
                    updated = true;
                }
            }

            if (updated)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Backfilled card set references for existing pack definitions.");
            }

            return existingDefinitions;
        }

        private static async Task BackfillCardSetLinksAsync(
            ApplicationDbContext context,
            Dictionary<string, CardSetDefinition> setLookup,
            Dictionary<int, CardPackDefinition> packBySetId,
            CardSetDefinition? legacySet,
            ILogger logger)
        {
            var cardsToUpdate = await context.CardDefinitions
                .Where(card => card.CardSetDefinitionId == null || card.CardSetDefinitionId == 0)
                .ToListAsync();

            if (cardsToUpdate.Count == 0)
            {
                return;
            }

            foreach (var card in cardsToUpdate)
            {
                var setDefinition = ResolveSetDefinition(card.CardSet, setLookup) ?? legacySet;
                if (setDefinition == null)
                {
                    continue;
                }

                card.CardSetDefinitionId = setDefinition.Id;
                card.CardSet = setDefinition.Name;

                if (packBySetId.TryGetValue(setDefinition.Id, out var packDefinition))
                {
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Backfilled card set links for {Count} card definitions.", cardsToUpdate.Count);
        }

        private static async Task EnsureQuestDefinitionsAsync(ApplicationDbContext context, ILogger logger)
        {
            var seeds = new List<QuestDefinition>
            {
                new()
                {
                    Id = 10001,
                    Name = "每日对战",
                    Description = "完成 1 场官方对战",
                    Type = QuestType.PlayGames,
                    ActivityType = QuestActivityType.PlayMatch,
                    Category = QuestCategory.Daily,
                    TargetValue = 1,
                    CoinReward = 50,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                    IsActive = true
                },
                new()
                {
                    Id = 10002,
                    Name = "每日胜利",
                    Description = "赢得 1 场官方对战",
                    Type = QuestType.WinGames,
                    ActivityType = QuestActivityType.WinMatch,
                    Category = QuestCategory.Daily,
                    TargetValue = 1,
                    CoinReward = 80,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                    IsActive = true
                },
                new()
                {
                    Id = 10003,
                    Name = "每日评论",
                    Description = "评论 1 张卡牌",
                    Type = QuestType.CollectCards,
                    ActivityType = QuestActivityType.Comment,
                    Category = QuestCategory.Daily,
                    TargetValue = 1,
                    CoinReward = 50,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                    IsActive = true
                },
                new()
                {
                    Id = 10004,
                    Name = "每日投票",
                    Description = "为 3 张不同卡牌点赞或点踩",
                    Type = QuestType.CollectCards,
                    ActivityType = QuestActivityType.Vote,
                    Category = QuestCategory.Daily,
                    TargetValue = 3,
                    CoinReward = 80,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                    IsActive = true
                },
                new()
                {
                    Id = 10005,
                    Name = "每日登录",
                    Description = "登录游戏 1 次",
                    Type = QuestType.LoginDaily,
                    ActivityType = QuestActivityType.Login,
                    Category = QuestCategory.Daily,
                    TargetValue = 1,
                    CoinReward = 30,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.OncePerPeriod,
                    IsActive = true
                },
                new()
                {
                    Id = 10006,
                    Name = "每日观战",
                    Description = "观战 1 场对局",
                    Type = QuestType.PlayGames,
                    ActivityType = QuestActivityType.SpectateMatch,
                    Category = QuestCategory.Daily,
                    TargetValue = 1,
                    CoinReward = 50,
                    RefreshIntervalHours = 24,
                    DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                    IsActive = true
                }
            };

            var existing = await context.QuestDefinitions.ToListAsync();
            var existingById = existing.ToDictionary(definition => definition.Id);
            var existingNames = new HashSet<string>(existing.Select(definition => definition.Name), StringComparer.OrdinalIgnoreCase);
            var added = 0;
            var updated = 0;

            foreach (var seed in seeds)
            {
                existingById.TryGetValue(seed.Id, out var definition);
                definition ??= existing.FirstOrDefault(item =>
                    string.Equals(item.Name, seed.Name, StringComparison.OrdinalIgnoreCase));

                if (definition != null)
                {
                    if (ApplyQuestSeedMapping(definition, seed))
                    {
                        updated++;
                    }
                    continue;
                }

                if (existingNames.Add(seed.Name))
                {
                    context.QuestDefinitions.Add(seed);
                    added++;
                }
            }

            foreach (var definition in existing)
            {
                var mapped = MapQuestActivity(definition.Type);
                if (definition.ActivityType != mapped.ActivityType || definition.DeduplicationPolicy == QuestDeduplicationPolicy.None)
                {
                    definition.ActivityType = mapped.ActivityType;
                    definition.DeduplicationPolicy = mapped.DeduplicationPolicy;
                    updated++;
                }
            }

            if (added > 0 || updated > 0)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Ensured quest definitions. Added={Added}, Updated={Updated}.", added, updated);
            }
        }

        private static bool ApplyQuestSeedMapping(QuestDefinition target, QuestDefinition seed)
        {
            var changed = false;
            if (target.ActivityType != seed.ActivityType)
            {
                target.ActivityType = seed.ActivityType;
                changed = true;
            }

            if (target.DeduplicationPolicy != seed.DeduplicationPolicy)
            {
                target.DeduplicationPolicy = seed.DeduplicationPolicy;
                changed = true;
            }

            if (target.RefreshIntervalHours <= 0)
            {
                target.RefreshIntervalHours = seed.RefreshIntervalHours;
                changed = true;
            }

            return changed;
        }

        private static (QuestActivityType ActivityType, QuestDeduplicationPolicy DeduplicationPolicy) MapQuestActivity(QuestType type)
        {
            return type switch
            {
                QuestType.WinGames => (QuestActivityType.WinMatch, QuestDeduplicationPolicy.PerSourceEntityPerPeriod),
                QuestType.LoginDaily => (QuestActivityType.Login, QuestDeduplicationPolicy.OncePerPeriod),
                QuestType.CollectCards => (QuestActivityType.Vote, QuestDeduplicationPolicy.PerSourceEntityPerPeriod),
                _ => (QuestActivityType.PlayMatch, QuestDeduplicationPolicy.PerSourceEntityPerPeriod)
            };
        }

        // Changed signature to accept WebApplication for easier service/environment access
        public static async Task SeedDatabaseAsync(WebApplication app)
        {
            // Use a scope specifically for this operation
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>(); // Use Program logger context
                var context = services.GetRequiredService<ApplicationDbContext>();
                var environment = services.GetRequiredService<IHostEnvironment>(); // Get environment for path resolving
                var tribeRegistry = services.GetRequiredService<ITribeRegistry>();

                // Construct the path relative to the application's content root
                var dataDirectory = Path.Combine(environment.ContentRootPath, "..", "data");
                dataDirectory = Path.GetFullPath(dataDirectory);
                var workspaceRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, ".."));


                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };

                try
                {
                    logger.LogInformation("Checking if database seeding is required...");
                    var existingCardCount = await context.CardDefinitions.CountAsync();
                    var packSeeds = await LoadCardPackSeedsAsync(dataDirectory, jsonOptions, logger);
                    var setDefinitions = await EnsureCardSetDefinitionsAsync(context, packSeeds, logger);
                    var setLookup = BuildSetLookup(setDefinitions, packSeeds);
                    var packDefinitions = await EnsureCardPackDefinitionsAsync(context, packSeeds, setLookup, logger);
                    var packBySetId = packDefinitions
                        .Where(pack => pack.CardSetDefinitionId != 0)
                        .GroupBy(pack => pack.CardSetDefinitionId)
                        .ToDictionary(group => group.Key, group => group.First());
                    var legacySet = ResolveSetDefinition("Legacy", setLookup)
                        ?? setDefinitions.FirstOrDefault();

                    if (existingCardCount > 0)
                    {
                        logger.LogInformation("Database already contains {Count} card definitions. Backfilling set links.", existingCardCount);
                        await BackfillCardSetLinksAsync(context, setLookup, packBySetId, legacySet, logger);
                    }
                    else
                    {
                        logger.LogInformation("Database is empty. Proceeding with data seeding from card_data files in {Directory}...", dataDirectory);

                        if (!Directory.Exists(dataDirectory))
                        {
                            throw new DirectoryNotFoundException($"Seed data directory not found: {dataDirectory}");
                        }

                        var cardDataFiles = Directory
                            .GetFiles(dataDirectory, "card_data*.json", SearchOption.TopDirectoryOnly)
                            .OrderBy(path => Path.GetFileName(path))
                            .ToArray();
                        if (cardDataFiles.Length == 0)
                        {
                            throw new FileNotFoundException($"No card_data*.json files found in {dataDirectory}.");
                        }

                        logger.LogInformation("Reading seed data files: {Files}", string.Join(", ", cardDataFiles.Select(Path.GetFileName)));
                        var cardDtosById = new Dictionary<int, CardDataSeedDto>();
                        foreach (var cardDataFile in cardDataFiles)
                        {
                            logger.LogInformation("Reading seed data file: {FilePath}", cardDataFile);
                            var jsonData = await File.ReadAllTextAsync(cardDataFile);
                            var fileCards = JsonSerializer.Deserialize<List<CardDataSeedDto>>(jsonData, jsonOptions)
                                ?? throw new JsonException($"Card seed data must be an array: {cardDataFile}");
                            if (fileCards == null || fileCards.Count == 0)
                            {
                                logger.LogWarning("No card data found in {FilePath}.", cardDataFile);
                                continue;
                            }
                            foreach (var cardDto in fileCards)
                            {
                                if (!cardDtosById.TryAdd(cardDto.CardId, cardDto))
                                {
                                    logger.LogWarning("Duplicate CardId {CardId} found in {FilePath}. Keeping the first occurrence.",
                                        cardDto.CardId, cardDataFile);
                                }
                            }
                            logger.LogInformation("Loaded {Count} cards from {FilePath}.", fileCards.Count, cardDataFile);
                        }

                        var cardDtos = cardDtosById.Values.ToList();
                        if (!cardDtos.Any())
                        {
                            logger.LogWarning("No card data found after deserializing card_data files, or the file format is incorrect.");
                            return;
                        }
                        logger.LogInformation("Successfully deserialized {Count} card DTOs.", cardDtos.Count);

                        logger.LogInformation("Mapping DTOs to CardDefinition entities...");
                        // Filter out DTOs with null/empty names first
                        var validCardDtos = cardDtos.Where(dto => !string.IsNullOrEmpty(dto.Name));
                        int originalCount = cardDtos.Count;
                        int validCount = validCardDtos.Count();
                        if (originalCount != validCount)
                        {
                            logger.LogWarning("Filtered out {FilteredCount} card DTOs due to missing or empty names.", originalCount - validCount);
                        }
                        logger.LogInformation("Mapping {ValidCount} valid DTOs to CardDefinition entities...", validCount);

                        var cardEntities = new List<CardDefinition>();
                        foreach (var dto in validCardDtos)
                        {
                            var normalizedTribes = await tribeRegistry.NormalizeAndRegisterAsync(dto.Tribe);
                            var setDefinition = ResolveSetDefinition(dto.CardSet, setLookup) ?? legacySet;
                            var normalizedCardSet = setDefinition?.Name ?? dto.CardSet;
                            var packDefinition = setDefinition != null && packBySetId.TryGetValue(setDefinition.Id, out var pack)
                                ? pack
                                : null;

                            cardEntities.Add(new CardDefinition
                            {
                                Id = dto.CardId,
                                Name = dto.Name,
                                Cost = dto.Cost,
                                Type = dto.Type,
                                Rarity = Enum.TryParse<CardRarity>(dto.Rarity, true, out var rarity) ? rarity : CardRarity.Bronze,
                                CardSet = normalizedCardSet,
                                CardSetDefinitionId = setDefinition?.Id,
                                Attack = dto.Attack,
                                Health = dto.Health,
                                Countdown = dto.Countdown,
                                Description = dto.Description ?? string.Empty,
                                IsCollectible = dto.IsCollectible,
                                Keywords = dto.Keywords,
                                Tribe = normalizedTribes.Count == 0 ? null : normalizedTribes.ToList(),
                                Class = Enum.TryParse<CardClass>(dto.Class, true, out var cardClass) ? cardClass : CardClass.Neutral,
                                AbilityIds = new List<long>()
                            });
                        }
                        // Note: Removed filtering logic that discarded entities with ID 0,
                        // as 0 is now a valid ID for the Leader card.
                        // int originalEntityCount = cardEntities.Count;
                        // cardEntities = cardEntities.Where(e => e.Id != 0).ToList();
                        // if (cardEntities.Count != originalEntityCount)
                        // {
                        //     logger.LogWarning("Removed {RemovedCount} entities due to ID parsing errors (ID became 0).", originalEntityCount - cardEntities.Count);
                        // }
                        logger.LogInformation("Mapping complete. {Count} valid entities created.", cardEntities.Count.ToString());

                        logger.LogInformation("Adding entities to the DbContext...");
                        await context.CardDefinitions.AddRangeAsync(cardEntities);
                        logger.LogInformation("Entities added to DbContext.");

                        logger.LogInformation("Saving changes to the database...");
                        await context.SaveChangesAsync();
                        logger.LogInformation("Successfully seeded {Count} card definitions into the database.", cardEntities.Count.ToString());

                        // --- BEGIN SEEDING CARD ABILITIES FROM CARD_DATA ---
                        logger.LogInformation("Seeding CardAbility data from card_data abilities...");
                        var abilityEntities = new List<AbilityDefinition>();
                        var abilityIdsByCard = new Dictionary<int, List<long>>();
                        var abilityIdSet = new HashSet<long>();

                        foreach (var cardDto in validCardDtos)
                        {
                            if (cardDto.Abilities == null || cardDto.Abilities.Count == 0)
                            {
                                continue;
                            }

                            for (int index = 0; index < cardDto.Abilities.Count; index++)
                            {
                                var abilityJson = cardDto.Abilities[index];
                                // Deterministic ability ID generation by appending a fixed-width index after CardId.
                                var abilityId = BuildAbilityId(cardDto.CardId, index);

                                if (!abilityIdSet.Add(abilityId))
                                {
                                    throw new InvalidOperationException($"Duplicate ability ID detected during seeding: {abilityId}. CardId={cardDto.CardId}, Index={index}");
                                }

                                var abilityName = GetJsonString(abilityJson, "name") ?? $"{cardDto.Name}-Ability-{index + 1}";
                                var abilityDescription = GetJsonString(abilityJson, "description") ?? string.Empty;
                                var abilityType = ParseAbilityType(GetJsonString(abilityJson, "type"));
                                var priority = GetJsonInt32(abilityJson, "priority") ?? 0;

                                abilityEntities.Add(new AbilityDefinition
                                {
                                    Id = abilityId,
                                    CardId = cardDto.CardId,
                                    Name = abilityName,
                                    Description = abilityDescription,
                                    AbilityType = (int)abilityType,
                                    Priority = priority,
                                    IsActive = true,
                                    JsonConfig = abilityJson.GetRawText(),
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                });

                                if (!abilityIdsByCard.TryGetValue(cardDto.CardId, out var abilityIds))
                                {
                                    abilityIds = new List<long>();
                                    abilityIdsByCard[cardDto.CardId] = abilityIds;
                                }
                                abilityIds.Add(abilityId);
                            }
                        }

                        if (abilityEntities.Count > 0)
                        {
                            await context.CardAbilities.AddRangeAsync(abilityEntities);
                            await context.SaveChangesAsync();
                            logger.LogInformation("Successfully seeded {Count} card abilities into the database.", abilityEntities.Count);
                        }
                        else
                        {
                            logger.LogInformation("No abilities found in card_data. Skipping CardAbilities seeding.");
                        }
                        // --- END SEEDING CARD ABILITIES FROM CARD_DATA ---

                        // --- UPDATE CARD ABILITY ASSOCIATIONS ---
                        logger.LogInformation("Updating card ability associations...");

                        foreach (var cardDefinition in cardEntities)
                        {
                            if (abilityIdsByCard.TryGetValue(cardDefinition.Id, out var abilityIds))
                            {
                                cardDefinition.AbilityIds = abilityIds;
                                logger.LogInformation("Updated card {CardId} with {Count} ability IDs: [{AbilityIds}]",
                                    cardDefinition.Id,
                                    abilityIds.Count,
                                    string.Join(", ", abilityIds));
                            }
                        }

                        await context.SaveChangesAsync();
                        logger.LogInformation("Card ability associations updated successfully.");
                        // --- END UPDATE CARD ABILITY ASSOCIATIONS ---
                    }

                    await EnsureTribeDefinitionsAsync(context, tribeRegistry, logger);
                    await EnsureQuestDefinitionsAsync(context, logger);
                    await NormalizeCardArtPathsAsync(context, workspaceRoot, logger);
                }
                catch (JsonException jsonEx)
                {
                    logger.LogError(jsonEx, "Error deserializing seed data from {Directory}. Please check the JSON format.", dataDirectory);
                    throw;
                }
                catch (DbUpdateException dbEx)
                {
                    logger.LogError(dbEx, "Error saving seed data to the database. Check database connection, schema, and permissions.");
                    throw;
                }
                catch (IOException ioEx)
                {
                    logger.LogError(ioEx, "Error reading seed data files from {Directory}.", dataDirectory);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred during database seeding operation.");
                    throw;
                }
            }
        }

        private static async Task NormalizeCardArtPathsAsync(ApplicationDbContext context, string workspaceRoot, ILogger logger)
        {
            var cardsDirectory = Path.Combine(workspaceRoot, "assets", "cards");
            if (!Directory.Exists(cardsDirectory))
            {
                logger.LogWarning("Card art directory not found at {CardsDirectory}. Skipping art path normalization.", cardsDirectory);
                return;
            }

            var cards = await context.CardDefinitions.ToListAsync();
            if (cards.Count == 0)
            {
                return;
            }

            var previewUpdates = 0;
            var detailUpdates = 0;

            foreach (var card in cards)
            {
                var cardFolder = Path.Combine(cardsDirectory, card.Id.ToString());
                if (!Directory.Exists(cardFolder))
                {
                    continue;
                }

                var normalizedPreview = ResolveNormalizedArtPath(cardFolder, card.Id, "preview", card.ArtPreview);
                if (!string.Equals(card.ArtPreview, normalizedPreview, StringComparison.Ordinal))
                {
                    card.ArtPreview = normalizedPreview;
                    previewUpdates++;
                }

                var normalizedDetail = ResolveNormalizedArtPath(cardFolder, card.Id, "detail", card.ArtDetail);
                if (!string.Equals(card.ArtDetail, normalizedDetail, StringComparison.Ordinal))
                {
                    card.ArtDetail = normalizedDetail;
                    detailUpdates++;
                }
            }

            var totalUpdates = previewUpdates + detailUpdates;
            if (totalUpdates <= 0)
            {
                logger.LogInformation("Card art path normalization complete. No updates were required.");
                return;
            }

            await context.SaveChangesAsync();
            logger.LogInformation(
                "Card art path normalization updated {Total} fields (preview: {PreviewCount}, detail: {DetailCount}).",
                totalUpdates,
                previewUpdates,
                detailUpdates);
        }

        private static async Task EnsureTribeDefinitionsAsync(
            ApplicationDbContext context,
            ITribeRegistry tribeRegistry,
            ILogger logger)
        {
            var allTribes = await context.CardDefinitions
                .AsNoTracking()
                .Select(card => card.Tribe)
                .ToListAsync();

            var flattened = allTribes
                .Where(tribes => tribes != null)
                .SelectMany(tribes => tribes!)
                .ToList();
            flattened.Add(GameConstants.Tribes.MatchAll);

            await tribeRegistry.NormalizeAndRegisterAsync(flattened);
            logger.LogInformation("Ensured tribe definition bootstrap for {Count} raw tribe entries.", flattened.Count);
        }

        private static string? ResolveNormalizedArtPath(string cardFolder, int cardId, string kind, string? currentPath)
        {
            var currentFileName = GetFileNameFromArtPath(currentPath);
            if (!string.IsNullOrWhiteSpace(currentFileName))
            {
                var currentDiskPath = Path.Combine(cardFolder, currentFileName);
                if (File.Exists(currentDiskPath))
                {
                    return BuildArtPath(cardId, currentFileName);
                }
            }

            var preferredFileName = FindPreferredArtFileName(cardFolder, kind);
            if (!string.IsNullOrWhiteSpace(preferredFileName))
            {
                return BuildArtPath(cardId, preferredFileName);
            }

            return currentPath;
        }

        private static string? FindPreferredArtFileName(string cardFolder, string kind)
        {
            var versioned = Directory
                .EnumerateFiles(cardFolder, $"{kind}-*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedArtExtensions.Contains(Path.GetExtension(path)))
                .OrderByDescending(path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            if (!string.IsNullOrWhiteSpace(versioned))
            {
                return versioned;
            }

            foreach (var extension in PreferredArtExtensions)
            {
                var fileName = $"{kind}{extension}";
                var diskPath = Path.Combine(cardFolder, fileName);
                if (File.Exists(diskPath))
                {
                    return fileName;
                }
            }

            return null;
        }

        private static string? GetFileNameFromArtPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var normalized = path.Replace('\\', '/').Trim();
            var queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                normalized = normalized[..queryIndex];
            }

            var fileName = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }

        private static string BuildArtPath(int cardId, string fileName)
        {
            return $"/assets/cards/{cardId}/{fileName}";
        }

        private static string? GetJsonString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static int? GetJsonInt32(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var number))
            {
                return number;
            }

            return null;
        }

        private static AbilityType ParseAbilityType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AbilityType.Triggered;
            }

            return Enum.TryParse<AbilityType>(value, true, out var parsed)
                ? parsed
                : AbilityType.Triggered;
        }

        private static long BuildAbilityId(int cardId, int index)
        {
            var maxIndex = 1;
            for (int i = 0; i < AbilityIndexWidth; i++)
            {
                maxIndex *= 10;
            }

            if (index >= maxIndex)
            {
                throw new InvalidOperationException(
                    $"Ability index {index} exceeds the fixed width of {AbilityIndexWidth} digits for CardId {cardId}.");
            }

            var suffix = index.ToString($"D{AbilityIndexWidth}");
            var idText = $"{cardId}{suffix}";
            if (!long.TryParse(idText, out var abilityId))
            {
                throw new InvalidOperationException($"Failed to build ability ID from CardId {cardId} and index {index}.");
            }

            return abilityId;
        }
    }
}

