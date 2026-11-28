using PeerMindAI.Models;

namespace PeerMindAI.Data;

/// <summary>Figures for the admin "AI usage" page, read from ai_usage_logs (FR-13).</summary>
public class AiUsageRepository
{
    private readonly Database _db;

    public AiUsageRepository(Database db) => _db = db;

    public async Task<AiUsagePage> GetPageAsync()
    {
        var page = new AiUsagePage
        {
            Today = await GetTotalsAsync(0),
            Last7Days = await GetTotalsAsync(6),
            Last30Days = await GetTotalsAsync(29)
        };

        page.ByFeature = await _db.QuerySqlAsync(
            @"SELECT feature, COUNT(*) AS requests, SUM(succeeded = 0) AS failed,
                     SUM(input_tokens + output_tokens) AS tokens,
                     ROUND(AVG(CASE WHEN succeeded = 1 THEN latency_ms END)) AS avg_latency
              FROM ai_usage_logs WHERE created_at >= CURDATE() - INTERVAL 29 DAY
              GROUP BY feature ORDER BY requests DESC",
            r => new AiUsageRow
            {
                Label = r.Text("feature"),
                Requests = r.Int("requests"),
                Failed = r.Int("failed"),
                Tokens = Convert.ToInt64(r["tokens"]),
                AverageLatencyMs = r.IntOrNull("avg_latency")
            });

        var perDay = await _db.QuerySqlAsync(
            @"SELECT DATE(created_at) AS day, COUNT(*) AS requests, SUM(succeeded = 0) AS failed
              FROM ai_usage_logs WHERE created_at >= CURDATE() - INTERVAL 13 DAY
              GROUP BY DATE(created_at)",
            r => (Day: r.Date("day").Date, Requests: r.Int("requests"), Failed: r.Int("failed")));
        // one bar for every day, also the days without requests
        page.Days = new List<AiUsageDay>();
        for (var day = DateTime.Today.AddDays(-13); day <= DateTime.Today; day = day.AddDays(1))
        {
            var found = perDay.FirstOrDefault(d => d.Day == day);
            page.Days.Add(new AiUsageDay { Day = day, Requests = found.Requests, Failed = found.Failed });
        }

        page.TopUsers = await _db.QuerySqlAsync(
            @"SELECT IFNULL(u.full_name, '(deleted user)') AS name, COUNT(*) AS requests, SUM(l.succeeded = 0) AS failed,
                     SUM(l.input_tokens + l.output_tokens) AS tokens, NULL AS avg_latency
              FROM ai_usage_logs l LEFT JOIN users u ON u.user_id = l.user_id
              WHERE l.created_at >= CURDATE() - INTERVAL 29 DAY
              GROUP BY l.user_id, u.full_name ORDER BY requests DESC LIMIT 5",
            r => new AiUsageRow
            {
                Label = r.Text("name"),
                Requests = r.Int("requests"),
                Failed = r.Int("failed"),
                Tokens = Convert.ToInt64(r["tokens"])
            });

        page.RecentFailures = await _db.QuerySqlAsync(
            @"SELECT l.created_at, IFNULL(u.full_name, '(deleted user)') AS name, l.feature, l.error_message
              FROM ai_usage_logs l LEFT JOIN users u ON u.user_id = l.user_id
              WHERE l.succeeded = 0 ORDER BY l.created_at DESC, l.log_id DESC LIMIT 8",
            r => new AiUsageFailure
            {
                CreatedAt = r.Date("created_at"),
                UserName = r.Text("name"),
                Feature = r.Text("feature"),
                Error = r.TextOrNull("error_message")
            });
        return page;
    }

    /// <summary>Totals from <paramref name="daysBack"/> days ago until now (0 = today).</summary>
    private async Task<AiUsageTotals> GetTotalsAsync(int daysBack)
    {
        var rows = await _db.QuerySqlAsync(
            @"SELECT COUNT(*) AS requests, IFNULL(SUM(succeeded = 0), 0) AS failed,
                     IFNULL(SUM(input_tokens + output_tokens), 0) AS tokens,
                     ROUND(AVG(CASE WHEN succeeded = 1 THEN latency_ms END)) AS avg_latency
              FROM ai_usage_logs WHERE created_at >= CURDATE() - INTERVAL @days DAY",
            r => new AiUsageTotals
            {
                Requests = r.Int("requests"),
                Failed = r.Int("failed"),
                Tokens = Convert.ToInt64(r["tokens"]),
                AverageLatencyMs = r.IntOrNull("avg_latency")
            }, Database.In("@days", daysBack));
        return rows.FirstOrDefault() ?? new AiUsageTotals();
    }
}
