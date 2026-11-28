using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Html;

namespace PeerMindAI.Helpers;

/// <summary>
/// Small HTML building blocks used by the Razor views (icons, avatars, badges ...).
/// They replace the JavaScript helper functions of the Project #2 prototype,
/// so every page keeps the same look. All text is HTML-encoded here.
/// </summary>
public static class Ui
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private static readonly string[] AvatarColors =
        { "#5B4EF0", "#0EA5E9", "#EC4899", "#F59E0B", "#10B981", "#8B5CF6", "#EF4444", "#14B8A6" };

    // SVG path data of the line icons used in the prototype
    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["home"] = "M3 11l9-8 9 8v10a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z",
        ["book"] = "M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5z",
        ["upload"] = "M12 16V4m0 0l-4 4m4-4l4 4M4 20h16",
        ["search"] = "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM21 21l-4.3-4.3",
        ["cal"] = "M7 3v4M17 3v4M4 9h16M5 5h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z",
        ["bell"] = "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10 21h4",
        ["user"] = "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0",
        ["users"] = "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M17 3.1a4 4 0 0 1 0 7.8M22 21a7 7 0 0 0-5-6.7",
        ["shield"] = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z",
        ["cog"] = "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z",
        ["dollar"] = "M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6",
        ["star"] = "M12 2l3 6.5 7 .8-5.2 4.8 1.4 7L12 17.5 5.8 21l1.4-7L2 9.3l7-.8z",
        ["check"] = "M4 12l5 5L20 6",
        ["lock"] = "M6 11h12a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1zM8 11V7a4 4 0 0 1 8 0v4",
        ["mail"] = "M3 6h18v12H3zM3 7l9 7 9-7",
        ["arrow"] = "M5 12h14m-6-6l6 6-6 6",
        ["logout"] = "M9 21H5a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h4M16 17l5-5-5-5M21 12H9",
        ["file"] = "M14 3H7a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1V7zM14 3v4h4",
        ["flag"] = "M5 21V4m0 0h12l-2 4 2 4H5",
        ["clock"] = "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2",
        ["plus"] = "M12 5v14M5 12h14",
        ["x"] = "M6 6l12 12M18 6L6 18",
        ["lib"] = "M4 4h4v16H4zM10 4h4v16h-4zM16 5.5l3.9-1 3 14.5-3.9 1z",
        ["spark"] = "M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z",
        ["left"] = "M19 12H5m6-6l-6 6 6 6",
        ["menu"] = "M4 6h16M4 12h16M4 18h16",
        ["bot"] = "M12 3v3M5 8h14a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2zM9 13h.01M15 13h.01",
        ["send"] = "M22 2L11 13M22 2l-7 20-4-9-9-4z",
        ["trash"] = "M4 7h16M10 11v6M14 11v6M5 7l1 13a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1l1-13M9 7V4h6v3"
    };

    public static IHtmlContent Icon(string name, int size = 18)
    {
        var path = IconPaths.GetValueOrDefault(name, "");
        var style = size == 18 ? "" : $" style=\"width:{size}px;height:{size}px\"";
        return new HtmlString($"<svg class=\"ic\" viewBox=\"0 0 24 24\"{style}><path d=\"{path}\"/></svg>");
    }

    /// <summary>Coloured circle with the initials of a person (same colour for the same name).</summary>
    public static IHtmlContent Avatar(string? name, int size = 40)
    {
        name = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim();
        var hash = 0;
        foreach (var character in name)
        {
            hash = (hash * 31 + character) % 97;
        }
        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => word[0]));
        var fontSize = (int)Math.Round(size * 0.38);
        return new HtmlString(
            $"<span class=\"av\" style=\"width:{size}px;height:{size}px;background:{AvatarColors[hash % AvatarColors.Length]};" +
            $"font-size:{fontSize}px\">{WebUtility.HtmlEncode(initials)}</span>");
    }

    /// <summary>Pill label. Colours: ok, warn, bad, blue, pri, gray.</summary>
    public static IHtmlContent Badge(string text, string color = "gray") =>
        new HtmlString($"<span class=\"badge b-{color}\">{WebUtility.HtmlEncode(text)}</span>");

    /// <summary>Badge for a booking, account or application status with a matching colour.</summary>
    public static IHtmlContent StatusBadge(string status)
    {
        var (text, color) = status switch
        {
            "Pending" => ("Pending", "warn"),
            "Confirmed" => ("Confirmed", "ok"),
            "Completed" => ("Completed", "blue"),
            "Cancelled" => ("Cancelled", "bad"),
            "Declined" => ("Declined", "bad"),
            "Active" => ("Active", "ok"),
            "Suspended" => ("Suspended", "bad"),
            "PendingVerification" => ("Pending verification", "warn"),
            "Approved" => ("Approved", "ok"),
            "Rejected" => ("Rejected", "bad"),
            "InfoRequested" => ("More info requested", "blue"),
            "Draft" => ("Draft", "gray"),
            _ => (status, "gray")
        };
        return Badge(text, color);
    }

    public static IHtmlContent RoleBadge(string role) =>
        Badge(role, role == "Tutor" ? "pri" : role == "Admin" ? "blue" : "gray");

    public static IHtmlContent Stars(decimal rating) =>
        new HtmlString($"<span style=\"color:#F59E0B\">★</span> <b>{rating.ToString("0.0", CultureInfo.InvariantCulture)}</b>");

    public static string StarRow(int rating) => new string('★', Math.Clamp(rating, 0, 5));

    public static string Money(decimal amount) => "$" + amount.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>"Mon 16 Nov · 16:00", with Today / Tomorrow for near dates.</summary>
    public static string When(DateTime value)
    {
        var day = value.Date == DateTime.Today ? "Today"
            : value.Date == DateTime.Today.AddDays(1) ? "Tomorrow"
            : value.ToString("ddd d MMM", English);
        return $"{day} · {value:HH:mm}";
    }

    public static string Day(DateTime value) => value.ToString("ddd d MMM yyyy", English);

    public static string TimeRange(DateTime start, DateTime end) => $"{start:HH:mm} – {end:HH:mm}";

    /// <summary>"5 min ago", "3 h ago", "2 days ago" or the date.</summary>
    public static string Ago(DateTime value)
    {
        var elapsed = DateTime.Now - value;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays} day(s) ago";
        return value.ToString("d MMM yyyy", English);
    }

    public static string FirstName(string? fullName) =>
        (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
}
