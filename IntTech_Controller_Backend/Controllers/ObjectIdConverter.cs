
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

/**
 * Carries MongoDB ObjectIds across the API as plain hex strings, so clients
 * never see the driver's structured representation. Registered globally in
 * Program.cs.
 */
public class ObjectIdConverter : JsonConverter<ObjectId>
{
    /**
     * Reads an ObjectId from its string form. An absent or malformed value
     * yields ObjectId.Empty, which no document matches, rather than an error.
     *
     * <param name="reader">the JSON reader positioned on the value</param>
     * <param name="typeToConvert">the target type; always ObjectId</param>
     * <param name="options">the active serializer options</param>
     * <returns>the parsed id, or ObjectId.Empty when it could not be parsed</returns>
     */
    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return ObjectId.TryParse(value, out var objectId) ? objectId : ObjectId.Empty;
    }

    /**
     * Writes an ObjectId as its 24-character hex string.
     *
     * <param name="writer">the JSON writer to append to</param>
     * <param name="value">the id to write</param>
     * <param name="options">the active serializer options</param>
     */
    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

