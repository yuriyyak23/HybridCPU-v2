# Phase 6 FEATURE-000 - Executable Lane6 DSC Gate

Status date: 2026-04-29.

Status: open future architecture gate; not approved for implementation.

Related decision: Phase 3 selected Option A, so current lane6
`DmaStreamComputeMicroOp` remains a fail-closed descriptor/decode evidence
carrier.

## Scope

This gate exists only if a future architecture decision reopens Option B:
making lane6 `DmaStreamComputeMicroOp` executable.

Until that approval exists:

- do not change `DmaStreamComputeMicroOp.Execute(...)` to invoke runtime helper
  or DMA behavior;
- do not allocate DSC tokens from normal pipeline issue/execute/retire;
- do not make production compiler/backend lowering depend on executable DSC;
- do not migrate Future Design text into Current Implemented Contract.

## Required Architecture Decisions

An implementation plan for executable lane6 DSC must specify all of the
following before code changes:

- pipeline stage for execution;
- token allocation point;
- runtime invocation point;
- commit and retire boundary;
- fault priority and precise exception path;
- replay, squash, trap, and context-switch behavior;
- memory ordering against CPU loads, stores, atomics, and fences;
- physical versus virtual/IOMMU addressing;
- synchronous versus async completion semantics;
- compiler/backend lowering contract;
- conformance tests and migration sequence.

## Compatibility Gate

Executable lane6 DSC is breaking relative to the current fail-closed baseline.
The approval record must define:

- whether old fail-closed tests become compatibility-mode tests or are retired;
- how current explicit runtime/helper tests map to architectural execution;
- how DSC1 ABI compatibility is preserved or versioned;
- how WhiteBook Current Implemented Contract text is migrated after code lands;
- how residual Phase12 compat-freeze risk is kept separate from Stream/DMA
  regression analysis.

## Minimum Test Plan

No tests should be changed to expect executable DSC until this gate is approved.
Once approved, the dedicated implementation phase must include at least:

- positive copy/add/mul/fma/reduce execution tests;
- negative direct-execute fail-closed compatibility tests where applicable;
- token allocation and retire publication tests;
- fault priority tests across multi-slot VLIW bundles;
- replay/squash/trap/context-switch cancellation tests;
- all-or-none commit and rollback tests;
- CPU load/store/atomic/fence ordering litmus tests;
- physical/virtual/IOMMU addressing tests matching the approved model;
- compiler/backend conformance tests for production lowering;
- documentation claim-safety tests proving future text did not move early.

## Exit Rule

This gate remains open and non-executable until an explicit architecture
approval selects Option B and creates a dedicated implementation phase.
