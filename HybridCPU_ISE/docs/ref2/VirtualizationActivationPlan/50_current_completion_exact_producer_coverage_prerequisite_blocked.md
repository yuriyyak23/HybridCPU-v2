# Phase 50 — current-completion exact producer coverage prerequisite

Status: `BLOCKED-PREREQUISITE / NO NEW PRODUCER / NO D2 / NO PRODUCTION`.

Phase 49 established that the exact four-field operation cannot proceed until
registered neutral producer coverage exists for every field. This bounded audit
searched the real CPU retire, memory translation, IOMMU, nested composition, and
compatibility projection callers. It does not authorize any of those subsystems
to expand.

The only production architectural-completion producer remains
`CanonicalPipelineTrapEntryProducer`. Its canonical retire facts contain an
RISC-V `mcause` reason, absent qualification, at most a virtual-address fact, and
absent auxiliary data. No owner-approved mapping converts that neutral cause to
the frozen `VmExitReason` vocabulary.

`NestedTranslationResult` and `TranslationViolationInfo` contain typed guest
physical address and second-stage qualification facts. However, their production
contour ends inside IOMMU/nested translation helpers. No CPU pipeline or retire
caller submits those facts to `ArchitecturalCompletionCommitOwner`.
`NestedMemoryCompositionService` has no production caller. The only consumer of
`QualificationBits` that maps an exit reason is compatibility
`NestedExitMapper`, which is explicitly not runtime authority.

Creating a CPU translation-fault completion producer would require a separate
neutral owner/policy decision covering exact CPU memory-fault admission,
domain/context/VT and attempt/event identity, canonical retire ordering, restore,
and producer-specific facts. Wiring the existing IOMMU/nested helper directly
would be memory/IOMMU/nested expansion and is not authorized by the current pool.

Consequently Phase 50 creates no producer, registration, mapping, runtime caller,
SpecV2, AcceptanceRecordV2, VMREAD receipt, or production VMREAD composition.
No surrogate authority is inferred from typed helper data or compatibility code.

## 2026-06-11 Audit Contract

- File name: `50_current_completion_exact_producer_coverage_prerequisite_blocked.md`.
- Purpose: Determine whether exact producer coverage can be established from existing reachable production contours without forbidden expansion.
- Status: Blocked prerequisite; no canonical CPU translation-fault completion producer exists.
- Scope: Producer/field-semantic prerequisite for the exact Phase 49 four-field candidate only.
- No-goals: No new memory/IOMMU/nested producer, VMREAD D2/composition, VMWRITE, compiler, SecureCompute, device/lane/stream expansion.
- Code anchors: `CPU_Core.StateData.cs`, `CPU_Core.PipelineExecution.Retire.cs`, `IOMMU.DomainBinding.cs`, `NestedTranslationResult.cs`, and `NestedExitMapper.MemoryComposition.partial.cs`.
- Authority owner: Existing neutral completion owners only; translation helpers and compatibility mappers are not completion authority.
- Required RFC/ADR: Separate authorization for a neutral canonical CPU translation-fault completion producer before any implementation work.
- Acceptance criteria: A real CPU production caller, exact producer policy, canonical retire commit, complete neutral facts, restore/race closure, and no compatibility dependency.
- Tests/static scans: Producer-registration count, CPU retire reachability, translation-helper callers, compatibility-only qualification consumer, machine status, and no-side-authority checks.
- Risks: Promoting an uncalled helper, IOMMU result, nested mapper, or compatibility enum into architectural authority.
- Next-gate dependency: Explicit separate authorization for the neutral CPU translation-fault completion producer contour.

Owner map completeness remains unresolved at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
