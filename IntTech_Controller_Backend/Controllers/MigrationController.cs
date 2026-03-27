using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Controllers
{
    /// <summary>
    /// One-time migration controller for Phase 2.
    /// Converts the legacy locationType string array on games into tagIds ObjectId references.
    /// 
    /// HOW IT WORKS:
    /// 1. Ensures a "Location Type" category exists (creates it if missing).
    /// 2. For each unique locationType string across all games, ensures a matching tag exists.
    /// 3. For each game, resolves its locationType strings to tag ObjectIds and writes them to tagIds.
    /// 4. Returns a detailed report of what was created/mapped.
    ///
    /// SAFE TO RUN MULTIPLE TIMES — it skips games that already have tagIds populated
    /// and doesn't create duplicate categories or tags.
    ///
    /// DELETE THIS CONTROLLER after migration is verified in production (Phase 7 cleanup).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly IntTechDBContext _context;

        public MigrationController(IntTechDBContext context)
        {
            _context = context;
        }

        // POST: api/Migration/location-type-to-tags
        [HttpPost("location-type-to-tags")]
        public async Task<IActionResult> MigrateLocationTypeToTags()
        {
            var report = new MigrationReport();

            // ── Step 1: Ensure "Location Type" category exists ──
            var allCategories = await _context.Categories.ToListAsync();
            var locationCategory = allCategories.FirstOrDefault(c =>
                c.Slug == "location-type" || c.Name.ToLower() == "location type");

            if (locationCategory == null)
            {
                locationCategory = new Category
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Location Type",
                    Slug = "location-type",
                    Description = "Physical setup type for the game (auto-migrated from legacy locationType field)",
                    DisplayOrder = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Categories.Add(locationCategory);
                await _context.SaveChangesAsync();
                report.CategoryCreated = true;
                report.CategoryId = locationCategory.Id.ToString();
            }
            else
            {
                report.CategoryCreated = false;
                report.CategoryId = locationCategory.Id.ToString();
            }

            // ── Step 2: Collect all unique locationType strings across all games ──
            var allGames = await _context.Games.ToListAsync();

            var uniqueTypeStrings = allGames
                .Where(g => g.LocationType != null)
                .SelectMany(g => g.LocationType)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            report.UniqueLocationTypes = uniqueTypeStrings;

            // ── Step 3: Ensure a tag exists for each unique string ──
            var allTags = await _context.Tags.ToListAsync();
            var tagsForCategory = allTags
                .Where(t => t.CategoryId == locationCategory.Id && t.ParentTagId == null)
                .ToList();

            // Build a case-insensitive lookup: lowercase name -> Tag
            var tagByName = tagsForCategory
                .ToDictionary(t => t.Name.ToLower(), t => t);

            // Hardcoded colors matching your existing UI
            var colorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Wall", "#1E40AF" },
                { "Floor", "#166534" },
                { "Wall-Ball", "#9A3412" }
            };

            foreach (var typeStr in uniqueTypeStrings)
            {
                if (tagByName.ContainsKey(typeStr.ToLower()))
                {
                    report.TagsSkipped.Add(typeStr);
                    continue;
                }

                var slug = GenerateSlug(typeStr);
                colorMap.TryGetValue(typeStr, out var color);

                var newTag = new Tag
                {
                    Id = ObjectId.GenerateNewId(),
                    CategoryId = locationCategory.Id,
                    ParentTagId = null,
                    Name = typeStr,
                    Slug = slug,
                    DisplayOrder = tagByName.Count,
                    ColorHex = color,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tags.Add(newTag);
                tagByName[typeStr.ToLower()] = newTag;
                report.TagsCreated.Add(typeStr);
            }

            await _context.SaveChangesAsync();

            // ── Step 4: For each game, resolve locationType strings to tagIds ──
            int gamesUpdated = 0;
            int gamesSkipped = 0;
            var unmatchedEntries = new List<string>();

            foreach (var game in allGames)
            {
                // Skip games that already have tagIds populated (idempotent)
                if (game.TagIds != null && game.TagIds.Any())
                {
                    gamesSkipped++;
                    continue;
                }

                if (game.LocationType == null || !game.LocationType.Any())
                {
                    gamesSkipped++;
                    continue;
                }

                var resolvedIds = new List<ObjectId>();

                foreach (var typeStr in game.LocationType)
                {
                    var key = typeStr.Trim().ToLower();
                    if (tagByName.TryGetValue(key, out var matchedTag))
                    {
                        if (!resolvedIds.Contains(matchedTag.Id))
                            resolvedIds.Add(matchedTag.Id);
                    }
                    else
                    {
                        unmatchedEntries.Add($"Game '{game.Name}' (gameId: {game.GameId}): unmatched locationType '{typeStr}'");
                    }
                }

                game.TagIds = resolvedIds;
                gamesUpdated++;
            }

            await _context.SaveChangesAsync();

            report.GamesUpdated = gamesUpdated;
            report.GamesSkipped = gamesSkipped;
            report.UnmatchedEntries = unmatchedEntries;

            return Ok(report);
        }

        // GET: api/Migration/preview
        // Dry-run that shows what the migration WOULD do without changing anything.
        [HttpGet("preview")]
        public async Task<IActionResult> PreviewMigration()
        {
            var allGames = await _context.Games.ToListAsync();

            var uniqueTypes = allGames
                .Where(g => g.LocationType != null)
                .SelectMany(g => g.LocationType)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var gamesWithLocationType = allGames.Count(g => g.LocationType != null && g.LocationType.Any());
            var gamesAlreadyMigrated = allGames.Count(g => g.TagIds != null && g.TagIds.Any());
            var gamesNeedingMigration = allGames.Count(g =>
                (g.LocationType != null && g.LocationType.Any()) &&
                (g.TagIds == null || !g.TagIds.Any()));

            return Ok(new
            {
                TotalGames = allGames.Count,
                GamesWithLocationType = gamesWithLocationType,
                GamesAlreadyMigrated = gamesAlreadyMigrated,
                GamesNeedingMigration = gamesNeedingMigration,
                UniqueLocationTypeStrings = uniqueTypes,
                Message = gamesNeedingMigration > 0
                    ? $"Migration will create tags for {uniqueTypes.Count} location type(s) and update {gamesNeedingMigration} game(s)."
                    : "All games are already migrated. Nothing to do."
            });
        }

        private static string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant().Trim();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }
    }

    // ── Report DTO ──
    public class MigrationReport
    {
        public bool CategoryCreated { get; set; }
        public string CategoryId { get; set; }
        public List<string> UniqueLocationTypes { get; set; } = new();
        public List<string> TagsCreated { get; set; } = new();
        public List<string> TagsSkipped { get; set; } = new();
        public int GamesUpdated { get; set; }
        public int GamesSkipped { get; set; }
        public List<string> UnmatchedEntries { get; set; } = new();
    }
}