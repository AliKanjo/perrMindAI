-- =====================================================================
-- PeerMind AI - Initial demo data (MariaDB / MySQL)
-- Import after 01_schema.sql and 02_procedures.sql.
--
-- * Import it ONCE. If the data is already there, the first INSERT stops
--   with "Duplicate entry" and nothing is changed, so your data is safe.
-- * Dates are relative to the day you import it (sessions "tomorrow",
--   "last week"), so the demo always looks current.
-- * All demo accounts use the password:  Demo@12345
--   (bcrypt hash made with PHP password_hash(); check it with password_verify()).
--
-- Demo log-ins:
--   Student  aylin.kaya@university.edu
--   Tutor    elif.demir@university.edu
--   Admin    dana@peermind.ai
-- =====================================================================
-- Runs in the database selected in phpMyAdmin (e.g. peermindai_db).

SET @pwd = '$2y$10$945R9ISUwXSxl8oVBqdL.ejUeZozqE/dH20iK0btTDN5MW6QSfhzC';
SET @T   = CAST(CURDATE() AS DATETIME);   -- today 00:00
SET @now = NOW();

START TRANSACTION;

-- ---------- Universities & allowed e-mail domains ----------
INSERT INTO universities (university_id, name, city) VALUES
 (1, 'Bilkent University',  'Ankara'),
 (2, 'Koç University',      'Istanbul'),
 (3, 'METU',                'Ankara'),
 (4, 'Sabancı University',  'Istanbul'),
 (5, 'Boğaziçi University', 'Istanbul'),
 (6, 'ITU',                 'Istanbul'),
 (7, 'Işık University',     'Istanbul');

INSERT INTO allowed_email_domains (domain, university_id) VALUES
 ('university.edu', NULL), ('students.university.edu', NULL),
 ('bilkent.edu.tr', 1), ('ku.edu.tr', 2), ('metu.edu.tr', 3), ('sabanciuniv.edu', 4),
 ('boun.edu.tr', 5), ('itu.edu.tr', 6), ('isikun.edu.tr', 7);

-- ---------- Users (1 admin, 6 tutors, 13 students) ----------
INSERT INTO users (user_id, full_name, email, password_hash, role, status, phone, university_id, email_verified, ai_access_enabled, created_at, last_active_at) VALUES
 ( 1, 'Dana Admin',    'dana@peermind.ai',             @pwd, 'Admin',   'Active',    NULL,               1, 1, 1, @T - INTERVAL 120 DAY, @now),
 ( 2, 'Aylin Kaya',    'aylin.kaya@university.edu',    @pwd, 'Student', 'Active',    '+90 555 000 1234', 1, 1, 1, @T - INTERVAL 62 DAY,  @now),
 ( 3, 'Elif Demir',    'elif.demir@university.edu',    @pwd, 'Tutor',   'Active',    '+90 555 000 2001', 1, 1, 1, @T - INTERVAL 110 DAY, @now),
 ( 4, 'Marcus Lee',    'marcus.lee@university.edu',    @pwd, 'Tutor',   'Active',    NULL,               2, 1, 1, @T - INTERVAL 100 DAY, @now - INTERVAL 1 DAY),
 ( 5, 'Sara Haddad',   'sara.haddad@university.edu',   @pwd, 'Tutor',   'Active',    NULL,               3, 1, 1, @T - INTERVAL 98 DAY,  @now - INTERVAL 2 DAY),
 ( 6, 'Daniel Okafor', 'daniel.okafor@university.edu', @pwd, 'Tutor',   'Active',    NULL,               4, 1, 1, @T - INTERVAL 95 DAY,  @now - INTERVAL 3 DAY),
 ( 7, 'Mei Tanaka',    'mei.tanaka@university.edu',    @pwd, 'Tutor',   'Active',    NULL,               5, 1, 1, @T - INTERVAL 90 DAY,  @now - INTERVAL 1 DAY),
 ( 8, 'Ahmet Yıldız',  'ahmet.yildiz@university.edu',  @pwd, 'Tutor',   'Active',    NULL,               6, 1, 1, @T - INTERVAL 88 DAY,  @now - INTERVAL 4 DAY),
 ( 9, 'Can Özkan',     'can.ozkan@university.edu',     @pwd, 'Student', 'Active',    NULL,               1, 1, 1, @T - INTERVAL 50 DAY,  @now - INTERVAL 1 DAY),
 (10, 'Nora Blake',    'nora.blake@university.edu',    @pwd, 'Student', 'Active',    NULL,               1, 1, 1, @T - INTERVAL 45 DAY,  @now - INTERVAL 2 DAY),
 (11, 'Leo Martin',    'leo.martin@university.edu',    @pwd, 'Student', 'Active',    NULL,               1, 1, 1, @T - INTERVAL 70 DAY,  @now - INTERVAL 5 DAY),
 (12, 'Sam Rivera',    'sam.r@university.edu',         @pwd, 'Student', 'Suspended', NULL,               1, 1, 1, @T - INTERVAL 40 DAY,  @now - INTERVAL 8 DAY),
 (13, 'Chris Park',    'chris.park@university.edu',    @pwd, 'Student', 'Active',    NULL,               2, 1, 1, @T - INTERVAL 30 DAY,  @now - INTERVAL 1 DAY),
 (14, 'Ana Silva',     'ana.silva@university.edu',     @pwd, 'Student', 'Active',    NULL,               3, 1, 1, @T - INTERVAL 28 DAY,  @now - INTERVAL 2 DAY),
 (15, 'Ravi Patel',    'ravi.patel@university.edu',    @pwd, 'Student', 'Active',    NULL,               4, 1, 1, @T - INTERVAL 35 DAY,  @now - INTERVAL 1 DAY),
 (16, 'Zeynep Arslan', 'zeynep.arslan@university.edu', @pwd, 'Student', 'Active',    NULL,               1, 1, 1, @T - INTERVAL 80 DAY,  @now - INTERVAL 3 DAY),
 (17, 'Omar Khalil',   'omar.khalil@university.edu',   @pwd, 'Student', 'Active',    NULL,               2, 1, 1, @T - INTERVAL 75 DAY,  @now - INTERVAL 2 DAY),
 (18, 'Lina Petrova',  'lina.petrova@university.edu',  @pwd, 'Student', 'Active',    NULL,               7, 1, 1, @T - INTERVAL 60 DAY,  @now - INTERVAL 6 DAY),
 (19, 'Jay Reed',      'jay.reed@university.edu',      @pwd, 'Student', 'Active',    NULL,               7, 1, 1, @T - INTERVAL 55 DAY,  @now - INTERVAL 4 DAY),
 (20, 'Deniz Aydın',   'deniz.aydin@students.university.edu', @pwd, 'Student', 'PendingVerification', NULL, 7, 0, 1, @now - INTERVAL 2 HOUR, NULL);

-- every non-admin is a learner (tutors are students too)
INSERT INTO student_profiles (user_id, department, year_of_study, student_number, study_streak_days) VALUES
 ( 2, 'Computer Engineering',   2, '2023-48123', 7),
 ( 3, 'Computer Science',       5, '2019-10234', 0),
 ( 4, 'Mathematics',            4, '2020-22011', 0),
 ( 5, 'Computer Science',       6, '2018-30551', 0),
 ( 6, 'Software Engineering',   5, '2019-41200', 0),
 ( 7, 'Mathematics',            4, '2020-55102', 0),
 ( 8, 'Information Systems',    5, '2019-60331', 0),
 ( 9, 'Computer Engineering',   2, '2023-48210', 3),
 (10, 'Computer Engineering',   2, '2023-48300', 1),
 (11, 'Computer Engineering',   3, '2022-31877', 0),
 (12, 'Computer Engineering',   2, '2023-48999', 0),
 (13, 'Computer Engineering',   3, '2022-30110', 2),
 (14, 'Computer Engineering',   4, '2021-20045', 0),
 (15, 'Mathematics',            3, '2022-33001', 0),
 (16, 'Computer Engineering',   2, '2023-48055', 5),
 (17, 'Mechanical Engineering', 2, '2023-50120', 0),
 (18, 'Computer Engineering',   3, '2022-31011', 0),
 (19, 'Software Engineering',   2, '2023-52007', 0),
 (20, 'Computer Engineering',   1, '2026-10001', 0);

INSERT INTO user_preferences (user_id, weekly_report)
SELECT user_id, IF(user_id = 2, 0, 1) FROM users;

INSERT INTO tutor_profiles (user_id, headline, bio, hourly_rate, degree, major, gpa, is_approved, approved_at, accept_same_day, buffer_minutes) VALUES
 (3, 'MSc Computer Science · Data Structures TA',
     'I help students build intuition before exams with worked examples and practice problems. I have tutored Data Structures for 3 years.',
     25, 'MSc', 'Computer Science', 3.70, 1, @T - INTERVAL 100 DAY, 1, 15),
 (4, 'BSc Mathematics · Calculus tutor',      'Step-by-step explanations for integrals and series.',           22, 'BSc', 'Mathematics',          3.60, 1, @T - INTERVAL 92 DAY, 1, 15),
 (5, 'PhD Candidate, CS · Algorithms',        'Algorithm design, proofs and dynamic programming made simple.', 28, 'PhD', 'Computer Science',     3.90, 1, @T - INTERVAL 90 DAY, 0, 30),
 (6, 'MSc Software Eng. · Operating Systems', 'Processes, threads and scheduling with real examples.',         24, 'MSc', 'Software Engineering', 3.50, 1, @T - INTERVAL 87 DAY, 1, 15),
 (7, 'BSc Mathematics · Linear Algebra',      'Matrices and vector spaces explained visually.',                20, 'BSc', 'Mathematics',          3.80, 1, @T - INTERVAL 85 DAY, 1, 0),
 (8, 'MSc Information Systems · Databases',   'SQL, normalisation and database design for your projects.',     21, 'MSc', 'Information Systems',  3.40, 1, @T - INTERVAL 80 DAY, 1, 15);

INSERT INTO verification_tokens (user_id, token, purpose, expires_at) VALUES
 (20, 'c0ffee7d4b8a4a1f9d2e5b6a7c8d9e0f', 'EmailVerification', @now + INTERVAL 22 HOUR);

INSERT INTO login_attempts (user_id, email, succeeded, ip_address, device, attempted_at) VALUES
 (2,  'aylin.kaya@university.edu', 1, '85.105.12.40', 'Chrome on Windows', @now - INTERVAL 1 HOUR),
 (3,  'elif.demir@university.edu', 1, '88.230.4.11',  'Edge on Windows',   @now - INTERVAL 3 HOUR),
 (12, 'sam.r@university.edu',      0, '176.40.2.9',   'Chrome on Android', @now - INTERVAL 5 HOUR),
 (1,  'dana@peermind.ai',          1, '10.0.0.5',     'Chrome on macOS',   @now - INTERVAL 30 MINUTE);

-- ---------- Courses & topics ----------
INSERT INTO courses (course_id, code, name, department, professor, created_at) VALUES
 (1, 'COMP2112', 'Data Structures',        'Computer Engineering', 'Prof. A. Yılmaz', @T - INTERVAL 120 DAY),
 (2, 'MATH1102', 'Calculus II',            'Mathematics',          'Prof. M. Chen',   @T - INTERVAL 120 DAY),
 (3, 'COMP3304', 'Operating Systems',      'Computer Engineering', 'Prof. R. Şahin',  @T - INTERVAL 120 DAY),
 (4, 'MATH2201', 'Linear Algebra',         'Mathematics',          'Prof. N. Aydın',  @T - INTERVAL 120 DAY),
 (5, 'COMP3112', 'Analysis of Algorithms', 'Computer Engineering', 'Prof. S. Arıkan', @T - INTERVAL 120 DAY),
 (6, 'COMP3205', 'Databases',              'Computer Engineering', 'Prof. S. Kaya',   @T - INTERVAL 120 DAY),
 (7, 'PHYS1101', 'Physics I',              'Physics',              'Prof. K. Demir',  @T - INTERVAL 10 DAY);   -- unused: used to demo DELETE

INSERT INTO topics (topic_id, course_id, name) VALUES
 (1,1,'Arrays'), (2,1,'Linked Lists'), (3,1,'Stacks'), (4,1,'Queues'), (5,1,'Trees'), (6,1,'Graphs'),
 (7,1,'Recursion'), (8,1,'Big-O'),
 (9,2,'Integrals'), (10,2,'Series'),
 (11,3,'Processes'), (12,3,'Scheduling'),
 (13,4,'Matrices'),
 (14,5,'Sorting'), (15,5,'Dynamic Programming'),
 (16,6,'SQL'), (17,6,'Normalization');

-- ---------- Tutor applications (6 approved, 3 pending, 1 rejected) ----------
INSERT INTO tutor_applications (application_id, applicant_id, university_id, degree, major, gpa, year_of_study, bio, status, reviewer_note, reviewed_by, submitted_at, reviewed_at) VALUES
 ( 1, 3, 1, 'MSc', 'Computer Science',     3.70, 5, 'CS graduate student and TA with 3 years of tutoring experience.', 'Approved', NULL, 1, @T - INTERVAL 103 DAY, @T - INTERVAL 100 DAY),
 ( 2, 4, 2, 'BSc', 'Mathematics',          3.60, 4, 'Calculus tutor.',            'Approved', NULL, 1, @T - INTERVAL 94 DAY, @T - INTERVAL 92 DAY),
 ( 3, 5, 3, 'PhD', 'Computer Science',     3.90, 6, 'Algorithms researcher.',     'Approved', NULL, 1, @T - INTERVAL 93 DAY, @T - INTERVAL 90 DAY),
 ( 4, 6, 4, 'MSc', 'Software Engineering', 3.50, 5, 'OS teaching assistant.',     'Approved', NULL, 1, @T - INTERVAL 89 DAY, @T - INTERVAL 87 DAY),
 ( 5, 7, 5, 'BSc', 'Mathematics',          3.80, 4, 'Linear algebra enthusiast.', 'Approved', NULL, 1, @T - INTERVAL 88 DAY, @T - INTERVAL 85 DAY),
 ( 6, 8, 6, 'MSc', 'Information Systems',  3.40, 5, 'Database projects mentor.',  'Approved', NULL, 1, @T - INTERVAL 83 DAY, @T - INTERVAL 80 DAY),
 ( 7,13, 2, 'BSc', 'Computer Engineering', 3.55, 3, 'I enjoy explaining sorting and graph algorithms.', 'Pending', NULL, NULL, @now - INTERVAL 20 HOUR, NULL),
 ( 8,14, 3, 'BSc', 'Computer Engineering', 3.75, 4, 'Database course top student, worked on SQL projects.', 'Pending', NULL, NULL, @now - INTERVAL 6 HOUR, NULL),
 ( 9,15, 4, 'BSc', 'Mathematics',          3.30, 3, 'Calculus II tutor.', 'Rejected',
      'The uploaded transcript is not legible. Please upload a clear, official PDF.', 1, @T - INTERVAL 12 DAY, @T - INTERVAL 10 DAY),
 (10,15, 4, 'BSc', 'Mathematics',          3.30, 3, 'Calculus II tutor - resubmitted with a clear transcript.', 'Pending', NULL, NULL, @now - INTERVAL 1 DAY, NULL);

INSERT INTO application_courses (application_id, course_id, grade_obtained) VALUES
 (1,1,'A'), (1,5,'A-'), (2,2,'A'), (2,4,'A-'), (3,5,'A'), (3,1,'A'), (4,3,'A-'),
 (5,4,'A'), (5,2,'B+'), (6,6,'A-'), (7,5,'A-'), (8,6,'A'), (9,2,'B+'), (10,2,'B+');

INSERT INTO application_documents (application_id, document_type, file_name, storage_path, content_type, file_size_bytes, uploaded_at)
SELECT a.application_id, 'Transcript',
       CONCAT('transcript_', REPLACE(LOWER(u.full_name), ' ', '_'), '.pdf'),
       CONCAT('private/applications/', a.application_id, '/transcript.pdf'),
       'application/pdf', 245760 + a.application_id * 1024, a.submitted_at
FROM tutor_applications a JOIN users u ON u.user_id = a.applicant_id;

INSERT INTO tutor_courses (tutor_id, course_id, approved_at)
SELECT a.applicant_id, ac.course_id, a.reviewed_at
FROM tutor_applications a JOIN application_courses ac ON ac.application_id = a.application_id
WHERE a.status = 'Approved';

-- ---------- Enrolments, uploaded materials, AI-processed chunks ----------
INSERT INTO enrollments (enrollment_id, student_id, course_id, progress_percent, enrolled_at) VALUES
 (1, 2, 1, 40, @T - INTERVAL 58 DAY), (2, 2, 2, 62, @T - INTERVAL 58 DAY),
 (3, 2, 3, 25, @T - INTERVAL 40 DAY), (4, 2, 4, 90, @T - INTERVAL 58 DAY),
 (5, 9, 1, 20, @T - INTERVAL 45 DAY), (6,10, 1, 0,  @T - INTERVAL 40 DAY),
 (7,11, 1, 60, @T - INTERVAL 65 DAY), (8,16, 1, 80, @T - INTERVAL 75 DAY),
 (9,17, 2, 30, @T - INTERVAL 70 DAY);

INSERT INTO course_materials (material_id, uploaded_by, course_id, file_name, storage_path, file_type, file_size_bytes, page_count, processing_status, use_as_ai_source, uploaded_at) VALUES
 (1, 2, 1, 'Syllabus.pdf',              'private/materials/2/1/syllabus.pdf',      'PDF',  182044,  6,  'Processed', 1, @T - INTERVAL 57 DAY),
 (2, 2, 1, 'Lecture 5 – Stacks.pdf',    'private/materials/2/1/lecture5.pdf',      'PDF',  2411520, 24, 'Processed', 1, @T - INTERVAL 20 DAY),
 (3, 2, 1, 'Lecture 6 – Queues.pptx',   'private/materials/2/1/lecture6.pptx',     'PPTX', 3145728, 18, 'Processed', 1, @T - INTERVAL 13 DAY),
 (4, 2, 1, 'Textbook ch. 6.pdf',        'private/materials/2/1/textbook-ch6.pdf',  'PDF',  5242880, 30, 'Processed', 1, @T - INTERVAL 20 DAY),
 (5, 2, 2, 'Integrals cheat sheet.pdf', 'private/materials/2/2/integrals.pdf',     'PDF',  401408,  4,  'Uploaded',  1, @now - INTERVAL 1 HOUR);

INSERT INTO document_chunks (chunk_id, material_id, page_number, chunk_index, content) VALUES
 (1, 1, 4,  1, 'Week 5: Stacks and queues. Week 6: Queue variants and BFS. Section quizzes require 75% to pass.'),
 (2, 2, 3,  1, 'A stack is a linear structure where the last element added is the first removed (LIFO). Operations: push, pop, peek.'),
 (3, 2, 8,  2, 'LIFO vs FIFO: a stack removes the newest element first, a queue removes the oldest element first.'),
 (4, 3, 2,  1, 'A queue adds elements at the rear (enqueue) and removes them from the front (dequeue).'),
 (5, 3, 9,  2, 'Breadth-first search explores a graph level by level using a FIFO queue.'),
 (6, 4, 12, 1, 'Array-backed stacks double their capacity when full, so push costs O(1) amortized time.');

-- ---------- AI roadmap for Aylin's Data Structures course ----------
INSERT INTO roadmaps (roadmap_id, enrollment_id, generated_at) VALUES (1, 1, @T - INTERVAL 57 DAY);

INSERT INTO roadmap_sections (section_id, roadmap_id, section_number, title, lesson_content, estimated_minutes, topic_id, status, best_score) VALUES
 (1, 1, 1, 'Introduction',            'What data structures are and how we measure their cost with Big-O.', 20, 8, 'Passed', 90),
 (2, 1, 2, 'Arrays and Linked Lists', 'Contiguous arrays versus node-based linked lists and pointer handling.', 30, 2, 'Passed', 80),
 (3, 1, 3, 'Stacks and Queues',
     '1. Stacks (LIFO): the last element added is the first removed, like a pile of plates. 2. Queues (FIFO): the oldest element is removed first, like people waiting in line. Key takeaways: push/pop and enqueue/dequeue are O(1); stacks power undo and recursion; queues power BFS and scheduling.',
     25, 3, 'Current', 60),
 (4, 1, 4, 'Trees',  'Binary trees, traversals and binary search trees.', 35, 5, 'Locked', NULL),
 (5, 1, 5, 'Graphs', 'Graph representations, BFS and DFS.',              40, 6, 'Locked', NULL);

INSERT INTO section_references (section_id, material_id, page_number) VALUES
 (3, 2, 3), (3, 3, 2), (3, 1, 4), (1, 1, 1), (2, 4, 1);

-- ---------- Section 3 quiz (5 questions) and Aylin's first, failed attempt ----------
INSERT INTO quizzes (quiz_id, student_id, course_id, section_id, material_id, title, pass_mark_percent, generated_by_ai, created_at) VALUES
 (1, 2, 1, 3, 2, 'Section 3 quiz – Stacks and Queues', 75, 1, @T - INTERVAL 2 DAY);

INSERT INTO quiz_questions (question_id, quiz_id, question_number, question_text, explanation, topic_id, source_material_id, source_page) VALUES
 (1, 1, 1, 'Which principle does a stack follow?',
     'A stack removes the most recently added element first, which is LIFO.', 3, 2, 3),
 (2, 1, 2, 'Which operation adds an element to the back of a queue?',
     'Enqueue inserts at the rear; dequeue removes from the front.', 4, 3, 2),
 (3, 1, 3, 'What is the amortized time complexity of push on an array-backed stack?',
     'Occasional resizing is spread over many pushes, giving amortized O(1).', 3, 4, 12),
 (4, 1, 4, 'Which structure is typically used for breadth-first search?',
     'BFS explores level by level, so it uses a FIFO queue.', 4, 3, 9),
 (5, 1, 5, 'Which feature is most naturally built with a stack?',
     'Undo reverses the latest action first, matching LIFO behaviour.', 3, 2, 8);

INSERT INTO quiz_options (option_id, question_id, label, option_text, is_correct) VALUES
 ( 1,1,'A','FIFO – first in, first out',0), ( 2,1,'B','LIFO – last in, first out',1), ( 3,1,'C','Random access',0), ( 4,1,'D','Priority ordering',0),
 ( 5,2,'A','push',0), ( 6,2,'B','pop',0), ( 7,2,'C','enqueue',1), ( 8,2,'D','peek',0),
 ( 9,3,'A','O(n)',0), (10,3,'B','O(log n)',0), (11,3,'C','O(n²)',0), (12,3,'D','O(1)',1),
 (13,4,'A','Stack',0), (14,4,'B','Queue',1), (15,4,'C','Heap',0), (16,4,'D','Hash table',0),
 (17,5,'A','Print job scheduling',0), (18,5,'B','Undo / redo history',1), (19,5,'C','Round-robin scheduling',0), (20,5,'D','Message buffering',0);

INSERT INTO quiz_attempts (attempt_id, quiz_id, student_id, score_percent, passed, started_at, submitted_at) VALUES
 (1, 1, 2, 60, 0, @now - INTERVAL 1 DAY - INTERVAL 12 MINUTE, @now - INTERVAL 1 DAY);

INSERT INTO attempt_answers (attempt_id, question_id, selected_option_id, is_correct) VALUES
 (1,1,2,1), (1,2,7,1), (1,3,9,0), (1,4,13,0), (1,5,18,1);   -- 3 of 5 = 60%

INSERT INTO study_recommendations (student_id, attempt_id, course_id, topic_id, material_id, recommendation_type, message, created_at) VALUES
 (2, 1, 1, 3, 4,       'ReviewMaterial', 'Review "Stacks" - you missed 1 question(s) on this topic.', @now - INTERVAL 1 DAY),
 (2, 1, 1, 4, 3,       'ReviewMaterial', 'Review "Queues" - you missed 1 question(s) on this topic.', @now - INTERVAL 1 DAY),
 (2, 1, 1, NULL, NULL, 'RetryQuiz',      'You scored 60%. Review the explanations and retry - the pass mark is 75%.', @now - INTERVAL 1 DAY),
 (2, 1, 1, 2, NULL,    'FindTutor',      'A verified tutor can help you with Data Structures.', @now - INTERVAL 1 DAY);

-- ---------- Tutor availability ----------
INSERT INTO tutor_weekly_availability (tutor_id, day_of_week, start_time, end_time) VALUES
 (3,1,'13:00','19:00'), (3,2,'13:00','19:00'), (3,3,'13:00','19:00'), (3,4,'13:00','19:00'), (3,5,'13:00','19:00'), (3,6,'09:00','13:00'),
 (4,1,'10:00','12:30'), (4,3,'10:00','12:30'), (4,5,'10:00','12:30'),
 (5,2,'14:00','16:00'), (5,4,'14:00','16:00');

INSERT INTO availability_slots (slot_id, tutor_id, start_at, end_at, is_open) VALUES
 -- past slots (completed / cancelled sessions)
 ( 1, 5, @T - INTERVAL 11 DAY + INTERVAL 14 HOUR,   @T - INTERVAL 11 DAY + INTERVAL 15 HOUR, 1),
 ( 2, 7, @T - INTERVAL 26 DAY + INTERVAL 10 HOUR,   @T - INTERVAL 26 DAY + INTERVAL 11 HOUR, 1),
 ( 3, 8, @T - INTERVAL 32 DAY + INTERVAL 1050 MINUTE, @T - INTERVAL 32 DAY + INTERVAL 1110 MINUTE, 1),
 ( 4, 3, @T - INTERVAL 20 DAY + INTERVAL 16 HOUR,   @T - INTERVAL 20 DAY + INTERVAL 17 HOUR, 1),
 ( 5, 3, @T - INTERVAL 15 DAY + INTERVAL 16 HOUR,   @T - INTERVAL 15 DAY + INTERVAL 17 HOUR, 1),
 ( 6, 3, @T - INTERVAL 9 DAY + INTERVAL 18 HOUR,    @T - INTERVAL 9 DAY + INTERVAL 19 HOUR, 1),
 ( 7, 3, @T - INTERVAL 5 DAY + INTERVAL 18 HOUR,    @T - INTERVAL 5 DAY + INTERVAL 19 HOUR, 1),
 ( 8, 6, @T - INTERVAL 3 DAY + INTERVAL 15 HOUR,    @T - INTERVAL 3 DAY + INTERVAL 16 HOUR, 1),
 -- upcoming booked slots
 ( 9, 3, @T + INTERVAL 1 DAY + INTERVAL 16 HOUR,    @T + INTERVAL 1 DAY + INTERVAL 17 HOUR, 1),
 (10, 3, @T + INTERVAL 1 DAY + INTERVAL 18 HOUR,    @T + INTERVAL 1 DAY + INTERVAL 19 HOUR, 1),
 (11, 4, @T + INTERVAL 2 DAY + INTERVAL 690 MINUTE, @T + INTERVAL 2 DAY + INTERVAL 750 MINUTE, 1),
 (12, 3, @T + INTERVAL 2 DAY + INTERVAL 10 HOUR,    @T + INTERVAL 2 DAY + INTERVAL 11 HOUR, 1),
 (13, 3, @T + INTERVAL 3 DAY + INTERVAL 14 HOUR,    @T + INTERVAL 3 DAY + INTERVAL 15 HOUR, 1),
 (14, 3, @T + INTERVAL 4 DAY + INTERVAL 1050 MINUTE, @T + INTERVAL 4 DAY + INTERVAL 1110 MINUTE, 1),
 -- upcoming FREE slots (for search and booking demos)
 (15, 3, @T + INTERVAL 3 DAY + INTERVAL 16 HOUR,    @T + INTERVAL 3 DAY + INTERVAL 17 HOUR, 1),
 (16, 3, @T + INTERVAL 5 DAY + INTERVAL 13 HOUR,    @T + INTERVAL 5 DAY + INTERVAL 14 HOUR, 1),
 (17, 3, @T + INTERVAL 6 DAY + INTERVAL 15 HOUR,    @T + INTERVAL 6 DAY + INTERVAL 16 HOUR, 1),
 (18, 5, @T + INTERVAL 2 DAY + INTERVAL 14 HOUR,    @T + INTERVAL 2 DAY + INTERVAL 15 HOUR, 1),
 (19, 5, @T + INTERVAL 4 DAY + INTERVAL 14 HOUR,    @T + INTERVAL 4 DAY + INTERVAL 15 HOUR, 1),
 (20, 4, @T + INTERVAL 3 DAY + INTERVAL 10 HOUR,    @T + INTERVAL 3 DAY + INTERVAL 11 HOUR, 1),
 (21, 6, @T + INTERVAL 4 DAY + INTERVAL 15 HOUR,    @T + INTERVAL 4 DAY + INTERVAL 16 HOUR, 1),
 (22, 7, @T + INTERVAL 2 DAY + INTERVAL 10 HOUR,    @T + INTERVAL 2 DAY + INTERVAL 11 HOUR, 1),
 (23, 8, @T + INTERVAL 5 DAY + INTERVAL 1050 MINUTE, @T + INTERVAL 5 DAY + INTERVAL 1110 MINUTE, 1),
 (24, 7, @T + INTERVAL 7 DAY + INTERVAL 11 HOUR,    @T + INTERVAL 7 DAY + INTERVAL 12 HOUR, 1),
 -- more past sessions so every tutor has reviews
 (25, 4, @T - INTERVAL 12 DAY + INTERVAL 10 HOUR,   @T - INTERVAL 12 DAY + INTERVAL 11 HOUR, 1),
 (26, 8, @T - INTERVAL 18 DAY + INTERVAL 1050 MINUTE, @T - INTERVAL 18 DAY + INTERVAL 1110 MINUTE, 1),
 (27, 7, @T - INTERVAL 14 DAY + INTERVAL 11 HOUR,   @T - INTERVAL 14 DAY + INTERVAL 12 HOUR, 1),
 -- yesterday's session: confirmed and finished, so the tutor can "Mark as completed" in the demo
 (28, 3, @T - INTERVAL 1 DAY + INTERVAL 15 HOUR,    @T - INTERVAL 1 DAY + INTERVAL 16 HOUR, 1);

-- ---------- Bookings ----------
INSERT INTO bookings (booking_id, booking_code, student_id, tutor_id, slot_id, course_id, topic, notes, status, price, cancellation_reason, created_at) VALUES
 ( 1, 'PM-20460',  2, 5,  1, 5, 'Dynamic programming',  NULL, 'Completed', 28, NULL, @T - INTERVAL 14 DAY),
 ( 2, 'PM-20455',  2, 7,  2, 4, 'Eigenvalues',          NULL, 'Completed', 20, NULL, @T - INTERVAL 29 DAY),
 ( 3, 'PM-20450',  2, 8,  3, 6, 'SQL joins',            NULL, 'Cancelled', 21, 'My exam was moved, so I no longer need this session.', @T - INTERVAL 36 DAY),
 ( 4, 'PM-20440', 16, 3,  4, 1, 'Linked list pointers', NULL, 'Completed', 25, NULL, @T - INTERVAL 23 DAY),
 ( 5, 'PM-20445', 17, 3,  5, 5, 'Sorting algorithms',   NULL, 'Completed', 25, NULL, @T - INTERVAL 18 DAY),
 ( 6, 'PM-20462', 18, 3,  6, 1, 'Trees',                NULL, 'Completed', 25, NULL, @T - INTERVAL 12 DAY),
 ( 7, 'PM-20470', 19, 3,  7, 1, 'Exam preparation',     NULL, 'Completed', 25, NULL, @T - INTERVAL 8 DAY),
 ( 8, 'PM-20475', 11, 6,  8, 3, 'CPU scheduling',       NULL, 'Completed', 24, NULL, @T - INTERVAL 6 DAY),
 ( 9, 'PM-20481',  2, 3,  9, 1, 'Stacks and Queues', 'Focus on queue implementation and BFS before Friday''s quiz.', 'Confirmed', 25, NULL, @T - INTERVAL 3 DAY),
 (10, 'PM-20482',  9, 3, 10, 1, 'Trees',                NULL, 'Confirmed', 25, NULL, @T - INTERVAL 2 DAY),
 (11, 'PM-20483',  2, 4, 11, 2, 'Integrals',            NULL, 'Confirmed', 22, NULL, @T - INTERVAL 2 DAY),
 (12, 'PM-20490',  2, 3, 12, 1, 'Exam preparation',     NULL, 'Pending',   25, NULL, @now - INTERVAL 5 HOUR),
 (13, 'PM-20491',  9, 3, 13, 5, 'Graph algorithms',     NULL, 'Pending',   25, NULL, @now - INTERVAL 4 HOUR),
 (14, 'PM-20492', 10, 3, 14, 1, 'Queues',               NULL, 'Pending',   25, NULL, @now - INTERVAL 2 HOUR),
 (15, 'PM-20446', 17, 4, 25, 2, 'Integration by parts', NULL, 'Completed', 22, NULL, @T - INTERVAL 15 DAY),
 (16, 'PM-20447',  9, 8, 26, 6, 'Normalization',        NULL, 'Completed', 21, NULL, @T - INTERVAL 21 DAY),
 (17, 'PM-20448', 16, 7, 27, 4, 'Matrix inverse',       NULL, 'Completed', 20, NULL, @T - INTERVAL 17 DAY),
 (18, 'PM-20485',  2, 3, 28, 1, 'Recursion practice',   NULL, 'Confirmed', 25, NULL, @T - INTERVAL 4 DAY);

-- status history: requested -> confirmed -> completed / cancelled
INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, note, changed_at)
SELECT booking_id, NULL, 'Pending', student_id, 'Booking requested', created_at FROM bookings;
INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, changed_at)
SELECT booking_id, 'Pending', 'Confirmed', tutor_id, created_at + INTERVAL 2 HOUR
FROM bookings WHERE status IN ('Confirmed','Completed','Cancelled');
INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, changed_at)
SELECT b.booking_id, 'Confirmed', 'Completed', b.tutor_id, s.end_at
FROM bookings b JOIN availability_slots s ON s.slot_id = b.slot_id WHERE b.status = 'Completed';
INSERT INTO booking_status_history (booking_id, old_status, new_status, changed_by, note, changed_at)
SELECT booking_id, 'Confirmed', 'Cancelled', student_id, cancellation_reason, created_at + INTERVAL 1 DAY
FROM bookings WHERE status = 'Cancelled';

-- ---------- Reviews (triggers fill average_rating / review_count) ----------
INSERT INTO reviews (review_id, booking_id, student_id, tutor_id, rating, comment, tutor_reply, created_at) VALUES
 (1,  1,  2, 5, 5, 'Great at explaining dynamic programming step by step.', NULL, @T - INTERVAL 10 DAY),
 (2,  4, 16, 3, 5, 'Explained pointers so clearly. Passed my midterm!', 'So happy for you, Zeynep!', @T - INTERVAL 19 DAY),
 (3,  5, 17, 3, 5, 'Patient, prepared and friendly.', NULL, @T - INTERVAL 14 DAY),
 (4,  6, 18, 3, 4, 'Great examples, would book again.', 'Thanks Lina!', @T - INTERVAL 8 DAY),
 (5,  7, 19, 3, 4, 'Helpful, though we ran out of time. Buy cheap essays at essay-deals.example!!', NULL, @T - INTERVAL 4 DAY),
 (6,  8, 11, 6, 5, 'Finally understood round-robin scheduling.', NULL, @T - INTERVAL 2 DAY),
 (7, 15, 17, 4, 5, 'Integration by parts finally makes sense.', NULL, @T - INTERVAL 11 DAY),
 (8, 16,  9, 8, 4, 'Good normalisation examples.', NULL, @T - INTERVAL 17 DAY),
 (9, 17, 16, 7, 5, 'Very visual explanations of matrices.', NULL, @T - INTERVAL 13 DAY);
-- booking 2 (Aylin with Mei Tanaka) is completed but not reviewed yet -> used in the demo

-- ---------- Messages & notifications ----------
INSERT INTO messages (sender_id, receiver_id, booking_id, body, sent_at, read_at) VALUES
 (2, 3, 9,     'Hi Elif, could we focus on BFS with queues in our session?', @now - INTERVAL 26 HOUR, @now - INTERVAL 25 HOUR),
 (3, 2, 9,     'Sure! Bring your Lecture 6 slides and we will go through BFS together.', @now - INTERVAL 25 HOUR, @now - INTERVAL 24 HOUR),
 (2, 4, 11,    'Can we cover integration by parts?', @now - INTERVAL 3 HOUR, @now - INTERVAL 2 HOUR),
 (4, 2, 11,    'Yes, I will prepare some practice integrals for you.', @now - INTERVAL 1 HOUR, NULL),
 (12, 2, NULL, '[message hidden by moderation]', @now - INTERVAL 9 DAY, @now - INTERVAL 9 DAY);

INSERT INTO notifications (user_id, type, title, body, link_url, is_read, created_at) VALUES
 (2,  'Booking',       'Booking confirmed',   'Elif Demir confirmed your Data Structures session.',   '/bookings/9',     0, @now - INTERVAL 2 MINUTE),
 (2,  'Message',       'Tutor reply',         'Marcus Lee replied to your message about integrals.', '/bookings/11',    0, @now - INTERVAL 1 HOUR),
 (2,  'StudyReminder', 'Study reminder',      'Section 3 quiz is waiting - keep your 7-day streak!', '/roadmap/1',      0, @now - INTERVAL 3 HOUR),
 (2,  'Quiz',          'Quiz result',         'You scored 60% in Section 3 · Stacks and Queues.',    '/quiz-results/1', 1, @now - INTERVAL 1 DAY),
 (2,  'Review',        'Rate your session',   'How was your session with Mei Tanaka?',               '/bookings/2',     1, @now - INTERVAL 2 DAY),
 (3,  'Booking',       'New booking request', 'Aylin Kaya requested a Data Structures session.',     '/tutor/requests', 0, @now - INTERVAL 5 HOUR),
 (3,  'Booking',       'New booking request', 'Can Özkan requested an Algorithms session.',          '/tutor/requests', 0, @now - INTERVAL 4 HOUR),
 (3,  'Booking',       'New booking request', 'Nora Blake requested a Data Structures session.',     '/tutor/requests', 0, @now - INTERVAL 2 HOUR),
 (1,  'Application',   'New tutor application', 'Chris Park applied to tutor Analysis of Algorithms.', '/admin/tutor-verification', 0, @now - INTERVAL 20 HOUR),
 (1,  'Application',   'New tutor application', 'Ana Silva applied to tutor Databases.',              '/admin/tutor-verification', 0, @now - INTERVAL 6 HOUR),
 (15, 'Application',   'Application not approved', 'The uploaded transcript is not legible. Please upload a clear, official PDF.', '/tutor/verification', 1, @T - INTERVAL 10 DAY);

-- ---------- AI assistant conversation with page references ----------
INSERT INTO ai_conversations (conversation_id, student_id, course_id, material_id, title, created_at) VALUES
 (1, 2, 1, 2, 'Stacks and queues questions', @now - INTERVAL 6 HOUR);

INSERT INTO ai_messages (message_id, conversation_id, sender, content, is_grounded, created_at) VALUES
 (1, 1, 'User',      'Explain LIFO vs FIFO', NULL, @now - INTERVAL 360 MINUTE),
 (2, 1, 'Assistant', 'A stack is LIFO (last in, first out) while a queue is FIFO (first in, first out).', 1, @now - INTERVAL 359 MINUTE),
 (3, 1, 'User',      'Summarise Lecture 5', NULL, @now - INTERVAL 350 MINUTE),
 (4, 1, 'Assistant', 'Lecture 5 covers the stack definition, push/pop, array vs linked implementations, and uses such as undo.', 1, @now - INTERVAL 349 MINUTE),
 (5, 1, 'User',      'Who invented the queue data structure?', NULL, @now - INTERVAL 340 MINUTE),
 (6, 1, 'Assistant', 'I could not find this in your uploaded materials, so I cannot give a verified answer.', 0, @now - INTERVAL 339 MINUTE);

INSERT INTO ai_message_sources (message_id, chunk_id) VALUES (2, 3), (4, 2), (4, 6);

INSERT INTO ai_usage_logs (user_id, feature, model, input_tokens, output_tokens, cost_usd, latency_ms, succeeded, error_message, created_at) VALUES
 (2, 'DocumentProcessing', 'embedding-small', 18400, 0,    0.00037, 2100,  1, NULL, @T - INTERVAL 20 DAY),
 (2, 'RoadmapGenerator',   'llm-standard',    9200,  2100, 0.05910, 6400,  1, NULL, @T - INTERVAL 57 DAY),
 (2, 'QuizGenerator',      'llm-standard',    6100,  1400, 0.03930, 3900,  1, NULL, @T - INTERVAL 2 DAY),
 (2, 'StudyAssistant',     'llm-standard',    1800,  120,  0.00720, 1100,  1, NULL, @now - INTERVAL 359 MINUTE),
 (2, 'StudyAssistant',     'llm-standard',    1900,  160,  0.00810, 1300,  1, NULL, @now - INTERVAL 349 MINUTE),
 (2, 'StudyAssistant',     'llm-standard',    1700,  60,   0.00600, 900,   1, NULL, @now - INTERVAL 339 MINUTE),
 (9, 'StudyAssistant',     'llm-standard',    0,     0,    0,       30000, 0, 'AI service timeout', @now - INTERVAL 3 HOUR);

-- ---------- Reports & moderation ----------
INSERT INTO reports (report_id, reporter_id, reported_user_id, reported_review_id, report_type, description, status, decided_by, decision_note, created_at, decided_at) VALUES
 (1,  2, 12, NULL, 'InappropriateMessage', 'Chat message contained offensive language.', 'Open', NULL, NULL, @now - INTERVAL 1 HOUR, NULL),
 (2, 11,  6, NULL, 'Other', 'Tutor joined the session 30 minutes late and ended early.', 'Open', NULL, NULL, @now - INTERVAL 1 DAY, NULL),
 (3,  3, 19,    5, 'Spam',  'Repeated promotional links in review text.', 'Open', NULL, NULL, @now - INTERVAL 20 HOUR, NULL),
 (4, 17, 12, NULL, 'InappropriateContent', 'Abusive messages after a study group session.', 'UserSuspended', 1,
     'Second offence - account suspended.', @T - INTERVAL 9 DAY, @T - INTERVAL 8 DAY);

-- ---------- Platform settings (admin "Platform settings" page) ----------
INSERT INTO platform_settings (setting_key, setting_value, description, updated_by) VALUES
 ('RequireTwoFactorForAdminsAndTutors', 'true',  'Enforce 2FA for administrators and verified tutors.', 1),
 ('SessionTimeoutMinutes',              '30',    'Sign users out after this many minutes of inactivity.', 1),
 ('MinimumPasswordLength',              '10',    'Applies to new accounts and password resets.', 1),
 ('RequirePasswordComplexity',          'true',  'Upper and lower case, a number and a symbol.', 1),
 ('MaxFailedLoginAttempts',             '5',     'Temporarily lock an account after this many failures.', 1),
 ('NewDeviceLoginAlerts',               'true',  'E-mail users when a new device signs in.', 1),
 ('RestrictSignupToUniversityEmails',   'true',  'Only domains in allowed_email_domains can register.', 1),
 ('RequireEmailVerification',           'true',  'Users must confirm their e-mail before access.', 1),
 ('TutorApprovalBeforeListing',         'true',  'Tutors appear in search only after admin approval.', 1),
 ('TutorMinimumGPA',                    '3.0',   'Minimum GPA to apply as a tutor.', 1),
 ('QuizPassingThresholdPercent',        '75',    'Default pass mark for new section quizzes.', 1),
 ('BookingCancellationWindowHours',     '24',    'Students can cancel confirmed sessions up to this many hours before.', 1),
 ('AllowUsersToDeleteOwnAccount',       'false', 'When false, deletion requires an administrator.', 1),
 ('MaxUploadSizeMB',                    '50',    'Largest allowed course material upload.', 1),
 ('PlatformName',                       'PeerMindAI', 'Shown in e-mails and the browser title.', 1),
 ('DefaultLanguage',                    'English', 'Used for new accounts and system e-mails.', 1),
 ('DefaultTimeZone',                    'Europe/Istanbul', 'Used for scheduling unless a user sets their own.', 1),
 ('SupportEmail',                       'support@peermind.ai', 'Where help requests and reports are sent.', 1),
 ('EnableAiStudyAssistant',             'true',  'Let students ask questions about their materials.', 1),
 ('AiDisclaimer',                       'AI-generated answers may contain errors. Check them against the referenced course materials.', 'Shown under every AI answer (NFR-07).', 1),
 ('MaintenanceMode',                    'false', 'Block non-admin access while changes are made.', 1);

INSERT INTO audit_logs (admin_id, action, target_table, target_id, details, created_at)
SELECT 1, 'Tutor application approved', 'tutor_applications', application_id, NULL, reviewed_at
FROM tutor_applications WHERE status = 'Approved';
INSERT INTO audit_logs (admin_id, action, target_table, target_id, details, created_at) VALUES
 (1, 'Tutor application rejected', 'tutor_applications', 9, 'Transcript not legible', @T - INTERVAL 10 DAY),
 (1, 'Report decided: UserSuspended', 'reports', 4, 'Second offence - account suspended.', @T - INTERVAL 8 DAY);

COMMIT;
