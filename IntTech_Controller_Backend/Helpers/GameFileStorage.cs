using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Helpers;

/**
 * Stores game artwork and one-pagers under the web root. Every path is resolved
 * and checked against its intended folder before any write or delete, so a
 * crafted file name cannot reach outside that folder.
 */
public class GameFileStorage
{
    private readonly IWebHostEnvironment _env;

    /**
     * <param name="env">hosting environment, used to locate the web root</param>
     */
    public GameFileStorage(IWebHostEnvironment env) { _env = env; }

    /**
     * Checks an uploaded file against the allowed extensions and the 20MB limit.
     *
     * <param name="file">the uploaded file to check</param>
     * <returns>an error message to show the user, or null when the file is acceptable</returns>
     */
    public string? ValidateImageFile(IFormFile file)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return $"Invalid file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}";

        const long maxFileSize = 20 * 1024 * 1024;
        if (file.Length > maxFileSize)
            return "File size exceeds the 20MB limit.";

        return null;
    }

    /**
     * Builds a storage file name from a game's name: lowercased, spaces turned
     * into hyphens, and anything outside [a-z0-9-] dropped. Falls back to the
     * game id when that leaves nothing usable.
     *
     * <param name="gameName">display name of the game</param>
     * <param name="gameId">game id to fall back on</param>
     * <param name="extension">file extension to append, including the dot</param>
     * <returns>a file name safe to use as a single path segment</returns>
     */
    public string BuildSanitizedFileName(string gameName, string gameId, string extension)
    {
        var sanitized = Regex.Replace(gameName.ToLower().Trim(), @"\s+", "-");
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = gameId;
        return $"{sanitized}{extension}";
    }

    /**
     * Writes an upload into a web-root subfolder, creating the folder if needed,
     * and removes the file it replaces. The old file is deleted only when its
     * name is a bare path segment that resolves inside the same folder.
     *
     * <param name="subfolder">folder beneath the web root to write into</param>
     * <param name="newFileName">file name to write, as produced by <see cref="BuildSanitizedFileName"/></param>
     * <param name="file">the uploaded file to copy</param>
     * <param name="oldFileName">file being replaced, or null when there is none</param>
     * <returns>the full path the file was written to</returns>
     */
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

    /**
     * Deletes a stored file if it is present. Does nothing when the name is
     * empty, contains path separators, or resolves outside the subfolder.
     *
     * <param name="subfolder">folder beneath the web root to look in</param>
     * <param name="fileName">file name to delete, or null to do nothing</param>
     */
    public void DeleteIfExists(string subfolder, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;

        var folderPath = Path.Combine(_env.WebRootPath, subfolder);
        var folderFullPath = Path.GetFullPath(folderPath);
        var storedFileName = Path.GetFileName(fileName);

        if (string.Equals(storedFileName, fileName, StringComparison.Ordinal))
        {
            var filePath = Path.GetFullPath(Path.Combine(folderFullPath, storedFileName));
            var folderPathPrefix = folderFullPath.EndsWith(Path.DirectorySeparatorChar)
                ? folderFullPath
                : folderFullPath + Path.DirectorySeparatorChar;

            if (filePath.StartsWith(folderPathPrefix, StringComparison.Ordinal) &&
                File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
