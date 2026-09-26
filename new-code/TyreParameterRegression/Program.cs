using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace TyreParameterRegression;

internal static class Program
{
    private const double OutlierSigma = 3.0;
    private const int MaxOutlierPasses = 5;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- <export1.json> [export2.json ...]");
            Console.WriteLine("  dotnet run -- <directory-containing-json-exports>");
            return 1;
        }

        var paths = ExpandInputPaths(args).ToList();
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("No JSON export files were found.");
            return 1;
        }

        var results = new List<CaseResult>();

        foreach (var path in paths)
        {
            try
            {
                var result = Analyse(path);
                results.Add(result);
                PrintCase(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"FAILED: {path}");
                Console.Error.WriteLine(ex);
            }
        }

        if (results.Count > 0)
        {
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "tyre_parameter_regression_results.csv");
            WriteCsv(csvPath, results);
            Console.WriteLine();
            Console.WriteLine($"Summary CSV written to: {csvPath}");
        }

        return 0;
    }

    private static CaseResult Analyse(string path)
    {
        var json = File.ReadAllText(path);
        var export = JsonSerializer.Deserialize<FullExport>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("The export JSON could not be deserialised.");

        var prepared = PrepareRows(export);
        if (prepared.Rows.Count == 0)
            throw new InvalidOperationException("No eligible Medium/Hard race laps remained after filtering.");

        var driverNumbers = prepared.Rows
            .Select(x => x.DriverNumber)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        if (driverNumbers.Length < 2)
            throw new InvalidOperationException("At least two drivers are required to fit driver-specific pace effects.");

        var referenceDriver = driverNumbers[0];

        var linear = Fit(
            prepared.Rows,
            driverNumbers,
            referenceDriver,
            includeQuadraticLapTerm: false);

        var quadratic = Fit(
            prepared.Rows,
            driverNumbers,
            referenceDriver,
            includeQuadraticLapTerm: true);

        return new CaseResult(
            Path.GetFileName(path),
            export.EventDisplay ?? Path.GetFileName(path),
            prepared.Rows.Count,
            prepared.ExclusionCounts,
            referenceDriver,
            linear,
            quadratic);
    }

    private static PreparedData PrepareRows(FullExport export)
    {
        var rows = new List<RaceLapObservation>();
        var exclusions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var stintsByDriver = export.Data.Stints
            .GroupBy(x => x.DriverNumber)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.LapStart).ToArray());

        var deletedLaps = ParseDeletedLaps(export.Data.RaceControlMessages);

        foreach (var lap in export.Data.Laps)
        {
            if (lap.LapNumber == 1)
            {
                Count(exclusions, "Lap 1");
                continue;
            }

            if (lap.LapDuration is null || lap.LapDuration <= 0)
            {
                Count(exclusions, "Missing/invalid lap time");
                continue;
            }

            if (lap.IsPitOutLap)
            {
                Count(exclusions, "Pit out lap");
                continue;
            }

            if (deletedLaps.Contains((lap.DriverNumber, lap.LapNumber)))
            {
                Count(exclusions, "Deleted lap");
                continue;
            }

            if (!stintsByDriver.TryGetValue(lap.DriverNumber, out var driverStints))
            {
                Count(exclusions, "No stint");
                continue;
            }

            var stint = driverStints.FirstOrDefault(
                x => lap.LapNumber >= x.LapStart && lap.LapNumber <= x.LapEnd);

            if (stint is null)
            {
                Count(exclusions, "No stint");
                continue;
            }

            var compound = (stint.Compound ?? string.Empty).Trim().ToUpperInvariant();
            if (compound is not ("MEDIUM" or "HARD"))
            {
                Count(exclusions, "Non Medium/Hard compound");
                continue;
            }

            // A lap at the end of a stint is a pit-in lap if another stint follows.
            // This follows the export convention where the old stint includes the
            // lap on which the driver enters the pit lane at the end of the lap.
            var hasFollowingStint = driverStints.Any(x => x.LapStart > stint.LapStart);
            if (lap.LapNumber == stint.LapEnd && hasFollowingStint)
            {
                Count(exclusions, "Pit in lap");
                continue;
            }

            var tyreAgeAtStart = stint.TyreAgeAtStart ?? 0;
            var tyreAge = tyreAgeAtStart + (lap.LapNumber - stint.LapStart);

            if (tyreAge < 0)
            {
                Count(exclusions, "Invalid tyre age");
                continue;
            }

            rows.Add(new RaceLapObservation(
                lap.DriverNumber,
                lap.LapNumber,
                (float)lap.LapDuration.Value,
                compound,
                tyreAge));
        }

        return new PreparedData(rows, exclusions);
    }

    private static HashSet<(int Driver, int Lap)> ParseDeletedLaps(
        IReadOnlyCollection<RaceControlMessage> messages)
    {
        var deleted = new HashSet<(int Driver, int Lap)>();

        var carRegex = new Regex(@"\bCAR\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var lapRegex = new Regex(@"\bLAP\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var message in messages)
        {
            var text = message.Message ?? string.Empty;
            if (!text.Contains("DELETED", StringComparison.OrdinalIgnoreCase))
                continue;

            var carMatch = carRegex.Match(text);
            var lapMatches = lapRegex.Matches(text);

            if (!carMatch.Success || lapMatches.Count == 0)
                continue;

            // FIA messages can mention the message lap as well as the offending lap.
            // The last explicit "LAP n" in the text is the offending lap in these exports.
            var driver = int.Parse(carMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var lap = int.Parse(lapMatches[^1].Groups[1].Value, CultureInfo.InvariantCulture);
            deleted.Add((driver, lap));
        }

        return deleted;
    }

    private static FitResult Fit(
        IReadOnlyList<RaceLapObservation> sourceRows,
        IReadOnlyList<int> driverNumbers,
        int referenceDriver,
        bool includeQuadraticLapTerm)
    {
        var featureDefinition = BuildFeatureDefinition(
            driverNumbers,
            referenceDriver,
            includeQuadraticLapTerm);

        var current = sourceRows.ToList();
        var totalOutliersRemoved = 0;
        RegressionFit? finalFit = null;

        for (var pass = 0; pass < MaxOutlierPasses; pass++)
        {
            var fit = Train(current, featureDefinition);
            var residuals = fit.Residuals;

            var parameterCount = featureDefinition.Names.Count + 1; // + bias
            var degreesOfFreedom = Math.Max(1, residuals.Count - parameterCount);
            var residualStdDev = Math.Sqrt(
                residuals.Sum(x => x * x) / degreesOfFreedom);

            if (residualStdDev <= 0 || double.IsNaN(residualStdDev))
            {
                finalFit = fit;
                break;
            }

            var limit = OutlierSigma * residualStdDev;
            var keep = residuals
                .Select(x => Math.Abs(x) <= limit)
                .ToArray();

            var removedThisPass = keep.Count(x => !x);
            if (removedThisPass == 0)
            {
                finalFit = fit;
                break;
            }

            totalOutliersRemoved += removedThisPass;
            current = current
                .Where((_, index) => keep[index])
                .ToList();

            if (current.Count <= parameterCount + 1)
                throw new InvalidOperationException(
                    "Too few observations remain after outlier filtering.");
        }

        finalFit ??= Train(current, featureDefinition);

        var coefficients = finalFit.Coefficients;
        var coefficientMap = featureDefinition.Names
            .Select((name, index) => (name, value: coefficients[index]))
            .ToDictionary(x => x.name, x => x.value, StringComparer.Ordinal);

        return new FitResult(
            includeQuadraticLapTerm ? "Quadratic" : "Linear",
            current.Count,
            totalOutliersRemoved,
            finalFit.Bias,
            finalFit.RSquared,
            coefficientMap["MediumAge"],
            coefficientMap["HardAge"],
            coefficientMap["Hard"],
            coefficientMap,
            featureDefinition.Names);
    }

    private static RegressionFit Train(
        IReadOnlyList<RaceLapObservation> rows,
        FeatureDefinition definition)
    {
        var ml = new MLContext(seed: 0);

        var inputs = rows
            .Select(x => new RegressionInput
            {
                Label = x.LapTime,
                Features = definition.Build(x)
            })
            .ToList();

        var schema = SchemaDefinition.Create(typeof(RegressionInput));
        schema[nameof(RegressionInput.Features)].ColumnType =
            new VectorDataViewType(
                NumberDataViewType.Single,
                definition.Names.Count);

        var data = ml.Data.LoadFromEnumerable(inputs, schema);

        var trainer = ml.Regression.Trainers.Ols(
            new OlsTrainer.Options
            {
                LabelColumnName = nameof(RegressionInput.Label),
                FeatureColumnName = nameof(RegressionInput.Features),
                L2Regularization = 0,
                CalculateStatistics = true
            });

        var model = trainer.Fit(data);
        var scored = model.Transform(data);

        var predictions = ml.Data
            .CreateEnumerable<RegressionPrediction>(scored, reuseRowObject: false)
            .ToList();

        if (predictions.Count != rows.Count)
            throw new InvalidOperationException("Prediction count did not match input row count.");

        var residuals = predictions
            .Select(x => (double)x.Label - x.Score)
            .ToList();

        var weights = model.Model.Weights.ToArray();
        if (weights.Length != definition.Names.Count)
            throw new InvalidOperationException("OLS coefficient count did not match feature count.");

        return new RegressionFit(
            model.Model.Bias,
            model.Model.RSquared,
            weights.Select(x => (double)x).ToArray(),
            residuals);
    }

    private static FeatureDefinition BuildFeatureDefinition(
        IReadOnlyList<int> driverNumbers,
        int referenceDriver,
        bool includeQuadraticLapTerm)
    {
        var names = new List<string>
        {
            "RaceLap"
        };

        if (includeQuadraticLapTerm)
            names.Add("RaceLapSquared");

        names.Add("Hard");
        names.Add("MediumAge");
        names.Add("HardAge");

        foreach (var driver in driverNumbers.Where(x => x != referenceDriver))
            names.Add($"Driver_{driver}");

        float[] Build(RaceLapObservation row)
        {
            var values = new List<float>(names.Count)
            {
                row.RaceLap
            };

            if (includeQuadraticLapTerm)
                values.Add(row.RaceLap * row.RaceLap);

            var isMedium = row.Compound == "MEDIUM" ? 1f : 0f;
            var isHard = row.Compound == "HARD" ? 1f : 0f;

            values.Add(isHard);
            values.Add(row.TyreAge * isMedium);
            values.Add(row.TyreAge * isHard);

            foreach (var driver in driverNumbers.Where(x => x != referenceDriver))
                values.Add(row.DriverNumber == driver ? 1f : 0f);

            return values.ToArray();
        }

        return new FeatureDefinition(names, Build);
    }

    private static IEnumerable<string> ExpandInputPaths(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (Directory.Exists(argument))
            {
                foreach (var file in Directory
                             .EnumerateFiles(argument, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(x => x))
                {
                    yield return Path.GetFullPath(file);
                }

                continue;
            }

            if (File.Exists(argument))
            {
                yield return Path.GetFullPath(argument);
                continue;
            }

            Console.Error.WriteLine($"Input not found: {argument}");
        }
    }

    private static void PrintCase(CaseResult result)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(result.EventDisplay);
        Console.WriteLine($"File: {result.FileName}");
        Console.WriteLine($"Eligible laps before residual filtering: {result.InitialEligibleRows}");
        Console.WriteLine($"Reference driver for fixed effects: Car {result.ReferenceDriver}");
        Console.WriteLine("Initial exclusions:");

        foreach (var pair in result.ExclusionCounts.OrderBy(x => x.Key))
            Console.WriteLine($"  {pair.Key,-28} {pair.Value,5}");

        PrintFit(result.Linear);
        PrintFit(result.Quadratic);
    }

    private static void PrintFit(FitResult fit)
    {
        Console.WriteLine();
        Console.WriteLine($"{fit.Name} fit");
        Console.WriteLine($"  Laps used                 : {fit.ObservationsUsed}");
        Console.WriteLine($"  Residual outliers removed: {fit.OutliersRemoved}");
        Console.WriteLine($"  R^2                       : {fit.RSquared:F6}");
        Console.WriteLine($"  k_M                       : {fit.MediumDegradation:F6} s/lap");
        Console.WriteLine($"  k_H                       : {fit.HardDegradation:F6} s/lap");
        Console.WriteLine($"  C_H - C_M                 : {fit.HardMinusMedium:F6} s");
    }

    private static void WriteCsv(string path, IReadOnlyCollection<CaseResult> results)
    {
        using var writer = new StreamWriter(path, false);

        writer.WriteLine(
            "Event,File,Fit,InitialEligibleLaps,LapsUsed,OutliersRemoved,RSquared,k_M_s_per_lap,k_H_s_per_lap,C_H_minus_C_M_s");

        foreach (var result in results)
        {
            foreach (var fit in new[] { result.Linear, result.Quadratic })
            {
                writer.WriteLine(string.Join(",",
                    Csv(result.EventDisplay),
                    Csv(result.FileName),
                    Csv(fit.Name),
                    result.InitialEligibleRows.ToString(CultureInfo.InvariantCulture),
                    fit.ObservationsUsed.ToString(CultureInfo.InvariantCulture),
                    fit.OutliersRemoved.ToString(CultureInfo.InvariantCulture),
                    fit.RSquared.ToString("G17", CultureInfo.InvariantCulture),
                    fit.MediumDegradation.ToString("G17", CultureInfo.InvariantCulture),
                    fit.HardDegradation.ToString("G17", CultureInfo.InvariantCulture),
                    fit.HardMinusMedium.ToString("G17", CultureInfo.InvariantCulture)));
            }
        }
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void Count(IDictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }
}

internal sealed record RaceLapObservation(
    int DriverNumber,
    int RaceLap,
    float LapTime,
    string Compound,
    int TyreAge);

internal sealed record PreparedData(
    List<RaceLapObservation> Rows,
    Dictionary<string, int> ExclusionCounts);

internal sealed record FeatureDefinition(
    List<string> Names,
    Func<RaceLapObservation, float[]> Build);

internal sealed record RegressionFit(
    float Bias,
    double RSquared,
    double[] Coefficients,
    List<double> Residuals);

internal sealed record FitResult(
    string Name,
    int ObservationsUsed,
    int OutliersRemoved,
    float Bias,
    double RSquared,
    double MediumDegradation,
    double HardDegradation,
    double HardMinusMedium,
    IReadOnlyDictionary<string, double> Coefficients,
    IReadOnlyList<string> FeatureNames);

internal sealed record CaseResult(
    string FileName,
    string EventDisplay,
    int InitialEligibleRows,
    IReadOnlyDictionary<string, int> ExclusionCounts,
    int ReferenceDriver,
    FitResult Linear,
    FitResult Quadratic);

internal sealed class RegressionInput
{
    public float Label { get; set; }

    public float[] Features { get; set; } = Array.Empty<float>();
}

internal sealed class RegressionPrediction
{
    public float Label { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}

internal sealed class FullExport
{
    public string? EventDisplay { get; set; }

    public ExportData Data { get; set; } = new();
}

internal sealed class ExportData
{
    public List<LapRecord> Laps { get; set; } = [];

    public List<StintRecord> Stints { get; set; } = [];

    public List<RaceControlMessage> RaceControlMessages { get; set; } = [];
}

internal sealed class LapRecord
{
    [JsonPropertyName("driver_number")]
    public int DriverNumber { get; set; }

    [JsonPropertyName("lap_number")]
    public int LapNumber { get; set; }

    [JsonPropertyName("lap_duration")]
    public double? LapDuration { get; set; }

    [JsonPropertyName("is_pit_out_lap")]
    public bool IsPitOutLap { get; set; }
}

internal sealed class StintRecord
{
    [JsonPropertyName("driver_number")]
    public int DriverNumber { get; set; }

    [JsonPropertyName("lap_start")]
    public int LapStart { get; set; }

    [JsonPropertyName("lap_end")]
    public int LapEnd { get; set; }

    [JsonPropertyName("compound")]
    public string? Compound { get; set; }

    [JsonPropertyName("tyre_age_at_start")]
    public int? TyreAgeAtStart { get; set; }
}

internal sealed class RaceControlMessage
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
