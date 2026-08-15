using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Infrastructure
{
    /// <summary>
    /// Resilient JSON converter for nullable float values, accepting number tokens and
    /// numeric strings in common invariant/culture formats.
    /// </summary>
    public sealed class FloatNullableJsonConverter : JsonConverter<float?>
    {
        /// <summary>
        /// Attempts to parse a nullable float from JSON and returns null on failure.
        /// </summary>
        public override float? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                if (reader.TokenType == JsonTokenType.Number)
                    return (float)reader.GetDouble();

                if (reader.TokenType != JsonTokenType.String)
                    return null;

                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;

                if (float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariant))
                    return invariant;

                if (float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var current))
                    return current;

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Writes a nullable float as JSON number, or null when absent.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, float? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }
}
