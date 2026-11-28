-- =====================================================================
-- PeerMind AI - Views, triggers and stored procedures (MariaDB / MySQL)
-- Import after 01_schema.sql. Safe to import again (everything is
-- dropped and re-created; no table data is touched).
--
-- Full CRUD is implemented for three principal entities:
--   users     sp_user_create / _get_by_id / _list / _update / _set_status / _delete
--   courses   sp_course_create / _get_by_id / _list / _update / _delete
--   bookings  sp_booking_create / _get_by_id / _list_by_student / _list_by_tutor /
--             _update / _respond / _reschedule / _cancel / _complete / _delete
-- plus the main workflows: tutor search, availability, tutor applications,
-- quizzes with study recommendations, reviews, reports, AI chat.
--
-- Errors are raised with SIGNAL SQLSTATE '45000' and a user-friendly
-- message (NFR-04) that the application can show directly.
-- =====================================================================
-- Runs in the database selected in phpMyAdmin (e.g. peermindai_db).

-- Text constants inside a procedure get the collation of the connection that
-- creates it, while variables and columns use the collation of the database.
-- If the two differ (for example utf8mb4_unicode_ci and utf8mb4_general_ci),
-- every comparison fails with "Illegal mix of collations". This line makes
-- them equal, whatever collation the database was created with.
SET collation_connection = @@collation_database;

-- ---------------------------------------------------------------------
-- VIEWS
-- ---------------------------------------------------------------------

-- Tutors that students are allowed to see and book (FR-03)
CREATE OR REPLACE VIEW vw_approved_tutors AS
SELECT u.user_id        AS tutor_id,
       u.full_name,
       u.email,
       un.name          AS university,
       tp.headline,
       tp.bio,
       tp.degree,
       tp.major,
       tp.hourly_rate,
       tp.average_rating,
       tp.review_count
FROM tutor_profiles tp
JOIN users u               ON u.user_id = tp.user_id
LEFT JOIN universities un  ON un.university_id = u.university_id
WHERE tp.is_approved = 1 AND u.status = 'Active';

-- Everything the "Booking details" page needs
CREATE OR REPLACE VIEW vw_booking_details AS
SELECT b.booking_id, b.booking_code, b.status, b.topic, b.notes, b.price,
       b.cancellation_reason, b.created_at, b.updated_at,
       s.slot_id, s.start_at, s.end_at,
       c.course_id, c.code AS course_code, c.name AS course_name,
       b.student_id, st.full_name AS student_name, st.email AS student_email,
       b.tutor_id,   tu.full_name AS tutor_name,   tp.average_rating AS tutor_rating,
       r.review_id, r.rating AS review_rating
FROM bookings b
JOIN availability_slots s ON s.slot_id   = b.slot_id
JOIN courses c            ON c.course_id = b.course_id
JOIN users st             ON st.user_id  = b.student_id
JOIN users tu             ON tu.user_id  = b.tutor_id
JOIN tutor_profiles tp    ON tp.user_id  = b.tutor_id
LEFT JOIN reviews r       ON r.booking_id = b.booking_id;

-- Numbers for the admin dashboard (FR-13)
CREATE OR REPLACE VIEW vw_admin_dashboard AS
SELECT
  (SELECT COUNT(*) FROM users)                                         AS total_users,
  (SELECT COUNT(*) FROM users WHERE role = 'Student')                  AS students,
  (SELECT COUNT(*) FROM tutor_profiles WHERE is_approved = 1)          AS approved_tutors,
  (SELECT COUNT(*) FROM tutor_applications WHERE status = 'Pending')   AS pending_tutor_applications,
  (SELECT COUNT(*) FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
    WHERE s.start_at >= DATE_FORMAT(CURDATE(), '%Y-%m-01')
      AND b.status IN ('Confirmed','Completed'))                       AS sessions_this_month,
  (SELECT COUNT(*) FROM reports WHERE status = 'Open')                 AS open_reports,
  (SELECT COUNT(*) FROM ai_usage_logs WHERE created_at >= CURDATE())   AS ai_requests_today,
  (SELECT IFNULL(SUM(cost_usd), 0) FROM ai_usage_logs
    WHERE created_at >= DATE_FORMAT(CURDATE(), '%Y-%m-01'))            AS ai_cost_this_month_usd;

-- ---------------------------------------------------------------------
-- FUNCTIONS, PROCEDURES AND TRIGGERS
-- ---------------------------------------------------------------------
DELIMITER $$

-- Read one platform setting
DROP FUNCTION IF EXISTS fn_setting$$
CREATE FUNCTION fn_setting(p_key VARCHAR(80)) RETURNS VARCHAR(400)
READS SQL DATA
BEGIN
  RETURN (SELECT setting_value FROM platform_settings WHERE setting_key = p_key);
END$$

-- Stop if the caller is not an active administrator
DROP PROCEDURE IF EXISTS sp_assert_admin$$
CREATE PROCEDURE sp_assert_admin(IN p_user_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_user_id AND role = 'Admin' AND status = 'Active') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only administrators can perform this action.';
  END IF;
END$$

-- =====================================================================
-- TUTOR RATING: recalculated whenever a review changes
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_recalculate_tutor_rating$$
CREATE PROCEDURE sp_recalculate_tutor_rating(IN p_tutor_id INT)
BEGIN
  UPDATE tutor_profiles
     SET average_rating = (SELECT IFNULL(ROUND(AVG(r.rating), 2), 0) FROM reviews r
                           WHERE r.tutor_id = p_tutor_id AND r.is_hidden = 0),
         review_count   = (SELECT COUNT(*) FROM reviews r
                           WHERE r.tutor_id = p_tutor_id AND r.is_hidden = 0)
   WHERE user_id = p_tutor_id;
END$$

DROP TRIGGER IF EXISTS trg_reviews_after_insert$$
CREATE TRIGGER trg_reviews_after_insert AFTER INSERT ON reviews
FOR EACH ROW CALL sp_recalculate_tutor_rating(NEW.tutor_id)$$

DROP TRIGGER IF EXISTS trg_reviews_after_update$$
CREATE TRIGGER trg_reviews_after_update AFTER UPDATE ON reviews
FOR EACH ROW CALL sp_recalculate_tutor_rating(NEW.tutor_id)$$

DROP TRIGGER IF EXISTS trg_reviews_after_delete$$
CREATE TRIGGER trg_reviews_after_delete AFTER DELETE ON reviews
FOR EACH ROW CALL sp_recalculate_tutor_rating(OLD.tutor_id)$$

-- =====================================================================
-- USERS - full CRUD  (FR-01, FR-02)
-- =====================================================================

-- CREATE: register a new account. p_password_hash comes from the app
-- (PHP password_hash / bcrypt); the database never sees the plain password.
DROP PROCEDURE IF EXISTS sp_user_create$$
CREATE PROCEDURE sp_user_create(
  IN p_full_name VARCHAR(120), IN p_email VARCHAR(255), IN p_password_hash VARCHAR(255),
  IN p_role VARCHAR(20), IN p_university_id INT, IN p_phone VARCHAR(30),
  IN p_department VARCHAR(120), IN p_year_of_study TINYINT, IN p_student_number VARCHAR(30),
  IN p_created_by INT, OUT p_new_user_id INT)
BEGIN
  DECLARE v_email  VARCHAR(255) DEFAULT LOWER(TRIM(p_email));
  DECLARE v_domain VARCHAR(100);
  DECLARE v_role   VARCHAR(20)  DEFAULT IFNULL(p_role, 'Student');
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF v_role NOT IN ('Student','Tutor','Admin') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Unknown role.';
  END IF;
  IF v_role = 'Admin' THEN
    CALL sp_assert_admin(p_created_by);           -- only an admin can create another admin
  END IF;
  -- the Tutor role is only given by approving an application
  IF v_role = 'Tutor' THEN SET v_role = 'Student'; END IF;

  IF v_email NOT LIKE '%_@_%._%' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please enter a valid e-mail address.';
  END IF;
  IF EXISTS (SELECT 1 FROM users WHERE email = v_email) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'An account with this e-mail already exists.';
  END IF;

  -- only university e-mail domains may register (FR-01)
  SET v_domain = SUBSTRING_INDEX(v_email, '@', -1);
  IF v_role <> 'Admin'
     AND IFNULL(fn_setting('RestrictSignupToUniversityEmails'), 'true') = 'true'
     AND NOT EXISTS (SELECT 1 FROM allowed_email_domains WHERE domain = v_domain) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only university e-mail addresses are accepted.';
  END IF;

  START TRANSACTION;
    INSERT INTO users (full_name, email, password_hash, role, status, university_id, phone)
    VALUES (p_full_name, v_email, p_password_hash, v_role,
            IF(fn_setting('RequireEmailVerification') = 'false', 'Active', 'PendingVerification'),
            p_university_id, p_phone);
    SET p_new_user_id = LAST_INSERT_ID();

    IF v_role = 'Student' THEN
      INSERT INTO student_profiles (user_id, department, year_of_study, student_number)
      VALUES (p_new_user_id, p_department, p_year_of_study, p_student_number);
    END IF;

    INSERT INTO user_preferences (user_id) VALUES (p_new_user_id);
  COMMIT;

  CALL sp_user_get_by_id(p_new_user_id);
END$$

-- READ: one user with profile information
DROP PROCEDURE IF EXISTS sp_user_get_by_id$$
CREATE PROCEDURE sp_user_get_by_id(IN p_user_id INT)
BEGIN
  SELECT u.user_id, u.full_name, u.email, u.role, u.status, u.phone, u.avatar_url,
         u.university_id, un.name AS university, u.email_verified, u.ai_access_enabled,
         u.created_at, u.updated_at, u.last_active_at,
         sp.department, sp.year_of_study, sp.student_number, sp.study_streak_days,
         tp.headline, tp.hourly_rate, tp.average_rating, tp.is_approved AS tutor_approved
  FROM users u
  LEFT JOIN universities un     ON un.university_id = u.university_id
  LEFT JOIN student_profiles sp ON sp.user_id = u.user_id
  LEFT JOIN tutor_profiles tp   ON tp.user_id = u.user_id
  WHERE u.user_id = p_user_id;
END$$

-- READ: look-up for log-in (the app checks the hash with password_verify)
DROP PROCEDURE IF EXISTS sp_user_get_by_email$$
CREATE PROCEDURE sp_user_get_by_email(IN p_email VARCHAR(255))
BEGIN
  SELECT user_id, full_name, email, password_hash, role, status, email_verified
  FROM users WHERE email = LOWER(TRIM(p_email));
END$$

-- READ: admin "All users" page with search, filters and paging
DROP PROCEDURE IF EXISTS sp_user_list$$
CREATE PROCEDURE sp_user_list(
  IN p_search VARCHAR(100), IN p_role VARCHAR(20), IN p_status VARCHAR(20),
  IN p_page INT, IN p_page_size INT)
BEGIN
  DECLARE v_size   INT DEFAULT IFNULL(p_page_size, 20);
  DECLARE v_offset INT DEFAULT (IFNULL(p_page, 1) - 1) * IFNULL(p_page_size, 20);

  SELECT u.user_id, u.full_name, u.email, u.role, u.status, un.name AS university,
         u.created_at, u.last_active_at
  FROM users u
  LEFT JOIN universities un ON un.university_id = u.university_id
  WHERE (p_search IS NULL OR u.full_name LIKE CONCAT('%', p_search, '%') OR u.email LIKE CONCAT('%', p_search, '%'))
    AND (p_role   IS NULL OR u.role   = p_role)
    AND (p_status IS NULL OR u.status = p_status)
  ORDER BY u.full_name
  LIMIT v_size OFFSET v_offset;
END$$

-- UPDATE: profile & settings page (the e-mail cannot be changed here)
DROP PROCEDURE IF EXISTS sp_user_update$$
CREATE PROCEDURE sp_user_update(
  IN p_user_id INT, IN p_full_name VARCHAR(120), IN p_phone VARCHAR(30), IN p_avatar_url VARCHAR(400),
  IN p_university_id INT, IN p_department VARCHAR(120), IN p_year_of_study TINYINT, IN p_student_number VARCHAR(30),
  IN p_email_notifications TINYINT, IN p_study_reminders TINYINT, IN p_message_alerts TINYINT, IN p_weekly_report TINYINT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_user_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'User not found.';
  END IF;

  START TRANSACTION;
    UPDATE users
       SET full_name = p_full_name, phone = p_phone, avatar_url = p_avatar_url, university_id = p_university_id
     WHERE user_id = p_user_id;

    UPDATE student_profiles
       SET department = p_department, year_of_study = p_year_of_study, student_number = p_student_number
     WHERE user_id = p_user_id;

    UPDATE user_preferences
       SET email_notifications = IFNULL(p_email_notifications, email_notifications),
           study_reminders     = IFNULL(p_study_reminders, study_reminders),
           message_alerts      = IFNULL(p_message_alerts, message_alerts),
           weekly_report       = IFNULL(p_weekly_report, weekly_report)
     WHERE user_id = p_user_id;
  COMMIT;

  CALL sp_user_get_by_id(p_user_id);
END$$

-- UPDATE (admin): activate / suspend, change role or AI access
DROP PROCEDURE IF EXISTS sp_user_set_status$$
CREATE PROCEDURE sp_user_set_status(
  IN p_admin_id INT, IN p_user_id INT, IN p_status VARCHAR(20), IN p_role VARCHAR(20), IN p_ai_access TINYINT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);

  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_user_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'User not found.';
  END IF;
  IF p_admin_id = p_user_id AND p_status <> 'Active' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You cannot suspend your own account.';
  END IF;

  START TRANSACTION;
    UPDATE users
       SET status = p_status,
           role = IFNULL(p_role, role),
           ai_access_enabled = IFNULL(p_ai_access, ai_access_enabled)
     WHERE user_id = p_user_id;

    INSERT INTO audit_logs (admin_id, action, target_table, target_id, details)
    VALUES (p_admin_id, 'User status changed', 'users', p_user_id,
            CONCAT('status=', p_status, '; role=', IFNULL(p_role, '(unchanged)')));
  COMMIT;
END$$

-- DELETE: remove an account with no learning or booking history.
-- Accounts with history must be suspended instead, so records stay consistent.
DROP PROCEDURE IF EXISTS sp_user_delete$$
CREATE PROCEDURE sp_user_delete(IN p_requested_by INT, IN p_user_id INT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF p_requested_by <> p_user_id THEN
    CALL sp_assert_admin(p_requested_by);
  ELSEIF IFNULL(fn_setting('AllowUsersToDeleteOwnAccount'), 'false') <> 'true' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please contact an administrator to delete your account.';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_user_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'User not found.';
  END IF;

  IF EXISTS (SELECT 1 FROM bookings WHERE student_id = p_user_id OR tutor_id = p_user_id)
     OR EXISTS (SELECT 1 FROM quiz_attempts WHERE student_id = p_user_id)
     OR EXISTS (SELECT 1 FROM tutor_applications WHERE applicant_id = p_user_id)
     OR EXISTS (SELECT 1 FROM course_materials WHERE uploaded_by = p_user_id)
     OR EXISTS (SELECT 1 FROM enrollments WHERE student_id = p_user_id) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'This user has activity on the platform. Suspend the account instead of deleting it.';
  END IF;

  START TRANSACTION;
    DELETE FROM messages WHERE sender_id = p_user_id OR receiver_id = p_user_id;
    DELETE FROM reports  WHERE reporter_id = p_user_id OR reported_user_id = p_user_id;
    DELETE FROM study_recommendations WHERE student_id = p_user_id;
    DELETE FROM quizzes WHERE student_id = p_user_id;      -- unused generated quizzes (no attempts)
    UPDATE tutor_applications SET reviewed_by = NULL WHERE reviewed_by = p_user_id;
    DELETE FROM users WHERE user_id = p_user_id;   -- profiles, preferences, tokens, notifications cascade

    IF p_requested_by <> p_user_id THEN
      INSERT INTO audit_logs (admin_id, action, target_table, target_id)
      VALUES (p_requested_by, 'User deleted', 'users', p_user_id);
    END IF;
  COMMIT;
END$$

-- Change the password of a logged-in user (the app has already checked the
-- current password). p_password_hash is a bcrypt hash made by the application.
DROP PROCEDURE IF EXISTS sp_user_set_password$$
CREATE PROCEDURE sp_user_set_password(IN p_user_id INT, IN p_password_hash VARCHAR(255))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_user_id AND status = 'Active') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'User not found.';
  END IF;
  UPDATE users SET password_hash = p_password_hash WHERE user_id = p_user_id;
END$$

-- Create a password-reset link for an e-mail address. Returns no row when the
-- address is unknown or the account cannot log in, so the application can show
-- the same message either way (it never reveals which e-mails are registered).
DROP PROCEDURE IF EXISTS sp_password_reset_request$$
CREATE PROCEDURE sp_password_reset_request(IN p_email VARCHAR(255), IN p_token VARCHAR(128))
BEGIN
  DECLARE v_user_id INT DEFAULT NULL;
  DECLARE v_full_name VARCHAR(120);

  SELECT user_id, full_name INTO v_user_id, v_full_name
  FROM users WHERE email = LOWER(TRIM(p_email)) AND status = 'Active';

  IF v_user_id IS NOT NULL THEN
    -- an older unused link stops working when a new one is requested
    UPDATE verification_tokens SET used_at = NOW()
     WHERE user_id = v_user_id AND purpose = 'PasswordReset' AND used_at IS NULL;

    INSERT INTO verification_tokens (user_id, token, purpose, expires_at)
    VALUES (v_user_id, p_token, 'PasswordReset', NOW() + INTERVAL 30 MINUTE);

    SELECT v_user_id AS user_id, v_full_name AS full_name, LOWER(TRIM(p_email)) AS email;
  END IF;
END$$

-- Check a reset link: returns the e-mail of its account, or nothing when the
-- link is unknown, already used or expired.
DROP PROCEDURE IF EXISTS sp_password_reset_check$$
CREATE PROCEDURE sp_password_reset_check(IN p_token VARCHAR(128))
BEGIN
  SELECT u.email
  FROM verification_tokens t JOIN users u ON u.user_id = t.user_id
  WHERE t.token = p_token AND t.purpose = 'PasswordReset'
    AND t.used_at IS NULL AND t.expires_at > NOW() AND u.status = 'Active';
END$$

-- Set a new password with a reset link. The link works only once, and the
-- failed log-ins of the account are cleared so a locked user can log in again.
DROP PROCEDURE IF EXISTS sp_password_reset_complete$$
CREATE PROCEDURE sp_password_reset_complete(IN p_token VARCHAR(128), IN p_password_hash VARCHAR(255))
BEGIN
  DECLARE v_user_id INT DEFAULT NULL;
  DECLARE v_email VARCHAR(255);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT t.user_id, u.email INTO v_user_id, v_email
    FROM verification_tokens t JOIN users u ON u.user_id = t.user_id
    WHERE t.token = p_token AND t.purpose = 'PasswordReset'
      AND t.used_at IS NULL AND t.expires_at > NOW() AND u.status = 'Active'
    FOR UPDATE;

    IF v_user_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This password reset link is invalid or has expired. Please request a new one.';
    END IF;

    UPDATE users SET password_hash = p_password_hash WHERE user_id = v_user_id;
    UPDATE verification_tokens SET used_at = NOW() WHERE token = p_token;
    DELETE FROM login_attempts WHERE email = v_email AND succeeded = 0;

    INSERT INTO notifications (user_id, type, title, body)
    VALUES (v_user_id, 'System', 'Password changed', 'Your password was reset. If this was not you, contact support.');
  COMMIT;
END$$

-- Record a log-in attempt; returns recent failures so the app can lock the account
DROP PROCEDURE IF EXISTS sp_login_attempt_record$$
CREATE PROCEDURE sp_login_attempt_record(
  IN p_email VARCHAR(255), IN p_succeeded TINYINT, IN p_ip VARCHAR(45), IN p_device VARCHAR(200))
BEGIN
  DECLARE v_user_id INT DEFAULT NULL;
  SELECT user_id INTO v_user_id FROM users WHERE email = LOWER(p_email) LIMIT 1;

  INSERT INTO login_attempts (user_id, email, succeeded, ip_address, device)
  VALUES (v_user_id, LOWER(p_email), p_succeeded, p_ip, p_device);

  IF p_succeeded = 1 AND v_user_id IS NOT NULL THEN
    UPDATE users SET last_active_at = NOW() WHERE user_id = v_user_id;
  END IF;

  SELECT COUNT(*) AS recent_failures,
         CAST(fn_setting('MaxFailedLoginAttempts') AS UNSIGNED) AS max_allowed
  FROM login_attempts
  WHERE email = LOWER(p_email) AND succeeded = 0
    AND attempted_at > NOW() - INTERVAL 15 MINUTE;
END$$

-- =====================================================================
-- COURSES - full CRUD (admin course library)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_course_create$$
CREATE PROCEDURE sp_course_create(
  IN p_admin_id INT, IN p_code VARCHAR(20), IN p_name VARCHAR(150),
  IN p_department VARCHAR(120), IN p_professor VARCHAR(120), IN p_university_id INT,
  OUT p_new_course_id INT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF EXISTS (SELECT 1 FROM courses WHERE code = UPPER(p_code)) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A course with this code already exists.';
  END IF;

  START TRANSACTION;
    INSERT INTO courses (code, name, department, professor, university_id)
    VALUES (UPPER(p_code), p_name, p_department, p_professor, p_university_id);
    SET p_new_course_id = LAST_INSERT_ID();

    INSERT INTO audit_logs (admin_id, action, target_table, target_id, details)
    VALUES (p_admin_id, 'Course created', 'courses', p_new_course_id, p_name);
  COMMIT;

  CALL sp_course_get_by_id(p_new_course_id);
END$$

DROP PROCEDURE IF EXISTS sp_course_get_by_id$$
CREATE PROCEDURE sp_course_get_by_id(IN p_course_id INT)
BEGIN
  SELECT c.course_id, c.code, c.name, c.department, c.professor, c.is_active,
         un.name AS university, c.created_at, c.updated_at,
         (SELECT COUNT(*) FROM enrollments e WHERE e.course_id = c.course_id)      AS students,
         (SELECT COUNT(*) FROM tutor_courses t WHERE t.course_id = c.course_id)    AS tutors,
         (SELECT COUNT(*) FROM course_materials m WHERE m.course_id = c.course_id) AS materials,
         (SELECT GROUP_CONCAT(t.name ORDER BY t.name SEPARATOR ', ')
            FROM topics t WHERE t.course_id = c.course_id)                         AS topics
  FROM courses c
  LEFT JOIN universities un ON un.university_id = c.university_id
  WHERE c.course_id = p_course_id;
END$$

DROP PROCEDURE IF EXISTS sp_course_list$$
CREATE PROCEDURE sp_course_list(IN p_search VARCHAR(100), IN p_department VARCHAR(120), IN p_include_inactive TINYINT)
BEGIN
  SELECT c.course_id, c.code, c.name, c.department, c.professor, c.is_active,
         (SELECT COUNT(*) FROM enrollments e WHERE e.course_id = c.course_id)      AS students,
         (SELECT COUNT(*) FROM course_materials m WHERE m.course_id = c.course_id) AS materials
  FROM courses c
  WHERE (p_search IS NULL OR c.name LIKE CONCAT('%', p_search, '%') OR c.code LIKE CONCAT('%', p_search, '%'))
    AND (p_department IS NULL OR c.department = p_department)
    AND (IFNULL(p_include_inactive, 0) = 1 OR c.is_active = 1)
  ORDER BY c.name;
END$$

DROP PROCEDURE IF EXISTS sp_course_update$$
CREATE PROCEDURE sp_course_update(
  IN p_admin_id INT, IN p_course_id INT, IN p_code VARCHAR(20), IN p_name VARCHAR(150),
  IN p_department VARCHAR(120), IN p_professor VARCHAR(120), IN p_university_id INT, IN p_is_active TINYINT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF NOT EXISTS (SELECT 1 FROM courses WHERE course_id = p_course_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Course not found.';
  END IF;
  IF EXISTS (SELECT 1 FROM courses WHERE code = UPPER(p_code) AND course_id <> p_course_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A course with this code already exists.';
  END IF;

  START TRANSACTION;
    UPDATE courses
       SET code = UPPER(p_code), name = p_name, department = p_department, professor = p_professor,
           university_id = p_university_id, is_active = IFNULL(p_is_active, 1)
     WHERE course_id = p_course_id;

    INSERT INTO audit_logs (admin_id, action, target_table, target_id, details)
    VALUES (p_admin_id, 'Course updated', 'courses', p_course_id, p_name);
  COMMIT;

  CALL sp_course_get_by_id(p_course_id);
END$$

-- DELETE: unused courses are removed; courses in use are deactivated so that
-- bookings, quizzes and enrolments keep their history.
DROP PROCEDURE IF EXISTS sp_course_delete$$
CREATE PROCEDURE sp_course_delete(IN p_admin_id INT, IN p_course_id INT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF NOT EXISTS (SELECT 1 FROM courses WHERE course_id = p_course_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Course not found.';
  END IF;

  START TRANSACTION;
  IF EXISTS (SELECT 1 FROM enrollments WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM bookings WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM tutor_courses WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM course_materials WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM quizzes WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM application_courses WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM ai_conversations WHERE course_id = p_course_id)
     OR EXISTS (SELECT 1 FROM study_recommendations WHERE course_id = p_course_id) THEN
    UPDATE courses SET is_active = 0 WHERE course_id = p_course_id;
    INSERT INTO audit_logs (admin_id, action, target_table, target_id)
    VALUES (p_admin_id, 'Course deactivated (in use)', 'courses', p_course_id);
    SELECT 'Deactivated' AS result, 'The course is in use, so it was hidden instead of deleted.' AS message;
  ELSE
    DELETE FROM courses WHERE course_id = p_course_id;     -- its topics cascade
    INSERT INTO audit_logs (admin_id, action, target_table, target_id)
    VALUES (p_admin_id, 'Course deleted', 'courses', p_course_id);
    SELECT 'Deleted' AS result, 'The course was deleted.' AS message;
  END IF;
  COMMIT;
END$$

-- =====================================================================
-- TUTOR SEARCH & PROFILE  (FR-03, FR-04)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_tutor_search$$
CREATE PROCEDURE sp_tutor_search(
  IN p_course_id INT, IN p_search VARCHAR(100), IN p_min_rating DECIMAL(3,2),
  IN p_max_hourly_rate DECIMAL(8,2), IN p_available_within_days INT)   -- 0 = today, 7 = this week
BEGIN
  SELECT t.tutor_id, t.full_name, t.university, t.headline, t.degree, t.major,
         t.hourly_rate, t.average_rating, t.review_count,
         (SELECT GROUP_CONCAT(c.name ORDER BY c.name SEPARATOR ', ')
            FROM tutor_courses tc JOIN courses c ON c.course_id = tc.course_id
           WHERE tc.tutor_id = t.tutor_id) AS courses,
         (SELECT MIN(s.start_at) FROM availability_slots s
           WHERE s.tutor_id = t.tutor_id AND s.is_open = 1 AND s.start_at > NOW()
             AND NOT EXISTS (SELECT 1 FROM bookings b WHERE b.active_slot_id = s.slot_id)) AS next_available
  FROM vw_approved_tutors t
  WHERE (p_course_id IS NULL OR EXISTS (SELECT 1 FROM tutor_courses tc
                                        WHERE tc.tutor_id = t.tutor_id AND tc.course_id = p_course_id))
    AND (p_search IS NULL OR t.full_name LIKE CONCAT('%', p_search, '%')
         OR EXISTS (SELECT 1 FROM tutor_courses tc JOIN courses c ON c.course_id = tc.course_id
                    WHERE tc.tutor_id = t.tutor_id AND c.name LIKE CONCAT('%', p_search, '%')))
    AND (p_min_rating IS NULL OR t.average_rating >= p_min_rating)
    AND (p_max_hourly_rate IS NULL OR t.hourly_rate <= p_max_hourly_rate)
  HAVING (p_available_within_days IS NULL
          OR next_available < CURDATE() + INTERVAL (p_available_within_days + 1) DAY)
  ORDER BY t.average_rating DESC, t.review_count DESC;
END$$

-- Tutor profile page: 4 result sets (profile, courses, free slots, reviews)
DROP PROCEDURE IF EXISTS sp_tutor_get_profile$$
CREATE PROCEDURE sp_tutor_get_profile(IN p_tutor_id INT)
BEGIN
  SELECT * FROM vw_approved_tutors WHERE tutor_id = p_tutor_id;

  SELECT c.course_id, c.code, c.name
  FROM tutor_courses tc JOIN courses c ON c.course_id = tc.course_id
  WHERE tc.tutor_id = p_tutor_id ORDER BY c.name;

  SELECT s.slot_id, s.start_at, s.end_at
  FROM availability_slots s
  WHERE s.tutor_id = p_tutor_id AND s.is_open = 1 AND s.start_at > NOW()
    AND NOT EXISTS (SELECT 1 FROM bookings b WHERE b.active_slot_id = s.slot_id)
  ORDER BY s.start_at;

  SELECT r.review_id, u.full_name AS student_name, r.rating, r.comment, r.tutor_reply, r.created_at
  FROM reviews r JOIN users u ON u.user_id = r.student_id
  WHERE r.tutor_id = p_tutor_id AND r.is_hidden = 0
  ORDER BY r.created_at DESC;
END$$

DROP PROCEDURE IF EXISTS sp_tutor_update_profile$$
CREATE PROCEDURE sp_tutor_update_profile(
  IN p_tutor_id INT, IN p_headline VARCHAR(150), IN p_bio VARCHAR(1000), IN p_hourly_rate DECIMAL(8,2),
  IN p_accept_same_day TINYINT, IN p_buffer_minutes SMALLINT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM tutor_profiles WHERE user_id = p_tutor_id AND is_approved = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only approved tutors can edit a tutor profile.';
  END IF;
  UPDATE tutor_profiles
     SET headline = p_headline, bio = p_bio, hourly_rate = p_hourly_rate,
         accept_same_day = IFNULL(p_accept_same_day, accept_same_day),
         buffer_minutes  = IFNULL(p_buffer_minutes, buffer_minutes)
   WHERE user_id = p_tutor_id;
END$$

-- =====================================================================
-- AVAILABILITY  (FR-07)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_slot_add$$
CREATE PROCEDURE sp_slot_add(IN p_tutor_id INT, IN p_start_at DATETIME, IN p_end_at DATETIME, OUT p_new_slot_id INT)
BEGIN
  DECLARE v_overlaps INT DEFAULT 0;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF NOT EXISTS (SELECT 1 FROM tutor_profiles WHERE user_id = p_tutor_id AND is_approved = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only approved tutors can add availability.';
  END IF;
  IF p_end_at <= p_start_at THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'The end time must be after the start time.';
  END IF;
  IF p_start_at <= NOW() THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Availability must be in the future.';
  END IF;

  START TRANSACTION;
    -- no overlapping slots for the same tutor (rows are locked while we check)
    SELECT COUNT(*) INTO v_overlaps FROM availability_slots
    WHERE tutor_id = p_tutor_id AND start_at < p_end_at AND end_at > p_start_at
    FOR UPDATE;
    IF v_overlaps > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time overlaps another of your slots.';
    END IF;

    INSERT INTO availability_slots (tutor_id, start_at, end_at) VALUES (p_tutor_id, p_start_at, p_end_at);
    SET p_new_slot_id = LAST_INSERT_ID();
  COMMIT;
END$$

DROP PROCEDURE IF EXISTS sp_slot_remove$$
CREATE PROCEDURE sp_slot_remove(IN p_tutor_id INT, IN p_slot_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM availability_slots WHERE slot_id = p_slot_id AND tutor_id = p_tutor_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Slot not found.';
  END IF;
  IF EXISTS (SELECT 1 FROM bookings WHERE active_slot_id = p_slot_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This slot has an active booking. Cancel or decline the booking first.';
  END IF;

  IF EXISTS (SELECT 1 FROM bookings WHERE slot_id = p_slot_id) THEN
    UPDATE availability_slots SET is_open = 0 WHERE slot_id = p_slot_id;   -- keep it for booking history
  ELSE
    DELETE FROM availability_slots WHERE slot_id = p_slot_id;
  END IF;
END$$

-- =====================================================================
-- BOOKINGS - full CRUD  (FR-05, FR-07, NFR-05)
-- =====================================================================

-- CREATE: request a session. The slot row is locked (FOR UPDATE) inside a
-- transaction, and the UNIQUE key on bookings.active_slot_id is the final
-- guarantee, so two students can never both book the same slot.
DROP PROCEDURE IF EXISTS sp_booking_create$$
CREATE PROCEDURE sp_booking_create(
  IN p_student_id INT, IN p_slot_id INT, IN p_course_id INT,
  IN p_topic VARCHAR(150), IN p_notes VARCHAR(1000), OUT p_new_booking_id INT)
BEGIN
  DECLARE v_tutor_id INT DEFAULT NULL;
  DECLARE v_start DATETIME;
  DECLARE v_end DATETIME;
  DECLARE v_open TINYINT;
  DECLARE v_rate DECIMAL(8,2);
  DECLARE v_same_day TINYINT;
  -- duplicate key on active_slot_id = someone else booked the slot at the same moment
  DECLARE EXIT HANDLER FOR 1062
  BEGIN
    ROLLBACK;
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot is no longer available. Please choose another time.';
  END;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_student_id AND status = 'Active') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only active users can book sessions.';
  END IF;

  START TRANSACTION;
    SELECT s.tutor_id, s.start_at, s.end_at, s.is_open, tp.hourly_rate, tp.accept_same_day
      INTO v_tutor_id, v_start, v_end, v_open, v_rate, v_same_day
    FROM availability_slots s
    JOIN tutor_profiles tp ON tp.user_id = s.tutor_id AND tp.is_approved = 1
    JOIN users tu          ON tu.user_id = tp.user_id AND tu.status = 'Active'
    WHERE s.slot_id = p_slot_id
    FOR UPDATE;

    IF v_tutor_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot does not exist.';
    END IF;
    IF v_tutor_id = p_student_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You cannot book a session with yourself.';
    END IF;
    IF v_open = 0 OR v_start <= NOW() THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot is no longer available. Please choose another time.';
    END IF;
    IF v_same_day = 0 AND DATE(v_start) = CURDATE() THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This tutor does not accept same-day bookings.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM tutor_courses WHERE tutor_id = v_tutor_id AND course_id = p_course_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This tutor does not teach the selected course.';
    END IF;
    IF EXISTS (SELECT 1 FROM bookings WHERE active_slot_id = p_slot_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot is no longer available. Please choose another time.';
    END IF;
    IF EXISTS (SELECT 1 FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
               WHERE b.student_id = p_student_id AND b.status IN ('Pending','Confirmed')
                 AND s.start_at < v_end AND s.end_at > v_start) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You already have another session at this time.';
    END IF;

    INSERT INTO bookings (booking_code, student_id, tutor_id, slot_id, course_id, topic, notes, status, price)
    VALUES (UUID(), p_student_id, v_tutor_id, p_slot_id, p_course_id, p_topic, p_notes, 'Pending',
            ROUND(v_rate * TIMESTAMPDIFF(MINUTE, v_start, v_end) / 60, 2));
    SET p_new_booking_id = LAST_INSERT_ID();
    UPDATE bookings SET booking_code = CONCAT('PM-', 20000 + p_new_booking_id) WHERE booking_id = p_new_booking_id;

    INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, note)
    VALUES (p_new_booking_id, NULL, 'Pending', p_student_id, 'Booking requested');

    INSERT INTO notifications (user_id, type, title, body, link_url)
    SELECT v_tutor_id, 'Booking', 'New booking request',
           CONCAT(u.full_name, ' requested a session on ', DATE_FORMAT(v_start, '%a %d %b %H:%i')),
           '/tutor/requests'
    FROM users u WHERE u.user_id = p_student_id;
  COMMIT;

  SELECT * FROM vw_booking_details WHERE booking_id = p_new_booking_id;
END$$

-- READ: one booking (only its student, its tutor or an admin may see it)
DROP PROCEDURE IF EXISTS sp_booking_get_by_id$$
CREATE PROCEDURE sp_booking_get_by_id(IN p_booking_id INT, IN p_requested_by INT)
BEGIN
  SELECT d.*
  FROM vw_booking_details d
  WHERE d.booking_id = p_booking_id
    AND (d.student_id = p_requested_by OR d.tutor_id = p_requested_by
         OR EXISTS (SELECT 1 FROM users WHERE user_id = p_requested_by AND role = 'Admin'));

  SELECT h.old_status, h.new_status, u.full_name AS changed_by, h.note, h.changed_at
  FROM booking_status_history h
  JOIN bookings b ON b.booking_id = h.booking_id
  LEFT JOIN users u ON u.user_id = h.changed_by
  WHERE h.booking_id = p_booking_id
    AND (b.student_id = p_requested_by OR b.tutor_id = p_requested_by
         OR EXISTS (SELECT 1 FROM users WHERE user_id = p_requested_by AND role = 'Admin'))
  ORDER BY h.changed_at, h.history_id;
END$$

-- READ: "My bookings" tabs. p_status: NULL (all), 'Upcoming', 'Completed', 'Cancelled', ...
DROP PROCEDURE IF EXISTS sp_booking_list_by_student$$
CREATE PROCEDURE sp_booking_list_by_student(IN p_student_id INT, IN p_status VARCHAR(20))
BEGIN
  SELECT booking_id, booking_code, status, course_name, tutor_id, tutor_name,
         start_at, end_at, price, topic, review_id
  FROM vw_booking_details
  WHERE student_id = p_student_id
    AND (p_status IS NULL OR status = p_status
         OR (p_status = 'Upcoming' AND status IN ('Pending','Confirmed')))
  ORDER BY start_at DESC;
END$$

DROP PROCEDURE IF EXISTS sp_booking_list_by_tutor$$
CREATE PROCEDURE sp_booking_list_by_tutor(IN p_tutor_id INT, IN p_status VARCHAR(20))
BEGIN
  SELECT booking_id, booking_code, status, course_name, student_id, student_name,
         start_at, end_at, price, topic, notes
  FROM vw_booking_details
  WHERE tutor_id = p_tutor_id
    AND (p_status IS NULL OR status = p_status
         OR (p_status = 'Upcoming' AND status IN ('Pending','Confirmed')))
  ORDER BY start_at;
END$$

-- UPDATE: student edits topic / notes of an upcoming booking
DROP PROCEDURE IF EXISTS sp_booking_update$$
CREATE PROCEDURE sp_booking_update(IN p_booking_id INT, IN p_student_id INT, IN p_topic VARCHAR(150), IN p_notes VARCHAR(1000))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM bookings WHERE booking_id = p_booking_id AND student_id = p_student_id
                 AND status IN ('Pending','Confirmed')) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found, or it can no longer be changed.';
  END IF;
  UPDATE bookings SET topic = p_topic, notes = p_notes WHERE booking_id = p_booking_id;
  SELECT * FROM vw_booking_details WHERE booking_id = p_booking_id;
END$$

-- UPDATE: tutor accepts (p_accept = 1) or declines (0) a pending request
DROP PROCEDURE IF EXISTS sp_booking_respond$$
CREATE PROCEDURE sp_booking_respond(IN p_booking_id INT, IN p_tutor_id INT, IN p_accept TINYINT)
BEGIN
  DECLARE v_student_id INT DEFAULT NULL;
  DECLARE v_new VARCHAR(20) DEFAULT IF(p_accept = 1, 'Confirmed', 'Declined');
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT student_id INTO v_student_id FROM bookings
    WHERE booking_id = p_booking_id AND tutor_id = p_tutor_id AND status = 'Pending'
    FOR UPDATE;
    IF v_student_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only your pending booking requests can be accepted or declined.';
    END IF;

    UPDATE bookings SET status = v_new WHERE booking_id = p_booking_id;

    INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by)
    VALUES (p_booking_id, 'Pending', v_new, p_tutor_id);

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_student_id, 'Booking',
            IF(p_accept = 1, 'Booking confirmed', 'Booking declined'),
            IF(p_accept = 1, 'Your tutor confirmed the session.',
                             'Your tutor could not take this session. Please choose another time.'),
            CONCAT('/bookings/', p_booking_id));
  COMMIT;
END$$

-- UPDATE: student moves the booking to another free slot of the same tutor
DROP PROCEDURE IF EXISTS sp_booking_reschedule$$
CREATE PROCEDURE sp_booking_reschedule(IN p_booking_id INT, IN p_student_id INT, IN p_new_slot_id INT)
BEGIN
  DECLARE v_tutor_id INT DEFAULT NULL;
  DECLARE v_old_status VARCHAR(20);
  DECLARE v_slot_ok INT DEFAULT 0;
  DECLARE EXIT HANDLER FOR 1062
  BEGIN
    ROLLBACK;
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot is no longer available. Please choose another time.';
  END;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT tutor_id, status INTO v_tutor_id, v_old_status
    FROM bookings WHERE booking_id = p_booking_id AND student_id = p_student_id
    FOR UPDATE;

    IF v_tutor_id IS NULL OR v_old_status NOT IN ('Pending','Confirmed') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only your upcoming bookings can be rescheduled.';
    END IF;
    SELECT COUNT(*) INTO v_slot_ok FROM availability_slots
    WHERE slot_id = p_new_slot_id AND tutor_id = v_tutor_id AND is_open = 1 AND start_at > NOW()
    FOR UPDATE;
    IF v_slot_ok = 0 OR EXISTS (SELECT 1 FROM bookings WHERE active_slot_id = p_new_slot_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This time slot is no longer available. Please choose another time.';
    END IF;

    UPDATE bookings SET slot_id = p_new_slot_id, status = 'Pending' WHERE booking_id = p_booking_id;

    INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, note)
    VALUES (p_booking_id, v_old_status, 'Pending', p_student_id, 'Rescheduled - waiting for tutor confirmation');

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_tutor_id, 'Booking', 'Booking rescheduled', 'A student moved a session to a new time.', '/tutor/requests');
  COMMIT;
END$$

-- UPDATE: cancel (student or tutor). Cancelling frees the slot automatically,
-- because active_slot_id becomes NULL when the status is no longer active.
DROP PROCEDURE IF EXISTS sp_booking_cancel$$
CREATE PROCEDURE sp_booking_cancel(IN p_booking_id INT, IN p_user_id INT, IN p_reason VARCHAR(500))
BEGIN
  DECLARE v_student_id INT DEFAULT NULL;
  DECLARE v_tutor_id INT;
  DECLARE v_status VARCHAR(20);
  DECLARE v_start DATETIME;
  DECLARE v_window INT DEFAULT IFNULL(CAST(fn_setting('BookingCancellationWindowHours') AS UNSIGNED), 24);
  DECLARE v_msg VARCHAR(300);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT b.student_id, b.tutor_id, b.status, s.start_at
      INTO v_student_id, v_tutor_id, v_status, v_start
    FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
    WHERE b.booking_id = p_booking_id
    FOR UPDATE;

    IF v_student_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found.';
    END IF;
    IF p_user_id NOT IN (v_student_id, v_tutor_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You can only cancel your own bookings.';
    END IF;
    IF v_status NOT IN ('Pending','Confirmed') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending or confirmed bookings can be cancelled.';
    END IF;
    IF p_user_id = v_student_id AND v_status = 'Confirmed' AND v_start < NOW() + INTERVAL v_window HOUR THEN
      SET v_msg = CONCAT('Confirmed sessions can only be cancelled up to ', v_window,
                         ' hours before they start. Please message your tutor.');
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
    END IF;

    UPDATE bookings SET status = 'Cancelled', cancellation_reason = p_reason WHERE booking_id = p_booking_id;

    INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, note)
    VALUES (p_booking_id, v_status, 'Cancelled', p_user_id, p_reason);

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (IF(p_user_id = v_student_id, v_tutor_id, v_student_id), 'Booking', 'Session cancelled',
            IFNULL(p_reason, 'A session was cancelled.'), CONCAT('/bookings/', p_booking_id));
  COMMIT;
END$$

-- UPDATE: tutor marks a finished session as completed (then it can be reviewed)
DROP PROCEDURE IF EXISTS sp_booking_complete$$
CREATE PROCEDURE sp_booking_complete(IN p_booking_id INT, IN p_tutor_id INT)
BEGIN
  DECLARE v_student_id INT DEFAULT NULL;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT b.student_id INTO v_student_id
    FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id
    WHERE b.booking_id = p_booking_id AND b.tutor_id = p_tutor_id
      AND b.status = 'Confirmed' AND s.end_at <= NOW()
    FOR UPDATE;
    IF v_student_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only confirmed sessions that have already ended can be completed.';
    END IF;

    UPDATE bookings SET status = 'Completed' WHERE booking_id = p_booking_id;

    INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by)
    VALUES (p_booking_id, 'Confirmed', 'Completed', p_tutor_id);

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_student_id, 'Review', 'Rate your session', 'How was your tutoring session?', CONCAT('/bookings/', p_booking_id));
  COMMIT;
END$$

-- DELETE (admin only): permanently remove a booking and its review/history
DROP PROCEDURE IF EXISTS sp_booking_delete$$
CREATE PROCEDURE sp_booking_delete(IN p_admin_id INT, IN p_booking_id INT)
BEGIN
  DECLARE v_tutor_id INT DEFAULT NULL;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);

  SELECT tutor_id INTO v_tutor_id FROM bookings WHERE booking_id = p_booking_id;
  IF v_tutor_id IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found.';
  END IF;

  START TRANSACTION;
    -- reports about this booking's review: keep those that also name a user
    UPDATE reports SET reported_review_id = NULL
     WHERE reported_user_id IS NOT NULL
       AND reported_review_id IN (SELECT review_id FROM reviews WHERE booking_id = p_booking_id);
    DELETE FROM reports
     WHERE reported_review_id IN (SELECT review_id FROM reviews WHERE booking_id = p_booking_id);

    DELETE FROM bookings WHERE booking_id = p_booking_id;   -- review + history cascade, messages keep text
    CALL sp_recalculate_tutor_rating(v_tutor_id);           -- cascaded deletes do not fire triggers

    INSERT INTO audit_logs (admin_id, action, target_table, target_id)
    VALUES (p_admin_id, 'Booking deleted', 'bookings', p_booking_id);
  COMMIT;
END$$

-- =====================================================================
-- REVIEWS  (FR-12)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_review_create$$
CREATE PROCEDURE sp_review_create(
  IN p_booking_id INT, IN p_student_id INT, IN p_rating TINYINT, IN p_comment VARCHAR(1000), OUT p_new_review_id INT)
BEGIN
  DECLARE v_tutor_id INT DEFAULT NULL;
  DECLARE v_status VARCHAR(20);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  SELECT tutor_id, status INTO v_tutor_id, v_status
  FROM bookings WHERE booking_id = p_booking_id AND student_id = p_student_id;

  IF v_tutor_id IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You can only review your own sessions.';
  END IF;
  IF v_status <> 'Completed' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You can review a session only after it has been completed.';
  END IF;
  IF EXISTS (SELECT 1 FROM reviews WHERE booking_id = p_booking_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You have already reviewed this session.';
  END IF;
  IF p_rating NOT BETWEEN 1 AND 5 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'The rating must be between 1 and 5.';
  END IF;

  START TRANSACTION;
    INSERT INTO reviews (booking_id, student_id, tutor_id, rating, comment)
    VALUES (p_booking_id, p_student_id, v_tutor_id, p_rating, p_comment);   -- trigger updates the tutor rating
    SET p_new_review_id = LAST_INSERT_ID();

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_tutor_id, 'Review', 'New review', CONCAT(p_rating, '-star review received.'), '/tutor/reviews');
  COMMIT;
END$$

DROP PROCEDURE IF EXISTS sp_review_reply$$
CREATE PROCEDURE sp_review_reply(IN p_review_id INT, IN p_tutor_id INT, IN p_reply VARCHAR(1000))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM reviews WHERE review_id = p_review_id AND tutor_id = p_tutor_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Review not found.';
  END IF;
  UPDATE reviews SET tutor_reply = p_reply WHERE review_id = p_review_id;
END$$

-- =====================================================================
-- TUTOR APPLICATIONS  (FR-06, FR-13, Scenarios 4 and 5)
-- Flow: sp_application_create (Draft) -> sp_application_add_course (1..n)
--       -> sp_application_add_document -> sp_application_submit (Pending)
--       -> admin: sp_application_approve / _reject / _request_info
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_application_create$$
CREATE PROCEDURE sp_application_create(
  IN p_applicant_id INT, IN p_university_id INT, IN p_degree VARCHAR(5), IN p_major VARCHAR(120),
  IN p_gpa DECIMAL(3,2), IN p_year_of_study TINYINT, IN p_bio VARCHAR(1000), OUT p_new_application_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_applicant_id AND status = 'Active' AND email_verified = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please verify your university e-mail before applying.';
  END IF;
  IF EXISTS (SELECT 1 FROM tutor_applications
             WHERE applicant_id = p_applicant_id AND status IN ('Draft','Pending','InfoRequested')) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You already have an open tutor application.';
  END IF;

  INSERT INTO tutor_applications (applicant_id, university_id, degree, major, gpa, year_of_study, bio, status)
  VALUES (p_applicant_id, p_university_id, p_degree, p_major, p_gpa, p_year_of_study, p_bio, 'Draft');
  SET p_new_application_id = LAST_INSERT_ID();
END$$

DROP PROCEDURE IF EXISTS sp_application_add_course$$
CREATE PROCEDURE sp_application_add_course(
  IN p_application_id INT, IN p_applicant_id INT, IN p_course_id INT, IN p_grade VARCHAR(5))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM tutor_applications WHERE application_id = p_application_id
                 AND applicant_id = p_applicant_id AND status IN ('Draft','InfoRequested')) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Application not found or already submitted.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM courses WHERE course_id = p_course_id AND is_active = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Course not found.';
  END IF;
  INSERT INTO application_courses (application_id, course_id, grade_obtained)
  VALUES (p_application_id, p_course_id, p_grade)
  ON DUPLICATE KEY UPDATE grade_obtained = p_grade;
END$$

DROP PROCEDURE IF EXISTS sp_application_add_document$$
CREATE PROCEDURE sp_application_add_document(
  IN p_application_id INT, IN p_applicant_id INT, IN p_document_type VARCHAR(20), IN p_file_name VARCHAR(260),
  IN p_storage_path VARCHAR(500), IN p_content_type VARCHAR(100), IN p_file_size_bytes BIGINT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM tutor_applications WHERE application_id = p_application_id
                 AND applicant_id = p_applicant_id AND status IN ('Draft','Pending','InfoRequested')) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Application not found or already decided.';
  END IF;
  INSERT INTO application_documents (application_id, document_type, file_name, storage_path, content_type, file_size_bytes)
  VALUES (p_application_id, p_document_type, p_file_name, p_storage_path, p_content_type, p_file_size_bytes);
END$$

DROP PROCEDURE IF EXISTS sp_application_submit$$
CREATE PROCEDURE sp_application_submit(IN p_application_id INT, IN p_applicant_id INT)
BEGIN
  DECLARE v_gpa DECIMAL(3,2);
  DECLARE v_year TINYINT;
  DECLARE v_min_gpa DECIMAL(3,2) DEFAULT IFNULL(CAST(fn_setting('TutorMinimumGPA') AS DECIMAL(3,2)), 3.00);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  SELECT gpa, year_of_study INTO v_gpa, v_year FROM tutor_applications
  WHERE application_id = p_application_id AND applicant_id = p_applicant_id AND status IN ('Draft','InfoRequested');

  IF v_gpa IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Application not found, already submitted, or GPA missing.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM application_courses WHERE application_id = p_application_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please select at least one course you want to teach.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM application_documents WHERE application_id = p_application_id AND document_type = 'Transcript') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please upload your official transcript (PDF).';
  END IF;
  IF v_gpa < v_min_gpa THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Your GPA does not meet the minimum requirement for tutors.';
  END IF;
  IF IFNULL(v_year, 0) < 2 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You must have completed your 2nd year or above.';
  END IF;

  START TRANSACTION;
    UPDATE tutor_applications SET status = 'Pending', submitted_at = NOW() WHERE application_id = p_application_id;

    INSERT INTO notifications (user_id, type, title, body, link_url)
    SELECT user_id, 'Application', 'New tutor application', 'A tutor application is waiting for review.',
           '/admin/tutor-verification'
    FROM users WHERE role = 'Admin' AND status = 'Active';
  COMMIT;
END$$

-- Admin approves: tutor profile and courses are created, the role becomes Tutor
DROP PROCEDURE IF EXISTS sp_application_approve$$
CREATE PROCEDURE sp_application_approve(IN p_application_id INT, IN p_admin_id INT)
BEGIN
  DECLARE v_applicant_id INT DEFAULT NULL;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);

  START TRANSACTION;
    SELECT applicant_id INTO v_applicant_id FROM tutor_applications
    WHERE application_id = p_application_id AND status IN ('Pending','InfoRequested')
    FOR UPDATE;
    IF v_applicant_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending applications can be approved.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM application_documents WHERE application_id = p_application_id AND document_type = 'Transcript') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'The application has no transcript. Request more information instead.';
    END IF;

    UPDATE tutor_applications
       SET status = 'Approved', reviewed_by = p_admin_id, reviewed_at = NOW()
     WHERE application_id = p_application_id;

    -- create or update the tutor profile from the application
    INSERT INTO tutor_profiles (user_id, headline, bio, degree, major, gpa, is_approved, approved_at)
    SELECT applicant_id, CONCAT(degree, ' ', major), bio, degree, major, gpa, 1, NOW()
    FROM tutor_applications WHERE application_id = p_application_id
    ON DUPLICATE KEY UPDATE degree = VALUES(degree), major = VALUES(major), gpa = VALUES(gpa),
                            is_approved = 1, approved_at = NOW();

    INSERT INTO tutor_courses (tutor_id, course_id)
    SELECT v_applicant_id, ac.course_id
    FROM application_courses ac
    WHERE ac.application_id = p_application_id
      AND NOT EXISTS (SELECT 1 FROM tutor_courses tc WHERE tc.tutor_id = v_applicant_id AND tc.course_id = ac.course_id);

    UPDATE users SET role = 'Tutor' WHERE user_id = v_applicant_id AND role = 'Student';

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_applicant_id, 'Application', 'You are verified!',
            'Your tutor application was approved. Set your availability to start tutoring.', '/tutor/availability');

    INSERT INTO audit_logs (admin_id, action, target_table, target_id)
    VALUES (p_admin_id, 'Tutor application approved', 'tutor_applications', p_application_id);
  COMMIT;
END$$

DROP PROCEDURE IF EXISTS sp_application_reject$$
CREATE PROCEDURE sp_application_reject(IN p_application_id INT, IN p_admin_id INT, IN p_reason VARCHAR(1000))
BEGIN
  DECLARE v_applicant_id INT DEFAULT NULL;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF NULLIF(TRIM(p_reason), '') IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please give a reason for the rejection.';
  END IF;

  START TRANSACTION;
    SELECT applicant_id INTO v_applicant_id FROM tutor_applications
    WHERE application_id = p_application_id AND status IN ('Pending','InfoRequested')
    FOR UPDATE;
    IF v_applicant_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending applications can be rejected.';
    END IF;

    UPDATE tutor_applications
       SET status = 'Rejected', reviewer_note = p_reason, reviewed_by = p_admin_id, reviewed_at = NOW()
     WHERE application_id = p_application_id;

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_applicant_id, 'Application', 'Application not approved', p_reason, '/tutor/verification');

    INSERT INTO audit_logs (admin_id, action, target_table, target_id, details)
    VALUES (p_admin_id, 'Tutor application rejected', 'tutor_applications', p_application_id, p_reason);
  COMMIT;
END$$

DROP PROCEDURE IF EXISTS sp_application_request_info$$
CREATE PROCEDURE sp_application_request_info(IN p_application_id INT, IN p_admin_id INT, IN p_note VARCHAR(1000))
BEGIN
  DECLARE v_applicant_id INT DEFAULT NULL;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);

  START TRANSACTION;
    SELECT applicant_id INTO v_applicant_id FROM tutor_applications
    WHERE application_id = p_application_id AND status = 'Pending'
    FOR UPDATE;
    IF v_applicant_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending applications can be updated.';
    END IF;

    UPDATE tutor_applications
       SET status = 'InfoRequested', reviewer_note = p_note, reviewed_by = p_admin_id
     WHERE application_id = p_application_id;

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (v_applicant_id, 'Application', 'More information needed', p_note, '/tutor/application');
  COMMIT;
END$$

-- Admin "Tutor verification" list
DROP PROCEDURE IF EXISTS sp_application_list_pending$$
CREATE PROCEDURE sp_application_list_pending()
BEGIN
  SELECT a.application_id, u.full_name, u.email, un.name AS university, a.degree, a.major, a.gpa,
         a.status, a.submitted_at,
         (SELECT GROUP_CONCAT(c.name ORDER BY c.name SEPARATOR ', ')
            FROM application_courses ac JOIN courses c ON c.course_id = ac.course_id
           WHERE ac.application_id = a.application_id) AS courses,
         (SELECT COUNT(*) FROM application_documents d WHERE d.application_id = a.application_id) AS documents
  FROM tutor_applications a
  JOIN users u ON u.user_id = a.applicant_id
  LEFT JOIN universities un ON un.university_id = a.university_id
  WHERE a.status IN ('Pending','InfoRequested')
  ORDER BY a.submitted_at;
END$$

-- =====================================================================
-- QUIZZES & STUDY RECOMMENDATIONS  (FR-10, FR-11, Scenario 3)
-- Flow: sp_quiz_start -> sp_quiz_answer (once per question) -> sp_quiz_submit
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_quiz_start$$
CREATE PROCEDURE sp_quiz_start(IN p_quiz_id INT, IN p_student_id INT, OUT p_attempt_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM quizzes WHERE quiz_id = p_quiz_id AND student_id = p_student_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Quiz not found.';
  END IF;
  INSERT INTO quiz_attempts (quiz_id, student_id) VALUES (p_quiz_id, p_student_id);
  SET p_attempt_id = LAST_INSERT_ID();

  -- questions and options to show (the correct answer is NOT returned)
  SELECT q.question_id, q.question_number, q.question_text, o.option_id, o.label, o.option_text
  FROM quiz_questions q JOIN quiz_options o ON o.question_id = q.question_id
  WHERE q.quiz_id = p_quiz_id
  ORDER BY q.question_number, o.label;
END$$

DROP PROCEDURE IF EXISTS sp_quiz_answer$$
CREATE PROCEDURE sp_quiz_answer(IN p_attempt_id INT, IN p_student_id INT, IN p_question_id INT, IN p_option_id INT)
BEGIN
  DECLARE v_correct TINYINT DEFAULT NULL;

  IF NOT EXISTS (SELECT 1 FROM quiz_attempts WHERE attempt_id = p_attempt_id
                 AND student_id = p_student_id AND submitted_at IS NULL) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This quiz attempt is not open.';
  END IF;

  SELECT o.is_correct INTO v_correct
  FROM quiz_options o
  JOIN quiz_questions q ON q.question_id = o.question_id
  JOIN quiz_attempts a  ON a.quiz_id = q.quiz_id AND a.attempt_id = p_attempt_id
  WHERE o.option_id = p_option_id AND o.question_id = p_question_id;

  IF v_correct IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This answer does not belong to the question.';
  END IF;

  INSERT INTO attempt_answers (attempt_id, question_id, selected_option_id, is_correct)
  VALUES (p_attempt_id, p_question_id, p_option_id, v_correct)
  ON DUPLICATE KEY UPDATE selected_option_id = p_option_id, is_correct = v_correct;
END$$

DROP PROCEDURE IF EXISTS sp_quiz_submit$$
CREATE PROCEDURE sp_quiz_submit(IN p_attempt_id INT, IN p_student_id INT)
BEGIN
  DECLARE v_quiz_id INT DEFAULT NULL;
  DECLARE v_course_id INT;
  DECLARE v_section_id INT;
  DECLARE v_pass_mark TINYINT;
  DECLARE v_submitted DATETIME;
  DECLARE v_total INT;
  DECLARE v_answered INT;
  DECLARE v_correct INT;
  DECLARE v_score TINYINT;
  DECLARE v_passed TINYINT;
  DECLARE v_roadmap_id INT;
  DECLARE v_section_no INT;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
    SELECT a.quiz_id, a.submitted_at, q.course_id, q.section_id, q.pass_mark_percent
      INTO v_quiz_id, v_submitted, v_course_id, v_section_id, v_pass_mark
    FROM quiz_attempts a JOIN quizzes q ON q.quiz_id = a.quiz_id
    WHERE a.attempt_id = p_attempt_id AND a.student_id = p_student_id
    FOR UPDATE;

    IF v_quiz_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Quiz attempt not found.';
    END IF;
    IF v_submitted IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'This quiz has already been submitted.';
    END IF;

    SELECT COUNT(*) INTO v_total FROM quiz_questions WHERE quiz_id = v_quiz_id;
    SELECT COUNT(*), IFNULL(SUM(is_correct), 0) INTO v_answered, v_correct
    FROM attempt_answers WHERE attempt_id = p_attempt_id;

    IF v_total = 0 OR v_answered < v_total THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please answer every question before submitting the quiz.';
    END IF;

    SET v_score  = ROUND(100 * v_correct / v_total);
    SET v_passed = IF(v_score >= v_pass_mark, 1, 0);

    UPDATE quiz_attempts
       SET score_percent = v_score, passed = v_passed, submitted_at = NOW()
     WHERE attempt_id = p_attempt_id;

    -- roadmap progress: passing a section unlocks the next one
    IF v_section_id IS NOT NULL THEN
      SELECT roadmap_id, section_number INTO v_roadmap_id, v_section_no
      FROM roadmap_sections WHERE section_id = v_section_id;

      UPDATE roadmap_sections
         SET best_score = GREATEST(IFNULL(best_score, 0), v_score),
             status     = IF(v_passed = 1, 'Passed', status)
       WHERE section_id = v_section_id;

      IF v_passed = 1 THEN
        UPDATE roadmap_sections SET status = 'Current'
         WHERE roadmap_id = v_roadmap_id AND section_number = v_section_no + 1 AND status = 'Locked';
      END IF;

      UPDATE enrollments e
        JOIN roadmaps r ON r.enrollment_id = e.enrollment_id
         SET e.progress_percent = (SELECT FLOOR(100 * SUM(s.status = 'Passed') / COUNT(*))
                                   FROM roadmap_sections s WHERE s.roadmap_id = v_roadmap_id)
       WHERE r.roadmap_id = v_roadmap_id;
    END IF;

    -- FR-11: one "review this material" recommendation per weak topic
    INSERT INTO study_recommendations (student_id, attempt_id, course_id, topic_id, material_id, recommendation_type, message)
    SELECT p_student_id, p_attempt_id, v_course_id, q.topic_id, MIN(q.source_material_id), 'ReviewMaterial',
           CONCAT('Review "', t.name, '" - you missed ', COUNT(*), ' question(s) on this topic.')
    FROM attempt_answers aa
    JOIN quiz_questions q ON q.question_id = aa.question_id
    JOIN topics t         ON t.topic_id = q.topic_id
    WHERE aa.attempt_id = p_attempt_id AND aa.is_correct = 0
    GROUP BY q.topic_id, t.name;

    IF v_passed = 0 THEN
      INSERT INTO study_recommendations (student_id, attempt_id, course_id, recommendation_type, message)
      VALUES (p_student_id, p_attempt_id, v_course_id, 'RetryQuiz',
              CONCAT('You scored ', v_score, '%. Review the explanations and retry - the pass mark is ', v_pass_mark, '%.'));

      INSERT INTO study_recommendations (student_id, attempt_id, course_id, recommendation_type, message)
      SELECT p_student_id, p_attempt_id, v_course_id, 'FindTutor',
             CONCAT('A verified tutor can help you with ', c.name, '.')
      FROM courses c
      WHERE c.course_id = v_course_id
        AND EXISTS (SELECT 1 FROM tutor_courses tc JOIN tutor_profiles tp ON tp.user_id = tc.tutor_id
                    WHERE tc.course_id = v_course_id AND tp.is_approved = 1);
    END IF;

    INSERT INTO notifications (user_id, type, title, body, link_url)
    VALUES (p_student_id, 'Quiz', IF(v_passed = 1, 'Quiz passed', 'Quiz result'),
            CONCAT('You scored ', v_score, '%.'), CONCAT('/quiz-results/', p_attempt_id));
  COMMIT;

  CALL sp_quiz_get_result(p_attempt_id, p_student_id);
END$$

-- Quiz results page: score, answer review with explanations, recommendations
DROP PROCEDURE IF EXISTS sp_quiz_get_result$$
CREATE PROCEDURE sp_quiz_get_result(IN p_attempt_id INT, IN p_student_id INT)
BEGIN
  SELECT a.attempt_id, q.title, a.score_percent, a.passed, q.pass_mark_percent, a.submitted_at
  FROM quiz_attempts a JOIN quizzes q ON q.quiz_id = a.quiz_id
  WHERE a.attempt_id = p_attempt_id AND a.student_id = p_student_id;

  SELECT qq.question_number, qq.question_text,
         sel.label AS your_answer, sel.option_text AS your_answer_text,
         cor.label AS correct_answer, cor.option_text AS correct_answer_text,
         aa.is_correct, qq.explanation, m.file_name AS source_file, qq.source_page
  FROM attempt_answers aa
  JOIN quiz_attempts a   ON a.attempt_id = aa.attempt_id AND a.student_id = p_student_id
  JOIN quiz_questions qq ON qq.question_id = aa.question_id
  JOIN quiz_options sel  ON sel.option_id = aa.selected_option_id
  JOIN quiz_options cor  ON cor.question_id = qq.question_id AND cor.is_correct = 1
  LEFT JOIN course_materials m ON m.material_id = qq.source_material_id
  WHERE aa.attempt_id = p_attempt_id
  ORDER BY qq.question_number;

  SELECT recommendation_id, recommendation_type, message, topic_id, material_id
  FROM study_recommendations
  WHERE attempt_id = p_attempt_id AND student_id = p_student_id AND is_dismissed = 0;
END$$

-- =====================================================================
-- COURSE MATERIALS  (FR-08, NFR-07)
-- Flow: sp_material_create (Processing) -> the app extracts the text and
--       inserts document_chunks -> sp_material_set_processed
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_material_create$$
CREATE PROCEDURE sp_material_create(
  IN p_student_id INT, IN p_course_id INT, IN p_file_name VARCHAR(260), IN p_storage_path VARCHAR(500),
  IN p_file_size_bytes BIGINT, OUT p_new_material_id INT)
BEGIN
  DECLARE v_max_mb INT DEFAULT IFNULL(CAST(fn_setting('MaxUploadSizeMB') AS UNSIGNED), 50);

  IF NOT EXISTS (SELECT 1 FROM users WHERE user_id = p_student_id AND status = 'Active') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only active users can upload materials.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM courses WHERE course_id = p_course_id AND is_active = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please choose an existing course.';
  END IF;
  IF p_file_size_bytes > v_max_mb * 1024 * 1024 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'The file is larger than the maximum upload size.';
  END IF;

  INSERT INTO course_materials (uploaded_by, course_id, file_name, storage_path, file_type, file_size_bytes, processing_status)
  VALUES (p_student_id, p_course_id, p_file_name, p_storage_path, 'PDF', p_file_size_bytes, 'Processing');
  SET p_new_material_id = LAST_INSERT_ID();
END$$

-- Record the result of the text extraction: Processed (with page count) or Failed
DROP PROCEDURE IF EXISTS sp_material_set_processed$$
CREATE PROCEDURE sp_material_set_processed(IN p_material_id INT, IN p_page_count INT, IN p_succeeded TINYINT)
BEGIN
  UPDATE course_materials
     SET processing_status = IF(p_succeeded = 1, 'Processed', 'Failed'),
         page_count = p_page_count
   WHERE material_id = p_material_id;
END$$

-- DELETE: only the owner can delete a material, and not while a quiz or a
-- roadmap lesson refers to it. Its text chunks cascade.
DROP PROCEDURE IF EXISTS sp_material_delete$$
CREATE PROCEDURE sp_material_delete(IN p_student_id INT, IN p_material_id INT)
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  IF NOT EXISTS (SELECT 1 FROM course_materials WHERE material_id = p_material_id AND uploaded_by = p_student_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Material not found.';
  END IF;
  IF EXISTS (SELECT 1 FROM quizzes WHERE material_id = p_material_id)
     OR EXISTS (SELECT 1 FROM quiz_questions WHERE source_material_id = p_material_id)
     OR EXISTS (SELECT 1 FROM section_references WHERE material_id = p_material_id) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'This material is used by a quiz or a roadmap lesson. Turn it off as an AI source instead of deleting it.';
  END IF;

  START TRANSACTION;
    DELETE FROM study_recommendations WHERE material_id = p_material_id;
    DELETE FROM course_materials WHERE material_id = p_material_id;   -- chunks and their AI citations cascade
  COMMIT;
END$$

-- =====================================================================
-- AI ASSISTANT  (FR-08, FR-09)
-- =====================================================================
-- Start a new chat, optionally limited to the materials of one course
DROP PROCEDURE IF EXISTS sp_ai_conversation_create$$
CREATE PROCEDURE sp_ai_conversation_create(IN p_student_id INT, IN p_course_id INT, OUT p_new_conversation_id INT)
BEGIN
  IF p_course_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM courses WHERE course_id = p_course_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please choose an existing course.';
  END IF;
  INSERT INTO ai_conversations (student_id, course_id, title) VALUES (p_student_id, p_course_id, 'New chat');
  SET p_new_conversation_id = LAST_INSERT_ID();
END$$

DROP PROCEDURE IF EXISTS sp_ai_message_add$$
CREATE PROCEDURE sp_ai_message_add(
  IN p_conversation_id INT, IN p_student_id INT, IN p_sender VARCHAR(10), IN p_content TEXT,
  IN p_is_grounded TINYINT, OUT p_new_message_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM ai_conversations c JOIN users u ON u.user_id = c.student_id
                 WHERE c.conversation_id = p_conversation_id AND c.student_id = p_student_id
                   AND u.ai_access_enabled = 1) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Conversation not found or AI access is disabled for this account.';
  END IF;
  INSERT INTO ai_messages (conversation_id, sender, content, is_grounded)
  VALUES (p_conversation_id, p_sender, p_content, p_is_grounded);
  SET p_new_message_id = LAST_INSERT_ID();
END$$

-- Attach a source page to an AI answer; only the student's own materials can be cited (NFR-07)
DROP PROCEDURE IF EXISTS sp_ai_message_add_source$$
CREATE PROCEDURE sp_ai_message_add_source(IN p_message_id INT, IN p_student_id INT, IN p_chunk_id INT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM document_chunks ch JOIN course_materials m ON m.material_id = ch.material_id
                 WHERE ch.chunk_id = p_chunk_id AND m.uploaded_by = p_student_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Source not found in your materials.';
  END IF;
  INSERT IGNORE INTO ai_message_sources (message_id, chunk_id) VALUES (p_message_id, p_chunk_id);
END$$

DROP PROCEDURE IF EXISTS sp_ai_conversation_get$$
CREATE PROCEDURE sp_ai_conversation_get(IN p_conversation_id INT, IN p_student_id INT)
BEGIN
  SELECT m.message_id, m.sender, m.content, m.is_grounded, m.created_at,
         (SELECT GROUP_CONCAT(CONCAT(cm.file_name, ' p.', ch.page_number) SEPARATOR '; ')
            FROM ai_message_sources s
            JOIN document_chunks ch  ON ch.chunk_id = s.chunk_id
            JOIN course_materials cm ON cm.material_id = ch.material_id
           WHERE s.message_id = m.message_id) AS sources
  FROM ai_messages m
  JOIN ai_conversations c ON c.conversation_id = m.conversation_id AND c.student_id = p_student_id
  WHERE m.conversation_id = p_conversation_id
  ORDER BY m.created_at, m.message_id;
END$$

-- =====================================================================
-- REPORTS & MODERATION  (FR-13)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_report_create$$
CREATE PROCEDURE sp_report_create(
  IN p_reporter_id INT, IN p_reported_user_id INT, IN p_reported_review_id INT,
  IN p_report_type VARCHAR(30), IN p_description VARCHAR(1000), OUT p_new_report_id INT)
BEGIN
  IF p_reported_user_id IS NULL AND p_reported_review_id IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Please choose the user or review you are reporting.';
  END IF;
  IF p_reporter_id = p_reported_user_id THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'You cannot report yourself.';
  END IF;
  INSERT INTO reports (reporter_id, reported_user_id, reported_review_id, report_type, description)
  VALUES (p_reporter_id, p_reported_user_id, p_reported_review_id, p_report_type, p_description);
  SET p_new_report_id = LAST_INSERT_ID();
END$$

-- p_decision: 'Dismissed', 'WarningSent' or 'UserSuspended'
DROP PROCEDURE IF EXISTS sp_report_decide$$
CREATE PROCEDURE sp_report_decide(IN p_report_id INT, IN p_admin_id INT, IN p_decision VARCHAR(20), IN p_note VARCHAR(500))
BEGIN
  DECLARE v_found INT DEFAULT NULL;
  DECLARE v_user_id INT;
  DECLARE v_review_id INT;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF p_decision NOT IN ('Dismissed','WarningSent','UserSuspended') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Unknown decision.';
  END IF;

  START TRANSACTION;
    SELECT report_id, reported_user_id, reported_review_id INTO v_found, v_user_id, v_review_id
    FROM reports WHERE report_id = p_report_id AND status = 'Open'
    FOR UPDATE;
    IF v_found IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Report not found or already decided.';
    END IF;

    UPDATE reports
       SET status = p_decision, decided_by = p_admin_id, decision_note = p_note, decided_at = NOW()
     WHERE report_id = p_report_id;

    IF p_decision = 'UserSuspended' AND v_user_id IS NOT NULL THEN
      UPDATE users SET status = 'Suspended' WHERE user_id = v_user_id;
    END IF;
    IF p_decision <> 'Dismissed' AND v_review_id IS NOT NULL THEN
      UPDATE reviews SET is_hidden = 1 WHERE review_id = v_review_id;   -- trigger recalculates the rating
    END IF;
    IF p_decision = 'WarningSent' AND v_user_id IS NOT NULL THEN
      INSERT INTO notifications (user_id, type, title, body)
      VALUES (v_user_id, 'System', 'Warning from PeerMind AI',
              IFNULL(p_note, 'Your recent activity broke the platform rules.'));
    END IF;

    INSERT INTO audit_logs (admin_id, action, target_table, target_id, details)
    VALUES (p_admin_id, CONCAT('Report decided: ', p_decision), 'reports', p_report_id, p_note);
  COMMIT;
END$$

-- =====================================================================
-- NOTIFICATIONS & SETTINGS
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_notification_mark_all_read$$
CREATE PROCEDURE sp_notification_mark_all_read(IN p_user_id INT)
BEGIN
  UPDATE notifications SET is_read = 1 WHERE user_id = p_user_id AND is_read = 0;
  SELECT ROW_COUNT() AS marked_as_read;
END$$

DROP PROCEDURE IF EXISTS sp_setting_update$$
CREATE PROCEDURE sp_setting_update(IN p_admin_id INT, IN p_key VARCHAR(80), IN p_value VARCHAR(400))
BEGIN
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  CALL sp_assert_admin(p_admin_id);
  IF NOT EXISTS (SELECT 1 FROM platform_settings WHERE setting_key = p_key) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Unknown setting.';
  END IF;

  START TRANSACTION;
    UPDATE platform_settings SET setting_value = p_value, updated_by = p_admin_id WHERE setting_key = p_key;
    INSERT INTO audit_logs (admin_id, action, target_table, details)
    VALUES (p_admin_id, 'Setting changed', 'platform_settings', CONCAT(p_key, ' = ', p_value));
  COMMIT;
END$$

DELIMITER ;
