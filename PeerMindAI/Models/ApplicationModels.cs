using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>Tutor application states (tutor_applications.status).</summary>
public static class ApplicationStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string InfoRequested = "InfoRequested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public class ApplicationDocument
{
    public int DocumentId { get; set; }
    public int ApplicationId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSizeBytes { get; set; }
}

/// <summary>A tutor application with applicant, courses and uploaded documents.</summary>
public class TutorApplication
{
    public int ApplicationId { get; set; }
    public int ApplicantId { get; set; }
    public string ApplicantName { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";
    public int? UniversityId { get; set; }
    public string? University { get; set; }
    public string? Degree { get; set; }
    public string? Major { get; set; }
    public decimal? Gpa { get; set; }
    public int? YearOfStudy { get; set; }
    public string? Bio { get; set; }
    public string Status { get; set; } = "";
    public string? ReviewerNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<CourseOption> Courses { get; set; } = new();
    public List<ApplicationDocument> Documents { get; set; } = new();

    public string CourseNames => string.Join(", ", Courses.Select(c => c.Name));

    /// <summary>The applicant can still edit and (re)submit it.</summary>
    public bool IsEditable => Status is ApplicationStatus.Draft or ApplicationStatus.InfoRequested;
}

/// <summary>The "Become a tutor" form.</summary>
public class TutorApplicationForm
{
    [Required(ErrorMessage = "Please choose your university.")]
    [Display(Name = "University")]
    public int? UniversityId { get; set; }

    [Required(ErrorMessage = "Please choose your degree.")]
    [RegularExpression("^(BSc|MSc|PhD)$", ErrorMessage = "Please choose BSc, MSc or PhD.")]
    public string Degree { get; set; } = "";

    [Required(ErrorMessage = "Please enter your major.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "The major must have 2 to 120 characters.")]
    public string Major { get; set; } = "";

    [Required(ErrorMessage = "Please enter your GPA.")]
    [Range(0, 4, ErrorMessage = "The GPA must be between 0.00 and 4.00.")]
    [Display(Name = "GPA")]
    public decimal? Gpa { get; set; }

    [Required(ErrorMessage = "Please choose your year of study.")]
    [Range(1, 8, ErrorMessage = "The year of study must be between 1 and 8.")]
    [Display(Name = "Year of study")]
    public int? YearOfStudy { get; set; }

    [Required(ErrorMessage = "Please write a short bio.")]
    [StringLength(1000, MinimumLength = 20, ErrorMessage = "The bio must have 20 to 1000 characters.")]
    [Display(Name = "Short bio")]
    public string Bio { get; set; } = "";

    [Display(Name = "Courses you can teach")]
    public List<int> CourseIds { get; set; } = new();

    [Display(Name = "Official transcript")]
    public IFormFile? Transcript { get; set; }
}

/// <summary>Model of the application page: the form plus what it needs to render.</summary>
public class TutorApplicationPage
{
    public TutorApplicationForm Form { get; set; } = new();
    public List<University> Universities { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public TutorApplication? Existing { get; set; }
    public decimal MinimumGpa { get; set; } = 3.0m;
}

/// <summary>Admin page: the list on the left and the selected application on the right.</summary>
public class VerificationPage
{
    public List<TutorApplication> Pending { get; set; } = new();
    public TutorApplication? Selected { get; set; }
    public HashSet<int> DocumentsOnDisk { get; set; } = new();
}

public class DecisionForm
{
    [StringLength(1000, ErrorMessage = "The note can have at most 1000 characters.")]
    public string? Note { get; set; }
}

/// <summary>Numbers of the admin dashboard (view vw_admin_dashboard).</summary>
public class AdminDashboard
{
    public int TotalUsers { get; set; }
    public int Students { get; set; }
    public int ApprovedTutors { get; set; }
    public int PendingApplications { get; set; }
    public int SessionsThisMonth { get; set; }
    public int OpenReports { get; set; }
    public List<TutorApplication> Pending { get; set; } = new();
    public List<AuditEntry> RecentActivity { get; set; } = new();
}

public class AuditEntry
{
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public string? AdminName { get; set; }
    public DateTime CreatedAt { get; set; }
}
