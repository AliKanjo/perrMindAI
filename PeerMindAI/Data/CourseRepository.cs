using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Course library: full create / read / update / delete through stored procedures.</summary>
public class CourseRepository
{
    private readonly Database _db;

    public CourseRepository(Database db) => _db = db;

    public Task<List<Course>> ListAsync(string? search, bool includeInactive) =>
        _db.QueryProcedureAsync("sp_course_list", r => new Course
        {
            CourseId = r.Int("course_id"),
            Code = r.Text("code"),
            Name = r.Text("name"),
            Department = r.TextOrNull("department"),
            Professor = r.TextOrNull("professor"),
            IsActive = r.Bool("is_active"),
            Students = r.Int("students"),
            Materials = r.Int("materials")
        },
        Database.In("p_search", search),
        Database.In("p_department", null),
        Database.In("p_include_inactive", includeInactive));

    public async Task<Course?> GetAsync(int courseId)
    {
        var rows = await _db.QueryProcedureAsync("sp_course_get_by_id", r => new Course
        {
            CourseId = r.Int("course_id"),
            Code = r.Text("code"),
            Name = r.Text("name"),
            Department = r.TextOrNull("department"),
            Professor = r.TextOrNull("professor"),
            IsActive = r.Bool("is_active"),
            Students = r.Int("students"),
            Materials = r.Int("materials"),
            Tutors = r.Int("tutors"),
            Topics = r.TextOrNull("topics")
        }, Database.In("p_course_id", courseId));
        return rows.FirstOrDefault();
    }

    public async Task<int> CreateAsync(int adminId, CourseForm form)
    {
        var newId = Database.Out("p_new_course_id");
        await _db.ExecuteProcedureAsync("sp_course_create",
            Database.In("p_admin_id", adminId),
            Database.In("p_code", form.Code.Trim()),
            Database.In("p_name", form.Name.Trim()),
            Database.In("p_department", form.Department?.Trim()),
            Database.In("p_professor", form.Professor?.Trim()),
            Database.In("p_university_id", null),
            newId);
        return Database.OutInt(newId);
    }

    public Task UpdateAsync(int adminId, int courseId, CourseForm form) =>
        _db.ExecuteProcedureAsync("sp_course_update",
            Database.In("p_admin_id", adminId),
            Database.In("p_course_id", courseId),
            Database.In("p_code", form.Code.Trim()),
            Database.In("p_name", form.Name.Trim()),
            Database.In("p_department", form.Department?.Trim()),
            Database.In("p_professor", form.Professor?.Trim()),
            Database.In("p_university_id", null),
            Database.In("p_is_active", form.IsActive));

    /// <summary>
    /// Deletes the course. A course that is in use is only deactivated, so the
    /// procedure tells us what happened ("Deleted" or "Deactivated") with a message.
    /// </summary>
    public async Task<string> DeleteAsync(int adminId, int courseId)
    {
        var rows = await _db.QueryProcedureAsync("sp_course_delete", r => r.Text("message"),
            Database.In("p_admin_id", adminId),
            Database.In("p_course_id", courseId));
        return rows.FirstOrDefault() ?? "The course was removed.";
    }
}
