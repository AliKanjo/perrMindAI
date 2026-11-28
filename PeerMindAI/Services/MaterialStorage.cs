namespace PeerMindAI.Services;

/// <summary>
/// Stores uploaded course materials in a private folder outside wwwroot, so a
/// file can never be downloaded by guessing its address (NFR-07).
/// </summary>
public class MaterialStorage
{
    private readonly string _rootFolder;

    public MaterialStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var folder = configuration["Uploads:Folder"] ?? "App_Data/uploads";
        _rootFolder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, folder));
        MaxSizeMB = configuration.GetValue("Uploads:MaxMaterialSizeMB", 20);
    }

    public int MaxSizeMB { get; }

    /// <summary>Returns an error message for the user, or null when the file is acceptable.</summary>
    public async Task<string?> ValidateAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "The uploaded file is empty.";
        }
        if (file.Length > MaxSizeMB * 1024L * 1024L)
        {
            return $"The file is too large. The maximum size is {MaxSizeMB} MB.";
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "Only PDF files can be uploaded.";
        }
        await using var stream = file.OpenReadStream();
        return await PdfSignature.IsPdfAsync(stream) ? null : "The file does not look like a real PDF document.";
    }

    /// <summary>Saves the file under a new random name and returns its path relative to the upload folder.</summary>
    public async Task<string> SaveAsync(int studentId, IFormFile file)
    {
        var relativePath = Path.Combine("materials", studentId.ToString(), Guid.NewGuid().ToString("N") + ".pdf");
        var fullPath = Path.Combine(_rootFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await file.CopyToAsync(target);
        return relativePath.Replace('\\', '/');
    }

    /// <summary>Removes a stored file if it exists (seeded materials have no file on disk).</summary>
    public void Delete(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootFolder, relativePath));
        if (fullPath.StartsWith(_rootFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}

/// <summary>Every PDF file starts with the bytes "%PDF"; this catches renamed files.</summary>
public static class PdfSignature
{
    private static readonly byte[] Signature = "%PDF"u8.ToArray();

    public static async Task<bool> IsPdfAsync(Stream stream)
    {
        var header = new byte[Signature.Length];
        var read = await stream.ReadAsync(header);
        return read == Signature.Length && header.SequenceEqual(Signature);
    }
}
