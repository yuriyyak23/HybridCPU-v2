# 11 — LLVM optimization pipeline and semantic firewall

## Goal

Use mature LLVM language-independent optimizations where useful while preserving HybridCPU-specific legality and scheduling ownership.

## Allowed role

Optional LLVM passes may simplify SSA/CFG, canonicalize loops, perform standard scalar/loop transformations and expose profile/alias facts before Canonical IR import. The exact ordered pass pipeline, pass parameters, LLVM version and profile hash/absence are part of provenance.

A production LLVM pipeline is a pinned deterministic recipe. Auto-tuning or pass-pipeline search may exist only in research/CI and cannot choose production output through wall-clock limits.

## Forbidden role

LLVM MachineScheduler, LLVM backend register allocation, target itinerary/resource scheduling and host-target lowering are not substitutes for HybridCPU scheduling, W=8 placement, register-group/PRF constraints, VT/FSP/VDSA or runtime legality.

Do not build a pseudo-HybridCPU LLVM backend merely to access C++ CodeGen internals through unsupported bindings. If a future real LLVM target backend is ever pursued, it is a separate project and still cannot bypass the canonical HybridCPU legality/evidence contracts of this platform.

## Semantic firewall

- Validate module before and after optimization.
- Preserve Phase 08 DataLayout/memory/atomic semantics.
- Reject transformations that introduce unsupported intrinsics/address spaces/ABI constructs.
- Treat profile as profitability only.
- Canonical IR importer re-establishes HybridCPU dependencies/resources after optimization.
- Re-run the Phase 10 semantic-support table after the last LLVM pass; unsupported constructs cannot leak into Canonical IR because they appeared after initial validation.
- Optimization never marks HybridCPU runtime legality, Stage A/B admission, FSP/VDSA eligibility or replay/freshness state.
- Any LLVM transform that changes CFG/memory/value lifetimes invalidates imported pre-transform analysis evidence unless an explicit preservation rule exists.

## Tests / rollout

Pipeline default-off initially; compare optimized vs unoptimized functional results and provenance. Run metamorphic tests with pass reorderings supported by policy and ensure deterministic selected pipeline produces identical output.

Add negative controls where LLVM legitimately performs aggressive alias/loop/speculation transforms but the imported program still fails or conservatively schedules under HybridCPU-specific guard, memory, lane or certificate rules. Verify that disabling the LLVM adapter leaves native outputs byte-identical.

## Acceptance metrics

Measure frontend IR size, Canonical IR size, HybridCPU schedule cycles/bundles, final code size and runtime cycles/IPC. An LLVM pass-pipeline win is accepted only when downstream HybridCPU metrics improve or remain within declared thresholds; LLVM IR instruction-count reduction alone is not a success criterion.

**Acceptance:** measurable generic optimization benefit on LLVM inputs with zero authority-boundary violations, deterministic pinned pipeline provenance, and no native-profile dependency or codegen delta.
