using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>A tutor as shown in the search results (also sent to the browser as JSON).</summary>
public class TutorCard
{
    public int TutorId { get; set; }
    public string FullName { get; set; } = "";
    public string? University { get; set; }
    public string? Headline { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? Courses { get; set; }
    public DateTime? NextAvailable { get; set; }
}

public class CourseOption
{
    public int CourseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TimeSlot
{
    public int SlotId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    /// <summary>Status of the active booking on this slot, or null when the slot is free.</summary>
    public string? BookingStatus { get; set; }
    public string? StudentName { get; set; }
}

public class Review
{
    public int ReviewId { get; set; }
    public string StudentName { get; set; } = "";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? TutorReply { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Everything the public tutor profile page shows.</summary>
public class TutorProfile
{
    public int TutorId { get; set; }
    public string FullName { get; set; } = "";
    public string? University { get; set; }
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public string? Degree { get; set; }
    public string? Major { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<CourseOption> Courses { get; set; } = new();
    public List<TimeSlot> FreeSlots { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}

/// <summary>Numbers on the tutor dashboard.</summary>
public class TutorStats
{
    public int UpcomingSessions { get; set; }
    public int PendingRequests { get; set; }
    public decimal EarnedThisMonth { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

/// <summary>The "Tutor profile" edit form.</summary>
public class TutorProfileForm
{
    [Required(ErrorMessage = "Please enter a headline.")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "The headline must have 5 to 150 characters.")]
    public string Headline { get; set; } = "";

    [StringLength(1000, ErrorMessage = "The bio can have at most 1000 characters.")]
    public string? Bio { get; set; }

    [Required(ErrorMessage = "Please enter your hourly rate.")]
    [Range(0, 500, ErrorMessage = "The hourly rate must be between 0 and 500.")]
    [Display(Name = "Hourly rate")]
    public decimal? HourlyRate { get; set; }

    [Display(Name = "Accept same-day bookings")]
    public bool AcceptSameDay { get; set; }

    /// <summary>The courses the tutor teaches; shown on the page, never posted back.</summary>
    public List<CourseOption> Courses { get; set; } = new();
}

/// <summary>The "Add availability" form on the tutor availability page.</summary>
public class SlotForm
{
    [Required(ErrorMessage = "Please choose a date.")]
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Please choose a start time.")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Enter the start time as HH:MM.")]
    [Display(Name = "Start time")]
    public string StartTime { get; set; } = "";

    [Range(30, 180, ErrorMessage = "A session lasts between 30 and 180 minutes.")]
    [Display(Name = "Length")]
    public int DurationMinutes { get; set; } = 60;
}
