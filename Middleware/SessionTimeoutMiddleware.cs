using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace AceJob.Middleware;

/// <summary>
/// Razor Pages apps often use cookie-auth (Identity) *and* Session.
/// Session idle timeout does not automatically redirect users; it simply clears session state.
///
/// This middleware detects when a user is authenticated but their session has expired
/// (missing expected session keys) and forces a sign-out + redirect to /Login.
/// </summary>
public sealed class SessionTimeoutMiddleware
{
 private readonly RequestDelegate _next;
 private readonly ILogger<SessionTimeoutMiddleware> _logger;

 // Session keys your app sets on login/registration
 private const string UserIdKey = "UserId";

 // Paths we should not interfere with (avoid redirect loops)
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

 public SessionTimeoutMiddleware(RequestDelegate next, ILogger<SessionTimeoutMiddleware> logger)
 {
 _next = next;
 _logger = logger;
 }

 public async Task InvokeAsync(HttpContext context)
 {
 // Only apply to authenticated users
 if (context.User?.Identity?.IsAuthenticated == true)
 {
 var path = context.Request.Path;

 // Ignore static files + auth pages
 if (!BypassPaths.Any(p => path.StartsWithSegments(p))
 && !path.StartsWithSegments("/css")
 && !path.StartsWithSegments("/js")
 && !path.StartsWithSegments("/lib")
 && !path.StartsWithSegments("/uploads")
 && !path.StartsWithSegments("/favicon"))
 {
 // If the session doesn't contain our "UserId" marker, treat it as expired.
 // (Session values are cleared after IdleTimeout.)
 if (string.IsNullOrEmpty(context.Session.GetString(UserIdKey)))
 {
 var safePath = path.ToString().Replace("\r", string.Empty).Replace("\n", string.Empty);
 _logger.LogInformation("Session expired for authenticated user. Redirecting to /Login. Path: {Path}", safePath);

 // Clear any remaining session values
 context.Session.Clear();

 // Force auth cookie removal to avoid "authenticated but no session" state.
 await context.SignOutAsync();

 // Preserve returnUrl for convenience
 var returnUrl = context.Request.Path + context.Request.QueryString;
 context.Response.Redirect($"/Login?reason=session-timeout&returnUrl={Uri.EscapeDataString(returnUrl)}");
 return;
 }
 }
 }

 await _next(context);
 }
}
