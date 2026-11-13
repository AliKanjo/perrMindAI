-- =====================================================================
-- PeerMind AI - Database schema (MariaDB / MySQL, phpMyAdmin)
-- SOFT3111 - Internet and Web Programming, Project #1
--
-- Import order (click your database in the left sidebar, then Import):
--   01_schema.sql      tables, keys, constraints, indexes
--   02_procedures.sql  views, triggers, stored procedures (CRUD + workflows)
--   03_seed_data.sql   initial demo data
--   99_reset_database.sql  only when you want to start again from an empty database
--
-- PERSISTENCE: every statement is "IF NOT EXISTS". Running this file
-- again never deletes or changes existing data, and MariaDB keeps the
-- data on disk, so everything survives restarting XAMPP / the app.
-- Use 99_reset_database.sql only to start again from an empty database.
-- =====================================================================

-- Select your database in phpMyAdmin first (e.g. peermindai_db),
-- then import this file. The tables are created in the selected database.

-- =====================================================================
-- 1. USERS & ACCOUNTS  (FR-01, FR-02, NFR-02)
-- =====================================================================

CREATE TABLE IF NOT EXISTS universities (
  university_id  INT AUTO_INCREMENT PRIMARY KEY,
  name           VARCHAR(150) NOT NULL,
  city           VARCHAR(100) NULL,
  CONSTRAINT uq_universities_name UNIQUE (name)
) ENGINE=InnoDB;

-- Only these e-mail domains may register as students / tutors
CREATE TABLE IF NOT EXISTS allowed_email_domains (
  domain_id      INT AUTO_INCREMENT PRIMARY KEY,
  domain         VARCHAR(100) NOT NULL,
  university_id  INT NULL,
  CONSTRAINT uq_allowed_domains UNIQUE (domain),
  CONSTRAINT fk_domains_university FOREIGN KEY (university_id) REFERENCES universities(university_id)
) ENGINE=InnoDB;

-- One row per account; the role decides what the user may do
CREATE TABLE IF NOT EXISTS users (
  user_id            INT AUTO_INCREMENT PRIMARY KEY,
  full_name          VARCHAR(120) NOT NULL,
  email              VARCHAR(255) NOT NULL,
  password_hash      VARCHAR(255) NOT NULL,          -- password_hash() / bcrypt, never plain text
  role               ENUM('Student','Tutor','Admin') NOT NULL DEFAULT 'Student',
  status             ENUM('PendingVerification','Active','Suspended') NOT NULL DEFAULT 'PendingVerification',
  phone              VARCHAR(30)  NULL,
  avatar_url         VARCHAR(400) NULL,
  university_id      INT NULL,
  email_verified     TINYINT(1) NOT NULL DEFAULT 0,
  ai_access_enabled  TINYINT(1) NOT NULL DEFAULT 1,
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  last_active_at     DATETIME NULL,
  CONSTRAINT uq_users_email UNIQUE (email),
  CONSTRAINT fk_users_university FOREIGN KEY (university_id) REFERENCES universities(university_id),
  INDEX ix_users_role_status (role, status)
) ENGINE=InnoDB;

-- Extra data for every learner (tutors are students too, so they keep this row)
CREATE TABLE IF NOT EXISTS student_profiles (
  user_id            INT PRIMARY KEY,
  department         VARCHAR(120) NULL,
  year_of_study      TINYINT NULL,
  student_number     VARCHAR(30) NULL,
  study_streak_days  INT NOT NULL DEFAULT 0,
  CONSTRAINT ck_student_year CHECK (year_of_study BETWEEN 1 AND 8),
  CONSTRAINT fk_student_profiles_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Public tutor profile; only is_approved = 1 tutors appear in search (FR-03, FR-04)
CREATE TABLE IF NOT EXISTS tutor_profiles (
  user_id          INT PRIMARY KEY,
  headline         VARCHAR(150) NULL,
  bio              VARCHAR(1000) NULL,
  hourly_rate      DECIMAL(8,2) NOT NULL DEFAULT 0,   -- shown only; no online payments (out of scope)
  degree           ENUM('BSc','MSc','PhD') NULL,
  major            VARCHAR(120) NULL,
  gpa              DECIMAL(3,2) NULL,
  average_rating   DECIMAL(3,2) NOT NULL DEFAULT 0,   -- kept up to date by triggers
  review_count     INT NOT NULL DEFAULT 0,
  is_approved      TINYINT(1) NOT NULL DEFAULT 0,
  approved_at      DATETIME NULL,
  accept_same_day  TINYINT(1) NOT NULL DEFAULT 1,
  buffer_minutes   SMALLINT NOT NULL DEFAULT 15,
  CONSTRAINT ck_tutor_rate CHECK (hourly_rate >= 0),
  CONSTRAINT ck_tutor_gpa  CHECK (gpa BETWEEN 0 AND 4),
  CONSTRAINT fk_tutor_profiles_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS user_preferences (
  user_id              INT PRIMARY KEY,
  email_notifications  TINYINT(1) NOT NULL DEFAULT 1,
  study_reminders      TINYINT(1) NOT NULL DEFAULT 1,
  message_alerts       TINYINT(1) NOT NULL DEFAULT 1,
  weekly_report        TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_user_preferences_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- E-mail verification and password-reset links
CREATE TABLE IF NOT EXISTS verification_tokens (
  token_id    INT AUTO_INCREMENT PRIMARY KEY,
  user_id     INT NOT NULL,
  token       VARCHAR(128) NOT NULL,
  purpose     ENUM('EmailVerification','PasswordReset') NOT NULL,
  expires_at  DATETIME NOT NULL,
  used_at     DATETIME NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT uq_verification_token UNIQUE (token),
  CONSTRAINT fk_tokens_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Used for "max failed login attempts" and "new-device login alerts"
CREATE TABLE IF NOT EXISTS login_attempts (
  attempt_id    BIGINT AUTO_INCREMENT PRIMARY KEY,
  user_id       INT NULL,
  email         VARCHAR(255) NOT NULL,
  succeeded     TINYINT(1) NOT NULL,
  ip_address    VARCHAR(45) NULL,
  device        VARCHAR(200) NULL,
  attempted_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_login_attempts_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE SET NULL,
  INDEX ix_login_attempts_email (email, attempted_at)
) ENGINE=InnoDB;

-- =====================================================================
-- 2. COURSES (course library managed by admins)
-- =====================================================================

CREATE TABLE IF NOT EXISTS courses (
  course_id      INT AUTO_INCREMENT PRIMARY KEY,
  code           VARCHAR(20)  NOT NULL,
  name           VARCHAR(150) NOT NULL,
  department     VARCHAR(120) NULL,
  professor      VARCHAR(120) NULL,
  university_id  INT NULL,
  is_active      TINYINT(1) NOT NULL DEFAULT 1,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at     DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT uq_courses_code UNIQUE (code),
  CONSTRAINT fk_courses_university FOREIGN KEY (university_id) REFERENCES universities(university_id)
) ENGINE=InnoDB;

-- Topics inside a course: used to find weak areas from quiz results (FR-11)
CREATE TABLE IF NOT EXISTS topics (
  topic_id   INT AUTO_INCREMENT PRIMARY KEY,
  course_id  INT NOT NULL,
  name       VARCHAR(120) NOT NULL,
  CONSTRAINT uq_topics_course_name UNIQUE (course_id, name),
  CONSTRAINT fk_topics_course FOREIGN KEY (course_id) REFERENCES courses(course_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- =====================================================================
-- 3. TUTOR APPLICATION & VERIFICATION  (FR-06, FR-07, FR-13)
-- =====================================================================

CREATE TABLE IF NOT EXISTS tutor_applications (
  application_id  INT AUTO_INCREMENT PRIMARY KEY,
  applicant_id    INT NOT NULL,
  university_id   INT NULL,
  degree          ENUM('BSc','MSc','PhD') NULL,
  major           VARCHAR(120) NULL,
  gpa             DECIMAL(3,2) NULL,
  year_of_study   TINYINT NULL,
  bio             VARCHAR(1000) NULL,
  status          ENUM('Draft','Pending','InfoRequested','Approved','Rejected') NOT NULL DEFAULT 'Draft',
  reviewer_note   VARCHAR(1000) NULL,     -- rejection reason or "more info" request
  reviewed_by     INT NULL,
  submitted_at    DATETIME NULL,
  reviewed_at     DATETIME NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_application_gpa CHECK (gpa BETWEEN 0 AND 4),
  CONSTRAINT ck_application_reject_reason CHECK (status <> 'Rejected' OR reviewer_note IS NOT NULL),
  CONSTRAINT fk_applications_applicant FOREIGN KEY (applicant_id) REFERENCES users(user_id),
  CONSTRAINT fk_applications_university FOREIGN KEY (university_id) REFERENCES universities(university_id),
  CONSTRAINT fk_applications_reviewer FOREIGN KEY (reviewed_by) REFERENCES users(user_id),
  INDEX ix_applications_status (status, submitted_at)
) ENGINE=InnoDB;

-- Courses the applicant wants to teach, with the grade they obtained
CREATE TABLE IF NOT EXISTS application_courses (
  application_id  INT NOT NULL,
  course_id       INT NOT NULL,
  grade_obtained  VARCHAR(5) NULL,
  PRIMARY KEY (application_id, course_id),
  CONSTRAINT fk_app_courses_application FOREIGN KEY (application_id) REFERENCES tutor_applications(application_id) ON DELETE CASCADE,
  CONSTRAINT fk_app_courses_course FOREIGN KEY (course_id) REFERENCES courses(course_id)
) ENGINE=InnoDB;

-- Qualification evidence (transcript etc.), kept in private storage (NFR-07)
CREATE TABLE IF NOT EXISTS application_documents (
  document_id      INT AUTO_INCREMENT PRIMARY KEY,
  application_id   INT NOT NULL,
  document_type    ENUM('Transcript','Certificate','Other') NOT NULL,
  file_name        VARCHAR(260) NOT NULL,
  storage_path     VARCHAR(500) NOT NULL,
  content_type     VARCHAR(100) NOT NULL,
  file_size_bytes  BIGINT NOT NULL,
  uploaded_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_app_document_size CHECK (file_size_bytes > 0),
  CONSTRAINT fk_app_documents_application FOREIGN KEY (application_id) REFERENCES tutor_applications(application_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Courses an APPROVED tutor teaches (many-to-many tutor <-> course)
CREATE TABLE IF NOT EXISTS tutor_courses (
  tutor_id     INT NOT NULL,
  course_id    INT NOT NULL,
  approved_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (tutor_id, course_id),
  CONSTRAINT fk_tutor_courses_tutor FOREIGN KEY (tutor_id) REFERENCES tutor_profiles(user_id) ON DELETE CASCADE,
  CONSTRAINT fk_tutor_courses_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  INDEX ix_tutor_courses_course (course_id)
) ENGINE=InnoDB;

-- =====================================================================
-- 4. LEARNING: ENROLMENTS, MATERIALS, AI ROADMAP  (FR-08, FR-09)
-- =====================================================================

CREATE TABLE IF NOT EXISTS enrollments (
  enrollment_id     INT AUTO_INCREMENT PRIMARY KEY,
  student_id        INT NOT NULL,
  course_id         INT NOT NULL,
  progress_percent  TINYINT NOT NULL DEFAULT 0,
  enrolled_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_enrollment_progress CHECK (progress_percent BETWEEN 0 AND 100),
  CONSTRAINT uq_enrollments UNIQUE (student_id, course_id),
  CONSTRAINT fk_enrollments_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  CONSTRAINT fk_enrollments_course FOREIGN KEY (course_id) REFERENCES courses(course_id)
) ENGINE=InnoDB;

-- Uploaded course files; only the uploader can use them (NFR-07)
CREATE TABLE IF NOT EXISTS course_materials (
  material_id        INT AUTO_INCREMENT PRIMARY KEY,
  uploaded_by        INT NOT NULL,
  course_id          INT NOT NULL,
  file_name          VARCHAR(260) NOT NULL,
  storage_path       VARCHAR(500) NOT NULL,
  file_type          ENUM('PDF','PPTX','DOCX') NOT NULL,
  file_size_bytes    BIGINT NOT NULL,
  page_count         INT NULL,
  processing_status  ENUM('Uploaded','Processing','Processed','Failed') NOT NULL DEFAULT 'Uploaded',
  use_as_ai_source   TINYINT(1) NOT NULL DEFAULT 1,
  uploaded_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_material_size CHECK (file_size_bytes > 0 AND file_size_bytes <= 52428800),  -- 50 MB
  CONSTRAINT fk_materials_user FOREIGN KEY (uploaded_by) REFERENCES users(user_id),
  CONSTRAINT fk_materials_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  INDEX ix_materials_owner (uploaded_by, course_id)
) ENGINE=InnoDB;

-- Text pieces extracted from a material, with page numbers for AI source references (FR-09)
CREATE TABLE IF NOT EXISTS document_chunks (
  chunk_id        INT AUTO_INCREMENT PRIMARY KEY,
  material_id     INT NOT NULL,
  page_number     INT NULL,
  chunk_index     INT NOT NULL,
  content         TEXT NOT NULL,
  embedding_json  LONGTEXT NULL,          -- vector stored as JSON text for retrieval
  CONSTRAINT uq_chunks UNIQUE (material_id, chunk_index),
  CONSTRAINT fk_chunks_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- The AI-generated study plan for one enrolment
CREATE TABLE IF NOT EXISTS roadmaps (
  roadmap_id     INT AUTO_INCREMENT PRIMARY KEY,
  enrollment_id  INT NOT NULL,
  generated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT uq_roadmaps_enrollment UNIQUE (enrollment_id),
  CONSTRAINT fk_roadmaps_enrollment FOREIGN KEY (enrollment_id) REFERENCES enrollments(enrollment_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- One step of the roadmap; pass its quiz to unlock the next section
CREATE TABLE IF NOT EXISTS roadmap_sections (
  section_id         INT AUTO_INCREMENT PRIMARY KEY,
  roadmap_id         INT NOT NULL,
  section_number     INT NOT NULL,
  title              VARCHAR(150) NOT NULL,
  lesson_content     TEXT NULL,
  estimated_minutes  INT NULL,
  topic_id           INT NULL,
  status             ENUM('Locked','Current','Passed') NOT NULL DEFAULT 'Locked',
  best_score         TINYINT NULL,
  CONSTRAINT uq_sections_number UNIQUE (roadmap_id, section_number),
  CONSTRAINT fk_sections_roadmap FOREIGN KEY (roadmap_id) REFERENCES roadmaps(roadmap_id) ON DELETE CASCADE,
  CONSTRAINT fk_sections_topic FOREIGN KEY (topic_id) REFERENCES topics(topic_id)
) ENGINE=InnoDB;

-- Which material pages a lesson was built from ("Lecture 5, p.4")
CREATE TABLE IF NOT EXISTS section_references (
  section_id   INT NOT NULL,
  material_id  INT NOT NULL,
  page_number  INT NOT NULL DEFAULT 0,
  PRIMARY KEY (section_id, material_id, page_number),
  CONSTRAINT fk_section_refs_section FOREIGN KEY (section_id) REFERENCES roadmap_sections(section_id) ON DELETE CASCADE,
  CONSTRAINT fk_section_refs_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id)
) ENGINE=InnoDB;

-- =====================================================================
-- 5. PRACTICE QUIZZES & RECOMMENDATIONS  (FR-10, FR-11)
-- =====================================================================

CREATE TABLE IF NOT EXISTS quizzes (
  quiz_id            INT AUTO_INCREMENT PRIMARY KEY,
  student_id         INT NOT NULL,
  course_id          INT NOT NULL,
  section_id         INT NULL,
  material_id        INT NULL,
  title              VARCHAR(150) NOT NULL,
  pass_mark_percent  TINYINT NOT NULL DEFAULT 75,
  generated_by_ai    TINYINT(1) NOT NULL DEFAULT 1,
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_quiz_pass_mark CHECK (pass_mark_percent BETWEEN 1 AND 100),
  CONSTRAINT fk_quizzes_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  CONSTRAINT fk_quizzes_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  CONSTRAINT fk_quizzes_section FOREIGN KEY (section_id) REFERENCES roadmap_sections(section_id),
  CONSTRAINT fk_quizzes_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS quiz_questions (
  question_id         INT AUTO_INCREMENT PRIMARY KEY,
  quiz_id             INT NOT NULL,
  question_number     INT NOT NULL,
  question_text       VARCHAR(1000) NOT NULL,
  explanation         VARCHAR(1000) NULL,
  topic_id            INT NULL,
  source_material_id  INT NULL,
  source_page         INT NULL,
  CONSTRAINT uq_questions_number UNIQUE (quiz_id, question_number),
  CONSTRAINT fk_questions_quiz FOREIGN KEY (quiz_id) REFERENCES quizzes(quiz_id) ON DELETE CASCADE,
  CONSTRAINT fk_questions_topic FOREIGN KEY (topic_id) REFERENCES topics(topic_id),
  CONSTRAINT fk_questions_material FOREIGN KEY (source_material_id) REFERENCES course_materials(material_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS quiz_options (
  option_id    INT AUTO_INCREMENT PRIMARY KEY,
  question_id  INT NOT NULL,
  label        CHAR(1) NOT NULL,
  option_text  VARCHAR(500) NOT NULL,
  is_correct   TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT ck_option_label CHECK (label IN ('A','B','C','D')),
  CONSTRAINT uq_options_label UNIQUE (question_id, label),
  CONSTRAINT fk_options_question FOREIGN KEY (question_id) REFERENCES quiz_questions(question_id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS quiz_attempts (
  attempt_id     INT AUTO_INCREMENT PRIMARY KEY,
  quiz_id        INT NOT NULL,
  student_id     INT NOT NULL,
  score_percent  TINYINT NULL,
  passed         TINYINT(1) NULL,
  started_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  submitted_at   DATETIME NULL,          -- NULL = attempt still in progress
  CONSTRAINT ck_attempt_score CHECK (score_percent BETWEEN 0 AND 100),
  CONSTRAINT fk_attempts_quiz FOREIGN KEY (quiz_id) REFERENCES quizzes(quiz_id) ON DELETE CASCADE,
  CONSTRAINT fk_attempts_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  INDEX ix_attempts_student (student_id, quiz_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS attempt_answers (
  attempt_id          INT NOT NULL,
  question_id         INT NOT NULL,
  selected_option_id  INT NOT NULL,
  is_correct          TINYINT(1) NOT NULL,
  PRIMARY KEY (attempt_id, question_id),
  CONSTRAINT fk_answers_attempt FOREIGN KEY (attempt_id) REFERENCES quiz_attempts(attempt_id) ON DELETE CASCADE,
  CONSTRAINT fk_answers_question FOREIGN KEY (question_id) REFERENCES quiz_questions(question_id) ON DELETE CASCADE,
  CONSTRAINT fk_answers_option FOREIGN KEY (selected_option_id) REFERENCES quiz_options(option_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- "Review Lecture 6" / "Find a tutor for Linked Lists" (FR-11)
CREATE TABLE IF NOT EXISTS study_recommendations (
  recommendation_id    INT AUTO_INCREMENT PRIMARY KEY,
  student_id           INT NOT NULL,
  attempt_id           INT NULL,
  course_id            INT NOT NULL,
  topic_id             INT NULL,
  material_id          INT NULL,
  recommendation_type  ENUM('ReviewMaterial','FindTutor','RetryQuiz') NOT NULL,
  message              VARCHAR(500) NOT NULL,
  is_dismissed         TINYINT(1) NOT NULL DEFAULT 0,
  created_at           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_recs_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  CONSTRAINT fk_recs_attempt FOREIGN KEY (attempt_id) REFERENCES quiz_attempts(attempt_id) ON DELETE SET NULL,
  CONSTRAINT fk_recs_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  CONSTRAINT fk_recs_topic FOREIGN KEY (topic_id) REFERENCES topics(topic_id),
  CONSTRAINT fk_recs_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id)
) ENGINE=InnoDB;

-- =====================================================================
-- 6. AVAILABILITY & BOOKINGS  (FR-05, FR-07, FR-12, NFR-05)
-- =====================================================================

-- Weekly template the tutor edits on the "Availability" grid
CREATE TABLE IF NOT EXISTS tutor_weekly_availability (
  weekly_id    INT AUTO_INCREMENT PRIMARY KEY,
  tutor_id     INT NOT NULL,
  day_of_week  TINYINT NOT NULL,          -- 1 = Monday ... 7 = Sunday
  start_time   TIME NOT NULL,
  end_time     TIME NOT NULL,
  CONSTRAINT ck_weekly_day   CHECK (day_of_week BETWEEN 1 AND 7),
  CONSTRAINT ck_weekly_times CHECK (end_time > start_time),
  CONSTRAINT uq_weekly UNIQUE (tutor_id, day_of_week, start_time),
  CONSTRAINT fk_weekly_tutor FOREIGN KEY (tutor_id) REFERENCES tutor_profiles(user_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Concrete bookable time slots (one session each)
CREATE TABLE IF NOT EXISTS availability_slots (
  slot_id     INT AUTO_INCREMENT PRIMARY KEY,
  tutor_id    INT NOT NULL,
  start_at    DATETIME NOT NULL,
  end_at      DATETIME NOT NULL,
  is_open     TINYINT(1) NOT NULL DEFAULT 1,     -- tutor can close a slot
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_slot_times CHECK (end_at > start_at),
  CONSTRAINT uq_slots_tutor_start UNIQUE (tutor_id, start_at),
  CONSTRAINT fk_slots_tutor FOREIGN KEY (tutor_id) REFERENCES tutor_profiles(user_id) ON DELETE CASCADE,
  INDEX ix_slots_time (tutor_id, start_at, end_at, is_open)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS bookings (
  booking_id           INT AUTO_INCREMENT PRIMARY KEY,
  booking_code         VARCHAR(40) NOT NULL,       -- e.g. PM-20481
  student_id           INT NOT NULL,
  tutor_id             INT NOT NULL,
  slot_id              INT NOT NULL,
  course_id            INT NOT NULL,
  topic                VARCHAR(150) NULL,
  notes                VARCHAR(1000) NULL,
  status               ENUM('Pending','Confirmed','Declined','Cancelled','Completed') NOT NULL DEFAULT 'Pending',
  price                DECIMAL(8,2) NOT NULL DEFAULT 0,   -- agreed price, paid outside the platform
  cancellation_reason  VARCHAR(500) NULL,
  created_at           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at           DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
  -- NFR-05: this column holds slot_id only while the booking is active
  -- (Pending/Confirmed), otherwise NULL. Its UNIQUE key means a slot can
  -- hold only ONE active booking - even if two students click at the same moment.
  active_slot_id       INT AS (CASE WHEN status IN ('Pending','Confirmed') THEN slot_id ELSE NULL END) STORED,
  CONSTRAINT uq_bookings_code UNIQUE (booking_code),
  CONSTRAINT uq_bookings_active_slot UNIQUE (active_slot_id),
  CONSTRAINT ck_bookings_not_self CHECK (student_id <> tutor_id),
  CONSTRAINT fk_bookings_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  CONSTRAINT fk_bookings_tutor FOREIGN KEY (tutor_id) REFERENCES tutor_profiles(user_id),
  CONSTRAINT fk_bookings_slot FOREIGN KEY (slot_id) REFERENCES availability_slots(slot_id),
  CONSTRAINT fk_bookings_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  INDEX ix_bookings_student (student_id, status),
  INDEX ix_bookings_tutor (tutor_id, status)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS booking_status_history (
  history_id  INT AUTO_INCREMENT PRIMARY KEY,
  booking_id  INT NOT NULL,
  old_status  VARCHAR(20) NULL,
  new_status  VARCHAR(20) NOT NULL,
  changed_by  INT NULL,
  note        VARCHAR(300) NULL,
  changed_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_history_booking FOREIGN KEY (booking_id) REFERENCES bookings(booking_id) ON DELETE CASCADE,
  CONSTRAINT fk_history_user FOREIGN KEY (changed_by) REFERENCES users(user_id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- One review per completed booking (FR-12)
CREATE TABLE IF NOT EXISTS reviews (
  review_id    INT AUTO_INCREMENT PRIMARY KEY,
  booking_id   INT NOT NULL,
  student_id   INT NOT NULL,
  tutor_id     INT NOT NULL,
  rating       TINYINT NOT NULL,
  comment      VARCHAR(1000) NULL,
  tutor_reply  VARCHAR(1000) NULL,
  is_hidden    TINYINT(1) NOT NULL DEFAULT 0,      -- hidden by moderation
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ck_review_rating CHECK (rating BETWEEN 1 AND 5),
  CONSTRAINT uq_reviews_booking UNIQUE (booking_id),
  CONSTRAINT fk_reviews_booking FOREIGN KEY (booking_id) REFERENCES bookings(booking_id) ON DELETE CASCADE,
  CONSTRAINT fk_reviews_student FOREIGN KEY (student_id) REFERENCES users(user_id),
  CONSTRAINT fk_reviews_tutor FOREIGN KEY (tutor_id) REFERENCES tutor_profiles(user_id),
  INDEX ix_reviews_tutor (tutor_id)
) ENGINE=InnoDB;

-- =====================================================================
-- 7. COMMUNICATION
-- =====================================================================

CREATE TABLE IF NOT EXISTS messages (
  message_id   INT AUTO_INCREMENT PRIMARY KEY,
  sender_id    INT NOT NULL,
  receiver_id  INT NOT NULL,
  booking_id   INT NULL,
  body         VARCHAR(2000) NOT NULL,
  sent_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  read_at      DATETIME NULL,
  CONSTRAINT ck_messages_not_self CHECK (sender_id <> receiver_id),
  CONSTRAINT fk_messages_sender FOREIGN KEY (sender_id) REFERENCES users(user_id),
  CONSTRAINT fk_messages_receiver FOREIGN KEY (receiver_id) REFERENCES users(user_id),
  CONSTRAINT fk_messages_booking FOREIGN KEY (booking_id) REFERENCES bookings(booking_id) ON DELETE SET NULL,
  INDEX ix_messages_receiver (receiver_id, read_at)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS notifications (
  notification_id  INT AUTO_INCREMENT PRIMARY KEY,
  user_id          INT NOT NULL,
  type             ENUM('Booking','Message','StudyReminder','Quiz','Review','Application','System') NOT NULL,
  title            VARCHAR(150) NOT NULL,
  body             VARCHAR(500) NULL,
  link_url         VARCHAR(300) NULL,
  is_read          TINYINT(1) NOT NULL DEFAULT 0,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_notifications_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
  INDEX ix_notifications_user (user_id, is_read, created_at)
) ENGINE=InnoDB;

-- =====================================================================
-- 8. AI ASSISTANT  (FR-08, FR-09, NFR-07)
-- =====================================================================

CREATE TABLE IF NOT EXISTS ai_conversations (
  conversation_id  INT AUTO_INCREMENT PRIMARY KEY,
  student_id       INT NOT NULL,
  course_id        INT NULL,
  material_id      INT NULL,
  title            VARCHAR(150) NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_conversations_student FOREIGN KEY (student_id) REFERENCES users(user_id) ON DELETE CASCADE,
  CONSTRAINT fk_conversations_course FOREIGN KEY (course_id) REFERENCES courses(course_id),
  CONSTRAINT fk_conversations_material FOREIGN KEY (material_id) REFERENCES course_materials(material_id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ai_messages (
  message_id       INT AUTO_INCREMENT PRIMARY KEY,
  conversation_id  INT NOT NULL,
  sender           ENUM('User','Assistant') NOT NULL,
  content          TEXT NOT NULL,
  is_grounded      TINYINT(1) NULL,     -- 0 = not enough information in the materials (Scenario 2)
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_ai_messages_conversation FOREIGN KEY (conversation_id) REFERENCES ai_conversations(conversation_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- The document pages an AI answer was based on (FR-09)
CREATE TABLE IF NOT EXISTS ai_message_sources (
  message_id  INT NOT NULL,
  chunk_id    INT NOT NULL,
  PRIMARY KEY (message_id, chunk_id),
  CONSTRAINT fk_ai_sources_message FOREIGN KEY (message_id) REFERENCES ai_messages(message_id) ON DELETE CASCADE,
  CONSTRAINT fk_ai_sources_chunk FOREIGN KEY (chunk_id) REFERENCES document_chunks(chunk_id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Every call to the AI service (admin "AI usage" page)
CREATE TABLE IF NOT EXISTS ai_usage_logs (
  log_id         BIGINT AUTO_INCREMENT PRIMARY KEY,
  user_id        INT NULL,
  feature        ENUM('StudyAssistant','QuizGenerator','RoadmapGenerator','DocumentProcessing') NOT NULL,
  model          VARCHAR(60) NULL,
  input_tokens   INT NOT NULL DEFAULT 0,
  output_tokens  INT NOT NULL DEFAULT 0,
  cost_usd       DECIMAL(10,5) NOT NULL DEFAULT 0,
  latency_ms     INT NULL,
  succeeded      TINYINT(1) NOT NULL DEFAULT 1,
  error_message  VARCHAR(500) NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_ai_usage_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE SET NULL,
  INDEX ix_ai_usage_date (created_at, feature)
) ENGINE=InnoDB;

-- =====================================================================
-- 9. ADMINISTRATION & MODERATION  (FR-13)
-- =====================================================================

CREATE TABLE IF NOT EXISTS reports (
  report_id           INT AUTO_INCREMENT PRIMARY KEY,
  reporter_id         INT NOT NULL,
  reported_user_id    INT NULL,
  reported_review_id  INT NULL,
  report_type         ENUM('InappropriateMessage','NoShow','Spam','InappropriateContent','Other') NOT NULL,
  description         VARCHAR(1000) NOT NULL,
  status              ENUM('Open','Dismissed','WarningSent','UserSuspended') NOT NULL DEFAULT 'Open',
  decided_by          INT NULL,
  decision_note       VARCHAR(500) NULL,
  created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  decided_at          DATETIME NULL,
  CONSTRAINT ck_reports_target CHECK (reported_user_id IS NOT NULL OR reported_review_id IS NOT NULL),
  CONSTRAINT fk_reports_reporter FOREIGN KEY (reporter_id) REFERENCES users(user_id),
  CONSTRAINT fk_reports_reported_user FOREIGN KEY (reported_user_id) REFERENCES users(user_id),
  CONSTRAINT fk_reports_review FOREIGN KEY (reported_review_id) REFERENCES reviews(review_id),
  CONSTRAINT fk_reports_decided_by FOREIGN KEY (decided_by) REFERENCES users(user_id) ON DELETE SET NULL,
  INDEX ix_reports_status (status, created_at)
) ENGINE=InnoDB;

-- Global key/value settings (quiz threshold, cancellation window, maintenance mode ...)
CREATE TABLE IF NOT EXISTS platform_settings (
  setting_key    VARCHAR(80) PRIMARY KEY,
  setting_value  VARCHAR(400) NOT NULL,
  description    VARCHAR(300) NULL,
  updated_by     INT NULL,
  updated_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_settings_user FOREIGN KEY (updated_by) REFERENCES users(user_id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- Record of administrator actions
CREATE TABLE IF NOT EXISTS audit_logs (
  audit_id      BIGINT AUTO_INCREMENT PRIMARY KEY,
  admin_id      INT NULL,
  action        VARCHAR(80) NOT NULL,
  target_table  VARCHAR(60) NULL,
  target_id     INT NULL,
  details       VARCHAR(1000) NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_audit_admin FOREIGN KEY (admin_id) REFERENCES users(user_id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- Done: 40 tables.
