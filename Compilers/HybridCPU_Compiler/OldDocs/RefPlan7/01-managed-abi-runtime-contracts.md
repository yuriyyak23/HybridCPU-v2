# Phase 1 — Managed/platform ABI, bootstrap and minimal RuntimeKernel substrate

## Goal

Evolve the existing managed ABI family and establish the minimum image/bootstrap/kernel contracts required to execute runtime helpers safely before object allocation begins.

## Prerequisites

Phase 0 is closed for all mechanisms used here.

## Existing authority to evolve

`Core/Target/Managed/HybridCpuManagedAbiContractsV1.cs -> HybridCpuManagedAbiFamilyV1` is the baseline. Do **not** introduce a competing `ManagedAbiV1` authority. Qualify and extend its existing call/reference/GC/safepoint/helper/thread/EH/code-manager subcontracts.

Create or extract a neutral versioned contract boundary such as `HybridCPU.Platform.Contracts` for POD/constants/schema only. The production compiler must depend on architecture/contracts, not ISE execution implementation.

## Compiler / image contracts

Define and test:

- managed call ABI and hidden argument rules;
- frame descriptor identity and final native-PC mapping;
- runtime helper symbol namespace and deterministic helper imports;
- code-manager registration format;
- stack-walk/unwind record schema: CFA/SP rule, saved registers, return PC, frame kind;
- `.hcexe` managed runtime bootstrap descriptor;
- runtime/helper/static-root metadata sections and version/feature digests;
- module/static initialization registration;
- failure on unsupported signatures before machine emission.

Current `HybridCpuRestrictedImageBuilderV1` rejects runtime helper symbols and executable static initializers; Phase 1 must intentionally replace that restriction with a versioned managed bootstrap contract before Phase 3 can allocate objects.

## Minimal RuntimeKernel V1 substrate

Introduce only language-neutral execution environment responsibilities required early:

```text
Boot(image, bootInfo) -> ExecutionContext
current_context()
reserve/commit/protect/release_vm(...)
trap_entry(ArchitecturalTrapFrame)
trap_return(...)
host_transition(service, request)
process_exit(code)
```

The initial execution context owns an initial stack, VM mappings, trap attachment and host/process boundary. Multiple managed threads, scheduling, TLS objects and wait/wakeup remain Phase 11.

## Runtime startup contract

Define deterministic sequence:

```text
image load
-> kernel boot/context + stack/VM
-> runtime registration/code manager
-> runtime heap/static-root setup
-> module/type initialization registration
-> managed entry
-> process_exit
```

Define startup failure semantics separately for image mismatch, runtime contract violation, kernel failure and managed exception.

## Forbidden ownership

- RuntimeKernel does not parse TypeDescriptor, GC stack maps or EH clauses.
- ISE does not parse managed/image runtime metadata.
- Managed Runtime does not control retire/MMU implementation internals.
- Compiler does not call ISE scheduler/execution classes in the production managed path.

## Tests and closure

Require ABI golden tests, helper resolution/missing-helper negatives, managed image load/version mismatch, code-manager registration, stack-walk record round-trip, startup/static-init registration, kernel boot/trap transition mocks, deterministic metadata ordering and dependency-boundary tests.

Phase closes only when a minimal managed image can enter runtime bootstrap and call a versioned runtime helper through the new image/ABI contract.