using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>One quiz in the "Practice quizzes" list.</summary>
public class QuizSummary
{
    public int QuizId { get; set; }
    public string Title { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string? MaterialName { get; set; }
    public int QuestionCount { get; set; }
    public int PassMark { get; set; }
    public int Attempts { get; set; }
    public int? BestScore { get; set; }
    public int? LastAttemptId { get; set; }
    public bool IsRoadmapQuiz { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>The "Generate a quiz" form.</summary>
public class QuizGenerateForm
{
    [Required(ErrorMessage = "Please choose a material.")]
    [Display(Name = "Material")]
    public int? MaterialId { get; set; }

    [Range(3, 10, ErrorMessage = "Choose between 3 and 10 questions.")]
    [Display(Name = "Number of questions")]
    public int QuestionCount { get; set; } = 5;
}

public class QuizzesPage
{
    public QuizGenerateForm Form { get; set; } = new();
    public List<QuizSummary> Quizzes { get; set; } = new();
    public List<Material> Materials { get; set; } = new();
    public bool AiConfigured { get; set; }
    public bool AiAllowed { get; set; }
}

/// <summary>A question while the quiz is being taken (the correct answer is not included).</summary>
public class QuizQuestion
{
    public int QuestionId { get; set; }
    public int Number { get; set; }
    public string Text { get; set; } = "";
    public List<QuizOption> Options { get; set; } = new();
}

public class QuizOption
{
    public int OptionId { get; set; }
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
}

public class QuizAttemptPage
{
    public int AttemptId { get; set; }
    public string Title { get; set; } = "";
    public int PassMark { get; set; }
    public List<QuizQuestion> Questions { get; set; } = new();
}

/// <summary>Results page (sp_quiz_get_result).</summary>
public class QuizResult
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public string Title { get; set; } = "";
    public int Score { get; set; }
    public bool Passed { get; set; }
    public int PassMark { get; set; }
    public int CourseId { get; set; }
    public List<AnswerReview> Answers { get; set; } = new();
    public List<StudyRecommendation> Recommendations { get; set; } = new();
}

public class AnswerReview
{
    public int Number { get; set; }
    public string Question { get; set; } = "";
    public string YourAnswer { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
    public string? SourceFile { get; set; }
    public int? SourcePage { get; set; }
}

/// <summary>A suggestion after a quiz: review material, retry, or find a tutor (FR-11).</summary>
public class StudyRecommendation
{
    public int RecommendationId { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public int CourseId { get; set; }
    public int? AttemptId { get; set; }
    public int? QuizId { get; set; }
    public DateTime CreatedAt { get; set; }
}
