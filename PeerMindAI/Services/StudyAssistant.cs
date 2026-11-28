using System.Text;
using System.Text.RegularExpressions;
using PeerMindAI.Data;
using PeerMindAI.Models;

namespace PeerMindAI.Services;

/// <summary>
/// Answers a student's question from their own course materials (FR-08, FR-09, Scenario 2):
/// 1. find the most relevant pages with SourceFinder,
/// 2. if nothing matches, say so without calling the AI,
/// 3. otherwise send only those pages to the AI (Claude or Groq) with strict instructions,
/// 4. save the question, the answer and the cited pages.
/// </summary>
public partial class StudyAssistant
{
    public const int MaxQuestionsPerHour = 30;
    private const int MaxSources = 4;
    private const string NotFoundMarker = "NOT_IN_MATERIALS";

    public const string NotFoundAnswer =
        "I could not find this in your uploaded materials, so I cannot give a verified answer. " +
        "Try rephrasing the question, upload the relevant lecture notes, or ask a tutor.";

    private const string SystemPrompt =
        "You are PeerMind AI, a study assistant for university students. " +
        "Answer the student's question using ONLY the numbered excerpts from their own course materials. " +
        "Do not use outside knowledge. Cite the excerpts you used with their numbers in square brackets, " +
        "for example [1] or [2][3]. Write a clear explanation of at most about 200 words. " +
        "If the excerpts do not contain enough information to answer, reply with exactly: " + NotFoundMarker;

    private readonly AssistantRepository _assistant;
    private readonly MaterialRepository _materials;
    private readonly AiClient _ai;
    private readonly ILogger<StudyAssistant> _logger;

    public StudyAssistant(AssistantRepository assistant, MaterialRepository materials, AiClient ai,
        ILogger<StudyAssistant> logger)
    {
        _assistant = assistant;
        _materials = materials;
        _ai = ai;
        _logger = logger;
    }

    public record Reply(AiMessage Question, AiMessage Answer);

    // a citation such as [2]; some models write 【2】 or 【2†source】 instead
    [GeneratedRegex(@"[\[【](\d+)(?:†[^\]】]*)?[\]】]")]
    private static partial Regex CitationPattern();

    /// <summary>
    /// Answers the question and saves both messages. Throws DbRuleException (not allowed)
    /// or AiServiceException (AI failed); in both cases nothing is saved.
    /// </summary>
    public async Task<Reply> AskAsync(int studentId, Conversation conversation, string question)
    {
        if (await _assistant.CountRecentQuestionsAsync(studentId) >= MaxQuestionsPerHour)
        {
            throw new DbRuleException($"You can ask up to {MaxQuestionsPerHour} questions per hour. Please try again later.");
        }

        var chunks = await _materials.GetSearchableChunksAsync(studentId, conversation.CourseId);
        var sources = SourceFinder.FindRelevant(question, chunks, MaxSources);

        string answerText;
        bool grounded;
        var cited = new List<SourceChunk>();

        if (sources.Count == 0)
        {
            // nothing in the materials matches: answer honestly without spending an AI request (Scenario 2)
            answerText = NotFoundAnswer;
            grounded = false;
        }
        else
        {
            var answer = await CallAiAsync(studentId, question, sources);
            if (answer.Contains(NotFoundMarker))
            {
                answerText = NotFoundAnswer;
                grounded = false;
            }
            else
            {
                (answerText, cited) = ReplaceCitations(answer, sources);
                grounded = true;
            }
        }

        await _assistant.SetTitleIfNewAsync(conversation.ConversationId, Shorten(question, 60));
        var questionId = await _assistant.AddMessageAsync(conversation.ConversationId, studentId, "User", question, null);
        var answerId = await _assistant.AddMessageAsync(conversation.ConversationId, studentId, "Assistant", answerText, grounded);
        foreach (var chunk in cited)
        {
            await _assistant.AddSourceAsync(answerId, studentId, chunk.ChunkId);
        }

        var now = DateTime.Now;
        return new Reply(
            new AiMessage { MessageId = questionId, Sender = "User", Content = question, CreatedAt = now },
            new AiMessage
            {
                MessageId = answerId,
                Sender = "Assistant",
                Content = answerText,
                IsGrounded = grounded,
                Sources = cited.Count == 0 ? null : string.Join("; ", cited.Select(chunk => chunk.Label)),
                CreatedAt = now
            });
    }

    private async Task<string> CallAiAsync(int studentId, string question, List<SourceChunk> sources)
    {
        var prompt = new StringBuilder("Excerpts from the student's course materials:\n\n");
        for (var i = 0; i < sources.Count; i++)
        {
            prompt.Append($"[{i + 1}] ({sources[i].Label})\n{sources[i].Content}\n\n");
        }
        prompt.Append("Question: ").Append(question);

        try
        {
            var answer = await _ai.AskAsync(SystemPrompt, prompt.ToString());
            await _assistant.LogUsageAsync(studentId, _ai.Model, answer.InputTokens, answer.OutputTokens,
                answer.LatencyMs, succeeded: true, error: null);
            return answer.Text;
        }
        catch (AiServiceException ex)
        {
            _logger.LogWarning("AI request failed: {Details}", ex.TechnicalDetails);
            await _assistant.LogUsageAsync(studentId, _ai.Model, 0, 0, 0, succeeded: false, error: ex.TechnicalDetails);
            throw;
        }
    }

    /// <summary>
    /// Turns "[2]" into "[Lecture 5.pdf, p.3]" so the saved answer names its pages,
    /// and returns the excerpts that were actually cited.
    /// </summary>
    private static (string Text, List<SourceChunk> Cited) ReplaceCitations(string answer, List<SourceChunk> sources)
    {
        var cited = new List<SourceChunk>();
        var text = CitationPattern().Replace(answer, match =>
        {
            var index = int.Parse(match.Groups[1].Value) - 1;
            if (index < 0 || index >= sources.Count)
            {
                return "";   // a number that was not in the excerpts
            }
            if (!cited.Contains(sources[index]))
            {
                cited.Add(sources[index]);
            }
            return $"[{sources[index].Label}]";
        });

        // an answer without citations still came from these excerpts
        if (cited.Count == 0)
        {
            cited.AddRange(sources);
        }
        return (text.Trim(), cited);
    }

    private static string Shorten(string text, int length) =>
        text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";
}
