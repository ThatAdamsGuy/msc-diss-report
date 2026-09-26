# Tyre Parameter Regression

Small standalone .NET 10 utility for estimating the historic tyre parameters used by the Undercut Analyser dissertation validation.

It reads the application's **full JSON export** and fits two ordinary least-squares (OLS) multiple linear regressions using ML.NET:

Primary linear fit:

```text
LapTime =
    driver effect
  + beta1 * race lap
  + (C_H - C_M) * Hard
  + k_M * Medium tyre age
  + k_H * Hard tyre age
  + residual
```

Quadratic robustness check:

```text
LapTime =
    driver effect
  + beta1 * race lap
  + beta2 * race lap^2
  + (C_H - C_M) * Hard
  + k_M * Medium tyre age
  + k_H * Hard tyre age
  + residual
```

The values of interest are:

- `k_M`: Medium degradation, seconds/lap
- `k_H`: Hard degradation, seconds/lap
- `C_H - C_M`: Hard pace offset relative to Medium

Medium is the local reference compound (`C_M = 0`). This does **not** mean that Medium has zero absolute pace offset relative to Soft; it only means that the Medium/Hard comparison is expressed relative to Medium.

## Why this method

A raw lap-time comparison would mix tyre behaviour with:

- different driver/car pace;
- changing fuel load and other broad race progression;
- different tyre ages;
- compounds being used at different points in the race.

Multiple linear regression is used because it is a standard way to estimate several effects from the same set of observed data simultaneously. The nuisance driver and race-lap coefficients are fitted by the regression rather than manually selected. They are included so those effects are less likely to be incorrectly attributed to compound or tyre age.

ML.NET's `OlsTrainer` is used rather than implementing the regression algorithm manually.

## Filtering

The utility consistently excludes:

- lap 1;
- missing/invalid lap times;
- pit-out laps;
- pit-in laps;
- FIA race-control messages identifying deleted laps;
- compounds other than Medium and Hard.

Tyre age is reconstructed from the stint export as:

```text
tyre age = tyre_age_at_start + (race lap - stint lap_start)
```

This is important because used tyres can begin a stint with a non-zero age.

After the fixed filters, the regression is fitted and observations with residuals further than **3 residual standard deviations** from the fit are removed. The regression is then repeated, for a maximum of five passes. This is a fixed rule applied to every race and prevents isolated abnormal race laps from dominating the estimated coefficients.

## Run

From the project directory:

```powershell
dotnet restore
dotnet run -- "C:\path\to\hung2023_fullexport.json"
```

Run all four exports at once:

```powershell
dotnet run -- `
  "C:\path\to\hung2023_fullexport.json" `
  "C:\path\to\ital2024_fullexport.json" `
  "C:\path\to\belg2024_fullexport.json" `
  "C:\path\to\japa2025_fullexport.json"
```

Or pass a directory containing the JSON exports:

```powershell
dotnet run -- "C:\path\to\exports"
```

The console prints the linear and quadratic results for every event and writes:

```text
tyre_parameter_regression_results.csv
```

to the current working directory.

## Packages

- `Microsoft.ML` 5.0.0
- `Microsoft.ML.Mkl.Components` 5.0.0

`OlsTrainer` is supplied by `Microsoft.ML.Mkl.Components`.

## Expected ballpark from the four dissertation exports

These are included as a regression check, not hard-coded outputs. Small differences are possible if the export data or filtering changes.

| Event | Fit | k_M | k_H | C_H - C_M |
|---|---|---:|---:|---:|
| Hungary 2023 | Linear | ~0.049 | ~0.063 | ~-0.073 |
| Hungary 2023 | Quadratic | ~0.046 | ~0.059 | ~+0.052 |
| Italy 2024 | Linear | ~0.045 | ~0.049 | ~-0.275 |
| Italy 2024 | Quadratic | ~0.043 | ~0.050 | ~-0.329 |
| Belgium 2024 | Linear | ~0.103 | ~0.057 | ~-0.084 |
| Belgium 2024 | Quadratic | ~0.113 | ~0.053 | ~+0.109 |
| Japan 2025 | Linear | ~0.045 | ~0.033 | ~+0.090 |
| Japan 2025 | Quadratic | ~0.050 | ~0.028 | ~+0.223 |

These ballpark values were independently reproduced from the same four exports using the same feature construction and filtering logic before the C# utility was generated.
