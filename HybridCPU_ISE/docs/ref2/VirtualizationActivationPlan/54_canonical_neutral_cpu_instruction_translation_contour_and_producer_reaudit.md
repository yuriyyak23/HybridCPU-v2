# Phase 54 — canonical neutral CPU instruction translation contour and producer re-audit

Status: `CLOSED GREEN / SUBJECT AND LATER NON-SELF-REFERENTIAL EVIDENCE`.

Provenance: subject `284ed3e6454bdbc8eabb93e97db97122da13a9da`,
tree `2b9d6c94d19bb41e23337461f8bfab10745be236`, and later
`2026-08-13-phase54-canonical-neutral-cpu-instruction-translation-clean-evidence.json`.

## Authorization and bounded scope

The repository owner explicitly authorized one CPU-owned neutral instruction
translation contour for production instruction fetch and scalar load/store,
followed by one neutral translation-fault completion producer. Identity
addressing remains the construction default; bounded region translation is
enabled only by an explicit immutable `CpuInstructionTranslationPolicy`.

This phase does not authorize VMX, VMCS, VMREAD, VMWRITE, IOMMU, DMA, device,
nested, SecureCompute, or compiler authority. It does not promote
`PageFaultException` and does not reuse EPT-shaped or compatibility facts.

## 2026-06-11 Audit Contract

- File name: `54_canonical_neutral_cpu_instruction_translation_contour_and_producer_reaudit.md`.
- Purpose: Implement and re-audit one canonical CPU instruction fetch/scalar load/store translation contour and one exact neutral completion producer.
- Status: Implemented in the worktree; full verification, subject provenance, and later non-self-referential evidence are pending.
- Scope: CPU instruction fetch and scalar load/store translation, typed fault arbitration, and existing completion-owner publication only.
- No-goals: VMX/VMCS/VMREAD/VMWRITE authority, IOMMU/DMA/device/nested reuse, SecureCompute/compiler changes, or adjacent compatibility fields.
- Code anchors: `CpuInstructionTranslationContour.cs`, `CPU_Core.PipelineExecution.CpuTranslation.cs`, `CPU_Core.PipelineExecution.StageFlow.cs`, `MicroOp.LoadStore.cs`, `CPU_Core.Pipeline.Helpers.cs`, and `ArchitecturalCompletionCommitOwner.cs`.
- Authority owner: `CpuInstructionTranslationOwner`, existing stage-aware precise exception arbitration, and existing `ArchitecturalCompletionCommitOwner`.
- Required RFC/ADR: The repository-owner explicit bounded authorization is recorded by this phase; compatibility projection/read still requires a separate authorization and exact field-coverage decision.
- Acceptance criteria: Identity default, production callers, exact typed provenance, deterministic arbitration, squash/replay/restore closure, mandatory commit fail-fast, cross-gates, Release, forbidden scans, and clean evidence.
- Tests/static scans: Phase 54 focused positives/negatives, Phase 52 lifecycle races, Phase 40–54 guards, full VMX and SecureCompute matrices, Release without test hooks, ignored/untracked source scan, compatibility-authority scan, and `git diff --check`.
- Risks: Accidental default activation, generic page-fault promotion, IOMMU/nested authority borrowing, stale completion publication, non-deterministic same-window selection, or implicit VMREAD expansion.
- Next-gate dependency: Separate completion-backed VMREAD projection/read authorization and explicit resolution of incomplete GuestPhysicalAddress/EPT-violation qualification coverage.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Repeated E0 from current production source

Phase 53 remains the historical blocked E0 for the source tree that existed
before the explicit contour authorization. E0 was repeated from scratch after
the new typed production seam was implemented:

- `PipelineStage_Fetch` creates an owner-issued fetch identity, translates the
  virtual PC, reads the resulting CPU physical address, and preserves the
  virtual PC as architectural identity;
- `LoadMicroOp` and `StoreMicroOp` translate only scalar CPU memory operations
  before the existing bound-main-memory or memory-controller access;
- Stage-B attempt/event identity is reused where present; a CPU-owner fallback
  identity is issued only for production legacy paths that have no Stage-B
  carrier, and a distinct memory-operation identity binds access kind and
  virtual address;
- typed faults carry exact domain/context/VT, access kind, virtual address,
  access size, attempt/event/memory-operation identity, owner epoch, reason,
  neutral qualification, and neutral auxiliary facts;
- `TryResolveStageAwareExceptionWinnerMetadata` remains the single precise
  exception arbitration owner: WB precedes MEM, MEM precedes EX, and the
  established per-stage lane order chooses same-window winners;
- speculative scalar faults are squashed without publication; committed fetch
  or scalar winners use the existing `ArchitecturalCompletionCommitOwner` and
  fail fast if its mandatory commit is denied.

No parallel translation-fault manager or completion owner was introduced.
The instruction cache now reads through the CPU core's bound physical memory
surface, removing the former accidental dependency on the global memory
object's DMA/IOMMU read entry point.

## Lifecycle and publication model

The producer is registered once with the existing completion owner for the
exact `TranslationFault` class, required reason, allowed qualification,
`VirtualAddress` semantic, and `TranslationFault` auxiliary semantic. A
successful commit installs the existing issuer-sealed receipt and neutral
observation snapshot. Restore and architectural owner replacement use the
Phase 52 linearization and remove observations, live receipts, and replay
identities. Pipeline flush and restore clear pending fetch faults; a new
committed completion is required before observation becomes present again.

## Exact completion-field coverage re-audit

The producer proves neutral reason, qualification, virtual fault address, and
translation auxiliary facts. It does not prove `GuestPhysicalAddress` or an
EPT-violation qualification, and no compatibility mapping is accepted here.
Therefore the four-field completion-backed VMREAD candidate remains blocked:
no SpecV2, AcceptanceRecordV2, production VMREAD composition, receipt, registry,
PRF/writeback delivery, or compatibility authority is opened by this phase.

## Verification boundary

The subject boundary passed focused positive/negative tests, repeated
restore/arbitration races, the full VMX and SecureCompute matrices, Release
without `TESTING` or internal test hooks, forbidden scans, `git diff --check`,
and worktree forbidden scans. Current results are focused Phase 40–54 `68/68`,
Phase 54 `13/13`, five race runs of `3/3`, VMX `518/518`, SecureCompute
`404/404`, and Release `0 errors / 0 warnings`. Clean-source verification,
The exact clean detached subject tree additionally passed VMX `500/500`,
SecureCompute `395/395`, and Release `0 errors / 0 warnings`. The subject and
later non-self-referential evidence provenance are closed.

## Next candidate

There is no automatic compatibility expansion. The next candidate remains
closed until a separate completion-backed VMREAD projection/read authorization
and an explicit decision on the still-incomplete exact field coverage.
