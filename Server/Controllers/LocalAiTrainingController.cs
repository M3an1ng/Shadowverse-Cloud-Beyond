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
public sealed class LocalAiTrainingController(LocalAiTrainingService training,AiLearningModel model,ApplicationDbContext db,IWebHostEnvironment environment,IConfiguration configuration) : ControllerBase
{
    async Task<Guid?> Actor(CancellationToken token) {
        if(!LocalAiTrainingAccess.Allowed(HttpContext,environment,configuration))return null;
        if(!Guid.TryParse(Request.Headers["X-Editor-UserId"].FirstOrDefault(),out var id))return null;
        return await db.Users.AnyAsync(u=>u.Id==id&&u.IsActive&&u.Role==UserRole.Admin,token)?id:null;
    }
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken token)=>Ok(new{enabled=await Actor(token)!=null});
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken token) => await Actor(token)==null?NotFound():Ok(new{training=training.Status,modelVersion=model.Current?.Version,models=model.Current?.Models.Count??0,specialists=model.Current?.Specialists.Count??0});
    [HttpPost("start")]
    public async Task<IActionResult> Start(LocalTrainingRequest request,CancellationToken token) {
        var actor=await Actor(token);if(actor==null)return NotFound();
        if(model.Current==null)return BadRequest(new{error="尚未载入迁移模型"});
        if(request.PlayerDeckId==Guid.Empty||request.OpponentDeckId==Guid.Empty||request.Games<10||request.Games>1000||request.Parallel<1||request.Parallel>16)return BadRequest(new{error="请选择两副现有卡组，训练局数为10至1000局，并行路数为1至16路"});
        if(request.CompareLegacy && request.Games%4!=0)return BadRequest(new{error="对照评测局数必须是4的倍数"});
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
