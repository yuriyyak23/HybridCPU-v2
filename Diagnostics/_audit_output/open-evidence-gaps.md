# Open Evidence Gaps

- Matrix/tile physical ownership requires an architecture decision: LSU-owned memory load/store vs ALU-owned runtime capture with explicit memory-domain resources.
- VLOAD/VSTORE need an explicit current-status decision because tests still claim they are absent from OpcodeRegistry while registry rows exist.
- BurstIO fallback needs a policy decision: legitimate backend abstraction or hidden fallback that must fail closed outside controlled tests.
- IsMemoryOp consumers must be inventoried before replacing the predicate; the generated symbol inventory identifies call sites but not semantic intent.
- The audit did not edit production source. Any future code phase should first re-run the baseline after resolving SDK drift.
- Git history was intentionally not used as source authority; exact authorship/change chronology remains outside this audit.
