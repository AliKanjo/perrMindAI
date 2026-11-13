using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Admin dashboard and user management (FR-13).</summary>
[Authorize(Roles = Roles.Admin)]
public class AdminController : BaseController
{
    private static readonly string[] KnownRoles = { Roles.Student, Roles.Tutor, Roles.Admin };
    private static readonly string[] KnownStatuses = { UserStatus.Active, UserStatus.PendingVerification, UserStatus.Suspended };

    private readonly UserRepository _users;
    private readonly ApplicationRepository _applications;

    public AdminController(UserRepository users, ApplicationRepository applications)
    {
        _users = users;
        _applications = applications;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        return View(await _applications.GetDashboardAsync());
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Users(string? q, string? role, string? status)
    {
        // ignore filter values that are not in the lists
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        role = KnownRoles.Contains(role) ? role : null;
        status = KnownStatuses.Contains(status) ? status : null;

        return View(new UserListPage
        {
            Users = await _users.ListAsync(q, role, status),
            Search = q,
            Role = role,
            Status = status
        });
    }

    [HttpGet("/admin/users/{id:int}")]
    public async Task<IActionResult> UserDetails(int id)
    {
        var user = await _users.GetByIdAsync(id);
        return user == null ? NotFound() : View(user);
    }

    /// <summary>Suspend or re-activate an account.</summary>
    [HttpPost("/admin/users/{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, string? status)
    {
        if (status != UserStatus.Active && status != UserStatus.Suspended)
        {
            Toast("Unknown account status.", isError: true);
            return RedirectToAction(nameof(UserDetails), new { id });
        }
        try
        {
            await _users.SetStatusAsync(CurrentUserId, id, status);
            Toast(status == UserStatus.Suspended ? "The user was suspended." : "The account is active again.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(UserDetails), new { id });
    }

    [HttpPost("/admin/users/{id:int}/delete")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            await _users.DeleteAsync(CurrentUserId, id);
            Toast("The user was deleted.");
            return RedirectToAction(nameof(Users));
        }
        catch (DbRuleException ex)
        {
            // e.g. the user has bookings: the procedure asks to suspend instead
            Toast(ex.Message, isError: true);
            return RedirectToAction(nameof(UserDetails), new { id });
        }
    }
}
