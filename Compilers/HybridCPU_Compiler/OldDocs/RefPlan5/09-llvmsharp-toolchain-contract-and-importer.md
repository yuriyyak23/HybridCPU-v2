# 09 — LLVMSharp toolchain contract and fail-closed importer

## Goal

Add LLVM only as an optional frontend/middle-end dependency with a pinned, testable public-API contract.

## Dependencies

Phase 06 Canonical IR plus both Phase 08 gates: 08A TargetMachine/DataLayout/register facts and 08B object/platform semantics relevant to imported IR. LLVM integration must not be used to define missing HybridCPU target facts.

## Required work

- Pin an exact supported LLVM release line and matching LLVMSharp bindings; record LLVM C API version, LLVMSharp package/commit, native-library identity and module producer version in provenance.
- Maintain an explicit **support matrix** for every LLVM facility the adapter uses: C API symbol, LLVMSharp binding, minimum/maximum supported LLVM version and fallback/unsupported behavior.
- Put all LLVMSharp types in a separate adapter project.
- Accept `.ll`/`.bc`, validate module, target triple policy and Phase 08 DataLayout compatibility before import.
- Use only functionality actually exported by LLVM C API and surfaced by the selected LLVMSharp version. Internal LLVM C++ `TargetMachine`, `MachineInstr`, `MachineScheduler`, `MachineFunction`, backend register allocators, SelectionDAG/GlobalISel target hooks or private analyses are not assumed available.
- If a required C API is missing from LLVMSharp, add a narrowly scoped binding in the adapter only after verifying the symbol exists in the pinned LLVM C API; do not model an internal C++ API as if it were public C API.
- Define deterministic diagnostics for unsupported LLVM opcodes, attributes, metadata, intrinsics, address spaces and version skew.
- Dispose LLVM contexts/modules deterministically and isolate native-library loading from Core.
- Validate all imported types/alignments/address spaces against HybridCPU `DataLayout`; host-machine defaults are forbidden.
- Make importer limits deterministic by module/instruction/type/constant counts; no wall-clock timeout may select a different production lowering.

## Non-goals

No HybridCPU LLVM backend, no replacement of HybridCPU scheduler/placement, no CIL compilation via LLVMSharp, no dependency on LLVM CodeGen C++ internals.

## Tests / fallback

Native build/run is tested with LLVM packages/runtime absent. Importer version mismatch and incompatible DataLayout fail before Canonical IR creation. A disabled/unavailable LLVM adapter cannot affect native codegen.

Add CI against exactly the pinned supported LLVM/LLVMSharp combinations, plus deliberate one-version-too-new/old negative cases. Add symbol-surface tests proving every invoked native entry point belongs to the approved C API contract. Bitcode produced with opaque/unknown target extensions or unsupported intrinsics must fail closed with stable diagnostics.

**Acceptance:** pinned LLVM IR modules import reproducibly through a verified public C API/LLVMSharp surface; the adapter has a machine-readable version/API support matrix; no LLVM dependency or backend authority leaks into Core.
