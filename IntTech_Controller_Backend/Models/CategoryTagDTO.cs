using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models
{

    public class CreateCategoryDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class CreateTagDto
    {
        public string CategoryId { get; set; }
        public string? ParentTagId { get; set; }
        public string Name { get; set; }
        public int? DisplayOrder { get; set; }
        public string? ColorHex { get; set; }
    }

    public class UpdateTagDto
    {
        public string? Name { get; set; }
        public string? ParentTagId { get; set; }
        public int? DisplayOrder { get; set; }
        public string? ColorHex { get; set; }
    }

    // ── Response DTOs ──

    public class CategoryWithTagsDto
    {
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Slug { get; set; }
        public int DisplayOrder { get; set; }
        public List<TagTreeNodeDto> Tags { get; set; } = new();
    }

    public class TagTreeNodeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public int DisplayOrder { get; set; }
        public string? ColorHex { get; set; }
        public List<TagTreeNodeDto> Children { get; set; } = new();
    }
}