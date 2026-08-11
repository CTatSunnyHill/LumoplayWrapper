namespace IntTech_Controller_Backend.Models
{
    /**
     * Request body for creating (POST) or editing (PUT) a projector: every field
     * an admin is allowed to set. Status and input data are discovered by polling,
     * not supplied here.
     */
    public class ProjectorUpsertDto
    {
        /** Human-readable projector name shown in the UI. */
        public string Name { get; set; } = string.Empty;
        /** Host or IP address of the projector on the local network. */
        public string IpAddress { get; set; } = string.Empty;
        /** PJLink TCP port; 4352 is the protocol default. */
        public int Port { get; set; } = 4352;
        /** PJLink password, or null when the projector needs no authentication. */
        public string? Password { get; set; }
        /** String form of the owning location's ObjectId. */
        public string LocationId { get; set; } = string.Empty;
    }

    /** One input the admin is naming, sent to the label-save endpoint. */
    public class ProjectorInputLabelDto
    {
        /** Two-character PJLink input code the label applies to. */
        public string Code { get; set; }
        /** Name to show for that input, or null to clear it. */
        public string? Label { get; set; }
    }
}
