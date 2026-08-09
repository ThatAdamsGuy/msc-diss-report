using System.Globalization;
using System.Text.Json;
using UndercutAnalyser.Infrastructure;

namespace UndercutAnalyser.Tests;

public sealed class DateTimeNullableJsonConverterTests
{
    #region Read token handling and parse resilience
    // These tests exist to lock down accepted token forms and null-on-failure behavior,
    // preventing API data quirks from throwing at deserialization boundaries.

    [Fact]
    public void Read_NullToken_ReturnsNull()
    {
        var result = DeserializeDate("{" + "\"value\":null" + "}");

        Assert.Null(result);
    }

    [Fact]
    public void Read_IsoString_ReturnsParsedDateTime()
    {
        var result = DeserializeDate("{" + "\"value\":\"2025-01-02T03:04:05Z\"" + "}");

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), result.Value.ToUniversalTime());
    }

    [Fact]
    public void Read_CommonDateFormat_ReturnsParsedDateTime()
    {
        var result = DeserializeDate("{" + "\"value\":\"2025-01-02\"" + "}");

        Assert.NotNull(result);
    }

    [Fact]
    public void Read_InvalidString_ReturnsNull()
    {
        var result = DeserializeDate("{" + "\"value\":\"not-a-date\"" + "}");

        Assert.Null(result);
    }

    [Fact]
    public void Read_UnixEpochSeconds_ReturnsUtcDateTime()
    {
        var result = DeserializeDate("{" + "\"value\":1700000000" + "}");

        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, result.Value);
    }

    [Fact]
    public void Read_UnixEpochMilliseconds_ReturnsUtcDateTime()
    {
        var result = DeserializeDate("{" + "\"value\":1700000000000" + "}");

        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime, result.Value);
    }

    [Fact]
    public void Read_UnsupportedTokenType_ReturnsNull()
    {
        var result = DeserializeDate("{" + "\"value\":true" + "}");

        Assert.Null(result);
    }

    #endregion

    #region Write behavior
    // These tests exist to ensure serialization remains round-trippable and stable for
    // API diagnostics and persisted snapshots.

    [Fact]
    public void Write_NullValue_WritesJsonNull()
    {
        var json = SerializeDate(null);

        Assert.Equal("{\"value\":null}", json);
    }

    [Fact]
    public void Write_DateTime_WritesRoundTripIsoString()
    {
        var dt = new DateTime(2025, 6, 1, 12, 30, 45, DateTimeKind.Utc);

        var json = SerializeDate(dt);

        Assert.Equal("{\"value\":\"2025-06-01T12:30:45.0000000Z\"}", json);
    }

    #endregion

    private static DateTime? DeserializeDate(string json)
    {
        var wrapper = JsonSerializer.Deserialize<DateWrapper>(json, CreateOptions());
        return wrapper?.Value;
    }

    private static string SerializeDate(DateTime? value)
    {
        var wrapper = new DateWrapper { Value = value };
        return JsonSerializer.Serialize(wrapper, CreateOptions());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new DateTimeNullableJsonConverter());
        return options;
    }

    private sealed class DateWrapper
    {
        public DateTime? Value { get; init; }
    }
}
