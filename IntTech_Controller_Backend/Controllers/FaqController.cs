using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    /**
     * Serves and maintains the in-app help page. Any signed-in user may read
     * the entries meant for them; only admins may write.
     */
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FaqController : ControllerBase
    {
        private readonly IntTechDBContext _context;

        /**
         * <param name="context">database context for the faqs collection</param>
         */
        public FaqController(IntTechDBContext context)
        {
            _context = context;
        }

        /**
         * Lists the FAQ entries the caller is allowed to see, in display order.
         * Admins get everything; other users get only the "all" audience.
         *
         * <returns>200 with the visible entries, ids rendered as strings</returns>
         */
        // GET: api/Faq
        [HttpGet]
        public async Task<IActionResult> GetFaqs()
        {
            var isAdmin = User.IsInRole("Admin");
            var all = await _context.Faqs.ToListAsync();

            var filtered = all
                .Where(f => isAdmin || f.Audience == "all")
                .OrderBy(f => f.DisplayOrder)
                .Select(f => new
                {
                    Id = f.Id.ToString(),
                    f.Question,
                    f.AnswerParagraphs,
                    Steps = f.Steps?.Select(s => new { s.Label, s.Detail }),
                    f.Audience,
                    f.DisplayOrder,
                });

            return Ok(filtered);
        }

        /**
         * Creates an FAQ entry and appends it to the end of the help page.
         * An entry must carry at least one paragraph or one step, so an empty
         * answer cannot be published.
         *
         * <param name="dto">the question, answer content, and audience</param>
         * <returns>200 with the created entry; 400 when the question is blank,
         * the answer is empty, or the audience is not "all" or "admin"</returns>
         */
        // POST: api/Faq
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateFaq([FromBody] CreateFaqDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Question))
                return BadRequest(new { Message = "Question is required" });

            bool hasAnswerParagraphs = dto.AnswerParagraphs != null && dto.AnswerParagraphs.Count > 0;
            bool hasSteps = dto.Steps != null && dto.Steps.Count > 0;

            if (!hasAnswerParagraphs && !hasSteps)
                return BadRequest(new { Message = "At least one answer paragraph or step is required" });

            if (dto.Audience != "all" && dto.Audience != "admin")
                return BadRequest(new { Message = "Audience must be \"all\" or \"admin\"" });

            // Auto-assign DisplayOrder = max(existing) + 1
            var allFaqs = await _context.Faqs.ToListAsync();
            int displayOrder = allFaqs.Count > 0 ? allFaqs.Max(f => f.DisplayOrder) + 1 : 0;

            var newFaq = new Faq
            {
                Id = ObjectId.GenerateNewId(),
                Question = dto.Question.Trim(),
                AnswerParagraphs = dto.AnswerParagraphs,
                Steps = dto.Steps?.Select(s => new FaqStep
                {
                    Label = s.Label,
                    Detail = s.Detail
                }).ToList(),
                Audience = dto.Audience,
                DisplayOrder = displayOrder,
            };

            _context.Faqs.Add(newFaq);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = newFaq.Id.ToString(),
                newFaq.Question,
                newFaq.AnswerParagraphs,
                Steps = newFaq.Steps?.Select(s => new { s.Label, s.Detail }),
                newFaq.Audience,
                newFaq.DisplayOrder,
            });
        }

        /**
         * Updates an FAQ entry in place; omitted fields are left alone. Supplying
         * Steps or AnswerParagraphs replaces that collection outright rather than
         * merging into it.
         *
         * <param name="id">string form of the FAQ's ObjectId</param>
         * <param name="dto">the fields to change</param>
         * <returns>200 with the updated entry; 400 for a malformed id or an
         * invalid audience; 404 when not found</returns>
         */
        // PUT: api/Faq/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateFaq(string id, [FromBody] UpdateFaqDto dto)
        {
            if (!ObjectId.TryParse(id, out var oid))
                return BadRequest(new { Message = "Invalid ID format" });

            var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == oid);
            if (faq == null)
                return NotFound(new { Message = "FAQ not found" });

            if (!string.IsNullOrWhiteSpace(dto.Question))
                faq.Question = dto.Question.Trim();

            if (dto.AnswerParagraphs != null)
                faq.AnswerParagraphs = dto.AnswerParagraphs;

            if (dto.Steps != null)
                faq.Steps = dto.Steps.Select(s => new FaqStep
                {
                    Label = s.Label,
                    Detail = s.Detail
                }).ToList();

            if (dto.Audience != null)
            {
                if (dto.Audience != "all" && dto.Audience != "admin")
                    return BadRequest(new { Message = "Audience must be \"all\" or \"admin\"" });
                faq.Audience = dto.Audience;
            }

            if (dto.DisplayOrder.HasValue)
                faq.DisplayOrder = dto.DisplayOrder.Value;


            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = faq.Id.ToString(),
                faq.Question,
                faq.AnswerParagraphs,
                Steps = faq.Steps?.Select(s => new { s.Label, s.Detail }),
                faq.Audience,
                faq.DisplayOrder,
            });
        }

        /**
         * Deletes an FAQ entry. Remaining entries keep their display orders, so
         * the sequence may contain gaps until the next reorder.
         *
         * <param name="id">string form of the FAQ's ObjectId</param>
         * <returns>200 on success; 400 for a malformed id; 404 when not found</returns>
         */
        // DELETE: api/Faq/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFaq(string id)
        {
            if (!ObjectId.TryParse(id, out var oid))
                return BadRequest(new { Message = "Invalid ID format" });

            var faq = await _context.Faqs.FirstOrDefaultAsync(f => f.Id == oid);
            if (faq == null)
                return NotFound(new { Message = "FAQ not found" });

            _context.Faqs.Remove(faq);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "FAQ deleted successfully" });
        }

        /**
         * Assigns new display orders to a set of FAQs. Unlike the category
         * reorder, positions are taken from each item rather than its index, so
         * a subset can be reordered without listing every entry. The whole
         * request is validated before anything is written.
         *
         * <param name="items">the FAQs to move and the position to give each</param>
         * <returns>200 on success; 400 when the body is missing or any id is
         * malformed or unknown</returns>
         */
        // PUT: api/Faq/reorder
        [HttpPut("reorder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReorderFaqs([FromBody] List<ReorderFaqItemDto> items)
        {
            if (items == null)
                return BadRequest(new { Message = "Request body is required" });

            var invalidIds = new List<string>();
            var parsedItems = new List<(ReorderFaqItemDto Item, ObjectId Oid)>();

            foreach (var item in items)
            {
                if (!ObjectId.TryParse(item.Id, out var oid))
                {
                    invalidIds.Add(item.Id);
                    continue;
                }

                parsedItems.Add((item, oid));
            }

            if (invalidIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more FAQ IDs have an invalid format",
                    InvalidIds = invalidIds
                });
            }

            var requestedIds = parsedItems.Select(x => x.Oid).ToList();
            var faqs = await _context.Faqs
                .Where(f => requestedIds.Contains(f.Id))
                .ToListAsync();

            var faqById = faqs.ToDictionary(f => f.Id, f => f);
            var missingIds = parsedItems
                .Where(x => !faqById.ContainsKey(x.Oid))
                .Select(x => x.Item.Id)
                .Distinct()
                .ToList();

            if (missingIds.Count > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more FAQs were not found",
                    MissingIds = missingIds
                });
            }

            foreach (var parsedItem in parsedItems)
            {
                faqById[parsedItem.Oid].DisplayOrder = parsedItem.Item.DisplayOrder;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Reorder complete" });
        }
    }
}
