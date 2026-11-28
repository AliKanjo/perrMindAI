-- =====================================================================
-- PeerMind AI - Reset script (MariaDB / MySQL)
-- Removes every table, view, function and procedure of the application,
-- so the database is empty again. ALL DATA IS LOST.
-- Afterwards import 01_schema.sql, 02_procedures.sql and 03_seed_data.sql.
-- Runs in the database selected in phpMyAdmin (e.g. peermindai_db).
-- =====================================================================

SET FOREIGN_KEY_CHECKS = 0;

DROP VIEW IF EXISTS vw_approved_tutors, vw_booking_details, vw_admin_dashboard;

DROP TABLE IF EXISTS audit_logs;
DROP TABLE IF EXISTS platform_settings;
DROP TABLE IF EXISTS reports;
DROP TABLE IF EXISTS ai_usage_logs;
DROP TABLE IF EXISTS ai_message_sources;
DROP TABLE IF EXISTS ai_messages;
DROP TABLE IF EXISTS ai_conversations;
DROP TABLE IF EXISTS notifications;
DROP TABLE IF EXISTS messages;
DROP TABLE IF EXISTS reviews;
DROP TABLE IF EXISTS booking_status_history;
DROP TABLE IF EXISTS bookings;
DROP TABLE IF EXISTS availability_slots;
DROP TABLE IF EXISTS tutor_weekly_availability;
DROP TABLE IF EXISTS study_recommendations;
DROP TABLE IF EXISTS attempt_answers;
DROP TABLE IF EXISTS quiz_attempts;
DROP TABLE IF EXISTS quiz_options;
DROP TABLE IF EXISTS quiz_questions;
DROP TABLE IF EXISTS quizzes;
DROP TABLE IF EXISTS section_references;
DROP TABLE IF EXISTS roadmap_sections;
DROP TABLE IF EXISTS roadmaps;
DROP TABLE IF EXISTS document_chunks;
DROP TABLE IF EXISTS course_materials;
DROP TABLE IF EXISTS enrollments;
DROP TABLE IF EXISTS tutor_courses;
DROP TABLE IF EXISTS application_documents;
DROP TABLE IF EXISTS application_courses;
DROP TABLE IF EXISTS tutor_applications;
DROP TABLE IF EXISTS topics;
DROP TABLE IF EXISTS courses;
DROP TABLE IF EXISTS login_attempts;
DROP TABLE IF EXISTS verification_tokens;
DROP TABLE IF EXISTS user_preferences;
DROP TABLE IF EXISTS tutor_profiles;
DROP TABLE IF EXISTS student_profiles;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS allowed_email_domains;
DROP TABLE IF EXISTS universities;

SET FOREIGN_KEY_CHECKS = 1;

DROP FUNCTION IF EXISTS fn_setting;
DROP PROCEDURE IF EXISTS sp_assert_admin;
DROP PROCEDURE IF EXISTS sp_recalculate_tutor_rating;
DROP PROCEDURE IF EXISTS sp_user_create;
DROP PROCEDURE IF EXISTS sp_user_get_by_id;
DROP PROCEDURE IF EXISTS sp_user_get_by_email;
DROP PROCEDURE IF EXISTS sp_user_list;
DROP PROCEDURE IF EXISTS sp_user_update;
DROP PROCEDURE IF EXISTS sp_user_set_status;
DROP PROCEDURE IF EXISTS sp_user_delete;
DROP PROCEDURE IF EXISTS sp_user_set_password;
DROP PROCEDURE IF EXISTS sp_password_reset_request;
DROP PROCEDURE IF EXISTS sp_password_reset_check;
DROP PROCEDURE IF EXISTS sp_password_reset_complete;
DROP PROCEDURE IF EXISTS sp_login_attempt_record;
DROP PROCEDURE IF EXISTS sp_course_create;
DROP PROCEDURE IF EXISTS sp_course_get_by_id;
DROP PROCEDURE IF EXISTS sp_course_list;
DROP PROCEDURE IF EXISTS sp_course_update;
DROP PROCEDURE IF EXISTS sp_course_delete;
DROP PROCEDURE IF EXISTS sp_tutor_search;
DROP PROCEDURE IF EXISTS sp_tutor_get_profile;
DROP PROCEDURE IF EXISTS sp_tutor_update_profile;
DROP PROCEDURE IF EXISTS sp_tutor_remove_course;
DROP PROCEDURE IF EXISTS sp_slot_add;
DROP PROCEDURE IF EXISTS sp_slot_remove;
DROP PROCEDURE IF EXISTS sp_booking_create;
DROP PROCEDURE IF EXISTS sp_booking_get_by_id;
DROP PROCEDURE IF EXISTS sp_booking_list_by_student;
DROP PROCEDURE IF EXISTS sp_booking_list_by_tutor;
DROP PROCEDURE IF EXISTS sp_booking_update;
DROP PROCEDURE IF EXISTS sp_booking_respond;
DROP PROCEDURE IF EXISTS sp_booking_reschedule;
DROP PROCEDURE IF EXISTS sp_booking_cancel;
DROP PROCEDURE IF EXISTS sp_booking_complete;
DROP PROCEDURE IF EXISTS sp_booking_delete;
DROP PROCEDURE IF EXISTS sp_review_create;
DROP PROCEDURE IF EXISTS sp_review_reply;
DROP PROCEDURE IF EXISTS sp_application_create;
DROP PROCEDURE IF EXISTS sp_application_add_course;
DROP PROCEDURE IF EXISTS sp_application_add_document;
DROP PROCEDURE IF EXISTS sp_application_submit;
DROP PROCEDURE IF EXISTS sp_application_approve;
DROP PROCEDURE IF EXISTS sp_application_reject;
DROP PROCEDURE IF EXISTS sp_application_request_info;
DROP PROCEDURE IF EXISTS sp_application_list_pending;
DROP PROCEDURE IF EXISTS sp_quiz_create;
DROP PROCEDURE IF EXISTS sp_quiz_add_question;
DROP PROCEDURE IF EXISTS sp_quiz_add_option;
DROP PROCEDURE IF EXISTS sp_quiz_delete;
DROP PROCEDURE IF EXISTS sp_quiz_start;
DROP PROCEDURE IF EXISTS sp_quiz_answer;
DROP PROCEDURE IF EXISTS sp_quiz_submit;
DROP PROCEDURE IF EXISTS sp_quiz_get_result;
DROP PROCEDURE IF EXISTS sp_material_create;
DROP PROCEDURE IF EXISTS sp_material_set_processed;
DROP PROCEDURE IF EXISTS sp_material_delete;
DROP PROCEDURE IF EXISTS sp_ai_conversation_create;
DROP PROCEDURE IF EXISTS sp_ai_message_add;
DROP PROCEDURE IF EXISTS sp_ai_message_add_source;
DROP PROCEDURE IF EXISTS sp_ai_conversation_get;
DROP PROCEDURE IF EXISTS sp_report_create;
DROP PROCEDURE IF EXISTS sp_report_decide;
DROP PROCEDURE IF EXISTS sp_notification_mark_all_read;
DROP PROCEDURE IF EXISTS sp_setting_update;
