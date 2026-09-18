# 10 — LLVM semantic mapping and analysis bridge

## Goal

Define a semantic firewall from LLVM IR into Canonical HybridCPU IR and import only analysis facts whose precision/provenance is understood.

## Required semantic mapping

Map integer/floating/pointer types, address spaces, PHI/select, CFG, calls, loads/stores, volatile and atomic operations, fences, casts, GEP, `undef`/poison/freeze semantics, overflow/exact flags, function/call attributes, intrinsics and source locations. Unsupported semantics fail closed or lower to an explicitly verified helper.

The mapping specification is normative and table-driven: each accepted LLVM opcode/intrinsic/attribute records its Canonical IR expansion, side effects, possible faults/traps, memory-order requirements, target capability requirements and losslessness status. “Ignored metadata” must be explicitly classified as non-semantic for the supported language/toolchain profile.

## Analysis facts

LLVM alias/TBAA/range/alignment/profile/loop facts are evidence with provenance and precision. Use only facts obtainable through the selected LLVM C API/LLVMSharp surface or explicit pass-produced metadata. Do not design against internal C++ analysis objects. `MayAlias` remains conservative unless a trusted static proof within the HybridCPU compiler establishes a stronger result; profile data never proves `NoAlias`.

Imported evidence uses a trust lattice such as `ProvenByIRSemantics`, `FrontendStaticEvidence`, `ProfileOnly`, `Unknown`. Only the first two may participate in a stronger static proof, and only when the HybridCPU-side proof rule explicitly consumes them. Profile-only evidence is profitability-only.

## Contracts

Imported facts attach to Canonical IR as frontend-neutral evidence, never LLVM handles. Core revalidates HybridCPU opcode/resource/capability legality independently.

No LLVM `DataLayout`, target triple, alignment annotation, dereferenceability attribute or alias scope may override an incompatible Phase 08 target fact. Unsupported address spaces, atomics/orderings, convergent/noduplicate semantics, EH constructs, GC statepoints or target intrinsics fail before scheduling unless a dedicated verified lowering contract exists.

## Tests / acceptance

Differential semantic tests include poison/overflow, volatile, atomics, address-space mismatch, opaque pointers, unsupported intrinsics and alias negative controls. Strip all optional LLVM facts and verify legality remains correct, though profitability may change.

Add negative controls proving: profile/TBAA cannot convert unresolved `MayAlias` to `NoAlias`; unknown metadata cannot relax memory/control dependencies; incompatible target `DataLayout` cannot be imported by coercion; potentially trapping operations cannot cross guards merely because LLVM marked them profitable to speculate.

**Acceptance:** LLVM IR can be translated without semantic weakening and without granting LLVM analysis runtime or target legality authority; every semantic loss is either an explicit verified lowering/helper or a deterministic rejection.
