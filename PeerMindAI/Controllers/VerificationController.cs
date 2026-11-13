using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>Admin review of tutor applications (FR-13, Scenario 5).</summary>
[Authorize(Roles = Roles.Admin)]
public class VerificationController : BaseController
{
    private readonly ApplicationRepository _applications;
    private readonly TranscriptStorage _storage;

    public VerificationController(ApplicationRepository applications, TranscriptStorage storage)
    {
        _applications = applications;
        _storage = storage;
    }

    /// <summary>Pending applications on the left, the selected one on the right.</summary>
    [HttpGet("/admin/tutor-verification")]
    public async Task<IActionResult> Index(int? id)
    {
        var page = new VerificationPage { Pending = await _applications.ListPendingAsync() };

        var selectedId = id ?? page.Pending.FirstOrDefault()?.ApplicationId;
        if (selectedId != null)
        {
            page.Selected = await _applications.GetAsync(selectedId.Value);
            if (page.Selected == null)
            {
                return NotFound();
            }
            page.DocumentsOnDisk = page.Selected.Documents
                .Where(d => _storage.FindFile(d.StoragePath) != null)
                .Select(d => d.DocumentId)
                .ToHashSet();
        }
        return View(page);
    }

    [HttpPost("/admin/tutor-verification/{id:int}/approve")]
    public Task<IActionResult> Approve(int id) =>
        DecideAsync(id, "The application was approved. The applicant is now a tutor.",
            () => _applications.ApproveAsync(id, CurrentUserId));

    [HttpPost("/admin/tutor-verification/{id:int}/reject")]
    public Task<IActionResult> Reject(int id, DecisionForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Note))
        {
            Toast("Please give a reason for the rejection.", isError: true);
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index), new { id }));
        }
        return DecideAsync(id, "The application was rejected and the applicant was notified.",
            () => _applications.RejectAsync(id, CurrentUserId, form.Note.Trim()));
    }

    [HttpPost("/admin/tutor-verification/{id:int}/request-info")]
    public Task<IActionResult> RequestInfo(int id, DecisionForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Note))
        {
            Toast("Please write what information is missing.", isError: true);
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index), new { id }));
        }
        return DecideAsync(id, "The applicant was asked for more information.",
            () => _applications.RequestInfoAsync(id, CurrentUserId, form.Note.Trim()));
    }

    /// <summary>Downloads an uploaded transcript. Only administrators reach this action.</summary>
    [HttpGet("/admin/tutor-verification/documents/{documentId:int}")]
    public async Task<IActionResult> Document(int documentId)
    {
        var document = await _applications.GetDocumentAsync(documentId);
        var path = document == null ? null : _storage.FindFile(document.StoragePath);
        if (document == null || path == null)
        {
            return NotFound();
        }
        return PhysicalFile(path, "application/pdf", document.FileName);
    }

    private async Task<IActionResult> DecideAsync(int applicationId, string successMessage, Func<Task> decision)
    {
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Index), new { id = applicationId });
        }
        try
        {
            await decision();
            Toast(successMessage);
            return RedirectToAction(nameof(Index));
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
            return RedirectToAction(nameof(Index), new { id = applicationId });
        }
    }
}
