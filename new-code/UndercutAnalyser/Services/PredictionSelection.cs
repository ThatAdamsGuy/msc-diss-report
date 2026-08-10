namespace UndercutAnalyser.Services;

/// <summary>
/// Minimal driver selection payload shared across MainWindow scan/prediction-related services.
/// </summary>
public sealed record PredictionSelection(int DriverNumber, string Code, string DisplayName);
