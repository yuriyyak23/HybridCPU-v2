HybridCPU ISE diagnostics console
Primary runtime validation harness starting...

SPEC-like iterations for default SPEC-like matrix [250]: 200
Configured SPEC-like iterations: 200
Wall-clock budgets will be auto-scaled from the prompted iteration count.

=== Default SPEC-like diagnostic matrix ===
--- Running alu [NativeVLIW] ---
>>> Starting mode: SingleThreadNoVector [NativeVLIW]
SPEC-like iterations: 200
Mode: SingleThreadNoVector
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwSingleThread
Frontend supported: True
Elapsed: 00:00:07.1600231
Workload shape: spec-like-single-thread-int
Reference slice iterations: 36
Slice executions: 6
Reference slice instructions: 185
Aggregate retirement target: 1028
Diagnostics run completed.
IPC (retire-normalized): 3.5979
Raw cycle IPC: 3.3238
Instructions retired: 1047
Cycle count: 315
Pipeline stalls: 0
Active cycles: 315
Stall share: 0.00%
Effective issue width: 3.3238
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 17
Scalar issue width [1]: 17
Scalar issue width [2]: 34
Scalar issue width [3]: 0
Scalar issue width [4]: 184
Total bursts: 761
Bytes transferred: 6088
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
Multi-lane execute count: 235
Cluster prepared execution choices: 185
Wide-path successes: 185
Partial-width issues: 34
Decoder prepared scalar groups: 185
VT spread per bundle: 185
Issue packet prepared lane sum: 872
Issue packet materialized lane sum: 872
Issue packet prepared physical lane sum: 1107
Issue packet materialized physical lane sum: 1107
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7135
Materialized scalar-lane occupancy per cluster choice: 4.7135
Prepared physical lanes per cluster choice: 5.9838
Materialized physical lanes per cluster choice: 5.9838
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 824
Non-scalar lanes retired: 223
Retire cycles: 291
Retired physical lanes per retire cycle: 3.5979
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
NOP elision skips: 124
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:07.7241807
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\alu
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=35, retired=107
Last observed core focus: VT=0, PC=0x1C00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running novt [NativeVLIW] ---
>>> Starting mode: WithoutVirtualThreads [NativeVLIW]
SPEC-like iterations: 200
Mode: WithoutVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwVectorProbe
Frontend supported: True
Elapsed: 00:00:07.1443960
Workload shape: spec-like-single-thread-vector
Reference slice iterations: 36
Slice executions: 6
Reference slice instructions: 186
Aggregate retirement target: 1034
Diagnostics run completed.
IPC (retire-normalized): 3.5979
Raw cycle IPC: 3.3238
Instructions retired: 1047
Cycle count: 315
Pipeline stalls: 0
Active cycles: 315
Stall share: 0.00%
Effective issue width: 3.3238
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 17
Scalar issue width [1]: 17
Scalar issue width [2]: 34
Scalar issue width [3]: 0
Scalar issue width [4]: 184
Total bursts: 761
Bytes transferred: 6088
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
Multi-lane execute count: 235
Cluster prepared execution choices: 185
Wide-path successes: 185
Partial-width issues: 34
Decoder prepared scalar groups: 185
VT spread per bundle: 185
Issue packet prepared lane sum: 872
Issue packet materialized lane sum: 872
Issue packet prepared physical lane sum: 1107
Issue packet materialized physical lane sum: 1107
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7135
Materialized scalar-lane occupancy per cluster choice: 4.7135
Prepared physical lanes per cluster choice: 5.9838
Materialized physical lanes per cluster choice: 5.9838
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 824
Non-scalar lanes retired: 223
Retire cycles: 291
Retired physical lanes per retire cycle: 3.5979
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
NOP elision skips: 124
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:07.7137714
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\novt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=35, retired=107
Last observed core focus: VT=0, PC=0x1C00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running vt [NativeVLIW] ---
>>> Starting mode: WithVirtualThreads [NativeVLIW]
SPEC-like iterations: 200
Mode: WithVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedScalar
Frontend supported: True
Elapsed: 00:00:08.6052443
Workload shape: spec-like-rate-packed-scalar
Reference slice iterations: 8
Slice executions: 25
Reference slice instructions: 164
Aggregate retirement target: 4100
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 4200
Cycle count: 1000
Pipeline stalls: 0
Active cycles: 1000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 75
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 875
Total bursts: 10725
Bytes transferred: 85800
NOPs avoided: 6500
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 8775
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 8775
SMT legality rejects by class: ALU=8775, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 15275
Class-flexible injects: 6500
Hard-pinned injects: 0
Slack reclaim ratio: 0.4255
Flexible inject share: 1.0000
Multi-lane execute count: 875
Cluster prepared execution choices: 900
Wide-path successes: 900
Partial-width issues: 0
Decoder prepared scalar groups: 900
VT spread per bundle: 1900
Issue packet prepared lane sum: 3500
Issue packet materialized lane sum: 3500
Issue packet prepared physical lane sum: 4450
Issue packet materialized physical lane sum: 4450
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 3300
Non-scalar lanes retired: 900
Retire cycles: 900
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
NOP elision skips: 75
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x77, normalized=0x77, ready=0x32, visible=0x32, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:09.1721840
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\vt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running max [NativeVLIW] ---
>>> Starting mode: PackedMixedEnvelope [NativeVLIW]
SPEC-like iterations: 200
Mode: PackedMixedEnvelope
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedMixedEnvelope
Frontend supported: True
Elapsed: 00:00:08.6596660
Workload shape: spec-like-rate-packed-mixed
Reference slice iterations: 8
Slice executions: 25
Reference slice instructions: 165
Aggregate retirement target: 4125
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 4200
Cycle count: 1000
Pipeline stalls: 0
Active cycles: 1000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 75
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 875
Total bursts: 10725
Bytes transferred: 85800
NOPs avoided: 6500
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 8775
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 8775
SMT legality rejects by class: ALU=8775, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 15275
Class-flexible injects: 6500
Hard-pinned injects: 0
Slack reclaim ratio: 0.4255
Flexible inject share: 1.0000
Multi-lane execute count: 875
Cluster prepared execution choices: 900
Wide-path successes: 900
Partial-width issues: 0
Decoder prepared scalar groups: 900
VT spread per bundle: 1900
Issue packet prepared lane sum: 3500
Issue packet materialized lane sum: 3500
Issue packet prepared physical lane sum: 4450
Issue packet materialized physical lane sum: 4450
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 3300
Non-scalar lanes retired: 900
Retire cycles: 900
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
NOP elision skips: 75
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x77, normalized=0x77, ready=0x32, visible=0x32, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:09.2553066
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\max
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running lk [NativeVLIW] ---
>>> Starting mode: Lk [NativeVLIW]
SPEC-like iterations: 200
Mode: Lk
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwLatencyHidingLoadKernel
Frontend supported: True
Elapsed: 00:00:08.5954945
Workload shape: spec-like-latency-hiding-memory
Reference slice iterations: 8
Slice executions: 25
Reference slice instructions: 164
Aggregate retirement target: 4100
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 4200
Cycle count: 1000
Pipeline stalls: 0
Active cycles: 1000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 75
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 875
Total bursts: 10725
Bytes transferred: 85800
NOPs avoided: 20150
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 4550
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 4550
SMT legality rejects by class: ALU=4550, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 24700
Class-flexible injects: 20150
Hard-pinned injects: 0
Slack reclaim ratio: 0.8158
Flexible inject share: 1.0000
Multi-lane execute count: 875
Cluster prepared execution choices: 900
Wide-path successes: 900
Partial-width issues: 0
Decoder prepared scalar groups: 900
VT spread per bundle: 2725
Issue packet prepared lane sum: 3500
Issue packet materialized lane sum: 3500
Issue packet prepared physical lane sum: 4450
Issue packet materialized physical lane sum: 4450
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 3300
Non-scalar lanes retired: 900
Retire cycles: 900
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
NOP elision skips: 75
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x77, normalized=0x77, ready=0x5E, visible=0x5E, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:09.2019955
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\lk
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running bnmcz [NativeVLIW] ---
>>> Starting mode: Bnmcz [NativeVLIW]
SPEC-like iterations: 200
Mode: Bnmcz
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwBankNoConflictMixedZoo
Frontend supported: True
Elapsed: 00:00:08.5582144
Workload shape: spec-like-bank-rotated-memory
Reference slice iterations: 8
Slice executions: 25
Reference slice instructions: 164
Aggregate retirement target: 4100
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 4.2000
Instructions retired: 4200
Cycle count: 1000
Pipeline stalls: 0
Active cycles: 1000
Stall share: 0.00%
Effective issue width: 4.2000
Data hazards: 0
Memory stalls: 0
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 75
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 875
Total bursts: 10725
Bytes transferred: 85800
NOPs avoided: 19175
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 5200
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 5200
SMT legality rejects by class: ALU=5200, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 24375
Class-flexible injects: 19175
Hard-pinned injects: 0
Slack reclaim ratio: 0.7867
Flexible inject share: 1.0000
Multi-lane execute count: 875
Cluster prepared execution choices: 900
Wide-path successes: 900
Partial-width issues: 0
Decoder prepared scalar groups: 900
VT spread per bundle: 2700
Issue packet prepared lane sum: 3500
Issue packet materialized lane sum: 3500
Issue packet prepared physical lane sum: 4450
Issue packet materialized physical lane sum: 4450
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8889
Materialized scalar-lane occupancy per cluster choice: 3.8889
Prepared physical lanes per cluster choice: 4.9444
Materialized physical lanes per cluster choice: 4.9444
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 3300
Non-scalar lanes retired: 900
Retire cycles: 900
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
NOP elision skips: 75
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x77, normalized=0x77, ready=0x5E, visible=0x5E, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:09.1428641
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\bnmcz
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=40, retired=168
Last observed core focus: VT=0, PC=0x1E00
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running replay [NativeVLIW] ---
=== Replay phase pair ===
SPEC-like iterations: 200
Replay pair summary:
Iterations: 200
Stable phase: hits=600, misses=0, hit-rate=100.00%, checks-saved=3600, invalidations=600
Rotating phase: hits=600, misses=0, hit-rate=100.00%, checks-saved=3600, invalidations=799
Replay-aware cycle delta (stable - rotating): 0
Ready-hit delta (stable - rotating): 0
Checks-saved delta (stable - rotating): 0
Phase-mismatch invalidation delta (stable - rotating): -199
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.2844510
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\replay

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
Elapsed: 00:00:00.2573826
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\safety

--- Running replay-reuse [NativeVLIW] ---
=== Replay template reuse diagnostics ===
SPEC-like iterations: 200
Template aggregate: attempts=800, hits=199, misses=601, hit-rate=24.87%
Invalidations: phase-key=199, structural=199, boundary=199, witness-accesses=800
stable replay-template reuse: attempts=200, hits=199, misses=1, passed=True
phase-key invalidation: attempts=200, hits=0, misses=200, passed=True
structural-identity invalidation: attempts=200, hits=0, misses=200, passed=True
boundary-state invalidation: attempts=200, hits=0, misses=200, passed=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.3050266
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\replay-reuse

--- Running assistant [NativeVLIW] ---
=== Assistant decision matrix ===
Matrix aggregate: attempts=6, accepted=1, quota-rejects=1, backpressure-rejects=1, owner-domain-rejects=1, invalid-replay-rejects=1, primary-priority-rejects=1
assistance accepted with residual capacity: expected=Accepted/AcceptedWithResidualCapacity, actual=Accepted/AcceptedWithResidualCapacity, passed=True, detail=reserved-lines=2, residual-after=1
assistance rejected by quota: expected=Rejected/Quota, actual=Rejected/Quota, passed=True, detail=LineCredits
assistance rejected by backpressure: expected=Rejected/Backpressure, actual=Rejected/Backpressure, passed=True, detail=SharedOuterCap
assistance rejected by owner/domain administrator: expected=Rejected/OwnerDomainAdministrator, actual=Rejected/OwnerDomainAdministrator, passed=True, detail=owner administrator rejected assist context
assistance rejected by invalid replay: expected=Rejected/InvalidReplay, actual=Rejected/InvalidReplay, passed=True, detail=replay phase cannot carry an assistant template
primary stream priority over assistant stream: expected=Rejected/PrimaryStreamPriority, actual=Rejected/PrimaryStreamPriority, passed=True, detail=primary stream consumed all assistant-eligible residual capacity
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.2416218
Artifacts: HybridCPU ISE\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260425_005626_116_matrix\assistant

Default SPEC-like diagnostic matrix summary:
Aggregate status: Succeeded
Child runs: 10

## L7-SDC Phase 03 matrix-smoke evidence - 2026-04-28

Command:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result:

```text
Matrix Smoke summary:
Aggregate status: Succeeded
Child runs: 3
Artifacts: C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\TestResults\TestAssemblerConsoleApps\20260428_114428_445_matrix-smoke
```

Comparison notes:

- Current `matrix-smoke` is a focused diagnostics profile with `safety`,
  `replay-reuse`, and `assistant` child runs. It intentionally does not emit the
  default SPEC-like issue-width, stall, branch/control/system progress,
  pinned-constraint NOP, legality-reject, or lane6 counter blocks shown above
  for the older 10-child default matrix.
- `replay-reuse` now reports additional detail fields:
  `fallback-to-live-witness` in the aggregate invalidation line and
  per-scenario `warmup-misses` plus `fallback-to-live-witness`.
- `assistant` now reports an extra accepted-then-discarded replay invalidation
  scenario and an `Assistant visibility/non-retirement counters` line:
  `assist accepted=1`, `replay-invalidated-after-acceptance=1`,
  `assist discarded=1`, `assist retire records=0`,
  `assist architectural writes=0`, `assist committed stores=0`,
  `assist telemetry events=2`, `assist carrier publications=1`, and
  `foreground retire records preserved=True`.
- No L7-SDC backend, descriptor ABI parser, token lifecycle, staged write
  commit path, or compiler emission is exercised by this diagnostics profile.

## L7-SDC Phase 15 full validation evidence - 2026-04-28

Validation commands:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7Sdc" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "DmaStreamCompute" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "Phase09" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "Phase12" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "AssistRuntime" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "StreamRegisterFile" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "CompilerV5ContractAlignment" --no-restore
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "Phase4Extensibility" --no-restore
dotnet test
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-memory --iterations 200 --telemetry-logs minimal
```

Test results:

```text
L7Sdc: Passed 267, Failed 0
DmaStreamCompute: Passed 113, Failed 0
Phase09: Passed 1091, Failed 0
Phase12: Passed 217, Failed 0
AssistRuntime: Passed 125, Failed 0
StreamRegisterFile: Passed 15, Failed 0
CompilerV5ContractAlignment: Passed 143, Failed 0
Phase4Extensibility: Passed 13, Failed 0
dotnet test: Passed 5917, Skipped 2, Failed 0, Total 5919
```

Diagnostics results:

```text
Matrix Smoke summary:
Aggregate status: Succeeded
Child runs: 3
Artifacts: C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\TestResults\TestAssemblerConsoleApps\20260428_202008_353_matrix-smoke

Matrix Memory summary:
Aggregate status: Succeeded
Child runs: 2
Artifacts: C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\TestResults\TestAssemblerConsoleApps\20260428_202017_546_matrix-memory
```

Comparison notes:

- `matrix-smoke` shape is unchanged from the accepted focused profile recorded
  above: `safety`, `replay-reuse`, and `assistant` child runs all succeeded.
- `matrix-memory` establishes the Phase 15 memory diagnostics baseline for
  `lk` and `bnmcz`. Both runs reported `Likely failing stage:
  NoGrossFailureDetected`, `Failure message: <none>`, `Instructions retired:
  4200`, `Cycle count: 1000`, raw IPC `4.2000`, retire-normalized IPC
  `4.6667`, and zero pipeline, data, memory, load-use, control, branch,
  frontend, no-class-capacity, pinned-constraint, and dynamic-state stalls.
- Lane-class legality remained bounded to expected ALU register-group pressure:
  `lk` reported ALU rejects `4550`, lane6/DMA `0`, Branch/Control `0`,
  System `0`; `bnmcz` reported ALU rejects `5200`, lane6/DMA `0`,
  Branch/Control `0`, System `0`.
- Memory counters remained stable for both `lk` and `bnmcz`: total bursts
  `10725`, bytes transferred `85800`, wide-path successes `900`, partial-width
  issues `0`, issue-packet width drops `0`, and hard-pinned injects `0`.
- No unexplained IPC, cycle, stall, legality-reject, issue-width,
  branch/system progress, lane6, or memory-counter drift was observed.
