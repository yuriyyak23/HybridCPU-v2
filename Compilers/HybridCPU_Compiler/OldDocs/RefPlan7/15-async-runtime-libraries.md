# Phase 15 — Async and runtime libraries

## Goal

Enable task/async-style libraries using ordinary compiled CIL state machines plus managed runtime scheduling primitives; no async-specific compiler backend or ISA.

## Prerequisites

Managed EH/GC, Phase 11 threads/TLS/park-wakeup, Phase 12 synchronization and a deterministic/virtualizable timer service.

## Architecture

C# async state machines are ordinary IL. The compiler only needs the general managed CIL/type/call capabilities already implemented.

Managed Runtime/library work must define a bounded V1 for:

- `Task`/completion state;
- continuation queue;
- bounded ThreadPool or cooperative worker queue;
- cancellation semantics admitted by the library subset;
- execution-context propagation policy;
- timer/deadline queue.

RuntimeKernel provides generic wait/wakeup and monotonic time/deadline primitives. Prefer cooperative timer queues; architectural preemption is **not** a prerequisite.

## Timer contract

```text
monotonic_ticks()
sleep_until(deadline)
```

Time must support deterministic virtual/mock operation for tests. If preemption is later required, qualify generic timer/interrupt semantics separately rather than adding async opcodes.

## Tests

Continuation success/failure, exception propagation, cancellation, timer completion under virtual time, GC while continuations/Tasks are queued, bounded worker scheduling, execution-context policy, deterministic scheduler tests, and end-to-end `async` C# through the normal CIL pipeline.

## Closure

Async works because the general runtime platform is sufficient; no phase-specific ISA or backend special case exists.