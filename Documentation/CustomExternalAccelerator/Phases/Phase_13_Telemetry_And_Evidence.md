# Phase 13 - Telemetry and evidence surfaces

Status:

- Closed on 2026-04-28.
- Implemented L7-SDC telemetry/evidence as observation-only counters and
  immutable snapshots; telemetry is not accepted by guard, capability, submit,
  backend, commit, cancellation, fence, fault, or exception-publication
  authority surfaces.
- Export is additive through `TypedSlotTelemetryProfile.AcceleratorTelemetry`;
  DmaStreamCompute telemetry remains a separate lane6 field.
- Latest diagnostics artifact:
  `TestResults/TestAssemblerConsoleApps/20260428_193712_284_matrix-smoke`.

Closure validation:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcTelemetry|L7SdcEvidenceIsNotAuthority" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "DmaStreamComputeTelemetry|Phase09|Phase12" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcCompilerEmission|L7SdcCompilerNoRuntimeFallback|L7SdcCompilerLane7Pressure" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcConflictManager|L7SdcDmaStreamComputeConflict|L7SdcSrfAssistConflict" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcMatMulCapability|L7SdcMatMulDescriptor|L7SdcMatMulNoLegacyExecute" --no-restore
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Goal:

- Add L7-SDC telemetry counters and evidence records while preserving the rule
  that telemetry is never authority.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Telemetry/*`
- `HybridCPU_ISE/Core/Diagnostics/TelemetryExporter.cs`
- `HybridCPU_ISE/Core/Diagnostics/TypedSlotTelemetryProfile.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/*`

Compiler files likely touched:

- optional compiler diagnostics for emitted accelerator commands

New ISE types:

- `AcceleratorTelemetry`
- `AcceleratorTelemetrySnapshot`
- `AcceleratorEvidenceRecord`
- `AcceleratorRejectCounters`
- `AcceleratorLifecycleCounters`
- `AcceleratorByteCounters`
- `AcceleratorConflictCounters`

Methods to design:

- `AcceleratorTelemetry.RecordCapabilityQuery(...)`
- `AcceleratorTelemetry.RecordDescriptorParse(...)`
- `AcceleratorTelemetry.RecordSubmit(...)`
- `AcceleratorTelemetry.RecordTokenTransition(...)`
- `AcceleratorTelemetry.RecordDeviceBusyReject(...)`
- `AcceleratorTelemetry.RecordQueueFullReject(...)`
- `AcceleratorTelemetry.RecordDomainReject(...)`
- `AcceleratorTelemetry.RecordOwnerDriftReject(...)`
- `AcceleratorTelemetry.RecordFootprintConflictReject(...)`
- `AcceleratorTelemetry.RecordDirectWriteViolation(...)`
- `AcceleratorTelemetry.RecordCommitRollback(...)`
- `AcceleratorTelemetry.Snapshot()`
- `TelemetryExporter.ExportAcceleratorTelemetry(...)`

Required counters:

- capability query attempts/success/reject
- descriptor parse attempts/accept/reject
- submit attempts/accepted/rejected
- tokens created/validated/queued/running/device-completed/commit-pending
- tokens committed/faulted/canceled/timed out/abandoned
- device busy rejects
- queue full rejects
- domain rejects
- owner drift rejects
- mapping epoch drift rejects
- footprint conflict rejects
- direct-write violation rejects
- commit rollback count
- bytes read/staged/committed
- operation count
- latency cycles
- SRF/cache invalidations caused by accelerator commit
- DmaStreamCompute conflict rejects
- lane7 submit/poll throttle rejects

Authority rule:

- telemetry can explain a decision after the fact
- telemetry cannot authorize descriptor acceptance
- telemetry cannot authorize capability acceptance
- telemetry cannot authorize token commit
- telemetry cannot authorize exception publication

Tests to add:

- `L7SdcTelemetryTests`
- `L7SdcEvidenceIsNotAuthorityTests`

Test cases:

- every reject path increments an evidence counter
- every token transition increments lifecycle counter
- direct write violation increments violation counter but cannot commit
- telemetry snapshot cannot be passed as guard evidence
- replay/certificate/token ids remain non-authoritative
- lane7 pressure throttling increments evidence without hiding reject

Must not break:

- existing DmaStreamCompute telemetry counters
- diagnostics export format compatibility
- TestAssemblerConsoleApps artifact generation

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcTelemetry|L7SdcEvidenceIsNotAuthority"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeTelemetry|Phase09|Phase12"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; telemetry output growth
  must not mask IPC/cycle/stall regressions.

Definition of done:

- all L7-SDC critical events have evidence counters
- tests prove counters cannot grant authority

Rollback rule:

- remove or disable telemetry consumers if unstable, but keep guard and
  fail-closed behavior
