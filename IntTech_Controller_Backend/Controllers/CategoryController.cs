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
    public class CategoryController : ControllerBase
    {
        private readonly IntTechDBContext _context;

        public CategoryController(IntTechDBContext context)
        {
            _context = context;
        }

        // GET: api/Category
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories.ToListAsync();
            var sorted = categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList();
            return Ok(sorted);
        }

        // GET: api/Category/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out var oid)) return BadRequest(new { Message = "Invalid ID format" });
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == oid);
            if (category == null) return NotFound(new { Message = "Category not found" });
            return Ok(category);

        }

        // POST: api/Category
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { Message = "Name is required" });

            var trimmedName = dto.Name.Trim();
            var slug = GenerateSlug(trimmedName);

            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower());
            if (exists) return BadRequest(new { Message = "A category with this name already exists" });

            int displayOrder = dto.DisplayOrder ?? 0;
            if (dto.DisplayOrder == null)
            {
                var allCategories = await _context.Categories.ToListAsync();
                displayOrder = allCategories.Count > 0 ? allCategories.Max(c => c.DisplayOrder) + 1 : 0;
            }

            var newCategory = new Category
            {
                Id = ObjectId.GenerateNewId(),
                Name = trimmedName,
                Slug = slug,
                Description = dto.Description?.Trim(),
                DisplayOrder = displayOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();
            return Ok(newCategory);
        }

        // PUT: api/Category/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(string id, [FromBody] UpdateCategoryDto dto)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out var oid)) return BadRequest(new { Message = "Invalid ID format" });

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == oid);
            if (category == null) return NotFound(new { Message = "Category not found" });

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var trimmedName = dto.Name.Trim();
                var slug = GenerateSlug(trimmedName);
                var exists = await _context.Categories.AnyAsync(c => c.Id != oid && c.Name.ToLower() == trimmedName.ToLower());
                if (exists) return BadRequest(new { Message = "A category with this name already exists" });
                category.Name = trimmedName;
                category.Slug = slug;
            }

            if (dto.Description != null)
            {
                category.Description = dto.Description.Trim();
            }

            if (dto.DisplayOrder.HasValue)
            {
                category.DisplayOrder = dto.DisplayOrder.Value;
            }
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        // PUT: api/Category/reorder
        // Body: ["categoryId1", "categoryId2", ...] in the new desired order.
        // Assigns DisplayOrder = list index for each requested category after validating the request.
        [HttpPut("reorder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReorderCategories([FromBody] List<string> categoryIds)
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return BadRequest(new { Message = "Category ID list is required." });

            var orderedOids = new List<ObjectId>(categoryIds.Count);
            var invalidIds = new List<string>();
            var duplicateIds = new List<string>();
            var seenOids = new HashSet<ObjectId>();

            foreach (var id in categoryIds)
            {
                if (!ObjectId.TryParse(id, out var oid))
                {
                    invalidIds.Add(id);
                    continue;
                }

                if (!seenOids.Add(oid))
                {
                    duplicateIds.Add(id);
                    continue;
                }

                orderedOids.Add(oid);
            }

            if (invalidIds.Count > 0 || duplicateIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more category IDs are invalid or duplicated.",
                    InvalidIds = invalidIds,
                    DuplicateIds = duplicateIds
                });
            }

            var categories = await _context.Categories
                .Where(c => orderedOids.Contains(c.Id))
                .ToListAsync();

            var byId = categories.ToDictionary(c => c.Id);
            var missingIds = new List<string>();

            foreach (var oid in orderedOids)
            {
                if (!byId.ContainsKey(oid))
                {
                    missingIds.Add(oid.ToString());
                }
            }

            if (missingIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more category IDs were not found.",
                    MissingIds = missingIds
                });
            }

            for (int i = 0; i < orderedOids.Count; i++)
            {
                byId[orderedOids[i]].DisplayOrder = i;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Category order updated", Count = categories.Count });
        }

        // DELETE: api/Category/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out var oid)) return BadRequest(new { Message = "Invalid ID format" });

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == oid);
            if (category == null) return NotFound(new { Message = "Category not found" });

            var hasTags = await _context.Tags.AnyAsync(t => t.CategoryId == oid);
            if (hasTags) return BadRequest(new { Message = "Cannot delete category with associated tags. Please delete the tags first." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Category deleted successfully" });
        }

        private static string GenerateSlug(string name)
        {
            var slug = name.ToLower();
            slug = Regex.Replace(slug, @"\s+", "-0");
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }

    }
}