using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowManualPredictionRequestBuilderTests
{
    [Fact]
    public void Build_ValidInput_MapsRequestFieldsAndCodes()
    {
        var validation = PredictionInputValidationResult.Success(
            attacker: new MainWindowPredictionSelection(4, "NOR", "Lando Norris"),
            target: new MainWindowPredictionSelection(81, "PIA", "Oscar Piastri"),
            decisionLap: 18,
            initialGapSeconds: 1.25,
            attackerReferencePace: 90.3,
            targetReferencePace: 90.7);

        var requestData = MainWindowManualPredictionRequestBuilder.Build(
            eventName: "Monza GP",
            validation: validation,
            attackerTyreState: ("SOFT", 6),
            targetTyreState: ("MEDIUM", 10),
            attackerReplacementCompoundText: "HARD",
            targetReplacementCompoundText: "SOFT",
            attackerReplacementAgeText: "2",
            targetReplacementAgeText: "3",
            targetResponseLaps: 2,
            modelParameters: LapModelParameters.CreateDefault());

        Assert.Equal("NOR", requestData.AttackerCode);
        Assert.Equal("PIA", requestData.TargetCode);

        var request = requestData.Request;
        Assert.Equal("Monza GP", request.EventName);
        Assert.Equal(18, request.DecisionLap);
        Assert.Equal(1.25, request.InitialAttackerGapToTargetSeconds, 10);
        Assert.Equal(TyreCompound.Soft, request.Attacker.CurrentCompound);
        Assert.Equal(6, request.Attacker.CurrentTyreAgeLaps);
        Assert.Equal(TyreCompound.Medium, request.Target.CurrentCompound);
        Assert.Equal(10, request.Target.CurrentTyreAgeLaps);
        Assert.Equal(TyreCompound.Hard, request.AttackerReplacementTyre.Compound);
        Assert.Equal(2, request.AttackerReplacementTyre.InitialAgeLaps);
        Assert.Equal(TyreCompound.Soft, request.TargetReplacementTyre.Compound);
        Assert.Equal(3, request.TargetReplacementTyre.InitialAgeLaps);
    }

    [Fact]
    public void Build_InvalidReplacementAgeText_FallsBackToZero()
    {
        var validation = PredictionInputValidationResult.Success(
            attacker: new MainWindowPredictionSelection(4, "NOR", "Lando Norris"),
            target: new MainWindowPredictionSelection(81, "PIA", "Oscar Piastri"),
            decisionLap: 20,
            initialGapSeconds: 0.8,
            attackerReferencePace: 90.0,
            targetReferencePace: 90.5);

        var requestData = MainWindowManualPredictionRequestBuilder.Build(
            eventName: "Spa GP",
            validation: validation,
            attackerTyreState: ("SOFT", 5),
            targetTyreState: ("HARD", 7),
            attackerReplacementCompoundText: "MEDIUM",
            targetReplacementCompoundText: "HARD",
            attackerReplacementAgeText: "not-a-number",
            targetReplacementAgeText: null,
            targetResponseLaps: 1,
            modelParameters: LapModelParameters.CreateDefault());

        Assert.Equal(0, requestData.Request.AttackerReplacementTyre.InitialAgeLaps);
        Assert.Equal(0, requestData.Request.TargetReplacementTyre.InitialAgeLaps);
    }

    [Fact]
    public void Build_UnrecognizedReplacementCompound_MapsToUnknown()
    {
        var validation = PredictionInputValidationResult.Success(
            attacker: new MainWindowPredictionSelection(4, "NOR", "Lando Norris"),
            target: new MainWindowPredictionSelection(81, "PIA", "Oscar Piastri"),
            decisionLap: 12,
            initialGapSeconds: 1.1,
            attackerReferencePace: 89.9,
            targetReferencePace: 90.2);

        var requestData = MainWindowManualPredictionRequestBuilder.Build(
            eventName: "Silverstone GP",
            validation: validation,
            attackerTyreState: ("SOFT", 4),
            targetTyreState: ("MEDIUM", 6),
            attackerReplacementCompoundText: "C5",
            targetReplacementCompoundText: "UNKNOWN-COMPOUND",
            attackerReplacementAgeText: "0",
            targetReplacementAgeText: "1",
            targetResponseLaps: 1,
            modelParameters: LapModelParameters.CreateDefault());

        Assert.Equal(TyreCompound.Unknown, requestData.Request.AttackerReplacementTyre.Compound);
        Assert.Equal(TyreCompound.Unknown, requestData.Request.TargetReplacementTyre.Compound);
    }
}
