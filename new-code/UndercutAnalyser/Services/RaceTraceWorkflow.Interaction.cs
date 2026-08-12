namespace UndercutAnalyser.Services;

/// <summary>
/// Encapsulates race-trace legend interaction state transitions and emphasis rules.
/// </summary>
public static partial class RaceTraceWorkflowService
{
    public static RaceTraceLegendInteractionState ApplyLeftClickSelection(
        IReadOnlySet<int> selectedDriverNumbers,
        int? focusedDriverNumber,
        int clickedDriverNumber)
    {
        var nextSelected = selectedDriverNumbers.ToHashSet();

        if (focusedDriverNumber.HasValue)
        {
            nextSelected.Add(focusedDriverNumber.Value);
            nextSelected.Add(clickedDriverNumber);
            return new RaceTraceLegendInteractionState(nextSelected, FocusedDriverNumber: null);
        }

        if (nextSelected.Contains(clickedDriverNumber))
            nextSelected.Remove(clickedDriverNumber);
        else
            nextSelected.Add(clickedDriverNumber);

        return new RaceTraceLegendInteractionState(nextSelected, FocusedDriverNumber: null);
    }

    public static RaceTraceLegendInteractionState ApplyRightClickFocus(
        IReadOnlySet<int> selectedDriverNumbers,
        int? focusedDriverNumber,
        int clickedDriverNumber)
    {
        if (selectedDriverNumbers.Count > 0)
            return new RaceTraceLegendInteractionState([], clickedDriverNumber);

        int? nextFocused = focusedDriverNumber == clickedDriverNumber ? null : clickedDriverNumber;
        return new RaceTraceLegendInteractionState(selectedDriverNumbers.ToHashSet(), nextFocused);
    }

    public static HashSet<int> PruneSelectedDrivers(
        IReadOnlySet<int> selectedDriverNumbers,
        IReadOnlyList<RaceTraceDriverSeries> series)
    {
        var active = series.Select(s => s.DriverNumber).ToHashSet();
        return selectedDriverNumbers.Where(active.Contains).ToHashSet();
    }

    public static int? PruneFocusedDriver(int? focusedDriverNumber, IReadOnlyList<RaceTraceDriverSeries> series)
    {
        if (!focusedDriverNumber.HasValue)
            return null;

        return series.Any(s => s.DriverNumber == focusedDriverNumber.Value)
            ? focusedDriverNumber
            : null;
    }

    public static double? ResolveDimmingMix(
        int driverNumber,
        IReadOnlySet<int> selectedDriverNumbers,
        int? focusedDriverNumber,
        int? hoveredDriverNumber)
    {
        if (selectedDriverNumbers.Count > 0 && hoveredDriverNumber.HasValue)
        {
            var isPrimary = selectedDriverNumbers.Contains(driverNumber) || driverNumber == hoveredDriverNumber.Value;
            return isPrimary ? null : 0.90;
        }

        if (selectedDriverNumbers.Count > 0)
        {
            return selectedDriverNumbers.Contains(driverNumber) ? null : 0.90;
        }

        if (focusedDriverNumber.HasValue && hoveredDriverNumber.HasValue)
        {
            var isPrimary = driverNumber == focusedDriverNumber.Value || driverNumber == hoveredDriverNumber.Value;
            return isPrimary ? null : 0.90;
        }

        if (focusedDriverNumber.HasValue)
            return driverNumber == focusedDriverNumber.Value ? null : 0.90;

        if (hoveredDriverNumber.HasValue)
            return driverNumber == hoveredDriverNumber.Value ? null : 0.75;

        return null;
    }
}

public sealed record RaceTraceLegendInteractionState(
    HashSet<int> SelectedDriverNumbers,
    int? FocusedDriverNumber);
