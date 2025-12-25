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
using ShadowverseBeyond.Server.Dtos; // Needed for IHostEnvironment via WebApplication
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Infrastructure.Data // Added ShadowverseBeyond prefix
{
    public static class DataSeeder
    {
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

                // Construct the path relative to the application's content root
                var jsonFilePath = Path.Combine(environment.ContentRootPath, "..", "data", "card_data.json");
                // Normalize path for logging consistency
                jsonFilePath = Path.GetFullPath(jsonFilePath);


                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                var entityJsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                try
                {
                    logger.LogInformation("Checking if database seeding is required...");
                    var existingCardCount = await context.CardDefinitions.CountAsync();

                    // 临时：强制重新种子数据以确保包含主战者卡牌
                    if (existingCardCount > 0)
                    {
                        logger.LogInformation("Database contains {Count} card definitions. Clearing and reseeding to ensure data completeness...", existingCardCount);

                        // 清除现有数据
                        context.CardDefinitions.RemoveRange(context.CardDefinitions);
                        await context.SaveChangesAsync();
                        logger.LogInformation("Cleared existing card definitions.");
                    }

                    logger.LogInformation("Database is empty. Proceeding with data seeding from {FilePath}...", jsonFilePath);

                    if (!File.Exists(jsonFilePath))
                    {
                        logger.LogError("Seed data file not found at {FilePath}. Cannot seed database.", jsonFilePath);
                        return;
                    }

                    logger.LogInformation("Reading seed data file...");
                    string jsonData = await File.ReadAllTextAsync(jsonFilePath);
                    logger.LogInformation("Seed data file read successfully.");

                    logger.LogInformation("Deserializing JSON data...");
                    var cardDtos = JsonSerializer.Deserialize<List<CardDefinitionDto>>(jsonData, jsonOptions);

                    if (cardDtos == null || !cardDtos.Any())
                    {
                        logger.LogWarning("No card data found after deserializing {FilePath}, or the file format is incorrect.", jsonFilePath);
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

                    var cardEntities = validCardDtos.Select(dto => new CardDefinition
                    {
                        Id = dto.CardId,
                        Name = dto.Name,
                        Cost = dto.Cost,
                        Type = dto.Type,
                        Rarity = Enum.TryParse<CardRarity>(dto.Rarity, true, out var rarity) ? rarity : CardRarity.Bronze,
                        CardSet = dto.CardSet,
                        Attack = dto.Attack,
                        Health = dto.Health,
                        Description = dto.Description ?? string.Empty,
                        Keywords = dto.Keywords,
                        EvolveId = dto.EvolveId,
                        Tribe = dto.Tribe,
                        Class = Enum.TryParse<CardClass>(dto.Class, true, out var cardClass) ? cardClass : CardClass.Neutral,
                        AbilityIds = new List<long>()
                    }).ToList();
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

                    // --- BEGIN SEEDING CARD ABILITIES ---
                    logger.LogInformation("Seeding CardAbility data from SQL script...");
                    var abilityScriptPath = Path.Combine(environment.ContentRootPath, "..", "Server", "Infrastructure", "Data", "SeedData", "CardAbilitySeedData.sql");
                    abilityScriptPath = Path.GetFullPath(abilityScriptPath);

                    if (!File.Exists(abilityScriptPath))
                    {
                        logger.LogWarning("CardAbility seed script not found at: {ScriptPath}", abilityScriptPath);
                    }
                    else
                    {
                        logger.LogInformation("Reading CardAbility seed script from: {ScriptPath}", abilityScriptPath);
                        var sqlScript = await File.ReadAllTextAsync(abilityScriptPath);
                        logger.LogInformation("CardAbility seed script read successfully. Length: {Length} characters.", sqlScript.Length);

                        var statements = sqlScript.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        logger.LogInformation("Split SQL script into {Count} statements.", statements.Length);

                        // Use a transaction to ensure all statements succeed or fail together.
                        await using var transaction = await context.Database.BeginTransactionAsync();
                        logger.LogInformation("Database transaction started for CardAbility seeding.");
                        try
                        {
                            int totalRowsAffected = 0;
                            // Simpler, more robust approach: execute the entire script at once.
                            if (!string.IsNullOrWhiteSpace(sqlScript))
                            {
                                logger.LogInformation("Executing the entire CardAbility SQL script at once...");
                                // To prevent EF Core from interpreting curly braces in JSON as string format placeholders,
                                // we must escape them by doubling them up.
                                var escapedSqlScript = sqlScript.Replace("{", "{{").Replace("}", "}}");
                                totalRowsAffected = await context.Database.ExecuteSqlRawAsync(escapedSqlScript);
                                logger.LogInformation("SQL script executed. Total rows affected reported by ExecuteSqlRawAsync: {TotalRowsAffected}", totalRowsAffected);
                            }
                            else
                            {
                                logger.LogWarning("SQL script is empty. No CardAbility data will be seeded.");
                            }

                            await transaction.CommitAsync();
                            logger.LogInformation("Database transaction committed successfully.");

                            var finalAbilityCount = await context.CardAbilities.CountAsync();
                            logger.LogInformation("Verification: Found {Count} entries in the CardAbilities table after seeding.", finalAbilityCount);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            logger.LogError(ex, "Error executing CardAbility seed script. Transaction rolled back.");
                            throw; // Re-throw to indicate a critical failure in seeding.
                        }
                    }
                    // --- END SEEDING CARD ABILITIES ---

                    // --- UPDATE CARD ABILITY ASSOCIATIONS ---
                    logger.LogInformation("Updating card ability associations...");

                    // 获取所有卡牌能力，按卡牌ID分组
                    var cardAbilities = await context.CardAbilities
                        .GroupBy(ca => ca.CardId)
                        .Select(g => new { CardId = g.Key, AbilityIds = g.Select(ca => ca.Id).ToList() })
                        .ToListAsync();

                    // 更新每张卡牌的AbilityIds字段
                    foreach (var cardAbilityGroup in cardAbilities)
                    {
                        var cardDefinition = await context.CardDefinitions
                            .FirstOrDefaultAsync(cd => cd.Id == cardAbilityGroup.CardId);

                        if (cardDefinition != null)
                        {
                            cardDefinition.AbilityIds = cardAbilityGroup.AbilityIds;
                            logger.LogInformation("Updated card {CardId} with {Count} ability IDs: [{AbilityIds}]",
                                cardAbilityGroup.CardId,
                                cardAbilityGroup.AbilityIds.Count,
                                string.Join(", ", cardAbilityGroup.AbilityIds));
                        }
                    }

                    await context.SaveChangesAsync();
                    logger.LogInformation("Card ability associations updated successfully.");
                    // --- END UPDATE CARD ABILITY ASSOCIATIONS ---
                }
                catch (JsonException jsonEx)
                {
                    logger.LogError(jsonEx, "Error deserializing seed data from {FilePath}. Please check the JSON format.", jsonFilePath);
                    // Optionally rethrow or handle to prevent app startup? For now, log and continue.
                }
                catch (DbUpdateException dbEx)
                {
                    logger.LogError(dbEx, "Error saving seed data to the database. Check database connection, schema, and permissions.");
                    // Optionally rethrow or handle
                }
                catch (IOException ioEx)
                {
                    logger.LogError(ioEx, "Error reading seed data file from {FilePath}.", jsonFilePath);
                    // Optionally rethrow or handle
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred during database seeding operation.");
                    // Optionally rethrow or handle to prevent app startup
                }
            }
        }
    }
}