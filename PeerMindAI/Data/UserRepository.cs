using System.Data.Common;
using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Accounts: registration, log-in look-ups, profile and the admin user list.</summary>
public class UserRepository
{
    private readonly Database _db;

    public UserRepository(Database db) => _db = db;

    public async Task<UserAccount?> GetByEmailAsync(string email)
    {
        var rows = await _db.QueryProcedureAsync("sp_user_get_by_email", r => new UserAccount
        {
            UserId = r.Int("user_id"),
            FullName = r.Text("full_name"),
            Email = r.Text("email"),
            PasswordHash = r.Text("password_hash"),
            Role = r.Text("role"),
            Status = r.Text("status")
        }, Database.In("p_email", email));
        return rows.FirstOrDefault();
    }

    public async Task<UserProfile?> GetByIdAsync(int userId)
    {
        var rows = await _db.QueryProcedureAsync("sp_user_get_by_id", MapProfile, Database.In("p_user_id", userId));
        return rows.FirstOrDefault();
    }

    /// <summary>Registers a student account and returns the new user id.</summary>
    public async Task<int> CreateAsync(string fullName, string email, string passwordHash, int? universityId,
        string? department, int? yearOfStudy)
    {
        var newId = Database.Out("p_new_user_id");
        await _db.ExecuteProcedureAsync("sp_user_create",
            Database.In("p_full_name", fullName),
            Database.In("p_email", email),
            Database.In("p_password_hash", passwordHash),
            Database.In("p_role", Roles.Student),
            Database.In("p_university_id", universityId),
            Database.In("p_phone", null),
            Database.In("p_department", department),
            Database.In("p_year_of_study", yearOfStudy),
            Database.In("p_student_number", null),
            Database.In("p_created_by", null),
            newId);
        return Database.OutInt(newId);
    }

    public Task<List<UserListItem>> ListAsync(string? search, string? role, string? status) =>
        _db.QueryProcedureAsync("sp_user_list", r => new UserListItem
        {
            UserId = r.Int("user_id"),
            FullName = r.Text("full_name"),
            Email = r.Text("email"),
            Role = r.Text("role"),
            Status = r.Text("status"),
            University = r.TextOrNull("university"),
            CreatedAt = r.Date("created_at"),
            LastActiveAt = r.DateOrNull("last_active_at")
        },
        Database.In("p_search", search),
        Database.In("p_role", role),
        Database.In("p_status", status),
        Database.In("p_page", 1),
        Database.In("p_page_size", 200));

    public Task UpdateProfileAsync(int userId, string fullName, string? phone, int? universityId,
        string? department, int? yearOfStudy, string? studentNumber, UserPreferences preferences) =>
        _db.ExecuteProcedureAsync("sp_user_update",
            Database.In("p_user_id", userId),
            Database.In("p_full_name", fullName),
            Database.In("p_phone", phone),
            Database.In("p_avatar_url", null),
            Database.In("p_university_id", universityId),
            Database.In("p_department", department),
            Database.In("p_year_of_study", yearOfStudy),
            Database.In("p_student_number", studentNumber),
            Database.In("p_email_notifications", preferences.EmailNotifications),
            Database.In("p_study_reminders", preferences.StudyReminders),
            Database.In("p_message_alerts", preferences.MessageAlerts),
            Database.In("p_weekly_report", preferences.WeeklyReport));

    public async Task<UserPreferences> GetPreferencesAsync(int userId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT email_notifications, study_reminders, message_alerts, weekly_report
              FROM user_preferences WHERE user_id = @user_id",
            r => new UserPreferences
            {
                EmailNotifications = r.Bool("email_notifications"),
                StudyReminders = r.Bool("study_reminders"),
                MessageAlerts = r.Bool("message_alerts"),
                WeeklyReport = r.Bool("weekly_report")
            }, Database.In("@user_id", userId));
        return rows.FirstOrDefault() ?? new UserPreferences();
    }

    /// <summary>Admin: activate or suspend an account.</summary>
    public Task SetStatusAsync(int adminId, int userId, string status) =>
        _db.ExecuteProcedureAsync("sp_user_set_status",
            Database.In("p_admin_id", adminId),
            Database.In("p_user_id", userId),
            Database.In("p_status", status),
            Database.In("p_role", null),
            Database.In("p_ai_access", null));

    /// <summary>Admin: delete an account that has no activity (the procedure refuses otherwise).</summary>
    public Task DeleteAsync(int adminId, int userId) =>
        _db.ExecuteProcedureAsync("sp_user_delete",
            Database.In("p_requested_by", adminId),
            Database.In("p_user_id", userId));

    /// <summary>True when the e-mail has too many failed log-ins in the last 15 minutes.</summary>
    public async Task<bool> IsLockedOutAsync(string email)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT COUNT(*) AS recent_failures,
                     IFNULL(CAST(fn_setting('MaxFailedLoginAttempts') AS UNSIGNED), 5) AS max_allowed
              FROM login_attempts
              WHERE email = LOWER(@email) AND succeeded = 0
                AND attempted_at > NOW() - INTERVAL 15 MINUTE",
            r => r.Int("recent_failures") >= r.Int("max_allowed"),
            Database.In("@email", email));
        return rows.FirstOrDefault();
    }

    public Task RecordLoginAttemptAsync(string email, bool succeeded, string? ipAddress, string? device) =>
        _db.ExecuteProcedureAsync("sp_login_attempt_record",
            Database.In("p_email", email),
            Database.In("p_succeeded", succeeded),
            Database.In("p_ip", ipAddress),
            Database.In("p_device", device));

    // ---------- passwords ----------

    public Task SetPasswordAsync(int userId, string passwordHash) =>
        _db.ExecuteProcedureAsync("sp_user_set_password",
            Database.In("p_user_id", userId),
            Database.In("p_password_hash", passwordHash));

    /// <summary>
    /// Creates a reset link for the address. Returns the account (to send the e-mail to),
    /// or null when the address is unknown; the caller shows the same message in both cases.
    /// </summary>
    public async Task<(string FullName, string Email)?> CreatePasswordResetAsync(string email, string token)
    {
        var rows = await _db.QueryProcedureAsync("sp_password_reset_request",
            r => (r.Text("full_name"), r.Text("email")),
            Database.In("p_email", email),
            Database.In("p_token", token));
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>The e-mail of the account a valid reset link belongs to, or null.</summary>
    public async Task<string?> CheckPasswordResetAsync(string token)
    {
        var rows = await _db.QueryProcedureAsync("sp_password_reset_check",
            r => r.Text("email"), Database.In("p_token", token));
        return rows.FirstOrDefault();
    }

    /// <summary>Sets the new password; throws DbRuleException when the link is invalid or expired.</summary>
    public Task CompletePasswordResetAsync(string token, string passwordHash) =>
        _db.ExecuteProcedureAsync("sp_password_reset_complete",
            Database.In("p_token", token),
            Database.In("p_password_hash", passwordHash));

    /// <summary>Creates the token that the verification e-mail would contain.</summary>
    public async Task<string> CreateVerificationTokenAsync(int userId)
    {
        var token = Guid.NewGuid().ToString("N");
        await _db.ExecuteSqlAsync(
            @"INSERT INTO verification_tokens (user_id, token, purpose, expires_at)
              VALUES (@user_id, @token, 'EmailVerification', NOW() + INTERVAL 24 HOUR)",
            Database.In("@user_id", userId), Database.In("@token", token));
        return token;
    }

    /// <summary>The newest unused verification token of a user, or null.</summary>
    public async Task<string?> GetOpenVerificationTokenAsync(int userId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT token FROM verification_tokens
              WHERE user_id = @user_id AND purpose = 'EmailVerification'
                AND used_at IS NULL AND expires_at > NOW()
              ORDER BY created_at DESC LIMIT 1",
            r => r.Text("token"), Database.In("@user_id", userId));
        return rows.FirstOrDefault();
    }

    /// <summary>The user a valid, unused verification token belongs to (null if invalid or expired).</summary>
    public async Task<int?> GetUserIdByVerificationTokenAsync(string token)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT user_id FROM verification_tokens
              WHERE token = @token AND purpose = 'EmailVerification'
                AND used_at IS NULL AND expires_at > NOW()",
            r => r.Int("user_id"), Database.In("@token", token));
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>Marks the token as used and activates the account.</summary>
    public async Task ConfirmEmailAsync(int userId, string token)
    {
        await _db.ExecuteSqlAsync(
            "UPDATE verification_tokens SET used_at = NOW() WHERE token = @token",
            Database.In("@token", token));
        await _db.ExecuteSqlAsync(
            @"UPDATE users SET email_verified = 1, status = 'Active'
              WHERE user_id = @user_id AND status = 'PendingVerification'",
            Database.In("@user_id", userId));
    }

    public Task<List<University>> GetUniversitiesAsync() =>
        _db.QuerySqlAsync("SELECT university_id, name FROM universities ORDER BY name",
            r => new University { UniversityId = r.Int("university_id"), Name = r.Text("name") });

    /// <summary>Reads one value of the platform_settings table.</summary>
    public async Task<string?> GetSettingAsync(string key)
    {
        var rows = await _db.QuerySqlAsync("SELECT fn_setting(@key) AS setting_value",
            r => r.TextOrNull("setting_value"), Database.In("@key", key));
        return rows.FirstOrDefault();
    }

    // ---------- notifications ----------

    public Task<List<Notification>> GetNotificationsAsync(int userId) =>
        _db.QuerySqlAsync(
            @"SELECT notification_id, type, title, body, link_url, is_read, created_at
              FROM notifications WHERE user_id = @user_id
              ORDER BY created_at DESC, notification_id DESC LIMIT 50",
            r => new Notification
            {
                NotificationId = r.Int("notification_id"),
                Type = r.Text("type"),
                Title = r.Text("title"),
                Body = r.TextOrNull("body"),
                LinkUrl = r.TextOrNull("link_url"),
                IsRead = r.Bool("is_read"),
                CreatedAt = r.Date("created_at")
            }, Database.In("@user_id", userId));

    public async Task<int> CountUnreadNotificationsAsync(int userId)
    {
        var rows = await _db.QuerySqlAsync(
            "SELECT COUNT(*) AS unread FROM notifications WHERE user_id = @user_id AND is_read = 0",
            r => r.Int("unread"), Database.In("@user_id", userId));
        return rows.FirstOrDefault();
    }

    public async Task<int> MarkAllNotificationsReadAsync(int userId)
    {
        var rows = await _db.QueryProcedureAsync("sp_notification_mark_all_read",
            r => r.Int("marked_as_read"), Database.In("p_user_id", userId));
        return rows.FirstOrDefault();
    }

    private static UserProfile MapProfile(DbDataReader r) => new()
    {
        UserId = r.Int("user_id"),
        FullName = r.Text("full_name"),
        Email = r.Text("email"),
        Role = r.Text("role"),
        Status = r.Text("status"),
        Phone = r.TextOrNull("phone"),
        UniversityId = r.IntOrNull("university_id"),
        University = r.TextOrNull("university"),
        EmailVerified = r.Bool("email_verified"),
        CreatedAt = r.Date("created_at"),
        LastActiveAt = r.DateOrNull("last_active_at"),
        Department = r.TextOrNull("department"),
        YearOfStudy = r.IntOrNull("year_of_study"),
        StudentNumber = r.TextOrNull("student_number"),
        TutorHeadline = r.TextOrNull("headline"),
        TutorRating = r.DecimalOrNull("average_rating"),
        IsApprovedTutor = r.Bool("tutor_approved")
    };
}
