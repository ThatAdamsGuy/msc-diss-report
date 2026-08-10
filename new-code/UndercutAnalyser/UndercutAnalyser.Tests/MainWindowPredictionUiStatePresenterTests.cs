using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionUiStatePresenterTests
{
    [Fact]
    public void Initial_ReturnsHiddenValidationHiddenResultAndDisabledExport()
    {
        var state = MainWindowPredictionUiStatePresenter.Initial();

        Assert.False(state.ShowValidation);
        Assert.Null(state.ValidationMessage);
        Assert.False(state.ShowResult);
        Assert.False(state.EnableExport);
    }

    [Fact]
    public void ValidationError_ReturnsVisibleValidationWithMessage()
    {
        var state = MainWindowPredictionUiStatePresenter.ValidationError("Select an attacking driver.");

        Assert.True(state.ShowValidation);
        Assert.Equal("Select an attacking driver.", state.ValidationMessage);
        Assert.False(state.ShowResult);
        Assert.False(state.EnableExport);
    }

    [Fact]
    public void ExecutionError_ReturnsVisibleValidationWithMessage()
    {
        var state = MainWindowPredictionUiStatePresenter.ExecutionError("Prediction failed: boom");

        Assert.True(state.ShowValidation);
        Assert.Equal("Prediction failed: boom", state.ValidationMessage);
        Assert.False(state.ShowResult);
        Assert.False(state.EnableExport);
    }

    [Fact]
    public void Success_ReturnsHiddenValidationVisibleResultAndEnabledExport()
    {
        var state = MainWindowPredictionUiStatePresenter.Success();

        Assert.False(state.ShowValidation);
        Assert.Null(state.ValidationMessage);
        Assert.True(state.ShowResult);
        Assert.True(state.EnableExport);
    }
}
