using IntTech_Controller_Backend.Models;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers
{
    public static class GameAccessHelper
    {
        public static bool IsGameVisibleToUser(
            Game game,
            HashSet<ObjectId> allowedTagIds,
            Dictionary<ObjectId, Tag> tagsById)
        {
            var allowedTagsByCategory = allowedTagIds
                .Where(tagsById.ContainsKey)
                .GroupBy(tagId => tagsById[tagId].CategoryId)
                .ToDictionary(g => g.Key, g => g.ToHashSet());

            // No restrictions at all → see everything
            if (allowedTagsByCategory.Count == 0) return true;

            var gameTagsByCategory = (game.TagIds ?? new List<ObjectId>())
                .Where(tagsById.ContainsKey)
                .GroupBy(tagId => tagsById[tagId].CategoryId)
                .ToDictionary(g => g.Key, g => g.ToHashSet());

            // For every category the user restricts on, the game must have
            // at least one tag in that category that's in the allowed set.
            foreach (var (categoryId, allowedInCategory) in allowedTagsByCategory)
            {
                if (!gameTagsByCategory.TryGetValue(categoryId, out var gameTagsInCategory))
                    return false; // Strict: game has no tag in a restricted category → hidden

                if (!gameTagsInCategory.Overlaps(allowedInCategory))
                    return false; // Game has tags in this category but none are allowed
            }

            return true;
        }

        public static List<Game> FilterVisibleGames(
            List<Game> games,
            string userRole,
            HashSet<ObjectId> allowedTagIds,
            Dictionary<ObjectId, Tag> tagsById)
        {
            if (userRole == "Admin") return games;
            return games.Where(g => IsGameVisibleToUser(g, allowedTagIds, tagsById)).ToList();
        }
    }
}
