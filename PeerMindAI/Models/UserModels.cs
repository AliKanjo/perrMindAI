namespace PeerMindAI.Models;

/// <summary>The three roles of the platform (users.role).</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Tutor = "Tutor";
    public const string Admin = "Admin";
}

/// <summary>Account states (users.status).</summary>
public static class UserStatus
{
    public const string PendingVerification = "PendingVerification";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
}

/// <summary>The columns needed to check a log-in.</summary>
public class UserAccount
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
}

/// <summary>A user with the profile information joined in (sp_user_get_by_id).</summary>
public class UserProfile
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Phone { get; set; }
    public int? UniversityId { get; set; }
    public string? University { get; set; }
    public bool EmailVerified { get; set; }
    public bool AiAccessEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public string? Department { get; set; }
    public int? YearOfStudy { get; set; }
    public string? StudentNumber { get; set; }
    public string? TutorHeadline { get; set; }
    public decimal? TutorRating { get; set; }
    public bool IsApprovedTutor { get; set; }
}

/// <summary>One row of the admin "All users" table.</summary>
public class UserListItem
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string? University { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public class University
{
    public int UniversityId { get; set; }
    public string Name { get; set; } = "";
}

public class UserPreferences
{
    public bool EmailNotifications { get; set; } = true;
    public bool StudyReminders { get; set; } = true;
    public bool MessageAlerts { get; set; } = true;
    public bool WeeklyReport { get; set; }
}

public class Notification
{
    public int NotificationId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
