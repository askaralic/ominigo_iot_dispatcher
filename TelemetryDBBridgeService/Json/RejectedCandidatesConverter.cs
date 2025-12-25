using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelemetryDBBridgeService.Json;

public sealed class RejectedCandidatesConverter : JsonConverter<List<long>?>
{
    public override List<long>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<long>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<long>>(raw, options);
                return parsed ?? new List<long>();
            }
            catch (JsonException)
            {
                return new List<long>();
            }
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var value))
            {
                return new List<long> { value };
            }

            return new List<long>();
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = JsonSerializer.Deserialize<List<long>>(ref reader, options);
            return list ?? new List<long>();
        }

        throw new JsonException("Unsupported token type for rejected_candidates_json.");
    }

    public override void Write(Utf8JsonWriter writer, List<long>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}
