using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * One help-page entry, stored in the "faqs" collection. An entry is prose
     * (<see cref="AnswerParagraphs"/>), an optional numbered walkthrough
     * (<see cref="Steps"/>), or both.
     */
    [Collection("faqs")]
    public class Faq
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** The question as shown in the help page heading. */
        [BsonElement("question")]
        public string Question { get; set; }

        /** Body paragraphs, rendered in order; empty when the answer is steps only. */
        [BsonElement("answerParagraphs")]
        public List<string> AnswerParagraphs { get; set; } = new();

        /** Ordered walkthrough steps, or null when the answer is prose only. */
        [BsonElement("steps")]
        public List<FaqStep>? Steps { get; set; }

        /** Who may see this entry: "all" or "admin". */
        [BsonElement("audience")]
        public string Audience { get; set; } = "all";

        /** Sort position on the help page; lower values are listed first. */
        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    /** A single step in an FAQ walkthrough. */
    public class FaqStep
    {
        /** Short instruction shown as the step title. */
        [BsonElement("label")]
        public string Label { get; set; }

        /** Optional elaboration shown beneath the label. */
        [BsonElement("detail")]
        public string? Detail { get; set; }
    }

    // ── DTOs ─────────────────────────────────────────────────────────
    /** Request body for creating an FAQ entry. Display order is server-assigned. */
    public class CreateFaqDto
    {
        /** The question heading. */
        public string Question { get; set; }
        /** Body paragraphs, in render order. */
        public List<string> AnswerParagraphs { get; set; }
        /** Optional ordered walkthrough steps. */
        public List<FaqStepDto>? Steps { get; set; }
        /** Who may see this entry: "all" or "admin". */
        public string Audience { get; set; }
    }

    /** Request body for editing an FAQ entry. Null members are left unchanged. */
    public class UpdateFaqDto
    {
        /** New question heading, or null to keep the current one. */
        public string? Question { get; set; }
        /** Replacement body paragraphs, or null to keep the current ones. */
        public List<string>? AnswerParagraphs { get; set; }
        /** Replacement walkthrough steps, or null to keep the current ones. */
        public List<FaqStepDto>? Steps { get; set; }
        /** New audience, or null to keep the current one. */
        public string? Audience { get; set; }
        /** New sort position, or null to keep the current one. */
        public int? DisplayOrder { get; set; }
    }

    /** Transport form of a single walkthrough step. */
    public class FaqStepDto
    {
        /** Short instruction shown as the step title. */
        public string Label { get; set; }
        /** Optional elaboration shown beneath the label. */
        public string? Detail { get; set; }
    }

    /** One entry in a bulk reorder request: an FAQ and its new sort position. */
    public class ReorderFaqItemDto
    {
        /** String form of the FAQ's ObjectId. */
        public string Id { get; set; }
        /** Sort position to assign to that FAQ. */
        public int DisplayOrder { get; set; }
    }
}
