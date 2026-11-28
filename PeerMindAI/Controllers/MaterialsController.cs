using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;
using PeerMindAI.Services;

namespace PeerMindAI.Controllers;

/// <summary>"My materials": upload PDFs for the AI assistant, switch them on or off, delete them (FR-08).</summary>
[Authorize(Roles = Roles.Student)]
public class MaterialsController : BaseController
{
    private readonly MaterialRepository _materials;
    private readonly CourseRepository _courses;
    private readonly MaterialStorage _storage;
    private readonly PdfTextExtractor _extractor;

    public MaterialsController(MaterialRepository materials, CourseRepository courses, MaterialStorage storage,
        PdfTextExtractor extractor)
    {
        _materials = materials;
        _courses = courses;
        _storage = storage;
        _extractor = extractor;
    }

    [HttpGet("/materials")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildPageAsync(new MaterialUploadForm()));
    }

    /// <summary>Upload: check the file, save it, extract the text page by page.</summary>
    [HttpPost("/materials")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload([Bind(Prefix = "Form")] MaterialUploadForm form)
    {
        if (form.File == null)
        {
            ModelState.AddModelError("Form.File", "Please choose a PDF file.");
        }
        else if (await _storage.ValidateAsync(form.File) is { } fileError)
        {
            ModelState.AddModelError("Form.File", fileError);
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildPageAsync(form));
        }

        var file = form.File!;
        int materialId;
        try
        {
            var path = await _storage.SaveAsync(CurrentUserId, file);
            materialId = await _materials.CreateAsync(CurrentUserId, form.CourseId!.Value,
                Path.GetFileName(file.FileName), path, file.Length);
        }
        catch (DbRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(nameof(Index), await BuildPageAsync(form));
        }

        // read the text now; a PDF without text is kept but marked "Failed"
        try
        {
            await using var stream = file.OpenReadStream();
            var result = _extractor.Extract(stream);
            for (var i = 0; i < result.Chunks.Count; i++)
            {
                await _materials.AddChunkAsync(materialId, result.Chunks[i].PageNumber, i + 1, result.Chunks[i].Text);
            }
            await _materials.SetProcessedAsync(materialId, result.PageCount, succeeded: true);
            Toast($"\"{file.FileName}\" was uploaded and {result.PageCount} page(s) were read. You can ask the AI assistant about it now.");
        }
        catch (InvalidDataException ex)
        {
            await _materials.SetProcessedAsync(materialId, 0, succeeded: false);
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Index));
    }

    public record SourceRequest(bool Use);

    /// <summary>Asynchronous switch: use this material as an AI source or not (fetch + JSON).</summary>
    [HttpPost("/materials/{id:int}/source")]
    public async Task<IActionResult> SetSource(int id, [FromBody] SourceRequest? request)
    {
        if (request == null)
        {
            return JsonError("Invalid request.");
        }
        if (!await _materials.SetAiSourceAsync(CurrentUserId, id, request.Use))
        {
            return JsonError("Material not found.", StatusCodes.Status404NotFound);
        }
        return Json(new
        {
            ok = true,
            message = request.Use ? "The assistant will use this material." : "The assistant will ignore this material."
        });
    }

    [HttpPost("/materials/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var path = await _materials.GetStoragePathAsync(CurrentUserId, id);
        if (path == null)
        {
            return NotFound();
        }
        try
        {
            await _materials.DeleteAsync(CurrentUserId, id);
            _storage.Delete(path);
            Toast("The material was deleted.");
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<MaterialsPage> BuildPageAsync(MaterialUploadForm form) => new()
    {
        Form = form,
        Materials = await _materials.ListForStudentAsync(CurrentUserId),
        Courses = await _courses.ListAsync(null, includeInactive: false),
        MaxSizeMB = _storage.MaxSizeMB
    };
}
