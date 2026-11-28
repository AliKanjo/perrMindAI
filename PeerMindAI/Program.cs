using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Helpers;
using PeerMindAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Optional files for each computer's own settings (database password, AI API key).
// Both are listed in .gitignore, so secrets are never committed:
//  - appsettings.Local.json (same format as appsettings.json)
//  - .env with lines such as GROQ_API_KEY=... (in the PeerMindAI folder or the repository folder)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddInMemoryCollection(
    DotEnvFile.Load(builder.Environment.ContentRootPath, Path.Combine(builder.Environment.ContentRootPath, "..")));

// MVC, with the anti-forgery token checked on every POST
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// data access (one repository per area of the application)
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<CourseRepository>();
builder.Services.AddSingleton<TutorRepository>();
builder.Services.AddSingleton<BookingRepository>();
builder.Services.AddSingleton<ApplicationRepository>();
builder.Services.AddSingleton<ReportRepository>();
builder.Services.AddSingleton<TranscriptStorage>();
builder.Services.AddSingleton<DemoEmailSender>();
builder.Services.AddSingleton<MaterialRepository>();
builder.Services.AddSingleton<AssistantRepository>();
builder.Services.AddSingleton<MaterialStorage>();
builder.Services.AddSingleton<PdfTextExtractor>();
builder.Services.AddScoped<StudyAssistant>();
builder.Services.AddSingleton<QuizRepository>();
builder.Services.AddSingleton<AiUsageRepository>();
builder.Services.AddScoped<QuizGenerator>();

// AI features: HttpClient for the AI provider, Anthropic (Claude) or Groq (settings in the "AI" section)
builder.Services.AddHttpClient<AiClient>();
builder.Services.AddScoped<SessionUserValidator>();

// cookie log-in with roles (Student, Tutor, Admin)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.EventsType = typeof(SessionUserValidator);
        options.Cookie.Name = "PeerMind.Auth";
        options.Cookie.HttpOnly = true;                            // JavaScript cannot read the log-in cookie
        options.Cookie.SameSite = SameSiteMode.Lax;                // not sent with forms posted from other sites
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

// security headers on every response
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";                 // the browser must not guess file types
    headers["X-Frame-Options"] = "DENY";                           // the site cannot be shown inside another site
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
