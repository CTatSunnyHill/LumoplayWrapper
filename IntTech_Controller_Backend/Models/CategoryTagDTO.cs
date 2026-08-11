using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models
{

    /** Request body for creating a category. Slug and creation time are server-assigned. */
    public class CreateCategoryDto
    {
        /** Display name of the new category. */
        public string Name { get; set; }
        /** Optional explanatory text for administrators. */
        public string? Description { get; set; }
        /** Sort position; when omitted the category is appended to the end. */
        public int? DisplayOrder { get; set; }
    }

    /** Request body for editing a category. Null members are left unchanged. */
    public class UpdateCategoryDto
    {
        /** New display name, or null to keep the current one. */
        public string? Name { get; set; }
        /** New description, or null to keep the current one. */
        public string? Description { get; set; }
        /** New sort position, or null to keep the current one. */
        public int? DisplayOrder { get; set; }
    }

    /** Request body for creating a tag inside a category. */
    public class CreateTagDto
    {
        /** String form of the owning category's ObjectId. */
        public string CategoryId { get; set; }
        /** String form of the parent tag's ObjectId, or null for a root-level tag. */
        public string? ParentTagId { get; set; }
        /** Display name of the new tag. */
        public string Name { get; set; }
        /** Sort position among siblings; when omitted the tag is appended to the end. */
        public int? DisplayOrder { get; set; }
        /** Optional "#RRGGBB" swatch used to colour the tag chip. */
        public string? ColorHex { get; set; }
        /** Whether the tag is shown to non-admin users; defaults to true. */
        public bool? IsVisible { get; set; }
    }

    /** Request body for editing a tag. Null members are left unchanged. */
    public class UpdateTagDto
    {
        /** New display name, or null to keep the current one. */
        public string? Name { get; set; }
        /** New parent tag id, or null to keep the current placement. */
        public string? ParentTagId { get; set; }
        /** New sort position, or null to keep the current one. */
        public int? DisplayOrder { get; set; }
        /** New "#RRGGBB" swatch, or null to keep the current one. */
        public string? ColorHex { get; set; }
        /** New visibility flag, or null to keep the current one. */
        public bool? IsVisible { get; set; }
    }

    // ── Response DTOs ──

    /** A category returned together with its tags already arranged as a tree. */
    public class CategoryWithTagsDto
    {
        /** String form of the category's ObjectId. */
        public string CategoryId { get; set; }
        /** Display name of the category. */
        public string CategoryName { get; set; }
        /** URL-safe form of the category name. */
        public string Slug { get; set; }
        /** Sort position among categories. */
        public int DisplayOrder { get; set; }
        /** Root-level tags of this category; nested tags hang off their children. */
        public List<TagTreeNodeDto> Tags { get; set; } = new();
    }

    /** One node in a tag tree, carrying its own children rather than a parent pointer. */
    public class TagTreeNodeDto
    {
        /** String form of the tag's ObjectId. */
        public string Id { get; set; }
        /** Display name of the tag. */
        public string Name { get; set; }
        /** URL-safe form of the tag name. */
        public string Slug { get; set; }
        /** Sort position among siblings. */
        public int DisplayOrder { get; set; }
        /** Optional "#RRGGBB" swatch used to colour the tag chip. */
        public string? ColorHex { get; set; }
        /** Whether the tag is shown to non-admin users. */
        public bool IsVisible { get; set; } = true;
        /** Tags nested directly beneath this one; empty for a leaf. */
        public List<TagTreeNodeDto> Children { get; set; } = new();
    }
}
