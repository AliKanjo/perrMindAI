using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>Applying to become a tutor and tracking the application (FR-06, Scenario 4).</summary>
[Authorize(Roles = Roles.Student + "," + Roles.Tutor)]
public class TutorApplicationController : BaseController
{
    private const int MinimumYearOfStudy = 2;

    private readonly ApplicationRepository _applications;
    private readonly CourseRepository _courses;
    private readonly UserRepository _users;
    private readonly TranscriptStorage _storage;

    public TutorApplicationController(ApplicationRepository applications, CourseRepository courses,
        UserRepository users, TranscriptStorage storage)
    {
        _applications = applications;
        _courses = courses;
        _users = users;
        _storage = storage;
    }

    /// <summary>Status page: pending, approved, rejected or "not applied yet".</summary>
    [HttpGet("/tutor/verification")]
    public async Task<IActionResult> Status()
    {
        return View(await _applications.GetLatestForApplicantAsync(CurrentUserId));
    }

    [HttpGet("/tutor/application")]
    public async Task<IActionResult> Apply()
    {
        var existing = await _applications.GetLatestForApplicantAsync(CurrentUserId);
        if (IsLocked(existing))
        {
            return RedirectToAction(nameof(Status));
        }

        var form = new TutorApplicationForm();
        if (existing != null)
        {
            // start from the previous answers (a draft, a "more info" request or a rejected application)
            form.UniversityId = existing.UniversityId;
            form.Degree = existing.Degree ?? "";
            form.Major = existing.Major ?? "";
            form.Gpa = existing.Gpa;
            form.YearOfStudy = existing.YearOfStudy;
            form.Bio = existing.Bio ?? "";
            form.CourseIds = existing.Courses.Select(c => c.CourseId).ToList();
        }
        else
        {
            var user = await _users.GetByIdAsync(CurrentUserId);
            form.UniversityId = user?.UniversityId;
            form.YearOfStudy = user?.YearOfStudy;
        }
        return View(await BuildPageAsync(form, existing));
    }

    [HttpPost("/tutor/application")]
    public async Task<IActionResult> Apply([Bind(Prefix = "Form")] TutorApplicationForm form)
    {
        var existing = await _applications.GetLatestForApplicantAsync(CurrentUserId);
        if (IsLocked(existing))
        {
            return RedirectToAction(nameof(Status));
        }

        var editable = existing is { IsEditable: true } ? existing : null;
        var minimumGpa = await GetMinimumGpaAsync();

        // rules that the data annotations cannot express
        if (form.CourseIds.Count == 0)
        {
            ModelState.AddModelError("Form.CourseIds", "Please select at least one course you want to teach.");
        }
        if (form.Gpa < minimumGpa)
        {
            ModelState.AddModelError("Form.Gpa", $"Tutors need a GPA of at least {minimumGpa:0.0}.");
        }
        if (form.YearOfStudy < MinimumYearOfStudy)
        {
            ModelState.AddModelError("Form.YearOfStudy", "You must have completed your 2nd year or above.");
        }

        var alreadyHasTranscript = editable?.Documents.Any(d => d.DocumentType == "Transcript") == true;
        if (form.Transcript == null && !alreadyHasTranscript)
        {
            ModelState.AddModelError("Form.Transcript", "Please upload your official transcript (PDF).");
        }
        else if (form.Transcript != null && await _storage.ValidateAsync(form.Transcript) is { } fileError)
        {
            ModelState.AddModelError("Form.Transcript", fileError);
        }

        if (ModelState.IsValid)
        {
            try
            {
                int applicationId;
                if (editable != null)
                {
                    applicationId = editable.ApplicationId;
                    await _applications.UpdateDraftAsync(applicationId, CurrentUserId, form);
                }
                else
                {
                    applicationId = await _applications.CreateDraftAsync(CurrentUserId, form);
                }

                await _applications.SetCoursesAsync(applicationId, CurrentUserId, form.CourseIds);

                if (form.Transcript != null)
                {
                    var path = await _storage.SaveAsync(applicationId, form.Transcript);
                    await _applications.AddDocumentAsync(applicationId, CurrentUserId,
                        Path.GetFileName(form.Transcript.FileName), path, "application/pdf", form.Transcript.Length);
                }

                await _applications.SubmitAsync(applicationId, CurrentUserId);
                Toast("Your application was submitted. An administrator will review it.");
                return RedirectToAction(nameof(Status));
            }
            catch (DbRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
                existing = await _applications.GetLatestForApplicantAsync(CurrentUserId);
            }
        }

        return View(await BuildPageAsync(form, existing));
    }

    /// <summary>A pending or approved application cannot be edited.</summary>
    private static bool IsLocked(TutorApplication? application) =>
        application?.Status is ApplicationStatus.Pending or ApplicationStatus.Approved;

    private async Task<decimal> GetMinimumGpaAsync()
    {
        var setting = await _users.GetSettingAsync("TutorMinimumGPA");
        return decimal.TryParse(setting, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 3.0m;
    }

    private async Task<TutorApplicationPage> BuildPageAsync(TutorApplicationForm form, TutorApplication? existing) => new()
    {
        Form = form,
        Existing = existing,
        Universities = await _users.GetUniversitiesAsync(),
        Courses = await _courses.ListAsync(null, includeInactive: false),
        MinimumGpa = await GetMinimumGpaAsync()
    };
}
