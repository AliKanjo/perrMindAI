using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Student dashboard.</summary>
[Authorize(Roles = Roles.Student)]
public class StudentController : BaseController
{
    private readonly BookingRepository _bookings;
    private readonly ApplicationRepository _applications;
    private readonly QuizRepository _quizzes;

    public StudentController(BookingRepository bookings, ApplicationRepository applications, QuizRepository quizzes)
    {
        _bookings = bookings;
        _applications = applications;
        _quizzes = quizzes;
    }

    [HttpGet("/student")]
    public async Task<IActionResult> Index()
    {
        var bookings = await _bookings.ListForStudentAsync(CurrentUserId);

        var dashboard = new StudentDashboard
        {
            FirstName = Ui.FirstName(User.GetFullName()),
            Upcoming = bookings.Where(b => b.IsActive && !b.HasEnded).OrderBy(b => b.StartAt).ToList(),
            ToReview = bookings.Where(b => b.Status == BookingStatus.Completed && b.ReviewId == null).ToList(),
            CompletedSessions = bookings.Count(b => b.Status == BookingStatus.Completed),
            PendingRequests = bookings.Count(b => b.Status == BookingStatus.Pending),
            Application = await _applications.GetLatestForApplicantAsync(CurrentUserId),
            Recommendations = await _quizzes.GetOpenRecommendationsAsync(CurrentUserId)
        };
        return View(dashboard);
    }
}
