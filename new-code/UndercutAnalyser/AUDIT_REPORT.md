# UndercutAnalyser Full Audit Report

Generated: 2026-08-12
Scope: Full solution audit (build, test, coverage, dependency checks, and recent-code sanity)
Mode: Informational (no fail gate)

## 1) Environment and Baseline

- Workspace: `new-code/UndercutAnalyser`
- Solution: `UndercutAnalyser.sln`
- SDK: .NET SDK `10.0.302`
- Repo baseline: uncommitted file detected outside app code (`Reprot/4_ui_config.png`)

## 2) Restore and Build Health

Commands run:
- `dotnet restore UndercutAnalyser.sln`
- `dotnet build UndercutAnalyser.sln -c Debug -v minimal`

Result:
- Restore: Success
- Build: Success
- Warnings:
  - `ScanWorkflow.Candidates.cs(120,49): CS8629 Nullable value type may be null` (pre-existing)

## 3) Test Health (Console Summary)

Command run:
- `dotnet test UndercutAnalyser.sln -v minimal`

Result:
- Total: 365
- Passed: 365
- Failed: 0
- Skipped: 0

## 4) Coverage Collection and Reports

Coverage collection command:
- `dotnet test UndercutAnalyser.sln --collect:"XPlat Code Coverage" --results-directory "TestReports/CoverageRaw" -v minimal`

Coverage report generation command:
- `reportgenerator -reports:"TestReports/CoverageRaw/**/coverage.cobertura.xml" -targetdir:"TestReports/CoverageReport" -reporttypes:"Html;Cobertura;TextSummary"`

Generated artifacts:
- HTML report: `TestReports/CoverageReport/index.html`
- Cobertura XML: `TestReports/CoverageReport/Cobertura.xml`
- Text summary: `TestReports/CoverageReport/Summary.txt`

Coverage summary (from `Summary.txt`):
- Line coverage: **64.4%** (2460/3814)
- Branch coverage: **55.9%** (654/1168)
- Method coverage: **81.6%** (575/704)
- Full method coverage: **76.7%** (540/704)

Notable class-level highlights:
- `UndercutAnalyser.MainWindow`: 0% (UI orchestration code not directly unit-tested)
- `UndercutAnalyser.RawDataView`: 88.6%
- `UndercutAnalyser.Services.ScanWorkflowService`: 99.6%
- `UndercutAnalyser.Services.RaceTraceWorkflowService`: 80.2%

## 5) Dependency and Package Audit

Commands run:
- `dotnet list UndercutAnalyser.sln package --vulnerable --include-transitive`
- `dotnet list UndercutAnalyser.sln package --outdated --include-transitive`

Vulnerability result:
- No vulnerable packages detected for `UndercutAnalyser` or `UndercutAnalyser.Tests`.

Outdated package result:
- Outdated top-level test packages include:
  - `coverlet.collector` 6.0.4 -> 10.0.1
  - `Microsoft.NET.Test.Sdk` 17.14.1 -> 18.8.1
  - `xunit.runner.visualstudio` 3.1.4 -> 3.1.5
- Multiple outdated transitive packages detected (notably SkiaSharp/HarfBuzz families and test platform transitive dependencies).

## 6) Code Audit Notes (Current Snapshot)

- Recent race-trace interaction logic was centralized into helper methods and accompanied by expanded regression tests.
- Current build/test status indicates no immediate functional regressions in covered paths.
- Raw-data border/null handling fixes are included and test suite remains green.

## 7) Recommended Next Actions (Informational)

1. Address nullable warning `CS8629` in `ScanWorkflow.Candidates.cs`.
2. Consider phased package updates for:
   - Test stack (`Microsoft.NET.Test.Sdk`, `coverlet.collector`, `xunit.runner.visualstudio`)
   - Graphics stack transitive families (SkiaSharp/HarfBuzz/OpenTK)
3. Increase coverage for UI orchestration (`MainWindow`) through additional extraction of pure logic into service helpers for unit testing.
4. Keep using generated coverage artifacts as baseline for future deltas.

---

## Reproducibility Commands

From `new-code/UndercutAnalyser`:

```powershell
dotnet restore UndercutAnalyser.sln
dotnet build UndercutAnalyser.sln -c Debug -v minimal
dotnet test UndercutAnalyser.sln -v minimal
dotnet test UndercutAnalyser.sln --collect:"XPlat Code Coverage" --results-directory "TestReports/CoverageRaw" -v minimal
dotnet tool install dotnet-reportgenerator-globaltool --tool-path .tools
.\.tools\reportgenerator.exe -reports:"TestReports\CoverageRaw\**\coverage.cobertura.xml" -targetdir:"TestReports\CoverageReport" -reporttypes:"Html;Cobertura;TextSummary"
dotnet list UndercutAnalyser.sln package --vulnerable --include-transitive
dotnet list UndercutAnalyser.sln package --outdated --include-transitive
```