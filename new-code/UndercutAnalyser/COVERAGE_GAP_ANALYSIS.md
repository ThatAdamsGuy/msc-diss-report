# 📊 TEST COVERAGE ANALYSIS & RECOMMENDATIONS REPORT
## UndercutAnalyser Solution - Coverage Assessment

**Generated:** Test run with XPlat Code Coverage / Cobertura  
**Overall Coverage:** 70.68% line | 59.76% branch  
**Build Status:** ✅ 279/279 tests passing  

---

## 📈 EXECUTIVE SUMMARY

Your test suite has **solid structural foundation** with:
- ✅ **66.7%** of classes (58/87) at 100% line coverage
- ✅ **Domain models & prediction layer** completely covered (100%)
- ✅ **Key prediction logic** untainted (LapTimePredictor, PitSequencePredictor, models)

**However, there are notable gaps:**
- ⚠️ **Branch coverage 59.76%** is 11% below line coverage
  - Indicates conditional paths and error handling are under-tested
- ❌ **6 classes below 50%** coverage (mostly async/workflow services)
- ❌ **Workflow services** average only 90.1% (vs domain 100%)
- ❌ **ViewModels** only 79.5% coverage (UI binding/events rarely tested)

---

## 🎯 CRITICAL GAPS (Immediate Attention Needed)

### 1. **EventWorkflowService / LoadAsync** - 42.1% | 27.3% (async state machine)
**File:** `Services/EventWorkflow.DataLoad.cs`

**Missing Coverage:**
- ✗ Success path when race session found (reference calculation not verified)
- ✗ Session name matching with `IndexOf()` variant (`"Race"` substring match)
- ✗ Null/empty list handling for each data fetch
- ✗ `ReferenceLapTimeCalculator.Calculate()` result path
- ✗ Exception scenarios (network failures, null data)

**Current Tests (2 tests):**
- Only test the `NoSessionFound()` paths
- No happy-path integration test
- No data validation on returned result

**Recommended Tests:**
```csharp
[Fact]
public async Task LoadAsync_SuccessfulLoad_WhenRaceSessionByExactName()
{
	// Arrange: Session named exactly "Race"
	// Assert: HasRaceSession=true, Reference populated, all data lists populated
}

[Fact]
public async Task LoadAsync_SuccessfulLoad_WhenRaceSessionBySubstringMatch()
{
	// Arrange: Session named "Feature Race" or "Race Simulation"
	// Assert: IndexOf() match works, reference calculated
}

[Fact]
public async Task LoadAsync_HandlesEmptyLapsGracefully()
{
	// Arrange: Race session found, but Laps list empty
	// Assert: Reference is null or handled gracefully
}

[Fact]
public async Task LoadAsync_PropagatesToResultCorrectly()
{
	// Arrange: Full valid data set
	// Assert: Verify all collections passed through correctly
}
```

**Impact:** HIGH - This is the entry point for all race event data

---

### 2. **WorkspaceWorkflowService** - 28.6% coverage
**File:** `Services/WorkspaceWorkflow.*.cs` (multiple partial files)

**Missing Coverage:**
- ✗ CSV export path (likely `WorkspaceWorkflow.CsvFileSave.cs`)
- ✗ Error handling and validation
- ✗ Boundary conditions on tyre/fuel parameters
- ✗ Grid population logic
- ✗ State transitions

**Current Challenge:**
This service is broken across multiple partial files. Each partial likely has orchestration code that isn't exercised.

**Recommended Action:**
- Sample each partial file to identify untested branches
- Add integration tests for each partial's public methods
- Test parameter validation (fuel ranges, tyre compounds)

---

### 3. **FileIO/CSV Path** - MainWindowCsvSaveOptions 33.3%
**File:** `Services/WorkspaceWorkflow.CsvFileSave.cs`

**Missing Coverage:**
- ✗ File write failures (disk full, permission denied)
- ✗ Path validation
- ✗ CSV formatting edge cases (special characters, null values)
- ✗ Success acknowledgment

**Recommended Tests:**
```csharp
[Fact]
public async Task SaveCsvAsync_HandlesIOException_Gracefully() { }

[Fact]
public async Task SaveCsvAsync_FormatsSpecialCharactersInData() { }
```

**Impact:** MEDIUM - User-facing feature, data integrity risk

---

### 4. **Infrastructure Converters** - 71-82% coverage
**Files:**
- `Infrastructure/Logger.cs` (71%)
- `Infrastructure/FloatNullableJsonConverter.cs` (71.9%)
- `Infrastructure/DateTimeNullableJsonConverter.cs` (82.2%)

**Missing Coverage (All Three):**
- ✗ Null value handling paths
- ✗ Invalid input exception scenarios
- ✗ Edge cases (very large/small numbers, date boundaries)
- ✗ Error logging paths

**Current Tests:** Exist but likely only test happy path

**Recommended Tests:**
```csharp
// For Float Converter
[Fact]
public void Read_WithNullValue_ReturnsNull() { }

[Fact]
public void Read_WithInvalidNumber_ThrowsFormatException() { }

[Fact]
public void Write_WithNaN_SerializesCorrectly() { }

// For DateTime Converter
[Fact]
public void Read_WithMinDateTime_ReturnsCorrectValue() { }

[Fact]
public void Read_WithInvalidDateString_ThrowsException() { }

// For Logger
[Fact]
public void Log_WithNullMessage_DoesNotThrow() { }

[Fact]
public void Log_WithVeryLongMessage_Truncates() { }
```

**Impact:** MEDIUM - Serialization bugs can cause data loss

---

### 5. **EventSession** (Domain Model) - 86.7%
**File:** `Domain/Models/EventSession.cs`

**Missing Coverage:**
- ✗ Constructor validation
- ✗ Property assignment edge cases
- ✗ Comparison operators (if any)
- ✗ ToString/GetHashCode (if overridden)

**Impact:** LOW - But these are data carriers, deserialize validation matters

---

### 6. **ViewModels (79.5% module avg)** - EventSelectorViewModel
**File:** `ViewModels/EventSelectorViewModel.cs`

**Identified Issues:**
- `EventSelectorViewModel/<>c__DisplayClass13_1` - **12.5%** (CRITICAL)
- `EventSelectorViewModel/<LoadAsync>d__13` - **92.8%** (missing edge cases)

**Missing Coverage:**
- ✗ UI binding change events
- ✗ Async cancellation (LoadAsync likely has CTSs)
- ✗ Event handler exceptions
- ✗ Property change notification
- ✗ Validation error paths

**Challenge:** Closure class (`<>c__DisplayClass`) indicates captured variables in nested scopes—these are rarely tested in isolation

**Recommended Tests:**
```csharp
[Fact]
public async Task LoadAsync_CancellationToken_CancelsGracefully() { }

[Fact]
public void PropertyChanged_NotifiesUIOnMeetingSelection() { }

[Fact]
public async Task LoadAsync_ExceptionDuringLoad_PreservesUI() { }
```

**Impact:** MEDIUM - Affects user responsiveness and crash resilience

---

## 🔴 CLASSES NEEDING DEEP DIVES

Query results from coverage analysis:

| Coverage | Class | File Range | Priority |
|----------|-------|-----------|----------|
| 12.5% | EventSelectorViewModel/<>c__DisplayClass | Closure in UI binding | 🔴 CRITICAL |
| 27.3% | EventWorkflowService/<LoadAsync>d__0 | Async state machine | 🔴 CRITICAL |
| 28.6% | WorkspaceWorkflowService | Multi-partial orchestration | 🔴 CRITICAL |
| 33.3% | MainWindowCsvSaveOptions | File export config | 🟡 HIGH |
| 42.1% | EventWorkflowService | Happy-path async logic | 🟡 HIGH |
| 57.1% | MainWindowEventDataLoadResult | Record factory methods | 🟡 HIGH |
| 62.5% | RaceTraceDriverSeries | Computation helper | 🟡 MEDIUM |
| 70.8% | RaceTraceWorkflowService | Multi-step race trace | 🟡 MEDIUM |

---

## 💡 BRANCH COVERAGE ANALYSIS

**Overall: 59.76% (569/952 branches)**

This **11.9 percentage-point gap** vs. line coverage indicates:

1. **Conditional logic under-tested**
   - `if/else` branches only test one path
   - `null` coalescing operators not checked for null
   - Ternary expressions missing edge cases

2. **Exception handling NOT exercised**
   - `try/catch` blocks never catch (only test happy path)
   - Validation guards never trigger
   - Async fault paths not tested

3. **Logic operators**
   - `&&` and `||` short-circuit not verified
   - Complex conditions only partially tested

**Fix Strategy:**
- Write tests that explicitly violate preconditions
- Test every `if` condition in both true/false states
- Force exceptions in try-catch blocks
- Use `null` inputs for everything nullable

---

## 📋 TEST STRATEGY BY TIER

### TIER 1: CRITICAL (Do First)
- [ ] EventWorkflowService.LoadAsync happy path
- [ ] CSV export success + failure paths
- [ ] Null handling in converters
- [ ] ViewModels async cancellation

**Estimated effort:** 8-10 tests | 2-3 hours

### TIER 2: HIGH PRIORITY (This Week)
- [ ] RaceTraceWorkflowService branch coverage
- [ ] EventSession validation
- [ ] Logger edge cases
- [ ] All async IEnumerable patterns

**Estimated effort:** 12-15 tests | 3-4 hours

### TIER 3: NICE TO HAVE (Polish)
- [ ] UI event integration tests
- [ ] High-complexity calculation edge cases
- [ ] Stress tests on large data sets

**Estimated effort:** 10-12 tests | 2-3 hours

---

## ✅ THINGS YOU'RE DOING WELL

**No issues in these areas:**
- ✅ Domain models (100% coverage)
- ✅ Prediction calculations (100% coverage)
- ✅ Core predictor logic (LapTimePredictor, PitSequencePredictor)
- ✅ Model parameter defaults and fallbacks
- ✅ Tyre parameter row model (post-refactor, 99.1%)

**Keep this up for:**
- TyreAndModelParameterTests
- LapTimePredictorTests
- PitSequencePredictorTests

---

## 🛠️ IMMEDIATE ACTION ITEMS

### Week 1: Close Critical Gaps
1. Add 3-4 tests for `EventWorkflowService.LoadAsync` success case
2. Add exception scenario tests for EventWorkflow
3. Add CSV write failure tests
4. Add null-input tests for converters

### Week 2: Branch Coverage
1. Profile which branches are uncovered using coverage report
2. Add conditional path tests for RaceTrace, Workspace workflows
3. Test error-handling paths (exceptions, timeouts)

### Week 3: ViewModels & UI
1. Add async cancellation tests for ViewModels
2. Add property-changed event tests
3. Test UI binding edge cases

---

## 📊 COVERAGE TARGETS

| Metric | Current | Target | Effort |
|--------|---------|--------|--------|
| Line Coverage | 70.68% | 80% | 40-50 additional tests |
| Branch Coverage | 59.76% | 75% | 60-80 additional tests |
| Zero-Coverage Classes | 1 | 0 | Minimal (EventSelectorViewModel closure) |
| Classes <50% | 6 | 0 | 15-20 additional tests |

---

## 📝 NOTES

- **Async Methods:** The `<>d__N` classes are compiler-generated async state machines. Low coverage here usually means the async path isn't exercised. Test the `await` paths explicitly.
- **Closures:** The `<>c__DisplayClass` indicates captured variables in lambdas/nested scopes. These are difficult to test without invoking their parent method.
- **SkipWAML:** XAML codegen (`*.xaml.cs`) has 0% coverage by design—don't test UI glue, test business logic behind it.

---

**Report Generated By:** Comprehensive Coverage Analysis Tool  
**Next Step:** Start with Tier 1 tests to maximize ROI on test time investment.
