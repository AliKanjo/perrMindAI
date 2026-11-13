using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySqlConnector;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Services;

/// <summary>
/// Runs on every request of a logged-in user. It re-reads the account so that
/// a suspended or deleted user is signed out immediately, and a role change
/// (a student approved as tutor) takes effect without logging in again.
/// </summary>
public class SessionUserValidator : CookieAuthenticationEvents
{
    private readonly UserRepository _users;

    public SessionUserValidator(UserRepository users) => _users = users;

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal == null)
        {
            return;
        }

        UserProfile? user;
        try
        {
            user = await _users.GetByIdAsync(principal.GetUserId());
        }
        catch (MySqlException)
        {
            return;   // database unreachable: the page itself will show the error
        }

        if (user == null || user.Status != UserStatus.Active)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (user.Role != principal.GetRole() || user.FullName != principal.GetFullName())
        {
            context.ReplacePrincipal(UserClaims.Create(user.UserId, user.FullName, user.Email, user.Role));
            context.ShouldRenew = true;
        }
    }

    // Background (fetch) requests get a status code instead of a redirect to the log-in page
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        IsApiRequest(context.Request) ? SetStatus(context, StatusCodes.Status401Unauthorized) : base.RedirectToLogin(context);

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        IsApiRequest(context.Request) ? SetStatus(context, StatusCodes.Status403Forbidden) : base.RedirectToAccessDenied(context);

    private static bool IsApiRequest(HttpRequest request) =>
        request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    private static Task SetStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
