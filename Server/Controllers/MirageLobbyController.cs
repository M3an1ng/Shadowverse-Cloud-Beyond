using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/mirage/rooms")]
    public sealed class MirageLobbyController : ControllerBase
    {
        private readonly IMirageLobbyService _mirageLobby;

        public MirageLobbyController(IMirageLobbyService mirageLobby)
        {
            _mirageLobby = mirageLobby ?? throw new ArgumentNullException(nameof(mirageLobby));
        }

        [HttpGet]
        [ProducesResponseType(typeof(MirageLobbyRoomListResponseDto), StatusCodes.Status200OK)]
        public ActionResult<MirageLobbyRoomListResponseDto> List()
        {
            var items = _mirageLobby.ListRooms().ToList();
            return Ok(new MirageLobbyRoomListResponseDto { Items = items });
        }

        [HttpGet("resolve-code/{code}")]
        [ProducesResponseType(typeof(MirageResolveCodeResponseDto), StatusCodes.Status200OK)]
        public ActionResult<MirageResolveCodeResponseDto> ResolveCode(string code)
        {
            return Ok(_mirageLobby.ResolveJoinCode(code));
        }

        [HttpPost]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Create(
            [FromBody] CreateMirageRoomRequest? request,
            CancellationToken cancellationToken)
        {
            var result = await _mirageLobby.CreateRoomAsync(
                request?.HostPlayerId,
                request?.DisplayName,
                request?.AvatarUrl,
                request?.IsPublic ?? true,
                request?.RecruitTimeoutSeconds,
                request?.QuickStartSoloAi ?? false,
                cancellationToken);
            if (!result.Success || result.Room == null)
            {
                return MapFailure(result);
            }

            return CreatedAtAction(nameof(Get), new { id = result.Room.RoomId }, result.Room);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<MirageLobbyRoomDto> Get(
            string id,
            [FromQuery] Guid? playerId = null,
            [FromQuery] string? token = null)
        {
            var effectivePlayerId = playerId;
            if (!effectivePlayerId.HasValue &&
                Guid.TryParse(Request.Headers["X-Room-PlayerId"].FirstOrDefault(), out var headerPid))
            {
                effectivePlayerId = headerPid;
            }

            var effectiveToken = !string.IsNullOrWhiteSpace(token)
                ? token
                : Request.Headers["X-Room-Token"].FirstOrDefault();

            var result = _mirageLobby.GetRoom(id, effectivePlayerId, effectiveToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpGet("{id}/standings")]
        [ProducesResponseType(typeof(MirageStandingsSnapshotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<MirageStandingsSnapshotDto> Standings(string id)
        {
            var standings = _mirageLobby.GetStandingsSnapshot(id);
            if (standings == null)
            {
                return NotFound("幻境房间战况不存在或尚未开赛。");
            }

            return Ok(standings);
        }

        [HttpPost("{id}/join")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Join(
            string id,
            [FromBody] JoinMirageRoomRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest("请求体不能为空。");
            }

            var result = await _mirageLobby.JoinRoomAsync(
                id,
                request.JoinCode,
                request.PlayerId,
                request.Token,
                request.DisplayName,
                request.AvatarUrl,
                allowPublicWithoutCode: true,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpPost("join-by-code")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> JoinByCode(
            [FromBody] JoinMirageRoomRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.JoinCode))
            {
                return BadRequest("邀请码不能为空。");
            }

            var result = await _mirageLobby.JoinByCodeAsync(
                request.JoinCode,
                request.PlayerId,
                request.Token,
                request.DisplayName,
                request.AvatarUrl,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpPost("{id}/ready")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Ready(
            string id,
            [FromBody] MirageSeatActionRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("playerId 与 token 必填。");
            }

            var result = await _mirageLobby.SetReadyAsync(
                id,
                request.PlayerId,
                request.Token,
                request.Ready ?? true,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpPost("{id}/kick")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Kick(
            string id,
            [FromBody] KickMirageSeatRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null
                || request.PlayerId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Token)
                || request.TargetPlayerId == Guid.Empty)
            {
                return BadRequest("playerId、token 与 targetPlayerId 必填。");
            }

            var result = await _mirageLobby.KickSeatAsync(
                id,
                request.PlayerId,
                request.Token,
                request.TargetPlayerId,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpPost("{id}/leave")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Leave(
            string id,
            [FromBody] MirageSeatActionRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("playerId 与 token 必填。");
            }

            var result = await _mirageLobby.LeaveRoomAsync(
                id,
                request.PlayerId,
                request.Token,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        [HttpPost("{id}/start")]
        [ProducesResponseType(typeof(MirageLobbyRoomDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MirageLobbyRoomDto>> Start(
            string id,
            [FromBody] MirageSeatActionRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("playerId 与 token 必填。");
            }

            var result = await _mirageLobby.StartRoomAsync(
                id,
                request.PlayerId,
                request.Token,
                cancellationToken);
            return result.Success && result.Room != null ? Ok(result.Room) : MapFailure(result);
        }

        private ActionResult<MirageLobbyRoomDto> MapFailure(MirageLobbyOperationResult result)
        {
            var msg = result.Message ?? "操作失败。";
            return result.Error switch
            {
                MirageLobbyError.NotFound => NotFound(new { message = msg }),
                MirageLobbyError.InvalidJoinCode => BadRequest(new { message = msg }),
                MirageLobbyError.InvalidOperation => BadRequest(new { message = msg }),
                MirageLobbyError.AlreadyFull => Conflict(new { message = msg }),
                MirageLobbyError.AlreadyStarted => Conflict(new { message = msg }),
                MirageLobbyError.AlreadyClosed => Conflict(new { message = msg, room = result.Room }),
                MirageLobbyError.Expired => Conflict(new { message = msg }),
                MirageLobbyError.NotReady => Conflict(new { message = msg }),
                MirageLobbyError.NotParticipant => StatusCode(StatusCodes.Status403Forbidden, new { message = msg }),
                MirageLobbyError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = msg }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = msg })
            };
        }

        public sealed class CreateMirageRoomRequest
        {
            public Guid? HostPlayerId { get; set; }
            public string? DisplayName { get; set; }
            public string? AvatarUrl { get; set; }
            public bool? IsPublic { get; set; }
            public int? RecruitTimeoutSeconds { get; set; }
            public bool? QuickStartSoloAi { get; set; }
        }

        public sealed class JoinMirageRoomRequest
        {
            public string? JoinCode { get; set; }
            public Guid? PlayerId { get; set; }
            public string? Token { get; set; }
            public string? DisplayName { get; set; }
            public string? AvatarUrl { get; set; }
        }

        public class MirageSeatActionRequest
        {
            public Guid PlayerId { get; set; }
            public string Token { get; set; } = string.Empty;
            public bool? Ready { get; set; }
        }

        public sealed class KickMirageSeatRequest : MirageSeatActionRequest
        {
            public Guid TargetPlayerId { get; set; }
        }
    }
}
