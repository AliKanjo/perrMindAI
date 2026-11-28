using System.Data.Common;
using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Practice quizzes: storing generated quizzes, taking them, results and recommendations (FR-10, FR-11).</summary>
public class QuizRepository
{
    private readonly Database _db;

    public QuizRepository(Database db) => _db = db;

    public Task<List<QuizSummary>> ListForStudentAsync(int studentId) =>
        _db.QuerySqlAsync(
            @"SELECT q.quiz_id, q.title, c.name AS course_name, m.file_name AS material_name, q.pass_mark_percent,
                     q.section_id, q.created_at,
                     (SELECT COUNT(*) FROM quiz_questions qq WHERE qq.quiz_id = q.quiz_id) AS question_count,
                     (SELECT COUNT(*) FROM quiz_attempts a WHERE a.quiz_id = q.quiz_id AND a.submitted_at IS NOT NULL) AS attempts,
                     (SELECT MAX(a.score_percent) FROM quiz_attempts a WHERE a.quiz_id = q.quiz_id AND a.submitted_at IS NOT NULL) AS best_score,
                     (SELECT a.attempt_id FROM quiz_attempts a WHERE a.quiz_id = q.quiz_id AND a.submitted_at IS NOT NULL
                       ORDER BY a.submitted_at DESC, a.attempt_id DESC LIMIT 1) AS last_attempt_id
              FROM quizzes q
              JOIN courses c ON c.course_id = q.course_id
              LEFT JOIN course_materials m ON m.material_id = q.material_id
              WHERE q.student_id = @student_id
              ORDER BY q.created_at DESC, q.quiz_id DESC",
            r => new QuizSummary
            {
                QuizId = r.Int("quiz_id"),
                Title = r.Text("title"),
                CourseName = r.Text("course_name"),
                MaterialName = r.TextOrNull("material_name"),
                PassMark = r.Int("pass_mark_percent"),
                IsRoadmapQuiz = r.IntOrNull("section_id") != null,
                CreatedAt = r.Date("created_at"),
                QuestionCount = r.Int("question_count"),
                Attempts = r.Int("attempts"),
                BestScore = r.IntOrNull("best_score"),
                LastAttemptId = r.IntOrNull("last_attempt_id")
            }, Database.In("@student_id", studentId));

    /// <summary>The topic names of a course, so the AI can label its questions with them.</summary>
    public Task<List<(int TopicId, string Name)>> GetCourseTopicsAsync(int courseId) =>
        _db.QuerySqlAsync("SELECT topic_id, name FROM topics WHERE course_id = @course_id ORDER BY name",
            r => (r.Int("topic_id"), r.Text("name")), Database.In("@course_id", courseId));

    // ---------- storing a generated quiz ----------

    public async Task<int> CreateAsync(int studentId, int materialId, string title)
    {
        var newId = Database.Out("p_new_quiz_id");
        await _db.ExecuteProcedureAsync("sp_quiz_create",
            Database.In("p_student_id", studentId),
            Database.In("p_material_id", materialId),
            Database.In("p_title", title),
            newId);
        return Database.OutInt(newId);
    }

    public async Task<int> AddQuestionAsync(int quizId, int studentId, int number, string text, string? explanation,
        int? topicId, int? page)
    {
        var newId = Database.Out("p_new_question_id");
        await _db.ExecuteProcedureAsync("sp_quiz_add_question",
            Database.In("p_quiz_id", quizId),
            Database.In("p_student_id", studentId),
            Database.In("p_question_number", number),
            Database.In("p_question_text", text),
            Database.In("p_explanation", explanation),
            Database.In("p_topic_id", topicId),
            Database.In("p_source_page", page),
            newId);
        return Database.OutInt(newId);
    }

    public Task AddOptionAsync(int questionId, string label, string text, bool isCorrect) =>
        _db.ExecuteProcedureAsync("sp_quiz_add_option",
            Database.In("p_question_id", questionId),
            Database.In("p_label", label),
            Database.In("p_option_text", text),
            Database.In("p_is_correct", isCorrect));

    public Task DeleteAsync(int studentId, int quizId) =>
        _db.ExecuteProcedureAsync("sp_quiz_delete",
            Database.In("p_student_id", studentId),
            Database.In("p_quiz_id", quizId));

    // ---------- taking a quiz ----------

    /// <summary>Starts a new attempt and returns its id (sp_quiz_start checks the quiz is the student's).</summary>
    public async Task<int> StartAttemptAsync(int quizId, int studentId)
    {
        var attemptId = Database.Out("p_attempt_id");
        await _db.ExecuteProcedureAsync("sp_quiz_start",
            Database.In("p_quiz_id", quizId), Database.In("p_student_id", studentId), attemptId);
        return Database.OutInt(attemptId);
    }

    /// <summary>An open attempt of the student with its questions and options (null if not found or already submitted).</summary>
    public async Task<QuizAttemptPage?> GetOpenAttemptAsync(int attemptId, int studentId)
    {
        var header = await _db.QuerySqlAsync(
            @"SELECT a.attempt_id, q.title, q.pass_mark_percent
              FROM quiz_attempts a JOIN quizzes q ON q.quiz_id = a.quiz_id
              WHERE a.attempt_id = @attempt_id AND a.student_id = @student_id AND a.submitted_at IS NULL",
            r => new QuizAttemptPage { AttemptId = r.Int("attempt_id"), Title = r.Text("title"), PassMark = r.Int("pass_mark_percent") },
            Database.In("@attempt_id", attemptId), Database.In("@student_id", studentId));
        var page = header.FirstOrDefault();
        if (page == null)
        {
            return null;
        }

        var rows = await _db.QuerySqlAsync(
            @"SELECT qq.question_id, qq.question_number, qq.question_text, o.option_id, o.label, o.option_text
              FROM quiz_attempts a
              JOIN quiz_questions qq ON qq.quiz_id = a.quiz_id
              JOIN quiz_options o    ON o.question_id = qq.question_id
              WHERE a.attempt_id = @attempt_id
              ORDER BY qq.question_number, o.label",
            r => (QuestionId: r.Int("question_id"), Number: r.Int("question_number"), Text: r.Text("question_text"),
                  Option: new QuizOption { OptionId = r.Int("option_id"), Label = r.Text("label"), Text = r.Text("option_text") }),
            Database.In("@attempt_id", attemptId));

        page.Questions = rows.GroupBy(row => (row.QuestionId, row.Number, row.Text))
            .Select(group => new QuizQuestion
            {
                QuestionId = group.Key.QuestionId,
                Number = group.Key.Number,
                Text = group.Key.Text,
                Options = group.Select(row => row.Option).ToList()
            }).ToList();
        return page;
    }

    public Task SaveAnswerAsync(int attemptId, int studentId, int questionId, int optionId) =>
        _db.ExecuteProcedureAsync("sp_quiz_answer",
            Database.In("p_attempt_id", attemptId),
            Database.In("p_student_id", studentId),
            Database.In("p_question_id", questionId),
            Database.In("p_option_id", optionId));

    /// <summary>Scores the attempt; the procedure also creates the study recommendations.</summary>
    public Task SubmitAsync(int attemptId, int studentId) =>
        _db.ExecuteProcedureAsync("sp_quiz_submit",
            Database.In("p_attempt_id", attemptId), Database.In("p_student_id", studentId));

    // ---------- results ----------

    public Task<QuizResult?> GetResultAsync(int attemptId, int studentId) =>
        _db.ReadProcedureAsync<QuizResult?>("sp_quiz_get_result", async reader =>
        {
            if (!await reader.ReadAsync())
            {
                return null;
            }
            var result = new QuizResult
            {
                AttemptId = reader.Int("attempt_id"),
                Title = reader.Text("title"),
                Score = reader.Int("score_percent"),
                Passed = reader.Bool("passed"),
                PassMark = reader.Int("pass_mark_percent")
            };

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                result.Answers.Add(new AnswerReview
                {
                    Number = reader.Int("question_number"),
                    Question = reader.Text("question_text"),
                    YourAnswer = $"{reader.Text("your_answer")}. {reader.Text("your_answer_text")}",
                    CorrectAnswer = $"{reader.Text("correct_answer")}. {reader.Text("correct_answer_text")}",
                    IsCorrect = reader.Bool("is_correct"),
                    Explanation = reader.TextOrNull("explanation"),
                    SourceFile = reader.TextOrNull("source_file"),
                    SourcePage = reader.IntOrNull("source_page")
                });
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                result.Recommendations.Add(new StudyRecommendation
                {
                    RecommendationId = reader.Int("recommendation_id"),
                    Type = reader.Text("recommendation_type"),
                    Message = reader.Text("message")
                });
            }
            return result;
        }, Database.In("p_attempt_id", attemptId), Database.In("p_student_id", studentId));

    /// <summary>Quiz and course of an attempt (to link "retry" and "find a tutor").</summary>
    public async Task<(int QuizId, int CourseId)?> GetAttemptQuizAsync(int attemptId, int studentId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT q.quiz_id, q.course_id FROM quiz_attempts a JOIN quizzes q ON q.quiz_id = a.quiz_id
              WHERE a.attempt_id = @attempt_id AND a.student_id = @student_id",
            r => (r.Int("quiz_id"), r.Int("course_id")),
            Database.In("@attempt_id", attemptId), Database.In("@student_id", studentId));
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>The newest recommendations that were not dismissed, for the dashboard (FR-11).</summary>
    public Task<List<StudyRecommendation>> GetOpenRecommendationsAsync(int studentId, int limit = 5) =>
        _db.QuerySqlAsync(
            @"SELECT r.recommendation_id, r.recommendation_type, r.message, r.course_id, r.attempt_id, a.quiz_id, r.created_at
              FROM study_recommendations r LEFT JOIN quiz_attempts a ON a.attempt_id = r.attempt_id
              WHERE r.student_id = @student_id AND r.is_dismissed = 0
              ORDER BY r.created_at DESC, r.recommendation_id DESC
              LIMIT @limit",
            MapRecommendation, Database.In("@student_id", studentId), Database.In("@limit", limit));

    public Task<int> DismissRecommendationAsync(int studentId, int recommendationId) =>
        _db.ExecuteSqlAsync(
            "UPDATE study_recommendations SET is_dismissed = 1 WHERE recommendation_id = @id AND student_id = @student_id",
            Database.In("@id", recommendationId), Database.In("@student_id", studentId));

    private static StudyRecommendation MapRecommendation(DbDataReader r) => new()
    {
        RecommendationId = r.Int("recommendation_id"),
        Type = r.Text("recommendation_type"),
        Message = r.Text("message"),
        CourseId = r.Int("course_id"),
        AttemptId = r.IntOrNull("attempt_id"),
        QuizId = r.IntOrNull("quiz_id"),
        CreatedAt = r.Date("created_at")
    };
}
