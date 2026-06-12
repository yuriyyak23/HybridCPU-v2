# Telemetry And Evidence

## Telemetry model

`AcceleratorTelemetry` records L7-SDC capability, descriptor, submit, lifecycle, backend,
commit, byte, conflict, direct-write, rollback, invalidation, operation, latency, and
lane7 pressure events. `AcceleratorTelemetrySnapshot` freezes those counters and
evidence records for diagnostics.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Telemetry/AcceleratorTelemetry.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`

## Immutable observation snapshots

Snapshots expose read-only observations. They are not guard credentials and cannot
authorize descriptor acceptance, capability acceptance, submit admission, backend
execution, commit, cancel, fence, fault, or exception publication. Tests pass telemetry
and evidence-plane inputs to authority surfaces and verify rejection.

Fault telemetry and status words are observations. They do not imply that the
current `ACCEL_*` carrier path publishes retire exceptions; current
commit/fence/backend failures remain runtime results surfaced through guarded
status/register ABI paths.

Telemetry snapshots, token handles, capability records, conflict records,
backend results, and status words are downstream evidence only. Under Ex1
Phase13 they cannot close gates for expansion beyond the current L7 contour,
lane6 DSC expansion beyond Phase 06 DSC1, DSC2 execution, IOMMU-backed
execution, async overlap, coherent DMA/cache, successful partial completion, or
production compiler/backend lowering.

They also do not constitute a lane7 golden/no-fallback artifact corpus. Full
corpus closure still requires exact L7-SDC artifact files, manifest rows,
manifest-consuming tests, no generic backend fallback evidence, and
retire/replay/status publication evidence for the named surface. That corpus is
currently `not evidenced`.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Telemetry/AcceleratorTelemetry.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcEvidenceIsNotAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`

## Diagnostics export

`TelemetryExporter` exports L7-SDC telemetry through the optional
`TypedSlotTelemetryProfile.AcceleratorTelemetry` field. This is additive and separate
from lane6 `DmaStreamComputeTelemetry`.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Diagnostics/TelemetryExporter.cs`
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/TypedSlotTelemetryProfile.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeTelemetry.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/04_Authority_Model.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
