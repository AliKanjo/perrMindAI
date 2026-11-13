using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Tutor search, public profile, the tutor's own profile, availability and reviews.</summary>
public class TutorRepository
{
    private readonly Database _db;

    public TutorRepository(Database db) => _db = db;

    /// <summary>Approved tutors that match the filters (every filter is optional).</summary>
    public Task<List<TutorCard>> SearchAsync(int? courseId, string? search, decimal? minRating, int? availableWithinDays) =>
        _db.QueryProcedureAsync("sp_tutor_search", r => new TutorCard
        {
            TutorId = r.Int("tutor_id"),
            FullName = r.Text("full_name"),
            University = r.TextOrNull("university"),
            Headline = r.TextOrNull("headline"),
            HourlyRate = r.Decimal("hourly_rate"),
            AverageRating = r.Decimal("average_rating"),
            ReviewCount = r.Int("review_count"),
            Courses = r.TextOrNull("courses"),
            NextAvailable = r.DateOrNull("next_available")
        },
        Database.In("p_course_id", courseId),
        Database.In("p_search", search),
        Database.In("p_min_rating", minRating),
        Database.In("p_max_hourly_rate", null),
        Database.In("p_available_within_days", availableWithinDays));

    /// <summary>Profile, courses, free slots and reviews of one approved tutor (null if not found).</summary>
    public Task<TutorProfile?> GetProfileAsync(int tutorId) =>
        _db.ReadProcedureAsync<TutorProfile?>("sp_tutor_get_profile", async reader =>
        {
            if (!await reader.ReadAsync())
            {
                return null;
            }
            var profile = new TutorProfile
            {
                TutorId = reader.Int("tutor_id"),
                FullName = reader.Text("full_name"),
                University = reader.TextOrNull("university"),
                Headline = reader.TextOrNull("headline"),
                Bio = reader.TextOrNull("bio"),
                Degree = reader.TextOrNull("degree"),
                Major = reader.TextOrNull("major"),
                HourlyRate = reader.Decimal("hourly_rate"),
                AverageRating = reader.Decimal("average_rating"),
                ReviewCount = reader.Int("review_count")
            };

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                profile.Courses.Add(new CourseOption
                {
                    CourseId = reader.Int("course_id"),
                    Code = reader.Text("code"),
                    Name = reader.Text("name")
                });
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                profile.FreeSlots.Add(new TimeSlot
                {
                    SlotId = reader.Int("slot_id"),
                    StartAt = reader.Date("start_at"),
                    EndAt = reader.Date("end_at")
                });
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                profile.Reviews.Add(new Review
                {
                    ReviewId = reader.Int("review_id"),
                    StudentName = reader.Text("student_name"),
                    Rating = reader.Int("rating"),
                    Comment = reader.TextOrNull("comment"),
                    TutorReply = reader.TextOrNull("tutor_reply"),
                    CreatedAt = reader.Date("created_at")
                });
            }
            return profile;
        }, Database.In("p_tutor_id", tutorId));

    public async Task<TutorProfileForm?> GetProfileFormAsync(int tutorId)
    {
        var rows = await _db.QuerySqlAsync(
            "SELECT headline, bio, hourly_rate, accept_same_day FROM tutor_profiles WHERE user_id = @tutor_id",
            r => new TutorProfileForm
            {
                Headline = r.Text("headline"),
                Bio = r.TextOrNull("bio"),
                HourlyRate = r.Decimal("hourly_rate"),
                AcceptSameDay = r.Bool("accept_same_day")
            }, Database.In("@tutor_id", tutorId));
        return rows.FirstOrDefault();
    }

    public Task UpdateProfileAsync(int tutorId, TutorProfileForm form) =>
        _db.ExecuteProcedureAsync("sp_tutor_update_profile",
            Database.In("p_tutor_id", tutorId),
            Database.In("p_headline", form.Headline.Trim()),
            Database.In("p_bio", form.Bio?.Trim()),
            Database.In("p_hourly_rate", form.HourlyRate),
            Database.In("p_accept_same_day", form.AcceptSameDay),
            Database.In("p_buffer_minutes", null));

    // ---------- availability ----------

    /// <summary>The tutor's future slots, with the booking that occupies each one (if any).</summary>
    public Task<List<TimeSlot>> GetUpcomingSlotsAsync(int tutorId) =>
        _db.QuerySqlAsync(
            @"SELECT s.slot_id, s.start_at, s.end_at, b.status AS booking_status, u.full_name AS student_name
              FROM availability_slots s
              LEFT JOIN bookings b ON b.active_slot_id = s.slot_id
              LEFT JOIN users u    ON u.user_id = b.student_id
              WHERE s.tutor_id = @tutor_id AND s.is_open = 1 AND s.start_at > NOW()
              ORDER BY s.start_at",
            r => new TimeSlot
            {
                SlotId = r.Int("slot_id"),
                StartAt = r.Date("start_at"),
                EndAt = r.Date("end_at"),
                BookingStatus = r.TextOrNull("booking_status"),
                StudentName = r.TextOrNull("student_name")
            }, Database.In("@tutor_id", tutorId));

    public async Task<int> AddSlotAsync(int tutorId, DateTime startAt, DateTime endAt)
    {
        var newId = Database.Out("p_new_slot_id");
        await _db.ExecuteProcedureAsync("sp_slot_add",
            Database.In("p_tutor_id", tutorId),
            Database.In("p_start_at", startAt),
            Database.In("p_end_at", endAt),
            newId);
        return Database.OutInt(newId);
    }

    public Task RemoveSlotAsync(int tutorId, int slotId) =>
        _db.ExecuteProcedureAsync("sp_slot_remove",
            Database.In("p_tutor_id", tutorId),
            Database.In("p_slot_id", slotId));

    // ---------- reviews ----------

    public Task<List<Review>> GetReviewsAsync(int tutorId) =>
        _db.QuerySqlAsync(
            @"SELECT r.review_id, u.full_name AS student_name, r.rating, r.comment, r.tutor_reply, r.created_at
              FROM reviews r JOIN users u ON u.user_id = r.student_id
              WHERE r.tutor_id = @tutor_id AND r.is_hidden = 0
              ORDER BY r.created_at DESC",
            r => new Review
            {
                ReviewId = r.Int("review_id"),
                StudentName = r.Text("student_name"),
                Rating = r.Int("rating"),
                Comment = r.TextOrNull("comment"),
                TutorReply = r.TextOrNull("tutor_reply"),
                CreatedAt = r.Date("created_at")
            }, Database.In("@tutor_id", tutorId));

    public Task ReplyToReviewAsync(int reviewId, int tutorId, string reply) =>
        _db.ExecuteProcedureAsync("sp_review_reply",
            Database.In("p_review_id", reviewId),
            Database.In("p_tutor_id", tutorId),
            Database.In("p_reply", reply));

    // ---------- dashboard ----------

    public async Task<TutorStats> GetStatsAsync(int tutorId)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT
                (SELECT COUNT(*) FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
                  WHERE b.tutor_id = @tutor_id AND b.status = 'Confirmed' AND s.end_at > NOW()) AS upcoming_sessions,
                (SELECT COUNT(*) FROM bookings
                  WHERE tutor_id = @tutor_id AND status = 'Pending')                            AS pending_requests,
                (SELECT IFNULL(SUM(b.price), 0) FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
                  WHERE b.tutor_id = @tutor_id AND b.status = 'Completed'
                    AND s.start_at >= DATE_FORMAT(CURDATE(), '%Y-%m-01'))                       AS earned_this_month,
                tp.average_rating, tp.review_count
              FROM tutor_profiles tp WHERE tp.user_id = @tutor_id",
            r => new TutorStats
            {
                UpcomingSessions = r.Int("upcoming_sessions"),
                PendingRequests = r.Int("pending_requests"),
                EarnedThisMonth = r.Decimal("earned_this_month"),
                AverageRating = r.Decimal("average_rating"),
                ReviewCount = r.Int("review_count")
            }, Database.In("@tutor_id", tutorId));
        return rows.FirstOrDefault() ?? new TutorStats();
    }
}
