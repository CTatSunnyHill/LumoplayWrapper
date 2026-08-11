using IntTech_Controller_Backend.Models;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers
{
    /**
     * Decides which games a user is allowed to see, based on the tags granted to
     * them. The rule is per-category and strict: within every category the user
     * is restricted on, the game must carry at least one allowed tag.
     */
    public static class GameAccessHelper
    {
        /**
         * Groups a user's allowed tags by the category each tag belongs to.
         * Tags that are not in <paramref name="tagsById"/> — typically deleted
         * ones still referenced by the user — are dropped.
         *
         * <param name="allowedTagIds">tag ids granted to the user</param>
         * <param name="tagsById">every known tag, keyed by id</param>
         * <returns>allowed tag ids keyed by their category id</returns>
         */
        public static Dictionary<ObjectId, HashSet<ObjectId>> BuildAllowedTagsByCategory(
            HashSet<ObjectId> allowedTagIds,
            Dictionary<ObjectId, Tag> tagsById)
        {
            return allowedTagIds
                .Where(tagsById.ContainsKey)
                .GroupBy(tagId => tagsById[tagId].CategoryId)
                .ToDictionary(g => g.Key, g => g.ToHashSet());
        }

        /**
         * Determines whether a user may see a game, starting from a flat set of
         * allowed tag ids. Convenience overload; when checking many games,
         * group once with <see cref="BuildAllowedTagsByCategory"/> and use the
         * other overload instead.
         *
         * <param name="game">the game to test</param>
         * <param name="allowedTagIds">tag ids granted to the user</param>
         * <param name="tagsById">every known tag, keyed by id</param>
         * <returns>true when the game is visible to that user</returns>
         */
        public static bool IsGameVisibleToUser(
            Game game,
            HashSet<ObjectId> allowedTagIds,
            Dictionary<ObjectId, Tag> tagsById)
        {
            var allowedTagsByCategory = BuildAllowedTagsByCategory(allowedTagIds, tagsById);
            return IsGameVisibleToUser(game, allowedTagsByCategory, tagsById);
        }

        /**
         * Determines whether a user may see a game, given their allowed tags
         * already grouped by category. A user with no restrictions sees
         * everything; otherwise a game is hidden as soon as one restricted
         * category is unsatisfied.
         *
         * <param name="game">the game to test</param>
         * <param name="allowedTagsByCategory">allowed tag ids keyed by category id</param>
         * <param name="tagsById">every known tag, keyed by id</param>
         * <returns>true when the game is visible to that user</returns>
         */
        public static bool IsGameVisibleToUser(
            Game game,
            Dictionary<ObjectId, HashSet<ObjectId>> allowedTagsByCategory,
            Dictionary<ObjectId, Tag> tagsById)
        {
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

        /**
         * Narrows a list of games to those the user may see. Admins bypass the
         * check entirely and receive the list unchanged.
         *
         * <param name="games">the games to filter</param>
         * <param name="userRole">the user's role; "Admin" skips all filtering</param>
         * <param name="allowedTagIds">tag ids granted to the user</param>
         * <param name="tagsById">every known tag, keyed by id</param>
         * <returns>the visible subset, in the original order</returns>
         */
        public static List<Game> FilterVisibleGames(
            List<Game> games,
            string userRole,
            HashSet<ObjectId> allowedTagIds,
            Dictionary<ObjectId, Tag> tagsById)
        {
            if (userRole == "Admin") return games;
            var allowedTagsByCategory = BuildAllowedTagsByCategory(allowedTagIds, tagsById);
            return games.Where(g => IsGameVisibleToUser(g, allowedTagsByCategory, tagsById)).ToList();
        }
    }
}
