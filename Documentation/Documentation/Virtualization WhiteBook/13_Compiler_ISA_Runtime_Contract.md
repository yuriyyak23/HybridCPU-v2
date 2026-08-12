# Compiler, ISA, And Runtime Contract

Status date: 2026-08-11.

The compiler is a deterministic producer of ISA carriers and compile-time intent. It is not a source of runtime authority.

```text
compiler intent / metadata / encoded opcode != runtime authority
```

## Current Exact Compiler Scope

The only controlled virtualization emission is:

```text
HybridCPU.VMCALL.Runtime.v1
leaf 0x0001
PROBE_NO_STATE_V1
Rs1 = architectural register containing the full numeric leaf value
Rs2 = x0
Rd  = x0
```

It is guarded by `CompilerEmissionDecisionV1`, references runtime decision `D2-HV-VMCALL-RUNTIME-V1-PROBE-0001` and its reviewed SpecDigest, and is disabled by default. `CompilerEmissionEnabled=false` is a compiler production kill switch only. It does not alter the runtime D2, grants, domain admission, backend owner, completion or retire rules.

No other VMCALL leaf, VMREAD, VMWRITE, nested virtualization, SecureCompute, memory/I/O/DMA/IOMMU/device operation or Lane6/Lane7/Stream passthrough is compiler-emittable through this decision.

## Canonical Compiler Pipeline

The active compiler implementation is under `HybridCPU_Compiler`, not the older ISE compatibility/no-emission modelling directory.

```text
CompilerExactProbeEmissionRequest
  -> CompilerEmissionDecisionV1 reference validation
  -> VirtualizationCompilerIntent
  -> ISA / operand ABI / exact-profile legality facts
  -> exact VLIW_Instruction carrier
  -> compiler-only intent binding and mandatory canonical-ingress revalidation
  -> HybridCpuIrBuilder
  -> local scheduling
  -> W=8 typed bundle formation and Stage-A-class/Stage-B-materialization checks
  -> SystemSingleton, hard-pinned slot/lane 7, non-stealable exclusive cycle
  -> normal bundle lowering and serialization
  -> decoder-visible ISA carrier
```

The intent, legality facts, emission metadata, plan and binding are compiler-only. They are not serialized as admission authority and are not consumed by runtime code.

The exact carrier then enters the independent runtime chain:

```text
decode/materialization
  -> live Stage A / Stage B validation
  -> SafetyVerifier E1
  -> immutable full-value operand capture (O1 input)
  -> D2/O1 resolution and live capability/domain/root/restore validation
  -> SafetyVerifier E2
  -> neutral backend E3
  -> neutral completion publication E5
  -> retire authorization E6
  -> drain/restore lifecycle E7
```

Each runtime boundary remains authoritative for its own decision. Accepted encoding, successful decode, typed bundle placement or lane selection does not imply later success.

## Compiler Legality Versus Runtime Policy

Compiler legality may check only facts needed to build deterministic ISA:

- exact accepted compiler decision reference and emission profile;
- opcode/namespace/operation/leaf identity;
- operand ABI and register shape;
- shared ISA classification and serialization;
- typed slot/lane compatibility, W=8 bundle legality and singleton isolation;
- explicit adjacent-operation denials.

The compiler does not decide live capabilities, root authority, domain admission, grant or restore generations, replay authorization, backend authorization, completion publication or retire authorization. It may name those requirements in diagnostics, but only runtime owners decide them.

## Raw Ingress And Forgery Boundary

Every canonical compiler `VMCALL` requires an exact compiler-only intent binding. Raw thread-context emission, raw insertion, direct canonical compilation and direct IR construction fail closed without it. Canonical ingress reruns the exact lowerer and rejects duplicate, stale, forged or carrier-mismatched bindings.

This protects compiler contract integrity; it is still not a security token. Runtime correctness does not depend on the binding being present because the binding never crosses the ISA boundary.

## Legacy And Compatibility Models

`HybridCPU_Compiler/Legacy/VMX-2/Core/IR/Model/VmxCompilerAuthority.cs` is a legacy diagnostic raw-opcode and VMCS-sideband inventory. Its `CompilerHelperEmittable=false` rule denies generic VMX helpers; the exact operation-specific gate does not consume it.

`HybridCPU_ISE/CloseToHSL/Core/Virtualization/CompilerBoundary/**` is ISE compatibility/no-emission modelling. It is not the canonical compiler IR, scheduler, bundler or emitter. Neither layer may call a VMX handler or runtime owner directly.

## Extensibility Rule

A future owner-specific operation becomes compiler-emittable only after:

1. its runtime semantic scope has a separately accepted owner-specific E0/D2;
2. a separate compiler decision references that exact DecisionId and SpecDigest;
3. its exact namespace/operation/field, operand ABI, emission profile, scheduling constraints and adjacent denials are implemented and tested;
4. runtime remains independently fail-closed without compiler metadata.

An accepted runtime D2 does not automatically open a compiler helper. Conversely, a compiler RFC or helper cannot open runtime execution.

The accepted governance-only `GuestCr0`/`GuestCr4` VMREAD decision does not authorize its production implementation or compiler emission. Read-only projection must use its owner-specific projection chain and must not invent VMCALL-like backend, completion or retire stages.
