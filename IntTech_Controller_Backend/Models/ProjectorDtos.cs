namespace IntTech_Controller_Backend.Models
{
    // Used by POST (create) and PUT (edit). All fields the admin can set.
    public class ProjectorUpsertDto
    {
public string Name { get; set; } = string.Empty;
public string IpAddress { get; set; } = string.Empty;
public int Port { get; set; } = 4352;
public string? Password { get; set; }
public string LocationId { get; set; } = string.Empty;   // string form of ObjectId
    }

    // Used by the label-save endpoint. One entry per input the admin is labelling.
    public class ProjectorInputLabelDto
    {
        public string Code { get; set; }
        public string? Label { get; set; }
    }
}
