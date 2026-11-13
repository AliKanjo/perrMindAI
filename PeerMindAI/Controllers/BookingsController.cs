using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>
/// Booking workflow (FR-05, FR-07, FR-12): request a session, view, edit notes,
/// accept / decline, cancel, complete and review.
/// </summary>
[Authorize]
public class BookingsController : BaseController
{
    private readonly BookingRepository _bookings;
    private readonly TutorRepository _tutors;
    private readonly UserRepository _users;

    public BookingsController(BookingRepository bookings, TutorRepository tutors, UserRepository users)
    {
        _bookings = bookings;
        _tutors = tutors;
        _users = users;
    }

    // ---------- student ----------

    [Authorize(Roles = Roles.Student)]
    [HttpGet("/bookings")]
    public async Task<IActionResult> Index()
    {
        return View(await _bookings.ListForStudentAsync(CurrentUserId));
    }

    /// <summary>Posted by the booking form on the tutor profile page.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("/bookings")]
    public async Task<IActionResult> Create(BookingForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var bookingId = await _bookings.CreateAsync(CurrentUserId, form.SlotId!.Value, form.CourseId!.Value,
                    form.Topic?.Trim(), form.Notes?.Trim());
                Toast("Booking request sent. The tutor will confirm it soon.");
                return RedirectToAction(nameof(Details), new { id = bookingId });
            }
            catch (DbRuleException ex)
            {
                // e.g. another student took the slot a moment ago
                ModelState.AddModelError("", ex.Message);
            }
        }

        // show the profile again with the error and the slots that are still free
        var tutor = await _tutors.GetProfileAsync(form.TutorId);
        if (tutor == null)
        {
            return NotFound();
        }
        return View("~/Views/Tutors/Details.cshtml", new TutorProfilePage { Tutor = tutor, Form = form });
    }

    [Authorize(Roles = Roles.Student)]
    [HttpPost("/bookings/{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, BookingNotesForm form)
    {
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Details), new { id });
        }
        return await RunAndReturnAsync(id, "Your notes were updated.",
            () => _bookings.UpdateNotesAsync(id, CurrentUserId, form.Topic?.Trim(), form.Notes?.Trim()));
    }

    [Authorize(Roles = Roles.Student)]
    [HttpPost("/bookings/{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewForm form)
    {
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Details), new { id });
        }
        return await RunAndReturnAsync(id, "Thank you! Your review was published.",
            () => _bookings.CreateReviewAsync(id, CurrentUserId, form.Rating!.Value, form.Comment?.Trim()));
    }

    // ---------- student and tutor ----------

    /// <summary>Booking details. The stored procedure only returns it to its student, its tutor or an admin.</summary>
    [HttpGet("/bookings/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _bookings.GetAsync(id, CurrentUserId);
        if (result == null)
        {
            return NotFound();
        }

        var windowSetting = await _users.GetSettingAsync("BookingCancellationWindowHours");
        return View(new BookingDetailsViewModel
        {
            Booking = result.Value.Booking,
            History = result.Value.History,
            ViewerIsStudent = result.Value.Booking.StudentId == CurrentUserId,
            ViewerIsTutor = result.Value.Booking.TutorId == CurrentUserId,
            CancellationWindowHours = int.TryParse(windowSetting, out var hours) ? hours : 24
        });
    }

    [Authorize(Roles = Roles.Student + "," + Roles.Tutor)]
    [HttpPost("/bookings/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelForm form)
    {
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Details), new { id });
        }
        return await RunAndReturnAsync(id, "The session was cancelled.",
            () => _bookings.CancelAsync(id, CurrentUserId, form.Reason.Trim()));
    }

    // ---------- tutor (called with fetch, answers in JSON) ----------

    public record RespondRequest(bool Accept);

    [Authorize(Roles = Roles.Tutor)]
    [HttpPost("/bookings/{id:int}/respond")]
    public async Task<IActionResult> Respond(int id, [FromBody] RespondRequest? request)
    {
        if (request == null)
        {
            return JsonError("Please choose to accept or decline the request.");
        }
        try
        {
            await _bookings.RespondAsync(id, CurrentUserId, request.Accept);
            return Json(new
            {
                ok = true,
                status = request.Accept ? BookingStatus.Confirmed : BookingStatus.Declined,
                message = request.Accept ? "Booking confirmed. The student was notified." : "Booking declined. The student was notified."
            });
        }
        catch (DbRuleException ex)
        {
            return JsonError(ex.Message);
        }
    }

    [Authorize(Roles = Roles.Tutor)]
    [HttpPost("/bookings/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        try
        {
            await _bookings.CompleteAsync(id, CurrentUserId);
            return Json(new { ok = true, status = BookingStatus.Completed, message = "Session marked as completed. The student can now review it." });
        }
        catch (DbRuleException ex)
        {
            return JsonError(ex.Message);
        }
    }

    /// <summary>Runs a change, then goes back to the booking page with a success or error message.</summary>
    private async Task<IActionResult> RunAndReturnAsync(int bookingId, string successMessage, Func<Task> change)
    {
        try
        {
            await change();
            Toast(successMessage);
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Details), new { id = bookingId });
    }
}
