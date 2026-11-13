using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>A course of the course library (admin CRUD entity).</summary>
public class Course
{
    public int CourseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Department { get; set; }
    public string? Professor { get; set; }
    public bool IsActive { get; set; }
    public int Students { get; set; }
    public int Materials { get; set; }
    public int Tutors { get; set; }
    public string? Topics { get; set; }
}

/// <summary>The "Add course" / "Edit course" form.</summary>
public class CourseForm
{
    public int? CourseId { get; set; }

    [Required(ErrorMessage = "Please enter the course code.")]
    [StringLength(20, ErrorMessage = "The course code can have at most 20 characters.")]
    [RegularExpression(@"^[A-Za-z0-9\- ]+$", ErrorMessage = "Use only letters, numbers and dashes in the course code.")]
    [Display(Name = "Course code")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Please enter the course name.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "The course name must have 3 to 150 characters.")]
    [Display(Name = "Course name")]
    public string Name { get; set; } = "";

    [StringLength(120, ErrorMessage = "The department can have at most 120 characters.")]
    public string? Department { get; set; }

    [StringLength(120, ErrorMessage = "The professor name can have at most 120 characters.")]
    public string? Professor { get; set; }

    [Display(Name = "Active (visible to students)")]
    public bool IsActive { get; set; } = true;
}
