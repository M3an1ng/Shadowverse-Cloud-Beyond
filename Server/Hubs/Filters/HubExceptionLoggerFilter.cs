using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Hubs.Filters
{
    /// <summary>
    /// A SignalR Hub Filter that catches unhandled exceptions during Hub method invocation
    /// and logs them using the standard ILogger.
    /// </summary>
    public class HubExceptionLoggerFilter : IHubFilter
    {
        private readonly ILogger<HubExceptionLoggerFilter> _logger;

        public HubExceptionLoggerFilter(ILogger<HubExceptionLoggerFilter> logger)
        {
            _logger = logger;
        }

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            // Log entry point (optional, can be verbose)
            // _logger.LogInformation("Filter: Entering Hub method '{HubMethodName}'", invocationContext.HubMethodName);

            try
            {
                // Continue the pipeline, executing the actual Hub method
                return await next(invocationContext);
            }
            catch (Exception ex)
            {
                // Log the unhandled exception that bubbled up from the Hub method
                _logger.LogError(ex, "FILTER_ERROR: Unhandled exception caught in Hub method '{HubMethodName}'", invocationContext.HubMethodName);

                // It's important to re-throw the exception so SignalR's default
                // error handling (including sending errors to the client based on EnableDetailedErrors
                // or HubException types) can still proceed.
                throw;
            }
            // Log exit point (optional)
            // finally
            // {
            //     _logger.LogInformation("Filter: Exiting Hub method '{HubMethodName}'", invocationContext.HubMethodName);
            // }
        }

        // Optional methods for connection lifetime events (can be omitted if not needed)
        // public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        // {
        //     _logger.LogInformation("Filter: Client connected: {ConnectionId}", context.Context.ConnectionId);
        //     return next(context);
        // }

        // public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
        // {
        //     if (exception != null)
        //     {
        //         _logger.LogWarning(exception, "Filter: Client disconnected with error: {ConnectionId}", context.Context.ConnectionId);
        //     }
        //     else
        //     {
        //         _logger.LogInformation("Filter: Client disconnected gracefully: {ConnectionId}", context.Context.ConnectionId);
        //     }
        //     return next(context, exception);
        // }
    }
}