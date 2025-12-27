using System.Text.Json;
using System.Text.Json.Serialization;

namespace DispatcherService.Json;

/// <summary>
/// Converts Unix epoch seconds to/from DateTimeOffset for API payloads that use numeric timestamps.
/// </summary>
public class UnixSecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out var seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds),
            JsonTokenType.String when DateTimeOffset.TryParse(reader.GetString(), out var parsed) => parsed,
            _ => throw new JsonException($"Unable to convert token of type {reader.TokenType} to DateTimeOffset")
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
    }
}

public class NullableUnixSecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt64(out var seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds),
            JsonTokenType.String when DateTimeOffset.TryParse(reader.GetString(), out var parsed) => parsed,
            _ => throw new JsonException($"Unable to convert token of type {reader.TokenType} to nullable DateTimeOffset")
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value.ToUnixTimeSeconds());
            return;
        }

        writer.WriteNullValue();
    }
}
