# Copilot Instructions

## Project Guidelines
- ASSISTANT_MEMORY: On every new thread/context, parse and analyze all files in the AgentInformation folder at repo root 'C:\Users\harry\Documents\Motorsport Engineering\9_Thesis\msc-diss-report\AgentInformation' so the agent is aware of project state. Append future agent log entries to 'AgentInformation/agent_work_log.json' (not the repository root) and incrementally update the execution log as work proceeds.

## Agent Log Readme
This repository-level agent work log records every substantive change or investigation performed by the autonomous agent(s).

Purpose
- Provide a machine- and human-readable record of what the agent did, why, and where to find the changes.
- Support reproducibility and audit for the MSc dissertation engineering tool.

Primary log file
- agent_work_log.json (root)

Schema (agent_work_log.json entries array)
- id: string (unique, e.g., step-20260805-001)
- timestamp: ISO-8601 string
- author: string (agent id, e.g., "GitHub Copilot")
- summary: short description of the change/action
- rationale: why the action was taken
- filesChanged: array of file paths modified or created
- commandsRun: array of terminal commands run (if any)
- testsRun: array of test identifiers and results (if any)
- buildResult: brief build status (if applicable)
- references: array of relevant file paths (e.g., the context brief)
- notes: freeform observations

How the agent will use this file
- Append a new entry object to the entries array for each future work step.
- Keep entries concise and factual.
- Update buildResult and testsRun when relevant.

Example entry
{
  "id": "step-20260805-001",
  "timestamp": "2026-08-05T00:00:00Z",
  "author": "GitHub Copilot",
  "summary": "Read Undercut_Tool_Agent_Context.md and created logging artifacts",
  "rationale": "User requested the agent to record future work in a parseable document",
  "filesChanged": ["agent_work_log.json", "AGENT_WORK_LOG_README.md"],
  "commandsRun": [],
  "testsRun": [],
  "buildResult": null,
  "references": ["new-code/UndercutAnalyser/Undercut_Tool_Agent_Context.md"],
  "notes": "Initial log created. Agent will append future entries."
}

Addendum
- Humans may edit the README to change the schema version; the agent will preserve existing entries.
- If the repository already has a different agent log, notify maintainers before duplicating.

# Formula One Undercut Prediction Tool
## Complete Context and Build Brief for an Engineering Software Agent

**Project owner:** Harry Adams  
**Academic context:** MSc Advanced Motorsport Engineering dissertation  
**Preferred implementation stack:** C# / .NET 8 / WPF  
**Primary purpose:** Build a transparent, deterministic engineering tool that predicts whether an attacking Formula One driver could successfully undercut the driver ahead if they pit at the current decision point.

---

# 1. How to use this document

This document is the primary context brief for an autonomous software agent.

The agent should treat it as:

- the functional specification for the application;
- the current modelling intent;
- a set of architectural constraints;
- a guide to terminology and assumptions;
- a list of known uncertainties that must not be silently invented away;
- the baseline for implementation and test planning.

The project is part of an engineering dissertation. The software must therefore be:

- understandable;
- deterministic;
- reproducible;
- inspectable;
- easy to validate;
- clearly separated into model, data, presentation, and infrastructure concerns.

Do not optimise for cleverness. Optimise for transparency and traceability.

The tool is not intended to reproduce every detail of a Formula One race. It is intended to answer a focused question:

> If the attacking driver pits now, and the target driver responds after a configurable number of laps, what is the predicted relative gap after the pit sequence, and is the attacking driver predicted to emerge ahead?

---

# 2. Core conceptual model

## 2.1 What the tool is

The tool is a **deterministic undercut prediction tool**.

It begins from a known race state at a decision point and predicts one hypothetical future:

1. The attacking driver commits to a pit stop.
2. The target driver remains on track for a configurable number of laps.
3. The target driver then pits.
4. Both drivers are predicted lap by lap using the same deterministic lap time model.
5. Their cumulative race times are compared.
6. The tool outputs the predicted relative gap and whether the undercut is successful.

The model should behave as if it were being used live.

Historic race data may be used to initialise the race state and later validate the prediction, but future historic information must not leak into the prediction.

## 2.2 What the tool is not

It is not:

- a complete race simulator;
- a stochastic strategy optimiser;
- a machine learning model;
- a live traffic simulator;
- an overtaking simulator;
- a universal tyre model;
- a tool that compares the attacking driver pitting now against the attacking driver staying out;
- a tool that relies on what actually happened after the chosen historic decision point.

The historic race is the source of the state at the selected decision point, not the source of the predicted future.

## 2.3 Important modelling distinction

There are two nested models:

### Inner model: lap time prediction

Predicts one lap for one driver.

### Outer model: race prediction

Applies the lap time model repeatedly to both drivers throughout the pit sequence and determines the resulting relative gap.

The lap time model is a component of the undercut prediction model. It is not the complete contribution.

---

# 3. Domain terminology

Use the following terminology consistently.

## 3.1 Attacking driver

The driver considering the earlier pit stop in an attempt to gain track position.

## 3.2 Target driver

The driver ahead whose track position is being challenged.

Do not call this driver the “defending driver” unless discussing active on-track defence. “Target driver” is the stable model term.

## 3.3 Decision point

The point at which the model state is sampled and the undercut decision is evaluated.

The current dissertation treatment places this at Sector Line Two on lap \(n\), because:

- the latest complete timing information is available;
- the driver still has time to prepare for pit entry;
- the team still has time to prepare the tyre set and pit crew;
- the pit entry line may require an altered line before the driver physically leaves the racing surface.

This choice is particularly illustrated using the Red Bull Ring, but the software should not hard-code Austria-specific geometry.

## 3.4 Pit lap

The lap to which the complete pit lane time loss is assigned.

For the attacking driver, the pit lap is lap \(n\).

For a one-lap target response, the target driver’s pit lap is lap \(n+1\).

## 3.5 Out lap

The first lap following the pit stop on the replacement tyre.

For the attacking driver, this is lap \(n+1\).

For a one-lap target response, this is lap \(n+2\).

## 3.6 Target response delay

The number of laps between the attacking driver’s stop and the target driver’s stop.

Suggested notation:

- \(r = 1\): target responds on the following lap;
- \(r = 2\): target remains out for two laps;
- and so on.

Do not use \(n\) for both the decision lap and target response count. The dissertation has used \(n\) as the decision lap index, so the implementation should use a clearly different name, such as `TargetResponseLaps`.

## 3.7 Pit sequence

The predicted sequence beginning at the decision point and containing:

- the attacking driver’s pit lap;
- the attacking driver’s out lap;
- any target stay-out laps;
- the target driver’s pit lap;
- the target driver’s out lap;
- optionally, one additional normal lap for both drivers.

## 3.8 Core pit sequence endpoint

The end of the target driver’s out lap.

For a one-lap target response:

- attacker pits on \(n\);
- attacker out lap on \(n+1\);
- target pits on \(n+1\);
- target out lap on \(n+2\);
- core pit sequence ends at the end of \(n+2\).

## 3.9 Stabilised comparison endpoint

An optional additional comparison at the end of the next lap, when both cars have completed a normal lap.

For a one-lap target response, this is the end of \(n+3\).

This output is useful because the core endpoint is asymmetric:

- attacking driver is on a normal lap;
- target driver is on an out lap.

At the stabilised endpoint, both are on normal laps.

## 3.10 Prediction window

The interval over which the model predicts cumulative race time.

The tool should support at least two outputs:

1. **Gap after pit sequence** — at the end of the target’s out lap.
2. **Gap after stabilisation** — after one additional normal lap for both drivers.

The first is the primary undercut result. The second is a supporting output.

---

# 4. Mathematical model

## 4.1 Minimum lap time model

The introductory lap time model is:

\[
L = B + C + D(a)
\]

Where:

- \(L\): predicted lap time;
- \(B\): driver reference pace;
- \(C\): tyre compound offset;
- \(D(a)\): tyre degradation loss as a function of tyre age \(a\).

The implementation may use indexed internal types, but the conceptual model should remain simple and inspectable.

## 4.2 Linear degradation

The initial implementation should use a locally linear degradation approximation:

\[
D(a) = k a
\]

Where:

- \(k\): degradation rate in seconds per lap;
- \(a\): current tyre age in laps.

A used replacement tyre does not require a separate model term. It changes the initial tyre age.

Examples:

- new replacement set: initial age \(a_0 = 0\);
- used replacement set: initial age \(a_0 > 0\).

Do not create a separate “used tyre effect” unless later work explicitly models heat cycles, altered warm up, or a changed degradation curve.

## 4.3 Warm up effect

The lap time model is extended to include a warm up loss:

\[
L = B + C + D(a) + W(i)
\]

Where:

- \(W(i)\): warm up penalty on lap \(i\) following a stop.

The first implementation may use a simple configurable representation, such as:

- a single out-lap penalty;
- a list of penalties by lap after fitting;
- or a short decay sequence.

Keep the model deterministic and user configurable.

## 4.4 Traffic effect

The model includes a flat conditional traffic penalty:

\[
L = B + C + D(a) + W(i) + R(i)
\]

Where:

- \(R(i)=0\) if no traffic penalty applies;
- \(R(i)>0\) if traffic is assumed.

Traffic is an external race effect, not an intrinsic property of the car, tyre, or driver.

The current intended implementation does not attempt to predict traffic from first principles. It predicts the effect if traffic is assumed.

The software may separately support user-configurable feasibility thresholds:

- minimum acceptable gap ahead;
- minimum acceptable gap behind;
- minimum clear rejoin gap for an undercut proposal.

Those thresholds influence the decision logic or validity of an opportunity. They are not part of the lap time equation.

## 4.5 Pit lane loss

The full pit lane time loss should be assigned to the pit lap.

This is a modelling convention adopted because the decision point occurs before pit entry and there is no clean, general way to split pit lane loss across the finish line.

The implementation should preserve total elapsed time and document the convention clearly.

A pit lap therefore becomes approximately:

\[
L_{\text{pit}} = L_{\text{normal}} + P
\]

Where:

- \(P\): pit lane time loss.

Avoid double counting any in-lap effect. Decide whether the reference lap already represents the compromised pit entry line or whether this is included in \(P\).

For the first implementation, the simplest transparent option is:

- normal predicted lap time;
- plus full configurable pit loss;
- assigned to the pit lap.

## 4.6 Complete lap model

A practical first complete form is:

\[
L = B + C + D(a) + W(i) + R(i) + P(i)
\]

Where \(P(i)\) is non-zero only on a pit lap.

Internal implementation should model terms separately and return a breakdown, not only the final total.

Example breakdown:
Reference pace:        90.000 s
Compound offset:       +0.300 s
Degradation:           +0.800 s
Warm up:               +1.500 s
Traffic:               +0.000 s
Pit lane loss:         +20.500 s
--------------------------------
Predicted lap time:   113.100 s
This traceability is academically valuable and useful for debugging.

---

# 5. Relative gap formulation

## 5.1 Sign convention

The user strongly prefers the normal motorsport convention:

> Negative time deltas are better / faster.

The application must state its sign convention explicitly and use it consistently.

A recommended gap convention is:

\[
G = T_{\text{attacker}} - T_{\text{target}}
\]

Where:

- \(G < 0\): attacking driver is ahead;
- \(G = 0\): drivers are level;
- \(G > 0\): attacking driver is behind.

This is intuitive because a lower cumulative time is better.

Alternatively, an explicit “attacker gap to target” can be used with the same meaning.

Do not mix conventions between UI labels, equations, and tests.

## 5.2 Initial gap

The prediction starts with an initial relative gap at the decision point.

The race state must include enough information to calculate or directly provide:

- attacker cumulative race time;
- target cumulative race time;
- or attacker gap to target.

The initial gap is then updated by the difference in their predicted cumulative times.

A conceptual formulation is:

\[
G_j = G_0 + T_{\text{att},j} - T_{\text{tar},j}
\]

Where:

- \(G_0\): initial attacker-to-target gap;
- \(T_{\text{att},j}\): attacking driver’s predicted elapsed time over the window to evaluation point \(j\);
- \(T_{\text{tar},j}\): target driver’s predicted elapsed time over the same interval;
- \(G_j\): predicted relative gap at evaluation point \(j\).

## 5.3 Required evaluation outputs

At minimum calculate:

### Core gap

`GapAfterPitSequence`

At the end of the target’s out lap.

### Stabilised gap

`GapAfterNormalLap`

At the end of one additional normal lap for both drivers.

## 5.4 Undercut success

The primary binary success criterion should be based on the core gap:
GapAfterPitSequence < 0
This means the attacker is predicted to be ahead after the target completes its stop.

However, the tool should also expose the stabilised gap because:

- it removes the out-lap asymmetry;
- it shows whether the advantage persists;
- it may support engineering interpretation on circuits where track position and overtaking difficulty matter.

The first version should not claim to model overtaking difficulty. It may display the information and leave interpretation to the user.

Potential future extension:

- circuit-specific minimum required gap;
- overtaking difficulty category;
- probability of completing a pass.

Do not implement these as facts without a supported model.

---

# 6. Prediction sequence

## 6.1 One-lap response example

For decision lap \(n\) with `TargetResponseLaps = 1`:

| Lap | Attacking driver | Target driver | Comparison |
|---|---|---|---|
| \(n\) | Pit lap, includes pit loss | Standard reference lap | — |
| \(n+1\) | Out lap, includes warm up and optional traffic | Pit lap, includes pit loss | **Primary** `GapAtN1Seconds` |
| \(n+2\) | Standard predicted lap | Out lap, includes warm up and optional traffic | Secondary `GapAtN2Seconds` |
| \(n+3\) | Standard predicted lap | Standard predicted lap | Tertiary `GapAtN3Seconds` |

## 6.2 Generalised response delay

For `TargetResponseLaps = r`:

- attacker pits on lap \(n\);
- target pits on lap \(n+r\);
- target out lap is \(n+r+1\);
- primary endpoint is end of \(n+r+1\) → `GapAtN1Seconds`;
- secondary endpoint is end of \(n+r+2\) → `GapAtN2Seconds`;
- tertiary endpoint is end of \(n+r+3\) → `GapAtN3Seconds`.

The naming `GapAtN1`, `GapAtN2`, `GapAtN3` refers to the number of laps after the attacker's out lap completes, not absolute lap numbers.

The implementation should not hard-code a one-lap response.

## 6.3 Driver state progression

For each predicted lap, update:

- tyre age;
- tyre compound;
- laps since pit;
- whether the lap is a pit lap;
- whether the lap is an out lap;
- warm up state;
- traffic assumption;
- cumulative elapsed time.

The prediction should be side-effect free where practical: use immutable or copied state objects for each lap.

---

# 7. Historic data use

## 7.1 Data source

The project has planned to use FastF1 as the primary historic data source.

OpenF1 has been considered for meeting/session mapping and live data, but live prediction is not required for the dissertation implementation.

The architecture should isolate external data access behind interfaces so the model can run without an API.

## 7.2 Historic data role

Historic data should initialise:

- event;
- session;
- current lap;
- driver identities;
- current positions;
- gap between drivers;
- current compounds;
- current tyre ages;
- reference pace estimates;
- relevant recent lap or sector information.

Historic future laps must not be read by the prediction engine.

## 7.3 Validation role

Observed historic future data can be used only after prediction, in a separate validation flow.

Example validation process:

1. Select a known historic decision point.
2. Build the model state using only data available at that point.
3. Run the prediction.
4. Retrieve what actually happened after the point.
5. Compare predicted and observed gap or outcome.
6. Record error and whether the predicted success classification matched reality.

Keep prediction and validation code clearly separated to prevent data leakage.

## 7.4 Data quality constraints

Expect:

- missing sector times;
- deleted laps;
- safety car laps;
- pit entry and pit exit anomalies;
- inconsistent tyre age metadata;
- null compounds;
- lapped cars;
- timing gaps relative to different references;
- sprint sessions;
- changing API schemas.

The agent should build null-safe data mapping and explicit eligibility checks.

---

# 8. Proposed solution architecture

Use a clean, layered .NET solution.

Suggested structure:
UndercutTool.sln

src/
  UndercutTool.Domain/
  UndercutTool.Application/
  UndercutTool.Infrastructure/
  UndercutTool.Wpf/

tests/
  UndercutTool.Domain.Tests/
  UndercutTool.Application.Tests/
  UndercutTool.Infrastructure.Tests/
## 8.1 Domain project

Contains pure engineering concepts and calculations.

Suggested content:

- entities;
- value objects;
- model parameters;
- lap prediction engine;
- pit sequence engine;
- relative gap calculation;
- validation result types;
- no WPF;
- no HTTP;
- no filesystem;
- no database.

## 8.2 Application project

Coordinates use cases.

Suggested use cases:

- load meetings;
- load sessions;
- load race state;
- select attacker and target;
- create prediction request;
- run prediction;
- run validation;
- export result.

## 8.3 Infrastructure project

Contains:

- FastF1 integration;
- OpenF1 integration if retained;
- Python process bridge if FastF1 is accessed through Python;
- JSON persistence;
- settings storage;
- caching;
- external API DTOs;
- logging.

## 8.4 WPF project

Contains:

- views;
- view models;
- commands;
- validation;
- charts;
- parameter editors;
- result visualisation.

Use MVVM. Avoid model logic in code-behind.

---

# 9. Suggested domain types

## 9.1 DriverId
public readonly record struct DriverId(string Value);
## 9.2 TyreCompound
public enum TyreCompound
{
    Soft,
    Medium,
    Hard,
    Intermediate,
    Wet,
    Unknown
}
For the first implementation, dry compounds may be the only valid prediction compounds. Wet races can be explicitly unsupported.

## 9.3 DriverRaceState
public sealed record DriverRaceState(
    DriverId DriverId,
    string Acronym,
    int Position,
    double CumulativeRaceTimeSeconds,
    TyreCompound Compound,
    int TyreAgeLaps,
    double ReferencePaceSeconds);  // Driver-specific baseline; auto-derived from clean historic laps, user-overridable

// Note: DegradationRateSecondsPerLap is NOT per-driver. It is per-compound and lives in
// LapModelParameters.DegradationRatesSeconds (keyed by TyreCompound). Both drivers share
// the same compound degradation rates from the model parameters.
May also include:

- team;
- colour;
- lap number;
- most recent sectors;
- gap to car ahead;
- pit status.

## 9.4 RaceDecisionState
public sealed record RaceDecisionState(
    string EventName,
    string SessionName,
    int DecisionLap,
    double InitialAttackerGapToTargetSeconds,
    DriverRaceState Attacker,
    DriverRaceState Target);
## 9.5 TyreSetSpecification
public sealed record TyreSetSpecification(
    TyreCompound Compound,
    int InitialAgeLaps);
## 9.6 LapModelParameters
public sealed record LapModelParameters(
    IReadOnlyDictionary<TyreCompound, double> CompoundOffsetsSeconds,
    IReadOnlyDictionary<TyreCompound, double> DegradationRatesSecondsPerLap,
    WarmUpModelParameters WarmUp,
    double PitLaneLossSeconds,
    double MarginalThresholdSeconds,   // Default 0.25 s
    TrafficModelParameters Traffic);

Reference compound convention:

- Soft compound offset = 0 seconds;
- other compounds relative to Soft.

Degradation rates are per-compound, not per-driver. Both the attacker and target use the same rate for a given compound.

**Fuel effect is excluded from LapModelParameters and from the prediction engine.** The dissertation explicitly excludes it on the grounds that both drivers burn fuel at equal rates over the short comparison window. Fuel correction may be applied to the race trace visualisation for display purposes but must not enter the prediction model.

## 9.7 WarmUpModelParameters

A single flat penalty applied to the first out lap only (the dissertation base model):
public sealed record WarmUpModelParameters(
    double OutLapPenaltySeconds);
This is the penalty W in the lap time equation. It applies only to the out lap; subsequent laps are unaffected. A per-lap list is a future extension if non-linear warm-up is needed.

## 9.8 TrafficModelParameters
public sealed record TrafficModelParameters(
    bool ApplyAttackerTrafficPenalty,
    bool ApplyTargetTrafficPenalty,
    double PenaltySecondsPerAffectedLap,
    double MinimumGapAheadSeconds,
    double MinimumGapBehindSeconds);
The UI should make clear which values affect lap time and which affect feasibility.

A more explicit split may be better:
public sealed record TrafficPenaltyParameters(...);
public sealed record RejoinFeasibilityParameters(...);
## 9.9 PredictionRequest
public sealed record PredictionRequest(
    RaceDecisionState RaceState,
    TyreSetSpecification AttackerReplacementTyre,
    TyreSetSpecification TargetReplacementTyre,
    int TargetResponseLaps,
    LapModelParameters ModelParameters);

// All three comparison gaps are always calculated. There is no IncludeStabilisedLap flag.
## 9.10 LapPredictionBreakdown
public sealed record LapPredictionBreakdown(
    double ReferencePaceSeconds,
    double CompoundOffsetSeconds,
    double DegradationSeconds,
    double WarmUpSeconds,
    double TrafficSeconds,
    double PitLossSeconds)
{
    public double TotalSeconds =>
        ReferencePaceSeconds +
        CompoundOffsetSeconds +
        DegradationSeconds +
        WarmUpSeconds +
        TrafficSeconds +
         PitLossSeconds;
}
## 9.11 PredictedLap
public sealed record PredictedLap(
    int LapNumber,
    DriverId DriverId,
    bool IsPitLap,
    bool IsOutLap,
    TyreCompound Compound,
    int TyreAgeAtStart,
    LapPredictionBreakdown Breakdown,
    double CumulativePredictionTimeSeconds);
## 9.12 PredictionResult
public sealed record PredictionResult(
    IReadOnlyList<PredictedLap> AttackerLaps,
    IReadOnlyList<PredictedLap> TargetLaps,
    double GapAtN1Seconds,          // Primary: attacker out lap done, target just pitted
    double GapAtN2Seconds,          // Secondary: target out lap done
    double GapAtN3Seconds,          // Tertiary: both on first normal lap
    UndercutClassification Classification,  // Ahead / Marginal / Behind based on GapAtN1
    double MarginalThresholdSeconds,        // Default 0.25 s, configurable
    IReadOnlyList<PredictionWarning> Warnings);

public enum UndercutClassification
{
    PredictedAhead,
    PredictedMarginal,
    PredictedBehind
}
---

# 10. Core services

## 10.1 ILapTimePredictor
public interface ILapTimePredictor
{
    PredictedLap Predict(LapPredictionInput input);
}
Responsibilities:

- calculate each term;
- return a breakdown;
- contain no race sequence logic.

## 10.2 IPitSequencePredictor
public interface IPitSequencePredictor
{
    PredictionResult Predict(PredictionRequest request);
}
Responsibilities:

- create the lap sequence;
- maintain separate driver states;
- call the lap predictor;
- accumulate time;
- calculate evaluation gaps.

## 10.3 IHistoricRaceDataProvider
public interface IHistoricRaceDataProvider
{
    Task<IReadOnlyList<MeetingSummary>> GetMeetingsAsync(...);
    Task<IReadOnlyList<SessionSummary>> GetSessionsAsync(...);
    Task<RaceDecisionState> GetDecisionStateAsync(...);
}
## 10.4 IValidationService
public interface IValidationService
{
    Task<ValidationResult> ValidateAsync(
        PredictionRequest request,
        PredictionResult prediction,
        CancellationToken cancellationToken);
}
Validation must not be called by the predictor.

---

# 11. Prediction engine algorithm

## 11.1 Inputs

Validate:

- attacker is not the race leader;
- target is ahead of attacker;
- target response laps is at least 1;
- pit loss is non-negative;
- tyre ages are non-negative;
- reference pace is positive;
- degradation rates are plausible;
- required compounds have offsets;
- no unsupported safety car state unless deliberately allowed.

## 11.2 Sequence construction

Pseudo-code:
decisionLap = n
targetPitLap = n + targetResponseLaps
targetOutLap = targetPitLap + 1
primaryEndLap   = n + 1              // attacker out lap done, target just pitted
secondaryEndLap = targetOutLap       // target out lap done
tertiaryEndLap  = targetOutLap + 1  // both on first normal lap

for lap from n to tertiaryEndLap:
    predict attacker lap:
        if lap == n:
            pit lap (old tyre + full pit loss)
        else if lap == n + 1:
            out lap (new tyre, age = replacement age, warm up penalty W)
        else:
            standard lap

    predict target lap:
        if lap == targetPitLap:
            pit lap (old tyre + full pit loss)
        else if lap == targetOutLap:
            out lap (new tyre, age = replacement age, warm up penalty W)
        else:
            standard lap

    update cumulative times
    update tyre age/state

    if lap == primaryEndLap:
        calculate GapAtN1 = G0 + attackerElapsed - targetElapsed

    if lap == secondaryEndLap:
        calculate GapAtN2

    if lap == tertiaryEndLap:
        calculate GapAtN3
        derive Classification from GapAtN1 and marginalThreshold
## 11.3 Tyre age progression

Clarify whether tyre age represents age at lap start or lap end.

Recommended:

- `TyreAgeAtStart`;
- degradation is calculated from age at lap start;
- age increments after completing the lap.

On pit lap:

- the driver completes the lap on the old tyre;
- pit loss is added;
- replacement tyre becomes active for the next lap.

On out lap:

- new tyre age at start is the specified replacement age;
- after the lap it increments by one.

Document this carefully to avoid off-by-one errors.

## 11.4 Pit lap tyre state

The pit lap should use the old tyre degradation state because the car drives most of the racing lap before changing tyres.

Replacement tyre state begins on the out lap.

## 11.5 Relative gap

At each evaluation point:
predictedGap =
    initialAttackerGapToTarget
    + attackerPredictedElapsed
    - targetPredictedElapsed
With the chosen sign convention:

- negative = attacker ahead;
- positive = attacker behind.

---

# 12. Reference pace

Reference pace requires a clear strategy.

Possible options:

1. User entered directly.
2. Derived from recent clean laps.
3. Derived from sector data.
4. Derived from a fitted regression.

For the first working version, user-configurable reference pace is acceptable and supports deterministic validation.

A later derived mode can be added behind an interface.

Do not silently mix observed lap time and modelled effects. If reference pace is derived from a lap that already includes degradation and compound effect, subtract or clearly define what the parameter represents.

The software should expose the source of each parameter:

- historic data;
- derived;
- user configured;
- default.

---

# 13. User interface

## 13.1 Main workflow

Recommended layout:

### Step 1: Select historic context

- season;
- meeting;
- session;
- lap;
- attacking driver;
- target driver.

### Step 2: Review decision state

Display:

- positions;
- initial gap;
- compounds;
- tyre ages;
- reference pace;
- latest valid laps/sectors;
- any data warnings.

### Step 3: Configure prediction

Inputs:

- attacker replacement compound;
- attacker replacement tyre age;
- target replacement compound;
- target replacement tyre age;
- target response laps;
- degradation rates;
- compound offsets;
- warm up penalties;
- pit loss;
- traffic penalties;
- rejoin feasibility thresholds;
- include stabilised comparison.

### Step 4: Run prediction

Prominent action:
Predict Undercut
### Step 5: Review result

Primary cards:

- gap after target out lap;
- attacker ahead/behind;
- undercut successful;
- gap after both normal laps.

Supporting displays:

- lap-by-lap table;
- term breakdown;
- timeline;
- cumulative race trace;
- warnings and assumptions.

## 13.2 Race trace

The project has previously planned a race trace visual.

Recommended display:

- x-axis: lap;
- y-axis: cumulative time relative to a chosen reference;
- attacker and target lines;
- pit lap markers;
- out lap markers;
- core endpoint marker;
- stabilised endpoint marker.

Do not rely on colour alone. Use labels, symbols, or line patterns.

## 13.3 Parameter provenance

Each parameter should show:

- value;
- unit;
- source;
- whether editable.

Example:
Attacker degradation: 0.085 s/lap
Source: user configured
## 13.4 Validation state

Disable the Predict button when inputs are invalid and show precise errors.

Avoid modal error dialogs for ordinary input validation.

---

# 14. WPF implementation guidance

## 14.1 MVVM

Use:

- `INotifyPropertyChanged` or CommunityToolkit.Mvvm;
- commands for load/predict/export;
- dependency injection through `Microsoft.Extensions.DependencyInjection`;
- async loading with cancellation.

CommunityToolkit.Mvvm is appropriate.

## 14.2 Chart library

Prefer ScottPlot for WPF charting (free, open source, actively maintained) for race-trace visualisations.

## 14.3 Navigation

A single window with a staged workspace is preferable to many modal windows.

Possible tabs:

- Race State;
- Model Parameters;
- Prediction;
- Validation.

## 14.4 Configuration persistence

Persist user settings to JSON:

- compound offsets;
- default degradation rates;
- pit losses by circuit;
- warm up profiles;
- traffic thresholds.

Use versioned settings DTOs.

---

# 15. FastF1 integration options

FastF1 is Python based, while the preferred application stack is C#.

Possible integration approaches:

## Option A: Python preprocessing script

A Python script uses FastF1 and exports JSON consumed by the C# app.

Advantages:

- simple;
- easy to debug;
- FastF1 used natively;
- suitable for dissertation.

Disadvantages:

- external Python dependency;
- process invocation.

## Option B: Local Python service

A small FastAPI service wraps FastF1.

Advantages:

- clean HTTP contract;
- easy caching;
- separation of concerns.

Disadvantages:

- more deployment complexity.

## Option C: Build directly against Ergast/OpenF1-style APIs

Avoids Python but may not expose all required historic tyre data and timing.

Recommended initial choice:

> Python preprocessing or a thin Python bridge with a stable JSON contract.

The C# model engine must be runnable against hand-authored JSON fixtures with no Python process.

---

# 16. Suggested JSON contract

Example:
{
  "eventName": "Austrian Grand Prix",
  "season": 2024,
  "session": "Race",
  "decisionLap": 20,
  "decisionSector": 2,
  "attacker": {
    "driverCode": "NOR",
    "position": 4,
    "cumulativeRaceTimeSeconds": 1850.2,
    "compound": "MEDIUM",
    "tyreAgeLaps": 18,
    "referencePaceSeconds": 68.8,
    "degradationRateSecondsPerLap": 0.09
  },
  "target": {
    "driverCode": "HAM",
    "position": 3,
    "cumulativeRaceTimeSeconds": 1848.7,
    "compound": "MEDIUM",
    "tyreAgeLaps": 17,
    "referencePaceSeconds": 68.9,
    "degradationRateSecondsPerLap": 0.08
  },
  "initialAttackerGapToTargetSeconds": 1.5
}
Use explicit units in property names where practical.

---

# 17. Testing strategy

## 17.1 Unit tests

Test every model term separately.

### Minimum model

- no degradation;
- compound offset;
- degradation at age zero;
- degradation at positive age.

### Warm up

- out lap penalty;
- first normal lap penalty;
- no penalty after warm up expires.

### Traffic

- no traffic;
- attacker traffic;
- target traffic;
- different affected laps.

### Pit loss

- non-pit lap;
- pit lap;
- applied exactly once.

### Used tyre

- replacement age zero;
- replacement age positive.

## 17.2 Sequence tests

### One-lap response

Verify classifications:

| Lap | Attacker | Target |
|---|---|---|
| n | Pit | Normal |
| n+1 | Out | Pit |
| n+2 | Normal | Out |
| n+3 | Normal | Normal |

### Two-lap response

Verify:

| Lap | Attacker | Target |
|---|---|---|
| n | Pit | Normal |
| n+1 | Out | Normal |
| n+2 | Normal | Pit |
| n+3 | Normal | Out |
| n+4 | Normal | Normal |

## 17.3 Gap tests

- equal initial gap and equal elapsed time;
- attacker gains enough to pass;
- attacker narrowly fails;
- negative/positive sign convention;
- core success but stabilised loss;
- core failure but stabilised gain.

That last case must be allowed as an output, not forced into one binary narrative.

## 17.4 Golden test fixtures

Create small human-calculable fixtures.

Example:
Initial gap: +2.0 s
Attacker elapsed: 200.0 s
Target elapsed: 203.0 s
Predicted gap: -1.0 s
Result: attacker ahead
## 17.5 Data adapter tests

Use stored JSON fixtures, not live internet calls, for repeatable tests.

## 17.6 Validation tests

Ensure observed future data is never accessed before prediction completes.

One way is to use separate interfaces and separate object graphs.

---

# 18. Model exclusions

The following have been considered but are initially excluded.

## 18.1 Fuel mass effect

Excluded because:

- both drivers burn fuel over the short prediction window;
- the relative effect is assumed similar;
- competitor starting fuel is unknown;
- the short local window makes cancellation a reasonable approximation.

## 18.2 Track evolution

Excluded because:

- difficult to quantify;
- assumed to affect both drivers similarly over the short window;
- more important over longer periods.

## 18.3 Non-linear degradation

Initial implementation uses local linear degradation because:

- the prediction window is short;
- a linear approximation is transparent;
- fitting a more complex curve requires more data;
- most undercut decisions are not made during the immediate peak tyre phase.

## 18.4 Dirty air before the stop

Not separately modelled unless represented through the traffic term.

## 18.5 Driver variability

No stochastic pace variation or uncertainty bands in the first version.

## 18.6 Safety Car and Virtual Safety Car

Exclude or mark the decision point ineligible because neutralisations fundamentally alter:

- pit loss;
- gaps;
- traffic;
- strategic behaviour.

## 18.7 Overtaking simulation

The model predicts relative time and track position at evaluation points. It does not predict whether a pass can be completed or defended.

The stabilised gap output is supporting information, not an overtaking probability.

---

# 19. Logging and diagnostics

Use structured logging.

Recommended events:

- race state loaded;
- parameters resolved;
- prediction started;
- lap predicted;
- core gap calculated;
- stabilised gap calculated;
- validation completed;
- warning generated;
- external data failure.

For each predicted lap, optionally log the model breakdown at debug level.

Do not log excessive raw timing data at information level.

---

# 20. Export and reproducibility

The tool should support exporting a prediction to JSON or CSV.

A prediction export should contain:

- event and session;
- decision point;
- driver states;
- all user parameters;
- model version;
- per-lap breakdown;
- outputs;
- warnings;
- timestamp;
- optional observed validation result.

This allows dissertation results to be reproduced.

Add a `ModelVersion` string or semantic version.

---

# 21. Suggested implementation milestones

## Milestone 1: Pure model prototype

Deliver:

- domain project;
- lap time predictor;
- pit sequence predictor;
- core and stabilised gaps;
- comprehensive unit tests;
- manual fixture inputs.

No UI and no external API.

## Milestone 2: Basic WPF shell

Deliver:

- parameter entry;
- attacker and target state entry;
- prediction button;
- result cards;
- lap table.

## Milestone 3: Historic data bridge

Deliver:

- FastF1 JSON extraction;
- meeting/session/lap selectors;
- decision state loading;
- caching;
- error handling.

## Milestone 4: Visualisation

Deliver:

- timeline;
- cumulative trace;
- parameter breakdown;
- warnings.

## Milestone 5: Validation workflow

Deliver:

- known historic cases;
- observed outcome comparison;
- error metrics;
- export.

## Milestone 6: Dissertation hardening

Deliver:

- screenshots;
- reproducible fixtures;
- architecture diagram;
- model versioning;
- documented assumptions;
- no known data leakage.

---

# 22. Acceptance criteria for the first complete version

The tool is acceptable when:

1. A user can define or load a race decision state.
2. A user can select an attacking and target driver.
3. The race leader cannot be selected as an attacker without a target.
4. A user can configure replacement compounds and tyre ages.
5. A user can configure target response delay.
6. A user can configure degradation, compound offsets, warm up, traffic, and pit loss.
7. The tool predicts both drivers lap by lap.
8. Each lap exposes a complete term breakdown.
9. The tool outputs the gap after the target’s out lap.
10. The tool outputs the gap after one additional normal lap.
11. The sign convention is consistent.
12. The tool provides a binary undercut success result based on the core gap.
13. The tool never uses future historic data during prediction.
14. The same input always produces the same output.
15. Core domain tests pass without WPF or internet access.
16. Predictions can be exported for validation and dissertation evidence.
17. Invalid inputs produce clear errors rather than crashes.
18. External data failures do not corrupt the model state.

---

# 23. Agent working instructions

The software agent should work in small, reviewable increments.

For every meaningful implementation step:

1. Explain the intended change briefly.
2. Identify assumptions.
3. Implement domain logic before UI.
4. Add or update tests.
5. Run tests.
6. Report what passed and what remains uncertain.
7. Avoid silently choosing unresolved engineering parameters.
8. Keep the model free of WPF and API dependencies.
9. Preserve British English in user-facing text.
10. Avoid unnecessary hyphens in prose:
   - use “lap time”, “pit stop”, “warm up”;
   - technical identifiers and file names may remain hyphenated.

Do not:

- rewrite the model into a stochastic optimiser;
- introduce machine learning;
- add future historic information to make tests pass;
- bury assumptions in code;
- hard-code one circuit;
- hard-code a one-lap response;
- infer tyre behaviour not present in the configured model;
- add features without tests;
- conflate validation data with prediction inputs.

---

# 24. Open questions that require explicit decisions

The agent should surface these, not silently choose.

## 24.1 Reference pace derivation

Will it be:

- manually configured;
- based on recent laps;
- based on sector times;
- fitted automatically?

## 24.2 Compound offsets

Are offsets:

- global;
- circuit specific;
- driver specific;
- session specific?

Initial recommendation: user configurable per prediction, with saved defaults.

## 24.3 Degradation rate

Is degradation:

- one rate per driver and compound;
- one rate per compound;
- manually configured;
- estimated from prior laps?

Initial recommendation: user configurable per driver for transparency.

## 24.4 Warm up model

Single out-lap penalty or multi-lap sequence?

Initial recommendation: list by lap after stop.

## 24.5 Traffic assignment

How does the user indicate affected laps?

Possible UI:

- checkbox per predicted lap;
- attacker/target flat toggles;
- automatic from gap thresholds.

Initial recommendation: explicit per-lap flags or a simple attacker out-lap toggle.

## 24.6 Pit loss

Is pit loss:

- total stationary and lane loss;
- circuit default;
- user configured;
- derived from historic pit stops?

Initial recommendation: user configurable with optional circuit default.

## 24.7 Initial timing reference

Use:

- cumulative race time;
- gap at Sector Line Two;
- projected finish-line gap;
- latest available sector delta?

This is important because the decision point is mid-lap.

The implementation must be mathematically consistent about how the initial gap at Sector Line Two connects to lap-based predictions.

A robust approach may require:

- sector-level state at decision point;
- prediction of remaining Sector Three time for both drivers;
- then lap-based sequence.

If the dissertation simplification treats the decision point as the start of the decision lap for timing purposes, this must be stated explicitly.

## 24.8 Core success point

Current recommendation:

- primary success at end of target out lap;
- supporting stabilised gap after next normal lap.

Confirm before locking UI wording.

---

# 25. Important unresolved modelling concern: sector decision point versus lap model

The decision is made at Sector Line Two, but the prediction table currently labels the attacking driver’s pit stop loss as part of lap \(n\).

This creates a timing alignment question:

- The race state is sampled after Sectors One and Two of lap \(n\).
- Only Sector Three remains before the finish line and pit stop.
- A full-lap model cannot simply predict all of lap \(n\) from that point without double counting already completed sectors.

The agent must not ignore this.

Possible approaches:

## Approach A: sector-aware first partial lap

At the decision point:

- initialise cumulative gap at Sector Line Two;
- predict only Sector Three plus pit loss for the attacking driver;
- predict only Sector Three for the target;
- begin full-lap predictions from \(n+1\).

This is the most physically correct.

## Approach B: define the model decision point at the start of lap \(n\)

This simplifies the mathematics but conflicts with the operational explanation about Sector Line Two.

## Approach C: use observed partial-lap timing to the decision point and a configurable remaining-lap estimate

This is a compromise.

Recommendation:

> Implement the engine so it can support a partial first interval, even if the initial UI uses a simplified full-lap convention.

A generic prediction step type could be:
public enum PredictionSegmentType
{
    RemainingLap,
    FullLap
}
This issue should be resolved before final validation claims are made.

---

# 26. Recommended first agent task

The first task should not be “build the entire WPF application”.

It should be:

> Create the .NET solution and implement the pure domain model for a manually supplied decision state, including lap term breakdowns, a configurable target response delay, core and stabilised relative gap outputs, and unit tests.

Expected deliverables:

- solution structure;
- domain records;
- `ILapTimePredictor`;
- `IPitSequencePredictor`;
- one-lap and two-lap response tests;
- sign convention tests;
- JSON fixture example;
- README describing how to run tests;
- a list of unresolved assumptions.

Only after the model tests are trusted should the agent begin WPF or FastF1 integration.

---

# 27. Final conceptual summary

The intended engineering flow is:
Known race state at decision point
                +
User-configured engineering parameters
                |
                v
Deterministic lap time model
                |
                v
Predict attacking and target drivers through pit sequence
                |
                v
Calculate relative gap at:
  1. end of target out lap
  2. end of next normal lap
                |
                v
Present:
  - predicted gaps
  - attacker ahead or behind
  - undercut success
  - model term breakdowns
  - warnings and assumptions
The tool should remain focused on this flow.

The strongest implementation will be the one that makes every assumption visible, every output traceable, and every result reproducible.