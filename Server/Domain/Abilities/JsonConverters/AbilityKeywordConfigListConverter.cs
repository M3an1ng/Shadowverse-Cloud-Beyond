using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    public sealed class AbilityKeywordConfigListConverter : JsonConverter<List<AbilityKeywordConfig>>
    {
        public override List<AbilityKeywordConfig> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return ParseElement(document.RootElement);
        }

        public override void Write(Utf8JsonWriter writer, List<AbilityKeywordConfig> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var keyword in value ?? new List<AbilityKeywordConfig>())
            {
                writer.WriteStartObject();
                writer.WriteString("keyword", keyword.Keyword.ToString());
                if (keyword.Value.HasValue)
                {
                    writer.WriteNumber("value", keyword.Value.Value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static List<AbilityKeywordConfig> ParseElement(JsonElement element)
        {
            var result = new List<AbilityKeywordConfig>();

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    AddParsed(result, ParseKeywordText(element.GetString()));
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var numeric) && Enum.IsDefined(typeof(AbilityKeyword), numeric))
                    {
                        result.Add(new AbilityKeywordConfig { Keyword = (AbilityKeyword)numeric });
                    }
                    break;
                case JsonValueKind.Object:
                    AddParsed(result, ParseKeywordObject(element));
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        switch (item.ValueKind)
                        {
                            case JsonValueKind.String:
                                AddParsed(result, ParseKeywordText(item.GetString()));
                                break;
                            case JsonValueKind.Number:
                                if (item.TryGetInt32(out var arrayNumeric) && Enum.IsDefined(typeof(AbilityKeyword), arrayNumeric))
                                {
                                    result.Add(new AbilityKeywordConfig { Keyword = (AbilityKeyword)arrayNumeric });
                                }
                                break;
                            case JsonValueKind.Object:
                                AddParsed(result, ParseKeywordObject(item));
                                break;
                        }
                    }
                    break;
            }

            return result;
        }

        private static AbilityKeywordConfig? ParseKeywordText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();
            var keywordName = trimmed;
            var value = ParseInlineValue(trimmed, out keywordName);

            var keyword = Keywords.ParseEffectKeywordOrDefault(keywordName, AbilityKeyword.None);
            if (keyword == AbilityKeyword.None)
            {
                return null;
            }

            return new AbilityKeywordConfig
            {
                Keyword = keyword,
                Value = value
            };
        }

        private static AbilityKeywordConfig? ParseKeywordObject(JsonElement element)
        {
            if (!TryGetKeywordText(element, out var keywordText))
            {
                return null;
            }

            var keyword = Keywords.ParseEffectKeywordOrDefault(keywordText, AbilityKeyword.None);
            if (keyword == AbilityKeyword.None)
            {
                return null;
            }

            int? value = null;
            if (TryGetInt(element, "value", out var parsedValue) ||
                TryGetInt(element, "amount", out parsedValue) ||
                TryGetInt(element, "cost", out parsedValue))
            {
                value = parsedValue;
            }

            return new AbilityKeywordConfig
            {
                Keyword = keyword,
                Value = value
            };
        }

        private static bool TryGetKeywordText(JsonElement element, out string? keywordText)
        {
            if (element.TryGetProperty("keyword", out var keywordProp) && keywordProp.ValueKind == JsonValueKind.String)
            {
                keywordText = keywordProp.GetString();
                return true;
            }

            if (element.TryGetProperty("tag", out var tagProp) && tagProp.ValueKind == JsonValueKind.String)
            {
                keywordText = tagProp.GetString();
                return true;
            }

            keywordText = null;
            return false;
        }

        private static bool TryGetInt(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            {
                return true;
            }

            return false;
        }

        private static int? ParseInlineValue(string text, out string keywordName)
        {
            keywordName = text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var colonIndex = text.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex > 0)
            {
                keywordName = text[..colonIndex].Trim();
                return ParseNumber(text[(colonIndex + 1)..]);
            }

            var equalsIndex = text.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0)
            {
                keywordName = text[..equalsIndex].Trim();
                return ParseNumber(text[(equalsIndex + 1)..]);
            }

            var leftParenIndex = text.IndexOf('(', StringComparison.Ordinal);
            if (leftParenIndex > 0 && text.EndsWith(")", StringComparison.Ordinal))
            {
                keywordName = text[..leftParenIndex].Trim();
                var inner = text[(leftParenIndex + 1)..^1];
                return ParseNumber(inner);
            }

            return null;
        }

        private static int? ParseNumber(string? valueText)
        {
            if (string.IsNullOrWhiteSpace(valueText))
            {
                return null;
            }

            return int.TryParse(valueText.Trim(), out var parsed) ? parsed : null;
        }

        private static void AddParsed(List<AbilityKeywordConfig> list, AbilityKeywordConfig? keyword)
        {
            if (keyword == null || keyword.Keyword == AbilityKeyword.None)
            {
                return;
            }

            list.Add(keyword);
        }
    }
}
