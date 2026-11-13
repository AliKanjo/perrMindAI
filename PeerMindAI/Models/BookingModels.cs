using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>Booking states (bookings.status).</summary>
public static class BookingStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Declined = "Declined";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
}

/// <summary>A booking with slot, course, student and tutor (view vw_booking_details).</summary>
public class Booking
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Topic { get; set; }
    public string? Notes { get; set; }
    public decimal Price { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string CourseName { get; set; } = "";
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public int TutorId { get; set; }
    public string TutorName { get; set; } = "";
    public int? ReviewId { get; set; }
    public int? ReviewRating { get; set; }

    public bool IsActive => Status is BookingStatus.Pending or BookingStatus.Confirmed;
    public bool HasEnded => EndAt <= DateTime.Now;
}

public class BookingHistoryEntry
{
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = "";
    public string? ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>The booking form on the tutor profile page.</summary>
public class BookingForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Tutor not found.")]
    public int TutorId { get; set; }

    [Required(ErrorMessage = "Please pick a time slot.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please pick a time slot.")]
    public int? SlotId { get; set; }

    [Required(ErrorMessage = "Please choose a course.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please choose a course.")]
    public int? CourseId { get; set; }

    [StringLength(150, ErrorMessage = "The topic can have at most 150 characters.")]
    public string? Topic { get; set; }

    [StringLength(1000, ErrorMessage = "The notes can have at most 1000 characters.")]
    public string? Notes { get; set; }
}

/// <summary>Editing the topic and notes of an upcoming booking.</summary>
public class BookingNotesForm
{
    [StringLength(150, ErrorMessage = "The topic can have at most 150 characters.")]
    public string? Topic { get; set; }

    [StringLength(1000, ErrorMessage = "The notes can have at most 1000 characters.")]
    public string? Notes { get; set; }
}

public class ReviewForm
{
    [Required(ErrorMessage = "Please choose a rating.")]
    [Range(1, 5, ErrorMessage = "The rating must be between 1 and 5 stars.")]
    public int? Rating { get; set; }

    [StringLength(1000, ErrorMessage = "The comment can have at most 1000 characters.")]
    public string? Comment { get; set; }
}

public class CancelForm
{
    [Required(ErrorMessage = "Please tell the other person why you are cancelling.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "The reason must have 5 to 500 characters.")]
    public string Reason { get; set; } = "";
}

/// <summary>Model of the booking details page.</summary>
public class BookingDetailsViewModel
{
    public Booking Booking { get; set; } = new();
    public List<BookingHistoryEntry> History { get; set; } = new();
    public bool ViewerIsStudent { get; set; }
    public bool ViewerIsTutor { get; set; }
    public int CancellationWindowHours { get; set; } = 24;
}
