# Phase 11 - Compiler Backend Lowering Contract

Status:
Current contract restrictions plus Future gated lowering rules.

Scope:
Define compiler/backend behavior under current fail-closed/model-only contract and future executable gates.

Current code evidence:
- Lane6 `DmaStreamComputeMicroOp.Execute` is fail-closed and `WritesRegister = false`.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is false.
- L7 `SystemDeviceCommandMicroOp.Execute` is fail-closed and `WritesRegister = false`.
- Phase11 compiler/backend lowering contract names future capability states but keeps production lowering rejected unless `ProductionExecutable` and all required gates are present.
- Existing compiler/backend conformance tests in the audit baseline protect no-production-executable-lowering assumptions.
- Runtime/helper and external accelerator model APIs exist but are not ISA execution.

Architecture decision:
Current contract:
Compiler/backend production lowering is forbidden for:
- executable lane6 DSC;
- executable `ACCEL_*`;
- async DMA overlap;
- IOMMU-translated DSC addresses;
- coherent DMA/cache;
- successful partial completion;
- DSC1 stride/tile/scatter-gather;
- L7 fake/test backend as production device protocol.

Allowed:
- descriptor preservation;
- descriptor validation;
- model/test-only helper usage with explicit test/orchestration labeling;
- fail-closed conformance tests;
- future capability-gated lowering only after implementation and migration.

Non-goals:
- Do not infer target availability from descriptor parser availability.
- Do not make helper/model APIs part of production lowering.
- Do not hide future assumptions behind optimization flags without architecture feature bits and tests.

Required design gates:
- Executable lane6 DSC ADR and implementation.
- Token lifecycle and completion model.
- Precise retire fault model.
- Ordering/fence/wait/poll semantics.
- Physical/IOMMU backend selection and descriptor ABI.
- Cache flush/invalidate protocol.
- DSC2/SDC capability discovery.
- L7 executable ADR for `ACCEL_*`.
- Conformance suite and documentation migration.

Implementation plan:
1. Keep current compiler/backend tests that reject production lowering to fail-closed carriers.
2. Add capability model names for future features but do not enable production use until implementation exists.
3. For future DSC lowering, require descriptor capability, address-space capability, cache protocol capability, and wait/fence semantics.
4. For future L7 lowering, require executable instruction capability, register/CSR result contract, device capability, backend protocol, and ordering/cache contracts.
5. Separate model/test helpers from production backends in API naming, feature flags, and tests.

Affected files/classes/methods:
- `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs`
- `HybridCPU_Compiler/*`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- DSC descriptor/parser/capability surfaces
- L7 descriptor/register/capability surfaces
- test suites under `HybridCPU_ISE.Tests`

Testing requirements:
- `HybridCPU_ISE.Tests/CompilerTests/CompilerBackendLoweringPhase11Tests.cs` covers the Phase11 contract guard.
- Production lowering to executable lane6 DSC fails while gate is closed.
- Production lowering to executable `ACCEL_*` fails while gate is closed.
- Descriptor preservation/validation tests pass.
- Model/test helper use is explicitly marked and cannot be selected by production lowering.
- Future lowering tests require capabilities and reject absent or partial capabilities.
- Tests reject assumptions about coherent DMA, partial success, and DSC1 stride/tile/scatter.

Documentation updates:
Compiler/backend documentation must state the current prohibitions and future capability requirements. Future implementation docs must describe exact lowering sequences, including submit, wait/poll/fence, flush/invalidate, and fault behavior.

Compiler/backend impact:
This phase is the impact contract. No production executable lowering under current contract. Future lowering is capability-gated after code, tests, and architecture approval.

Compatibility risks:
Premature lowering would generate programs that silently rely on unimplemented semantics. Capability names without implementation can also mislead downstream tools unless marked unavailable.

Exit criteria:
- Current prohibitions are documented.
- Allowed model/test behavior is separated from production lowering.
- Future capability-gated lowering prerequisites are explicit.

Blocked by:
Current executable gates. Production lowering is blocked by phases 02 through 10 and phase 12.

Enables:
Safe compiler/backend planning, conformance tests, and future production lowering only after architecture implementation is real.
