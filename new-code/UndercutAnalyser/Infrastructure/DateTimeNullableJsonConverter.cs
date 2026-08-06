using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Infrastructure
{
    public sealed class DateTimeNullableJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s))
                        return null;

                    // Try ISO parse
                    if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                        return dt;

                    // Try common formats
                    var formats = new[] {"yyyy-MM-dd'T'HH:mm:ss.fffK","yyyy-MM-dd'T'HH:mm:ssK","yyyy-MM-dd HH:mm:ss","yyyy-MM-dd"};
                    if (DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out dt))
                        return dt;

                    // Last resort: try parse without styles
                    if (DateTime.TryParse(s, out dt))
                        return dt;

                    // Could not parse — return null rather than throwing
                    return null;
                }

                if (reader.TokenType == JsonTokenType.Number)
                {
                    // Unix epoch seconds or milliseconds
                    if (reader.TryGetInt64(out var l))
                    {
                        // Heuristic: if larger than 1e12 treat as milliseconds
                        if (l > 1_000_000_000_000L)
                            return DateTimeOffset.FromUnixTimeMilliseconds(l).UtcDateTime;

                        return DateTimeOffset.FromUnixTimeSeconds(l).UtcDateTime;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                DebuggerLog(ex);
                return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("o"));
            else
                writer.WriteNullValue();
        }

        private static void DebuggerLog(Exception ex)
        {
            try
            {
                var msg = $"DateTimeNullableJsonConverter parse error: {ex.GetType()}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Log(0, "UndercutAnalyser", msg + "\n");
            }
            catch { }
        }
    }
}
