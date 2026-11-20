using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;

namespace ShadowverseBeyond.Server.Controllers;

// Full event histories include both hands and the order of both decks.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ClosedReplayHistoryAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var gameId = context.RouteData.Values["gameId"]?.ToString();
        var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
        var readable = await db.ReplayGames.AsNoTracking().AnyAsync(game => game.GameId == gameId &&
            (game.SchemaVersion != SqliteReplayHistory.FormatVersion || (game.EndedAtUtc != null &&
             db.ReplayEvents.Any(e => e.GameId == game.GameId && e.EventType == "recording.closed"))), http.RequestAborted);
        if (!readable)
        {
            var session = await http.AuthenticateAsync(AccountSession.Scheme);
            var auth = http.RequestServices.GetRequiredService<IAuthorizationService>();
            if (session.Principal == null || !(await auth.AuthorizeAsync(session.Principal, http, AccountSession.AdminPolicy)).Succeeded)
            {
                context.Result = new NotFoundResult();
                return;
            }
        }
        await next();
    }
}
