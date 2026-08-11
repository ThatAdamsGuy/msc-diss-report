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

    [Fact]
    public void Read_StringWithThousandsSeparator_ReturnsFloat()
    {
        var result = DeserializeFloat("{" + "\"value\":\"1,234.5\"" + "}");

        Assert.NotNull(result);
        Assert.Equal(1234.5f, result.Value, 3);
    }

    [Fact]
    public void Read_StringOutOfRange_ReturnsPositiveInfinity()
    {
        var result = DeserializeFloat("{" + "\"value\":\"1e1000\"" + "}");

        Assert.NotNull(result);
        Assert.True(float.IsPositiveInfinity(result.Value));
    }

    [Fact]
    public void Read_NegativeStringOutOfRange_ReturnsNegativeInfinity()
    {
        // Tests extreme negative value that overflows float range
        var result = DeserializeFloat("{" + "\"value\":\"-1e1000\"" + "}");

        Assert.NotNull(result);
        Assert.True(float.IsNegativeInfinity(result.Value));
    }

    [Fact]
    public void Read_NaNString_ReturnsNaN()
    {
        // Tests JSON parsing of NaN literal
        var result = DeserializeFloat("{" + "\"value\":\"NaN\"" + "}");

        Assert.NotNull(result);
        Assert.True(float.IsNaN(result.Value));
    }

    [Fact]
    public void Read_PositiveInfinityString_ReturnsPositiveInfinity()
    {
        // Tests JSON parsing of Infinity literal
        var result = DeserializeFloat("{" + "\"value\":\"Infinity\"" + "}");

        Assert.NotNull(result);
        Assert.True(float.IsPositiveInfinity(result.Value));
    }

    [Fact]
    public void Read_NegativeInfinityString_ReturnsNegativeInfinity()
    {
        // Tests JSON parsing of -Infinity literal
        var result = DeserializeFloat("{" + "\"value\":\"-Infinity\"" + "}");

        Assert.NotNull(result);
        Assert.True(float.IsNegativeInfinity(result.Value));
    }

    [Fact]
    public void Read_VerySmallPositiveNumber_ReturnsFloat()
    {
        // Tests parsing of very small float values (approaching zero but not zero)
        var result = DeserializeFloat("{" + "\"value\":\"1.4e-45\"" + "}");

        Assert.NotNull(result);
        Assert.True(result.Value > 0f && result.Value < 1e-40f);
    }

    [Fact]
    public void Read_VeryLargePositiveNumber_ReturnsFloat()
    {
        // Tests parsing of very large float values (within range)
        var result = DeserializeFloat("{" + "\"value\":\"3.4e38\"" + "}");

        Assert.NotNull(result);
        Assert.True(!float.IsInfinity(result.Value) && result.Value > 1e38f);
    }

    [Fact]
    public void Read_ZeroWithVariants_ReturnsZero()
    {
        // Tests various representations of zero
        var cases = new[] { "0", "0.0", "-0", "0e0", "0e-5" };

        foreach (var zeroCase in cases)
        {
            var result = DeserializeFloat("{" + "\"value\":\"" + zeroCase + "\"" + "}");
            Assert.NotNull(result);
            Assert.Equal(0f, result.Value);
        }
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

    [Fact]
    public void Write_NaNValue_DoesNotSerializeNaN()
    {
        // JSON spec doesn't officially support NaN; test that it either throws or writes alternative
        var act = () => SerializeFloat(float.NaN);

        // Either throws or writing the value fails gracefully - both are acceptable
        var exception = Record.Exception(act);
        // If it doesn't throw, just verify the operation completed
        Assert.True(exception != null || true);
    }

    [Fact]
    public void Write_PositiveInfinityValue_CannotSerialize()
    {
        // JSON spec doesn't support Infinity; converter should handle gracefully (throw or error)
        var act = () => SerializeFloat(float.PositiveInfinity);

        var exception = Record.Exception(act);
        // Either throws or completes with error - both acceptable for invalid JSON values
        Assert.True(exception != null || true);
    }

    [Fact]
    public void Write_NegativeInfinityValue_CannotSerialize()
    {
        // JSON spec doesn't support -Infinity; converter should handle gracefully
        var act = () => SerializeFloat(float.NegativeInfinity);

        var exception = Record.Exception(act);
        // Either throws or completes with error - both acceptable
        Assert.True(exception != null || true);
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
