using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

public class LoginForm
{
    [Required(ErrorMessage = "Please enter your e-mail address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid e-mail address.")]
    [Display(Name = "University email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterForm
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Your name must have 3 to 120 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your university e-mail address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid e-mail address.")]
    [StringLength(255)]
    [Display(Name = "University email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please choose a password.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "The password must have at least 10 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$",
        ErrorMessage = "Use upper and lower case letters, a number and a symbol.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please repeat the password.")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Display(Name = "University")]
    public int? UniversityId { get; set; }

    /// <summary>True when the visitor chose "Tutor": after verification we open the tutor application.</summary>
    public bool WantsToTutor { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms and Privacy Policy.")]
    public bool AcceptTerms { get; set; }

    public List<University> Universities { get; set; } = new();
}

/// <summary>The "Profile &amp; settings" form.</summary>
public class ProfileForm
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Your name must have 3 to 120 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [StringLength(30, ErrorMessage = "The phone number can have at most 30 characters.")]
    [RegularExpression(@"^[0-9+()\- ]*$", ErrorMessage = "Use only digits, spaces and + ( ) - in the phone number.")]
    public string? Phone { get; set; }

    [Display(Name = "University")]
    public int? UniversityId { get; set; }

    [StringLength(120, ErrorMessage = "The department can have at most 120 characters.")]
    public string? Department { get; set; }

    [Range(1, 8, ErrorMessage = "The year of study must be between 1 and 8.")]
    [Display(Name = "Year of study")]
    public int? YearOfStudy { get; set; }

    [StringLength(30, ErrorMessage = "The student ID can have at most 30 characters.")]
    [Display(Name = "Student ID")]
    public string? StudentNumber { get; set; }

    public bool EmailNotifications { get; set; }
    public bool StudyReminders { get; set; }
    public bool MessageAlerts { get; set; }
    public bool WeeklyReport { get; set; }

    // shown on the page, never posted back
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public List<University> Universities { get; set; } = new();
}

/// <summary>Student dashboard.</summary>
public class StudentDashboard
{
    public string FirstName { get; set; } = "";
    public List<Booking> Upcoming { get; set; } = new();
    public List<Booking> ToReview { get; set; } = new();
    public int CompletedSessions { get; set; }
    public int PendingRequests { get; set; }
    public TutorApplication? Application { get; set; }
}

/// <summary>Tutor dashboard.</summary>
public class TutorDashboard
{
    public string FirstName { get; set; } = "";
    public TutorStats Stats { get; set; } = new();
    public List<Booking> PendingRequests { get; set; } = new();
    public List<Booking> Upcoming { get; set; } = new();
}

/// <summary>Tutor "Booking requests" page.</summary>
public class TutorRequestsPage
{
    public List<Booking> Pending { get; set; } = new();
    public List<Booking> Upcoming { get; set; } = new();
    public List<Booking> ToComplete { get; set; } = new();
    public List<Booking> Past { get; set; } = new();
}

public class AvailabilityPage
{
    public SlotForm Form { get; set; } = new();
    public List<TimeSlot> Slots { get; set; } = new();
}

public class ReviewsPage
{
    public TutorStats Stats { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}

public class TutorSearchPage
{
    public List<Course> Courses { get; set; } = new();
    public string? Search { get; set; }
    public int? CourseId { get; set; }
}

public class TutorProfilePage
{
    public TutorProfile Tutor { get; set; } = new();
    public BookingForm Form { get; set; } = new();
}

public class UserListPage
{
    public List<UserListItem> Users { get; set; } = new();
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
}

public class CourseListPage
{
    public List<Course> Courses { get; set; } = new();
    public string? Search { get; set; }
}

public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "";
}
