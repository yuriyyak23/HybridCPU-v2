# RefPlan7 — HybridCPU Managed C# AOT Platform

## Purpose and audited baseline

This roadmap is the dependency-correct transition from the current `ScalarControlFlowV2` compiler foundation to a managed C# AOT platform while preserving strict Compiler / Managed Runtime / RuntimeKernel / ISE authority boundaries.

Audit baseline used for this revision:

- Compiler: `yuriyyak23/HybridCPU_Compiler_v2`, branch `master`, HEAD `be9295ea3b167d442512d7d4caab5e18888e7271`.
- ISE: `yuriyyak23/HybridCPU-v2`, subtree `HybridCPU_ISE`, branch `master`, HEAD `38bf0614d8a58e2543b4a956ccc23bb22e1a8170`.
- RefPlan7 tree at audit: `76ecc8a5771942250e2a4017b31d527f6f0c8a7b`.

The source code and checked-in tests remain authoritative over this document. Every phase must refresh its evidence against the then-current HEADs before implementation starts.

## Authority model

```text
Compiler
  -> semantic admission, CIL import, Canonical IR/CFG/SSA, optimization,
     scheduling, ABI/frame lowering, final stack maps/EH/unwind metadata,
     symbols/relocations/object/link/image
Managed Runtime
  -> managed references, object/type model, heap/GC, dispatch, delegates,
     managed EH, static initialization, reflection and runtime helpers
RuntimeKernel / execution environment
  -> boot/execution contexts, stacks, VM ownership, VT carrier binding,
     TLS base, traps, blocking/wakeup, timers, generic host transitions
ISE / CPU
  -> ISA execution, architectural VT/register/PC state, MMU/TLB/PTW,
     atomics/order, traps/faults, privilege, replay, commit/retire
```

Managed metadata is never ISE legality/execution/retire authority. No managed-specific ISA extension is permitted without a failed conformance test proving that existing generic ISA mechanisms are insufficient.

## Corrected dependency order

0. [Conformance gate](00-conformance-gate.md)
1. [Managed/platform ABI, bootstrap and minimal RuntimeKernel substrate](01-managed-abi-runtime-contracts.md)
2. [Managed references, type identity, object/static layout](02-managed-references-object-layout.md)
3. [Heap allocator](03-heap-allocator.md)
4. [Arrays, strings, value types and type initialization completion](04-arrays-strings-value-types.md)
5. [Single-thread precise GC V1](05-gc-v1.md)
6. [Instance methods and virtual/interface dispatch](06-virtual-interface-dispatch.md)
7. [Delegates and function pointers](07-delegates-function-pointers.md)
8. [Generics core](08-generics-core.md)
9. [Managed EH core](09-managed-eh-core.md)
10. [Architectural fault/trap integration](10-fault-trap-integration.md)
11. [Managed threading, TLS and multi-thread GC rendezvous](11-runtime-kernel-threading-tls.md)
12. [Synchronization and managed memory model](12-synchronization-memory-model.md)
13. [Managed interop and host services](13-interop-host-services.md)
14. [Bounded public reflection](14-reflection-runtime-metadata.md)
15. [Async and runtime libraries](15-async-runtime-libraries.md)
16. [Release and determinism qualification](16-release-determinism-qualification.md)

Phase 1 deliberately introduces the minimal language-neutral kernel substrate needed by later allocation/trap/runtime execution. Phase 11 does **not** introduce the kernel from scratch; it adds managed-thread semantics, multiple contexts, TLS, blocking and GC rendezvous.

## Project/dependency rule

Target project boundary:

```text
HybridCPU.Compiler.ManagedAot ----\
HybridCPU.ManagedRuntime ---------+--> HybridCPU.Platform.Contracts
HybridCPU.RuntimeKernel ----------/
HybridCPU_ISE ---------------------> architecture/ISA contract only
```

`HybridCPU.Platform.Contracts` is a neutral, versioned POD/constants/schema boundary for NativeAbi, ManagedAbi, ImageAbi, RuntimeHelperAbi, KernelAbi and TrapAbi. It must not depend on compiler IR, runtime internals or ISE execution implementation. Production compiler code must not depend on ISE scheduler/execution implementation classes.

## Global architectural rules

- Evolve the existing `HybridCpuManagedAbiFamilyV1`; do not create a parallel managed ABI authority.
- Managed references/byrefs remain ordinary machine values to the CPU; lifetime/validity are runtime semantics.
- Internal runtime type metadata is required early for layout, dispatch, casts, generics and EH; public reflection remains Phase 14.
- GC V1 is precise, STW, non-moving, non-generational and non-concurrent.
- `JALR`-style generic indirect control transfer is the baseline for `callvirt`, delegates and managed function pointers.
- Managed EH and CPU traps remain separate mechanisms; mapping is selective policy at the runtime/kernel boundary.
- A HybridCPU VT is an execution carrier, not `System.Threading.Thread` semantic identity.
- Console/files/network/time are services behind a generic host boundary, never service-specific CPU instructions.
- LLVM is not a frontend/backend/runtime solution in this roadmap.

## Mandatory phase closure evidence

Every phase must check in a machine-readable evidence manifest containing, as applicable:

```text
CompilerSHA
ISESHA
RuntimeSHA
KernelSHA
AbiDigests[]
FeatureFlags[]
PositiveTests[]
NegativeTests[]
PropertyTests[]
CrossLayerTests[]
DeterminismTests[]
AuthorityBoundaryTests[]
ArtifactDigests[]
```

Evidence for stack maps, EH ranges, unwind data, relocations and calls must be derived from the **final lowered representation after scheduling, register allocation and frame lowering**. A feature is not complete merely because code exists or an earlier IR test passes.