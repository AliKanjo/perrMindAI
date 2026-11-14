using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Admin review of user reports: dismiss, warn or suspend (FR-13).</summary>
[Authorize(Roles = Roles.Admin)]
public class ModerationController : BaseController
{
    private readonly ReportRepository _reports;

    public ModerationController(ReportRepository reports) => _reports = reports;

    [HttpGet("/admin/moderation")]
    public async Task<IActionResult> Index()
    {
        return View(new ModerationPage
        {
            Open = await _reports.ListOpenAsync(),
            Decided = await _reports.ListDecidedAsync()
        });
    }

    [HttpPost("/admin/moderation/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, ReportDecisionForm form)
    {
        if (!ReportDecision.All.Contains(form.Decision))
        {
            ModelState.AddModelError(nameof(form.Decision), "Unknown decision.");
        }
        if (!ModelState.IsValid)
        {
            Toast(ModelErrors(), isError: true);
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
            await _reports.DecideAsync(id, CurrentUserId, form.Decision, note);
            Toast(form.Decision switch
            {
                ReportDecision.UserSuspended => "Decision recorded: the user was suspended.",
                ReportDecision.WarningSent => "Decision recorded: a warning was sent.",
                _ => "The report was dismissed."
            });
        }
        catch (DbRuleException ex)
        {
            // e.g. another administrator already decided this report
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Index));
    }
}
