using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiRuleContextTests
{
    private static readonly MethodInfo Collect = typeof(AiStrategyPlanner).Assembly
        .GetType("ShadowverseBeyond.Server.Services.AiRuleContext")!
        .GetMethod("Collect", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static JsonElement Catalog(string[] texts, Dictionary<int,CardDefinition> cards, int limit = 40) =>
        JsonSerializer.SerializeToElement(Collect.Invoke(null, [texts, cards, limit]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    internal static void Run()
    {
        var full = new string('文', 1800) + "完整尾部条件『瓶』";
        var cards = new Dictionary<int,CardDefinition>
        {
            [2] = new() { Id=2, Name="瓶", Cost=1, Type=CardType.Spell, Description="增加1张『妖精』。另一个『未知规则片段』。" },
            [3] = new() { Id=3, Name="妖精", Cost=0, Class=CardClass.Forestcraft, Tribe=["妖精"], Countdown=2,
                IsCollectible=false, Type=CardType.Follower, Attack=1, Health=1, Keywords=["Storm"], Description=full },
            [4] = new() { Id=4, Name="瓶", Cost=9, Type=CardType.Amulet, Description="另一张同名卡" },
            [5] = new() { Id=5, Name="其他", Cost=2, Description="没有更多引用" }
        };
        var result = Catalog(["由『瓶』获得卡片"], cards);
        var rows = result.GetProperty("cards").EnumerateArray().ToArray();
        Check(rows.Select(row => row.GetProperty("id").GetInt32()).SequenceEqual([2,3,4]),
            "recursive or same-name reference definitions were missing or duplicated");
        var token = rows.Single(row => row.GetProperty("id").GetInt32() == 3);
        Check(token.GetProperty("text").GetString() == full && token.GetProperty("cost").GetInt32() == 0
            && token.GetProperty("keywords")[0].GetString() == "Storm" && token.GetProperty("class").GetString() == "Forestcraft"
            && token.GetProperty("tribe")[0].GetString() == "妖精" && token.GetProperty("countdown").GetInt32() == 2
            && !token.GetProperty("isCollectible").GetBoolean(), "referenced card lost full text, real cost or keywords");
        Check(result.GetProperty("unresolvedNames")[0].GetString() == "未知规则片段"
            && result.GetProperty("unresolvedCount").GetInt32() == 1 && result.GetProperty("omittedCount").GetInt32() == 0,
            "unresolved quoted text was hidden or mistaken for a known card rule");
        var reordered = cards.Reverse().ToDictionary(item => item.Key, item => item.Value);
        Check(Catalog(["『瓶』", "『其他』"], cards).GetRawText() == Catalog(["『其他』", "『瓶』"], reordered).GetRawText(),
            "equivalent reference rules changed ordering and broke shared prefix stability");
        var limited = Catalog(["『瓶』", "『其他』"], cards, 1);
        Check(limited.GetProperty("cards").GetArrayLength() == 1 && limited.GetProperty("omittedCount").GetInt32() == 2
            && limited.GetProperty("cards")[0].GetProperty("id").GetInt32() == 5
            && limited.GetProperty("omittedNames").EnumerateArray().Select(name => name.GetString()).ToHashSet().SetEquals(["瓶", "妖精"])
            && limited.GetProperty("unresolvedCount").GetInt32() == 1,
            "bounded reference expansion concealed downstream missing context or failed on cycles");
        var malformed = Catalog([new string('『', 50000)], cards);
        Check(malformed.GetProperty("cards").GetArrayLength() == 0 && malformed.GetProperty("unresolvedCount").GetInt32() == 0,
            "unterminated reference text caused false card rules");
        var none = Catalog([], cards);
        Check(none.GetProperty("cards").GetArrayLength() == 0 && none.GetProperty("unresolvedCount").GetInt32() == 0,
            "no-reference card acquired unrelated rules");
    }

    internal static void Probe(string database, string output)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource=Path.GetFullPath(database), Mode=SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Name,Description,Cost,Attack,Health,Type,Keywords,Class,Tribe,Countdown,IsCollectible FROM CardDefinitions";
        var cards = new Dictionary<int,CardDefinition>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                var card = new CardDefinition { Id=reader.GetInt32(0), Name=reader.GetString(1), Description=reader.GetString(2),
                    Cost=reader.GetInt32(3), Attack=reader.IsDBNull(4)?null:reader.GetInt32(4), Health=reader.IsDBNull(5)?null:reader.GetInt32(5),
                    Type=Enum.Parse<CardType>(reader.GetString(6), true), Keywords=reader.IsDBNull(7)?[]:JsonSerializer.Deserialize<List<string>>(reader.GetString(7)),
                    Class=Enum.Parse<CardClass>(reader.GetString(8), true),
                    Tribe=reader.IsDBNull(9)?[]:JsonSerializer.Deserialize<List<string>>(reader.GetString(9)),
                    Countdown=reader.IsDBNull(10)?null:reader.GetInt32(10), IsCollectible=reader.GetBoolean(11) };
                cards[card.Id] = card;
            }
        var roots = new[] {1471888,1474120}.Where(cards.ContainsKey).Select(id => new
            { id, references=Catalog([cards[id].Description], cards) }).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(new { roots }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
        var lufika = roots.Single(row => row.id == 1471888).references.GetProperty("cards").EnumerateArray().Select(row => row.GetProperty("id").GetInt32());
        Check(lufika.Contains(1471896) && lufika.Contains(1010402), "real Lufika to bottle to Fairy chain remained incomplete");
        var token = roots.Single(row => row.id == 1471888).references.GetProperty("cards").EnumerateArray()
            .Single(row => row.GetProperty("id").GetInt32() == 1010402);
        Check(token.GetProperty("type").GetString() == "Follower" && token.GetProperty("cost").GetInt32() == 1,
            "current catalog probe misread database enum or actual card cost");
    }
}
