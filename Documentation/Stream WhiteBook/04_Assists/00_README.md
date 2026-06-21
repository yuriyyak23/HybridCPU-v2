# Assist Plane

Assists are architecturally invisible, non-retiring, replay-discardable
MicroOps for bounded warming.

## Taxonomy

| Kind | Execution mode | Carrier |
| --- | --- | --- |
| `DonorPrefetch` | cache prefetch | `LsuHosted` |
| `DonorPrefetch` | SRF prefetch | `Lane6Dma` |
| `Ldsa` | cache prefetch | `LsuHosted` |
| `Ldsa` | SRF prefetch | `Lane6Dma` |
| `Vdsa` | SRF prefetch | `Lane6Dma` |

LSU-hosted assists use `LsuClass`. Lane6 assists use `DmaStreamClass`; they do
not use `MatrixTileStreamClass`.

## Visibility

Assists may change cache/SRF residency and telemetry. They do not:

- write architectural registers;
- retire;
- publish architectural faults;
- execute VectorALU;
- accept DSC or MTILE descriptors;
- commit memory;
- become replay or owner authority.

## Admission

Admission validates tuple legality, owner/context/domain, donor identity,
replay and assist epochs, carrier placement, quotas, backpressure, and SRF/cache
partition budgets.

Lane6 SRF warming may use StreamEngine prefetch and BurstIO backend reads.
Success remains a transient optimization, not architectural evidence.
