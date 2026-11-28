using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>The AI study assistant: chats about the student's own materials (FR-08, FR-09, Scenario 2).</summary>
[Authorize(Roles = Roles.Student)]
public class AssistantController : BaseController
{
    private const int MaxQuestionLength = 500;

    private readonly AssistantRepository _assistant;
    private readonly MaterialRepository _materials;
    private readonly CourseRepository _courses;
    private readonly UserRepository _users;
    private readonly StudyAssistant _studyAssistant;
    private readonly AiClient _ai;

    public AssistantController(AssistantRepository assistant, MaterialRepository materials, CourseRepository courses,
        UserRepository users, StudyAssistant studyAssistant, AiClient ai)
    {
        _assistant = assistant;
        _materials = materials;
        _courses = courses;
        _users = users;
        _studyAssistant = studyAssistant;
        _ai = ai;
    }

    /// <summary>Chat list on the left, the selected chat on the right.</summary>
    [HttpGet("/assistant")]
    public async Task<IActionResult> Index(int? c)
    {
        var conversations = await _assistant.ListConversationsAsync(CurrentUserId);
        var selected = c == null ? conversations.FirstOrDefault() : conversations.FirstOrDefault(x => x.ConversationId == c);
        if (c != null && selected == null)
        {
            return NotFound();   // not this student's chat
        }

        var materials = await _materials.ListForStudentAsync(CurrentUserId);
        return View(new AssistantPage
        {
            Conversations = conversations,
            Selected = selected,
            Messages = selected == null ? new() : await _assistant.GetMessagesAsync(selected.ConversationId, CurrentUserId),
            Courses = await _courses.ListAsync(null, includeInactive: false),
            UsableMaterials = materials.Count(m => m.ProcessingStatus == "Processed" && m.UseAsAiSource),
            AiConfigured = _ai.IsConfigured,
            AiAllowed = await _assistant.IsAiAllowedAsync(CurrentUserId),
            Disclaimer = await _users.GetSettingAsync("AiDisclaimer")
                         ?? "AI-generated answers may contain errors. Check them against the referenced course materials."
        });
    }

    [HttpPost("/assistant/new")]
    public async Task<IActionResult> Create(NewConversationForm form)
    {
        try
        {
            var id = await _assistant.CreateConversationAsync(CurrentUserId, form.CourseId);
            return Redirect($"/assistant?c={id}");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
            return Redirect("/assistant");
        }
    }

    /// <summary>Asynchronous question: the browser sends JSON and gets the answer as JSON.</summary>
    [HttpPost("/assistant/{id:int}/ask")]
    public async Task<IActionResult> Ask(int id, [FromBody] AskRequest? request)
    {
        var question = request?.Question?.Trim() ?? "";
        if (question.Length == 0)
        {
            return JsonError("Please type a question.");
        }
        if (question.Length > MaxQuestionLength)
        {
            return JsonError($"Questions can have at most {MaxQuestionLength} characters.");
        }

        var conversation = (await _assistant.ListConversationsAsync(CurrentUserId)).FirstOrDefault(x => x.ConversationId == id);
        if (conversation == null)
        {
            return JsonError("This chat was not found.", StatusCodes.Status404NotFound);
        }
        if (!await _assistant.IsAiAllowedAsync(CurrentUserId))
        {
            return JsonError("The AI assistant is turned off for your account.", StatusCodes.Status403Forbidden);
        }

        try
        {
            var reply = await _studyAssistant.AskAsync(CurrentUserId, conversation, question);
            return Json(new
            {
                ok = true,
                question = new { reply.Question.Content, Time = Ui.Ago(reply.Question.CreatedAt) },
                answer = new
                {
                    reply.Answer.Content,
                    reply.Answer.IsGrounded,
                    reply.Answer.Sources,
                    Time = Ui.Ago(reply.Answer.CreatedAt)
                }
            });
        }
        catch (DbRuleException ex)
        {
            return JsonError(ex.Message);
        }
        catch (AiServiceException ex)
        {
            // only the friendly message goes to the browser; the details are in the server log
            return JsonError(ex.Message, StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("/assistant/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _assistant.DeleteConversationAsync(CurrentUserId, id);
        if (deleted == 0)
        {
            return NotFound();
        }
        Toast("The chat was deleted.");
        return Redirect("/assistant");
    }
}
