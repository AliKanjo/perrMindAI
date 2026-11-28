namespace PeerMindAI.Services;

/// <summary>
/// Stand-in for an e-mail service. The project has no mail server, so the
/// message is written to the application log (the terminal where the app runs).
/// A real deployment would replace this class with one that sends the e-mail.
/// </summary>
public class DemoEmailSender
{
    private readonly ILogger<DemoEmailSender> _logger;

    public DemoEmailSender(ILogger<DemoEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogWarning("DEMO E-MAIL (not really sent)\n  To:      {To}\n  Subject: {Subject}\n  {Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
