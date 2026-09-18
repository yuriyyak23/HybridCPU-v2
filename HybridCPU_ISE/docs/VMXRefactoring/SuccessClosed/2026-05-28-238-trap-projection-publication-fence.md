# Success Closure: 238 Trap Projection Publication Fence

Date: 2026-05-28

## Slice

Closed the retire/completion publication fence for the admitted-denied VMCALL trap projection path.

The slice deliberately does not enable successful VMCALL, VM-exit backend execution, VMCS-owned completion authority, active VMCS pointer state, VMCS field storage, or a new VMX runtime manager.

## Dependencies Found

The current trap/intercept/publication path is:

- `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection`;
- `RuntimeBoundaryAdmissionService`;
- neutral `TrapRequest`, `TrapPolicyDescriptor`, `TrapPolicyBitmap`, and `NeutralTrapResult`;
- VMX compatibility `VmxTrapProjectionMapper` and `TrapDecision`;
- compatibility completion projection via `CompletionRecordCompatibilityProjection` and `CompletionProjectionService`;
- frozen retire vocabulary via `VmxRetireEffect` / `VmxRetireOutcome`;
- production VMX dispatch/retire callers in `ExecutionDispatcherV4.VmxCompatibility.cs`, `MicroOp.IO.cs`, and `CPU_Core.PipelineExecution.VmxRetire.cs`.

## Neutral Runtime Fence

Added neutral runtime completion publication vocabulary:

- `TrapCompletionPublicationDecision`;
- `TrapCompletionPublicationFenceResult`;
- `TrapCompletionPublicationFence`.

The fence lives under neutral runtime completion records and carries no VMX vocabulary. For the admitted-denied VMCALL path it returns:

```text
TrapCompletionPublicationDecision.DeniedBackendExecution
```

Both `CompletionPublicationAllowed` and `RetirePublicationAllowed` are false.

## VMX-Compatible Projection

VMX-compatible exit projection remains in `VmxTrapProjectionMapper` only. `VmExitReason.VmCall`, exit qualification, and `TrapDecision` are projected after neutral admission and neutral trap policy evaluation.

The publication boundary is separate:

- `CompletionRecord.TryFromCompatibilityExit` / `FromCompatibilityExit` require `TrapCompletionPublicationFenceResult`;
- `CompletionProjectionService` projects only explicit `CompletionRecordClass.CompatibilityExit` records;
- neutral trap completion records with VMX-looking numeric reason codes do not become VMX exit projections;
- `VmxRetireEffect.InterceptExit` requires the neutral fence and returns `SecurityPolicyViolation` while publication is denied.

## Why `VmExitReason` Is Not Runtime Authority

Runtime trap authority remains `NeutralTrapResult`. Runtime completion publication authority is now `TrapCompletionPublicationFenceResult`.

`VmExitReason` is used only after neutral authority for compatibility projection. It is not stored or branched on as the runtime decision for trap admission, completion publication, or retire publication.

## Production Authority

Production VMX opcode routing remains fail-closed:

- dispatch still creates `VmxRetireEffect.Fault`;
- retire still applies `ApplyRemovedFrontendFailClosedEffect`;
- no production caller invokes `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, or `CompletionRecord.FromCompatibilityExit`;
- no backend success path was added.

## Rewritten Or Denied

- Added `TrapCompletionPublicationFence`.
- Extended `VmxCompatibilityTrapAdmissionResult` with `PublicationFence`.
- Rewrote compatibility completion creation to require the neutral fence.
- Rewrote `CompletionProjectionService` so arbitrary nonzero neutral reason codes are not VMX projection sources.
- Fenced `VmxRetireEffect.InterceptExit` behind the neutral fence and fail-closed denial.
- Left VMCALL admitted-denied: no successful backend execution.

## Absence Checks

- Legacy/VMX C# count: `0`, path absent.
- Core/VMX legacy-marked C# count: `0`, path absent in this workspace shape.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.
- VMCS manager/backend forbidden markers in active `CloseToHSL/Core/Virtualization` excluding conformance: `0`.
- `TestAssemblerConsoleApps` stale `vmxUnit`, `core.VmxUnit`, `core.Vmcs`, `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager` markers: `0`.

## Verification

Builds:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; existing warning noise remains.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed; existing warning noise remains.
- `dotnet build TestAssemblerConsoleApps.csproj --no-restore`: passed; existing warning noise remains.

Targeted tests:

- `VmxTrapProjectionPublicationFenceTests`: passed 4/4.
- `VmxAdmittedDeniedVmCallTrapPathTests`: passed 5/5.
- `VmxNeutralTrapResultSplitTests`: passed 4/4.
- `RuntimeBoundaryAdmissionTests`: passed 4/4.
- `VmxFirstAdmittedCompatibilityPathTests`: passed 1/1.
- `VmxProjectionSchemaAndQuarantineTests`: passed 1/1.
- `VmxCompatibilityProjectionInventoryTests`: passed 1/1.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed 214/214.

Static:

- neutral fence source contains no `VmExitReason`, `Vmx`, `VMX`, `TrapDecision`, or `VmxExitQualification`;
- production VMX dispatch/retire callers contain no success trap/completion publication calls;
- compatibility completion and retire helpers require the neutral fence.

## Known Unrelated Debt

- Raw `FullyQualifiedName~Vmx` can still match `NonVmx` because `NonVmx` contains `Vmx`.
- Existing nullable/obsolete/xUnit analyzer warnings remain outside this slice.
- CloseToHSL/physical source path naming differs from older `Core/VMX` audit path vocabulary; current active VMX compatibility implementation remains under `CloseToHSL/Core/Virtualization/Compatibility`.

## Residual Risk

`CompletionRecord.FromCompatibilityExit` and `VmxRetireEffect.InterceptExit` still exist as frozen compatibility vocabulary, but they now require the neutral publication fence. A future successful publication path must prove a real neutral runtime completion-route owner and must not reuse this admitted-denied VMCALL projection as backend success.

## Next Heavy Step

Design the runtime-owned trap completion route and admission policy required before any future successful VMCALL/intercept publication, or continue the VMREAD path only if a neutral owner can expose generated read-only values without a VMCS field store.
