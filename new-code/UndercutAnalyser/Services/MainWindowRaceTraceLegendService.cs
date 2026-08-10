namespace UndercutAnalyser.Services;

/// <summary>
/// Encapsulates race-trace legend label formatting and visibility-state transitions.
/// </summary>
public static class MainWindowRaceTraceLegendService
{
    /// <summary>
    /// Builds the style-prefixed legend label text for a driver.
    /// </summary>
    public static string BuildLegendLabel(string driverName, bool isSolidLine)
    {
        var stylePrefix = isSolidLine ? "━" : "┅";
        return $"{stylePrefix} {driverName}";
    }

    /// <summary>
    /// Updates visibility for a single driver due to checked/unchecked toggle action.
    /// </summary>
    public static IReadOnlyDictionary<int, bool> ApplyToggle(
        IReadOnlyDictionary<int, bool> currentVisibility,
        int driverNumber,
        bool isVisible)
    {
        var next = currentVisibility.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        next[driverNumber] = isVisible;
        return next;
    }

    /// <summary>
    /// Isolates one driver and hides all other currently tracked drivers.
    /// </summary>
    public static IReadOnlyDictionary<int, bool> IsolateDriver(
        IReadOnlyDictionary<int, bool> currentVisibility,
        int driverNumber)
    {
        var next = new Dictionary<int, bool>(currentVisibility.Count);
        foreach (var key in currentVisibility.Keys)
        {
            next[key] = key == driverNumber;
        }

        return next;
    }
}
