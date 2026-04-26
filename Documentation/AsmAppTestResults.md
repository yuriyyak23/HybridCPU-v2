HybridCPU ISE diagnostics console
Primary runtime validation harness starting...

SPEC-like iterations for default SPEC-like matrix [250]: 10000
Configured SPEC-like iterations: 10,000
Wall-clock budgets will be auto-scaled from the prompted iteration count.

Enable extended telemetry logging? This writes heartbeat history and partial telemetry files. [y/N]: n
Telemetry logging: Minimal
Minimal logging will keep console-equivalent stdout/stderr, manifests, result metrics, and the latest heartbeat only.

=== Default SPEC-like diagnostic matrix ===
--- Running alu [NativeVLIW] ---
>>> Starting mode: SingleThreadNoVector [NativeVLIW]
SPEC-like iterations: 10,000
Mode: SingleThreadNoVector
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwSingleThread
Frontend supported: True
Elapsed: 00:00:44.8414187
Workload shape: spec-like-single-thread-int
Reference slice iterations: 36
Slice executions: 278
Reference slice instructions: 185
Aggregate retirement target: 51389
Diagnostics run completed.
IPC (retire-normalized): 3.6157
Raw cycle IPC: 3.3572
Instructions retired: 52221
Cycle count: 15555
Pipeline stalls: 0
Active cycles: 15555
Stall share: 0.00%
Effective issue width: 3.3572
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 833
Scalar issue width [1]: 834
Scalar issue width [2]: 1667
Scalar issue width [3]: 0
Scalar issue width [4]: 9165
Total bursts: 1434889
Bytes transferred: 11479112
NOPs avoided: 0
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 0
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 0
SMT legality rejects by class: ALU=0, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 0
Class-flexible injects: 0
Hard-pinned injects: 0
Slack reclaim ratio: 0.0000
Flexible inject share: 0.0000
Multi-lane execute count: 11666
Cluster prepared execution choices: 9165
Wide-path successes: 9165
Partial-width issues: 1667
Decoder prepared scalar groups: 9165
VT spread per bundle: 9165
Issue packet prepared lane sum: 43328
Issue packet materialized lane sum: 43328
Issue packet prepared physical lane sum: 54994
Issue packet materialized physical lane sum: 54994
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7276
Materialized scalar-lane occupancy per cluster choice: 4.7276
Prepared physical lanes per cluster choice: 6.0004
Materialized physical lanes per cluster choice: 6.0004
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 41110
Non-scalar lanes retired: 11111
Retire cycles: 14443
Retired physical lanes per retire cycle: 3.6157
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 185
Reference slice bundle count: 60
Compiler emitted distinct VTs: 1
Compiler IR distinct VTs: 1
Compiler schedule cycle groups: 60
Compiler schedule cross-VT cycle groups: 0
Compiler schedule avg width: 3.0833
Compiler schedule avg VT spread: 1.0000
Compiler schedule max VT spread: 1
Compiler bundle count: 60
Compiler cross-VT bundles: 0
Compiler bundle avg VT spread: 1.0000
Compiler bundle max VT spread: 1
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 6112
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:45.6132643
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=43, retired=145
Last observed core focus: VT=0, PC=0x2400
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running novt [NativeVLIW] ---
>>> Starting mode: WithoutVirtualThreads [NativeVLIW]
SPEC-like iterations: 10,000
Mode: WithoutVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwVectorProbe
Frontend supported: True
Elapsed: 00:00:36.1524399
Workload shape: spec-like-single-thread-vector
Reference slice iterations: 36
Slice executions: 278
Reference slice instructions: 186
Aggregate retirement target: 51667
Diagnostics run completed.
IPC (retire-normalized): 3.6157
Raw cycle IPC: 3.3572
Instructions retired: 52221
Cycle count: 15555
Pipeline stalls: 0
Active cycles: 15555
Stall share: 0.00%
Effective issue width: 3.3572
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 833
Scalar issue width [1]: 834
Scalar issue width [2]: 1667
Scalar issue width [3]: 0
Scalar issue width [4]: 9165
Total bursts: 1434889
Bytes transferred: 11479112
NOPs avoided: 0
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 0
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 0
SMT legality rejects by class: ALU=0, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 0
Class-flexible injects: 0
Hard-pinned injects: 0
Slack reclaim ratio: 0.0000
Flexible inject share: 0.0000
Multi-lane execute count: 11666
Cluster prepared execution choices: 9165
Wide-path successes: 9165
Partial-width issues: 1667
Decoder prepared scalar groups: 9165
VT spread per bundle: 9165
Issue packet prepared lane sum: 43328
Issue packet materialized lane sum: 43328
Issue packet prepared physical lane sum: 54994
Issue packet materialized physical lane sum: 54994
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7276
Materialized scalar-lane occupancy per cluster choice: 4.7276
Prepared physical lanes per cluster choice: 6.0004
Materialized physical lanes per cluster choice: 6.0004
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 41110
Non-scalar lanes retired: 11111
Retire cycles: 14443
Retired physical lanes per retire cycle: 3.6157
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 186
Reference slice bundle count: 61
Compiler emitted distinct VTs: 1
Compiler IR distinct VTs: 1
Compiler schedule cycle groups: 61
Compiler schedule cross-VT cycle groups: 0
Compiler schedule avg width: 3.0492
Compiler schedule avg VT spread: 1.0000
Compiler schedule max VT spread: 1
Compiler bundle count: 61
Compiler cross-VT bundles: 0
Compiler bundle avg VT spread: 1.0000
Compiler bundle max VT spread: 1
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 6112
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:36.8309118
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=43, retired=145
Last observed core focus: VT=0, PC=0x2400
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running vt [NativeVLIW] ---
>>> Starting mode: WithVirtualThreads [NativeVLIW]
SPEC-like iterations: 10,000
Mode: WithVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedScalar
Frontend supported: True
Elapsed: 00:01:46.9219598
Workload shape: spec-like-rate-packed-scalar
Reference slice iterations: 8
Slice executions: 1,250
Reference slice instructions: 164
Aggregate retirement target: 205000
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 210000
Cycle count: 50000
Pipeline stalls: 0
Active cycles: 50000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 3750
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 43750
Total bursts: 25801875
Bytes transferred: 206415000
NOPs avoided: 15637500
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 21110625
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 21110625
SMT legality rejects by class: ALU=21110625, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 36748125
Class-flexible injects: 15637500
Hard-pinned injects: 0
Slack reclaim ratio: 0.4255
Flexible inject share: 1.0000
Multi-lane execute count: 43750
Cluster prepared execution choices: 45000
Wide-path successes: 45000
Partial-width issues: 0
Decoder prepared scalar groups: 45000
VT spread per bundle: 95000
Issue packet prepared lane sum: 175000
Issue packet materialized lane sum: 175000
Issue packet prepared physical lane sum: 222500
Issue packet materialized physical lane sum: 222500
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 165000
Non-scalar lanes retired: 45000
Retire cycles: 45000
Retired physical lanes per retire cycle: 4.6667
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 36
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 2.3333
Compiler schedule max VT spread: 4
Compiler bundle count: 36
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 2.3333
Compiler bundle max VT spread: 4
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 3750
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x3E, normalized=0x3E, ready=0xC4, visible=0xC4, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:47.5372185
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running max [NativeVLIW] ---
>>> Starting mode: PackedMixedEnvelope [NativeVLIW]
SPEC-like iterations: 10,000
Mode: PackedMixedEnvelope
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedMixedEnvelope
Frontend supported: True
Elapsed: 00:01:38.0907658
Workload shape: spec-like-rate-packed-mixed
Reference slice iterations: 8
Slice executions: 1,250
Reference slice instructions: 165
Aggregate retirement target: 206250
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 210000
Cycle count: 50000
Pipeline stalls: 0
Active cycles: 50000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 3750
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 43750
Total bursts: 25801875
Bytes transferred: 206415000
NOPs avoided: 15637500
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 21110625
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 21110625
SMT legality rejects by class: ALU=21110625, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 36748125
Class-flexible injects: 15637500
Hard-pinned injects: 0
Slack reclaim ratio: 0.4255
Flexible inject share: 1.0000
Multi-lane execute count: 43750
Cluster prepared execution choices: 45000
Wide-path successes: 45000
Partial-width issues: 0
Decoder prepared scalar groups: 45000
VT spread per bundle: 95000
Issue packet prepared lane sum: 175000
Issue packet materialized lane sum: 175000
Issue packet prepared physical lane sum: 222500
Issue packet materialized physical lane sum: 222500
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 165000
Non-scalar lanes retired: 45000
Retire cycles: 45000
Retired physical lanes per retire cycle: 4.6667
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 165
Reference slice bundle count: 37
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 37
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.4595
Compiler schedule avg VT spread: 2.2973
Compiler schedule max VT spread: 4
Compiler bundle count: 37
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 2.2973
Compiler bundle max VT spread: 4
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 3750
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x3E, normalized=0x3E, ready=0xC4, visible=0xC4, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:38.6931088
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running lk [NativeVLIW] ---
>>> Starting mode: Lk [NativeVLIW]
SPEC-like iterations: 10,000
Mode: Lk
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwLatencyHidingLoadKernel
Frontend supported: True
Elapsed: 00:01:37.4293358
Workload shape: spec-like-latency-hiding-memory
Reference slice iterations: 8
Slice executions: 1,250
Reference slice instructions: 164
Aggregate retirement target: 205000
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 210000
Cycle count: 50000
Pipeline stalls: 0
Active cycles: 50000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 3750
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 43750
Total bursts: 25801875
Bytes transferred: 206415000
NOPs avoided: 48476250
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 10946250
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 10946250
SMT legality rejects by class: ALU=10946250, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 59422500
Class-flexible injects: 48476250
Hard-pinned injects: 0
Slack reclaim ratio: 0.8158
Flexible inject share: 1.0000
Multi-lane execute count: 43750
Cluster prepared execution choices: 45000
Wide-path successes: 45000
Partial-width issues: 0
Decoder prepared scalar groups: 45000
VT spread per bundle: 136250
Issue packet prepared lane sum: 175000
Issue packet materialized lane sum: 175000
Issue packet prepared physical lane sum: 222500
Issue packet materialized physical lane sum: 222500
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 165000
Non-scalar lanes retired: 45000
Retire cycles: 45000
Retired physical lanes per retire cycle: 4.6667
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 36
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 3.0000
Compiler schedule max VT spread: 4
Compiler bundle count: 36
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 3.0000
Compiler bundle max VT spread: 4
First opcode: 0x27
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 3750
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x3E, normalized=0x3E, ready=0x5C, visible=0x5C, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:38.0093529
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running bnmcz [NativeVLIW] ---
>>> Starting mode: Bnmcz [NativeVLIW]
SPEC-like iterations: 10,000
Mode: Bnmcz
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwBankNoConflictMixedZoo
Frontend supported: True
Elapsed: 00:01:37.8818559
Workload shape: spec-like-bank-rotated-memory
Reference slice iterations: 8
Slice executions: 1,250
Reference slice instructions: 164
Aggregate retirement target: 205000
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 210000
Cycle count: 50000
Pipeline stalls: 0
Active cycles: 50000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 3750
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 43750
Total bursts: 25801875
Bytes transferred: 206415000
NOPs avoided: 46130625
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 12510000
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 12510000
SMT legality rejects by class: ALU=12510000, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 58640625
Class-flexible injects: 46130625
Hard-pinned injects: 0
Slack reclaim ratio: 0.7867
Flexible inject share: 1.0000
Multi-lane execute count: 43750
Cluster prepared execution choices: 45000
Wide-path successes: 45000
Partial-width issues: 0
Decoder prepared scalar groups: 45000
VT spread per bundle: 135000
Issue packet prepared lane sum: 175000
Issue packet materialized lane sum: 175000
Issue packet prepared physical lane sum: 222500
Issue packet materialized physical lane sum: 222500
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 165000
Non-scalar lanes retired: 45000
Retire cycles: 45000
Retired physical lanes per retire cycle: 4.6667
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 36
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 2.6667
Compiler schedule max VT spread: 4
Compiler bundle count: 36
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 2.6667
Compiler bundle max VT spread: 4
First opcode: 0x27
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 3750
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x3E, normalized=0x3E, ready=0x5C, visible=0x5C, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:38.4454743
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running replay [NativeVLIW] ---
=== Replay phase pair ===
SPEC-like iterations: 10,000
Replay pair summary:
Iterations: 10,000
Stable phase: hits=30000, misses=0, hit-rate=100.00%, checks-saved=180000, invalidations=30000
Rotating phase: hits=30000, misses=0, hit-rate=100.00%, checks-saved=180000, invalidations=39999
Replay-aware cycle delta (stable - rotating): 0
Ready-hit delta (stable - rotating): 0
Checks-saved delta (stable - rotating): 0
Phase-mismatch invalidation delta (stable - rotating): -9999
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.7767290

--- Running safety [NativeVLIW] ---
=== SafetyVerifier negative controls ===
Counters: owner=1, domain=1, boundary=1, invalid-replay=1, stale-witness=1
mismatch owner/context: rejected=True, actual=OwnerMismatch/GuardPlane, counter=1, passed=True
mismatch domains: rejected=True, actual=DomainMismatch/GuardPlane, counter=1, passed=True
closed serialization boundary: rejected=True, actual=Boundary/GuardPlane, counter=1, passed=True
invalid replay boundary: rejected=True, actual=InvalidReplayBoundary/ReplayTemplateWitness, counter=1, passed=True
stale witness/template rejection: rejected=True, actual=StaleStructuralIdentity/ReplayTemplateWitness, counter=1, passed=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.2502289

--- Running replay-reuse [NativeVLIW] ---
=== Replay template reuse diagnostics ===
SPEC-like iterations: 10,000
Template aggregate: attempts=16384, hits=4095, misses=12289, hit-rate=24.99%
Invalidations: phase-key=4095, structural=4095, boundary=4095, witness-accesses=16384, fallback-to-live-witness=12289
stable replay-template reuse: attempts=4096, hits=4095, misses=1, warmup-misses=1, fallback-to-live-witness=1, passed=True
phase-key invalidation: attempts=4096, hits=0, misses=4096, warmup-misses=1, fallback-to-live-witness=4096, passed=True
structural-identity invalidation: attempts=4096, hits=0, misses=4096, warmup-misses=1, fallback-to-live-witness=4096, passed=True
boundary-state invalidation: attempts=4096, hits=0, misses=4096, warmup-misses=1, fallback-to-live-witness=4096, passed=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.8763829

--- Running assistant [NativeVLIW] ---
=== Assistant decision matrix ===
Matrix aggregate: attempts=6, accepted=1, quota-rejects=1, backpressure-rejects=1, owner-domain-rejects=1, invalid-replay-rejects=1, primary-priority-rejects=1
assistance accepted with residual capacity: expected=Accepted/AcceptedWithResidualCapacity, actual=Accepted/AcceptedWithResidualCapacity, passed=True, detail=reserved-lines=2, residual-after=1
assistance rejected by quota: expected=Rejected/Quota, actual=Rejected/Quota, passed=True, detail=LineCredits
assistance rejected by backpressure: expected=Rejected/Backpressure, actual=Rejected/Backpressure, passed=True, detail=SharedOuterCap
assistance rejected by owner/domain administrator: expected=Rejected/OwnerDomainAdministrator, actual=Rejected/OwnerDomainAdministrator, passed=True, detail=owner administrator rejected assist context
assistance rejected by invalid replay: expected=Rejected/InvalidReplay, actual=Rejected/InvalidReplay, passed=True, detail=replay phase cannot carry an assistant template
primary stream priority over assistant stream: expected=Rejected/PrimaryStreamPriority, actual=Rejected/PrimaryStreamPriority, passed=True, detail=primary stream consumed all assistant-eligible residual capacity
assistance accepted then discarded on replay invalidation: expected=DiscardedOnReplayInvalidation, actual=DiscardedOnReplayInvalidation, invalidation=PhaseMismatch, passed=True, scope=test-local lifecycle model; does not exercise production retire
Assistant visibility/non-retirement counters: assist accepted=1, replay-invalidated-after-acceptance=1, assist discarded=1, assist retire records=0, assist architectural writes=0, assist committed stores=0, assist telemetry events=2, assist carrier publications=1, foreground retire records preserved=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.2783450