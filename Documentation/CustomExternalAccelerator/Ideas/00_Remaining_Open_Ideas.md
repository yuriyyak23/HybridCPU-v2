# Remaining Open Ideas

Status: residual idea backlog after L7-SDC canonicalization.

## Scope

This file preserves future research and implementation questions that are not
fully specified by the canonical L7-SDC, DmaStreamCompute, StreamEngine/SRF, or
VDSA assist documents.

It does not restate the current architecture. In particular, it does not make
legacy custom accelerator surfaces executable, does not redefine lane6
`DmaStreamCompute`, and does not turn StreamEngine/SRF/VectorALU or VDSA assist
into external accelerator authority.

## Canonical References

- `Documentation/CustomExternalAccelerator/00_L7_SDC_Executive_Spec.md`
- `Documentation/CustomExternalAccelerator/01_L7_SDC_Migration_Phases.md`
- `Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md`
- `Documentation/CustomExternalAccelerator/03_L7_SDC_Phase_Code_Audit.md`
- `Documentation/CustomExternalAccelerator/Phases/*.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/01_StreamEngine_SFR_SRF_VectorALU.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md`

## Remaining Ideas

### Quantitative accelerator workload selection model

- Idea: define compile-time and runtime-adjacent thresholds for when a workload
  should become an L7-SDC accelerator command instead of staying on CPU,
  StreamEngine, or lane6 `DmaStreamCompute`.
- Why not covered yet: the canonical compiler phase defines the legal selection
  boundary but does not define size, arithmetic intensity, synchronization,
  memory-footprint, or lane7 pressure thresholds.
- Relevant phase(s): phase 11, phase 12, phase 13, phase 15, and post-v1
  compiler performance work.
- Required authority/placement/commit constraints: the decision must happen
  before emitting `ACCEL_SUBMIT`; emitted commands still require lane7
  `SystemSingleton`, typed sideband descriptor, owner/domain guard, capability
  acceptance, `AcceleratorToken`, staged writes, and token commit.
- Risk if implemented incorrectly: a performance heuristic could become a
  hidden fallback path, or tiny kernels could saturate lane7 while producing
  worse branch/system progress.
- Suggested tests: compiler selection table tests; capability-present and
  capability-absent lowering tests; no-runtime-fallback tests; lane7 pressure
  regression tests using submit/poll density counters.
- Decision status: Open.

### Lane7 command pressure and batching threshold model

- Idea: define measurable thresholds for submit, poll, wait, cancel, and fence
  density so L7-SDC remains a coarse-grain command path rather than a per-vector
  or per-element issue stream.
- Why not covered yet: canonical docs require throttling and telemetry, but do
  not define a quantitative pressure model or batching policy.
- Relevant phase(s): phase 09, phase 12, phase 13, phase 15, and post-v1 batch
  command design.
- Required authority/placement/commit constraints: throttling evidence cannot
  authorize commands; batching must preserve sideband descriptor identity,
  owner/domain binding, mapping epoch, footprint normalization, conflict
  reservation, and per-token commit validation.
- Risk if implemented incorrectly: lane7 branch/system progress can regress,
  `WAIT`/`FENCE` can become common serializing operations, or batching can hide
  per-command faults and partial completion.
- Suggested tests: dense submit/poll storm tests; branch/system progress
  regression tests; per-command fault accounting under batch-like policy;
  telemetry tests proving throttle counters are evidence only.
- Decision status: Future.

### Device-private state virtualization for detachable tokens

- Idea: specify how an external accelerator backend exposes, checkpoints,
  discards, or virtualizes device-private state for detachable or suspendable
  tokens without making that state CPU architectural state.
- Why not covered yet: canonical L7-SDC names drain, cancel, detach, and suspend
  policies, but does not define backend interfaces for queues, scratchpad,
  tiling buffers, partial accumulators, or device pipeline state.
- Relevant phase(s): phase 05, phase 06, phase 07, phase 09, phase 15, and
  post-v1 backend virtualization work.
- Required authority/placement/commit constraints: any detached token remains
  owner/domain and mapping-epoch bound; resume and commit must revalidate guard
  authority; device-private state must not be observable as GPR, CSR, SRF, or
  architectural memory.
- Risk if implemented incorrectly: a stale command can complete under a
  different owner/domain, private device scratch can leak across contexts, or
  resumed state can bypass descriptor and footprint checks.
- Suggested tests: detach/resume after owner drift; mapping epoch drift during
  suspend; backend checkpoint corruption; cancel versus resume race; privileged
  diagnostic visibility without user-visible commit.
- Decision status: Open.

### Cross-contour long-operation checkpoint coordination

- Idea: keep a future coordination note for long lane6 `DmaStreamCompute`
  operations that may need chunk boundaries, cancel, drain, or checkpoint when
  they coexist with active L7-SDC tokens.
- Why not covered yet: DmaStreamCompute docs define token/commit and evidence
  rules, while L7-SDC docs mention coexistence and conflicts. A precise shared
  scheduling policy for chunk checkpoints is not currently specified here.
- Relevant phase(s): L7-SDC phase 10 and phase 15; future DmaStreamCompute
  scheduling and context-switch documentation.
- Required authority/placement/commit constraints: this note must remain owned
  by the lane6 DmaStreamCompute contract; L7-SDC must only observe conflicts
  through `ExternalAcceleratorConflictManager` and must not treat lane6 state as
  an external accelerator fallback.
- Risk if implemented incorrectly: lane6 can become a non-preemptible long
  operation, or cross-contour checkpointing can blur command ownership,
  conflict truth, and commit authority.
- Suggested tests: context switch with active lane6 token and active L7-SDC
  token; conflict reservation across checkpoint boundaries; cancel/drain race;
  no stale staged write publication after owner/domain drift.
- Decision status: Parked.

### Capability backlog beyond MatMul

- Idea: evaluate additional future capability providers after MatMul, such as
  convolution, cryptography/hash pipelines, compression/decompression,
  CRC/checksum, bounded sparse scan/filter, bulk reductions, and
  memcpy/memset-with-transform.
- Why not covered yet: canonical phase work only specifies MatMul provider
  migration in detail. Other operations need separate descriptor schemas,
  footprint rules, datatype policies, and backend evidence.
- Relevant phase(s): phase 02, phase 04, phase 07, phase 10, phase 12, phase
  13, and post-v1 provider work.
- Required authority/placement/commit constraints: every provider must remain
  metadata-only until descriptor ABI, owner/domain guard, conflict policy,
  staged write generation, token commit, telemetry, and no-fallback tests exist.
- Risk if implemented incorrectly: a provider can smuggle an underspecified
  operation into command acceptance, especially for pointer-heavy or
  branch-heavy kernels that do not fit a bounded descriptor.
- Suggested tests: provider metadata does not submit; unsupported shape rejects;
  descriptor footprint exactness; datatype and alignment rejects; no
  post-rejection scalar/vector/lane6 fallback.
- Decision status: Future.
