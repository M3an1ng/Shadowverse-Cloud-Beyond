// File: Server/Controllers/MatchController.cs
//
// 相关文档:
// - 多人联机方案A: 内存房间 + JoinCode/Token
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ILogger<MatchController> _logger;

        public MatchController(IMatchService matchService, ILogger<MatchController> logger)
        {
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status201Created)]
        public ActionResult<MatchResponse> Create([FromBody] CreateMatchRequest request)
        {
            var ttl = request?.TtlMinutes.HasValue == true ? TimeSpan.FromMinutes(request.TtlMinutes.Value) : (TimeSpan?)null;
            var result = _matchService.Create(request?.HostPlayerId, request?.UseAiFallback ?? false, ttl);
            if (!result.Success || result.Room == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, result.Message ?? "创建房间失败");
            }

            var response = ToResponse(result.Room, includeTokens: true);
            return CreatedAtAction(nameof(Get), new { id = response.GameId }, response);
        }

        [HttpPost("{id}/join")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<MatchResponse> Join(string id, [FromBody] JoinMatchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.JoinCode))
            {
                return BadRequest("JoinCode 不能为空");
            }

            var result = _matchService.TryJoin(id, request.JoinCode);
            return MapResult(result, includeTokens: true);
        }

        [HttpPost("{id}/ready")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<MatchResponse> Ready(string id, [FromBody] SeatRequest request)
        {
            var validate = ValidateSeatRequest(request);
            if (validate != null) return validate;

            var auth = _matchService.ValidateConnection(id, request.PlayerId, request.Token);
            if (!auth.Success) return MapResult(auth, includeTokens: false);

            var result = _matchService.MarkReady(id, request.PlayerId);
            return MapResult(result, includeTokens: false);
        }

        [HttpPost("{id}/start")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<MatchResponse> Start(string id, [FromBody] SeatRequest request)
        {
            var validate = ValidateSeatRequest(request);
            if (validate != null) return validate;

            var auth = _matchService.ValidateConnection(id, request.PlayerId, request.Token);
            if (!auth.Success) return MapResult(auth, includeTokens: false);

            var result = _matchService.Start(id, request.PlayerId);
            return MapResult(result, includeTokens: false);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<MatchResponse> Get(string id)
        {
            var result = _matchService.Get(id);
            return MapResult(result, includeTokens: true);
        }

        private ActionResult<MatchResponse>? ValidateSeatRequest(SeatRequest request)
        {
            if (request == null)
            {
                return BadRequest("请求体不能为空");
            }

            if (request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("playerId 和 token 必填");
            }

            return null;
        }

        private ActionResult<MatchResponse> MapResult(MatchOperationResult result, bool includeTokens)
        {
            if (result.Success && result.Room != null)
            {
                return Ok(ToResponse(result.Room, includeTokens));
            }

            return result.Error switch
            {
                MatchOperationError.NotFound => NotFound(result.Message ?? "房间不存在"),
                MatchOperationError.InvalidJoinCode => BadRequest(result.Message ?? "JoinCode 不正确"),
                MatchOperationError.AlreadyFull => Conflict(result.Message ?? "房间已满"),
                MatchOperationError.AlreadyStarted => Conflict(result.Message ?? "房间已开始"),
                MatchOperationError.AlreadyClosed => Conflict(result.Message ?? "房间已关闭"),
                MatchOperationError.Expired => Conflict(result.Message ?? "房间已过期"),
                MatchOperationError.NotParticipant => StatusCode(StatusCodes.Status403Forbidden, result.Message ?? "玩家不在房间内"),
                MatchOperationError.NotReady => Conflict(result.Message ?? "未满足开始条件"),
                MatchOperationError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Message ?? "无权限"),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result.Message ?? "未知错误")
            };
        }

        private static MatchResponse ToResponse(MatchRoom room, bool includeTokens)
        {
            return new MatchResponse
            {
                GameId = room.GameId,
                HostPlayerId = room.HostPlayerId,
                GuestPlayerId = room.GuestPlayerId,
                JoinCode = room.JoinCode,
                HostToken = includeTokens ? room.HostToken : null,
                GuestToken = includeTokens ? room.GuestToken : null,
                Status = room.Status.ToString(),
                HostReady = room.HostReady,
                GuestReady = room.GuestReady,
                UseAiFallback = room.UseAiFallback
            };
        }

        // --- Request/Response DTOs ---
        public class CreateMatchRequest
        {
            public Guid? HostPlayerId { get; set; }
            public bool UseAiFallback { get; set; }
            public int? TtlMinutes { get; set; }
        }

        public class JoinMatchRequest
        {
            public string JoinCode { get; set; } = string.Empty;
        }

        public class SeatRequest
        {
            public Guid PlayerId { get; set; }
            public string Token { get; set; } = string.Empty;
        }

        public class MatchResponse
        {
            public string GameId { get; set; } = string.Empty;
            public Guid HostPlayerId { get; set; }
            public Guid? GuestPlayerId { get; set; }
            public string JoinCode { get; set; } = string.Empty;
            public string? HostToken { get; set; }
            public string? GuestToken { get; set; }
            public string Status { get; set; } = string.Empty;
            public bool HostReady { get; set; }
            public bool GuestReady { get; set; }
            public bool UseAiFallback { get; set; }
        }
    }
}
