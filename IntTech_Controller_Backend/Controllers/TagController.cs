using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TagController : ControllerBase
    {
        private readonly IntTechDBContext _context;

        public TagController(IntTechDBContext context)
        {
            _context = context;
        }

        // GET: api/Tag
        // Returns ALL tags grouped by category with subcategory nesting.
        // This is the primary endpoint the frontend uses to build filter UI.
        [HttpGet]
        public async Task<IActionResult> GetAllTagsGrouped()
        {
            var categories = await _context.Categories.ToListAsync();
            var allTags = await _context.Tags.ToListAsync();

            var result = categories
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .Select(cat =>
                {
                    var tagsForCategory = allTags
                        .Where(t => t.CategoryId == cat.Id)
                        .ToList();

                    // Build tree: top-level tags (no parent) with children nested
                    var topLevel = tagsForCategory
                        .Where(t => t.ParentTagId == null)
                        .OrderBy(t => t.DisplayOrder)
                        .ThenBy(t => t.Name)
                        .Select(parent => BuildTagNode(parent, tagsForCategory))
                        .ToList();

                    return new CategoryWithTagsDto
                    {
                        CategoryId = cat.Id.ToString(),
                        CategoryName = cat.Name,
                        Slug = cat.Slug,
                        DisplayOrder = cat.DisplayOrder,
                        Tags = topLevel
                    };
                })
                .ToList();

            return Ok(result);
        }

        // GET: api/Tag?categoryId={id}
        // Returns tags for a single category (flat list, not grouped).
        [HttpGet("by-category/{categoryId}")]
        public async Task<IActionResult> GetTagsByCategory(string categoryId)
        {
            if (!ObjectId.TryParse(categoryId, out ObjectId oid))
                return BadRequest("Invalid category ID format.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == oid);
            if (!categoryExists)
                return NotFound(new { Message = "Category not found" });

            var tags = await _context.Tags.ToListAsync();
            var filtered = tags
                .Where(t => t.CategoryId == oid)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .ToList();

            // Build hierarchical response
            var topLevel = filtered
                .Where(t => t.ParentTagId == null)
                .Select(parent => BuildTagNode(parent, filtered))
                .ToList();

            return Ok(topLevel);
        }

        // GET: api/Tag/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTag(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid tag ID format.");

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == oid);
            if (tag == null) return NotFound(new { Message = "Tag not found" });

            return Ok(tag);
        }

        // POST: api/Tag
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTag([FromBody] CreateTagDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { Message = "Name is required" });

            if (string.IsNullOrWhiteSpace(dto.CategoryId))
                return BadRequest(new { Message = "CategoryId is required" });

            if (!ObjectId.TryParse(dto.CategoryId, out ObjectId categoryOid))
                return BadRequest("Invalid CategoryId format.");

            // Verify category exists
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryOid);
            if (!categoryExists)
                return NotFound(new { Message = "Category not found" });

            // Validate parent tag if provided
            ObjectId? parentTagOid = null;
            if (!string.IsNullOrWhiteSpace(dto.ParentTagId))
            {
                if (!ObjectId.TryParse(dto.ParentTagId, out ObjectId parsedParent))
                    return BadRequest("Invalid ParentTagId format.");

                var parentTag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == parsedParent);
                if (parentTag == null)
                    return NotFound(new { Message = "Parent tag not found" });

                // Parent must belong to the same category
                if (parentTag.CategoryId != categoryOid)
                    return BadRequest(new { Message = "Parent tag must belong to the same category" });

                // Prevent deeper than 1-level nesting: parent cannot itself have a parent
                if (parentTag.ParentTagId != null)
                    return BadRequest(new { Message = "Subcategory nesting is limited to one level. The parent tag is already a child of another tag." });

                parentTagOid = parsedParent;
            }

            var trimmedName = dto.Name.Trim();
            var slug = GenerateSlug(trimmedName);

            // Check for duplicate name within the same category + parent scope
            var allTags = await _context.Tags.ToListAsync();
            var duplicate = allTags.Any(t =>
                t.CategoryId == categoryOid &&
                t.ParentTagId == parentTagOid &&
                t.Name.ToLower() == trimmedName.ToLower());

            if (duplicate)
                return BadRequest(new { Message = "A tag with this name already exists in this scope" });

            // Auto-assign displayOrder if not provided
            int displayOrder = dto.DisplayOrder ?? 0;
            if (dto.DisplayOrder == null)
            {
                var siblings = allTags
                    .Where(t => t.CategoryId == categoryOid && t.ParentTagId == parentTagOid)
                    .ToList();
                displayOrder = siblings.Any() ? siblings.Max(t => t.DisplayOrder) + 1 : 0;
            }

            var tag = new Tag
            {
                Id = ObjectId.GenerateNewId(),
                CategoryId = categoryOid,
                ParentTagId = parentTagOid,
                Name = trimmedName,
                Slug = slug,
                DisplayOrder = displayOrder,
                ColorHex = dto.ColorHex?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return Ok(tag);
        }

        // PUT: api/Tag/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTag(string id, [FromBody] UpdateTagDto dto)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid tag ID format.");

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == oid);
            if (tag == null) return NotFound(new { Message = "Tag not found" });

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var trimmedName = dto.Name.Trim();

                // Check for duplicate within same scope
                var allTags = await _context.Tags.ToListAsync();
                var duplicate = allTags.Any(t =>
                    t.CategoryId == tag.CategoryId &&
                    t.ParentTagId == tag.ParentTagId &&
                    t.Name.ToLower() == trimmedName.ToLower() &&
                    t.Id != oid);

                if (duplicate)
                    return BadRequest(new { Message = "A tag with this name already exists in this scope" });

                tag.Name = trimmedName;
                tag.Slug = GenerateSlug(trimmedName);
            }

            // Allow re-parenting (but validate)
            if (dto.ParentTagId != null)
            {
                if (dto.ParentTagId == "")
                {
                    // Explicitly clearing parent (promoting to top-level)
                    tag.ParentTagId = null;
                }
                else
                {
                    if (!ObjectId.TryParse(dto.ParentTagId, out ObjectId newParentOid))
                        return BadRequest("Invalid ParentTagId format.");

                    // Cannot parent to self
                    if (newParentOid == oid)
                        return BadRequest(new { Message = "A tag cannot be its own parent" });

                    var newParent = await _context.Tags.FirstOrDefaultAsync(t => t.Id == newParentOid);
                    if (newParent == null)
                        return NotFound(new { Message = "New parent tag not found" });

                    if (newParent.CategoryId != tag.CategoryId)
                        return BadRequest(new { Message = "Parent tag must belong to the same category" });

                    if (newParent.ParentTagId != null)
                        return BadRequest(new { Message = "Subcategory nesting is limited to one level" });

                    // If this tag currently has children, it cannot become a child itself
                    var allTags = await _context.Tags.ToListAsync();
                    bool hasChildren = allTags.Any(t => t.ParentTagId == oid);
                    if (hasChildren)
                        return BadRequest(new { Message = "Cannot nest a tag that already has children (max 1 level deep)" });

                    tag.ParentTagId = newParentOid;
                }
            }

            if (dto.DisplayOrder.HasValue)
                tag.DisplayOrder = dto.DisplayOrder.Value;

            if (dto.ColorHex != null)
                tag.ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? null : dto.ColorHex.Trim();

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Tag updated successfully" });
        }

        // DELETE: api/Tag/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTag(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return BadRequest("Invalid tag ID format.");

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == oid);
            if (tag == null) return NotFound(new { Message = "Tag not found" });

            // Block deletion if any child tags exist
            bool hasChildren = await _context.Tags.AnyAsync(t => t.ParentTagId == oid);
            if (hasChildren)
                return BadRequest(new { Message = "Cannot delete this tag because it has child tags. Remove them first." });

            // Block deletion if any games reference this tag via tagIds
            bool inUseByGame = await _context.Games.AnyAsync(g => g.TagIds != null && g.TagIds.Contains(oid));

            if (inUseByGame)
                return BadRequest(new { Message = "Cannot delete this tag because it is assigned to one or more games. Unassign it first." });

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Tag deleted" });
        }

        // ── Helpers ──

        private static TagTreeNodeDto BuildTagNode(Tag tag, List<Tag> allTags)
        {
            var children = allTags
                .Where(t => t.ParentTagId == tag.Id)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .Select(child => new TagTreeNodeDto
                {
                    Id = child.Id.ToString(),
                    Name = child.Name,
                    Slug = child.Slug,
                    DisplayOrder = child.DisplayOrder,
                    ColorHex = child.ColorHex,
                    Children = new List<TagTreeNodeDto>() // Max 1 level, no recursion
                })
                .ToList();

            return new TagTreeNodeDto
            {
                Id = tag.Id.ToString(),
                Name = tag.Name,
                Slug = tag.Slug,
                DisplayOrder = tag.DisplayOrder,
                ColorHex = tag.ColorHex,
                Children = children
            };
        }

        private static string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant().Trim();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }
    }
}