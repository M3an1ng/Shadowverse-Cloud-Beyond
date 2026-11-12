using System.Text.Json;

namespace ShadowverseBeyond.Server.Services;

// Report provider measurements only. Missing cache telemetry is not a measured cache miss.
internal sealed class AiProviderUsageTracker
{
    private readonly object gate=new();
    private long responses,promptTokens,cachedTokens,completionTokens,usageMissingResponses,promptUnreportedResponses,
        cacheReportedResponses,cacheUnknownResponses,cacheReportedPromptTokens,invalidCacheResponses;
    private static long? Count(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object
        &&value.TryGetProperty(key,out var item)&&item.ValueKind==JsonValueKind.Number&&item.TryGetInt64(out var count)&&count>=0?count:null;
    private static long Add(long a,long b)=>a>long.MaxValue-b?long.MaxValue:a+b;
    internal void Observe(JsonElement envelope)
    {
        lock(gate){
            responses=Add(responses,1);
            if(envelope.ValueKind!=JsonValueKind.Object||!envelope.TryGetProperty("usage",out var usage)||usage.ValueKind!=JsonValueKind.Object){
                usageMissingResponses=Add(usageMissingResponses,1);promptUnreportedResponses=Add(promptUnreportedResponses,1);cacheUnknownResponses=Add(cacheUnknownResponses,1);return;
            }
            var prompt=Count(usage,"prompt_tokens");var hit=Count(usage,"prompt_cache_hit_tokens");var completion=Count(usage,"completion_tokens");
            if(prompt.HasValue)promptTokens=Add(promptTokens,prompt.Value);else promptUnreportedResponses=Add(promptUnreportedResponses,1);
            if(completion.HasValue)completionTokens=Add(completionTokens,completion.Value);
            if(prompt.HasValue&&hit.HasValue&&hit.Value<=prompt.Value){
                cacheReportedResponses=Add(cacheReportedResponses,1);cacheReportedPromptTokens=Add(cacheReportedPromptTokens,prompt.Value);cachedTokens=Add(cachedTokens,hit.Value);
            }else{
                cacheUnknownResponses=Add(cacheUnknownResponses,1);
                if(usage.TryGetProperty("prompt_cache_hit_tokens",out _))invalidCacheResponses=Add(invalidCacheResponses,1);
            }
        }
    }
    internal object Snapshot()
    {
        lock(gate)return new{responses,promptTokens,cachedTokens,completionTokens,usageMissingResponses,promptUnreportedResponses,
            cacheReportedResponses,cacheUnknownResponses,cacheReportedPromptTokens,invalidCacheResponses,
            scope="Successful HTTP JSON responses, not accepted strategy count. Token totals include valid reported fields only. Cache ratio uses cacheReportedPromptTokens; missing/invalid cache fields remain unknown, not zero misses. Per-service lifetime, not billing/cost or a guarantee of future prefix reuse."};
    }
}
