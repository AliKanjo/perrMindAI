using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Helpers;

namespace PeerMindAI.Controllers;

/// <summary>Things every controller needs: the logged-in user and feedback messages.</summary>
public abstract class BaseController : Controller
{
    protected int CurrentUserId => User.GetUserId();

    /// <summary>Shows a short message after the next page load (green for success, red for errors).</summary>
    protected void Toast(string message, bool isError = false)
    {
        TempData["Toast"] = message;
        TempData["ToastType"] = isError ? "bad" : "ok";
    }

    /// <summary>JSON answer for a background request that failed a business rule.</summary>
    protected IActionResult JsonError(string message, int statusCode = StatusCodes.Status400BadRequest) =>
        StatusCode(statusCode, new { ok = false, message });

    /// <summary>All validation messages of the posted form, joined for a JSON answer.</summary>
    protected string ModelErrors() =>
        string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
}
