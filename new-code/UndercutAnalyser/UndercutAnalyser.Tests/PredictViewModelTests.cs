using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class PredictViewModelTests
{
    #region Defaults and simple value semantics
    // These tests exist to lock down initial UI state so parameter defaults remain stable
    // and accidental changes do not shift model behavior before user interaction.

    [Fact]
    public void Constructor_SetsExpectedDefaults()
    {
        var vm = new PredictViewModel();

        Assert.Empty(vm.Drivers);
        Assert.Empty(vm.Laps);

        Assert.Equal(90.0, vm.AttackerReferencePace, 10);
        Assert.Equal("SOFT", vm.AttackerCurrentCompound);
        Assert.Equal("SOFT", vm.AttackerReplCompound);

        Assert.Equal(90.0, vm.TargetReferencePace, 10);
        Assert.Equal("SOFT", vm.TargetCurrentCompound);
        Assert.Equal("SOFT", vm.TargetReplCompound);

        Assert.Equal(1.5, vm.InitialGap, 10);
        Assert.Equal(1, vm.TargetResponseLaps);
        Assert.Equal(22.0, vm.PitLaneLoss, 10);
        Assert.Equal(2.0, vm.WarmUpPenalty, 10);
        Assert.Equal(0.25, vm.MarginalThreshold, 10);
        Assert.Equal(0.0, vm.TrafficPenalty, 10);
        Assert.False(vm.ApplyAttackerTraffic);
        Assert.False(vm.ApplyTargetTraffic);

        Assert.Null(vm.Result);
        Assert.False(vm.HasResult);
        Assert.Equal(string.Empty, vm.ResultError);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void TargetResponseLaps_ClampsToAtLeastOne(int input, int expected)
    {
        var vm = new PredictViewModel();

        vm.TargetResponseLaps = input;

        Assert.Equal(expected, vm.TargetResponseLaps);
    }

    [Fact]
    public void DriverItem_AndLapItem_ToString_ReturnDisplayText()
    {
        var driver = new DriverItem { DisplayName = "44 HAM" };
        var lap = new LapItem { LapNumber = 31 };

        Assert.Equal("44 HAM", driver.ToString());
        Assert.Equal("Lap 31", lap.ToString());
    }

    #endregion

    #region Property changed notifications and result-state coupling
    // These tests exist to ensure WPF bindings get deterministic updates and that Result/HasResult
    // state transitions always remain synchronized.

    [Fact]
    public void SettingDistinctProperties_RaisesPropertyChanged()
    {
        var vm = new PredictViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        vm.AttackerReferencePace = 91.2;
        vm.TargetCurrentCompound = "MEDIUM";
        vm.InitialGap = 0.8;
        vm.ApplyAttackerTraffic = true;
        vm.ResultError = "input invalid";

        Assert.Contains(nameof(PredictViewModel.AttackerReferencePace), changed);
        Assert.Contains(nameof(PredictViewModel.TargetCurrentCompound), changed);
        Assert.Contains(nameof(PredictViewModel.InitialGap), changed);
        Assert.Contains(nameof(PredictViewModel.ApplyAttackerTraffic), changed);
        Assert.Contains(nameof(PredictViewModel.ResultError), changed);
    }

    [Fact]
    public void SettingSameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new PredictViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        vm.InitialGap = vm.InitialGap;
        vm.AttackerCurrentCompound = vm.AttackerCurrentCompound;

        Assert.DoesNotContain(nameof(PredictViewModel.InitialGap), changed);
        Assert.DoesNotContain(nameof(PredictViewModel.AttackerCurrentCompound), changed);
    }

    [Fact]
    public void Result_SetToNonNull_SetsHasResultTrue_AndRaisesBothNotifications()
    {
        var vm = new PredictViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        var result = CreateResult();
        vm.Result = result;

        Assert.Same(result, vm.Result);
        Assert.True(vm.HasResult);
        Assert.Contains(nameof(PredictViewModel.Result), changed);
        Assert.Contains(nameof(PredictViewModel.HasResult), changed);
    }

    [Fact]
    public void Result_SetToNull_SetsHasResultFalse()
    {
        var vm = new PredictViewModel { Result = CreateResult() };

        vm.Result = null;

        Assert.Null(vm.Result);
        Assert.False(vm.HasResult);
    }

    #endregion

    private static PredictionResult CreateResult() =>
        new(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.0,
            GapAfterTargetPitSeconds: 0.5,
            GapAtN2Seconds: 0.3,
            GapAtN3Seconds: 0.1,
            DeltaGAtN1Seconds: -0.5,
            DeltaGAtN2Seconds: -0.7,
            DeltaGAtN3Seconds: -0.9,
            Classification: UndercutClassification.PredictedMarginal,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
}
