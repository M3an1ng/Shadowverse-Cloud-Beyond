using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Logging;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Hubs;

public class LogStreamHub : Hub
{
    public const string AuthorizedGroup = "log-stream-users";
    public const string LogEntryMethod = "LogEntry";
    private readonly LogStreamBuffer _buffer;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LogStreamHub> _logger;

    public LogStreamHub(LogStreamBuffer buffer, ApplicationDbContext db, ILogger<LogStreamHub> logger)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var userIdValue = httpContext?.Request.Query["userId"].ToString();
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("Log stream connection rejected: missing or invalid userId. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var exists = await _db.Users.AnyAsync(user => user.Id == userId);
        if (!exists)
        {
            _logger.LogWarning("Log stream connection rejected: user not found. UserId={UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AuthorizedGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AuthorizedGroup);
        await base.OnDisconnectedAsync(exception);
    }

    public Task<IReadOnlyList<LogStreamEntryDto>> GetSnapshot(int take = 200)
    {
        var safeTake = Math.Clamp(take, 0, 1000);
        var entries = _buffer.Snapshot(safeTake);
        return Task.FromResult(entries);
    }
}
