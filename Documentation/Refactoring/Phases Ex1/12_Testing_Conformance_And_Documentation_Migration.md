# Phase 12 - Testing Conformance And Documentation Migration

Status:
Current conformance and documentation migration guard implemented for policy.
Future executable feature implementation remains gated.

Scope:
Define the conformance strategy for current fail-closed/model-only behavior and future executable DSC/L7/IOMMU/cache/compiler features. Define when Future Design may migrate into Current Implemented Contract.

Current code evidence:
- Existing phase docs preserve fail-closed lane6 and L7 behavior.
- Audit baseline identifies tests for DSC descriptors, tokens, helper/runtime, L7 model APIs, conflict manager, cache surfaces, and compiler contract.
- Current code has helper/model APIs that require tests to prevent accidental interpretation as executable ISA.

Architecture decision:
Current contract:
- Tests must protect fail-closed/model-only behavior.
- Documentation must not promote Future Design to Current Implemented Contract without code, tests, and architecture approval.
- Phase12 conformance tests may verify traceability, migration policy, and claim-safety only; they are not evidence that executable DSC, executable L7, DSC2 execution, coherent DMA/cache, async overlap, IOMMU-backed execution, successful partial completion, or production executable compiler lowering exists.

Future gated:
- Every executable claim requires implementation tests, negative compatibility tests, fault/order/cache tests, compiler conformance tests, and documentation claim-safety tests.

Non-goals:
- Do not rewrite existing tests in this documentation phase.
- Do not retire fail-closed tests before compatibility policy is approved.
- Do not migrate docs based on design intent alone.

Required design gates:
- Test taxonomy for each feature.
- Compatibility-mode policy for old fail-closed tests.
- Documentation migration checklist.
- Traceability from architecture claims to tests.
- CI gate for claim-safety if available.

Phase12 traceability matrix:

| Ex1 phase | Conformance category | Current evidence boundary | Active tests |
|---|---|---|---|
| 00 | Architecture baseline and claim partition | Current/future documentation split; no executable claim | `Ex1Phase12ConformanceMigrationTests` |
| 01 | Current contract lock | Fail-closed/model-only global contract remains authoritative | `Ex1Phase12ConformanceMigrationTests` |
| 02 | Lane6 DSC fail-closed compatibility | Direct `DmaStreamComputeMicroOp.Execute` failure and `ExecutionEnabled == false` | `DmaStreamComputeTokenStorePhase03Tests`, `DmaStreamComputeAllOrNonePhase08Tests`, `Ex1Phase12ConformanceMigrationTests` |
| 03 | Token lifecycle and issue/admission model separation | Token store/admission APIs are model/future seams; no decode allocation and no normal executable issue | `DmaStreamComputeTokenStorePhase03Tests` |
| 04 | Retire publication and fault contour | Helper faults remain model/retire-style; commit-pending is not visible memory | `DmaStreamComputeRetirePublicationPhase04Tests` |
| 05 | Ordering/conflict service gate | Absent/passive conflict service does not install global CPU load/store authority or publish memory | `GlobalMemoryConflictServicePhase05Tests` |
| 06 | Addressing/backend and no-fallback gate | Physical and IOMMU backend resolver is explicit infrastructure, not current DSC/L7 execution | `AddressingBackendResolverPhase06Tests` |
| 07 | DSC2 parser-only and capability gate | DSC1 strict/current-only; DSC2 parser-only cannot issue tokens, publish memory, or production-lower | `DmaStreamComputeDsc2Phase07Tests` |
| 08 | All-or-none and progress diagnostics | `AllOrNone` is the only successful policy; progress/poll diagnostics do not publish memory | `DmaStreamComputeAllOrNonePhase08Tests` |
| 09 | Cache/prefetch non-coherent protocol | Explicit observer-routed invalidation exists; coherent DMA/cache remains rejected | `CachePrefetchNonCoherentPhase09Tests` |
| 10 | L7 model-only and fail-closed gate | `ACCEL_*` carriers fail closed, `WritesRegister == false`, fake backend remains test-only | `L7SdcPhase10GateTests`, `L7SdcDocumentationClaimSafetyTests` |
| 11 | Compiler/backend lowering contract | Descriptor/carrier preservation is separated from production executable lowering | `CompilerBackendLoweringPhase11Tests` |
| 12 | Testing conformance and documentation migration | Future claim migration requires approval, code, positive/negative tests, compiler/backend conformance, and claim-safety | `Ex1Phase12ConformanceMigrationTests` |
| 13 | Dependency order and downstream evidence guard | Parser/model/compiler/cache/backend surfaces cannot satisfy upstream executable gates | `Ex1Phase13DependencyOrderTests`, `Ex1Phase12ConformanceMigrationTests` |

Phase12 implemented guard tests:
- `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs`
  verifies this traceability matrix covers every Ex1 phase document.
- The same test file scans Current Contract / Current Implemented Contract
  sections for forbidden affirmative current claims.
- The same test file checks that fail-closed compatibility evidence remains
  active while the current contract holds.
- The same test file checks future migration cannot bypass architecture
  approval, code evidence, positive and negative tests, compiler/backend
  conformance, and documentation claim-safety.

Implementation plan:
Current Phase12 implementation:
1. Done: add a Phase12 conformance migration test slice that verifies
   traceability, claim-safety, fail-closed compatibility retention, helper/model
   separation, and future migration gates.
2. Done: update Phase12/ADR12 documentation evidence only for the added guard
   tests.

Future feature implementation plan remains:
1. Maintain fail-closed compatibility tests:
   - lane6 `DmaStreamComputeMicroOp.Execute` direct failure;
   - `ExecutionEnabled == false`;
   - direct `ACCEL_*` execution failure;
   - `WritesRegister == false` while current contract holds.
2. Add token lifecycle tests when phase 03 is implemented.
3. Add precise fault tests when phase 04 is implemented.
4. Add memory ordering litmus tests when phase 05 is implemented.
5. Add IOMMU/backend tests when phase 06 is implemented.
6. Add DSC2 parser tests when phase 07 is implemented.
7. Add all-or-none/progress tests when phase 08 diagnostics are implemented.
8. Add cache invalidation/flush tests when phase 09 is implemented.
9. Add L7 model-only and future executable tests when phase 10 is approved.
10. Add compiler/backend conformance tests for current prohibitions and future capability-gated lowering.
11. Add documentation claim-safety checks before migration.

Affected files/classes/methods:
- `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs`
- `HybridCPU_ISE.Tests/CompilerTests/*`
- L7 external accelerator tests
- cache/prefetch tests
- IOMMU/backend tests
- documentation under `Documentation/Stream WhiteBook/*`
- phase documents under `Documentation/Refactoring/*`

Testing requirements:
Required suites:
- fail-closed compatibility tests;
- token lifecycle tests;
- precise fault and multi-slot priority tests;
- memory ordering litmus tests;
- global conflict service tests;
- IOMMU backend and no-fallback tests;
- cache invalidation and flush tests;
- DSC2 parser and capability tests;
- all-or-none rollback and progress diagnostic tests;
- L7 model-only and future executable tier tests;
- compiler/backend conformance tests;
- documentation claim-safety tests.

Documentation updates:
Migration rule:
Future Design moves into Current Implemented Contract only after:
1. Architecture approval is recorded.
2. Code implementation lands.
3. Positive and negative tests pass.
4. Compatibility risks are resolved or documented.
5. Compiler/backend conformance passes and the compiler/backend contract is updated.
6. Documentation claim-safety passes.
7. Documentation is changed in the same or later reviewed change.
8. The old future-gated text is either removed, marked implemented, or replaced with new future gates.

Compiler/backend impact:
Compiler/backend tests must be blockers for any migration that enables production lowering. No lowering contract changes without conformance coverage.

Compatibility risks:
Changing docs before tests creates false architectural claims. Removing fail-closed tests too early hides regressions. Migration must be traceable and reversible until release.

Exit criteria:
- Test matrix exists for every Ex1 phase.
- Documentation migration rule is explicit.
- Future claims require code plus tests plus approval.
- Documentation claim-safety blocks forbidden current claims.
- Fail-closed compatibility tests remain active while the current contract holds.

Blocked by:
Feature-specific phases for implementation. The migration rule itself is not blocked.

Enables:
Conformance-driven implementation, safe documentation updates, and compiler/backend release readiness.
