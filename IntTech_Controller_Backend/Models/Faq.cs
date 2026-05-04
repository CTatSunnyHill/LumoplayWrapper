using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("faqs")]
    public class Faq
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("question")]
        public string Question { get; set; }

        [BsonElement("answerParagraphs")]
        public List<string> AnswerParagraphs { get; set; } = new();

        [BsonElement("steps")]
        public List<FaqStep>? Steps { get; set; }

        // "all" | "admin"
        [BsonElement("audience")]
        public string Audience { get; set; } = "all";

        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    public class FaqStep
    {
        [BsonElement("label")]
        public string Label { get; set; }

        [BsonElement("detail")]
        public string? Detail { get; set; }
    }

    // ── DTOs ─────────────────────────────────────────────────────────
    public class CreateFaqDto
    {
        public string Question { get; set; }
        public List<string> AnswerParagraphs { get; set; }
        public List<FaqStepDto>? Steps { get; set; }
        public string Audience { get; set; }   // "all" | "admin"
    }

    public class UpdateFaqDto
    {
        public string? Question { get; set; }
        public List<string>? AnswerParagraphs { get; set; }
        public List<FaqStepDto>? Steps { get; set; }
        public string? Audience { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class FaqStepDto
    {
        public string Label { get; set; }
        public string? Detail { get; set; }
    }

    public class ReorderFaqItemDto
    {
        public string Id { get; set; }
        public int DisplayOrder { get; set; }
    }
}
