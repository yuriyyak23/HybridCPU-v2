# BurstIO And Memory Backends

## Role

`BurstIO` plans and executes bounded memory movement for StreamEngine:

- maximum AXI-style burst length;
- 4 KiB boundary splitting;
- contiguous and strided transfer;
- 2D and indexed helper paths;
- explicit `MemorySubsystem` binding;
- exact SRF bypass and overlap invalidation.

`BurstPlanner` owns segmentation math. `IBurstBackend` implementations own the
actual read/write operation.

## Completion

Incomplete non-empty transfer is an error, not partial success. Invalid 2D
geometry fails closed. Successful writes invalidate overlapping SRF windows.

Large helper transfers may route through `DMAController`, but the current helper
drives completion synchronously. This does not prove architectural async
CPU/DMA overlap.

## Backend Boundaries

The active backend may be `MemorySubsystem`, IOMMU-backed infrastructure, a
physical backend, or a test backend depending on the owning contour. Backend
availability alone does not authorize:

- DSC execution;
- MTILE publication;
- coherent DMA/cache;
- successful partial completion;
- L7 command commit.

MTILE uses a typed adapter and retire-owned store commit. DSC uses its own
backend/token contour. They do not inherit generic BurstIO write authority.
