# 2026-05-25 task 219 - final freeze-readiness move-away probe

## Scope

Final freeze-readiness certification covered the full compiled `Legacy/VMX` physical quarantine, the three remaining production compatibility carriers, generated/debug/lifecycle conformance inventory, and broad-filter debt separation.

This step explicitly tested the deletion hypothesis by moving:

```text
C:/Users/Yuriy Kurnosov/Desktop/HybridCPU ISE/HybridCPU_ISE/Legacy/VMX
```

to:

```text
C:/Users/Yuriy Kurnosov/Desktop/New folder/VMX-freeze-probe-*
```

then running the production build and restoring the directory in `finally`.

## Move-away result

The probe failed before tests with `36` production compile errors.

The missing symbols were:

- `VmxInstructionPayload`
- `VmxRetireEffect`
- `VmxRetireOutcome`
- `VmxOperationKind`

The callers are production paths, not old tests:

- `Core/VMX/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs`
- `Core/Pipeline/MicroOps/InstructionIR.cs`
- `Core/Pipeline/MicroOps/MicroOp.IO.cs`
- `Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs`
- `Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs`
- `Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs`
- `Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.cs`
- `NonRTL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs`

Conclusion: deleting the whole physical quarantine is not a safe cleanup today. It would be a compatibility frontend ABI rewrite.

## Change made

`ShadowVmcsNestedProjectionService` no longer depends on `ShadowVmcsBridgeRetirementContract`. Production code no longer calls conformance evidence as an API.

The bridge still:

- constructs over `VmcsV2Descriptor`;
- returns `NestedValidationResult.Fail`;
- reports `CompatibilityProjectionFailed`;
- returns `false`;
- owns no nested/checkpoint/runtime authority.

## New evidence

Added `LegacyVmxFreezeReadinessCertificationContract`.

It records:

- the move-away probe was executed;
- mechanical deletion is rejected by production missing-symbol errors;
- `CanDeclareFreeze == false`;
- retained production compatibility source count remains `3`;
- broad conformance/build matrix requirements;
- unrelated broad-filter/warning debt must not be mixed into VMX freeze evidence.

## Inventory

After this step:

- `Legacy/VMX/Compatibility`: `3` production `.cs` files.
- Total `Legacy/VMX`: `40` `.cs` files.
- `Core/VMX` legacy-marked `.cs`: `0`.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.

## Freeze decision

VMX freeze is not declared.

The current blocker is physical production dependency on `Legacy/VMX` for live compatibility ABI carriers. Those carriers are not authority owners, but freeze should not be declared while the production build requires the physical quarantine directory.

## Verification

- Temporary move-away production build: failed as expected with `36` missing-symbol errors; folder restored.
- Production build after restore: passed, existing `54` warnings, `0` errors.
- Tests build after restore: passed, existing `93` warnings, `0` errors.
- Focused certification test: passed `1/1`.
- Broad `FullyQualifiedName~Vmx` filter: failed with one unrelated stale path failure in `Phase09DirectFactoryCallerBoundaryTests`, which expects `Core/Diagnostics/InstructionRegistry.Helpers.Core.cs` while the current source is under `NonRTL/Core/Diagnostics`.

## Residual risk

The retained ABI carriers still contain VMX/VMCS vocabulary and currently live in physical `Legacy/VMX`. Their authority is constrained, but their placement remains a freeze blocker.

Known broad warnings and the stale `InstructionRegistry.Helpers.Core.cs` repository-shape test failure are unrelated to the move-away carrier decision and remain separated from freeze evidence.

## Next heavy step

Create a no-legacy production-carrier exit:

- rehome/rename or generate neutral compatibility vocabulary for `VmxInstructionPayload`;
- split or rehome typed retire carrier vocabulary for `VmxRetireEffect`, `VmxRetireOutcome`, and `VmxOperationKind`;
- keep the Shadow VMCS bridge fail-closed while moving any required generated projection vocabulary out of physical `Legacy/VMX`;
- rerun the same move-away build probe and broad conformance matrix before any freeze declaration.
