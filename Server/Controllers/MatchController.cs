// File: Server/Controllers/MatchController.cs
//
// 相关文档:
// - 多人联机方案A: 内存房间 + JoinCode/Token
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ILogger<MatchController> _logger;
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly IGameService _gameService;
        private readonly IDeckService _deckService;
        private readonly IDeckValidationService _deckValidation;
        private readonly IDeckCodeService _deckCodeService;

        public MatchController(
            IMatchService matchService,
            ILogger<MatchController> logger,
            IHubContext<GameHub, IGameClient> hubContext,
            IGameService gameService,
            IDeckService deckService,
            IDeckValidationService deckValidation,
            IDeckCodeService deckCodeService)
        {
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _deckService = deckService ?? throw new ArgumentNullException(nameof(deckService));
            _deckValidation = deckValidation ?? throw new ArgumentNullException(nameof(deckValidation));
            _deckCodeService = deckCodeService ?? throw new ArgumentNullException(nameof(deckCodeService));
        }

        private enum ResponseTokenScope
        {
            None = 0,
            HostOnly = 1,
            GuestOnly = 2,
            AuthenticatedParticipant = 3
        }

        [HttpPost]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<MatchResponse> Create([FromBody] CreateMatchRequest request)
        {
            var mode = GameMode.Standard;
            if (!string.IsNullOrWhiteSpace(request?.Mode) &&
                !Enum.TryParse(request.Mode.Trim(), ignoreCase: true, out mode))
            {
                return BadRequest($"不支持的对战模式: {request.Mode}");
            }

            DevMatchConfig? devConfig = null;
            if (request?.TurnTimeoutSeconds.HasValue == true)
            {
                devConfig = new DevMatchConfig
                {
                    TurnTimeoutSeconds = Math.Clamp(request.TurnTimeoutSeconds.Value, 1, 600)
                };
            }

            var ttl = request?.TtlMinutes.HasValue == true ? TimeSpan.FromMinutes(request.TtlMinutes.Value) : (TimeSpan?)null;
            var isPublic = request?.IsPublic ?? true;
            var result = _matchService.Create(request?.HostPlayerId, request?.UseAiFallback ?? false, ttl, devConfig, isPublic, mode);
            if (!result.Success || result.Room == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, result.Message ?? "创建房间失败");
            }

            var response = ToResponse(result.Room, ResponseTokenScope.HostOnly);
            return CreatedAtAction(nameof(Get), new { id = response.RoomId }, response);
        }

        [HttpPost("{id}/join")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<MatchResponse> Join(string id, [FromBody] JoinMatchRequest request)
        {
            if (request == null)
            {
                return BadRequest("请求体不能为空");
            }

            var result = _matchService.TryJoin(id, request.JoinCode, request.PlayerId, request.Token, allowPublicWithoutCode: true);
            return MapResult(result, ResponseTokenScope.GuestOnly);
        }

        [HttpPost("join-by-code")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult<MatchResponse> JoinByCode([FromBody] JoinMatchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.JoinCode))
            {
                return BadRequest("JoinCode 不能为空");
            }

            var result = _matchService.TryJoinByCode(request.JoinCode, request.PlayerId, request.Token);
            return MapResult(result, ResponseTokenScope.GuestOnly);
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
            if (!auth.Success) return MapResult(auth, ResponseTokenScope.None);

            var result = _matchService.MarkReady(id, request.PlayerId, request.Ready ?? true, request.Token);
            return MapResult(result, ResponseTokenScope.None);
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
            if (!auth.Success) return MapResult(auth, ResponseTokenScope.None);

            var result = _matchService.Start(id, request.PlayerId, request.Token);
            return MapResult(result, ResponseTokenScope.None);
        }

        [HttpPost("{id}/deck")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MatchResponse>> SelectDeck(string id, [FromBody] DeckSelectionRequest request, CancellationToken cancellationToken)
        {
            var validate = ValidateDeckSelectionRequest(request);
            if (validate != null) return validate;

            var auth = _matchService.ValidateConnection(id, request.PlayerId, request.Token);
            if (!auth.Success) return MapResult(auth, ResponseTokenScope.None);

            if (auth.Room?.Mode == GameMode.Mirage)
            {
                return BadRequest("幻境模式无需选择构筑卡组。");
            }

            if (!TryBuildDeckOwner(request.UserId, request.GuestId, out var owner, out var ownerError))
            {
                return BadRequest(ownerError);
            }

            var devConfig = auth.Room?.DevMatchConfig;
            var playerDeckId = request.DeckId;
            var playerDeck = await _deckService.GetAsync(owner, playerDeckId, cancellationToken);
            if (playerDeck == null)
            {
                return NotFound("Deck not found.");
            }

            var playerDeckValidation = await _deckService.ExportCodeAsync(owner, playerDeckId, cancellationToken);
            if (!playerDeckValidation.Success)
            {
                return MapNotFoundOrBadRequest(playerDeckValidation.Error);
            }

            var selection = new MatchDeckSelection
            {
                UserId = owner.UserId,
                GuestId = owner.GuestId,
                DeckId = playerDeckId,
                DeckClass = playerDeck.Class,
                SecondaryClass = playerDeck.SecondaryClass,
                Environment = playerDeck.Environment,
                Rule = playerDeck.Rule
            };

            MatchDeckSelection? aiSelection = null;
            if (request.AiDeckId.HasValue || !string.IsNullOrWhiteSpace(request.AiDeckCode))
            {
                if (!TryBuildDeckOwner(request.AiUserId, request.AiGuestId, out var aiOwner, out _))
                {
                    aiOwner = owner;
                }

                if (request.AiDeckId.HasValue)
                {
                    var aiDeckId = request.AiDeckId.Value;
                    var aiDeck = await _deckService.GetAsync(aiOwner, aiDeckId, cancellationToken);
                    if (aiDeck == null)
                    {
                        return NotFound("AI deck not found.");
                    }

                    var aiDeckValidation = await _deckService.ExportCodeAsync(aiOwner, aiDeckId, cancellationToken);
                    if (!aiDeckValidation.Success)
                    {
                        return MapNotFoundOrBadRequest(aiDeckValidation.Error);
                    }

                    aiSelection = new MatchDeckSelection
                    {
                        UserId = aiOwner.UserId,
                        GuestId = aiOwner.GuestId,
                        DeckId = aiDeckId,
                        DeckClass = aiDeck.Class,
                        SecondaryClass = aiDeck.SecondaryClass,
                        Environment = aiDeck.Environment,
                        Rule = aiDeck.Rule
                    };
                }
                else if (!string.IsNullOrWhiteSpace(request.AiDeckCode))
                {
                    if (!_deckCodeService.TryDecode(
                        request.AiDeckCode,
                        out var aiDeckClass,
                        out var aiSecondaryClass,
                        out var aiEnvironment,
                        out var aiMode,
                        out var aiCards,
                        out var decodeError))
                    {
                        return BadRequest(decodeError ?? "AI deck code is invalid.");
                    }

                    var effectiveAiEnvironment = aiEnvironment ?? DeckEnvironment.Rotation;
                    var effectiveAiMode = aiMode ?? DeckRule.Standard;
                    var aiValidation = await _deckValidation.ValidateAsync(
                        aiDeckClass,
                        aiSecondaryClass,
                        aiCards,
                        effectiveAiEnvironment,
                        effectiveAiMode,
                        requireComplete: true,
                        cancellationToken);
                    if (!aiValidation.Success)
                    {
                        return BadRequest(aiValidation.Error ?? "AI deck validation failed.");
                    }

                    aiSelection = new MatchDeckSelection
                    {
                        DeckCode = request.AiDeckCode,
                        DeckClass = aiDeckClass,
                        SecondaryClass = aiSecondaryClass,
                        Environment = effectiveAiEnvironment,
                        Rule = effectiveAiMode
                    };
                }
            }

            if (aiSelection != null && devConfig?.IgnoreDeckFormatConsistency != true)
            {
                if (aiSelection.Environment.HasValue &&
                    selection.Environment.HasValue &&
                    aiSelection.Environment.Value != selection.Environment.Value)
                {
                    return BadRequest("AI deck environment must match player deck environment.");
                }

                if (aiSelection.Rule.HasValue &&
                    selection.Rule.HasValue &&
                    aiSelection.Rule.Value != selection.Rule.Value)
                {
                    return BadRequest("AI deck rule must match player deck rule.");
                }
            }

            var result = _matchService.SubmitDeckSelection(id, request.PlayerId, selection, aiSelection, request.Token);
            return MapResult(result, ResponseTokenScope.None);
        }

        [HttpPost("{id}/leave")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MatchResponse>> Leave(string id, [FromBody] SeatRequest request)
        {
            var validate = ValidateSeatRequest(request);
            if (validate != null) return validate;

            var auth = _matchService.ValidateConnection(id, request.PlayerId, request.Token);
            if (!auth.Success && auth.Error != MatchOperationError.AlreadyClosed) return MapResult(auth, ResponseTokenScope.None);
            if (auth.Room == null) return MapResult(auth, ResponseTokenScope.None);
            if (auth.Error == MatchOperationError.AlreadyClosed)
            {
                return Ok(ToResponse(auth.Room, ResponseTokenScope.None));
            }

            var room = auth.Room;
            var isHost = request.PlayerId == room.HostPlayerId;
            var hostLeaveReason = "房主已退出，房间已解散。";

            // 结算广播和房间收尾完成前必须保留 game -> room 映射。
            var activeGameId = room.ActiveGameId;
            var gameState = string.IsNullOrWhiteSpace(activeGameId) ? null : _gameService.GetGameState(activeGameId);
            if (!string.IsNullOrWhiteSpace(activeGameId) && gameState != null)
            {
                try
                {
                    await _gameService.EndGameForRoomLeaveAsync(activeGameId, request.PlayerId, HttpContext.RequestAborted);
                }
                catch (TimeoutException)
                {
                    return StatusCode(StatusCodes.Status504GatewayTimeout, "退出结算超时，房间席位尚未释放，请稍后重试。");
                }
                catch (ShadowverseBeyond.Server.Exceptions.GameLogicException error)
                {
                    return Conflict(error.Message);
                }
            }

            var leaveResult = _matchService.Leave(id, request.PlayerId, hostLeaveReason, request.Token);
            if (!leaveResult.Success)
            {
                if (leaveResult.Error is MatchOperationError.AlreadyClosed or MatchOperationError.NotFound or MatchOperationError.NotParticipant)
                    return Ok(ToResponse(leaveResult.Room ?? room, ResponseTokenScope.None));
                return MapResult(leaveResult, ResponseTokenScope.None);
            }

            if (isHost)
            {
                // Notify any connected gameplay clients immediately; lobby clients rely on polling.
                await _hubContext.Clients.Group(SignalRGroupNames.Game(id)).ReceiveError($"ROOM_CLOSED:{hostLeaveReason}");
            }
            else
            {
                await _hubContext.Clients.Group(SignalRGroupNames.Game(id)).ReceiveError("ROOM_GUEST_LEFT:访客已退出房间。");
            }

            return MapResult(leaveResult, ResponseTokenScope.None);
        }

        [HttpPost("{id}/kick")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MatchResponse>> Kick(string id, [FromBody] KickGuestRequest request)
        {
            var validate = ValidateSeatRequest(request);
            if (validate != null) return validate;

            var kickResult = _matchService.KickGuest(id, request.PlayerId, request.Token, request.TargetGuestPlayerId);
            if (!kickResult.Success)
            {
                return MapResult(kickResult, ResponseTokenScope.None);
            }

            await _hubContext.Clients.Group(SignalRGroupNames.Game(id)).ReceiveError("ROOM_KICKED:已被房主移出房间。");
            return MapResult(kickResult, ResponseTokenScope.None);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<MatchResponse> Get(string id, [FromQuery] Guid? playerId = null, [FromQuery] string? token = null)
        {
            var effectivePlayerId = playerId;
            if (!effectivePlayerId.HasValue &&
                Guid.TryParse(Request.Headers["X-Room-PlayerId"].FirstOrDefault(), out var headerPlayerId))
            {
                effectivePlayerId = headerPlayerId;
            }

            var effectiveToken = !string.IsNullOrWhiteSpace(token)
                ? token
                : Request.Headers["X-Room-Token"].FirstOrDefault();

            var scope = ResponseTokenScope.None;
            if (effectivePlayerId.HasValue && effectivePlayerId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(effectiveToken))
            {
                var auth = _matchService.ValidateConnection(id, effectivePlayerId.Value, effectiveToken);
                if (auth.Success)
                {
                    _matchService.RefreshExpiration(id, effectivePlayerId.Value);
                    scope = ResponseTokenScope.AuthenticatedParticipant;
                }
            }

            var result = _matchService.Get(id);
            return MapResult(result, scope);
        }

        [HttpGet]
        [ProducesResponseType(typeof(MatchRoomListResponse), StatusCodes.Status200OK)]
        public ActionResult<MatchRoomListResponse> List()
        {
            var rooms = _matchService.ListCurrentRooms()
                .Select(ToListItemResponse)
                .ToList();

            return Ok(new MatchRoomListResponse { Items = rooms });
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

        private ActionResult<MatchResponse>? ValidateDeckSelectionRequest(DeckSelectionRequest request)
        {
            if (request == null)
            {
                return BadRequest("请求体不能为空");
            }

            if (request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("playerId 和 token 必填");
            }

            if (request.DeckId == Guid.Empty)
            {
                return BadRequest("deckId 必填");
            }

            return null;
        }

        private ActionResult<MatchResponse> MapResult(MatchOperationResult result, ResponseTokenScope tokenScope)
        {
            if (result.Success && result.Room != null)
            {
                return Ok(ToResponse(result.Room, tokenScope));
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
                MatchOperationError.MissingDeckSelection => Conflict(result.Message ?? "卡组未选择完成"),
                MatchOperationError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Message ?? "无权限"),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result.Message ?? "未知错误")
            };
        }

        private static bool TryBuildDeckOwner(Guid? userId, Guid? guestId, out DeckOwner owner, out string? error)
        {
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                owner = new DeckOwner(userId, null);
                error = null;
                return true;
            }

            if (guestId.HasValue && guestId.Value != Guid.Empty)
            {
                owner = new DeckOwner(null, guestId);
                error = null;
                return true;
            }

            owner = default;
            error = "userId or guestId is required.";
            return false;
        }

        private ActionResult<MatchResponse> MapNotFoundOrBadRequest(string? error)
        {
            if (!string.IsNullOrWhiteSpace(error) && error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(error);
            }

            return BadRequest(error ?? "请求无效");
        }

        private static MatchResponse ToResponse(MatchRoom room, ResponseTokenScope tokenScope)
        {
            var isTakeTwo = room.HostDeckSelection?.GeneratedDraftCardIds?.Length == 30;
            var isMirage = room.Mode == GameMode.Mirage;
            var canRematch = room.GuestPlayerId.HasValue &&
                             !room.UseAiFallback &&
                             !room.IsResumedRoom &&
                             !isTakeTwo;
            var includeJoinCode = tokenScope != ResponseTokenScope.None || room.IsPublic;

            var response = new MatchResponse
            {
                RoomId = room.RoomId,
                ActiveGameId = room.ActiveGameId,
                HostPlayerId = room.HostPlayerId,
                GuestPlayerId = room.GuestPlayerId,
                JoinCode = includeJoinCode ? room.JoinCode : string.Empty,
                HostToken = tokenScope == ResponseTokenScope.HostOnly ? room.HostToken : null,
                GuestToken = tokenScope == ResponseTokenScope.GuestOnly ? room.GuestToken : null,
                Status = room.Status.ToString(),
                Mode = room.Mode.ToString(),
                IsMirage = isMirage,
                HostReady = room.HostReady,
                GuestReady = room.GuestReady,
                UseAiFallback = room.UseAiFallback,
                IsPublic = room.IsPublic,
                Round = room.Round,
                HostWins = room.HostWins,
                GuestWins = room.GuestWins,
                Draws = room.Draws,
                HostDeckClass = room.HostDeckSelection?.HasSelection == true ? room.HostDeckSelection.DeckClass.ToString() : null,
                GuestDeckClass = room.GuestDeckSelection?.HasSelection == true ? room.GuestDeckSelection.DeckClass.ToString() : null,
                CanRematch = canRematch,
                ClosedReason = room.ClosedReason
            };

            if (!isMirage && room.HostDeckSelection?.HasSelection == true)
            {
                response.RequiredEnvironment = room.HostDeckSelection.Environment ?? DeckEnvironment.Rotation;
                response.RequiredRule = room.HostDeckSelection.Rule ?? DeckRule.Standard;
            }

#pragma warning disable CS0618
            response.GameId = room.RoomId;
#pragma warning restore CS0618
            return response;
        }

        private static MatchRoomListItemResponse ToListItemResponse(MatchRoom room)
        {
            var isTakeTwo = room.HostDeckSelection?.GeneratedDraftCardIds?.Length == 30;
            var isMirage = room.Mode == GameMode.Mirage;
            var canJoin = room.IsPublic &&
                          room.Status == MatchStatus.Waiting &&
                          !room.GuestPlayerId.HasValue &&
                          !room.UseAiFallback &&
                          !room.IsResumedRoom &&
                          !isTakeTwo;

            return new MatchRoomListItemResponse
            {
                IsTakeTwo = isTakeTwo,
                IsMirage = isMirage,
                Mode = room.Mode.ToString(),
                DraftEnvironment = room.HostDeckSelection?.DraftEnvironment,
                RoomId = room.RoomId,
                ActiveGameId = room.ActiveGameId,
                Status = room.Status.ToString(),
                HostReady = room.HostReady,
                GuestReady = room.GuestReady,
                HasGuest = room.GuestPlayerId.HasValue,
                UseAiFallback = room.UseAiFallback,
                IsPublic = room.IsPublic,
                CanJoin = canJoin,
                Round = room.Round,
                HostWins = room.HostWins,
                GuestWins = room.GuestWins,
                Draws = room.Draws,
                CreatedAt = room.CreatedAt,
                ExpiresAt = room.ExpiresAt,
                RequiredEnvironment = !isMirage && room.HostDeckSelection?.HasSelection == true
                    ? room.HostDeckSelection.Environment ?? DeckEnvironment.Rotation
                    : null,
                RequiredRule = !isMirage && room.HostDeckSelection?.HasSelection == true
                    ? room.HostDeckSelection.Rule ?? DeckRule.Standard
                    : null
            };
        }

        // --- Request/Response DTOs ---
        public class CreateMatchRequest
        {
            public Guid? HostPlayerId { get; set; }
            public bool UseAiFallback { get; set; }
            public bool? IsPublic { get; set; }
            public int? TtlMinutes { get; set; }
            public string? Mode { get; set; }
            public int? TurnTimeoutSeconds { get; set; }
        }

        public class JoinMatchRequest
        {
            public string? JoinCode { get; set; } = string.Empty;
            public Guid? PlayerId { get; set; }
            public string? Token { get; set; }
        }

        public class SeatRequest
        {
            public Guid PlayerId { get; set; }
            public string Token { get; set; } = string.Empty;
            public bool? Ready { get; set; }
        }

        public class KickGuestRequest : SeatRequest
        {
            public Guid? TargetGuestPlayerId { get; set; }
        }

        public class DeckSelectionRequest
        {
            public Guid PlayerId { get; set; }
            public string Token { get; set; } = string.Empty;
            public Guid? UserId { get; set; }
            public Guid? GuestId { get; set; }
            public Guid DeckId { get; set; }
            public Guid? AiUserId { get; set; }
            public Guid? AiGuestId { get; set; }
            public Guid? AiDeckId { get; set; }
            public string? AiDeckCode { get; set; }
        }

        public class MatchResponse
        {
            public string RoomId { get; set; } = string.Empty;
            public string? ActiveGameId { get; set; }
            [Obsolete("Use RoomId instead.")]
            public string GameId { get; set; } = string.Empty;
            public Guid HostPlayerId { get; set; }
            public Guid? GuestPlayerId { get; set; }
            public string JoinCode { get; set; } = string.Empty;
            public string? HostToken { get; set; }
            public string? GuestToken { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Mode { get; set; } = nameof(GameMode.Standard);
            public bool IsMirage { get; set; }
            public bool HostReady { get; set; }
            public bool GuestReady { get; set; }
            public bool UseAiFallback { get; set; }
            public bool IsPublic { get; set; } = true;
            public int Round { get; set; }
            public int HostWins { get; set; }
            public int GuestWins { get; set; }
            public int Draws { get; set; }
            public string? HostDeckClass { get; set; }
            public string? GuestDeckClass { get; set; }
            public bool CanRematch { get; set; }
            public string? ClosedReason { get; set; }
            public DeckEnvironment? RequiredEnvironment { get; set; }
            public DeckRule? RequiredRule { get; set; }
        }

        public class MatchRoomListResponse
        {
            public List<MatchRoomListItemResponse> Items { get; set; } = new();
        }

        public class MatchRoomListItemResponse
        {
            public bool IsTakeTwo { get; set; }
            public bool IsMirage { get; set; }
            public string Mode { get; set; } = nameof(GameMode.Standard);
            public string? DraftEnvironment { get; set; }
            public string RoomId { get; set; } = string.Empty;
            public string? ActiveGameId { get; set; }
            public string Status { get; set; } = string.Empty;
            public bool HostReady { get; set; }
            public bool GuestReady { get; set; }
            public bool HasGuest { get; set; }
            public bool UseAiFallback { get; set; }
            public bool IsPublic { get; set; } = true;
            public bool CanJoin { get; set; }
            public int Round { get; set; }
            public int HostWins { get; set; }
            public int GuestWins { get; set; }
            public int Draws { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DeckEnvironment? RequiredEnvironment { get; set; }
            public DeckRule? RequiredRule { get; set; }
        }
    }
}
