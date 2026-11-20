using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Services;

public static class AccountSession
{
    public const string Scheme = "AccountSession";
    public const string AdminPolicy = "ActiveAdministrator";
    public const string PlayerPolicy = "ActivePlayerAccount";
    private const string CredentialVersionClaim = "svb:credential-version";

    public static void AddAccountSession(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddCookie(Scheme, options =>
            {
                options.Cookie.Name = "svb.account";
                options.Cookie.Path = "/api";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicy, policy => RequireCurrentAccount(policy, adminOnly: true));
            options.AddPolicy(PlayerPolicy, policy => RequireCurrentAccount(policy, adminOnly: false));
        });
    }

    private static void RequireCurrentAccount(AuthorizationPolicyBuilder policy, bool adminOnly)
    {
        policy
            .AddAuthenticationSchemes(Scheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(async context =>
            {
                if (context.Resource is not HttpContext httpContext || GetUserId(context.User) is not Guid userId)
                    return false;

                var db = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var passwordHash = await db.Users
                    .Where(user => user.Id == userId && user.IsActive && user.Role != UserRole.Guest
                        && (!adminOnly || user.Role == UserRole.Admin))
                    .Select(user => user.PasswordHash)
                    .SingleOrDefaultAsync(httpContext.RequestAborted);
                return passwordHash != null
                    && context.User.FindFirstValue(CredentialVersionClaim) == GetCredentialVersion(passwordHash);
            });
    }

    public static Task SignInAsync(HttpContext context, User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(CredentialVersionClaim, GetCredentialVersion(user.PasswordHash))
        ], Scheme);

        return context.SignInAsync(Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    public static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string GetCredentialVersion(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
}
