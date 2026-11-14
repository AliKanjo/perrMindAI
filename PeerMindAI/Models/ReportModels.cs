using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>Moderation outcomes (reports.status). "Open" means not decided yet.</summary>
public static class ReportDecision
{
    public const string Dismissed = "Dismissed";
    public const string WarningSent = "WarningSent";
    public const string UserSuspended = "UserSuspended";

    public static readonly string[] All = { Dismissed, WarningSent, UserSuspended };
}

/// <summary>The kinds of complaint a user can choose (reports.report_type).</summary>
public static class ReportTypes
{
    public static readonly (string Value, string Label)[] All =
    {
        ("NoShow", "Did not show up"),
        ("InappropriateMessage", "Inappropriate message"),
        ("InappropriateContent", "Inappropriate content"),
        ("Spam", "Spam"),
        ("Other", "Other")
    };

    public static bool IsValid(string? value) => All.Any(type => type.Value == value);

    public static string Label(string value) =>
        All.FirstOrDefault(type => type.Value == value).Label ?? value;
}

/// <summary>A complaint about a user and/or a review, with the admin decision once it is made.</summary>
public class Report
{
    public int ReportId { get; set; }
    public string ReporterName { get; set; } = "";
    public int? ReportedUserId { get; set; }
    public string? ReportedUserName { get; set; }
    public int? ReportedReviewId { get; set; }
    public string? ReviewComment { get; set; }
    public string ReportType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string? DecidedByName { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>The "Report a problem" form (booking page and tutor reviews page).</summary>
public class ReportForm
{
    [Required(ErrorMessage = "Please choose what the problem is.")]
    public string ReportType { get; set; } = "";

    [Required(ErrorMessage = "Please describe what happened.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "The description must have 10 to 1000 characters.")]
    public string Description { get; set; } = "";
}

/// <summary>The admin decision on one report.</summary>
public class ReportDecisionForm
{
    [Required(ErrorMessage = "Please choose a decision.")]
    public string Decision { get; set; } = "";

    [StringLength(500, ErrorMessage = "The note can have at most 500 characters.")]
    public string? Note { get; set; }
}

/// <summary>Admin "Moderation" page.</summary>
public class ModerationPage
{
    public List<Report> Open { get; set; } = new();
    public List<Report> Decided { get; set; } = new();
}
