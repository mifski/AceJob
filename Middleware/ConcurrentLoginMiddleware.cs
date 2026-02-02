using AceJob.Model;
using AceJob.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace AceJob.Middleware;

/// <summary>
/// Detects and invalidates older auth sessions when a user logs in elsewhere.
///
/// Mechanism:
/// - On successful login we increment ApplicationUser.SessionVersion and issue a claim "sv" with the new value.
/// - On each request, if the authenticated user's "sv" claim doesn't match the DB SessionVersion, we sign them out.
/// </summary>
public sealed class ConcurrentLoginMiddleware
{
    private const string SessionVersionClaimType = "sv";

    private readonly RequestDelegate _next;
    private readonly ILogger<ConcurrentLoginMiddleware> _logger;

    private static readonly PathString[] BypassPaths =
    [
        "/Login",
        "/Logout",
        "/Register",
        "/Error",
        "/AccessDenied",
        "/NotFound",
        "/LoginWith2fa",
        "/LoginWithRecoveryCode"
    ];

    public ConcurrentLoginMiddleware(
        RequestDelegate next,
        ILogger<ConcurrentLoginMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        if (BypassPaths.Any(p => path.StartsWithSegments(p))
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/lib")
            || path.StartsWithSegments("/uploads")
            || path.StartsWithSegments("/favicon"))
        {
            await _next(context);
            return;
        }

        // Resolve scoped services from the request's service provider
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var auditService = context.RequestServices.GetRequiredService<IAuditService>();

        var userId = userManager.GetUserId(context.User);
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var claim = context.User.FindFirst(SessionVersionClaimType)?.Value;
        if (!int.TryParse(claim, out var tokenSessionVersion))
        {
            // No claim => treat as invalid/old session.
            await KickAsync(context, userId, "Missing/invalid session version claim", auditService);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            await KickAsync(context, userId, "User no longer exists", auditService);
            return;
        }

        if (tokenSessionVersion != user.SessionVersion)
        {
            await KickAsync(context, userId, "SessionVersion mismatch (logged in elsewhere)", auditService);
            return;
        }

        await _next(context);
    }

    private async Task KickAsync(HttpContext context, string userId, string reason, IAuditService auditService)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.Identity?.Name ?? "";

        _logger.LogInformation("Concurrent login detected. Signing out userId={UserId}. Reason={Reason}", userId, reason);

        try
        {
            await auditService.LogAsync(
                AuditActions.ConcurrentLogin,
                userId,
                email,
                $"Signed out due to concurrent login. {reason}",
                isSuccess: true);
        }
        catch
        {
            // ignore audit failures
        }

        context.Session.Clear();
        await context.SignOutAsync();

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Login?reason=concurrent-login&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
}
