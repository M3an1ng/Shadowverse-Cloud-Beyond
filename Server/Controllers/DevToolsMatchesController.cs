// File: Server/Controllers/DevToolsMatchesController.cs
//
// Devtools-only match start API for configurable test matches.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/devtools/matches")]
    public class DevToolsMatchesController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ApplicationDbContext _db;
        private readonly IDeckService _deckService;
        private readonly ILogger<DevToolsMatchesController> _logger;

        public DevToolsMatchesController(
            IMatchService matchService,
            ApplicationDbContext db,
            IDeckService deckService,
            ILogger<DevToolsMatchesController> logger)
        {
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _deckService = deckService ?? throw new ArgumentNullException(nameof(deckService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("start")]
        [ProducesResponseType(typeof(DevToolsMatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<DevToolsMatchResponse>> Start([FromBody] DevToolsMatchRequest request, CancellationToken cancellationToken)
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
                return StatusCode(StatusCodes.Status403Forbidden, "无权限访问 Devtools 对战。");
            }

            var mode = NormalizeMode(request.Mode);
            if (mode == null)
            {
                return BadRequest("mode 必须为 ai 或 dual。");
            }

            if (request.PlayerDeckId == Guid.Empty || request.OpponentDeckId == Guid.Empty)
            {
                return BadRequest("playerDeckId 与 opponentDeckId 必填。");
            }

            if (!TryBuildConfig(request.Config, out var safeConfig, out var configError))
            {
                return BadRequest(configError ?? "配置参数无效。");
            }

            var devMatchConfig = safeConfig ?? new DevMatchConfig();
            devMatchConfig.IgnoreDeckFormatConsistency = true;
            devMatchConfig.AllowIncompleteDecks = true;
            var createResult = _matchService.Create(request.UserId, useAiFallback: mode == "ai", devMatchConfig: devMatchConfig);
            if (!createResult.Success || createResult.Room == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, createResult.Message ?? "创建房间失败。");
            }

            var room = createResult.Room;

            Guid? guestPlayerId = null;
            string? guestToken = null;

            if (mode == "dual")
            {
                var joinResult = _matchService.TryJoin(room.RoomId, room.JoinCode, null);
                if (!joinResult.Success || joinResult.Room == null || !joinResult.Room.GuestPlayerId.HasValue || string.IsNullOrWhiteSpace(joinResult.Room.GuestToken))
                {
                    _matchService.Close(room.RoomId, "Devtools join failed.");
                    return StatusCode(StatusCodes.Status500InternalServerError, joinResult.Message ?? "加入房间失败。");
                }

                guestPlayerId = joinResult.Room.GuestPlayerId.Value;
                guestToken = joinResult.Room.GuestToken;
            }

            var owner = new DeckOwner(request.UserId, null);
            var playerDeckDetail = await _deckService.GetAsync(owner, request.PlayerDeckId, cancellationToken);
            if (playerDeckDetail == null)
            {
                _matchService.Close(room.RoomId, "Devtools host deck invalid.");
                return BadRequest("玩家卡组不存在或无效。");
            }

            var opponentDeckDetail = await _deckService.GetAsync(owner, request.OpponentDeckId, cancellationToken);
            if (opponentDeckDetail == null)
            {
                _matchService.Close(room.RoomId, "Devtools opponent deck invalid.");
                return BadRequest("对手卡组不存在或无效。");
            }

            var hostSelection = new MatchDeckSelection
            {
                UserId = request.UserId,
                DeckId = request.PlayerDeckId,
                DeckClass = playerDeckDetail.Class,
                SecondaryClass = playerDeckDetail.SecondaryClass,
                Environment = playerDeckDetail.Environment,
                Rule = playerDeckDetail.Rule
            };

            if (mode == "ai")
            {
                var aiSelection = new MatchDeckSelection
                {
                    UserId = request.UserId,
                    DeckId = request.OpponentDeckId,
                    DeckClass = opponentDeckDetail.Class,
                    SecondaryClass = opponentDeckDetail.SecondaryClass,
                    Environment = opponentDeckDetail.Environment,
                    Rule = opponentDeckDetail.Rule
                };

                var selectResult = _matchService.SubmitDeckSelection(room.RoomId, room.HostPlayerId, hostSelection, aiSelection);
                if (!selectResult.Success)
                {
                    _matchService.Close(room.RoomId, "Devtools deck selection failed.");
                    return StatusCode(StatusCodes.Status500InternalServerError, selectResult.Message ?? "卡组选择失败。");
                }
            }
            else if (guestPlayerId.HasValue)
            {
                var guestSelection = new MatchDeckSelection
                {
                    UserId = request.UserId,
                    DeckId = request.OpponentDeckId,
                    DeckClass = opponentDeckDetail.Class,
                    SecondaryClass = opponentDeckDetail.SecondaryClass,
                    Environment = opponentDeckDetail.Environment,
                    Rule = opponentDeckDetail.Rule
                };

                var hostSelect = _matchService.SubmitDeckSelection(room.RoomId, room.HostPlayerId, hostSelection);
                if (!hostSelect.Success)
                {
                    _matchService.Close(room.RoomId, "Devtools host deck selection failed.");
                    return StatusCode(StatusCodes.Status500InternalServerError, hostSelect.Message ?? "玩家卡组选择失败。");
                }

                var guestSelect = _matchService.SubmitDeckSelection(room.RoomId, guestPlayerId.Value, guestSelection);
                if (!guestSelect.Success)
                {
                    _matchService.Close(room.RoomId, "Devtools guest deck selection failed.");
                    return StatusCode(StatusCodes.Status500InternalServerError, guestSelect.Message ?? "对手卡组选择失败。");
                }
            }

            var hostReady = _matchService.MarkReady(room.RoomId, room.HostPlayerId);
            if (!hostReady.Success)
            {
                _matchService.Close(room.RoomId, "Devtools host ready failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, hostReady.Message ?? "准备失败。");
            }

            if (guestPlayerId.HasValue)
            {
                var guestReady = _matchService.MarkReady(room.RoomId, guestPlayerId.Value);
                if (!guestReady.Success)
                {
                    _matchService.Close(room.RoomId, "Devtools guest ready failed.");
                    return StatusCode(StatusCodes.Status500InternalServerError, guestReady.Message ?? "对手准备失败。");
                }
            }

            var startResult = _matchService.Start(room.RoomId, room.HostPlayerId);
            if (!startResult.Success || startResult.Room == null)
            {
                var failureMessage = startResult.Message ?? "房间启动失败。";
                _logger.LogWarning(
                    "Devtools match start failed. RoomId={RoomId}, Mode={Mode}, HostDeckId={HostDeckId}, OpponentDeckId={OpponentDeckId}, Reason={Reason}",
                    room.RoomId,
                    mode,
                    request.PlayerDeckId,
                    request.OpponentDeckId,
                    failureMessage);
                _matchService.Close(room.RoomId, $"Devtools match start failed: {failureMessage}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = failureMessage });
            }

            var startedRoom = startResult.Room;

            var response = new DevToolsMatchResponse
            {
                RoomId = startedRoom.RoomId,
                ActiveGameId = startedRoom.ActiveGameId,
                JoinCode = startedRoom.JoinCode,
                HostPlayerId = startedRoom.HostPlayerId,
                HostToken = startedRoom.HostToken,
                GuestPlayerId = guestPlayerId,
                GuestToken = guestToken,
                UseAiFallback = startedRoom.UseAiFallback,
                Mode = mode
            };

#pragma warning disable CS0618
            response.GameId = startedRoom.RoomId;
#pragma warning restore CS0618

            return Ok(response);
        }

        private static string? NormalizeMode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "ai";
            var normalized = raw.Trim().ToLowerInvariant();
            return normalized switch
            {
                "ai" => "ai",
                "dual" => "dual",
                _ => null
            };
        }

        internal static bool TryBuildConfig(DevMatchConfigRequest? request, out DevMatchConfig? config, out string? error)
        {
            config = null;
            error = null;
            if (request == null)
            {
                return true;
            }

            int? startingPp = request.StartingPlayPoints;
            int? maxPp = request.MaxPlayPoints;
            int? handSize = request.StartingHandSize;
            int? startingTurn = request.StartingTurn;
            int? startingHp = request.StartingHealth;
            int? ppBoostUses = request.PpBoostUses;

            if (startingPp.HasValue && (startingPp < 0 || startingPp > GameConstants.PlayPoints.MaxPlayPoints))
            {
                error = $"startingPlayPoints 必须在 0 到 {GameConstants.PlayPoints.MaxPlayPoints} 之间。";
                return false;
            }

            if (maxPp.HasValue && (maxPp < 0 || maxPp > GameConstants.PlayPoints.MaxPlayPoints))
            {
                error = $"maxPlayPoints 必须在 0 到 {GameConstants.PlayPoints.MaxPlayPoints} 之间。";
                return false;
            }

            if (startingPp.HasValue && maxPp.HasValue && startingPp > maxPp)
            {
                error = "startingPlayPoints 不能大于 maxPlayPoints。";
                return false;
            }

            if (handSize.HasValue && (handSize < 0 || handSize > GameConstants.Hand.MaxHandSize))
            {
                error = $"startingHandSize 必须在 0 到 {GameConstants.Hand.MaxHandSize} 之间。";
                return false;
            }

            if (startingTurn.HasValue && (startingTurn < 1 || startingTurn > GameConstants.Timing.MaxTurnsBeforeDraw))
            {
                error = $"startingTurn 必须在 1 到 {GameConstants.Timing.MaxTurnsBeforeDraw} 之间。";
                return false;
            }

            if (startingHp.HasValue && (startingHp < 1 || startingHp > GameConstants.Health.PlayerMaxHealth))
            {
                error = $"startingHealth 必须在 1 到 {GameConstants.Health.PlayerMaxHealth} 之间。";
                return false;
            }

            if (ppBoostUses.HasValue && (ppBoostUses < 0 || ppBoostUses > 5))
            {
                error = "ppBoostUses 必须在 0 到 5 之间。";
                return false;
            }

            config = new DevMatchConfig
            {
                StartingPlayPoints = startingPp,
                MaxPlayPoints = maxPp,
                StartingHandSize = handSize,
                StartingTurn = startingTurn,
                StartingHealth = startingHp,
                PpBoostUses = ppBoostUses,
                Seed = request.Seed
            };
            return true;
        }

        public class DevToolsMatchRequest
        {
            public Guid UserId { get; set; }
            public string? Mode { get; set; }
            public Guid PlayerDeckId { get; set; }
            public Guid OpponentDeckId { get; set; }
            public DevMatchConfigRequest? Config { get; set; }
        }

        public class DevMatchConfigRequest
        {
            public int? StartingPlayPoints { get; set; }
            public int? MaxPlayPoints { get; set; }
            public int? StartingHandSize { get; set; }
            public int? StartingTurn { get; set; }
            public int? StartingHealth { get; set; }
            public int? PpBoostUses { get; set; }
            public int? Seed { get; set; }
        }

        public class DevToolsMatchResponse
        {
            public string RoomId { get; set; } = string.Empty;
            public string? ActiveGameId { get; set; }
            [Obsolete("Use RoomId instead.")]
            public string GameId { get; set; } = string.Empty;
            public string JoinCode { get; set; } = string.Empty;
            public Guid HostPlayerId { get; set; }
            public string HostToken { get; set; } = string.Empty;
            public Guid? GuestPlayerId { get; set; }
            public string? GuestToken { get; set; }
            public bool UseAiFallback { get; set; }
            public string Mode { get; set; } = "ai";
        }
    }
}
