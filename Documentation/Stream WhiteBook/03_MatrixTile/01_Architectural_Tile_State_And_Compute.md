# MatrixTile Architectural State and Compute

## Architectural state and validation

`MatrixTileArchitecturalTileRegisterFile` is the owner of guest architectural
tile state. A tile image is identified by its owner thread and tile identifier,
and consists of a canonical descriptor plus a packed byte image. The descriptor
validates non-zero rows, columns, element size, stride, and row-major layout;
zero and reserved encodings fail closed. Snapshot capture clones the image and
checks its descriptor and packed footprint before it can become execution input.

The core controls the register file and its retire and checkpoint operations.
StreamEngine/SRF rows, compiler objects, host state, and backend state may carry
or stage data, but none is architectural tile state. Owner identity is checked
at capture, retire, replay, and rollback; a tile identifier never authorizes
access across owners.

## Compute contour

`MTILE_MACC` and `MTRANSPOSE` are `MatrixTileCompute`. Their slot class is
`AluClass` for deterministic tile-compute placement, but that physical choice
does not make either operation a generic scalar, vector, or VectorALU
operation. Compute does not acquire ordinary LSU, StreamEngine, SRF, DSC,
Lane7, VMX, assist, or external-backend authority.

`MTILE_MACC` captures left and right source tiles plus the accumulator snapshot,
validates their descriptors and policies, stages the complete accumulator image,
and publishes that image only at retire. For output coordinate `(i, j)`, the
runtime applies the canonical row-major layout and ascending `k` order:

```text
acc = accumulator(i, j)
for k = 0 .. K - 1:
    acc = add(acc, multiply(left(i, k), right(k, j)))
stage(i, j) = finalize(acc)
```

`MTRANSPOSE` is a layout, shape, and alias operation, not a MACC arithmetic
variant. Its supported layout maps source `(row, column)` to destination
`(column, row)`. It permits an out-of-place destination or square in-place
transpose only, stages the complete destination image, and publishes it only at
retire.

## Runtime-owned numeric and layout policies

`MatrixTileNumericPolicy` ABI version 1 is runtime-owned and fingerprinted. It
selects source, accumulator, and publication formats; signedness; widening;
multiply/add; rounding; saturation; overflow; NaN, infinity, and denormal
handling; reproducibility; and exception policy. The supported profiles are:

| Profile | Source | accumulator and published format |
| --- | --- | --- |
| `SignedInt8ToInt32` | `INT8` | `INT32` |
| `UnsignedInt8ToUInt32` | `UINT8` | `UINT32` |
| `SignedInt16ToInt32` | `INT16` | `INT32` |
| `UnsignedInt16ToUInt32` | `UINT16` | `UINT32` |
| `SignedInt32ToInt64` | `INT32` | `INT64` |
| `UnsignedInt32ToUInt64` | `UINT32` | `UINT64` |
| `SignedInt64ToInt64` | `INT64` | `INT64` |
| `UnsignedInt64ToUInt64` | `UINT64` | `UINT64` |
| `Binary32ToBinary32` | `FLOAT32` | `FLOAT32` |
| `Binary64ToBinary64` | `FLOAT64` | `FLOAT64` |

For integer profiles, source values are sign- or zero-extended to the
accumulator domain; multiplication is exact, summation uses an exact unbounded
intermediate, saturation is disabled, and overflow traps only when the final
accumulator encoding is produced. Integer rounding and floating exceptional
values are not applicable. Integer failure becomes typed arithmetic-fault
capture.

For binary32 and binary64 profiles, the runtime uses software IEEE-754
arithmetic with separately rounded multiply and add, round-to-nearest
ties-to-even, canonical quiet NaN, preserved infinity and denormals, and a
deterministic little-endian representation. It does not use host floating point
arithmetic, fused multiply-add, or a host matrix library. Integer and binary
element codecs are canonical little-endian. Formats outside the table,
including float16, bfloat16, float8, custom, mixed-width, mixed-sign, and
quantized profiles, are unsupported and fail closed.

`MatrixTileLayoutPolicy` ABI version 1 is also runtime-owned and fingerprinted.
The only executable layout policies are canonical packed row-major with
ascending K for MACC and canonical packed row-major coordinate permutation for
transpose. Column-major, blocked, interleaved, missing, tampered, descriptor-
mismatched, and operation-mismatched policies fail closed. Descriptor layout
does not replace the explicit policy.

## Capture, retire, replay, and rollback

Compute capture is policy-bound and architecturally invisible. Its identity
binds the core and owner, opcode and operation, descriptors, numeric/layout ABI
versions and fingerprints, resource and slot contour, snapshots and footprint,
core-owned replay epoch, dependency fingerprint, and publication surface.

Retire revalidates this identity before the single permitted publication. It
rejects stale, duplicate, wrong-owner, wrong-descriptor, wrong-policy,
wrong-resource, wrong-epoch, wrong-dependency, and wrong-publication-surface
captures. Faulted or cancelled captures retire as faults without publishing an
image. Rollback restores only core-owned checkpoints and cannot expose a partial
MACC or transpose result; replay consumes the registered retired journal and
revalidates the same identity.

Compiler sidebands are only the transport of the runtime-owned policy identity.
They are not arithmetic, layout, tile-state, capture, retire, replay, or
rollback authority.
