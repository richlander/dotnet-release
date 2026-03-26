using System.Text.Json;

namespace JsonSchemaInjector;

public static class JsonSchemaInjector
{
    /// <summary>
    /// Adds a $schema property to JSON content.
    /// </summary>
    /// <param name="jsonContent">JSON content as string</param>
    /// <param name="schemaUri">URI of the schema to add</param>
    /// <returns>Updated JSON content with $schema property, or null if parsing failed</returns>
    public static string? AddSchemaToContent(string jsonContent, string schemaUri)
    {
        try
        {
            // Parse as JsonDocument to preserve property order
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Use a MemoryStream and Utf8JsonWriter to build the JSON with proper ordering
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            
            writer.WriteStartObject();
            
            // Write $schema first
            writer.WriteString("$schema", schemaUri);
            
            // Copy all other properties in their original order
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "$schema") // Skip if it already exists
                {
                    property.WriteTo(writer);
                }
            }
            
            writer.WriteEndObject();
            writer.Flush();
            
            // Convert to string
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
