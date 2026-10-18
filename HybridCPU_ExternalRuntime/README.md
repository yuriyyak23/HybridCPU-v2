# HybridCPU external runtime

Fail-closed implementation of the versioned external facade. Runtime package
1.1.0 retains the bounded ordinary-domain backend qualification from 1.0.1 and
adds the parent-bound child-domain admission owner.

The child owner enforces authority subsets, opaque lease/epoch identity,
legal Start/Park/Resume transitions, bounded non-overlapping guest mappings,
monotonic event/trap sequences, replay-resistant operation identities,
mapping-before-child-before-parent teardown, and exact terminal receipts.
These families remain `RuntimeAdmission`; this package does not claim a
production hardware execution owner.
