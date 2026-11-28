namespace PeerMindAI.Helpers;

/// <summary>
/// Reads a <c>.env</c> file (lines of NAME=value) and turns the names into configuration settings,
/// so secrets such as the AI API key can live in a file that is never committed (it is in .gitignore).
///
/// Recognised names:
/// <list type="bullet">
/// <item><c>GROQ_API_KEY</c> – Groq key; the provider becomes Groq.</item>
/// <item><c>ANTHROPIC_API_KEY</c> – Anthropic key; the provider becomes Anthropic.</item>
/// <item><c>AI_PROVIDER</c> – "Groq" or "Anthropic", when both keys are present.</item>
/// <item><c>AI_MODEL</c> – another model than the default.</item>
/// <item><c>DB_CONNECTION</c> – the database connection string.</item>
/// <item>Any name written like an ASP.NET setting with "__", for example <c>AI__TimeoutSeconds</c>.</item>
/// </list>
/// </summary>
public static class DotEnvFile
{
    /// <summary>The settings from the first .env file found in <paramref name="folders"/> (empty if there is none).</summary>
    public static Dictionary<string, string?> Load(params string[] folders)
    {
        var path = folders.Select(folder => Path.Combine(folder, ".env")).FirstOrDefault(File.Exists);
        return path == null ? new Dictionary<string, string?>() : ToSettings(Parse(File.ReadAllLines(path)));
    }

    /// <summary>NAME=value pairs; blank lines and lines starting with # are skipped, quotes around values removed.</summary>
    public static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            if (line.StartsWith("export "))
            {
                line = line["export ".Length..].TrimStart();
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;   // not a NAME=value line
            }
            var name = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }
            values[name] = value;
        }
        return values;
    }

    /// <summary>Translates the .env names into the configuration keys the application reads.</summary>
    public static Dictionary<string, string?> ToSettings(Dictionary<string, string> env)
    {
        var settings = new Dictionary<string, string?>();

        // names in ASP.NET style (AI__Model) are used as they are
        foreach (var (name, value) in env.Where(pair => pair.Key.Contains("__")))
        {
            settings[name.Replace("__", ":")] = value;
        }

        var groqKey = env.GetValueOrDefault("GROQ_API_KEY");
        var anthropicKey = env.GetValueOrDefault("ANTHROPIC_API_KEY");
        var provider = env.GetValueOrDefault("AI_PROVIDER");
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = !string.IsNullOrWhiteSpace(groqKey) ? "Groq" : !string.IsNullOrWhiteSpace(anthropicKey) ? "Anthropic" : null;
        }

        if (provider != null)
        {
            settings["AI:Provider"] = provider;
            var key = provider.Equals("Groq", StringComparison.OrdinalIgnoreCase) ? groqKey : anthropicKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                settings["AI:ApiKey"] = key;
            }
        }
        if (env.GetValueOrDefault("AI_MODEL") is { Length: > 0 } model)
        {
            settings["AI:Model"] = model;
        }
        if (env.GetValueOrDefault("DB_CONNECTION") is { Length: > 0 } connection)
        {
            settings["ConnectionStrings:PeerMind"] = connection;
        }
        return settings;
    }
}
