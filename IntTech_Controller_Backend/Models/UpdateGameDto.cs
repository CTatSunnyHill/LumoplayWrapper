namespace IntTech_Controller_Backend.Models
{
    public class UpdateGameDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageFileName { get; set; }

        public string? OnePagerFileName { get; set; }
    }
}