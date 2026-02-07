// File: Server/Controllers/DevToolsConditionTraceController.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Logging;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/devtools/condition-trace")]
    public class DevToolsConditionTraceController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConditionTraceSettings _traceSettings;
        private readonly ILogger<DevToolsConditionTraceController> _logger;

        public DevToolsConditionTraceController(
            ApplicationDbContext db,
            IConditionTraceSettings traceSettings,
            ILogger<DevToolsConditionTraceController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _traceSettings = traceSettings ?? throw new ArgumentNullException(nameof(traceSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [ProducesResponseType(typeof(ConditionTraceStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ConditionTraceStateResponse>> Get([FromQuery] Guid userId, CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("userId 必填。");
            }

            var userExists = await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
            if (!userExists)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "无权限访问 Devtools 条件追踪设置。");
            }

            return Ok(new ConditionTraceStateResponse
            {
                ConditionEnabled = _traceSettings.IsConditionEnabled(userId),
                TargetEnabled = _traceSettings.IsTargetEnabled(userId),
                EffectEnabled = _traceSettings.IsEffectEnabled(userId)
            });
        }

        [HttpPost]
        [ProducesResponseType(typeof(ConditionTraceStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ConditionTraceStateResponse>> Set([FromBody] ConditionTraceStateRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest("请求体不能为空。");
            }

            if (request.UserId == Guid.Empty)
            {
                return BadRequest("userId 必填。");
            }

            var userExists = await _db.Users.AnyAsync(user => user.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "无权限访问 Devtools 条件追踪设置。");
            }

            _traceSettings.SetConditionEnabled(request.UserId, request.ConditionEnabled);
            _traceSettings.SetTargetEnabled(request.UserId, request.TargetEnabled);
            _traceSettings.SetEffectEnabled(request.UserId, request.EffectEnabled);
            _logger.LogInformation(
                "Condition trace logging updated. UserId={UserId}, Condition={Condition}, Target={Target}, Effect={Effect}",
                request.UserId,
                request.ConditionEnabled,
                request.TargetEnabled,
                request.EffectEnabled);

            return Ok(new ConditionTraceStateResponse
            {
                ConditionEnabled = request.ConditionEnabled,
                TargetEnabled = request.TargetEnabled,
                EffectEnabled = request.EffectEnabled
            });
        }

        public sealed class ConditionTraceStateRequest
        {
            public Guid UserId { get; set; }
            public bool ConditionEnabled { get; set; }
            public bool TargetEnabled { get; set; }
            public bool EffectEnabled { get; set; }
        }

        public sealed class ConditionTraceStateResponse
        {
            public bool ConditionEnabled { get; set; }
            public bool TargetEnabled { get; set; }
            public bool EffectEnabled { get; set; }
        }
    }
}
