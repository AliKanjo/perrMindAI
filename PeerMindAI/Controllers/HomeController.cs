using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Public landing page and the error pages.</summary>
public class HomeController : BaseController
{
    private readonly TutorRepository _tutors;
    private readonly ILogger<HomeController> _logger;

    public HomeController(TutorRepository tutors, ILogger<HomeController> logger)
    {
        _tutors = tutors;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.GetRole() switch
            {
                Roles.Admin => Redirect("/admin"),
                Roles.Tutor => Redirect("/tutor"),
                _ => Redirect("/student")
            };
        }

        var tutors = await _tutors.SearchAsync(null, null, null, null);
        return View(tutors.Take(3).ToList());
    }

    /// <summary>Unhandled exceptions end here (see UseExceptionHandler in Program.cs).</summary>
    [IgnoreAntiforgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error()
    {
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (exception != null)
        {
            _logger.LogError(exception, "Unhandled error");
        }

        var model = exception switch
        {
            DbRuleException rule => new ErrorViewModel { StatusCode = 400, Title = "This action is not possible", Message = rule.Message },
            MySqlException => new ErrorViewModel
            {
                StatusCode = 503,
                Title = "The database is not available",
                Message = "PeerMind AI could not reach its database. Please make sure MariaDB/MySQL is running and try again."
            },
            _ => new ErrorViewModel { Message = "An unexpected error happened. Please go back and try again." }
        };

        Response.StatusCode = model.StatusCode;

        // a background (fetch) request gets JSON it can show, never an HTML page or technical details
        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { ok = false, message = model.Message });
        }
        return View("Error", model);
    }

    /// <summary>Friendly pages for 404, 403 and other status codes.</summary>
    [IgnoreAntiforgeryToken]
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { StatusCode = 404, Title = "Page not found", Message = "The page you are looking for does not exist or was moved." },
            403 => new ErrorViewModel { StatusCode = 403, Title = "Access denied", Message = "You do not have permission to open this page." },
            400 => new ErrorViewModel { StatusCode = 400, Title = "Invalid request", Message = "The request could not be processed. Please reload the page and try again." },
            _ => new ErrorViewModel { StatusCode = code, Message = "The request could not be completed." }
        };
        return View("Error", model);
    }
}
