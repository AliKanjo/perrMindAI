using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>The tutor area: dashboard, booking requests, availability, reviews and profile (FR-07).</summary>
[Authorize(Roles = Roles.Tutor)]
public class TutorController : BaseController
{
    private readonly TutorRepository _tutors;
    private readonly BookingRepository _bookings;
    private readonly ReportRepository _reports;

    public TutorController(TutorRepository tutors, BookingRepository bookings, ReportRepository reports)
    {
        _tutors = tutors;
        _bookings = bookings;
        _reports = reports;
    }

    [HttpGet("/tutor")]
    public async Task<IActionResult> Index()
    {
        var bookings = await _bookings.ListForTutorAsync(CurrentUserId);
        return View(new TutorDashboard
        {
            FirstName = Ui.FirstName(User.GetFullName()),
            Stats = await _tutors.GetStatsAsync(CurrentUserId),
            PendingRequests = bookings.Where(b => b.Status == BookingStatus.Pending).ToList(),
            Upcoming = bookings.Where(b => b.Status == BookingStatus.Confirmed && !b.HasEnded).Take(5).ToList()
        });
    }

    [HttpGet("/tutor/requests")]
    public async Task<IActionResult> Requests()
    {
        var bookings = await _bookings.ListForTutorAsync(CurrentUserId);
        return View(new TutorRequestsPage
        {
            Pending = bookings.Where(b => b.Status == BookingStatus.Pending).ToList(),
            Upcoming = bookings.Where(b => b.Status == BookingStatus.Confirmed && !b.HasEnded).ToList(),
            ToComplete = bookings.Where(b => b.Status == BookingStatus.Confirmed && b.HasEnded).ToList(),
            Past = bookings.Where(b => !b.IsActive).OrderByDescending(b => b.StartAt).Take(10).ToList()
        });
    }

    // ---------- availability ----------

    [HttpGet("/tutor/availability")]
    public async Task<IActionResult> Availability()
    {
        return View(await BuildAvailabilityPageAsync(new SlotForm { Date = DateTime.Today.AddDays(1), StartTime = "16:00" }));
    }

    [HttpPost("/tutor/availability")]
    public async Task<IActionResult> AddSlot([Bind(Prefix = "Form")] SlotForm form)
    {
        if (ModelState.IsValid)
        {
            var startAt = form.Date!.Value.Date + TimeSpan.Parse(form.StartTime);
            try
            {
                await _tutors.AddSlotAsync(CurrentUserId, startAt, startAt.AddMinutes(form.DurationMinutes));
                Toast("The time slot was added. Students can book it now.");
                return RedirectToAction(nameof(Availability));
            }
            catch (DbRuleException ex)
            {
                // e.g. "This time overlaps another of your slots."
                ModelState.AddModelError("", ex.Message);
            }
        }
        return View(nameof(Availability), await BuildAvailabilityPageAsync(form));
    }

    [HttpPost("/tutor/availability/{slotId:int}/remove")]
    public async Task<IActionResult> RemoveSlot(int slotId)
    {
        try
        {
            await _tutors.RemoveSlotAsync(CurrentUserId, slotId);
            Toast("The time slot was removed.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Availability));
    }

    // ---------- reviews ----------

    [HttpGet("/tutor/reviews")]
    public async Task<IActionResult> Reviews()
    {
        return View(new ReviewsPage
        {
            Stats = await _tutors.GetStatsAsync(CurrentUserId),
            Reviews = await _tutors.GetReviewsAsync(CurrentUserId)
        });
    }

    [HttpPost("/tutor/reviews/{reviewId:int}/reply")]
    public async Task<IActionResult> Reply(int reviewId, string? reply)
    {
        reply = reply?.Trim();
        if (string.IsNullOrEmpty(reply) || reply.Length > 1000)
        {
            Toast("Please write a reply of 1 to 1000 characters.", isError: true);
            return RedirectToAction(nameof(Reviews));
        }
        try
        {
            await _tutors.ReplyToReviewAsync(reviewId, CurrentUserId, reply);
            Toast("Your reply was published.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Reviews));
    }

    /// <summary>Reports one of the tutor's reviews (and its author) to the administrators.</summary>
    [HttpPost("/tutor/reviews/{reviewId:int}/report")]
    public async Task<IActionResult> ReportReview(int reviewId, ReportForm form)
    {
        if (!ReportTypes.IsValid(form.ReportType))
        {
            ModelState.AddModelError(nameof(form.ReportType), "Please choose what the problem is.");
        }
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Reviews));
        }

        var authorId = await _reports.GetReviewAuthorAsync(reviewId, CurrentUserId);
        if (authorId == null)
        {
            return NotFound();
        }
        try
        {
            await _reports.CreateAsync(CurrentUserId, authorId.Value, reviewId, form);
            Toast("Your report was sent. An administrator will review it.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Reviews));
    }

    // ---------- public tutor profile ----------

    [HttpGet("/tutor/profile")]
    public async Task<IActionResult> Profile()
    {
        var form = await _tutors.GetProfileFormAsync(CurrentUserId);
        if (form == null)
        {
            return NotFound();
        }
        form.Courses = await _tutors.GetCoursesAsync(CurrentUserId);
        return View(form);
    }

    [HttpPost("/tutor/profile")]
    public async Task<IActionResult> Profile(TutorProfileForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _tutors.UpdateProfileAsync(CurrentUserId, form);
                Toast("Your tutor profile was saved.");
                return RedirectToAction(nameof(Profile));
            }
            catch (DbRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }
        form.Courses = await _tutors.GetCoursesAsync(CurrentUserId);
        return View(form);
    }

    /// <summary>Stops teaching a course (FR-07). New courses go through a new tutor application.</summary>
    [HttpPost("/tutor/courses/{courseId:int}/remove")]
    public async Task<IActionResult> RemoveCourse(int courseId)
    {
        try
        {
            await _tutors.RemoveCourseAsync(CurrentUserId, courseId);
            Toast("The course was removed from your profile.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Profile));
    }

    private async Task<AvailabilityPage> BuildAvailabilityPageAsync(SlotForm form) => new()
    {
        Form = form,
        Slots = await _tutors.GetUpcomingSlotsAsync(CurrentUserId)
    };
}
