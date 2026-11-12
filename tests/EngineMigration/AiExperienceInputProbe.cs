using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services;

internal static class AiExperienceInputProbe
{
    internal static void Run(string database,string directory,string output)
    {
        var original=GameDataProvider.Instance;
        var copy=Path.Combine(Path.GetTempPath(),"ai-experience-input-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var target=new SqliteConnection("Data Source="+copy)){input.Open();target.Open();input.BackupDatabase(target);}
        using var services=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite("Data Source="+copy+";Mode=ReadOnly"))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
        var select=typeof(AiStrategyPlanner).GetMethod("SelectModelLessons",BindingFlags.Static|BindingFlags.NonPublic)!;
        var live=typeof(AiStrategyPlanner).GetMethod("LessonForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
        var rows=new List<object>();
        try {
            var catalog=new GameDataProvider(services.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            catalog.InitializeAsync().GetAwaiter().GetResult();GameDataProvider.SetInstanceForTesting(catalog);
            foreach(var file in Directory.EnumerateFiles(directory,"*.json").Where(file=>!file.EndsWith(".reflection.json",StringComparison.OrdinalIgnoreCase)).Order()) {
                var bytes=File.ReadAllBytes(file);using var document=JsonDocument.Parse(bytes);
                if(document.RootElement.ValueKind!=JsonValueKind.Array)continue;
                var lessons=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(bytes,json)??[];
                if(lessons.Length==0)continue;
                var chosen=(AiStrategyPlanner.Lesson[])select.Invoke(null,[lessons,lessons.Last().OpponentClass])!;
                var projections=chosen.Select(lesson=>JsonSerializer.SerializeToElement(live.Invoke(null,[lesson]),json)).ToArray();
                var observed=projections.Select(row=>row.GetProperty("observed").EnumerateObject()
                    .Where(item=>item.Name is not ("leaderHealthChanges" or "completedActions")||item.Value.ValueKind!=JsonValueKind.Null)
                    .ToDictionary(item=>item.Name,item=>item.Value)).ToArray();
                var rawPlanBytes=chosen.Sum(lesson=>JsonSerializer.SerializeToUtf8Bytes(lesson.Plans,json).Length);
                var packed=JsonSerializer.SerializeToUtf8Bytes(projections,json);
                rows.Add(new {file=Path.GetFileName(file),sourceSha256=Convert.ToHexString(SHA256.HashData(bytes)),games=lessons.Length,
                    selected=chosen.Length,liveBytes=packed.Length,storedRawPlanBytes=rawPlanBytes,
                    observedSha256=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(observed,json))),
                    rawPlanStringsPresent=chosen.SelectMany(lesson=>lesson.Plans).Count(plan=>System.Text.Encoding.UTF8.GetString(packed).Contains(JsonSerializer.Serialize(plan,json)[1..^1],StringComparison.Ordinal))});
                if(!bytes.SequenceEqual(File.ReadAllBytes(file)))throw new InvalidOperationException("History changed during audit; do not use this measurement");
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new {realProviderRequests=0,readOnlyHistory=true,scope="Bytes of selected live experience projections only, not token billing, total requests or measured cache hit rate.",rows},json));
            Console.WriteLine("Read-only experience input audit completed; provider requests=0");
        } finally {
            GameDataProvider.SetInstanceForTesting(original);SqliteConnection.ClearAllPools();
            if(!Path.GetFullPath(copy).StartsWith(Path.Combine(Path.GetTempPath(),"ai-experience-input-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected audit cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
    }
}
