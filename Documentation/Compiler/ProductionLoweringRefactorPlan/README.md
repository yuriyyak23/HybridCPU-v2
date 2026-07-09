# Production Refactor Compiler Lowering Plan

This directory contains the phased production-lowering refactor plan for `HybridCPU_Compiler` and its contract with `HybridCPU_ISE`.

The plan is intentionally **docs-only** in this branch. It does not change compiler, runtime, ISA, tests, or CI behavior.

## Core invariant

Every future production-lowering feature must answer all of the following before implementation:

1. What exactly is being produced?
2. Under which explicit production gate?
3. For which contour?
4. Which runtime authority is still pending?
5. What authority does this compiler result explicitly not have?

If the answer is not represented in types, tests, telemetry/evidence, and ADR/RFC text, the feature is not ready for implementation.

## Non-negotiable authority boundaries

The refactor must preserve:

```text
carrier != execution
execution != publication
publication != authority
authority != commit
commit != retire
retire != evidence
evidence != production lowering
```

Additional hard rules:

- compiler is not runtime authority;
- compiler never owns final runtime `LegalityDecision`;
- runtime Legality A/B remains runtime-owned;
- typed-slot facts are structural evidence only;
- sideband, descriptor, token, certificate, evidence are not execution rights;
- descriptor parser success is not production lowering;
- helper success is not production lowering;
- carrier emission is not execution, publication, commit, or retire;
- VMX remains projection/no-emission unless a separate runtime-owned VMX backend architecture is approved;
- SecureCompute remains policy/admission/evidence-only unless a separate runtime-owned secure backend architecture is approved;
- DSC and L7-SDC are distinct contours with no hidden fallback;
- L7 descriptorless submit remains fail-closed;
- host-owned evidence must not enter guest/domain architectural state.

## Repository evidence inspected

The plan is based on the current `master` implementation of the following areas:

- `HybridCPU_Compiler/Core/IR/Authority/CompilerAuthorityTaxonomy.cs`
- `HybridCPU_Compiler/Core/IR/Contours/CompilerExecutionContourSelection.cs`
- `HybridCPU_Compiler/Core/IR/Contours/ContourProviderContracts.cs`
- `HybridCPU_Compiler/Core/IR/Lowering/CompilerLoweringDecision.cs`
- `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs`
- `HybridCPU_Compiler/Core/IR/Construction/CompilerVectorTransferEmissionLowerer.cs`
- `HybridCPU_Compiler/Core/IR/Construction/CompilerMatrixTileEmissionLowerer.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/API/Threading/ThreadCompilerContext.FacadeAudit.cs`
- `HybridCPU_Compiler/API/Threading/ThreadCompilerContext.RuntimeGuardObservation.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Runtime/Completion/Routing/LaneCompletionRouting.cs`
- `Documentation/ISE_Instructions_By_Lane_CodeConfirmed.md`
- Phase 09 compiler negative/readiness tests under `HybridCPU_ISE.Tests/CompilerTests/`

## Phase files

| File | Purpose |
| --- | --- |
| `00-current-state-and-boundaries.md` | Current-state assessment and classification of existing lowering surfaces. |
| `01-readiness-source-scanner.md` | Source-scanner and negative guardrail phase. |
| `02-golden-artifact-harness.md` | Golden artifact harness foundation. |
| `03-explicit-production-gates.md` | Explicit production gate model. |
| `04-provider-contract-split.md` | Split shell providers from production providers. |
| `05-compiler-to-ise-parity.md` | Compiler-to-ISE decode/encode/lane/slot parity. |
| `06-native-vliw-scalar-provider.md` | First production provider candidate. |
| `07-native-vliw-load-store-provider.md` | Load/store provider candidate. |
| `08-native-vliw-branch-control-provider.md` | Branch/control provider candidate. |
| `09-stream-vector-scoped-candidate.md` | Scoped VLOAD/VSTORE vector-transfer candidate. |
| `10-dsc-lane6-provider.md` | Descriptor-backed DSC lane6 provider candidate. |
| `11-l7-sdc-lane7-provider.md` | Descriptor-backed L7-SDC lane7 provider candidate. |
| `12-rfc-only-and-exit-checklist.md` | RFC-only contours, final checklist, and ADR list. |

## Execution order

The intended order is:

```text
observe -> classify -> gate -> split provider contracts -> parity harness ->
first production provider -> shadow compare -> migrate callers -> deprecate legacy -> remove ambiguity
```

The forbidden order is:

```text
rewrite -> hope behavior is equivalent
```

## Current top-level conclusion

The current registry and contour providers are production-lowering shells, not production lowering implementation. Positive carrier emission exists for some helper surfaces, but it is explicitly helper ABI or compatibility transport and remains runtime-authority-bound. Production lowering should therefore be introduced only through a new production-provider track guarded by explicit production gates, golden artifacts, ISE parity, no-fallback proof, and runtime-authority-pending result headers.
