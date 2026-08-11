namespace IntTech_Controller_Backend.Models
{
    /** Request body for adding a game to the library. */
    public class CreateGameDto
    {
        /** Vendor game id. Required for lumoplay, auto-generated for other platforms. */
        public string? GameId { get; set; }
        /** Display name of the game. */
        public string Name { get; set; }
        /** Optional blurb shown on the game card. */
        public string? Description { get; set; }
        /** File name of an already-uploaded cover image, or null for none. */
        public string? ImageFileName { get; set; }

        /** File name of an already-uploaded one-pager PDF, or null for none. */
        public string? OnePagerFileName { get; set; }
        /** Target platform; see <see cref="PlatformTypes"/>. Defaults to lumoplay. */
        public string? Platform { get; set; }

    }
}
