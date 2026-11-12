using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Services;

internal static class AiProviderUsageTests
{
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static void Run()
    {
        using var provider=new Fake();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="offline-fake"}).Build();
        using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
        var ask=typeof(AiStrategyPlanner).GetMethod("AskAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(AiStrategyPlanner.DeckGuide));
        for(var i=0;i<6;i++){
            ((Task<AiStrategyPlanner.DeckGuide?>)ask.Invoke(planner,["offline usage fixture",new{probe=i},CancellationToken.None])!).GetAwaiter().GetResult();
            var usage=JsonSerializer.SerializeToElement(planner.ProviderUsage,Json);
            if(i==0)Check(usage.GetProperty("cacheReportedPromptTokens").GetInt64()==100&&usage.GetProperty("cachedTokens").GetInt64()==50,"reported cache not recorded");
            if(i==1)Check(usage.GetProperty("promptTokens").GetInt64()==300&&usage.GetProperty("cacheReportedPromptTokens").GetInt64()==100&&usage.GetProperty("cacheUnknownResponses").GetInt64()==1,"missing cache treated as zero miss");
            if(i==2)Check(usage.GetProperty("cacheReportedPromptTokens").GetInt64()==400&&usage.GetProperty("cacheReportedResponses").GetInt64()==2,"real zero cache hit misclassified as missing");
        }
        var final=JsonSerializer.SerializeToElement(planner.ProviderUsage,Json);
        Check(final.GetProperty("responses").GetInt64()==6&&final.GetProperty("promptTokens").GetInt64()==620&&final.GetProperty("cachedTokens").GetInt64()==50
            &&final.GetProperty("cacheReportedPromptTokens").GetInt64()==400&&final.GetProperty("cacheUnknownResponses").GetInt64()==4
            &&final.GetProperty("usageMissingResponses").GetInt64()==1&&final.GetProperty("promptUnreportedResponses").GetInt64()==2
            &&final.GetProperty("invalidCacheResponses").GetInt64()==2,"partial/invalid cache coverage totals incorrect");
        Check(provider.Calls==6,"tracking created extra model requests");
        Console.WriteLine("PASS actual planner usage parsing: measured hit/zero, missing cache/usage, invalid cache count and correct measured denominator; fake responses only, real paid calls=0");
    }
    sealed class Fake:HttpMessageHandler,IHttpClientFactory
    {
        internal int Calls;public HttpClient CreateClient(string name)=>new(this,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var i=Calls++;var envelope=new Dictionary<string,object?>{["choices"]=new[]{new{message=new{content="{\"gamePlan\":\"offline\",\"cards\":[]}"}}}};
            if(i!=3)envelope["usage"]=i switch{0=>new{prompt_tokens=100,prompt_cache_hit_tokens=50,completion_tokens=5},1=>(object)new{prompt_tokens=200,completion_tokens=10},2=>new{prompt_tokens=300,prompt_cache_hit_tokens=0,completion_tokens=20},5=>new{prompt_tokens="unknown",prompt_cache_hit_tokens=0},_=>new{prompt_tokens=20,prompt_cache_hit_tokens=30}};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(envelope,Json))});}
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="usage test";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
