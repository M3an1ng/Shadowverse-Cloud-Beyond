using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

// Optional, explicitly enabled real-provider semantic probe. It never saves player games or model weights.
internal static class AiRuleReasoningProbe
{
    internal static void Run(string output)
    {
        if(Environment.GetEnvironmentVariable("AI_ALLOW_PAID_PROBES")!="true")
            throw new InvalidOperationException("Paid AI probe disabled; explicit AI_ALLOW_PAID_PROBES=true opt-in is required.");
        var key = Environment.GetEnvironmentVariable("AI_RULE_PROBE_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Rule probe provider key is not configured");
        var original = GameDataProvider.Instance;
        var definitions = new Dictionary<int,CardDefinition>
        {
            [1] = new() { Id=1, Name="校准样本·甲", Cost=2, Class=CardClass.Neutral, Type=CardType.Follower, Attack=1, Health=1, Description="没有其他能力。" },
            [2] = new() { Id=2, Name="校准样本·乙", Cost=2, Class=CardClass.Abysscraft, Type=CardType.Follower, Attack=1, Health=1, Description="没有其他能力。" },
            [3] = new() { Id=3, Name="规则比较器", Cost=0, Type=CardType.Spell, Description="从当前提供的合法选项中选择1项。" }
        };
        var data = Proxy<IGameDataProvider>.Make((method,args) => method.Name switch
        {
            "GetCardDefinition" => definitions.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions" => definitions,
            "GetAbilityConfigs" => new List<AbilityConfig>(),
            _ => null
        });
        GameDataProvider.SetInstanceForTesting(data);
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["AiStrategy:Enabled"]="true", ["AiStrategy:Model"]="deepseek-flash",
                ["AiStrategy:Endpoint"]="https://api.deepseek.com/chat/completions", ["AiStrategy:ApiKey"]=key,
                ["AiStrategy:DisableThinking"]="true", ["AiStrategy:TimeoutMs"]="10000",
                ["AiStrategy:MemoryPath"]=Path.Combine(Path.GetDirectoryName(output)!, "isolated-memory")
            }).Build();
            using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            using var planner = new AiStrategyPlanner(services.GetRequiredService<IHttpClientFactory>(), config,
                new ProbeHost(), NullLogger<AiStrategyPlanner>.Instance);
            var own = new PlayerState(Guid.NewGuid()) { IsAI=true, TurnNumber=5, InitialDeckDefinitionIds=[1,2,3] };
            var other = new PlayerState(Guid.NewGuid());
            var game = new GameState("rule-reasoning-probe", own, other, own.PlayerId);
            typeof(GameState).GetProperty("StrategyChoicesOnly", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(game, true);
            var source = new CardInstance(Guid.NewGuid(), 3, own.PlayerId);
            var cases = new List<object>();
            var select = typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync", BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var scenario in new[] { "class-lethal", "full-health-draw" })
            foreach (var reverse in new[] { false, true })
            {
                var hp = scenario == "class-lethal" ? 20 : 12;
                var player = new PlayerStateDto { PlayerId=own.PlayerId, TurnNumber=own.TurnNumber, CurrentPP=2, MaxPP=2,
                    Class=CardClass.Forestcraft, DeckCount=20, LeaderCard=new() { CurrentHealth=hp, MaxHealth=hp },
                    Emblem=scenario == "class-lethal" ? [new() { CardId=4, Description="自己使用梦魇从者卡时，给予敌方的主战者2点伤害。" }] : [] };
                var enemy = new PlayerStateDto { PlayerId=other.PlayerId, DeckCount=20, LeaderCard=new() { CurrentHealth=scenario == "class-lethal"?2:20, MaxHealth=20 } };
                var snapshot = new GameStateDto { Players=[player,enemy] };
                planner.PlanAsync(game, snapshot, own.PlayerId, CancellationToken.None,
                    (_,_) => Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult();
                planner.WarmDeckAsync(game, own.PlayerId, other.Class.ToString())?.GetAwaiter().GetResult();
                if (!planner.CanRequest) throw new InvalidOperationException(planner.ProviderStatus.Message);
                var padding = string.Concat(Enumerable.Repeat("这是能力说明，以下文字不产生额外效果，只按最后一条规则结算。", 5));
                var options = scenario == "class-lethal"
                    ? new[] { definitions[1],definitions[2] }.Select(card => new CardInstanceDto { InstanceId=Guid.NewGuid(), CardId=card.Id,
                        Name=card.Name, Description=card.Description, Cost=card.Cost, CurrentCost=card.Cost, Class=card.Class,
                        Type=CardType.Follower, CurrentAttack=1, CurrentHealth=1, MaxHealth=1 }).ToList()
                    : new List<CardInstanceDto> { new() { InstanceId=Guid.NewGuid(), Name=padding+"回复自己的主战者5点生命值。", Type=CardType.Spell },
                        new() { InstanceId=Guid.NewGuid(), Name=padding+"由自己的牌堆中抽取1张卡片。", Type=CardType.Spell } };
                if (reverse) options.Reverse();
                var expected = scenario == "class-lethal" ? options.FindIndex(card => card.CardId==2)
                    : options.FindIndex(card => card.Name.EndsWith("抽取1张卡片。"));
                var request = new PlayerChoiceRequest { RequestingPlayerId=own.PlayerId, TargetKey=scenario=="class-lethal"?"discover":"decision",
                    MinSelectionCount=1, MaxSelectionCount=1, SelectableCardInstances=options,
                    SelectableTargetIds=options.Select(card=>card.InstanceId).ToList() };
                var timer = Stopwatch.StartNew();
                var chosen = ((Task<List<Guid>?>)select.Invoke(planner,[game,snapshot,request,source,null,CancellationToken.None])!).GetAwaiter().GetResult();
                var index = chosen is {Count:1} ? options.FindIndex(card=>card.InstanceId==chosen[0]) : -1;
                cases.Add(new { scenario, reverse, selected=index, expected, passed=index==expected, elapsedMs=timer.ElapsedMilliseconds });
                own.TurnNumber++;
            }
            var correct = cases.Count(item=>(bool)item.GetType().GetProperty("passed")!.GetValue(item)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new { correct, total=cases.Count, cases, usage=planner.ProviderUsage,
                provider=planner.ProviderStatus, scope="synthetic index-selection reasoning; no full effect resolution or win-rate claim" },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
            if (correct != cases.Count) throw new InvalidOperationException($"Rule reasoning probe: {correct}/{cases.Count} correct; inspect report");
        }
        finally { GameDataProvider.SetInstanceForTesting(original); }
    }
    private sealed class ProbeHost : IHostEnvironment
    {
        public string EnvironmentName {get;set;}="Development";
        public string ApplicationName {get;set;}="Rule probe";
        public string ContentRootPath {get;set;}=".";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
