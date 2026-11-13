using System.Data.Common;
using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Bookings and reviews. Every rule (double booking, cancellation window ...) lives in the stored procedures.</summary>
public class BookingRepository
{
    private readonly Database _db;

    public BookingRepository(Database db) => _db = db;

    /// <summary>Requests a session; throws DbRuleException when the slot was taken in the meantime.</summary>
    public async Task<int> CreateAsync(int studentId, int slotId, int courseId, string? topic, string? notes)
    {
        var newId = Database.Out("p_new_booking_id");
        await _db.ExecuteProcedureAsync("sp_booking_create",
            Database.In("p_student_id", studentId),
            Database.In("p_slot_id", slotId),
            Database.In("p_course_id", courseId),
            Database.In("p_topic", topic),
            Database.In("p_notes", notes),
            newId);
        return Database.OutInt(newId);
    }

    /// <summary>
    /// One booking with its status history. Returns null when the booking does not
    /// exist or the user is not its student, its tutor or an administrator.
    /// </summary>
    public Task<(Booking Booking, List<BookingHistoryEntry> History)?> GetAsync(int bookingId, int requestedBy) =>
        _db.ReadProcedureAsync<(Booking, List<BookingHistoryEntry>)?>("sp_booking_get_by_id", async reader =>
        {
            if (!await reader.ReadAsync())
            {
                return null;
            }
            var booking = MapBooking(reader);
            booking.Notes = reader.TextOrNull("notes");
            booking.CancellationReason = reader.TextOrNull("cancellation_reason");
            booking.ReviewRating = reader.IntOrNull("review_rating");

            var history = new List<BookingHistoryEntry>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                history.Add(new BookingHistoryEntry
                {
                    OldStatus = reader.TextOrNull("old_status"),
                    NewStatus = reader.Text("new_status"),
                    ChangedBy = reader.TextOrNull("changed_by"),
                    Note = reader.TextOrNull("note"),
                    ChangedAt = reader.Date("changed_at")
                });
            }
            return (booking, history);
        },
        Database.In("p_booking_id", bookingId),
        Database.In("p_requested_by", requestedBy));

    public Task<List<Booking>> ListForStudentAsync(int studentId) =>
        _db.QuerySqlAsync(
            "SELECT * FROM vw_booking_details WHERE student_id = @user_id ORDER BY start_at DESC",
            MapBooking, Database.In("@user_id", studentId));

    public Task<List<Booking>> ListForTutorAsync(int tutorId) =>
        _db.QuerySqlAsync(
            "SELECT * FROM vw_booking_details WHERE tutor_id = @user_id ORDER BY start_at",
            r =>
            {
                var booking = MapBooking(r);
                booking.Notes = r.TextOrNull("notes");
                return booking;
            }, Database.In("@user_id", tutorId));

    public Task UpdateNotesAsync(int bookingId, int studentId, string? topic, string? notes) =>
        _db.ExecuteProcedureAsync("sp_booking_update",
            Database.In("p_booking_id", bookingId),
            Database.In("p_student_id", studentId),
            Database.In("p_topic", topic),
            Database.In("p_notes", notes));

    /// <summary>Tutor accepts or declines a pending request.</summary>
    public Task RespondAsync(int bookingId, int tutorId, bool accept) =>
        _db.ExecuteProcedureAsync("sp_booking_respond",
            Database.In("p_booking_id", bookingId),
            Database.In("p_tutor_id", tutorId),
            Database.In("p_accept", accept));

    public Task CancelAsync(int bookingId, int userId, string reason) =>
        _db.ExecuteProcedureAsync("sp_booking_cancel",
            Database.In("p_booking_id", bookingId),
            Database.In("p_user_id", userId),
            Database.In("p_reason", reason));

    public Task CompleteAsync(int bookingId, int tutorId) =>
        _db.ExecuteProcedureAsync("sp_booking_complete",
            Database.In("p_booking_id", bookingId),
            Database.In("p_tutor_id", tutorId));

    public async Task<int> CreateReviewAsync(int bookingId, int studentId, int rating, string? comment)
    {
        var newId = Database.Out("p_new_review_id");
        await _db.ExecuteProcedureAsync("sp_review_create",
            Database.In("p_booking_id", bookingId),
            Database.In("p_student_id", studentId),
            Database.In("p_rating", rating),
            Database.In("p_comment", comment),
            newId);
        return Database.OutInt(newId);
    }

    private static Booking MapBooking(DbDataReader r) => new()
    {
        BookingId = r.Int("booking_id"),
        BookingCode = r.Text("booking_code"),
        Status = r.Text("status"),
        Topic = r.TextOrNull("topic"),
        Price = r.Decimal("price"),
        StartAt = r.Date("start_at"),
        EndAt = r.Date("end_at"),
        CourseName = r.Text("course_name"),
        StudentId = r.Int("student_id"),
        StudentName = r.Text("student_name"),
        TutorId = r.Int("tutor_id"),
        TutorName = r.Text("tutor_name"),
        ReviewId = r.IntOrNull("review_id")
    };
}
