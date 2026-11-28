using System.Text;
using System.Text.Json;
using PeerMindAI.Data;
using PeerMindAI.Models;

namespace PeerMindAI.Services;

/// <summary>
/// Creates a multiple-choice practice quiz from one of the student's materials (FR-10, Scenario 3):
/// 1. take the text of the material (spread over its pages),
/// 2. ask the AI for questions as JSON, labelled with the course topics and page numbers,
/// 3. check every question strictly (four different options, one correct answer, a real page),
/// 4. store the quiz with the stored procedures.
/// </summary>
public class QuizGenerator
{
    public const int MaxQuizzesPerHour = 10;
    private const int MaxSourceCharacters = 9000;
    private static readonly string[] Labels = { "A", "B", "C", "D" };

    private readonly QuizRepository _quizzes;
    private readonly MaterialRepository _materials;
    private readonly AssistantRepository _assistant;
    private readonly AiClient _ai;
    private readonly ILogger<QuizGenerator> _logger;

    public QuizGenerator(QuizRepository quizzes, MaterialRepository materials, AssistantRepository assistant, AiClient ai,
        ILogger<QuizGenerator> logger)
    {
        _quizzes = quizzes;
        _materials = materials;
        _assistant = assistant;
        _ai = ai;
        _logger = logger;
    }

    /// <summary>A question as checked from the AI's answer.</summary>
    public record GeneratedQuestion(string Text, string[] Options, int Correct, string? Explanation, int? Page, string? Topic);

    /// <summary>
    /// Generates and stores a quiz and returns its id. Throws DbRuleException (not allowed or unusable
    /// material) or AiServiceException (AI failed); nothing is stored then.
    /// </summary>
    public async Task<int> GenerateAsync(int studentId, int materialId, int questionCount)
    {
        if (await _assistant.CountRecentQuestionsAsync(studentId, "QuizGenerator") >= MaxQuizzesPerHour)
        {
            throw new DbRuleException($"You can generate up to {MaxQuizzesPerHour} quizzes per hour. Please try again later.");
        }

        var material = (await _materials.ListForStudentAsync(studentId)).FirstOrDefault(m => m.MaterialId == materialId);
        if (material == null || material.ProcessingStatus != "Processed")
        {
            throw new DbRuleException("Please choose one of your processed materials.");
        }

        var chunks = await _materials.GetChunksOfMaterialAsync(studentId, materialId);
        if (chunks.Count == 0)
        {
            throw new DbRuleException("This material has no readable text to make a quiz from.");
        }

        var topics = await _quizzes.GetCourseTopicsAsync(material.CourseId);
        var pages = chunks.Where(c => c.PageNumber != null).Select(c => c.PageNumber!.Value).ToHashSet();

        var (title, questions) = await AskAiAsync(studentId, material, chunks, topics.Select(t => t.Name).ToList(), questionCount, pages);

        // store the quiz; if anything fails half-way, remove what was stored
        var quizId = await _quizzes.CreateAsync(studentId, materialId, title);
        try
        {
            for (var i = 0; i < questions.Count; i++)
            {
                var question = questions[i];
                var topicId = topics.FirstOrDefault(t => string.Equals(t.Name, question.Topic, StringComparison.OrdinalIgnoreCase)).TopicId;
                var questionId = await _quizzes.AddQuestionAsync(quizId, studentId, i + 1, question.Text, question.Explanation,
                    topicId == 0 ? null : topicId, question.Page);
                for (var o = 0; o < 4; o++)
                {
                    await _quizzes.AddOptionAsync(questionId, Labels[o], question.Options[o], o == question.Correct);
                }
            }
        }
        catch
        {
            await _quizzes.DeleteAsync(studentId, quizId);
            throw;
        }
        return quizId;
    }

    private async Task<(string Title, List<GeneratedQuestion> Questions)> AskAiAsync(int studentId, Material material,
        List<SourceChunk> chunks, List<string> topics, int questionCount, HashSet<int> pages)
    {
        var system =
            "You write multiple-choice practice quizzes for university students. Use ONLY the excerpts from the " +
            "student's course material. Return only a JSON object with this exact shape: " +
            "{\"title\": string, \"questions\": [{\"question\": string, \"options\": [four strings], " +
            "\"correct\": index of the correct option from 0 to 3, \"explanation\": one or two sentences explaining " +
            "the correct answer, \"page\": the page number of the excerpt the question is based on, " +
            "\"topic\": one of the listed course topics, or an empty string}]}. " +
            $"Write exactly {questionCount} questions. Every question has exactly one correct answer and four different options. " +
            "Vary the position of the correct answer.";

        var prompt = new StringBuilder();
        prompt.Append("Course topics: ").Append(topics.Count == 0 ? "(none)" : string.Join(", ", topics)).Append("\n\n");
        prompt.Append($"Excerpts from \"{material.FileName}\":\n\n");
        foreach (var chunk in SelectExcerpts(chunks))
        {
            prompt.Append($"(page {chunk.PageNumber})\n{chunk.Content}\n\n");
        }
        prompt.Append($"Write {questionCount} questions.");

        AiClient.Answer answer;
        try
        {
            answer = await _ai.AskAsync(system, prompt.ToString(), maxTokens: 300 + questionCount * 250, jsonOnly: true);
            await _assistant.LogUsageAsync(studentId, _ai.Model, answer.InputTokens, answer.OutputTokens, answer.LatencyMs,
                succeeded: true, error: null, feature: "QuizGenerator");
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning("Quiz generation failed: {Details}", ex.TechnicalDetails);
            await _assistant.LogUsageAsync(studentId, _ai.Model, 0, 0, 0, succeeded: false, error: ex.TechnicalDetails,
                feature: "QuizGenerator");
            throw;
        }

        var parsed = Parse(answer.Text, pages);
        if (parsed == null || parsed.Value.Questions.Count < Math.Min(3, questionCount))
        {
            _logger.LogWarning("The AI returned an unusable quiz: {Text}", answer.Text.Length > 300 ? answer.Text[..300] : answer.Text);
            throw new AiServiceException("The AI could not create a usable quiz from this material. Please try again.",
                "Unusable quiz JSON");
        }

        var title = string.IsNullOrWhiteSpace(parsed.Value.Title) ? $"Quiz – {material.FileName}" : parsed.Value.Title;
        return (Shorten(title, 150), parsed.Value.Questions.Take(questionCount).ToList());
    }

    /// <summary>Takes excerpts spread evenly over the material until the size limit is reached.</summary>
    private static List<SourceChunk> SelectExcerpts(List<SourceChunk> chunks)
    {
        var total = chunks.Sum(c => c.Content.Length);
        var step = total <= MaxSourceCharacters ? 1 : (int)Math.Ceiling((double)total / MaxSourceCharacters);

        var selected = new List<SourceChunk>();
        var size = 0;
        for (var i = 0; i < chunks.Count && size < MaxSourceCharacters; i += step)
        {
            selected.Add(chunks[i]);
            size += chunks[i].Content.Length;
        }
        return selected;
    }

    /// <summary>Reads and checks the AI's JSON. Invalid questions are skipped; null when nothing usable came back.</summary>
    public static (string? Title, List<GeneratedQuestion> Questions)? Parse(string text, HashSet<int> pages)
    {
        // the JSON object may be surrounded by other text or a code block
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text[start..(end + 1)]);
            var root = document.RootElement;
            var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (!root.TryGetProperty("questions", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var questions = new List<GeneratedQuestion>();
            foreach (var item in list.EnumerateArray())
            {
                var question = ReadQuestion(item, pages);
                if (question != null)
                {
                    questions.Add(question);
                }
            }
            return (title, questions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GeneratedQuestion? ReadQuestion(JsonElement item, HashSet<int> pages)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("question", out var q) || q.ValueKind != JsonValueKind.String
            || !item.TryGetProperty("options", out var o) || o.ValueKind != JsonValueKind.Array
            || !item.TryGetProperty("correct", out var c) || !c.TryGetInt32(out var correct))
        {
            return null;
        }

        var text = q.GetString()!.Trim();
        var options = o.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!.Trim()).ToArray();

        // exactly four non-empty, different options and a valid correct index
        if (text.Length is < 5 or > 1000 || options.Length != 4 || options.Any(x => x.Length is 0 or > 500)
            || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || correct is < 0 or > 3)
        {
            return null;
        }

        var explanation = item.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : null;
        int? page = item.TryGetProperty("page", out var p) && p.TryGetInt32(out var pageNumber) && pages.Contains(pageNumber) ? pageNumber : null;
        var topic = item.TryGetProperty("topic", out var tp) && tp.ValueKind == JsonValueKind.String ? tp.GetString() : null;

        return new GeneratedQuestion(text, options, correct, explanation is { Length: > 0 } ? Shorten(explanation, 1000) : null, page, topic);
    }

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";
}
