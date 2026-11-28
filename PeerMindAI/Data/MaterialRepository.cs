using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>A student's uploaded course materials and their extracted text (FR-08, NFR-07).</summary>
public class MaterialRepository
{
    private readonly Database _db;

    public MaterialRepository(Database db) => _db = db;

    /// <summary>The materials of one student only: nobody else can see them.</summary>
    public Task<List<Material>> ListForStudentAsync(int studentId) =>
        _db.QuerySqlAsync(
            @"SELECT m.material_id, m.course_id, c.name AS course_name, m.file_name, m.file_size_bytes,
                     m.page_count, m.processing_status, m.use_as_ai_source, m.uploaded_at,
                     (SELECT COUNT(*) FROM document_chunks ch WHERE ch.material_id = m.material_id) AS chunk_count
              FROM course_materials m JOIN courses c ON c.course_id = m.course_id
              WHERE m.uploaded_by = @student_id
              ORDER BY m.uploaded_at DESC, m.material_id DESC",
            r => new Material
            {
                MaterialId = r.Int("material_id"),
                CourseId = r.Int("course_id"),
                CourseName = r.Text("course_name"),
                FileName = r.Text("file_name"),
                FileSizeBytes = Convert.ToInt64(r["file_size_bytes"]),
                PageCount = r.IntOrNull("page_count"),
                ProcessingStatus = r.Text("processing_status"),
                UseAsAiSource = r.Bool("use_as_ai_source"),
                ChunkCount = r.Int("chunk_count"),
                UploadedAt = r.Date("uploaded_at")
            }, Database.In("@student_id", studentId));

    /// <summary>Registers an upload (status Processing) and returns its id.</summary>
    public async Task<int> CreateAsync(int studentId, int courseId, string fileName, string storagePath, long sizeBytes)
    {
        var newId = Database.Out("p_new_material_id");
        await _db.ExecuteProcedureAsync("sp_material_create",
            Database.In("p_student_id", studentId),
            Database.In("p_course_id", courseId),
            Database.In("p_file_name", fileName),
            Database.In("p_storage_path", storagePath),
            Database.In("p_file_size_bytes", sizeBytes),
            newId);
        return Database.OutInt(newId);
    }

    public Task AddChunkAsync(int materialId, int pageNumber, int chunkIndex, string content) =>
        _db.ExecuteSqlAsync(
            @"INSERT INTO document_chunks (material_id, page_number, chunk_index, content)
              VALUES (@material_id, @page_number, @chunk_index, @content)",
            Database.In("@material_id", materialId),
            Database.In("@page_number", pageNumber),
            Database.In("@chunk_index", chunkIndex),
            Database.In("@content", content));

    public Task SetProcessedAsync(int materialId, int pageCount, bool succeeded) =>
        _db.ExecuteProcedureAsync("sp_material_set_processed",
            Database.In("p_material_id", materialId),
            Database.In("p_page_count", pageCount),
            Database.In("p_succeeded", succeeded));

    /// <summary>Turns a material on or off as an AI source. Returns false when it is not the student's.</summary>
    public async Task<bool> SetAiSourceAsync(int studentId, int materialId, bool useAsSource)
    {
        var rows = await _db.QuerySqlAsync(
            "SELECT COUNT(*) AS found FROM course_materials WHERE material_id = @material_id AND uploaded_by = @student_id",
            r => r.Int("found"),
            Database.In("@material_id", materialId), Database.In("@student_id", studentId));
        if (rows.FirstOrDefault() == 0)
        {
            return false;
        }
        await _db.ExecuteSqlAsync(
            "UPDATE course_materials SET use_as_ai_source = @use WHERE material_id = @material_id AND uploaded_by = @student_id",
            Database.In("@use", useAsSource),
            Database.In("@material_id", materialId),
            Database.In("@student_id", studentId));
        return true;
    }

    /// <summary>The stored file of a material owned by the student (null if not found).</summary>
    public async Task<string?> GetStoragePathAsync(int studentId, int materialId)
    {
        var rows = await _db.QuerySqlAsync(
            "SELECT storage_path FROM course_materials WHERE material_id = @material_id AND uploaded_by = @student_id",
            r => r.Text("storage_path"),
            Database.In("@material_id", materialId), Database.In("@student_id", studentId));
        return rows.FirstOrDefault();
    }

    /// <summary>Deletes a material; the procedure refuses when a quiz or lesson still uses it.</summary>
    public Task DeleteAsync(int studentId, int materialId) =>
        _db.ExecuteProcedureAsync("sp_material_delete",
            Database.In("p_student_id", studentId),
            Database.In("p_material_id", materialId));

    /// <summary>
    /// The text the assistant may search: processed materials of this student that are
    /// switched on as AI sources, optionally limited to one course.
    /// </summary>
    public Task<List<SourceChunk>> GetSearchableChunksAsync(int studentId, int? courseId) =>
        _db.QuerySqlAsync(
            @"SELECT ch.chunk_id, m.file_name, ch.page_number, ch.content
              FROM document_chunks ch
              JOIN course_materials m ON m.material_id = ch.material_id
              WHERE m.uploaded_by = @student_id AND m.processing_status = 'Processed' AND m.use_as_ai_source = 1
                AND (@course_id IS NULL OR m.course_id = @course_id)
              ORDER BY m.material_id, ch.chunk_index",
            r => new SourceChunk
            {
                ChunkId = r.Int("chunk_id"),
                FileName = r.Text("file_name"),
                PageNumber = r.IntOrNull("page_number"),
                Content = r.Text("content")
            },
            Database.In("@student_id", studentId),
            Database.In("@course_id", courseId));
}
