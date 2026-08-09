using System.Text.Json;
using UndercutAnalyser.Infrastructure;

namespace UndercutAnalyser.Tests;

public sealed class FloatNullableJsonConverterTests
{
    #region Read token handling and parse resilience
    // These tests exist to lock down numeric parsing behavior for mixed-format API payloads,
    // ensuring invalid numeric forms degrade to null instead of throwing.

    [Fact]
    public void Read_NullToken_ReturnsNull()
    {
        var result = DeserializeFloat("{" + "\"value\":null" + "}");

        Assert.Null(result);
    }

    [Fact]
    public void Read_NumberToken_ReturnsFloat()
    {
        var result = DeserializeFloat("{" + "\"value\":95.375" + "}");

        Assert.NotNull(result);
        Assert.Equal(95.375f, result.Value, 3);
    }

    [Fact]
    public void Read_StringNumber_ReturnsFloat()
    {
        var result = DeserializeFloat("{" + "\"value\":\"101.25\"" + "}");

        Assert.NotNull(result);
        Assert.Equal(101.25f, result.Value, 3);
    }

    [Fact]
    public void Read_WhitespaceString_ReturnsNull()
    {
        var result = DeserializeFloat("{" + "\"value\":\"   \"" + "}");

        Assert.Null(result);
    }

    [Fact]
    public void Read_InvalidString_ReturnsNull()
    {
        var result = DeserializeFloat("{" + "\"value\":\"not-a-number\"" + "}");

        Assert.Null(result);
    }

    [Fact]
    public void Read_UnsupportedTokenType_ReturnsNull()
    {
        var result = DeserializeFloat("{" + "\"value\":false" + "}");

        Assert.Null(result);
    }

    #endregion

    #region Write behavior
    // These tests exist to ensure serialization preserves numeric values and nulls in a
    // compact JSON form compatible with the same converter.

    [Fact]
    public void Write_NullValue_WritesJsonNull()
    {
        var json = SerializeFloat(null);

        Assert.Equal("{\"value\":null}", json);
    }

    [Fact]
    public void Write_FloatValue_WritesJsonNumber()
    {
        var json = SerializeFloat(88.5f);

        Assert.Equal("{\"value\":88.5}", json);
    }

    #endregion

    private static float? DeserializeFloat(string json)
    {
        var wrapper = JsonSerializer.Deserialize<FloatWrapper>(json, CreateOptions());
        return wrapper?.Value;
    }

    private static string SerializeFloat(float? value)
    {
        var wrapper = new FloatWrapper { Value = value };
        return JsonSerializer.Serialize(wrapper, CreateOptions());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new FloatNullableJsonConverter());
        return options;
    }

    private sealed class FloatWrapper
    {
        public float? Value { get; init; }
    }
}
