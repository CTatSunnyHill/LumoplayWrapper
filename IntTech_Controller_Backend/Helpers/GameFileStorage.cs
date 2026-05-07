using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Helpers;

public class GameFileStorage
{
    private readonly IWebHostEnvironment _env;

    public GameFileStorage(IWebHostEnvironment env) { _env = env; }

    public string? ValidateImageFile(IFormFile file)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return $"Invalid file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}";

        const long maxFileSize = 5 * 1024 * 1024;
        if (file.Length > maxFileSize)
            return "File size exceeds the 5MB limit.";

        return null;
    }

    public string BuildSanitizedFileName(string gameName, string gameId, string extension)
    {
        var sanitized = Regex.Replace(gameName.ToLower().Trim(), @"\s+", "-");
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = gameId;
        return $"{sanitized}{extension}";
    }

    public async Task<string> SaveAndReplaceAsync(
        string subfolder,
        string newFileName,
        IFormFile file,
        string? oldFileName)
    {
        var folderPath = Path.Combine(_env.WebRootPath, subfolder);
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var folderFullPath = Path.GetFullPath(folderPath);
        var filePath = Path.Combine(folderFullPath, newFileName);

        if (!string.IsNullOrEmpty(oldFileName))
        {
            var storedFileName = Path.GetFileName(oldFileName);
            if (string.Equals(storedFileName, oldFileName, StringComparison.Ordinal))
            {
                var oldFilePath = Path.GetFullPath(Path.Combine(folderFullPath, storedFileName));
                var folderPathPrefix = folderFullPath.EndsWith(Path.DirectorySeparatorChar)
                    ? folderFullPath
                    : folderFullPath + Path.DirectorySeparatorChar;

                if (oldFilePath.StartsWith(folderPathPrefix, StringComparison.Ordinal) &&
                    File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }
            }
        }

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return filePath;
    }

    public void DeleteIfExists(string subfolder, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;

        var filePath = Path.Combine(_env.WebRootPath, subfolder, fileName);
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
