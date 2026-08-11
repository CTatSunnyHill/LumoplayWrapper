namespace IntTech_Controller_Backend.Models
{
    /** Request body that replaces a game's tags outright with the ids given. */
    public class SetGameTagsToDto
    {
        /** String forms of the tag ObjectIds to apply; empty clears all tags. */
        public List<string> TagIds { get; set; } = new();
    }
}
