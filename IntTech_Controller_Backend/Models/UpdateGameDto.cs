namespace IntTech_Controller_Backend.Models
{
    /**
     * Request body for editing a game's presentation. Null members are left
     * unchanged. Platform and vendor game id are fixed at creation and cannot
     * be edited here; tags are set through their own endpoint.
     */
    public class UpdateGameDto
    {
        /** New display name, or null to keep the current one. */
        public string? Name { get; set; }
        /** New blurb, or null to keep the current one. */
        public string? Description { get; set; }
        /** File name of a newly uploaded cover image, or null to keep the current one. */
        public string? ImageFileName { get; set; }

        /** File name of a newly uploaded one-pager PDF, or null to keep the current one. */
        public string? OnePagerFileName { get; set; }
    }
}
