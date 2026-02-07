using Serilog.Core;
using Serilog.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ShadowverseBeyond.Server.Infrastructure.Logging;

public class LogStreamSink : ILogEventSink
{
    private readonly LogStreamBuffer _buffer;
    private IHubContext<LogStreamHub>? _hubContext;

    public LogStreamSink(LogStreamBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public void AttachHubContext(IHubContext<LogStreamHub> hubContext)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) return;

        var entry = new LogStreamEntryDto(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(),
            logEvent.Exception?.ToString(),
            TryGetSourceContext(logEvent)
        );

        _buffer.Append(entry);

        var hubContext = _hubContext;
        if (hubContext == null)
        {
            // Hub context is attached after the app is built; skip broadcast until then.
            return;
        }

        _ = hubContext.Clients
            .Group(LogStreamHub.AuthorizedGroup)
            .SendAsync(LogStreamHub.LogEntryMethod, entry);
    }

    private static string? TryGetSourceContext(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var value))
        {
            return value.ToString().Trim('"');
        }

        return null;
    }
}
