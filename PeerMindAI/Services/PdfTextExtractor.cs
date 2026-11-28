using System.Text;
using UglyToad.PdfPig;

namespace PeerMindAI.Services;

/// <summary>
/// Reads the text of a PDF page by page (PdfPig library) and cuts it into
/// pieces of about 1,200 characters, each remembering its page number. The
/// assistant later searches these pieces and cites their pages (FR-09).
/// </summary>
public class PdfTextExtractor
{
    private const int ChunkSize = 1200;

    public record Chunk(int PageNumber, string Text);

    public record Result(int PageCount, List<Chunk> Chunks);

    /// <summary>Extracts the text. Throws InvalidDataException with a user-friendly message on failure.</summary>
    public Result Extract(Stream pdf)
    {
        PdfDocument document;
        try
        {
            document = PdfDocument.Open(pdf);
        }
        catch (Exception)
        {
            throw new InvalidDataException("The PDF could not be opened. It may be damaged or password-protected.");
        }

        using (document)
        {
            var chunks = new List<Chunk>();
            foreach (var page in document.GetPages())
            {
                // words joined by single spaces give clean text for searching
                var text = string.Join(" ", page.GetWords().Select(word => word.Text));
                chunks.AddRange(Split(text).Select(piece => new Chunk(page.Number, piece)));
            }

            if (chunks.Count == 0)
            {
                throw new InvalidDataException(
                    "No text was found in this PDF. Scanned documents (images of pages) are not supported.");
            }
            return new Result(document.NumberOfPages, chunks);
        }
    }

    /// <summary>Splits long page text at spaces so that no piece is much longer than ChunkSize.</summary>
    private static IEnumerable<string> Split(string text)
    {
        text = text.Trim();
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length + word.Length + 1 > ChunkSize && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
