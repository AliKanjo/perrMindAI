using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PeerMindAI.Helpers;

/// <summary>Builds and reads the identity stored in the log-in cookie.</summary>
public static class UserClaims
{
    public static ClaimsPrincipal Create(int userId, string fullName, string email, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string GetRole(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static string GetFullName(this ClaimsPrincipal user) => user.Identity?.Name ?? "";
}
