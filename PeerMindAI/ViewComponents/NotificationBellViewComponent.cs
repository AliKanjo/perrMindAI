using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;

namespace PeerMindAI.ViewComponents;

/// <summary>The bell in the top bar, with a red dot when there are unread notifications.</summary>
public class NotificationBellViewComponent : ViewComponent
{
    private readonly UserRepository _users;

    public NotificationBellViewComponent(UserRepository users) => _users = users;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var unread = await _users.CountUnreadNotificationsAsync(UserClaimsPrincipal.GetUserId());
        return View(unread);
    }
}
