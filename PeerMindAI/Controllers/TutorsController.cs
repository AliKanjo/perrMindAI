using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Find a tutor and view a tutor profile (FR-03, FR-04, Scenario 1).</summary>
[Authorize(Roles = Roles.Student)]
public class TutorsController : BaseController
{
    private readonly TutorRepository _tutors;
    private readonly CourseRepository _courses;

    public TutorsController(TutorRepository tutors, CourseRepository courses)
    {
        _tutors = tutors;
        _courses = courses;
    }

    /// <summary>The search page. The results are loaded by JavaScript from <see cref="Search"/>.</summary>
    [HttpGet("/tutors")]
    public async Task<IActionResult> Index(string? q, int? courseId)
    {
        return View(new TutorSearchPage
        {
            Courses = await _courses.ListAsync(null, includeInactive: false),
            Search = q,
            CourseId = courseId
        });
    }

    /// <summary>Asynchronous search: returns the matching tutors as JSON.</summary>
    [HttpGet("/tutors/search")]
    public async Task<IActionResult> Search(string? q, int? courseId, decimal? minRating, int? withinDays)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (q?.Length > 100)
        {
            return JsonError("The search text is too long (100 characters at most).");
        }
        if (minRating is < 0 or > 5 || withinDays is < 0 or > 60)
        {
            return JsonError("One of the filters has an invalid value.");
        }

        var tutors = await _tutors.SearchAsync(courseId, q, minRating, withinDays);

        return Json(new
        {
            ok = true,
            tutors = tutors.Select(t => new
            {
                t.TutorId,
                t.FullName,
                t.University,
                t.Headline,
                t.Courses,
                t.HourlyRate,
                t.AverageRating,
                t.ReviewCount,
                NextAvailable = t.NextAvailable == null ? null : Ui.When(t.NextAvailable.Value)
            })
        });
    }

    [HttpGet("/tutors/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var tutor = await _tutors.GetProfileAsync(id);
        if (tutor == null)
        {
            return NotFound();
        }
        return View(new TutorProfilePage { Tutor = tutor, Form = new BookingForm { TutorId = id } });
    }
}
