using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiPlanArchiveTests
{
    private static readonly Type TextFacts = typeof(AiService).Assembly.GetType(
        "ShadowverseBeyond.Server.Services.AiPlanTextFacts")!;
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        const int limit = 16384;
        const string prefix = "T6 pressure: ";
        JsonElement Archive(string text) => JsonSerializer.SerializeToElement(
            TextFacts.GetMethod("ForArchive", Hidden)!.Invoke(null, [2, 6, "pressure", text]), Json);

        var exact = Archive(new string('中', limit - prefix.Length));
        Check(exact.GetProperty("value").GetString()!.Length == limit
            && !exact.GetProperty("capture").GetProperty("truncated").GetBoolean(),
            "exact archive limit was marked truncated");
        var unicode = Archive(new string('中', limit - prefix.Length - 1) + "🌙尾部");
        var text = unicode.GetProperty("value").GetString()!;
        var capture = unicode.GetProperty("capture");
        Check(text.Length == limit - 1 && !char.IsSurrogate(text[^1])
            && capture.GetProperty("index").GetInt32() == 2
            && capture.GetProperty("storedCharacters").GetInt32() == text.Length
            && capture.GetProperty("originalCharacters").GetInt32() == limit + 3
            && capture.GetProperty("truncated").GetBoolean(),
            "bounded archive split Unicode or misreported omitted characters");

        var path = Path.Combine(Path.GetTempPath(), "ai-plan-archive-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var fixture = new AiActionOutcomeTests.Fixture();
            fixture.Own.InitialDeckDefinitionIds = [2, 3];
            fixture.AddOwnHandCard();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiStrategy:Enabled"] = "true",
                ["AiStrategy:GuideCacheEnabled"] = "false",
                ["AiStrategy:Endpoint"] = "https://example.invalid/chat/completions",
                ["AiStrategy:Model"] = "scripted-archive-test",
                ["AiStrategy:MemoryPath"] = path
            }).Build();
            using var provider = new ScriptedProvider();
            using var planner = new AiStrategyPlanner(provider, config, new Host(), NullLogger<AiStrategyPlanner>.Instance);
            planner.WarmDeckAsync(fixture.Game, fixture.Own.PlayerId, "Neutral")!.GetAwaiter().GetResult();
            var expected = new[]
            {
                "使用#2。" + new string('文', 600) + "ARCHIVE_ONLY_TAIL_完整后续条件",
                new string('测', 20000) + "TRUNCATED_TAIL"
            };
            for (var index = 0; index < expected.Length; index++)
            {
                fixture.Own.TurnNumber = 6 + index * 2;
                provider.Explanation = expected[index];
                var plan = planner.PlanAsync(fixture.Game, fixture.Snapshot(), fixture.Own.PlayerId,
                    CancellationToken.None, (_, _) => Task.FromResult(new PlayabilityResultDto
                    { IsPlayable = true, SelectedPlayMode = PlayMode.Base,
                        PlayModes = [new() { Mode = PlayMode.Base, EffectiveCost = 0, IsAvailable = true }] }))
                    .GetAwaiter().GetResult();
                Check(plan is not null && plan.Plan.Length == 320, "short live plan unexpectedly changed");
            }
            var committed = fixture.Commit(new GameEndEvent(fixture.Own.PlayerId, fixture.Enemy.PlayerId, "leader defeat"));
            planner.ObserveCommittedEvents(fixture.Game, committed);
            var file = Directory.GetFiles(path, "*.json").Single();
            var lesson = JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(file), Json)!.Single();
            var full = "T6 setup: " + expected[0].Replace("#2", "public source");
            Check(lesson.Plans[0] == full && lesson.Plans[0].EndsWith("完整后续条件")
                && lesson.PlanTextCaptures is { Length: 2 }
                && lesson.PlanTextCaptures[0] == new AiStrategyPlanner.PlanTextCapture(0, full.Length, full.Length, false),
                "accepted explanation tail or completeness metadata was lost before finished-history persistence");
            Check(lesson.Plans[1].Length == limit && lesson.PlanTextCaptures![1].Truncated
                && lesson.PlanTextCaptures[1].StoredCharacters == limit
                && lesson.PlanTextCaptures[1].OriginalCharacters > limit,
                "oversized production explanation escaped the archive bound");
            var history = JsonSerializer.SerializeToElement(typeof(AiStrategyPlanner)
                .GetMethod("ReadHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(planner, null), Json);
            Check(history[0].GetProperty("lesson").GetProperty("plans")[0].GetString() == full,
                "history API lost the persisted explanation");
            var review = JsonSerializer.SerializeToElement(TextFacts.GetMethod("ForReview", Hidden)!
                .Invoke(null, [lesson.Plans, 8192]), Json);
            Check(review.GetProperty("texts")[0].GetProperty("value").GetString() == full
                && review.GetProperty("contentBytes").GetInt32() <= 8192
                && review.GetProperty("omitted").GetInt32() == 1,
                "review lost the complete ordinary plan or exceeded its existing request budget");
            Check(provider.Bodies.Count == 3 && !provider.Bodies.Any(body => body.Contains("ARCHIVE_ONLY_TAIL_"))
                && !provider.Bodies.Any(body => body.Contains("PRIVATE_CANARY")) && fixture.Http.Requests == 0,
                "full archive entered live context, private cards leaked, or unexpected provider requests occurred");
            var old = JsonSerializer.Deserialize<AiStrategyPlanner.Lesson>(
                """{"opponentClass":"Neutral","first":true,"won":false,"turns":6,"endReason":"old","plans":["old clipped explanation"],"played":[]}""", Json)!;
            Check(old.PlanTextCaptures is null && old.Plans.Single() == "old clipped explanation",
                "legacy history was falsely marked complete");
            Console.WriteLine("PASS plan archives: real planning and committed end -> disk -> history -> bounded review; ordinary tails preserved, Unicode-safe cap, legacy completeness unknown, live context bounded; scripted HTTP only, real provider requests=0");
        }
        finally
        {
            var resolved = Path.GetFullPath(path);
            Check(resolved.StartsWith(Path.Combine(Path.GetTempPath(), "ai-plan-archive-"), StringComparison.OrdinalIgnoreCase),
                "unexpected test cleanup path");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }

    private sealed class ScriptedProvider : HttpMessageHandler, IHttpClientFactory
    {
        internal string Explanation = "";
        internal readonly List<string> Bodies = [];
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = await request.Content!.ReadAsStringAsync(token);
            Bodies.Add(body);
            using var envelope = JsonDocument.Parse(body);
            var messages = envelope.RootElement.GetProperty("messages");
            using var input = JsonDocument.Parse(messages[messages.GetArrayLength() - 1].GetProperty("content").GetString()!);
            var answer = input.RootElement.TryGetProperty("own", out _)
                ? JsonSerializer.Serialize(new { focus = "setup", plan = Explanation, prioritize = Array.Empty<int>(), hold = Array.Empty<int>() })
                : """{"gamePlan":"archive fixture","cards":[{"id":2,"role":"public tempo"},{"id":3,"role":"reward"}]}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                new { choices = new[] { new { message = new { content = answer } } } })) };
        }
    }

    private sealed class Host : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "offline";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
