using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Controllers
{
    /**
     * Manages the tags that classify games and gate who may see them. Tags nest
     * one level deep at most — a tag may have children, or a parent, never
     * both — and that invariant is enforced on every write. Any signed-in user
     * may read tags; only admins may change them.
     */
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TagController : ControllerBase
    {
        private readonly IntTechDBContext _context;

        /**
         * <param name="context">database context for tags, categories, games, and users</param>
         */
        public TagController(IntTechDBContext context)
        {
            _context = context;
        }

        /**
         * Returns ALL tags grouped by category with subcategory nesting.
         * This is the primary endpoint the frontend uses to build filter UI.
         *
         * <returns>200 with every category and its tag tree, in display order</returns>
         */
        // GET: api/Tag
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

        /**
         * Returns the tag tree for a single category.
         *
         * <param name="categoryId">string form of the category's ObjectId</param>
         * <returns>200 with that category's top-level tags and their children;
         * 400 for a malformed id; 404 when the category does not exist</returns>
         */
        // GET: api/Tag/by-category/{categoryId}
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

        /**
         * Fetches one tag as a flat record, without its children.
         *
         * <param name="id">string form of the tag's ObjectId</param>
         * <returns>200 with the tag; 400 for a malformed id; 404 when not found</returns>
         */
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

        /**
         * Creates a tag, optionally nested under a parent. Names need only be
         * unique within their own scope — the same category and parent — so two
         * categories may both have a "General" tag. When no display order is
         * given the tag is appended after its last sibling.
         *
         * <param name="dto">the name, owning category, and optional parent, colour, and visibility</param>
         * <returns>200 with the created tag; 400 when a required field is blank,
         * an id is malformed, the parent is in another category or is itself
         * nested, or the name is taken in that scope; 404 when the category or
         * parent tag does not exist</returns>
         */
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
                IsVisible = dto.IsVisible ?? true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return Ok(tag);
        }

        /**
         * Updates a tag in place; omitted fields are left alone. A tag can be
         * re-parented, including promotion back to top level by passing an empty
         * ParentTagId, but never in a way that would nest more than one level or
         * make it its own ancestor.
         *
         * <param name="id">string form of the tag's ObjectId</param>
         * <param name="dto">the fields to change</param>
         * <returns>200 on success; 400 for a malformed id, a duplicate name in
         * scope, or an illegal re-parent; 404 when the tag or new parent does
         * not exist</returns>
         */
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

            if (dto.IsVisible.HasValue)
                tag.IsVisible = dto.IsVisible.Value;

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Tag updated successfully" });
        }

        /**
         * Reorders only top-level tags (ParentTagId == null) belonging to the
         * given category. Assigns DisplayOrder = list index for each match.
         * The whole request is validated before anything is written, and any id
         * that is nested or in another category counts as missing.
         *
         * <param name="categoryId">string form of the owning category's ObjectId</param>
         * <param name="tagIds">the top-level tag ids, in the desired order</param>
         * <returns>200 with the number reordered; 400 when the list is missing
         * or holds an invalid, duplicated, or ineligible id; 404 when the
         * category does not exist</returns>
         */
        // PUT: api/Tag/category/{categoryId}/reorder
        [HttpPut("category/{categoryId}/reorder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReorderTags(string categoryId, [FromBody] List<string> tagIds)
        {
            if (tagIds == null) return BadRequest(new { Message = "Tag ID list is required." });
            if (!ObjectId.TryParse(categoryId, out ObjectId categoryOid))
                return BadRequest("Invalid category ID format.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryOid);
            if (!categoryExists) return NotFound(new { Message = "Category not found" });

            var invalidIds = new List<string>();
            var duplicateIds = new List<string>();
            var parsedIds = new List<(string Id, ObjectId Oid)>(tagIds.Count);
            var seenIds = new HashSet<ObjectId>();

            foreach (var id in tagIds)
            {
                if (!ObjectId.TryParse(id, out var oid))
                {
                    invalidIds.Add(id);
                    continue;
                }

                if (!seenIds.Add(oid))
                {
                    duplicateIds.Add(id);
                    continue;
                }

                parsedIds.Add((id, oid));
            }

            if (invalidIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more tag IDs have an invalid format",
                    InvalidIds = invalidIds
                });
            }

            if (duplicateIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more tag IDs were duplicated",
                    InvalidIds = duplicateIds
                });
            }

            // The query itself enforces eligibility, so a nested tag or one from
            // another category simply does not come back and is reported missing.
            var requestedIds = parsedIds.Select(x => x.Oid).ToList();
            var eligibleTags = await _context.Tags
                .Where(t => t.CategoryId == categoryOid
                    && t.ParentTagId == null
                    && requestedIds.Contains(t.Id))
                .ToListAsync();
            var eligibleById = eligibleTags.ToDictionary(t => t.Id);

            var missingIds = parsedIds
                .Where(x => !eligibleById.ContainsKey(x.Oid))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            if (missingIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more tags were not found",
                    MissingIds = missingIds
                });
            }

            for (int i = 0; i < requestedIds.Count; i++)
            {
                eligibleById[requestedIds[i]].DisplayOrder = i;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Tag order updated", Count = requestedIds.Count });
        }

        /**
         * Deletes a tag, refusing while it still has children or is assigned to
         * any game. Users granted the tag have it revoked and their session
         * version bumped, so their access narrows on their very next request
         * rather than whenever their token happens to expire.
         *
         * <param name="id">string form of the tag's ObjectId</param>
         * <returns>200 with the number of users affected; 400 for a malformed
         * id, a tag with children, or a tag still in use by a game; 404 when
         * not found</returns>
         */
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

            var affectedUsers = await _context.Users
                .Where(u => u.AllowedTagIds != null && u.AllowedTagIds.Contains(oid))
                .ToListAsync();

            foreach (var user in affectedUsers)
            {
                user.AllowedTagIds ??= new List<ObjectId>();
                user.AllowedTagIds.Remove(oid);
                user.SessionVersion++;
            }

            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Tag deleted",
                usersAffected = affectedUsers.Count,
            });
        }

        // ── Helpers ──

        /**
         * Turns a tag and its siblings into a tree node. Children are attached
         * one level down with empty child lists of their own, matching the
         * one-level nesting limit the write endpoints enforce.
         *
         * <param name="tag">the tag to build a node for</param>
         * <param name="allTags">the candidate tags to draw children from, usually one category's worth</param>
         * <returns>the node, with its children in display order</returns>
         */
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
                    IsVisible = child.IsVisible,
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
                IsVisible = tag.IsVisible,
                Children = children
            };
        }

        /**
         * Derives a URL-safe slug from a tag name: lowercased, punctuation
         * dropped, whitespace turned into hyphens, and runs of hyphens collapsed.
         *
         * <param name="name">the tag name to convert</param>
         * <returns>the slug form of that name</returns>
         */
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
