using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// Stored once per recording, outside the frequent state checkpoints. No content hash is substituted for the definitions.
public static class ReplayExecutionRules
{
    public static async Task<JsonElement> CaptureAsync(
        IServiceProvider services,
        CancellationToken cancellationToken,
        GameMode mode = GameMode.Standard)
    {
        var provider = services.GetRequiredService<IGameDataProvider>();
        var expectMirage = mode == GameMode.Mirage;
        var cards = provider.GetAllCardDefinitions()
            .Where(kvp => kvp.Value != null && kvp.Value.IsMirageCard == expectMirage)
            .OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var abilities = provider.GetAllAbilityConfigs()
            .Where(kvp => cards.ContainsKey(kvp.Key))
            .OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var buffs = await services.GetRequiredService<ApplicationDbContext>()
            .Set<BuffDefinition>()
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        return JsonSerializer.SerializeToElement(new
        {
            Version = 1,
            EngineBuild = ReplayExecutionState.EngineBuild,
            Mode = mode,
            Cards = cards,
            Abilities = abilities,
            Buffs = buffs
        }, ReplayExecutionState.Json);
    }

    public static async Task ValidateAsync(
        JsonElement recorded,
        IServiceProvider services,
        CancellationToken cancellationToken,
        GameMode? expectedMode = null)
    {
        if (recorded.GetProperty("version").GetInt32() != 1
            || recorded.GetProperty("engineBuild").GetString() != ReplayExecutionState.EngineBuild)
        {
            throw new NotSupportedException("resume_engine_build_mismatch");
        }

        var mode = expectedMode ?? GameMode.Standard;
        var hasRecordedMode = recorded.TryGetProperty("mode", out var modeProp);
        if (hasRecordedMode)
        {
            var recordedMode = modeProp.ValueKind == JsonValueKind.Number
                ? (GameMode)modeProp.GetInt32()
                : Enum.TryParse<GameMode>(modeProp.GetString(), true, out var parsedMode)
                    ? parsedMode
                    : GameMode.Standard;
            if (expectedMode.HasValue && recordedMode != expectedMode.Value)
            {
                throw new NotSupportedException("resume_rules_changed_since_recording");
            }
            mode = recordedMode;
        }

        var current = await CaptureAsync(services, cancellationToken, mode);
        var recordedNode = JsonNode.Parse(recorded.GetRawText()) as JsonObject;
        var currentNode = JsonNode.Parse(current.GetRawText()) as JsonObject;
        if (recordedNode == null || currentNode == null)
        {
            throw new NotSupportedException("resume_rules_changed_since_recording");
        }

        if (!hasRecordedMode)
        {
            currentNode.Remove("mode");
        }

        NormalizeLegacyCardSchemaFields(recordedNode, currentNode);

        if (!JsonNode.DeepEquals(recordedNode, currentNode))
        {
            throw new NotSupportedException("resume_rules_changed_since_recording");
        }
    }

    private static void NormalizeLegacyCardSchemaFields(JsonObject recordedNode, JsonObject currentNode)
    {
        if (recordedNode["cards"] is not JsonObject recordedCards
            || currentNode["cards"] is not JsonObject currentCards)
        {
            return;
        }

        foreach (var kvp in recordedCards)
        {
            if (kvp.Value is not JsonObject recordedCard
                || currentCards[kvp.Key] is not JsonObject currentCard)
            {
                continue;
            }

            if (!recordedCard.ContainsKey("isMirageCard"))
            {
                currentCard.Remove("isMirageCard");
            }

            if (!recordedCard.ContainsKey("mirageTavernTier"))
            {
                currentCard.Remove("mirageTavernTier");
            }

            if (!recordedCard.ContainsKey("mirageBuyCost"))
            {
                currentCard.Remove("mirageBuyCost");
            }
        }
    }
}
