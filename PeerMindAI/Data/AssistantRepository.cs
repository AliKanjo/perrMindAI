using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>AI chats, their messages and cited sources, and the AI usage log (FR-08, FR-09).</summary>
public class AssistantRepository
{
    private readonly Database _db;

    public AssistantRepository(Database db) => _db = db;

    public Task<List<Conversation>> ListConversationsAsync(int studentId) =>
        _db.QuerySqlAsync(
            @"SELECT a.conversation_id, a.title, a.course_id, c.name AS course_name, a.created_at
              FROM ai_conversations a LEFT JOIN courses c ON c.course_id = a.course_id
              WHERE a.student_id = @student_id
              ORDER BY a.created_at DESC, a.conversation_id DESC",
            r => new Conversation
            {
                ConversationId = r.Int("conversation_id"),
                Title = r.Text("title"),
                CourseId = r.IntOrNull("course_id"),
                CourseName = r.TextOrNull("course_name"),
                CreatedAt = r.Date("created_at")
            }, Database.In("@student_id", studentId));

    public async Task<int> CreateConversationAsync(int studentId, int? courseId)
    {
        var newId = Database.Out("p_new_conversation_id");
        await _db.ExecuteProcedureAsync("sp_ai_conversation_create",
            Database.In("p_student_id", studentId),
            Database.In("p_course_id", courseId),
            newId);
        return Database.OutInt(newId);
    }

    /// <summary>Renames a chat that is still called "New chat" after its first question.</summary>
    public Task SetTitleIfNewAsync(int conversationId, string title) =>
        _db.ExecuteSqlAsync(
            "UPDATE ai_conversations SET title = @title WHERE conversation_id = @id AND title = 'New chat'",
            Database.In("@title", title), Database.In("@id", conversationId));

    public Task<int> DeleteConversationAsync(int studentId, int conversationId) =>
        _db.ExecuteSqlAsync(
            "DELETE FROM ai_conversations WHERE conversation_id = @id AND student_id = @student_id",   // messages cascade
            Database.In("@id", conversationId), Database.In("@student_id", studentId));

    /// <summary>The messages of a chat; the procedure returns nothing for someone else's chat.</summary>
    public Task<List<AiMessage>> GetMessagesAsync(int conversationId, int studentId) =>
        _db.QueryProcedureAsync("sp_ai_conversation_get", r => new AiMessage
        {
            MessageId = r.Int("message_id"),
            Sender = r.Text("sender"),
            Content = r.Text("content"),
            IsGrounded = r.IsDBNull(r.GetOrdinal("is_grounded")) ? null : r.Bool("is_grounded"),
            Sources = r.TextOrNull("sources"),
            CreatedAt = r.Date("created_at")
        },
        Database.In("p_conversation_id", conversationId),
        Database.In("p_student_id", studentId));

    /// <summary>Saves a message; the procedure checks ownership and the user's AI access.</summary>
    public async Task<int> AddMessageAsync(int conversationId, int studentId, string sender, string content, bool? isGrounded)
    {
        var newId = Database.Out("p_new_message_id");
        await _db.ExecuteProcedureAsync("sp_ai_message_add",
            Database.In("p_conversation_id", conversationId),
            Database.In("p_student_id", studentId),
            Database.In("p_sender", sender),
            Database.In("p_content", content),
            Database.In("p_is_grounded", isGrounded),
            newId);
        return Database.OutInt(newId);
    }

    /// <summary>Links an answer to a page it was based on (only the student's own materials, FR-09).</summary>
    public Task AddSourceAsync(int messageId, int studentId, int chunkId) =>
        _db.ExecuteProcedureAsync("sp_ai_message_add_source",
            Database.In("p_message_id", messageId),
            Database.In("p_student_id", studentId),
            Database.In("p_chunk_id", chunkId));

    /// <summary>True when the platform setting and the user's own AI access both allow the assistant.</summary>
    public async Task<bool> IsAiAllowedAsync(int userId)
    {
        // the setting is compared in C#: comparing text in SQL can fail when the
        // database and the connection use different collations
        var rows = await _db.QuerySqlAsync(
            "SELECT u.ai_access_enabled, fn_setting('EnableAiStudyAssistant') AS setting FROM users u WHERE u.user_id = @user_id",
            r => r.Bool("ai_access_enabled") && !string.Equals(r.TextOrNull("setting"), "false", StringComparison.OrdinalIgnoreCase),
            Database.In("@user_id", userId));
        return rows.FirstOrDefault();
    }

    /// <summary>How many questions the user asked in the last hour (to limit the cost).</summary>
    public async Task<int> CountRecentQuestionsAsync(int userId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT COUNT(*) AS recent FROM ai_usage_logs
              WHERE user_id = @user_id AND feature = 'StudyAssistant' AND created_at > NOW() - INTERVAL 1 HOUR",
            r => r.Int("recent"), Database.In("@user_id", userId));
        return rows.FirstOrDefault();
    }

    /// <summary>Records one call to the AI service (also failed ones) for the usage log.</summary>
    public Task LogUsageAsync(int userId, string model, int inputTokens, int outputTokens, int latencyMs, bool succeeded, string? error) =>
        _db.ExecuteSqlAsync(
            @"INSERT INTO ai_usage_logs (user_id, feature, model, input_tokens, output_tokens, latency_ms, succeeded, error_message)
              VALUES (@user_id, 'StudyAssistant', @model, @input_tokens, @output_tokens, @latency_ms, @succeeded, @error)",
            Database.In("@user_id", userId),
            Database.In("@model", model),
            Database.In("@input_tokens", inputTokens),
            Database.In("@output_tokens", outputTokens),
            Database.In("@latency_ms", latencyMs),
            Database.In("@succeeded", succeeded),
            Database.In("@error", error is { Length: > 500 } ? error[..500] : error));
}
