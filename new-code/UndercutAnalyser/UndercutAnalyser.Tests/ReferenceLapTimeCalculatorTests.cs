using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class ReferenceLapTimeCalculatorTests
{
    #region Baseline aggregation and empty-input behavior
    // These tests exist to lock down the top-level contract for summary fields so
    // downstream consumers (UI/debug outputs) can rely on stable defaults and counts.

    [Fact]
    public void Calculate_EmptyInputs_ReturnsZeroedSummaryWithNullAverage()
    {
        var result = ReferenceLapTimeCalculator.Calculate(
            laps: [],
            raceControlMessages: [],
            secondsPer10Kg: 0.3,
            fuelKg: 110.0);

        Assert.Equal(0, result.TotalLapRows);
        Assert.Equal(0, result.MaxSessionLapNumber);
        Assert.Equal(0, result.IncludedLaps);
        Assert.Equal(0, result.ExcludedPitOutLaps);
        Assert.Equal(0, result.ExcludedPitInLaps);
        Assert.Equal(0, result.ExcludedSafetyCarLaps);
        Assert.Equal(0.0, result.FuelEffectPerLapSeconds);
        Assert.Equal(0.0, result.SumLapTimeSeconds, 10);
        Assert.Null(result.AverageLapTimeSeconds);
        Assert.Empty(result.SafetyCarWindows);
    }

    [Fact]
    public void Calculate_ComputesFuelEffectPerLapFromMaxSessionLap()
    {
        var start = Utc(2025, 1, 1, 12, 0, 0);
        var laps = new[]
        {
            Lap(2, start.AddSeconds(100), 100f),
            Lap(5, start.AddSeconds(200), 100f),
            Lap(10, start.AddSeconds(300), 100f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(
            laps,
            raceControlMessages: [],
            secondsPer10Kg: 0.5,
            fuelKg: 100.0);

        // totalFuelEffectSeconds = 0.5 * (100/10) = 5.0; max lap = 10 => 0.5 per lap
        Assert.Equal(10, result.MaxSessionLapNumber);
        Assert.Equal(0.5, result.FuelEffectPerLapSeconds, 10);
    }

    #endregion

    #region Lap inclusion and exclusion rules
    // These tests exist to verify exactly which lap rows are eligible for the reference
    // average, including pit-out exclusion and null-data guard behavior.

    [Fact]
    public void Calculate_ExcludesPitOutLapsAndCountsThemSeparately()
    {
        var start = Utc(2025, 2, 1, 10, 0, 0);
        var laps = new[]
        {
            Lap(1, start, 100f, isPitOutLap: true),
            Lap(2, start.AddSeconds(100), 101f),
            Lap(3, start.AddSeconds(201), 102f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, [], secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(3, result.TotalLapRows);
        Assert.Equal(1, result.ExcludedPitOutLaps);
        Assert.Equal(0, result.ExcludedPitInLaps);
        Assert.Equal(2, result.IncludedLaps);
        Assert.Equal(203.0, result.SumLapTimeSeconds, 10);
        Assert.NotNull(result.AverageLapTimeSeconds);
        Assert.Equal(101.5, result.AverageLapTimeSeconds.Value, 10);
    }

    [Fact]
    public void Calculate_IgnoresLapsMissingDurationOrStartWithoutIncreasingExclusionCounters()
    {
        var start = Utc(2025, 2, 1, 11, 0, 0);
        var laps = new[]
        {
            Lap(1, start, 100f),
            new EventLap { LapNumber = 2, DateStart = null, LapDuration = 101f },
            new EventLap { LapNumber = 3, DateStart = start.AddSeconds(201), LapDuration = null }
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, [], secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(3, result.TotalLapRows);
        Assert.Equal(1, result.IncludedLaps);
        Assert.Equal(0, result.ExcludedPitOutLaps);
        Assert.Equal(0, result.ExcludedPitInLaps);
        Assert.Equal(0, result.ExcludedSafetyCarLaps);
        Assert.Equal(100.0, result.SumLapTimeSeconds, 10);
    }

    [Fact]
    public void Calculate_ExcludesInferredPitInLap_WhenFollowedByPitOutLap()
    {
        var start = Utc(2025, 2, 1, 12, 0, 0);
        var laps = new[]
        {
            new EventLap { DriverNumber = 44, LapNumber = 1, DateStart = start, LapDuration = 100f },
            new EventLap { DriverNumber = 44, LapNumber = 2, DateStart = start.AddSeconds(100), LapDuration = 101f },
            new EventLap { DriverNumber = 44, LapNumber = 3, DateStart = start.AddSeconds(201), LapDuration = 102f, IsPitOutLap = true },
            new EventLap { DriverNumber = 44, LapNumber = 4, DateStart = start.AddSeconds(303), LapDuration = 99f }
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, [], secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(1, result.ExcludedPitOutLaps);
        Assert.Equal(1, result.ExcludedPitInLaps);
        Assert.Equal(2, result.IncludedLaps);
        Assert.Equal(199.0, result.SumLapTimeSeconds, 10);
    }

    #endregion

    #region Safety car window construction and overlap semantics
    // These tests exist to ensure SC/VSC messages are transformed into windows exactly as
    // intended and that overlap checks stay inclusive at boundaries.

    [Fact]
    public void Calculate_BuildsClosedVscWindowAndExcludesOverlappingLaps()
    {
        var t0 = Utc(2025, 3, 1, 14, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(30), "SafetyCar", "VSC DEPLOYED"),
            Message(t0.AddSeconds(90), "SafetyCar", "VSC ENDING")
        };

        var laps = new[]
        {
            Lap(1, t0, 20f),          // ends before VSC starts -> included
            Lap(2, t0.AddSeconds(40), 20f), // inside VSC window -> excluded
            Lap(3, t0.AddSeconds(95), 20f)  // after VSC window -> included
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Single(result.SafetyCarWindows);
        Assert.Equal(t0.AddSeconds(30), result.SafetyCarWindows[0].StartUtc);
        Assert.Equal(t0.AddSeconds(90), result.SafetyCarWindows[0].EndUtc);
        Assert.Equal(1, result.ExcludedSafetyCarLaps);
        Assert.Equal(2, result.IncludedLaps);
    }

    [Fact]
    public void Calculate_BuildsClosedScWindowFromSafetyCarCategoryVariant()
    {
        var t0 = Utc(2025, 3, 2, 14, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(15), "Safety Car", "safety car deployed"),
            Message(t0.AddSeconds(55), "Safety Car", "Safety Car In This Lap")
        };

        var laps = new[]
        {
            Lap(1, t0, 10f),
            Lap(2, t0.AddSeconds(20), 20f),
            Lap(3, t0.AddSeconds(60), 20f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Single(result.SafetyCarWindows);
        Assert.Equal(t0.AddSeconds(15), result.SafetyCarWindows[0].StartUtc);
        Assert.Equal(t0.AddSeconds(55), result.SafetyCarWindows[0].EndUtc);
        Assert.Equal(1, result.ExcludedSafetyCarLaps);
    }

    [Fact]
    public void Calculate_LeavesOpenVscWindowUntilLatestKnownMessageDate()
    {
        var t0 = Utc(2025, 3, 3, 10, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(10), "SafetyCar", "VSC DEPLOYED"),
            Message(t0.AddSeconds(80), "SafetyCar", "OTHER SAFETY MESSAGE")
        };

        var laps = new[]
        {
            Lap(1, t0, 9f),
            Lap(2, t0.AddSeconds(20), 10f),
            Lap(3, t0.AddSeconds(85), 10f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Single(result.SafetyCarWindows);
        Assert.Equal(t0.AddSeconds(10), result.SafetyCarWindows[0].StartUtc);
        Assert.Equal(t0.AddSeconds(80), result.SafetyCarWindows[0].EndUtc);
        Assert.Equal(1, result.ExcludedSafetyCarLaps);
        Assert.Equal(2, result.IncludedLaps);
    }

    [Fact]
    public void Calculate_OverlapCheckIsInclusiveAtWindowBoundaries()
    {
        var t0 = Utc(2025, 3, 4, 9, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(10), "SafetyCar", "VSC DEPLOYED"),
            Message(t0.AddSeconds(20), "SafetyCar", "VSC ENDING")
        };

        var laps = new[]
        {
            Lap(1, t0, 10f),               // ends exactly at window start -> excluded
            Lap(2, t0.AddSeconds(20), 10f), // starts exactly at window end -> excluded
            Lap(3, t0.AddSeconds(21), 10f)  // fully outside -> included
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(2, result.ExcludedSafetyCarLaps);
        Assert.Equal(1, result.IncludedLaps);
    }

    [Fact]
    public void Calculate_IgnoresNonSafetyCategoriesAndNullDatedMessages()
    {
        var t0 = Utc(2025, 3, 5, 9, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(10), "TrackStatus", "VSC DEPLOYED"),
            new RaceControlMessage { Category = "SafetyCar", Message = "VSC DEPLOYED", Date = null }
        };

        var laps = new[]
        {
            Lap(1, t0, 10f),
            Lap(2, t0.AddSeconds(15), 10f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Empty(result.SafetyCarWindows);
        Assert.Equal(0, result.ExcludedSafetyCarLaps);
        Assert.Equal(2, result.IncludedLaps);
    }

    [Fact]
    public void Calculate_BuildsMultipleWindows_WhenVscAndScPhasesBothExist()
    {
        var t0 = Utc(2025, 3, 6, 9, 0, 0);
        var messages = new[]
        {
            Message(t0.AddSeconds(5), "SafetyCar", "VSC DEPLOYED"),
            Message(t0.AddSeconds(20), "SafetyCar", "VSC ENDING"),
            Message(t0.AddSeconds(40), "Safety Car", "SAFETY CAR DEPLOYED"),
            Message(t0.AddSeconds(70), "Safety Car", "SAFETY CAR IN THIS LAP")
        };

        var laps = new[]
        {
            Lap(1, t0, 4f),
            Lap(2, t0.AddSeconds(10), 5f),
            Lap(3, t0.AddSeconds(45), 5f),
            Lap(4, t0.AddSeconds(75), 5f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(2, result.SafetyCarWindows.Count);
        Assert.Equal(2, result.ExcludedSafetyCarLaps);
        Assert.Equal(2, result.IncludedLaps);
    }

    [Fact]
    public void Calculate_ConvertsUnspecifiedTimesToUtcForOverlapChecks()
    {
        var baseUnspecified = new DateTime(2025, 3, 7, 10, 0, 0, DateTimeKind.Unspecified);
        var messages = new[]
        {
            new RaceControlMessage { Date = baseUnspecified.AddSeconds(10), Category = "SafetyCar", Message = "VSC DEPLOYED" },
            new RaceControlMessage { Date = baseUnspecified.AddSeconds(20), Category = "SafetyCar", Message = "VSC ENDING" }
        };

        var laps = new[]
        {
            new EventLap { LapNumber = 1, DateStart = baseUnspecified.AddSeconds(12), LapDuration = 5f },
            new EventLap { LapNumber = 2, DateStart = baseUnspecified.AddSeconds(30), LapDuration = 5f }
        };

        var result = ReferenceLapTimeCalculator.Calculate(laps, messages, secondsPer10Kg: 0.0, fuelKg: 0.0);

        Assert.Equal(1, result.ExcludedSafetyCarLaps);
        Assert.Equal(1, result.IncludedLaps);
        Assert.All(result.SafetyCarWindows, window =>
        {
            Assert.Equal(DateTimeKind.Utc, window.StartUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, window.EndUtc.Kind);
        });
    }

    #endregion

    #region Fuel correction arithmetic
    // These tests exist to protect the fuel-adjustment math, especially the lap-index
    // weighting that references MaxSessionLapNumber.

    [Fact]
    public void Calculate_AppliesFuelAdjustmentByDistanceFromMaxSessionLap()
    {
        var t0 = Utc(2025, 4, 1, 8, 0, 0);
        var laps = new[]
        {
            Lap(1, t0, 100f),
            Lap(2, t0.AddSeconds(100), 100f),
            Lap(3, t0.AddSeconds(200), 100f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(
            laps,
            raceControlMessages: [],
            secondsPer10Kg: 1.0,
            fuelKg: 30.0);

        // totalFuelEffect = 1.0 * (30/10) = 3.0; max lap = 3 => effect per lap = 1.0
        // lap1 adjustment = 2.0 => 98.0
        // lap2 adjustment = 1.0 => 99.0
        // lap3 adjustment = 0.0 => 100.0
        Assert.Equal(1.0, result.FuelEffectPerLapSeconds, 10);
        Assert.Equal(297.0, result.SumLapTimeSeconds, 10);
        Assert.NotNull(result.AverageLapTimeSeconds);
        Assert.Equal(99.0, result.AverageLapTimeSeconds.Value, 10);
    }

    [Fact]
    public void Calculate_NegativeFuelInputsAreAppliedAsProvided()
    {
        var t0 = Utc(2025, 4, 2, 8, 0, 0);
        var laps = new[]
        {
            Lap(1, t0, 100f),
            Lap(2, t0.AddSeconds(100), 100f)
        };

        var result = ReferenceLapTimeCalculator.Calculate(
            laps,
            raceControlMessages: [],
            secondsPer10Kg: -1.0,
            fuelKg: 20.0);

        // totalFuelEffect = -1.0 * (20/10) = -2.0; max lap = 2 => -1.0 per lap
        // lap1 adjustment = -1.0 => subtracting adjustment increases lap to 101.0
        // lap2 adjustment = 0.0 => 100.0
        Assert.Equal(-1.0, result.FuelEffectPerLapSeconds, 10);
        Assert.Equal(201.0, result.SumLapTimeSeconds, 10);
        Assert.NotNull(result.AverageLapTimeSeconds);
        Assert.Equal(100.5, result.AverageLapTimeSeconds.Value, 10);
    }

    #endregion

    private static EventLap Lap(int lapNumber, DateTime dateStartUtc, float durationSeconds, bool isPitOutLap = false) =>
        new()
        {
            LapNumber = lapNumber,
            DateStart = dateStartUtc,
            LapDuration = durationSeconds,
            IsPitOutLap = isPitOutLap
        };

    private static RaceControlMessage Message(DateTime dateUtc, string category, string message) =>
        new()
        {
            Date = dateUtc,
            Category = category,
            Message = message
        };

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);
}
