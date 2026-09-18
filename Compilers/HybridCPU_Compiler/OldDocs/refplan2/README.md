# Production-lowering refactor: implementation record

This directory is the authoritative plan and completion record for the
`HybridCPU_Compiler` to `HybridCPU_ISE` production-package boundary.

Status as of 2026-07-10: phases 00-11 are implemented and verified. Phase 12
closes the normal provider track and records the RFC-only boundaries. This does
not claim runtime execution authority, caller migration completion, or legacy
API removal.

## Non-negotiable authority boundaries

```text
carrier != execution
execution != publication
publication != authority
authority != commit
commit != retire
retire != evidence
evidence != production lowering
```

- The compiler never owns final runtime `LegalityDecision`.
- Runtime Legality A/B, execution, publication, commit, and retire remain
  runtime-owned for every production package.
- Typed-slot facts, sideband, descriptors, tokens, certificates, guard
  observations, and evidence are structural or host-owned evidence only.
- Descriptor/parser/helper success does not grant production execution rights.
- Unknown contours fail closed and cross-contour fallback is forbidden.
- DSC lane6 and L7-SDC lane7 remain distinct contours.
- VMX remains projection/no-emission; SecureCompute remains
  policy/admission/evidence-only.

## Implemented normal provider track

Each provider can be resolved only through an explicitly enabled production
profile, exact contour gate, complete artifact/golden/parity/evidence gates,
and a preserved runtime dependency map. A successful provider result is always
`RuntimeAuthorityPending`.

| Contour | Provider | Bounded scope |
| --- | --- | --- |
| `NativeVliwScalar` | `NativeVliwScalarProductionProvider` | Explicit scalar opcode subset. |
| `NativeVliwLoadStore` | `NativeVliwLoadStoreProductionProvider` | Explicit LSU subset with runtime memory/fault dependency. |
| `NativeVliwBranchControl` | `NativeVliwBranchControlProductionProvider` | Explicit branch/control subset. |
| `StreamEngineVector` | `StreamEngineVectorDirectTransferProductionProvider` | Direct contiguous `VLOAD`/`VSTORE` only. |
| `DmaStreamComputeLane6` | `DmaStreamComputeLane6ProductionProvider` | Descriptor-backed canonical lane6 DSC only. |
| `L7SdcLane7` | `L7SdcLane7ProductionProvider` | Descriptor-backed lane7 `ACCEL_SUBMIT` only. |

The compatibility shells remain in place. Public caller migration and legacy
API removal are deliberately outside this plan's completed implementation
scope.

## Phase status

| Phase | Status | Evidence |
| --- | --- | --- |
| 00 | Complete | Current-state boundary inventory updated after implementation. |
| 01 | Complete | `ProductionLoweringReadinessScanner` and Phase 01 source-scanner tests. |
| 02 | Complete | Golden artifact harness and positive/negative manifests. |
| 03 | Complete | Typed explicit gate model and gate tests. |
| 04 | Complete | Separate production-provider contract and exact-contour registry resolution. |
| 05 | Complete | Compiler-to-ISE decode/encode/lane/slot/runtime-pending parity harness. |
| 06 | Complete | Native scalar provider and boundary matrix. |
| 07 | Complete | Native load/store provider with runtime memory/fault dependency. |
| 08 | Complete | Native branch/control provider. |
| 09 | Complete | Scoped direct vector-transfer provider plus negative matrices. |
| 10 | Complete | Descriptor-backed DSC lane6 provider and hard-pinned lane6 parity. |
| 11 | Complete | Descriptor-backed L7-SDC lane7 provider and token-destination structural parity. |
| 12 | Complete | RFC-only contour boundaries, ADR/RFC docket, and final checklist. |

## Verification baseline

The implementation record is backed by these test families:

- `CompilerPhase01ReadinessSourceScannerTests` through
  `CompilerPhase11L7SdcLane7ProductionProviderTests`;
- `CompilerPhase02GoldenArtifactHarnessTests`;
- `CompilerPhase05CompilerToIseParityHarnessTests`;
- Phase 09 negative matrices for DSC, L7, VMX, SecureCompute, stream/vector,
  MatrixTile, fallback, and cleanup boundaries.

The Phase 12 audit ran the cumulative Phase 01-11 suite and the full
`CompilerTests` suite: 213/213 cumulative phase tests passed; `CompilerTests`
passed 684 tests with 1 existing skip. Results and the remaining RFC-only work
are recorded in [`12-rfc-only-and-exit-checklist.md`](12-rfc-only-and-exit-checklist.md).

## Reading order

Read phase 00 for the reconciled current state, phases 01-05 for the shared
guardrail/package infrastructure, phases 06-11 for the bounded providers, and
phase 12 for the final non-goals and RFC-only exit criteria.
