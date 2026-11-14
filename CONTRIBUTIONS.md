# Contribution summary

PeerMind AI – SOFT3111 Project #3

> **To complete before submission:** write under each name what that member implemented.
> The list of work areas below covers everything in the project and can be used to divide it.

## Group members

### Ali Kanjo – 23COMP1065

- _to be completed_

### Ammar Azzan – 22COMP1048

- _to be completed_

### Kenan Aljabi – 21COMP1073

- _to be completed_

## Work areas in this project

| Area | Main files |
|---|---|
| Database design: tables, constraints, indexes | `database/01_schema.sql` |
| Stored procedures, views, triggers and seed data | `database/02_procedures.sql`, `database/03_seed_data.sql` |
| Database access layer | `PeerMindAI/Data/` |
| Accounts: registration, verification, log-in, roles, profile | `Controllers/AccountController.cs`, `Services/SessionUserValidator.cs`, `Views/Account/` |
| Workflow 1: tutor search (Fetch + JSON), tutor profile, booking | `Controllers/TutorsController.cs`, `Controllers/BookingsController.cs`, `Views/Tutors/`, `Views/Bookings/`, `wwwroot/js/tutor-search.js`, `wwwroot/js/booking-form.js` |
| Tutor area: requests, availability, reviews, profile | `Controllers/TutorController.cs`, `Views/Tutor/`, `wwwroot/js/tutor-requests.js` |
| Workflow 2: tutor application and transcript upload | `Controllers/TutorApplicationController.cs`, `Services/TranscriptStorage.cs`, `Views/TutorApplication/` |
| Admin: dashboard, users, tutor verification | `Controllers/AdminController.cs`, `Controllers/VerificationController.cs`, `Views/Admin/`, `Views/Verification/` |
| Admin: course library (create, read, update, delete) | `Controllers/CoursesController.cs`, `Data/CourseRepository.cs`, `Views/Courses/` |
| Admin: reports and moderation | `Controllers/ModerationController.cs`, `Data/ReportRepository.cs`, `Views/Moderation/`, `Views/Shared/_ReportModal.cshtml` |
| Shared layout, styles, client-side validation, toasts | `Views/Shared/`, `wwwroot/css/site.css`, `wwwroot/js/site.js`, `Helpers/Ui.cs` |
| Notifications and error pages | `Controllers/NotificationsController.cs`, `Controllers/HomeController.cs`, `Views/Notifications/`, `Views/Shared/Error.cshtml` |
| Testing and documentation | `README.md` |
