using System.ComponentModel.DataAnnotations;

namespace PeerMindAI.Models;

/// <summary>A PDF a student uploaded (course_materials).</summary>
public class Material
{
    public int MaterialId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public int? PageCount { get; set; }
    public string ProcessingStatus { get; set; } = "";
    public bool UseAsAiSource { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>The "Upload material" form.</summary>
public class MaterialUploadForm
{
    [Required(ErrorMessage = "Please choose the course this material belongs to.")]
    [Display(Name = "Course")]
    public int? CourseId { get; set; }

    [Display(Name = "PDF file")]
    public IFormFile? File { get; set; }
}

public class MaterialsPage
{
    public MaterialUploadForm Form { get; set; } = new();
    public List<Material> Materials { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public int MaxSizeMB { get; set; }
}

/// <summary>A piece of text of a material, with its page (document_chunks).</summary>
public class SourceChunk
{
    public int ChunkId { get; set; }
    public string FileName { get; set; } = "";
    public int? PageNumber { get; set; }
    public string Content { get; set; } = "";

    public string Label => PageNumber == null ? FileName : $"{FileName}, p.{PageNumber}";
}

/// <summary>One AI chat (ai_conversations).</summary>
public class Conversation
{
    public int ConversationId { get; set; }
    public string Title { get; set; } = "";
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A question or an answer in a chat (ai_messages).</summary>
public class AiMessage
{
    public int MessageId { get; set; }
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    /// <summary>False when the materials did not contain the answer (NULL for questions).</summary>
    public bool? IsGrounded { get; set; }
    public string? Sources { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Body of the asynchronous "ask" request.</summary>
public record AskRequest(string? Question);

/// <summary>The "New chat" form.</summary>
public class NewConversationForm
{
    /// <summary>Limit the chat to one course, or null for all materials.</summary>
    public int? CourseId { get; set; }
}

public class AssistantPage
{
    public List<Conversation> Conversations { get; set; } = new();
    public Conversation? Selected { get; set; }
    public List<AiMessage> Messages { get; set; } = new();
    public List<Course> Courses { get; set; } = new();
    public int UsableMaterials { get; set; }
    /// <summary>The API key is configured on the server.</summary>
    public bool AiConfigured { get; set; }
    /// <summary>The platform setting and the user's own AI access both allow it.</summary>
    public bool AiAllowed { get; set; }
    public string Disclaimer { get; set; } = "";
}
