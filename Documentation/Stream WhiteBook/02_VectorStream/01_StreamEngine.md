# StreamEngine

## Role

`StreamEngine` is the in-core vector-stream orchestrator. It validates a
`StreamExecutionRequest`, resolves the supported operation/address mode,
strip-mines large streams, obtains operands, calls `VectorALU`, and moves
results.

## Ingress

`StreamExecutionRequest` carries opcode, datatype, predicates, pointers,
immediate, stream length, stride, row stride, indexed/2D flags, and vector
policies. Compatibility-only raw VT hints are discarded and cannot become
owner authority.

## Dispatch

The engine distinguishes:

- scalar length-one helper contours;
- predicate/mask operations;
- memory-visible vector operations;
- comparisons;
- FMA, reductions, scans, dot products;
- permutation, slide, compress, and expand;
- unit-stride, strided, 2D, and indexed movement.

Every family has explicit representability checks. Zero-length hidden success,
raw descriptor-less FMA, unsupported addressing, and stale scratch success are
rejected.

## Boundaries

`StreamEngine` is not:

- a standalone ISA class;
- a scheduler slot class;
- an architectural register file;
- MTILE retire authority;
- DSC descriptor/token authority;
- L7 command authority.

Generic `Execute(...)` and transport completion cannot publish MTILE state or
commit an MTILE store.
