using System.Text.RegularExpressions;
using PeerMindAI.Models;

namespace PeerMindAI.Services;

/// <summary>
/// Finds the pieces of the student's materials that best match a question,
/// using simple keyword scoring. Only these pieces are sent to the AI, which
/// keeps answers tied to the course materials and keeps requests small.
/// </summary>
public static partial class SourceFinder
{
    // very common words that say nothing about the topic
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "but", "not", "you", "your", "with", "this", "that", "from", "have", "has",
        "what", "when", "where", "which", "who", "why", "how", "can", "could", "would", "should", "does", "did",
        "was", "were", "will", "into", "about", "between", "explain", "describe", "tell", "please", "give",
        "there", "their", "they", "them", "its", "our", "out", "all", "any", "also", "use", "used", "using",
        "mean", "means", "difference", "example", "examples", "lecture", "page", "summarise", "summarize"
    };

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();

    /// <summary>The best <paramref name="maxResults"/> chunks with at least one matching keyword, best first.</summary>
    public static List<SourceChunk> FindRelevant(string question, IEnumerable<SourceChunk> chunks, int maxResults)
    {
        var keywords = Keywords(question);
        if (keywords.Count == 0)
        {
            return new List<SourceChunk>();
        }

        return chunks
            .Select(chunk => (Chunk: chunk, Score: Score(keywords, chunk.Content)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .Take(maxResults)
            .Select(result => result.Chunk)
            .ToList();
    }

    /// <summary>
    /// Each distinct keyword found is worth 10 points, and every extra occurrence 1 point,
    /// so a chunk that mentions all the keywords beats one that repeats a single keyword.
    /// </summary>
    private static int Score(HashSet<string> keywords, string text)
    {
        var counts = new Dictionary<string, int>();
        foreach (Match match in WordPattern().Matches(text))
        {
            var stem = Stem(match.Value);
            if (keywords.Contains(stem))
            {
                counts[stem] = counts.GetValueOrDefault(stem) + 1;
            }
        }
        return counts.Count * 10 + counts.Values.Sum(count => count - 1);
    }

    private static HashSet<string> Keywords(string question) =>
        WordPattern().Matches(question)
            .Select(match => match.Value)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))
            .Select(Stem)
            .ToHashSet();

    /// <summary>A very small stemmer so that "queues", "queued" and "queue" match each other.</summary>
    private static string Stem(string word)
    {
        word = word.ToLowerInvariant();
        foreach (var suffix in new[] { "ing", "ies", "es", "ed", "s" })
        {
            if (word.Length > suffix.Length + 3 && word.EndsWith(suffix))
            {
                return word[..^suffix.Length];
            }
        }
        return word;
    }
}
