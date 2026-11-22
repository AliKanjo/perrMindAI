# Contribution summary

PeerMind AI – SOFT3111 Project #3

> **To complete before submission:** Ammar and Kenan, write under your name what you implemented.
> The list of work areas below covers everything in the project and can be used to divide it.

## Group members

### Ali Kanjo – 23COMP1065

- **Database design:** the relational schema in `database/01_schema.sql` (40 tables with keys, check constraints and indexes), including the unique generated column on `bookings` that prevents two active bookings on the same time slot.
- **Stored procedures and seed data:** the views, triggers and stored procedures in `database/02_procedures.sql` that hold the business rules (booking, cancellation window, reviews, tutor rating), and the demo data in `database/03_seed_data.sql`.
- **Workflow 1 – find and book a tutor:** tutor search with asynchronous filtering (Fetch + JSON), the tutor profile with free time slots, the booking request, "My bookings", booking details with notes, cancellation and review. Main files: `Controllers/TutorsController.cs`, `Controllers/BookingsController.cs`, `Data/TutorRepository.cs`, `Data/BookingRepository.cs`, `Views/Tutors/`, `Views/Bookings/`, `wwwroot/js/tutor-search.js`, `wwwroot/js/booking-form.js`.
- **Project set-up and scope:** created the GitHub repository and decided which workflows to implement for Project #3 (the AI features of the proposal were left out; reports and moderation were added).
- **Local environment and testing:** set up the database in XAMPP, imported the scripts, and ran and tested the application locally.
- **Coordination:** organised the group's documentation and the contribution summary.

### Ammar Azzan – 22COMP1048

- _to be completed_

### Kenan Aljabi – 21COMP1073

Admin – course library (CRUD): create, list, search, edit and delete courses; a course that is already in use is deactivated instead of deleted. Main files: Controllers/CoursesController.cs, Data/CourseRepository.cs, Models/CourseModels.cs, Views/Courses/.
Notifications: the notification list, the bell with the unread count in the header, and "mark all as read" with Fetch. Main files: Controllers/NotificationsController.cs, ViewComponents/NotificationBellViewComponent.cs, Views/Notifications/, Views/Shared/Components/NotificationBell/.
Error and status pages: the custom error page and the pages for status codes such as 404 and access denied. Main files: Controllers/HomeController.cs, Views/Shared/Error.cshtml.
Tutor area – availability: the page where a tutor adds and removes free time slots, with validation of the form. Main files: Controllers/TutorController.cs, Views/Tutor/Availability.cshtml.
Tutor area – public profile editor: the page where a tutor edits the public profile that students see. Main files: Controllers/TutorController.cs, Views/Tutor/Profile.cshtml, Models/TutorModels.cs.
Testing: tested the areas above with the demo accounts, including the error cases (invalid form, duplicate course code, deleting a course that is in use, overlapping time slots).

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
