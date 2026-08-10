namespace UndercutAnalyser.Services;

/// <summary>
/// Provides deterministic UI-state transitions for the manual prediction panel.
/// </summary>
public static class MainWindowPredictionUiStatePresenter
{
    /// <summary>
    /// Returns the baseline state used when starting a new prediction attempt.
    /// </summary>
    public static MainWindowPredictionUiState Initial() =>
        new(
            ShowValidation: false,
            ValidationMessage: null,
            ShowResult: false,
            EnableExport: false);

    /// <summary>
    /// Returns the UI state for a validation failure.
    /// </summary>
    public static MainWindowPredictionUiState ValidationError(string? message) =>
        new(
            ShowValidation: true,
            ValidationMessage: message,
            ShowResult: false,
            EnableExport: false);

    /// <summary>
    /// Returns the UI state for an execution failure.
    /// </summary>
    public static MainWindowPredictionUiState ExecutionError(string? message) =>
        new(
            ShowValidation: true,
            ValidationMessage: message,
            ShowResult: false,
            EnableExport: false);

    /// <summary>
    /// Returns the UI state for a successful prediction result.
    /// </summary>
    public static MainWindowPredictionUiState Success() =>
        new(
            ShowValidation: false,
            ValidationMessage: null,
            ShowResult: true,
            EnableExport: true);
}

/// <summary>
/// Render-ready state for prediction panel controls.
/// </summary>
public sealed record MainWindowPredictionUiState(
    bool ShowValidation,
    string? ValidationMessage,
    bool ShowResult,
    bool EnableExport);
