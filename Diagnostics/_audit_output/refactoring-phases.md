# Refactoring Phases

## P0 - Freeze the Audit Baseline
Keep the generated manifests, inventories, and failing-test list as the local truth for this refactor. Do not mix functional edits with evidence regeneration.

## P1 - Add Runtime Invariant Guards
Add a small authority check around micro-op materialization/admission: if a micro-op publishes memory ranges or memory-class semantics, its slot class, ordering, and memory taxonomy must be intentionally declared. Start with assertions/tests, then promote to guardrails.

## P2 - Normalize Vector Memory
Fix VLOAD/VSTORE, segment loads/stores, and 2D vector memory so memory side effects use the same slot/resource vocabulary as VGATHER/VSCATTER or explicitly document a non-LSU runtime owner. Remove or fail-close silent store success when a store buffer is absent.

## P3 - Split Memory Semantics
Replace the overloaded IsMemoryOp predicate with separate concepts: memory footprint, memory side effect, ordering class, physical slot owner, and backend/runtime owner. Keep compatibility shims until all call sites are migrated.

## P4 - Decide Matrix/Tile Physical Authority
Choose whether MTILE_LOAD/STORE are LSU-owned memory operations or ALU-owned runtime-capture operations with explicit memory-domain resources. Then align MatrixTileMicroOps, InstructionRegistry descriptors, OpcodeInfo flags, fail-closed tests, and positive golden tests.

## P5 - Resolve Compiler Facade Handoff
Either move matrix helpers out of the deprecated App facade surface or update the ABI/deprecation contracts to declare the new positive handoff. Keep runtime legality final; compiler must not reopen closed contours by helper naming alone.

## P6 - Repair Test Authority
Update stale skips and negative tests after P2/P4 decisions. Add active tests for slot placement, memory ranges, IsMemoryOp replacement predicates, hidden fallback absence, and replay/retire publication.

## P7 - CI Hardening
Pin or install the SDK requested by global.json, capture test logs as artifacts, and add an audit script target that regenerates inventories without touching production source. Keep compatibility-only acceptance explicit and test-backed; the `ACCEL_SUBMIT` default-metadata bypass is now closed in decoder admission.
