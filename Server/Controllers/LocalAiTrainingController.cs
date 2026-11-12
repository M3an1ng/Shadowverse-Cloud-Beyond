using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Entities;
namespace ShadowverseBeyond.Server.Controllers;

public static class LocalAiTrainingAccess
{
    public static bool Allowed(HttpContext http,IWebHostEnvironment environment,IConfiguration configuration)
    {
        static bool Loopback(string? name)=>name is "localhost" or "127.0.0.1" or "::1" or "[::1]";
        var remote=http.Connection.RemoteIpAddress;
        if(!environment.IsDevelopment() || !configuration.GetValue<bool>("LocalAiTraining:Enabled") || remote==null || !IPAddress.IsLoopback(remote) || !Loopback(http.Request.Host.Host))return false;
        if(http.Request.Headers.ContainsKey("Forwarded")||http.Request.Headers.ContainsKey("X-Forwarded-For")||http.Request.Headers.ContainsKey("CF-Connecting-IP"))return false;
        var origin=http.Request.Headers.Origin.FirstOrDefault();
        return string.IsNullOrEmpty(origin)||Uri.TryCreate(origin,UriKind.Absolute,out var uri)&&Loopback(uri.Host);
    }
}
[ApiController,Route("api/local-ai")]
public sealed class LocalAiTrainingController(LocalAiTrainingService training,AiLearningModel model,ApplicationDbContext db,IWebHostEnvironment environment,IConfiguration configuration,AiStrategyPlanner strategy) : ControllerBase
{
    async Task<Guid?> Actor(CancellationToken token) {
        if(!LocalAiTrainingAccess.Allowed(HttpContext,environment,configuration))return null;
        if(!Guid.TryParse(Request.Headers["X-Editor-UserId"].FirstOrDefault(),out var id))return null;
        return await db.Users.AnyAsync(u=>u.Id==id&&u.IsActive&&u.Role==UserRole.Admin,token)?id:null;
    }
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken token)=>Ok(new{enabled=await Actor(token)!=null});
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken token)
    {
        if (await Actor(token) == null) return NotFound();
        var status = training.Status;
        var exampleCards = (status.StrategyMatches ?? []).SelectMany(match => match.ExperienceOutcomeExamples ?? [])
            .SelectMany(action => new[] { action.Action.CardId, action.Action.TargetCardId,
                action.Decision?.Local.CardId, action.Decision?.Local.TargetCardId }).OfType<int>().Distinct()
            .ToDictionary(id => id, id => GameDataProvider.Instance.GetCardDefinition(id)?.Name);
        return Ok(new{training=status,strategyExperienceBenchmarkSupported=true,strategyExampleCards=exampleCards,modelVersion=model.Current?.Version,models=model.Current?.Models.Count??0,specialists=model.Current?.Specialists.Count??0,postActionTrainingSamples=true,generatedTokenTactics=true,eternalResourceCore=true,nativeModeRuleFacts=true,nativeSummonEvolutionLethal=true,nativeSummonSourceLethal=true,ruleIdentityVerified=AiEngineRulesIdentity.Current.VerifiedInputs,ruleIdentityDocuments=AiEngineRulesIdentity.Current.RuleDocuments,ruleHistoryBuildsProven=AiEngineRulesIdentity.ProvenLegacyCount,ruleIdentityFingerprint=AiEngineRulesIdentity.Current.Fingerprint,guidePromptIdentity=true,drawContinuationsRevalidated=strategy.RevalidatesDrawContinuations,localTacticalInputs=true,nativeFirstActionAdvice=true,nativeCombatOrderingFacts=true,nativeCombatEffectProjection=configuration.GetValue("AiStrategy:ProjectCombatEffects",true),combatProjectionBenchmarkSupported=true,combatProjectionPreviousBenchmarkSupported=true,skipPlainTacticalFinishes=strategy.SkipsPlainTacticalFinishes,trainingReplaySupported=true,trainingReplayEnabled=configuration.GetValue<bool>("LocalAiTraining:SaveReplay"),modelPromotionGate=true,modelPromotionGames=AiModelStrengthGate.Games,strategyConfigured=strategy.IsConfigured,strategyModel=strategy.ModelName,strategySequenceValidation=strategy.ValidatesPlaySequences,providerUsage=strategy.ProviderUsage,providerStatus=strategy.ProviderStatus});
    }
    [HttpPost("strategy/retry")]
    public async Task<IActionResult> RetryStrategy(CancellationToken token)
    {
        if (await Actor(token) == null) return NotFound();
        if (training.Status.Running) return Conflict(new { error = "请先停止当前训练再重试模型" });
        strategy.RetryProvider();
        return Ok(strategy.ProviderStatus);
    }
    [HttpGet("strategy/history")]
    public async Task<IActionResult> StrategyHistory(CancellationToken token)
    {
        if (await Actor(token) == null) return NotFound();
        var history = strategy.ReadHistory();
        var cards = history.SelectMany(entry => entry.Lesson.Played.Concat((entry.Lesson.HandActions ?? []).Select(action => action.CardId))
                .Concat((entry.Lesson.HandActions ?? []).Select(action => action.SourceCardId).OfType<int>())
                .Concat((entry.Lesson.Choices ?? []).SelectMany(choice => (choice.Comparison?.LocalCardIds ?? []).Prepend(choice.CardId)))
                .Concat((entry.Lesson.AbilityActions ?? []).Select(action => action.CardId))
                .Concat((entry.Lesson.ProposedAbilities ?? []).Select(item => item.Advice.CardId))
                .Concat((entry.Lesson.ProposedPlays ?? []).SelectMany(item=>(item.Line ?? [item.Advice]).Select(step=>step.CardId)))
                .Concat((entry.Lesson.PlanValidations ?? []).SelectMany(item=>item.ProposedLine.Select(step=>step.CardId)))
                .Concat((entry.Lesson.Attacks ?? []).SelectMany(attack => new[] { attack.AttackerCardId, attack.TargetCardId }))
                .Concat((entry.Lesson.ActionComparisons ?? []).SelectMany(item =>
                    new[] { item.Local.CardId, item.Selected.CardId, item.Local.TargetCardId, item.Selected.TargetCardId })
                    .OfType<int>())
                .Concat((entry.Lesson.LeaderDamage ?? []).Select(damage => damage.CardId))
                .Concat((entry.Lesson.CompletedActions ?? []).SelectMany(AiActionOutcomes.CardIds))).Distinct()
            .ToDictionary(id => id, id => GameDataProvider.Instance.GetCardDefinition(id)?.Name);
        var reflections = history.Select(entry => entry.DeckKey).Distinct()
            .Select(key => (Key: key, Reflection: strategy.ReadReflection(key)))
            .Where(item => item.Reflection != null).ToDictionary(item => item.Key, item => item.Reflection);
        return Ok(new { history, cards, reflections });
    }

    public sealed record ReflectionRequest(string DeckKey);

    [HttpPost("strategy/reflect")]
    public async Task<IActionResult> Reflect(ReflectionRequest request, CancellationToken token)
    {
        if (await Actor(token) == null) return NotFound();
        if (string.IsNullOrEmpty(request.DeckKey) || !strategy.ReadHistory().Any(entry => entry.DeckKey == request.DeckKey))
            return BadRequest(new { error = "未找到这副牌组的有效复盘" });
        return Ok(new { queued = strategy.RequestReflection(request.DeckKey) });
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(LocalTrainingRequest request,CancellationToken token) {
        var actor=await Actor(token);if(actor==null)return NotFound();
        if(model.Current==null)return BadRequest(new{error="尚未载入迁移模型"});
        var minimumGames=request.MinimumGames;
        if(request.PlayerDeckId==Guid.Empty||request.OpponentDeckId==Guid.Empty||!request.HasValidRunSize)return BadRequest(new{error=$"请选择两副现有卡组，局数为{minimumGames}至1000局，并行路数为1至16路"});
        if (!request.HasValidExperienceComparison)
            return BadRequest(new { error = "历史经验对照需开启大模型评测并单独运行，局数为8的倍数，并行最多2路" });
        if (!request.HasValidWeaverComparison)
            return BadRequest(new { error = "织法进化顺序对照须独立运行，局数为4的倍数，并行最多2路" });
        if(request.CompareRecoveryPolicy&&(request.CompareLegacy||request.CompareStrategy||request.CompareLocalBaseline||request.CompareModelInputs
            ||request.CompareCombatThreat||request.CompareValueSearch||request.CompareGeneratedTokens||request.CompareEternalEngine
            ||request.CompareSummonSources||request.CompareCombatProjection||request.ComparePreviousCombatProjection||request.Continuous||request.Games%4!=0||request.Parallel>2))
            return BadRequest(new{error="手牌与回血对照须独立运行、非连续模式；局数为4的倍数，并行最多2路"});
        if(request.ComparePreviousCombatProjection&&!request.CompareCombatProjection)return BadRequest(new{error="旧版有限预测对照必须启用攻击前预测对照"});
        if(request.CompareCombatProjection&&(request.CompareLegacy||request.CompareStrategy||request.CompareLocalBaseline||request.CompareModelInputs||request.CompareCombatThreat||request.CompareValueSearch||request.CompareGeneratedTokens||request.CompareEternalEngine||request.CompareSummonSources||request.Continuous||request.Games%4!=0||request.Parallel>2))
            return BadRequest(new{error="攻击前预测对照需独立运行，不连续训练；局数为4的倍数，并行最多2路"});
        if(request.StrategyTurnBudgetMs is int budget && (!request.CompareStrategy || request.StrategyChoicesOnly || budget is < 500 or > 10000))
            return BadRequest(new{error="回合规划时限仅用于全回合模型评测，范围为 500 到 10000 毫秒"});
        if(request.CompareSummonSources && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.CompareModelInputs || request.CompareCombatThreat || request.CompareValueSearch || request.CompareGeneratedTokens || request.CompareEternalEngine || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="召唤斩杀出牌对照需独立运行，局数为4的倍数，并行不超过2路"});
        if(request.CompareEternalEngine && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.CompareModelInputs || request.CompareCombatThreat || request.CompareValueSearch || request.CompareGeneratedTokens || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="芮丝资源核心对照需独立运行，局数为4的倍数，并行不超过2路"});
        if(request.CompareGeneratedTokens && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.CompareModelInputs || request.CompareCombatThreat || request.CompareValueSearch || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="生成疾驰路线对照需独立运行，每组局数为4的倍数，并行不超过2路"});
        if(request.CompareValueSearch && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.CompareModelInputs || request.CompareCombatThreat || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="本地模型搜索对照不能混用其他评测，局数须为4的倍数，并行最多2路"});
        if(request.CompareCombatThreat && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.CompareModelInputs || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="交战威胁对照不能混用其他评测，局数须为4的倍数，并行最多2路"});
        if(request.CompareModelInputs && (request.CompareStrategy || request.CompareLegacy || request.CompareLocalBaseline || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="本地模型输入对照不能混用其他评测，局数须为4的倍数，并行最多2路"});
        if(request.CompareLocalBaseline && (request.CompareStrategy || request.CompareLegacy || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="本地基线评测不能同时选择其他评测，局数须为4的倍数，并行最多2路"});
        if(request.CompareLegacy && request.Games%4!=0)return BadRequest(new{error="对照评测局数必须是4的倍数"});
        if(request.CompareStrategy && (!strategy.IsConfigured || request.CompareLegacy || request.Games%4!=0 || request.Parallel>2))
            return BadRequest(new{error="LLM evaluation requires a configured provider, a multiple of 4 games, and at most 2 workers. Disable compareLegacy."});
        if (request.CompareStrategy && !strategy.CanRequest)
            return Conflict(new { error = strategy.ProviderStatus.Message });
        return training.Start(actor.Value,request)?Ok(training.Status):Conflict(new{error="训练正在运行"});
    }
    [HttpPost("stop")]
    public async Task<IActionResult> Stop(CancellationToken token){if(await Actor(token)==null)return NotFound();training.Stop();return Ok(training.Status);}
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken token) {
        if(await Actor(token)==null)return NotFound();if(!System.IO.File.Exists(model.ModelPath))return NotFound();
        return PhysicalFile(model.ModelPath,"application/json","svnebula-ai-model.json");
    }
}
