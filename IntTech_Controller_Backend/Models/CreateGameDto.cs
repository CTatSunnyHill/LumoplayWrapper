namespace IntTech_Controller_Backend.Models
{
    public class CreateGameDto
    {
        public string? GameId { get; set; }        // Required for lumoplay, auto-generated for others
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? ImageFileName { get; set; }

        public string? OnePagerFileName { get; set; }
        public string? Platform { get; set; }

    }
}