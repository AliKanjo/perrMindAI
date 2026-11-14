using System.Data.Common;
using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>User reports and their moderation decisions (FR-13).</summary>
public class ReportRepository
{
    private const string SelectReport =
        @"SELECT r.report_id, reporter.full_name AS reporter_name,
                 r.reported_user_id, reported.full_name AS reported_user_name,
                 r.reported_review_id, review.comment AS review_comment,
                 r.report_type, r.description, r.status,
                 decider.full_name AS decided_by_name, r.decision_note, r.created_at, r.decided_at
          FROM reports r
          JOIN users reporter      ON reporter.user_id = r.reporter_id
          LEFT JOIN users reported ON reported.user_id = r.reported_user_id
          LEFT JOIN reviews review ON review.review_id = r.reported_review_id
          LEFT JOIN users decider  ON decider.user_id  = r.decided_by ";

    private readonly Database _db;

    public ReportRepository(Database db) => _db = db;

    /// <summary>Reports waiting for a decision, oldest first.</summary>
    public Task<List<Report>> ListOpenAsync() =>
        _db.QuerySqlAsync(SelectReport + "WHERE r.status = 'Open' ORDER BY r.created_at, r.report_id", MapReport);

    /// <summary>The most recently decided reports (the decision log).</summary>
    public Task<List<Report>> ListDecidedAsync() =>
        _db.QuerySqlAsync(SelectReport + "WHERE r.status <> 'Open' ORDER BY r.decided_at DESC, r.report_id DESC LIMIT 20", MapReport);

    /// <summary>Files a report about a user and, optionally, one of their reviews.</summary>
    public async Task<int> CreateAsync(int reporterId, int reportedUserId, int? reportedReviewId, ReportForm form)
    {
        var newId = Database.Out("p_new_report_id");
        await _db.ExecuteProcedureAsync("sp_report_create",
            Database.In("p_reporter_id", reporterId),
            Database.In("p_reported_user_id", reportedUserId),
            Database.In("p_reported_review_id", reportedReviewId),
            Database.In("p_report_type", form.ReportType),
            Database.In("p_description", form.Description.Trim()),
            newId);
        return Database.OutInt(newId);
    }

    /// <summary>The student who wrote a review of this tutor (null when the review is not the tutor's).</summary>
    public async Task<int?> GetReviewAuthorAsync(int reviewId, int tutorId)
    {
        var rows = await _db.QuerySqlAsync(
            "SELECT student_id FROM reviews WHERE review_id = @review_id AND tutor_id = @tutor_id",
            r => r.Int("student_id"),
            Database.In("@review_id", reviewId), Database.In("@tutor_id", tutorId));
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>
    /// Records the decision. The procedure also applies it: it suspends the user,
    /// sends a warning notification or hides the reported review.
    /// </summary>
    public Task DecideAsync(int reportId, int adminId, string decision, string? note) =>
        _db.ExecuteProcedureAsync("sp_report_decide",
            Database.In("p_report_id", reportId),
            Database.In("p_admin_id", adminId),
            Database.In("p_decision", decision),
            Database.In("p_note", note));

    private static Report MapReport(DbDataReader r) => new()
    {
        ReportId = r.Int("report_id"),
        ReporterName = r.Text("reporter_name"),
        ReportedUserId = r.IntOrNull("reported_user_id"),
        ReportedUserName = r.TextOrNull("reported_user_name"),
        ReportedReviewId = r.IntOrNull("reported_review_id"),
        ReviewComment = r.TextOrNull("review_comment"),
        ReportType = r.Text("report_type"),
        Description = r.Text("description"),
        Status = r.Text("status"),
        DecidedByName = r.TextOrNull("decided_by_name"),
        DecisionNote = r.TextOrNull("decision_note"),
        CreatedAt = r.Date("created_at"),
        DecidedAt = r.DateOrNull("decided_at")
    };
}
