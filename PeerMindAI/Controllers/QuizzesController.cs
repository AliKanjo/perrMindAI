using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>Practice quizzes generated from the student's materials, results and recommendations (FR-10, FR-11).</summary>
[Authorize(Roles = Roles.Student)]
public class QuizzesController : BaseController
{
    private readonly QuizRepository _quizzes;
    private readonly MaterialRepository _materials;
    private readonly AssistantRepository _assistant;
    private readonly QuizGenerator _generator;
    private readonly AiClient _ai;

    public QuizzesController(QuizRepository quizzes, MaterialRepository materials, AssistantRepository assistant,
        QuizGenerator generator, AiClient ai)
    {
        _quizzes = quizzes;
        _materials = materials;
        _assistant = assistant;
        _generator = generator;
        _ai = ai;
    }

    [HttpGet("/quizzes")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildPageAsync(new QuizGenerateForm()));
    }

    /// <summary>Asks the AI for a quiz about one material and stores it.</summary>
    [HttpPost("/quizzes/generate")]
    public async Task<IActionResult> Generate([Bind(Prefix = "Form")] QuizGenerateForm form)
    {
        if (ModelState.IsValid && !await _assistant.IsAiAllowedAsync(CurrentUserId))
        {
            ModelState.AddModelError("", "The AI features are turned off for your account.");
        }
        if (ModelState.IsValid)
        {
            try
            {
                var quizId = await _generator.GenerateAsync(CurrentUserId, form.MaterialId!.Value, form.QuestionCount);
                var attemptId = await _quizzes.StartAttemptAsync(quizId, CurrentUserId);
                Toast("Your quiz is ready. Good luck!");
                return Redirect($"/quizzes/attempts/{attemptId}");
            }
            catch (DbRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (AiServiceException ex)
            {
                // friendly message only; the details are in the server log
                ModelState.AddModelError("", ex.Message);
            }
        }
        return View(nameof(Index), await BuildPageAsync(form));
    }

    /// <summary>Starts a new attempt of a quiz (also used for "Retry").</summary>
    [HttpPost("/quizzes/{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        try
        {
            var attemptId = await _quizzes.StartAttemptAsync(id, CurrentUserId);
            return Redirect($"/quizzes/attempts/{attemptId}");
        }
        catch (DbRuleException)
        {
            return NotFound();   // not this student's quiz
        }
    }

    [HttpGet("/quizzes/attempts/{attemptId:int}")]
    public async Task<IActionResult> Take(int attemptId)
    {
        var attempt = await _quizzes.GetOpenAttemptAsync(attemptId, CurrentUserId);
        if (attempt == null)
        {
            // already submitted: show its result instead
            return await _quizzes.GetAttemptQuizAsync(attemptId, CurrentUserId) == null
                ? NotFound()
                : Redirect($"/quizzes/results/{attemptId}");
        }
        return View(attempt);
    }

    /// <summary>Saves the chosen answers and scores the attempt. answers[questionId] = optionId.</summary>
    [HttpPost("/quizzes/attempts/{attemptId:int}")]
    public async Task<IActionResult> Submit(int attemptId, Dictionary<int, int>? answers)
    {
        var attempt = await _quizzes.GetOpenAttemptAsync(attemptId, CurrentUserId);
        if (attempt == null)
        {
            return NotFound();
        }

        answers ??= new();
        var unanswered = attempt.Questions.Where(q => !answers.ContainsKey(q.QuestionId)).Select(q => q.Number).ToList();
        if (unanswered.Count > 0)
        {
            ModelState.AddModelError("", $"Please answer every question before submitting (missing: {string.Join(", ", unanswered)}).");
            ViewBag.Answers = answers;
            return View(nameof(Take), attempt);
        }

        try
        {
            foreach (var question in attempt.Questions)
            {
                await _quizzes.SaveAnswerAsync(attemptId, CurrentUserId, question.QuestionId, answers[question.QuestionId]);
            }
            await _quizzes.SubmitAsync(attemptId, CurrentUserId);
        }
        catch (DbRuleException ex)
        {
            // e.g. an option that does not belong to the question
            ModelState.AddModelError("", ex.Message);
            ViewBag.Answers = answers;
            return View(nameof(Take), attempt);
        }
        return Redirect($"/quizzes/results/{attemptId}");
    }

    [HttpGet("/quizzes/results/{attemptId:int}")]
    public async Task<IActionResult> Result(int attemptId)
    {
        var result = await _quizzes.GetResultAsync(attemptId, CurrentUserId);
        var quiz = await _quizzes.GetAttemptQuizAsync(attemptId, CurrentUserId);
        if (result == null || quiz == null)
        {
            return NotFound();
        }
        result.QuizId = quiz.Value.QuizId;
        result.CourseId = quiz.Value.CourseId;
        return View(result);
    }

    [HttpPost("/quizzes/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _quizzes.DeleteAsync(CurrentUserId, id);
            Toast("The quiz was deleted.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return Redirect("/quizzes");
    }

    /// <summary>Hides a study recommendation on the dashboard (fetch + JSON).</summary>
    [HttpPost("/recommendations/{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        if (await _quizzes.DismissRecommendationAsync(CurrentUserId, id) == 0)
        {
            return JsonError("Recommendation not found.", StatusCodes.Status404NotFound);
        }
        return Json(new { ok = true, message = "Recommendation dismissed." });
    }

    private async Task<QuizzesPage> BuildPageAsync(QuizGenerateForm form) => new()
    {
        Form = form,
        Quizzes = await _quizzes.ListForStudentAsync(CurrentUserId),
        Materials = (await _materials.ListForStudentAsync(CurrentUserId)).Where(m => m.ProcessingStatus == "Processed").ToList(),
        AiConfigured = _ai.IsConfigured,
        AiAllowed = await _assistant.IsAiAllowedAsync(CurrentUserId)
    };
}
