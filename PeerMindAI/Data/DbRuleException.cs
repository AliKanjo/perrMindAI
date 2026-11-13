namespace PeerMindAI.Data;

/// <summary>
/// A business rule was broken (for example "This time slot is no longer available").
/// The stored procedures raise these with SIGNAL SQLSTATE '45000', and the message
/// is written for the end user, so controllers can show it directly.
/// </summary>
public class DbRuleException : Exception
{
    public DbRuleException(string message) : base(message) { }
}
