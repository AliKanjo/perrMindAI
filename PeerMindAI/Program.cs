using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PeerMindAI.Data;
using PeerMindAI.Services;

var builder = WebApplication.CreateBuilder(args);

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
    });

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
