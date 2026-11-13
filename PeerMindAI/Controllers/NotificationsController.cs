using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;

namespace PeerMindAI.Controllers;

/// <summary>The notification list of the logged-in user.</summary>
[Authorize]
public class NotificationsController : BaseController
{
    private readonly UserRepository _users;

    public NotificationsController(UserRepository users) => _users = users;

    [HttpGet("/notifications")]
    public async Task<IActionResult> Index()
    {
        return View(await _users.GetNotificationsAsync(CurrentUserId));
    }

    /// <summary>Called with fetch from the "Mark all as read" button.</summary>
    [HttpPost("/notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var marked = await _users.MarkAllNotificationsReadAsync(CurrentUserId);
        return Json(new { ok = true, marked, message = marked == 0 ? "Everything was already read." : "All notifications marked as read." });
    }
}
