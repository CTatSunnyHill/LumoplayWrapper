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

            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());
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
        [HttpPost("{id}")]
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