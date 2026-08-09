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
                {
                    if (reader.TryGetDouble(out var d))
                        return (float)d;

                    return null;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s))
                        return null;

                    // Allow values like "1.234" or "1234" or with comma
                    if (float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f))
                        return f;

                    // Try parse with current culture just in case
                    if (float.TryParse(s, out f))
                        return f;

                    // Last resort: parse as double then cast
                    if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d2))
                        return (float)d2;

                    return null;
                }

                return null;
            }
            catch (Exception ex)
            {
                try { System.Diagnostics.Debug.WriteLine($"FloatNullableJsonConverter parse error: {ex.Message}"); } catch { }
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
