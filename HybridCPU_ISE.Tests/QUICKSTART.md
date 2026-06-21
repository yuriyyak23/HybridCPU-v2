# Historical Evaluation Quickstart

## Status

This file is historical and non-authoritative.
It is retained only as a navigation aid for the older evaluation-test directory layout.

Do not use this file for current validation claims, pass counts, expected IPC multipliers,
slot-distribution claims, or reviewer-facing evidence.
The current validation baseline is:

- `Documentation/validation-baseline.md`
- `Documentation/evidence-matrix.md`
- `build/run-validation-baseline.ps1`
- `build/recount-validation-evidence.ps1`
- `Documentation/AsmAppTestResults.md`

## Current Commands

Build the test project:

```powershell
dotnet build ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj"
```

Run the smoke baseline:

```powershell
powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore
```

Refresh live validation counts and optionally run smoke:

```powershell
powershell -ExecutionPolicy Bypass -File ".\build\recount-validation-evidence.ps1" -RunSmoke -NoRestore
```

Run the runtime sanity harness:

```powershell
dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200
```

Compare the runtime key values with `Documentation/AsmAppTestResults.md`.

## Current Evaluation Vocabulary

Use `HybridCPU_ISE.Tests/EVALUATION_TESTS_README.md` for current evaluation framing.
Current reviewer-facing language is based on:

- typed-slot reject taxonomy;
- structural identity reuse and certificate pressure;
- assist quota and assist backpressure legality;
- replay certificate coordination and replay invalidation;
- bounded replay-stable lane reuse;
- native-VLIW boundary freeze.

Legacy names such as `FSP` may still appear in retained counters or older test names.
Those names are historical aliases for typed-slot slack reclaim and bundle-compositional SMT
densification under legality/certificate constraints.

## What This File No Longer Claims

- No fixed IPC multiplier is claimed here.
- No fixed slot-distribution shape is claimed here.
- No full-suite pass count is claimed here.
- No graph-generation workflow is treated as current validation evidence without a fresh artifact path.
- No historical paper-response wording is authoritative for the live repository.
