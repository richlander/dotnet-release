using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Cve;

public class CnaJsonConverter : JsonConverter<Cna>
{
    public override Cna? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            return new Cna(name ?? "");
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? name = null;
            string? severity = null;
            string? impact = null;
            List<string>? acknowledgments = null;
            List<CnaFaq>? faq = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "name":
                            name = reader.GetString();
                            break;
                        case "severity":
                            severity = reader.GetString();
                            break;
                        case "impact":
                            impact = reader.GetString();
                            break;
                        case "acknowledgments":
                            acknowledgments = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                            break;
                        case "faq":
                            faq = JsonSerializer.Deserialize<List<CnaFaq>>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return new Cna(name ?? "", severity, impact, acknowledgments, faq);
        }

        throw new JsonException("Invalid CNA format");
    }

    public override void Write(Utf8JsonWriter writer, Cna value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (value.Severity != null)
            writer.WriteString("severity", value.Severity);
        if (value.Impact != null)
            writer.WriteString("impact", value.Impact);
        if (value.Acknowledgments != null && value.Acknowledgments.Count > 0)
        {
            writer.WritePropertyName("acknowledgments");
            JsonSerializer.Serialize(writer, value.Acknowledgments, options);
        }
        if (value.Faq != null && value.Faq.Count > 0)
        {
            writer.WritePropertyName("faq");
            JsonSerializer.Serialize(writer, value.Faq, options);
        }
        writer.WriteEndObject();
    }
}
