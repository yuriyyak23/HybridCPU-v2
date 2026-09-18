# Phase 11 — Managed threading, TLS and multi-thread GC rendezvous

## Goal

Build managed `Thread` semantics and multi-context coordination on top of the Phase 1 RuntimeKernel substrate without equating a HybridCPU VT with a managed thread.

## Prerequisites

Kernel boot/context/trap/VM contracts, single-thread GC, managed EH and Phase 0 per-VT state evidence are stable.

## Mapping model

```text
System.Threading.Thread
 -> ManagedRuntime ManagedThreadContext
 -> RuntimeKernel ExecutionContext handle
 -> HybridCPU VT carrier binding
```

One runnable managed thread per VT may be a V1 deployment policy, not semantic identity.

## RuntimeKernel extensions

Add language-neutral multiple-context facilities:

```text
create/start/exit/join_context
current_context
park/unpark
wait-capable blocked/runnable state
stack region + guard ownership
TLS base/context carrier
VT binding/unbinding
gc_rendezvous_enter/leave
```

Kernel coordinates stopping/parking contexts; it never scans managed roots or interprets stack maps.

## Managed Runtime responsibilities

- managed Thread object/state and lifecycle;
- thread registry and managed exception termination behavior;
- ThreadStatic/TLS layout over the generic TLS base;
- GC state classification (runnable/parked/native-transition as later needed);
- all-thread root enumeration after rendezvous;
- resume protocol.

## TLS policy

The native ABI already reserves x4 as a thread-pointer carrier. First prove x4 is preserved/isolated per execution context/VT. A new TLS opcode or architectural field is forbidden unless that evidence fails and a concrete generic need is demonstrated.

## Tests

Create/start/join/exit, unique stacks/guard faults, x4/TLS isolation across context switches, exception terminating one thread, parked/runnable transitions, STW while threads run/park, all-thread root oracle, deterministic cooperative scheduling mode.

## Closure

Multi-thread GC can stop all managed contexts, enumerate roots in Managed Runtime, and resume without exposing ISE scheduler internals to `System.Threading`.