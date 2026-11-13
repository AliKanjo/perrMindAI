namespace PeerMindAI.Services;

/// <summary>
/// Stores uploaded transcripts in a private folder outside wwwroot, so they can
/// only be downloaded through a controller action that checks the user's role (NFR-07).
/// </summary>
public class TranscriptStorage
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private readonly string _rootFolder;
    private readonly long _maxBytes;

    public TranscriptStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var folder = configuration["Uploads:Folder"] ?? "App_Data/uploads";
        _rootFolder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, folder));
        MaxSizeMB = configuration.GetValue("Uploads:MaxTranscriptSizeMB", 5);
        _maxBytes = MaxSizeMB * 1024L * 1024L;
    }

    public int MaxSizeMB { get; }

    /// <summary>Returns an error message for the user, or null when the file is acceptable.</summary>
    public async Task<string?> ValidateAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "The uploaded file is empty.";
        }
        if (file.Length > _maxBytes)
        {
            return $"The transcript is too large. The maximum size is {MaxSizeMB} MB.";
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "The transcript must be a PDF file.";
        }

        // the first bytes of every PDF are "%PDF" - this catches renamed files
        var header = new byte[PdfSignature.Length];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        if (read < PdfSignature.Length || !header.SequenceEqual(PdfSignature))
        {
            return "The file does not look like a real PDF document.";
        }
        return null;
    }

    /// <summary>Saves the file under a new random name and returns its path relative to the upload folder.</summary>
    public async Task<string> SaveAsync(int applicationId, IFormFile file)
    {
        var relativePath = Path.Combine("applications", applicationId.ToString(), Guid.NewGuid().ToString("N") + ".pdf");
        var fullPath = Path.Combine(_rootFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await file.CopyToAsync(target);
        return relativePath.Replace('\\', '/');
    }

    /// <summary>Full path of a stored file, or null if it does not exist (or points outside the upload folder).</summary>
    public string? FindFile(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootFolder, relativePath));
        var insideRoot = fullPath.StartsWith(_rootFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return insideRoot && File.Exists(fullPath) ? fullPath : null;
    }
}
