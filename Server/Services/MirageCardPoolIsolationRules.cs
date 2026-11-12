using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record AbilityReferenceAnalysis(
        IReadOnlySet<int> ReferencedCardIds,
        IReadOnlySet<long> ReferencedAbilityIds,
        bool UsesClassicNamedEnvironmentPool);

    public static class MirageCardPoolIsolationRules
    {
        private static readonly HashSet<string> CardDefinitionIdParamKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "CardDefinitionId",
            "CardDefinitionIds",
            "TargetCardDefinitionId",
            "PlayAsCardDefinitionId"
        };

        private static readonly HashSet<string> AbilityIdParamKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "AbilityId",
            "AbilityIds",
            "AbilityDefinitionId",
            "AbilityDefinitionIds"
        };

        private static readonly HashSet<string> DslExpressionPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "expression",
            "formula"
        };

        private static readonly Regex DslClassicPoolRegex = new(
            @"\bdb\s*\.\s*(rotationCards|unlimitedCards|openCards|compileCards)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DslCardIdEqualityRegex = new(
            @"\b(?<prop>id|cardId|cardDefinitionId|baseDefinitionId|currentDefinitionId)\s*==\s*(?<id>\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DslCardIdInListRegex = new(
            @"\b(?<prop>id|cardId|cardDefinitionId|baseDefinitionId|currentDefinitionId)\s+in\s+\[(?<list>\s*\d+(?:\s*,\s*\d+)*\s*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IntegerListLiteralRegex = new(
            @"^\s*\[\s*(?<body>\d+(?:\s*,\s*\d+)*)?\s*\]\s*$",
            RegexOptions.Compiled);

        public static bool TryParseEditorScope(string? rawScope, out bool isMirageScope)
        {
            if (string.IsNullOrWhiteSpace(rawScope))
            {
                isMirageScope = false;
                return false;
            }

            var normalized = rawScope.Trim();
            if (string.Equals(normalized, "mirage", StringComparison.OrdinalIgnoreCase))
            {
                isMirageScope = true;
                return true;
            }

            if (string.Equals(normalized, "standard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "classic", StringComparison.OrdinalIgnoreCase))
            {
                isMirageScope = false;
                return true;
            }

            isMirageScope = false;
            return false;
        }

        public static AbilityReferenceAnalysis ExtractAbilityConfigReferences(string? jsonConfig)
        {
            var cardIds = new HashSet<int>();
            var abilityIds = new HashSet<long>();
            var usesClassicNamedPool = false;

            if (string.IsNullOrWhiteSpace(jsonConfig))
            {
                return new AbilityReferenceAnalysis(cardIds, abilityIds, false);
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(jsonConfig);
            }
            catch
            {
                return new AbilityReferenceAnalysis(cardIds, abilityIds, false);
            }

            void Walk(JsonNode? node, string? currentKey)
            {
                if (node == null) return;

                if (node is JsonObject obj)
                {
                    foreach (var kvp in obj)
                    {
                        var key = kvp.Key;
                        var val = kvp.Value;
                        if (val == null) continue;

                        if (CardDefinitionIdParamKeys.Contains(key))
                        {
                            CollectIntIdsFromParameterNode(val, cardIds);
                        }
                        else if (AbilityIdParamKeys.Contains(key))
                        {
                            CollectLongIdsFromParameterNode(val, abilityIds);
                        }
                        else if (DslExpressionPropertyKeys.Contains(key)
                                 && val is JsonValue exprVal
                                 && exprVal.TryGetValue<string>(out var exprText)
                                 && !string.IsNullOrWhiteSpace(exprText))
                        {
                            if (DslClassicPoolRegex.IsMatch(exprText))
                            {
                                usesClassicNamedPool = true;
                            }

                            foreach (Match match in DslCardIdEqualityRegex.Matches(exprText))
                            {
                                if (int.TryParse(match.Groups["id"].Value, out var dslCardId) && dslCardId > 0)
                                {
                                    cardIds.Add(dslCardId);
                                }
                            }

                            foreach (Match listMatch in DslCardIdInListRegex.Matches(exprText))
                            {
                                foreach (var token in listMatch.Groups["list"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                {
                                    if (int.TryParse(token, out var dslListCardId) && dslListCardId > 0)
                                    {
                                        cardIds.Add(dslListCardId);
                                    }
                                }
                            }
                        }

                        Walk(val, key);
                    }
                }
                else if (node is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        Walk(item, currentKey);
                    }
                }
            }

            Walk(root, null);
            return new AbilityReferenceAnalysis(cardIds, abilityIds, usesClassicNamedPool);
        }

        public static string? RewriteAbilityConfigForMirageImport(
            string? jsonConfig,
            long newAbilityId,
            int newOwnerCardId,
            IReadOnlyDictionary<int, int> cardIdMap,
            IReadOnlyDictionary<long, long> abilityIdMap)
        {
            if (string.IsNullOrWhiteSpace(jsonConfig))
            {
                return jsonConfig;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(jsonConfig);
            }
            catch
            {
                return jsonConfig;
            }

            if (root is JsonObject rootObj)
            {
                if (rootObj.ContainsKey("id"))
                {
                    rootObj["id"] = $"ability_{newAbilityId}";
                }
                else if (rootObj.ContainsKey("Id"))
                {
                    rootObj["Id"] = $"ability_{newAbilityId}";
                }

                if (rootObj.ContainsKey("cardId"))
                {
                    rootObj["cardId"] = newOwnerCardId;
                }
                else if (rootObj.ContainsKey("CardId"))
                {
                    rootObj["CardId"] = newOwnerCardId;
                }
            }

            void RewriteNode(JsonNode? node)
            {
                if (node == null) return;

                if (node is JsonObject obj)
                {
                    var keys = obj.Select(kvp => kvp.Key).ToList();
                    foreach (var key in keys)
                    {
                        var val = obj[key];
                        if (val == null) continue;

                        if (CardDefinitionIdParamKeys.Contains(key))
                        {
                            var rewrittenParam = RewriteIntIdParameterNode(val, cardIdMap);
                            obj[key] = rewrittenParam;
                            RewriteNode(rewrittenParam);
                            continue;
                        }

                        if (AbilityIdParamKeys.Contains(key))
                        {
                            var rewrittenParam = RewriteLongIdParameterNode(val, abilityIdMap);
                            obj[key] = rewrittenParam;
                            RewriteNode(rewrittenParam);
                            continue;
                        }

                        if (DslExpressionPropertyKeys.Contains(key)
                            && val is JsonValue exprVal
                            && exprVal.TryGetValue<string>(out var exprText)
                            && !string.IsNullOrWhiteSpace(exprText))
                        {
                            var rewrittenExpr = DslClassicPoolRegex.Replace(
                                exprText,
                                $"db.{DslEnvironmentCardPoolNames.CurrentEnvironmentCardsPropertyName}");

                            rewrittenExpr = DslCardIdEqualityRegex.Replace(rewrittenExpr, match =>
                            {
                                if (int.TryParse(match.Groups["id"].Value, out var oldId)
                                    && cardIdMap.TryGetValue(oldId, out var mappedId))
                                {
                                    return $"{match.Groups["prop"].Value} == {mappedId}";
                                }
                                return match.Value;
                            });

                            rewrittenExpr = DslCardIdInListRegex.Replace(rewrittenExpr, match =>
                            {
                                var prop = match.Groups["prop"].Value;
                                var mappedTokens = match.Groups["list"].Value
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Select(token => int.TryParse(token, out var oldId) && cardIdMap.TryGetValue(oldId, out var mappedId)
                                        ? mappedId.ToString()
                                        : token);
                                return $"{prop} in [{string.Join(", ", mappedTokens)}]";
                            });

                            obj[key] = rewrittenExpr;
                            continue;
                        }

                        RewriteNode(val);
                    }
                }
                else if (node is JsonArray arr)
                {
                    for (int i = 0; i < arr.Count; i++)
                    {
                        RewriteNode(arr[i]);
                    }
                }
            }

            RewriteNode(root);
            return root?.ToJsonString();
        }

        public static async Task<(bool Valid, string? ErrorMessage)> ValidateAbilityJsonScopeAsync(
            ApplicationDbContext db,
            string? jsonConfig,
            bool ownerIsMirageCard,
            CancellationToken ct = default)
        {
            var analysis = ExtractAbilityConfigReferences(jsonConfig);
            if (ownerIsMirageCard && analysis.UsesClassicNamedEnvironmentPool)
            {
                return (false, "幻境卡牌能力禁止访问经典环境命名卡池（如 db.openCards / db.rotationCards），请使用 db.currentEnvironmentCards。");
            }

            if (analysis.ReferencedCardIds.Count > 0)
            {
                var ids = analysis.ReferencedCardIds.ToList();
                var foundCards = await db.CardDefinitions
                    .AsNoTracking()
                    .Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.IsMirageCard })
                    .ToListAsync(ct);

                var byId = foundCards.ToDictionary(c => c.Id, c => c.IsMirageCard);
                foreach (var id in ids)
                {
                    if (!byId.TryGetValue(id, out var isMirage) || isMirage != ownerIsMirageCard)
                    {
                        return (false, $"能力配置引用的卡牌定义 #{id} 不存在或与当前卡牌所属环境（{(ownerIsMirageCard ? "幻境" : "经典")}）不一致。");
                    }
                }
            }

            if (analysis.ReferencedAbilityIds.Count > 0)
            {
                var abilityIds = analysis.ReferencedAbilityIds.ToList();
                var foundAbilities = await (
                    from a in db.CardAbilities.AsNoTracking()
                    join c in db.CardDefinitions.AsNoTracking() on a.CardId equals c.Id
                    where abilityIds.Contains(a.Id)
                    select new { a.Id, c.IsMirageCard })
                    .ToListAsync(ct);

                var byAbilityId = foundAbilities.ToDictionary(a => a.Id, a => a.IsMirageCard);
                foreach (var abilityId in abilityIds)
                {
                    if (!byAbilityId.TryGetValue(abilityId, out var isMirage) || isMirage != ownerIsMirageCard)
                    {
                        return (false, $"能力配置引用的能力 #{abilityId} 不存在或与当前卡牌所属环境（{(ownerIsMirageCard ? "幻境" : "经典")}）不一致。");
                    }
                }
            }

            return (true, null);
        }

        public static string? CopyCardArtFileForImport(
            string workspaceRoot,
            string? sourceArtPath,
            int targetCardId,
            string kind)
        {
            if (string.IsNullOrWhiteSpace(sourceArtPath))
            {
                return null;
            }

            var normalized = sourceArtPath.Replace('\\', '/').Trim();
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return normalized;
            }

            var queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                normalized = normalized[..queryIndex];
            }

            if (!normalized.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var sourceDiskPath = Path.GetFullPath(Path.Combine(workspaceRoot, relative));
            var assetsRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "assets")) + Path.DirectorySeparatorChar;
            if (!sourceDiskPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(sourceDiskPath))
            {
                return null;
            }

            var extension = Path.GetExtension(sourceDiskPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".webp";
            }

            var targetFolder = Path.Combine(workspaceRoot, "assets", "cards", targetCardId.ToString());
            Directory.CreateDirectory(targetFolder);
            var versionToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var targetFileName = $"{kind}-{versionToken}-{Guid.NewGuid():N}{extension}";
            var targetDiskPath = Path.Combine(targetFolder, targetFileName);
            File.Copy(sourceDiskPath, targetDiskPath, overwrite: true);
            return $"/assets/cards/{targetCardId}/{targetFileName}";
        }

        private static void CollectIntIdsFromParameterNode(JsonNode node, HashSet<int> target)
        {
            if (node is JsonValue val)
            {
                if (TryExtractInt(val, out var parsed) && parsed > 0)
                {
                    target.Add(parsed);
                }
                else if (val.TryGetValue<string>(out var rawStr) && TryExtractIntListLiteral(rawStr, out var listIds))
                {
                    foreach (var item in listIds)
                    {
                        if (item > 0) target.Add(item);
                    }
                }
                return;
            }

            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null) CollectIntIdsFromParameterNode(item, target);
                }
                return;
            }

            if (node is JsonObject obj)
            {
                foreach (var valueKey in new[] { "value", "Value" })
                {
                    if (obj.TryGetPropertyValue(valueKey, out var vNode) && vNode != null)
                    {
                        CollectIntIdsFromParameterNode(vNode, target);
                    }
                }

                foreach (var exprKey in new[] { "expression", "Expression", "formula", "Formula" })
                {
                    if (obj.TryGetPropertyValue(exprKey, out var exprNode) && exprNode is JsonValue ev)
                    {
                        if (TryExtractInt(ev, out var parsedId) && parsedId > 0)
                        {
                            target.Add(parsedId);
                        }
                        else if (ev.TryGetValue<string>(out var exprStr) && TryExtractIntListLiteral(exprStr, out var listIds))
                        {
                            foreach (var item in listIds)
                            {
                                if (item > 0) target.Add(item);
                            }
                        }
                    }
                }
            }
        }

        private static void CollectLongIdsFromParameterNode(JsonNode node, HashSet<long> target)
        {
            if (node is JsonValue val)
            {
                if (TryExtractLong(val, out var parsed) && parsed > 0)
                {
                    target.Add(parsed);
                }
                else if (val.TryGetValue<string>(out var rawStr) && TryExtractLongListLiteral(rawStr, out var listIds))
                {
                    foreach (var item in listIds)
                    {
                        if (item > 0) target.Add(item);
                    }
                }
                return;
            }

            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null) CollectLongIdsFromParameterNode(item, target);
                }
                return;
            }

            if (node is JsonObject obj)
            {
                foreach (var valueKey in new[] { "value", "Value" })
                {
                    if (obj.TryGetPropertyValue(valueKey, out var vNode) && vNode != null)
                    {
                        CollectLongIdsFromParameterNode(vNode, target);
                    }
                }

                foreach (var exprKey in new[] { "expression", "Expression", "formula", "Formula" })
                {
                    if (obj.TryGetPropertyValue(exprKey, out var exprNode) && exprNode is JsonValue ev)
                    {
                        if (TryExtractLong(ev, out var parsedId) && parsedId > 0)
                        {
                            target.Add(parsedId);
                        }
                        else if (ev.TryGetValue<string>(out var exprStr) && TryExtractLongListLiteral(exprStr, out var listIds))
                        {
                            foreach (var item in listIds)
                            {
                                if (item > 0) target.Add(item);
                            }
                        }
                    }
                }
            }
        }

        private static JsonNode RewriteIntIdParameterNode(JsonNode node, IReadOnlyDictionary<int, int> idMap)
        {
            if (node is JsonValue val)
            {
                if (TryExtractInt(val, out var oldId) && idMap.TryGetValue(oldId, out var newId))
                {
                    return val.GetValueKind() == JsonValueKind.String
                        ? JsonValue.Create(newId.ToString())!
                        : JsonValue.Create(newId)!;
                }
                if (val.TryGetValue<string>(out var rawStr) && TryRewriteIntListLiteral(rawStr, idMap, out var rewrittenList))
                {
                    return JsonValue.Create(rewrittenList)!;
                }
                return node.DeepClone();
            }

            if (node is JsonArray arr)
            {
                var nextArr = new JsonArray();
                foreach (var item in arr)
                {
                    nextArr.Add(item == null ? null : RewriteIntIdParameterNode(item, idMap));
                }
                return nextArr;
            }

            if (node is JsonObject obj)
            {
                var cloned = (JsonObject)obj.DeepClone();
                foreach (var valueKey in new[] { "value", "Value" })
                {
                    if (cloned.TryGetPropertyValue(valueKey, out var vNode) && vNode != null)
                    {
                        cloned[valueKey] = RewriteIntIdParameterNode(vNode, idMap);
                    }
                }

                foreach (var exprKey in new[] { "expression", "Expression", "formula", "Formula" })
                {
                    if (cloned.TryGetPropertyValue(exprKey, out var exprNode) && exprNode is JsonValue ev)
                    {
                        if (TryExtractInt(ev, out var oldId) && idMap.TryGetValue(oldId, out var mappedId))
                        {
                            cloned[exprKey] = mappedId.ToString();
                        }
                        else if (ev.TryGetValue<string>(out var exprStr) && TryRewriteIntListLiteral(exprStr, idMap, out var rewrittenList))
                        {
                            cloned[exprKey] = rewrittenList;
                        }
                    }
                }
                return cloned;
            }

            return node.DeepClone();
        }

        private static JsonNode RewriteLongIdParameterNode(JsonNode node, IReadOnlyDictionary<long, long> idMap)
        {
            if (node is JsonValue val)
            {
                if (TryExtractLong(val, out var oldId) && idMap.TryGetValue(oldId, out var newId))
                {
                    return val.GetValueKind() == JsonValueKind.String
                        ? JsonValue.Create(newId.ToString())!
                        : JsonValue.Create(newId)!;
                }
                if (val.TryGetValue<string>(out var rawStr) && TryRewriteLongListLiteral(rawStr, idMap, out var rewrittenList))
                {
                    return JsonValue.Create(rewrittenList)!;
                }
                return node.DeepClone();
            }

            if (node is JsonArray arr)
            {
                var nextArr = new JsonArray();
                foreach (var item in arr)
                {
                    nextArr.Add(item == null ? null : RewriteLongIdParameterNode(item, idMap));
                }
                return nextArr;
            }

            if (node is JsonObject obj)
            {
                var cloned = (JsonObject)obj.DeepClone();
                foreach (var valueKey in new[] { "value", "Value" })
                {
                    if (cloned.TryGetPropertyValue(valueKey, out var vNode) && vNode != null)
                    {
                        cloned[valueKey] = RewriteLongIdParameterNode(vNode, idMap);
                    }
                }

                foreach (var exprKey in new[] { "expression", "Expression", "formula", "Formula" })
                {
                    if (cloned.TryGetPropertyValue(exprKey, out var exprNode) && exprNode is JsonValue ev)
                    {
                        if (TryExtractLong(ev, out var oldId) && idMap.TryGetValue(oldId, out var mappedId))
                        {
                            cloned[exprKey] = mappedId.ToString();
                        }
                        else if (ev.TryGetValue<string>(out var exprStr) && TryRewriteLongListLiteral(exprStr, idMap, out var rewrittenList))
                        {
                            cloned[exprKey] = rewrittenList;
                        }
                    }
                }
                return cloned;
            }

            return node.DeepClone();
        }

        private static bool TryExtractIntListLiteral(string? raw, out List<int> values)
        {
            values = new List<int>();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var match = IntegerListLiteralRegex.Match(raw);
            if (!match.Success) return false;
            var body = match.Groups["body"].Value;
            if (string.IsNullOrWhiteSpace(body)) return true;
            foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out var id)) return false;
                values.Add(id);
            }
            return true;
        }

        private static bool TryExtractLongListLiteral(string? raw, out List<long> values)
        {
            values = new List<long>();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var match = IntegerListLiteralRegex.Match(raw);
            if (!match.Success) return false;
            var body = match.Groups["body"].Value;
            if (string.IsNullOrWhiteSpace(body)) return true;
            foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(part, out var id)) return false;
                values.Add(id);
            }
            return true;
        }

        private static bool TryRewriteIntListLiteral(string? raw, IReadOnlyDictionary<int, int> idMap, out string rewritten)
        {
            rewritten = string.Empty;
            if (!TryExtractIntListLiteral(raw, out var items) || items.Count == 0)
            {
                return false;
            }

            var changed = false;
            var mapped = new List<string>(items.Count);
            foreach (var item in items)
            {
                if (idMap.TryGetValue(item, out var nextId))
                {
                    mapped.Add(nextId.ToString());
                    changed = true;
                }
                else
                {
                    mapped.Add(item.ToString());
                }
            }

            if (!changed) return false;
            rewritten = $"[{string.Join(", ", mapped)}]";
            return true;
        }

        private static bool TryRewriteLongListLiteral(string? raw, IReadOnlyDictionary<long, long> idMap, out string rewritten)
        {
            rewritten = string.Empty;
            if (!TryExtractLongListLiteral(raw, out var items) || items.Count == 0)
            {
                return false;
            }

            var changed = false;
            var mapped = new List<string>(items.Count);
            foreach (var item in items)
            {
                if (idMap.TryGetValue(item, out var nextId))
                {
                    mapped.Add(nextId.ToString());
                    changed = true;
                }
                else
                {
                    mapped.Add(item.ToString());
                }
            }

            if (!changed) return false;
            rewritten = $"[{string.Join(", ", mapped)}]";
            return true;
        }

        private static bool TryExtractInt(JsonValue val, out int parsed)
        {
            if (val.TryGetValue<int>(out parsed)) return true;
            if (val.TryGetValue<long>(out var l) && l >= int.MinValue && l <= int.MaxValue)
            {
                parsed = (int)l;
                return true;
            }
            if (val.TryGetValue<string>(out var s) && int.TryParse(s?.Trim(), out parsed))
            {
                return true;
            }
            parsed = 0;
            return false;
        }

        private static bool TryExtractLong(JsonValue val, out long parsed)
        {
            if (val.TryGetValue<long>(out parsed)) return true;
            if (val.TryGetValue<int>(out var i))
            {
                parsed = i;
                return true;
            }
            if (val.TryGetValue<string>(out var s) && long.TryParse(s?.Trim(), out parsed))
            {
                return true;
            }
            parsed = 0;
            return false;
        }
    }
}
