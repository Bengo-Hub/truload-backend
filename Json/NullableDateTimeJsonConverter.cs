using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TruLoad.Backend.Json;

/// <summary>
/// Allows nullable DateTime properties to accept null, empty string, or whitespace as null.
/// Avoids 400 when the client sends "dateOfBirth": "" for optional date fields.
/// </summary>
public sealed class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s))
                return null;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            // Allow Unix timestamp etc. if needed
            if (reader.TryGetDateTime(out var dt))
                return dt;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("O", CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}
