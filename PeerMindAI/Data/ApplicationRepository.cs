using System.Data.Common;
using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Tutor applications (applicant side and admin review) and the admin dashboard numbers.</summary>
public class ApplicationRepository
{
    private const string SelectApplication =
        @"SELECT a.application_id, a.applicant_id, u.full_name, u.email, a.university_id, un.name AS university,
                 a.degree, a.major, a.gpa, a.year_of_study, a.bio, a.status, a.reviewer_note,
                 a.submitted_at, a.reviewed_at
          FROM tutor_applications a
          JOIN users u ON u.user_id = a.applicant_id
          LEFT JOIN universities un ON un.university_id = a.university_id ";

    private readonly Database _db;

    public ApplicationRepository(Database db) => _db = db;

    // ---------- reading ----------

    /// <summary>The newest application of a user (null when the user never applied).</summary>
    public async Task<TutorApplication?> GetLatestForApplicantAsync(int applicantId)
    {
        var rows = await _db.QuerySqlAsync(
            SelectApplication + "WHERE a.applicant_id = @applicant_id ORDER BY a.application_id DESC LIMIT 1",
            MapApplication, Database.In("@applicant_id", applicantId));
        return await WithDetailsAsync(rows.FirstOrDefault());
    }

    public async Task<TutorApplication?> GetAsync(int applicationId)
    {
        var rows = await _db.QuerySqlAsync(
            SelectApplication + "WHERE a.application_id = @application_id",
            MapApplication, Database.In("@application_id", applicationId));
        return await WithDetailsAsync(rows.FirstOrDefault());
    }

    /// <summary>Applications waiting for an admin decision, oldest first.</summary>
    public async Task<List<TutorApplication>> ListPendingAsync()
    {
        var applications = await _db.QuerySqlAsync(
            SelectApplication + "WHERE a.status IN ('Pending','InfoRequested') ORDER BY a.submitted_at",
            MapApplication);
        foreach (var application in applications)
        {
            application.Courses = await GetCoursesAsync(application.ApplicationId);
        }
        return applications;
    }

    public async Task<ApplicationDocument?> GetDocumentAsync(int documentId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT document_id, application_id, document_type, file_name, storage_path, content_type, file_size_bytes
              FROM application_documents WHERE document_id = @document_id",
            MapDocument, Database.In("@document_id", documentId));
        return rows.FirstOrDefault();
    }

    // ---------- applicant ----------

    public async Task<int> CreateDraftAsync(int applicantId, TutorApplicationForm form)
    {
        var newId = Database.Out("p_new_application_id");
        await _db.ExecuteProcedureAsync("sp_application_create",
            Database.In("p_applicant_id", applicantId),
            Database.In("p_university_id", form.UniversityId),
            Database.In("p_degree", form.Degree),
            Database.In("p_major", form.Major.Trim()),
            Database.In("p_gpa", form.Gpa),
            Database.In("p_year_of_study", form.YearOfStudy),
            Database.In("p_bio", form.Bio.Trim()),
            newId);
        return Database.OutInt(newId);
    }

    /// <summary>Overwrites the academic details of an application that is still editable.</summary>
    public Task UpdateDraftAsync(int applicationId, int applicantId, TutorApplicationForm form) =>
        _db.ExecuteSqlAsync(
            @"UPDATE tutor_applications
                 SET university_id = @university_id, degree = @degree, major = @major,
                     gpa = @gpa, year_of_study = @year_of_study, bio = @bio
               WHERE application_id = @application_id AND applicant_id = @applicant_id
                 AND status IN ('Draft','InfoRequested')",
            Database.In("@university_id", form.UniversityId),
            Database.In("@degree", form.Degree),
            Database.In("@major", form.Major.Trim()),
            Database.In("@gpa", form.Gpa),
            Database.In("@year_of_study", form.YearOfStudy),
            Database.In("@bio", form.Bio.Trim()),
            Database.In("@application_id", applicationId),
            Database.In("@applicant_id", applicantId));

    /// <summary>Replaces the list of courses the applicant wants to teach.</summary>
    public async Task SetCoursesAsync(int applicationId, int applicantId, IEnumerable<int> courseIds)
    {
        await _db.ExecuteSqlAsync(
            @"DELETE ac FROM application_courses ac
              JOIN tutor_applications a ON a.application_id = ac.application_id
              WHERE ac.application_id = @application_id AND a.applicant_id = @applicant_id
                AND a.status IN ('Draft','InfoRequested')",
            Database.In("@application_id", applicationId),
            Database.In("@applicant_id", applicantId));

        foreach (var courseId in courseIds.Distinct())
        {
            await _db.ExecuteProcedureAsync("sp_application_add_course",
                Database.In("p_application_id", applicationId),
                Database.In("p_applicant_id", applicantId),
                Database.In("p_course_id", courseId),
                Database.In("p_grade", null));
        }
    }

    public Task AddDocumentAsync(int applicationId, int applicantId, string fileName, string storagePath,
        string contentType, long sizeBytes) =>
        _db.ExecuteProcedureAsync("sp_application_add_document",
            Database.In("p_application_id", applicationId),
            Database.In("p_applicant_id", applicantId),
            Database.In("p_document_type", "Transcript"),
            Database.In("p_file_name", fileName),
            Database.In("p_storage_path", storagePath),
            Database.In("p_content_type", contentType),
            Database.In("p_file_size_bytes", sizeBytes));

    /// <summary>Draft -> Pending. The procedure checks GPA, year, courses and transcript again.</summary>
    public Task SubmitAsync(int applicationId, int applicantId) =>
        _db.ExecuteProcedureAsync("sp_application_submit",
            Database.In("p_application_id", applicationId),
            Database.In("p_applicant_id", applicantId));

    // ---------- admin decisions ----------

    public Task ApproveAsync(int applicationId, int adminId) =>
        _db.ExecuteProcedureAsync("sp_application_approve",
            Database.In("p_application_id", applicationId),
            Database.In("p_admin_id", adminId));

    public Task RejectAsync(int applicationId, int adminId, string? reason) =>
        _db.ExecuteProcedureAsync("sp_application_reject",
            Database.In("p_application_id", applicationId),
            Database.In("p_admin_id", adminId),
            Database.In("p_reason", reason));

    public Task RequestInfoAsync(int applicationId, int adminId, string note) =>
        _db.ExecuteProcedureAsync("sp_application_request_info",
            Database.In("p_application_id", applicationId),
            Database.In("p_admin_id", adminId),
            Database.In("p_note", note));

    // ---------- admin dashboard ----------

    public async Task<AdminDashboard> GetDashboardAsync()
    {
        var rows = await _db.QuerySqlAsync("SELECT * FROM vw_admin_dashboard", r => new AdminDashboard
        {
            TotalUsers = r.Int("total_users"),
            Students = r.Int("students"),
            ApprovedTutors = r.Int("approved_tutors"),
            PendingApplications = r.Int("pending_tutor_applications"),
            SessionsThisMonth = r.Int("sessions_this_month"),
            OpenReports = r.Int("open_reports")
        });
        var dashboard = rows.FirstOrDefault() ?? new AdminDashboard();
        dashboard.Pending = await ListPendingAsync();
        dashboard.RecentActivity = await _db.QuerySqlAsync(
            @"SELECT l.action, l.details, u.full_name AS admin_name, l.created_at
              FROM audit_logs l LEFT JOIN users u ON u.user_id = l.admin_id
              ORDER BY l.created_at DESC, l.audit_id DESC LIMIT 8",
            r => new AuditEntry
            {
                Action = r.Text("action"),
                Details = r.TextOrNull("details"),
                AdminName = r.TextOrNull("admin_name"),
                CreatedAt = r.Date("created_at")
            });
        return dashboard;
    }

    // ---------- helpers ----------

    private async Task<TutorApplication?> WithDetailsAsync(TutorApplication? application)
    {
        if (application == null)
        {
            return null;
        }
        application.Courses = await GetCoursesAsync(application.ApplicationId);
        application.Documents = await _db.QuerySqlAsync(
            @"SELECT document_id, application_id, document_type, file_name, storage_path, content_type, file_size_bytes
              FROM application_documents WHERE application_id = @application_id ORDER BY uploaded_at DESC, document_id DESC",
            MapDocument, Database.In("@application_id", application.ApplicationId));
        return application;
    }

    private Task<List<CourseOption>> GetCoursesAsync(int applicationId) =>
        _db.QuerySqlAsync(
            @"SELECT c.course_id, c.code, c.name
              FROM application_courses ac JOIN courses c ON c.course_id = ac.course_id
              WHERE ac.application_id = @application_id ORDER BY c.name",
            r => new CourseOption { CourseId = r.Int("course_id"), Code = r.Text("code"), Name = r.Text("name") },
            Database.In("@application_id", applicationId));

    private static TutorApplication MapApplication(DbDataReader r) => new()
    {
        ApplicationId = r.Int("application_id"),
        ApplicantId = r.Int("applicant_id"),
        ApplicantName = r.Text("full_name"),
        ApplicantEmail = r.Text("email"),
        UniversityId = r.IntOrNull("university_id"),
        University = r.TextOrNull("university"),
        Degree = r.TextOrNull("degree"),
        Major = r.TextOrNull("major"),
        Gpa = r.DecimalOrNull("gpa"),
        YearOfStudy = r.IntOrNull("year_of_study"),
        Bio = r.TextOrNull("bio"),
        Status = r.Text("status"),
        ReviewerNote = r.TextOrNull("reviewer_note"),
        SubmittedAt = r.DateOrNull("submitted_at"),
        ReviewedAt = r.DateOrNull("reviewed_at")
    };

    private static ApplicationDocument MapDocument(DbDataReader r) => new()
    {
        DocumentId = r.Int("document_id"),
        ApplicationId = r.Int("application_id"),
        DocumentType = r.Text("document_type"),
        FileName = r.Text("file_name"),
        StoragePath = r.Text("storage_path"),
        ContentType = r.Text("content_type"),
        FileSizeBytes = Convert.ToInt64(r["file_size_bytes"])
    };
}
