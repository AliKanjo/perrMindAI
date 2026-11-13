using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Models;

namespace PeerMindAI.Controllers;

/// <summary>Admin course library: create, read, update and delete courses.</summary>
[Authorize(Roles = Roles.Admin)]
public class CoursesController : BaseController
{
    private readonly CourseRepository _courses;

    public CoursesController(CourseRepository courses) => _courses = courses;

    [HttpGet("/admin/courses")]
    public async Task<IActionResult> Index(string? q)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        return View(new CourseListPage
        {
            Courses = await _courses.ListAsync(q, includeInactive: true),
            Search = q
        });
    }

    [HttpGet("/admin/courses/create")]
    public IActionResult Create() => View("Form", new CourseForm());

    [HttpPost("/admin/courses/create")]
    public async Task<IActionResult> Create(CourseForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _courses.CreateAsync(CurrentUserId, form);
                Toast($"\"{form.Name.Trim()}\" was added to the course library.");
                return RedirectToAction(nameof(Index));
            }
            catch (DbRuleException ex)
            {
                // e.g. "A course with this code already exists."
                ModelState.AddModelError(nameof(form.Code), ex.Message);
            }
        }
        return View("Form", form);
    }

    [HttpGet("/admin/courses/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _courses.GetAsync(id);
        if (course == null)
        {
            return NotFound();
        }

        ViewBag.Course = course;
        return View("Form", new CourseForm
        {
            CourseId = course.CourseId,
            Code = course.Code,
            Name = course.Name,
            Department = course.Department,
            Professor = course.Professor,
            IsActive = course.IsActive
        });
    }

    [HttpPost("/admin/courses/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CourseForm form)
    {
        form.CourseId = id;
        if (ModelState.IsValid)
        {
            try
            {
                await _courses.UpdateAsync(CurrentUserId, id, form);
                Toast("The course was updated.");
                return RedirectToAction(nameof(Index));
            }
            catch (DbRuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        ViewBag.Course = await _courses.GetAsync(id);
        return View("Form", form);
    }

    [HttpPost("/admin/courses/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            // the message says whether the course was deleted or only deactivated (when it is in use)
            Toast(await _courses.DeleteAsync(CurrentUserId, id));
        }
        catch (DbRuleException ex)
        {
            Toast(ex.Message, isError: true);
        }
        return RedirectToAction(nameof(Index));
    }
}
