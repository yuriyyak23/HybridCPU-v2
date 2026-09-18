# Phase 0 — Current Compiler / ISE conformance gate

## Goal

Prove the actual compiler and ISE mechanisms required by all later managed phases. No managed object/runtime implementation starts while this gate is open.

## Baseline and source-of-truth rule

Refresh and record exact Compiler/ISE branch + HEAD SHA at phase start. Current audit baseline is Compiler `be9295ea3b167d442512d7d4caab5e18888e7271` and ISE `38bf0614d8a58e2543b4a956ccc23bb22e1a8170`.

Code and tests override RefPlan6/RefPlan7/README claims.

## Required Compiler closure

The active CIL -> Canonical IR path must prove, end to end:

- loops/backward branches are admitted and produce correct CFG/SSA/phi, or an explicit blocking diagnostic remains;
- phi construction/use/destruction across real loop edges;
- direct multi-method calls, nested calls and recursion;
- instance-method admission needed later for `this`/constructors;
- schedule-aware liveness and RA across calls;
- per-method frame lowering with spills and callee-saves in multi-function programs;
- caller/callee ABI parity, stack alignment and stack arguments for admitted signatures;
- symbols, relocations, static link and multi-method `.hcexe` generation;
- final-PC managed metadata can be generated only after final allocation/placement;
- NativeAOT/ILCompiler adapter path is proven by an executable test if it is claimed as production-ready.

Current evidence anchors to re-audit include:

- `Cil/RestrictedCilImporterV1.cs`;
- `Core/IR/Model/CanonicalIrContractsV1.cs`;
- `Core/IR/Model/ControlFlowGraph.cs`;
- `Core/IR/Analysis/IrValueLivenessPressureV1.cs`;
- `Core/IR/Allocation/HybridCpuScheduleAwareRegisterAllocatorV1.cs`;
- `Core/Target/HybridCpuNativeAbiContractV2.cs`;
- object/link/image writers and managed metadata finalizer.

The audit found current blockers: `RestrictedCilImporterV1` rejects backward branches/instance methods/EH/generics/PInvoke/.cctor, and the allocator rejects a relevant multi-function spill/callee-save frame case. These must be resolved or later phases remain blocked.

## Required ISE closure

Prove executable/conformance evidence, not opcode declaration, for:

- `JAL` direct call and normal return convention;
- `JALR` register-valued indirect transfer and nested call/return;
- scalar load/store;
- precise synchronous traps with exact faulting PC and no younger architectural publication;
- memory fault address + read/write/execute class where applicable;
- trap entry/return and VT identity;
- per-VT architectural state isolation, including x4/thread-pointer preservation if used later;
- MMU/TLB/PTW/page-permission behavior needed by kernel VM ownership;
- LR/SC/AMO and acquire/release/fence behavior;
- `ECALL`, WFE/SEV, timers/interrupts only if later phases intend to depend on them.

Current evidence anchors include:

- `HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.MemoryAndControl.cs`;
- `HybridCPU_ISE/CloseToHSL/Core/Execution/Scalar/BranchControl/CPU_Core.ControlFlow.cs`;
- `HybridCPU_ISE.Tests/tests/Phase09TrapCarrierFollowThroughTests.cs`;
- `HybridCPU_ISE.Tests/tests/Phase10AtomicAcquireReleaseOrderingTests.cs`;
- `HybridCPU_ISE.Tests/tests/Phase12FinalIsaIntegrationTests.cs`;
- `HybridCPU_ISE.Tests/ArchitectureAndExecution/IntraCoreSmt4WayTests.cs`.

Do not use stale paths such as `VirtualInstructionThreadScheduler.cs`, `InstructionBatchControlFlow.cs` or `LegacyDecisionRetireAdapter.cs` unless they again exist at the pinned HEAD.

## Mandatory tests

- real CIL loop + phi end-to-end;
- recursive multi-method program with spill/callee-save pressure;
- direct and indirect nested call/return;
- byte-identical repeated multi-method `.hcexe` builds;
- load/store protection/page fault exact PC/VA/access type;
- faulting instruction + younger effect non-retirement;
- VT isolation across flush/trap/context transitions;
- cross-VT ordering litmus for every primitive later claimed by Phase 12.

## Closure

Every assumption is marked `Proven`, `Partially proven`, `Not implemented`, `Contradicted` or `Hypothesis` with exact file/type/member/test evidence. Any dependency used by Phase 1 must be `Proven`; unresolved items are explicit blockers, not roadmap optimism.