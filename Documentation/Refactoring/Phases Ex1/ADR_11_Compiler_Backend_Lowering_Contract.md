# ADR 11: Compiler Backend Lowering Contract Gate

## Status

Current contract restrictions plus future-gated lowering rule.

This ADR does not approve compiler/backend production lowering to executable DSC or executable L7. It separates descriptor/carrier preservation from executable semantic dependence.

## Context

Phase 11 defines compiler/backend behavior while the ISA surfaces remain fail-closed/model-only and records the prerequisites for any future production lowering.

Current compiler/test surfaces may preserve descriptors, emit carrier instructions, or validate sideband metadata. That is not the same as producing code that depends on executable DSC/L7, async overlap, coherent cache, IOMMU-translated DSC, or partial success.

## Current Contract

Compiler/backend production lowering is forbidden for:

- executable lane6 DSC;
- executable L7 `ACCEL_*`;
- async DMA overlap;
- IOMMU-translated DSC addresses;
- coherent DMA/cache;
- successful partial completion;
- DSC1 stride/tile/scatter-gather;
- fake/test L7 backend as production protocol.

Allowed under the current contract:

- descriptor preservation;
- descriptor validation;
- carrier emission for fail-closed/model-only conformance where explicitly labeled;
- model/test helper orchestration;
- fail-closed negative conformance tests;
- future capability names only when marked unavailable/non-executable.

## Decision

Preserve the hard boundary between "compiler can carry descriptor evidence" and "compiler can lower to executable behavior."

Production lowering may depend on executable DSC/L7 only after:

- architecture approval;
- code implementation;
- feature/capability discovery;
- positive and negative conformance tests;
- cache/order/fault/backend contracts;
- documentation migration.

No optimization flag may bypass these gates.

## Accepted Direction

### Capability States

Future compiler/backend capabilities must distinguish:

- `Unavailable`: feature cannot be selected.
- `DescriptorOnly`: compiler may preserve descriptor evidence or sideband metadata.
- `ParserOnly`: compiler may target parser validation tests, not execution.
- `ModelOnly`: compiler may call explicit test/orchestration helpers outside production lowering.
- `ExecutableExperimental`: feature-gated internal execution after architecture approval and tests.
- `ProductionExecutable`: release-grade lowering after full conformance and documentation migration.

Only `ProductionExecutable` may be used for production lowering.

### Future DSC Lowering Requirements

Future DSC lowering requires:

- executable lane6 DSC approval;
- issue/admission token allocation;
- token scheduler/completion model;
- precise retire fault publication;
- ordering/fence/wait/poll semantics;
- physical/IOMMU backend selection;
- DSC2/capability ABI for non-DSC1 features;
- all-or-none or approved partial-success behavior;
- cache flush/invalidate protocol;
- compiler-visible barriers and fault behavior;
- conformance tests.

### Future L7 Lowering Requirements

Future L7 lowering requires:

- L7 executable ISA ADR;
- `rd` or CSR result publication;
- device/capability discovery;
- guarded token/queue/backpressure model;
- production backend protocol;
- staged commit/retire model;
- wait/fence/cancel semantics;
- conflict/cache/backend integration;
- conformance tests.

### Model/Test Separation

Model/test helpers must be named, flagged, and tested so production lowering cannot select them accidentally.

Fake/test backends must remain test-only unless a separate production backend contract replaces them.

## Rejected Alternatives

### Alternative 1: Treat Descriptor Emission As Executable Lowering

Rejected. Descriptor preservation or carrier emission does not prove runtime execution, retire, ordering, cache, or fault semantics.

### Alternative 2: Hide Future Use Behind Optimization Flags

Rejected. Architecture features require explicit capability bits and tests, not compiler flags alone.

### Alternative 3: Use Helper APIs As Production Backend

Rejected. Runtime/helper/model APIs are not ISA execution under the current contract.

### Alternative 4: Let Compiler Assume Coherence Or Partial Success

Rejected. Coherence and partial success are future-gated and unsafe for scheduling without explicit contracts.

## Exact Non-Goals

- Do not implement compiler code in this ADR.
- Do not authorize executable DSC lowering.
- Do not authorize executable L7 lowering.
- Do not convert model/test helpers into production backend APIs.
- Do not approve DSC1 stride/tile/scatter-gather.
- Do not approve IOMMU-translated DSC addresses.
- Do not approve coherent DMA/cache assumptions.
- Do not approve successful partial completion.

## Required Tests Before Production Lowering

- Production executable DSC lowering fails while the gate is closed.
- Production executable L7 lowering fails while the gate is closed.
- Descriptor preservation/validation tests pass without executable claims.
- Model/test helper use is explicitly labeled and unavailable to production lowering.
- Future DSC lowering rejects absent executable capability.
- Future DSC lowering rejects absent backend/address-space capability.
- Future DSC lowering rejects absent cache/order/fault capability.
- Future L7 lowering rejects absent result publication/backend/queue capability.
- Tests reject coherent DMA assumptions until coherent-DMA ADR exists.
- Tests reject partial-success assumptions until partial-success ADR exists.
- Documentation claim-safety tests block premature migration.

## Documentation Migration Rule

Compiler/backend docs must say:

- current lowering may preserve/validate descriptors or carriers;
- current lowering must not depend on execution;
- executable lowering is future-gated by architecture, implementation, tests, and documentation migration.

Any future feature must update compiler docs only when the matching ISA/runtime docs move from Future Design to Current Implemented Contract.

## Code And Test Evidence

- `HybridCPU_ISE\Core\Pipeline\MicroOps\DmaStreamComputeMicroOp.cs`
  - Lane6 `Execute` remains fail-closed and `WritesRegister = false`.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.cs`
  - `ExecutionEnabled => false`.
- `HybridCPU_ISE\Core\Pipeline\MicroOps\SystemDeviceCommandMicroOp.cs`
  - L7 `Execute` remains fail-closed and `WritesRegister = false`.
- `HybridCPU_Compiler\*`
  - Compiler/backend surfaces exist and must keep production lowering separate from descriptor preservation.
- `HybridCPU_Compiler\Core\IR\Model\CompilerBackendLoweringContract.cs`
  - Phase11 capability states and required future gates are explicit; production lowering is rejected unless the request is `ProductionExecutable` and capability-complete.
- `HybridCPU_ISE.Tests\CompilerTests\DmaStreamComputeCompilerContractTests.cs`
  - Existing tests cover descriptor emission/preservation and no fallback assumptions.
- `HybridCPU_ISE.Tests\CompilerTests\L7SdcCompilerPhase12Tests.cs`
  - Existing tests cover L7 carrier/descriptor sideband emission; this remains carrier evidence, not executable ISA.
- `HybridCPU_ISE.Tests\CompilerTests\CompilerBackendLoweringPhase11Tests.cs`
  - Phase11 tests cover non-production capability states, parser-only separation, model/test helper rejection, absent DSC/L7 gate rejection, and documentation claim safety.

## Strict Prohibitions

This ADR must not be used to claim:

- compiler/backend may production-lower to executable DSC;
- compiler/backend may production-lower to executable L7;
- descriptor preservation implies execution;
- parser availability implies execution;
- fake/test backend is production protocol;
- coherent cache or partial success may be assumed by scheduling.
