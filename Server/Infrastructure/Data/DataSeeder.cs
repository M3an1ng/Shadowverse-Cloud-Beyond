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
using Microsoft.Extensions.Hosting; // Needed for IHostEnvironment via WebApplication

namespace ShadowverseBeyond.Server.Infrastructure.Data // Added ShadowverseBeyond prefix
{
    // DTO matching the structure of data/card_data.json
    // Kept internal as they are only used within this seeding process
    internal class CardDataDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Cost { get; set; }
        public string Type { get; set; } = null!;
        public int? Attack { get; set; }
        public int? Health { get; set; }
        public string? Description { get; set; }
        public List<string>? Keywords { get; set; }
        public List<EffectDto>? Effects { get; set; }
        public string? Art { get; set; }
        public bool IsEvolved { get; set; }

        [JsonPropertyName("can_evolve")]
        public bool CanEvolve { get; set; }

        [JsonPropertyName("evolves_into")]
        public List<string>? EvolvesInto { get; set; }

        [JsonPropertyName("is_evolved_form_of")]
        public string? IsEvolvedFormOf { get; set; }

        public bool? RequiresTarget { get; set; }
        public List<string>? ValidTargets { get; set; }
    }

    internal class EffectDto
    {
        public string Trigger { get; set; } = null!;
        public string Action { get; set; } = null!;
        public int? Value { get; set; }
    }
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


                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entityJsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                try
                {
                    logger.LogInformation("Checking if database seeding is required...");
                    if (await context.CardDefinitions.AnyAsync())
                    {
                        logger.LogInformation("Database already contains data. Skipping data seeding.");
                        return;
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
                    var cardDtos = JsonSerializer.Deserialize<List<CardDataDto>>(jsonData, jsonOptions);

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
                        Id = dto.Id, // Assuming Id is always present in valid DTO
                        Name = dto.Name, // Safe because we filtered null/empty
                        Cost = dto.Cost,
                        Type = dto.Type ?? string.Empty, // Provide default if Type is null in DTO
                        Attack = dto.Attack,
                        Health = dto.Health,
                        Description = dto.Description ?? string.Empty, // Provide default if Description is null
                        Keywords = dto.Keywords, // Assign List<string>? directly
                        // Map Effects: Convert List<EffectDto>? to List<CardEffect>? safely
                        Effects = dto.Effects? // If dto.Effects is not null...
                                   .Select(e => new CardEffect( // ...select each EffectDto...
                                       e.Trigger ?? "DefaultTrigger", // Provide default for CardEffect record non-nullable string
                                       e.Action ?? "DefaultAction",   // Provide default for CardEffect record non-nullable string
                                       e.Value ?? (object)0 // If e.Value is null, pass boxed 0 // Assign int? to object is okay
                                   )).ToList() // ...and convert to List<CardEffect>
                                               // If dto.Effects is null, assign null to entity.Effects
                                   ?? null
                        // Removed mapping for: KeywordsJson, EffectsJson, Art, IsEvolved, CanEvolve, EvolvesInto, IsEvolvedFormOf, RequiresTarget, ValidTargets
                    }).ToList();
                    logger.LogInformation("Mapping complete. {Count} entities created.", cardEntities.Count);

                    logger.LogInformation("Adding entities to the DbContext...");
                    await context.CardDefinitions.AddRangeAsync(cardEntities);
                    logger.LogInformation("Entities added to DbContext.");

                    logger.LogInformation("Saving changes to the database...");
                    await context.SaveChangesAsync();
                    logger.LogInformation("Successfully seeded {Count} card definitions into the database.", cardEntities.Count);
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