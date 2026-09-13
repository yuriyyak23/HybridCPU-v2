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
Elapsed: 00:00:39.4914725
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
Elapsed: 00:00:40.0632591
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\alu
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
Elapsed: 00:00:41.9788411
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
Elapsed: 00:00:42.5788079
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\novt
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
Elapsed: 00:02:06.5794087
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
Elapsed: 00:02:07.1375768
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\vt
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
Elapsed: 00:02:05.3938141
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
Elapsed: 00:02:05.9595512
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\max
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
Elapsed: 00:01:57.1761664
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
Elapsed: 00:01:57.7357743
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\lk
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
Elapsed: 00:01:54.0002912
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
Elapsed: 00:01:54.5643738
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\bnmcz
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
Elapsed: 00:00:00.8462167
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\replay

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
Elapsed: 00:00:00.2590928
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\safety

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
Elapsed: 00:00:00.9163699
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\replay-reuse

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
Elapsed: 00:00:00.2767841
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\assistant

--- Running stream-vector [NativeVLIW] ---
=== Stream/Vector SPEC-like suite ===
SPEC-like iterations: 10,000
Suite aggregate: scenarios=6, passed=6, dynamic-instructions=780,000, vector-elements=4,240,000, modeled-bytes=42,600,000, checksum=0xDE1EF81CFD41AF21
sgemm-4x4-stream-rows: passed=True, instructions=480,000, elements=1,920,000, bytes=17,920,000, error=0, checksum=0xEE7EFA71580F6465
fir-vdotf-windowed-dsp: passed=True, instructions=40,000, elements=320,000, bytes=2,720,000, error=0, checksum=0xE621987091CE13F5
predicate-compress-filter: passed=True, instructions=20,000, elements=320,000, bytes=2,560,000, error=0, checksum=0x9D826A20B34550B9
crypto-bitmix-popcount: passed=True, instructions=60,000, elements=960,000, bytes=10,880,000, error=0, checksum=0x516AB2616F98B2AE
hydro-row-stencil-5point: passed=True, instructions=160,000, elements=640,000, bytes=7,680,000, error=0, checksum=0xCA5B6EC3F52B463D
dma-lane6-token-contract: passed=True, instructions=20,000, elements=80,000, bytes=840,000, error=0, checksum=0x97DE91B07C909900
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:03:07.5662201
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\stream-vector

--- Running matrix-tile [NativeVLIW] ---
=== MatrixTile SPEC-like pressure suite ===
SPEC-like iterations: 10,000
Suite aggregate: scenarios=17, passed=17, runtime-instructions=120,026, compiler-emissions=34, retire-publications=120,016, replay-round-trips=80,018, fail-closed-rejections=53, stream-bytes=480,032, checksum=0x8142407CDEB51D1C
mtile-memory-lane6-roundtrip-pressure: passed=True, instructions=20,000, compiler=0, retire=20,000, replay=20,000, rejected=0, bytes=80,000, invalidations=20,000, checksum=0xBEB9A779A97ECA35
mtile-memory-contour-varied-shape-pressure: passed=True, instructions=80,000, compiler=0, retire=80,000, replay=40,000, rejected=0, bytes=400,000, invalidations=100,000, checksum=0xD6F289414B671D85
mtile-lane6-scheduler-conflict-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=6, bytes=0, invalidations=0, checksum=0x4689ED1498CEC617
mtile-macc-numeric-policy-replay-pressure: passed=True, instructions=10,000, compiler=0, retire=10,000, replay=10,000, rejected=0, bytes=0, invalidations=0, checksum=0xF2A2AEE2D758B68D
mtile-numeric-layout-abi-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x385FB5510EDDEA78
mtile-golden-manifest-coverage-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=10, bytes=0, invalidations=0, checksum=0xDA5091B269D6FECE
mtile-golden-json-corpus-loader-pressure: passed=True, instructions=7, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x0983D721AA3A41D1
mtranspose-layout-policy-replay-pressure: passed=True, instructions=10,000, compiler=0, retire=10,000, replay=10,000, rejected=0, bytes=0, invalidations=0, checksum=0xAB4A5AB3F7003A6D
mtile-store-memory-fault-all-or-none-pressure: passed=True, instructions=1, compiler=0, retire=0, replay=1, rejected=1, bytes=0, invalidations=2, checksum=0xFB705C18BAD7725A
mtile-load-memory-fault-no-publication-pressure: passed=True, instructions=1, compiler=0, retire=0, replay=1, rejected=1, bytes=0, invalidations=0, checksum=0xE20C601871080DCC
mtile-compiler-sideband-lowering-conformance: passed=True, instructions=0, compiler=4, retire=0, replay=0, rejected=0, bytes=0, invalidations=0, checksum=0x634DC48718F0482C
mtile-compiler-lowered-runtime-execution-pressure: passed=True, instructions=4, compiler=4, retire=4, replay=4, rejected=0, bytes=8, invalidations=0, checksum=0x72B4AD7F9E4600C6
mtile-full-pipeline-e2e-pressure: passed=True, instructions=5, compiler=12, retire=4, replay=4, rejected=8, bytes=8, invalidations=0, checksum=0xAD6D158F604A952C
mtile-production-stageflow-e2e-pressure: passed=True, instructions=4, compiler=4, retire=4, replay=4, rejected=0, bytes=8, invalidations=0, checksum=0x3D3C5FA828B7774C
mtile-production-pc-fetch-e2e-pressure: passed=True, instructions=4, compiler=10, retire=4, replay=4, rejected=6, bytes=8, invalidations=0, checksum=0xCBF29CE484222325
mtile-fail-closed-policy-and-resource-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=7, bytes=0, invalidations=0, checksum=0xAF63BA4C8601B2C6
mtile-fault-fuzz-policy-identity-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=8, bytes=0, invalidations=0, checksum=0x6E9804EAB5223F68
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:20.5904092
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\matrix-tile

Default SPEC-like diagnostic matrix summary:
Aggregate status: Succeeded
Child runs: 12
Artifacts: \HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix

=== Stream/Vector final benchmarks, telemetry, statistics ===
Suite: stream-vector-spec-suite, status=Passed, iterations=10,000, artifact=\HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\stream-vector\stream_vector_spec_report.json
Aggregate: scenarios=6/6, dynamic-instructions=780,000, vector-elements=4,240,000, modeled-bytes=42,600,000, elapsed-ms=186,592.561, checksum=0xDE1EF81CFD41AF21
Throughput: vector-elements/ms=22.72, modeled-bytes/ms=228.30, dynamic-instructions/ms=4.18
Benchmarks:
  sgemm-4x4-stream-rows: passed, algorithm=Dense SGEMM micro-kernel C=A*B, instructions=480,000, elements=1,920,000, bytes=17,920,000, elapsed-ms=35,872.692, elements/ms=53.52, error=0, opcodes=VLOAD/VMUL/VADD
  fir-vdotf-windowed-dsp: passed, algorithm=DSP FIR convolution, instructions=40,000, elements=320,000, bytes=2,720,000, elapsed-ms=20,132.364, elements/ms=15.89, error=0, opcodes=VDOTF
  predicate-compress-filter: passed, algorithm=Columnar analytics filter/pack, instructions=20,000, elements=320,000, bytes=2,560,000, elapsed-ms=21,571.258, elements/ms=14.83, error=0, opcodes=VCMPGT/VCOMPRESS
  crypto-bitmix-popcount: passed, algorithm=Crypto/hash bit-mixing round, instructions=60,000, elements=960,000, bytes=10,880,000, elapsed-ms=28,422.170, elements/ms=33.78, error=0, opcodes=VXOR/VSLL/VADD/VSRL/VOR/VPOPCNT
  hydro-row-stencil-5point: passed, algorithm=Hydrodynamics-like 5-point stencil, instructions=160,000, elements=640,000, bytes=7,680,000, elapsed-ms=27,968.166, elements/ms=22.88, error=0, opcodes=VADD
  dma-lane6-token-contract: passed, algorithm=Descriptor-backed memory-memory compute, instructions=20,000, elements=80,000, bytes=840,000, elapsed-ms=52,625.912, elements/ms=1.52, error=0, opcodes=DmaStreamCompute.Fma/DmaStreamCompute.Reduce
Stream telemetry:
  bursts=2,090,000, transferred-bytes=44,320,000, foreground-warm=0/0, foreground-reuse=0, foreground-bypass=0
  assist-warm=0/0, assist-reuse=0, assist-bypass=0, translation-rejects=0, backend-rejects=0
DMA lane6 telemetry:
  lane6-backend-used=True, direct-destination-writes=0, bytes-read=640,000, bytes-staged=200,000, read-bursts=40,000, modeled-latency-cycles=320,000, element-ops=80,000

=== MatrixTile final benchmarks, resources, and fail-closed diagnostics ===
Suite: matrix-tile-spec-pressure-suite, status=Passed, iterations=10,000, artifact=\HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260621_111842_369_matrix\matrix-tile\matrix_tile_spec_report.json
Runtime closure: ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands
Aggregate: scenarios=17/17, instructions=120,026, compiler-emissions=34, retire=120,016, replay-round-trips=80,018, fail-closed=53, stream-bytes=480,032, stream-invalidations=120,002, elapsed-ms=15,738.744
Throughput: runtime-instructions/ms=7.63, compiler-emissions/ms=0.00, retire-publications/ms=7.63, stream-bytes/ms=30.50
Baselines: smoke-throughput=passed, runtime-instructions/ms=7.63, retire/ms=7.63, replay/ms=5.08, stream-bytes/ms=30.50
Scenarios:
  mtile-memory-lane6-roundtrip-pressure: passed, contour=MatrixTileMemory / MatrixTileStreamClass / lane6, instructions=20,000, retire=20,000, compiler=0, replay=20,000, rejected=0, bytes=80,000, invalidations=20,000, elapsed-ms=3,656.818, baseline=pass, inst/ms=5.47, bytes/ms=21.88, opcodes=MTILE_LOAD/MTILE_STORE
    resource: resource=MatrixTileMemory
    resource: slot=MatrixTileStreamClass
    resource: lane=6
    resource: channel=0
    resource: DmaStreamClass capacity conflict verified by runtime lane map
  mtile-memory-contour-varied-shape-pressure: passed, contour=MatrixTileMemory / varied descriptor shapes and SRF row windows, instructions=80,000, retire=80,000, compiler=0, replay=40,000, rejected=0, bytes=400,000, invalidations=100,000, elapsed-ms=9,217.599, baseline=pass, inst/ms=8.68, bytes/ms=43.40, opcodes=MTILE_LOAD/MTILE_STORE
    resource: shape=1x4/2x3/3x2/4x1
    resource: stride=canonical and padded row windows
    resource: publication=load retire only
    resource: store=all-or-none commit plus SRF invalidation
  mtile-lane6-scheduler-conflict-pressure: passed, contour=Scheduler / lane6 MatrixTileStreamClass capacity pressure, instructions=0, retire=0, compiler=0, replay=0, rejected=6, bytes=0, invalidations=0, elapsed-ms=30.473, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/TEST_SLOT_CLASS_CLAIM
    resource: foreground=MatrixTileStreamClass/lane6
    resource: candidate=DmaStreamClass/lane6 rejected
    resource: candidate=MatrixTileStreamClass/lane6 rejected
    resource: candidate=AluClass/LsuClass admitted on independent capacity
  mtile-macc-numeric-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=10,000, retire=10,000, compiler=0, replay=10,000, rejected=0, bytes=0, invalidations=0, elapsed-ms=1,353.335, baseline=pass, inst/ms=7.39, bytes/ms=0.00, opcodes=MTILE_MACC
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=SignedInt8ToInt32
    resource: layout=MaccCanonicalRowMajorAscendingK
    resource: publication=Accumulator
  mtile-numeric-layout-abi-pressure: passed, contour=MatrixTileNumericLayoutAbi / formal runtime arithmetic, instructions=0, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=11.128, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: numeric=all supported MatrixTileNumericPolicyAbi profiles byte-exact
    resource: numeric=signed/unsigned integer widening and final little-endian encoding
    resource: numeric=SignedInt64ToInt64 overflow traps before publication
    resource: numeric=Binary32ToBinary32 separate software IEEE rounding
    resource: numeric=Binary64ToBinary64 byte-exact software IEEE result
    resource: layout=Transpose non-square in-place and tampered destination addressing reject
  mtile-golden-manifest-coverage-pressure: passed, contour=MatrixTile golden manifest / runtime-owned corpus coverage, instructions=0, retire=0, compiler=0, replay=0, rejected=10, bytes=0, invalidations=0, elapsed-ms=5.295, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: golden=positive executable vectors for all canonical opcodes
    resource: golden=memory fault vectors for load/store identity
    resource: golden=descriptor and reserved carrier negatives fail closed
    resource: golden=no compiler-generated inputs and no fallback path
    resource: no-fallback=typed carrier, runtime memory, IL call target, and compiler boundary audit advertised
  mtile-golden-json-corpus-loader-pressure: passed, contour=MatrixTile WhiteBook golden JSON / production-path loader, instructions=7, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=8.582, baseline=pass, inst/ms=0.82, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: json=schema v1 and runtime ABI version binding
    resource: json=no compiler output and no private arithmetic oracle
    resource: json=positive MACC/transpose vectors validated against runtime ABI
    resource: json=execute/projection fault vectors fail closed
  mtranspose-layout-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=10,000, retire=10,000, compiler=0, replay=10,000, rejected=0, bytes=0, invalidations=0, elapsed-ms=1,031.367, baseline=pass, inst/ms=9.70, bytes/ms=0.00, opcodes=MTRANSPOSE
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=absent by operation contract
    resource: layout=TransposeCanonicalRowMajor
    resource: publication=TileState
  mtile-store-memory-fault-all-or-none-pressure: passed, contour=MatrixTileMemory / retire fault all-or-none, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=2, elapsed-ms=1.746, baseline=pass, inst/ms=0.57, bytes/ms=0.00, opcodes=MTILE_STORE
    resource: execute capture remains side-effect-free
    resource: retire reports MemoryCommitFault
    resource: all-or-none rollback preserves original memory
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-load-memory-fault-no-publication-pressure: passed, contour=MatrixTileMemory / load partial-row fault no-publication, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=0, elapsed-ms=5.024, baseline=pass, inst/ms=0.20, bytes/ms=0.00, opcodes=MTILE_LOAD
    resource: execute captures PartialMemoryFault with precise row/address
    resource: retire reports CapturedExecutionFault
    resource: no partial tile publication before or after retire
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-compiler-sideband-lowering-conformance: passed, contour=Compiler transport conformance, instructions=0, retire=0, compiler=4, replay=0, rejected=0, bytes=0, invalidations=0, elapsed-ms=157.437, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: MTILE_LOAD/STORE carry no compute numeric/layout sideband
    resource: MTILE_MACC preserves explicit numeric and layout sidebands in source and lowered InstructionSlotMetadata
    resource: MTRANSPOSE preserves layout-only sideband in source and lowered InstructionSlotMetadata
    resource: lowered MatrixTile memory transport is physically placed on lane6
  mtile-compiler-lowered-runtime-execution-pressure: passed, contour=Compiler lowered bundle / runtime carrier execution, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=33.608, baseline=pass, inst/ms=0.12, bytes/ms=0.24, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: lowered MTILE_LOAD materializes as runtime MatrixTileMicroOp and retires tile state
    resource: lowered MTILE_STORE materializes as runtime MatrixTileMicroOp and commits memory at retire
    resource: lowered MTILE_MACC executes runtime-owned numeric/layout arithmetic
    resource: lowered MTRANSPOSE executes runtime-owned layout permutation
  mtile-full-pipeline-e2e-pressure: passed, contour=Full pipeline E2E / compiler lowered MatrixTile fetch-decode-schedule-retire, instructions=5, retire=4, compiler=12, replay=4, rejected=8, bytes=8, invalidations=0, elapsed-ms=36.153, baseline=pass, inst/ms=0.14, bytes/ms=0.22, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: compiler-emissions=positive canonical four-op program plus targeted negative compiler emissions
    resource: fetch/decode=VliwDecoderV4 with lowered VliwBundleAnnotations
    resource: schedule=MicroOpScheduler.PackBundleIntraCoreSmt carrier placement
    resource: lane6=MTILE_LOAD/STORE scheduled as MatrixTileStreamClass on lane6
    resource: retire-only=load tile, store memory, MACC accumulator, transpose destination tile
    resource: replay=all four positive operations rollback and replay through retire-owned journal
    resource: sideband-preservation=source and decoded InstructionSlotMetadata policy identities match
    resource: fail-closed=missing/tampered/mismatched sidebands and wrong memory resource identity reject before publication
  mtile-production-stageflow-e2e-pressure: passed, contour=Production CPU stage flow / fetched compiler bundles to WB-retire, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=71.146, baseline=pass, inst/ms=0.06, bytes/ms=0.11, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: fetch-ingress=test-support stages serialized compiler-produced bundle bytes and lowered annotations into pipeIF
    resource: decode=production PipelineStage_Decode and canonical scheduler/materializer path
    resource: dispatch=production PipelineStage_Execute calls MatrixTileMicroOp.Execute
    resource: writeback-retire=production PipelineStage_WriteBack calls MatrixTileMicroOp.EmitWriteBackRetireRecords
    resource: retire-only=tile state and store memory remain unchanged until WB-retire
    resource: dataflow=MTILE_LOAD tile feeds MTILE_STORE, MTILE_MACC, and MTRANSPOSE
    resource: replay=all four WB-retired operations complete rollback and deterministic replay
  mtile-production-pc-fetch-e2e-pressure: passed, contour=Production PC fetch / canonical compiler annotation ingress, instructions=4, retire=4, compiler=10, replay=4, rejected=6, bytes=8, invalidations=0, elapsed-ms=116.215, baseline=pass, inst/ms=0.03, bytes/ms=0.07, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: ingress=EmitProgram only
    resource: transport=MainMemory -> L2 -> L1 -> pipeIF -> production decode
    resource: dataflow=loaded tile feeds store/MACC/transpose
    resource: retire-only=production WB-retire
    resource: replay=retired results rollback and replay
    resource: negatives=missing/tampered/mismatched sidebands fail before execute/retire
    resource: coherence=re-emission drops stale L1/L2 carriers and raw byte overwrite without republish rejects
  mtile-fail-closed-policy-and-resource-pressure: passed, contour=Fail-closed runtime validation, instructions=0, retire=0, compiler=0, replay=0, rejected=7, bytes=0, invalidations=0, elapsed-ms=0.786, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: retire rejects tampered policy before publication
    resource: retire rejects wrong-owner capture identity before publication
    resource: retire rejects cross-core capture identity before publication
    resource: retire rejects stale epoch capture identity before publication
    resource: retire rejects wrong MatrixTile stream resource class
    resource: retire rejects wrong MatrixTile stream direction
    resource: MatrixTileStreamClass aliases DmaStreamClass capacity on lane6
  mtile-fault-fuzz-policy-identity-pressure: passed, contour=MatrixTile fault fuzz / policy identity and descriptor negatives, instructions=0, retire=0, compiler=0, replay=0, rejected=8, bytes=0, invalidations=0, elapsed-ms=2.032, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: fuzz=missing/tampered numeric-layout policy identity
    resource: fuzz=wrong operation/opcode and zero ordinal identity
    resource: fuzz=load owner/channel/operation transfer identity
    resource: fuzz=no publication after rejected mutations
Done.