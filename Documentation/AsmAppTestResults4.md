HybridCPU ISE diagnostics console
Primary runtime validation harness starting...

SPEC-like iterations for default SPEC-like matrix [250]: 500
Configured SPEC-like iterations: 500
Wall-clock budgets will be auto-scaled from the prompted iteration count.

Enable extended telemetry logging? This writes heartbeat history and partial telemetry files. [y/N]: n
Telemetry logging: Minimal
Minimal logging will keep console-equivalent stdout/stderr, manifests, result metrics, and the latest heartbeat only.

=== Default SPEC-like diagnostic matrix ===
--- Running alu [NativeVLIW] ---
>>> Starting mode: SingleThreadNoVector [NativeVLIW]
SPEC-like iterations: 500
Mode: SingleThreadNoVector
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwSingleThread
Frontend supported: True
Elapsed: 00:00:16.8184244
Workload shape: spec-like-single-thread-int
Reference slice iterations: 36
Slice executions: 14
Reference slice instructions: 185
Aggregate retirement target: 2569
Diagnostics run completed.
IPC (retire-normalized): 3.6077
Raw cycle IPC: 1.2269
Instructions retired: 2612
Cycle count: 2129
Pipeline stalls: 1028
Active cycles: 1101
Stall share: 48.29%
Effective issue width: 2.3724
Data hazards: 0
Memory stalls: 1028
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 42
Scalar issue width [1]: 42
Scalar issue width [2]: 84
Scalar issue width [3]: 0
Scalar issue width [4]: 431
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=2129, pipeline-stall=1028, memory-stall=1028, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=514, completed=514
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=514, outstanding=0, identity-balance=Balanced
Data reads: accepted=278, completed=278, bytes=2224
Data writes: accepted=236, completed=236, committed-bytes=1888
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=356608
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=2129, read-service=278, store-readiness=236, completion-publication=514
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
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
Multi-lane execute count: 557
Cluster prepared execution choices: 431
Wide-path successes: 431
Partial-width issues: 84
Decoder prepared scalar groups: 431
VT spread per bundle: 431
Issue packet prepared lane sum: 2060
Issue packet materialized lane sum: 2060
Issue packet prepared physical lane sum: 2617
Issue packet materialized physical lane sum: 2617
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7796
Materialized scalar-lane occupancy per cluster choice: 4.7796
Prepared physical lanes per cluster choice: 6.0719
Materialized physical lanes per cluster choice: 6.0719
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 2056
Non-scalar lanes retired: 556
Retire cycles: 724
Retired physical lanes per retire cycle: 3.6077
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 185
Reference slice bundle count: 75
Compiler emitted distinct VTs: 1
Compiler IR distinct VTs: 1
Compiler schedule cycle groups: 60
Compiler schedule cross-VT cycle groups: 0
Compiler schedule avg width: 3.0833
Compiler schedule avg VT spread: 1.0000
Compiler schedule max VT spread: 1
Compiler bundle count: 75
Compiler cross-VT bundles: 0
Compiler bundle avg VT spread: 0.8000
Compiler bundle max VT spread: 1
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 420
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:17.7151552
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\alu
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=140, retired=168
Last observed core focus: VT=0, PC=0x3100
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running novt [NativeVLIW] ---
>>> Starting mode: WithoutVirtualThreads [NativeVLIW]
SPEC-like iterations: 500
Mode: WithoutVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwVectorProbe
Frontend supported: True
Elapsed: 00:00:16.4532128
Workload shape: spec-like-single-thread-vector
Reference slice iterations: 36
Slice executions: 14
Reference slice instructions: 186
Aggregate retirement target: 2583
Diagnostics run completed.
IPC (retire-normalized): 3.6077
Raw cycle IPC: 1.2269
Instructions retired: 2612
Cycle count: 2129
Pipeline stalls: 1028
Active cycles: 1101
Stall share: 48.29%
Effective issue width: 2.3724
Data hazards: 0
Memory stalls: 1028
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 42
Scalar issue width [1]: 42
Scalar issue width [2]: 84
Scalar issue width [3]: 0
Scalar issue width [4]: 431
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=2129, pipeline-stall=1028, memory-stall=1028, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=514, completed=514
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=514, outstanding=0, identity-balance=Balanced
Data reads: accepted=278, completed=278, bytes=2224
Data writes: accepted=236, completed=236, committed-bytes=1888
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=356608
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=2129, read-service=278, store-readiness=236, completion-publication=514
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
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
Multi-lane execute count: 557
Cluster prepared execution choices: 431
Wide-path successes: 431
Partial-width issues: 84
Decoder prepared scalar groups: 431
VT spread per bundle: 431
Issue packet prepared lane sum: 2060
Issue packet materialized lane sum: 2060
Issue packet prepared physical lane sum: 2617
Issue packet materialized physical lane sum: 2617
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7796
Materialized scalar-lane occupancy per cluster choice: 4.7796
Prepared physical lanes per cluster choice: 6.0719
Materialized physical lanes per cluster choice: 6.0719
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 2056
Non-scalar lanes retired: 556
Retire cycles: 724
Retired physical lanes per retire cycle: 3.6077
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 186
Reference slice bundle count: 75
Compiler emitted distinct VTs: 1
Compiler IR distinct VTs: 1
Compiler schedule cycle groups: 61
Compiler schedule cross-VT cycle groups: 0
Compiler schedule avg width: 3.0492
Compiler schedule avg VT spread: 1.0000
Compiler schedule max VT spread: 1
Compiler bundle count: 75
Compiler cross-VT bundles: 0
Compiler bundle avg VT spread: 0.8133
Compiler bundle max VT spread: 1
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 420
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:17.1482697
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\novt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=140, retired=168
Last observed core focus: VT=0, PC=0x3100
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running vt [NativeVLIW] ---
>>> Starting mode: WithVirtualThreads [NativeVLIW]
SPEC-like iterations: 500
Mode: WithVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedScalar
Frontend supported: True
Elapsed: 00:00:43.5985617
Workload shape: spec-like-rate-packed-scalar
Reference slice iterations: 8
Slice executions: 63
Reference slice instructions: 164
Aggregate retirement target: 10250
Diagnostics run completed.
IPC (retire-normalized): 4.6676
Raw cycle IPC: 1.3126
Instructions retired: 10502
Cycle count: 8001
Pipeline stalls: 4126
Active cycles: 3875
Stall share: 51.57%
Effective issue width: 2.7102
Data hazards: 0
Memory stalls: 4126
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 187
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 2125
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=8001, pipeline-stall=4126, memory-stall=4126, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=2063, completed=2063
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=2063, outstanding=0, identity-balance=Balanced
Data reads: accepted=1125, completed=1125, bytes=9000
Data writes: accepted=938, completed=938, committed-bytes=7504
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=1296128
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=8001, read-service=1125, store-readiness=938, completion-publication=2063
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 1188
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 1500
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 1500
SMT legality rejects by class: ALU=1500, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 2688
Class-flexible injects: 1188
Hard-pinned injects: 0
Slack reclaim ratio: 0.4420
Flexible inject share: 1.0000
Multi-lane execute count: 2125
Cluster prepared execution choices: 2126
Wide-path successes: 2126
Partial-width issues: 0
Decoder prepared scalar groups: 2126
VT spread per bundle: 4439
Issue packet prepared lane sum: 8500
Issue packet materialized lane sum: 8500
Issue packet prepared physical lane sum: 10812
Issue packet materialized physical lane sum: 10812
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.9981
Materialized scalar-lane occupancy per cluster choice: 3.9981
Prepared physical lanes per cluster choice: 5.0856
Materialized physical lanes per cluster choice: 5.0856
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 8252
Non-scalar lanes retired: 2250
Retire cycles: 2250
Retired physical lanes per retire cycle: 4.6676
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 48
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 2.3333
Compiler schedule max VT spread: 4
Compiler bundle count: 48
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 1.7500
Compiler bundle max VT spread: 4
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 748
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x04, visible=0x04, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:44.3099428
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\vt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=65, retired=86
Last observed core focus: VT=0, PC=0x1300
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running max [NativeVLIW] ---
>>> Starting mode: PackedMixedEnvelope [NativeVLIW]
SPEC-like iterations: 500
Mode: PackedMixedEnvelope
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedMixedEnvelope
Frontend supported: True
Elapsed: 00:00:51.0696183
Workload shape: spec-like-rate-packed-mixed
Reference slice iterations: 8
Slice executions: 63
Reference slice instructions: 165
Aggregate retirement target: 10313
Diagnostics run completed.
IPC (retire-normalized): 4.6676
Raw cycle IPC: 1.3126
Instructions retired: 10502
Cycle count: 8001
Pipeline stalls: 4126
Active cycles: 3875
Stall share: 51.57%
Effective issue width: 2.7102
Data hazards: 0
Memory stalls: 4126
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 187
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 2125
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=8001, pipeline-stall=4126, memory-stall=4126, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=2063, completed=2063
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=2063, outstanding=0, identity-balance=Balanced
Data reads: accepted=1125, completed=1125, bytes=9000
Data writes: accepted=938, completed=938, committed-bytes=7504
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=1296128
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=8001, read-service=1125, store-readiness=938, completion-publication=2063
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 1188
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 1500
NOPs due to dynamic state: 0
Last SMT legality reject kind: CrossLaneConflict
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 1500
SMT legality rejects by class: ALU=1500, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 2688
Class-flexible injects: 1188
Hard-pinned injects: 0
Slack reclaim ratio: 0.4420
Flexible inject share: 1.0000
Multi-lane execute count: 2125
Cluster prepared execution choices: 2126
Wide-path successes: 2126
Partial-width issues: 0
Decoder prepared scalar groups: 2126
VT spread per bundle: 4439
Issue packet prepared lane sum: 8500
Issue packet materialized lane sum: 8500
Issue packet prepared physical lane sum: 10812
Issue packet materialized physical lane sum: 10812
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.9981
Materialized scalar-lane occupancy per cluster choice: 3.9981
Prepared physical lanes per cluster choice: 5.0856
Materialized physical lanes per cluster choice: 5.0856
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 8252
Non-scalar lanes retired: 2250
Retire cycles: 2250
Retired physical lanes per retire cycle: 4.6676
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 165
Reference slice bundle count: 49
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 37
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.4595
Compiler schedule avg VT spread: 2.2973
Compiler schedule max VT spread: 4
Compiler bundle count: 49
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 1.7347
Compiler bundle max VT spread: 4
First opcode: 0x29
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 748
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x04, visible=0x04, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:51.8169083
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\max
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=65, retired=86
Last observed core focus: VT=0, PC=0x1300
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running lk [NativeVLIW] ---
>>> Starting mode: Lk [NativeVLIW]
SPEC-like iterations: 500
Mode: Lk
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwLatencyHidingLoadKernel
Frontend supported: True
Elapsed: 00:00:43.4906574
Workload shape: spec-like-latency-hiding-memory
Reference slice iterations: 8
Slice executions: 63
Reference slice instructions: 164
Aggregate retirement target: 10250
Diagnostics run completed.
IPC (retire-normalized): 4.6676
Raw cycle IPC: 1.3126
Instructions retired: 10502
Cycle count: 8001
Pipeline stalls: 4126
Active cycles: 3875
Stall share: 51.57%
Effective issue width: 2.7102
Data hazards: 0
Memory stalls: 4126
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 187
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 2125
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=8001, pipeline-stall=4126, memory-stall=4126, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=2063, completed=2063
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=2063, outstanding=0, identity-balance=Balanced
Data reads: accepted=1125, completed=1125, bytes=9000
Data writes: accepted=938, completed=938, committed-bytes=7504
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=1296128
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=8001, read-service=1125, store-readiness=938, completion-publication=2063
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 3500
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 877
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 877
SMT legality rejects by class: ALU=877, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 4377
Class-flexible injects: 3500
Hard-pinned injects: 0
Slack reclaim ratio: 0.7996
Flexible inject share: 1.0000
Multi-lane execute count: 2125
Cluster prepared execution choices: 2126
Wide-path successes: 2126
Partial-width issues: 0
Decoder prepared scalar groups: 2126
VT spread per bundle: 6315
Issue packet prepared lane sum: 8500
Issue packet materialized lane sum: 8500
Issue packet prepared physical lane sum: 10812
Issue packet materialized physical lane sum: 10812
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.9981
Materialized scalar-lane occupancy per cluster choice: 3.9981
Prepared physical lanes per cluster choice: 5.0856
Materialized physical lanes per cluster choice: 5.0856
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 8252
Non-scalar lanes retired: 2250
Retire cycles: 2250
Retired physical lanes per retire cycle: 4.6676
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 48
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 3.0000
Compiler schedule max VT spread: 4
Compiler bundle count: 48
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 2.2500
Compiler bundle max VT spread: 4
First opcode: 0x27
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 748
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x06, visible=0x06, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:44.1653373
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\lk
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=65, retired=86
Last observed core focus: VT=0, PC=0x1300
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running bnmcz [NativeVLIW] ---
>>> Starting mode: Bnmcz [NativeVLIW]
SPEC-like iterations: 500
Mode: Bnmcz
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwBankNoConflictMixedZoo
Frontend supported: True
Elapsed: 00:00:46.1506326
Workload shape: spec-like-bank-rotated-memory
Reference slice iterations: 8
Slice executions: 63
Reference slice instructions: 164
Aggregate retirement target: 10250
Diagnostics run completed.
IPC (retire-normalized): 4.6676
Raw cycle IPC: 1.3126
Instructions retired: 10502
Cycle count: 8001
Pipeline stalls: 4126
Active cycles: 3875
Stall share: 51.57%
Effective issue width: 2.7102
Data hazards: 0
Memory stalls: 4126
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 187
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 2064
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=8001, pipeline-stall=4126, memory-stall=4126, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=2063, completed=2063
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=2063, outstanding=0, identity-balance=Balanced
Data reads: accepted=1125, completed=1125, bytes=9000
Data writes: accepted=938, completed=938, committed-bytes=7504
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=1296128
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=8001, read-service=1125, store-readiness=938, completion-publication=2063
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 3313
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 1001
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 1001
SMT legality rejects by class: ALU=1001, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 4314
Class-flexible injects: 3313
Hard-pinned injects: 0
Slack reclaim ratio: 0.7680
Flexible inject share: 1.0000
Multi-lane execute count: 2064
Cluster prepared execution choices: 2126
Wide-path successes: 2126
Partial-width issues: 0
Decoder prepared scalar groups: 2126
VT spread per bundle: 6252
Issue packet prepared lane sum: 8256
Issue packet materialized lane sum: 8256
Issue packet prepared physical lane sum: 10507
Issue packet materialized physical lane sum: 10507
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8833
Materialized scalar-lane occupancy per cluster choice: 3.8833
Prepared physical lanes per cluster choice: 4.9421
Materialized physical lanes per cluster choice: 4.9421
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 8252
Non-scalar lanes retired: 2250
Retire cycles: 2250
Retired physical lanes per retire cycle: 4.6676
Compiler stage: BundleAnnotationPublish
Decoder stage: InstructionRegistry
Likely failing stage: NoGrossFailureDetected
Failure message: <none>
Reference slice emitted instructions: 164
Reference slice bundle count: 48
Compiler emitted distinct VTs: 4
Compiler IR distinct VTs: 4
Compiler schedule cycle groups: 36
Compiler schedule cross-VT cycle groups: 32
Compiler schedule avg width: 4.5556
Compiler schedule avg VT spread: 2.6667
Compiler schedule max VT spread: 4
Compiler bundle count: 48
Compiler cross-VT bundles: 32
Compiler bundle avg VT spread: 2.0000
Compiler bundle max VT spread: 4
First opcode: 0x27
First opcode registered: True
Dominant effect: NoGrossFailureDetected
NOP elision skips: 748
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x06, visible=0x06, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:46.9001355
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\bnmcz
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=65, retired=86
Last observed core focus: VT=0, PC=0x1300
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running replay [NativeVLIW] ---
=== Replay phase pair ===
SPEC-like iterations: 500
Replay pair summary:
Iterations: 500
Stable phase: hits=1500, misses=0, hit-rate=100.00%, checks-saved=9000, invalidations=1500
Rotating phase: hits=1500, misses=0, hit-rate=100.00%, checks-saved=9000, invalidations=1999
Replay-aware cycle delta (stable - rotating): 0
Ready-hit delta (stable - rotating): 0
Checks-saved delta (stable - rotating): 0
Phase-mismatch invalidation delta (stable - rotating): -499
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.3666096
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\replay

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
Elapsed: 00:00:00.3076073
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\safety

--- Running replay-reuse [NativeVLIW] ---
=== Replay template reuse diagnostics ===
SPEC-like iterations: 500
Template aggregate: attempts=2000, hits=499, misses=1501, hit-rate=24.95%
Invalidations: phase-key=499, structural=499, boundary=499, witness-accesses=2000, fallback-to-live-witness=1501
stable replay-template reuse: attempts=500, hits=499, misses=1, warmup-misses=1, fallback-to-live-witness=1, passed=True
phase-key invalidation: attempts=500, hits=0, misses=500, warmup-misses=1, fallback-to-live-witness=500, passed=True
structural-identity invalidation: attempts=500, hits=0, misses=500, warmup-misses=1, fallback-to-live-witness=500, passed=True
boundary-state invalidation: attempts=500, hits=0, misses=500, warmup-misses=1, fallback-to-live-witness=500, passed=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.3791344
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\replay-reuse

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
Elapsed: 00:00:00.3130483
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\assistant

--- Running stream-vector [NativeVLIW] ---
=== Stream/Vector SPEC-like suite ===
SPEC-like iterations: 500
Suite aggregate: scenarios=6, passed=6, dynamic-instructions=39,000, vector-elements=212,000, modeled-bytes=2,130,000, checksum=0xDE1EF81CFD41AF21
sgemm-4x4-stream-rows: passed=True, instructions=24,000, elements=96,000, bytes=896,000, error=0, checksum=0xEE7EFA71580F6465
fir-vdotf-windowed-dsp: passed=True, instructions=2,000, elements=16,000, bytes=136,000, error=0, checksum=0xE621987091CE13F5
predicate-compress-filter: passed=True, instructions=1,000, elements=16,000, bytes=128,000, error=0, checksum=0x9D826A20B34550B9
crypto-bitmix-popcount: passed=True, instructions=3,000, elements=48,000, bytes=544,000, error=0, checksum=0x516AB2616F98B2AE
hydro-row-stencil-5point: passed=True, instructions=8,000, elements=32,000, bytes=384,000, error=0, checksum=0xCA5B6EC3F52B463D
dma-lane6-token-contract: passed=True, instructions=1,000, elements=4,000, bytes=42,000, error=0, checksum=0x97DE91B07C909900
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:48.9729661
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\stream-vector

--- Running matrix-tile [NativeVLIW] ---
=== MatrixTile SPEC-like pressure suite ===
SPEC-like iterations: 500
Suite aggregate: scenarios=17, passed=17, runtime-instructions=6,026, compiler-emissions=34, retire-publications=6,016, replay-round-trips=4,018, fail-closed-rejections=53, stream-bytes=24,032, checksum=0xF4153CF89D0CD7B5
mtile-memory-lane6-roundtrip-pressure: passed=True, instructions=1,000, compiler=0, retire=1,000, replay=1,000, rejected=0, bytes=4,000, invalidations=1,000, checksum=0xFC76EF7F00EB0D41
mtile-memory-contour-varied-shape-pressure: passed=True, instructions=4,000, compiler=0, retire=4,000, replay=2,000, rejected=0, bytes=20,000, invalidations=5,000, checksum=0x34842443434445BD
mtile-lane6-scheduler-conflict-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=6, bytes=0, invalidations=0, checksum=0x4689ED1498CEC617
mtile-macc-numeric-policy-replay-pressure: passed=True, instructions=500, compiler=0, retire=500, replay=500, rejected=0, bytes=0, invalidations=0, checksum=0x124EC0510C29512E
mtile-numeric-layout-abi-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x385FB5510EDDEA78
mtile-golden-manifest-coverage-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=10, bytes=0, invalidations=0, checksum=0xDA5091B269D6FECE
mtile-golden-json-corpus-loader-pressure: passed=True, instructions=7, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x0983D721AA3A41D1
mtranspose-layout-policy-replay-pressure: passed=True, instructions=500, compiler=0, retire=500, replay=500, rejected=0, bytes=0, invalidations=0, checksum=0xD8A7310BF300F463
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
Elapsed: 00:00:06.7662101
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\matrix-tile

Default SPEC-like diagnostic matrix summary:
Aggregate status: Succeeded
Child runs: 12
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix

=== Stream/Vector final benchmarks, telemetry, statistics ===
Suite: stream-vector-spec-suite, status=Passed, iterations=500, artifact=\HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\stream-vector\stream_vector_spec_report.json
Aggregate: scenarios=6/6, dynamic-instructions=39,000, vector-elements=212,000, modeled-bytes=2,130,000, elapsed-ms=47,461.623, checksum=0xDE1EF81CFD41AF21
Throughput: vector-elements/ms=4.47, modeled-bytes/ms=44.88, dynamic-instructions/ms=0.82
Benchmarks:
  sgemm-4x4-stream-rows: passed, algorithm=Dense SGEMM micro-kernel C=A*B, instructions=24,000, elements=96,000, bytes=896,000, elapsed-ms=10,357.735, elements/ms=9.27, error=0, opcodes=VLOAD/VMUL/VADD
  fir-vdotf-windowed-dsp: passed, algorithm=DSP FIR convolution, instructions=2,000, elements=16,000, bytes=136,000, elapsed-ms=3,850.101, elements/ms=4.16, error=0, opcodes=VDOTF
  predicate-compress-filter: passed, algorithm=Columnar analytics filter/pack, instructions=1,000, elements=16,000, bytes=128,000, elapsed-ms=10,383.136, elements/ms=1.54, error=0, opcodes=VCMPGT/VCOMPRESS
  crypto-bitmix-popcount: passed, algorithm=Crypto/hash bit-mixing round, instructions=3,000, elements=48,000, bytes=544,000, elapsed-ms=9,954.991, elements/ms=4.82, error=0, opcodes=VXOR/VSLL/VADD/VSRL/VOR/VPOPCNT
  hydro-row-stencil-5point: passed, algorithm=Hydrodynamics-like 5-point stencil, instructions=8,000, elements=32,000, bytes=384,000, elapsed-ms=9,016.451, elements/ms=3.55, error=0, opcodes=VADD
  dma-lane6-token-contract: passed, algorithm=Descriptor-backed memory-memory compute, instructions=1,000, elements=4,000, bytes=42,000, elapsed-ms=3,899.209, elements/ms=1.03, error=0, opcodes=DmaStreamCompute.Fma/DmaStreamCompute.Reduce
Stream telemetry:
  bursts=88,500, transferred-bytes=1,960,000, foreground-warm=0/0, foreground-reuse=0, foreground-bypass=0
  assist-warm=0/0, assist-reuse=0, assist-bypass=0, translation-rejects=0, backend-rejects=0
DMA lane6 telemetry:
  lane6-backend-used=True, direct-destination-writes=0, bytes-read=32,000, bytes-staged=10,000, read-bursts=2,000, modeled-latency-cycles=16,000, element-ops=4,000

=== MatrixTile final benchmarks, resources, and fail-closed diagnostics ===
Suite: matrix-tile-spec-pressure-suite, status=Passed, iterations=500, artifact=\HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260911_221640_058_matrix\matrix-tile\matrix_tile_spec_report.json
Runtime closure: ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands
Aggregate: scenarios=17/17, instructions=6,026, compiler-emissions=34, retire=6,016, replay-round-trips=4,018, fail-closed=53, stream-bytes=24,032, stream-invalidations=6,002, elapsed-ms=3,177.914
Throughput: runtime-instructions/ms=1.90, compiler-emissions/ms=0.01, retire-publications/ms=1.89, stream-bytes/ms=7.56
Baselines: smoke-throughput=passed, runtime-instructions/ms=1.90, retire/ms=1.89, replay/ms=1.26, stream-bytes/ms=7.56
Scenarios:
  mtile-memory-lane6-roundtrip-pressure: passed, contour=MatrixTileMemory / MatrixTileStreamClass / lane6, instructions=1,000, retire=1,000, compiler=0, replay=1,000, rejected=0, bytes=4,000, invalidations=1,000, elapsed-ms=318.509, baseline=pass, inst/ms=3.14, bytes/ms=12.56, opcodes=MTILE_LOAD/MTILE_STORE
    resource: resource=MatrixTileMemory
    resource: slot=MatrixTileStreamClass
    resource: lane=6
    resource: channel=0
    resource: DmaStreamClass capacity conflict verified by runtime lane map
  mtile-memory-contour-varied-shape-pressure: passed, contour=MatrixTileMemory / varied descriptor shapes and SRF row windows, instructions=4,000, retire=4,000, compiler=0, replay=2,000, rejected=0, bytes=20,000, invalidations=5,000, elapsed-ms=479.809, baseline=pass, inst/ms=8.34, bytes/ms=41.68, opcodes=MTILE_LOAD/MTILE_STORE
    resource: shape=1x4/2x3/3x2/4x1
    resource: stride=canonical and padded row windows
    resource: publication=load retire only
    resource: store=all-or-none commit plus SRF invalidation
  mtile-lane6-scheduler-conflict-pressure: passed, contour=Scheduler / lane6 MatrixTileStreamClass capacity pressure, instructions=0, retire=0, compiler=0, replay=0, rejected=6, bytes=0, invalidations=0, elapsed-ms=43.758, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/TEST_SLOT_CLASS_CLAIM
    resource: foreground=MatrixTileStreamClass/lane6
    resource: candidate=DmaStreamClass/lane6 rejected
    resource: candidate=MatrixTileStreamClass/lane6 rejected
    resource: candidate=AluClass/LsuClass admitted on independent capacity
  mtile-macc-numeric-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=500, retire=500, compiler=0, replay=500, rejected=0, bytes=0, invalidations=0, elapsed-ms=112.488, baseline=pass, inst/ms=4.44, bytes/ms=0.00, opcodes=MTILE_MACC
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=SignedInt8ToInt32
    resource: layout=MaccCanonicalRowMajorAscendingK
    resource: publication=Accumulator
  mtile-numeric-layout-abi-pressure: passed, contour=MatrixTileNumericLayoutAbi / formal runtime arithmetic, instructions=0, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=9.924, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: numeric=all supported MatrixTileNumericPolicyAbi profiles byte-exact
    resource: numeric=signed/unsigned integer widening and final little-endian encoding
    resource: numeric=SignedInt64ToInt64 overflow traps before publication
    resource: numeric=Binary32ToBinary32 separate software IEEE rounding
    resource: numeric=Binary64ToBinary64 byte-exact software IEEE result
    resource: layout=Transpose non-square in-place and tampered destination addressing reject
  mtile-golden-manifest-coverage-pressure: passed, contour=MatrixTile golden manifest / runtime-owned corpus coverage, instructions=0, retire=0, compiler=0, replay=0, rejected=10, bytes=0, invalidations=0, elapsed-ms=4.248, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: golden=positive executable vectors for all canonical opcodes
    resource: golden=memory fault vectors for load/store identity
    resource: golden=descriptor and reserved carrier negatives fail closed
    resource: golden=no compiler-generated inputs and no fallback path
    resource: no-fallback=typed carrier, runtime memory, IL call target, and compiler boundary audit advertised
  mtile-golden-json-corpus-loader-pressure: passed, contour=MatrixTile WhiteBook golden JSON / production-path loader, instructions=7, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=9.962, baseline=pass, inst/ms=0.70, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: json=schema v1 and runtime ABI version binding
    resource: json=no compiler output and no private arithmetic oracle
    resource: json=positive MACC/transpose vectors validated against runtime ABI
    resource: json=execute/projection fault vectors fail closed
  mtranspose-layout-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=500, retire=500, compiler=0, replay=500, rejected=0, bytes=0, invalidations=0, elapsed-ms=82.664, baseline=pass, inst/ms=6.05, bytes/ms=0.00, opcodes=MTRANSPOSE
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=absent by operation contract
    resource: layout=TransposeCanonicalRowMajor
    resource: publication=TileState
  mtile-store-memory-fault-all-or-none-pressure: passed, contour=MatrixTileMemory / retire fault all-or-none, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=2, elapsed-ms=3.397, baseline=pass, inst/ms=0.29, bytes/ms=0.00, opcodes=MTILE_STORE
    resource: execute capture remains side-effect-free
    resource: retire reports MemoryCommitFault
    resource: all-or-none rollback preserves original memory
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-load-memory-fault-no-publication-pressure: passed, contour=MatrixTileMemory / load partial-row fault no-publication, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=0, elapsed-ms=2.030, baseline=pass, inst/ms=0.49, bytes/ms=0.00, opcodes=MTILE_LOAD
    resource: execute captures PartialMemoryFault with precise row/address
    resource: retire reports CapturedExecutionFault
    resource: no partial tile publication before or after retire
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-compiler-sideband-lowering-conformance: passed, contour=Compiler transport conformance, instructions=0, retire=0, compiler=4, replay=0, rejected=0, bytes=0, invalidations=0, elapsed-ms=318.179, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: MTILE_LOAD/STORE carry no compute numeric/layout sideband
    resource: MTILE_MACC preserves explicit numeric and layout sidebands in source and lowered InstructionSlotMetadata
    resource: MTRANSPOSE preserves layout-only sideband in source and lowered InstructionSlotMetadata
    resource: lowered MatrixTile memory transport is physically placed on lane6
  mtile-compiler-lowered-runtime-execution-pressure: passed, contour=Compiler lowered bundle / runtime carrier execution, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=490.245, baseline=pass, inst/ms=0.01, bytes/ms=0.02, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: lowered MTILE_LOAD materializes as runtime MatrixTileMicroOp and retires tile state
    resource: lowered MTILE_STORE materializes as runtime MatrixTileMicroOp and commits memory at retire
    resource: lowered MTILE_MACC executes runtime-owned numeric/layout arithmetic
    resource: lowered MTRANSPOSE executes runtime-owned layout permutation
  mtile-full-pipeline-e2e-pressure: passed, contour=Full pipeline E2E / compiler lowered MatrixTile fetch-decode-schedule-retire, instructions=5, retire=4, compiler=12, replay=4, rejected=8, bytes=8, invalidations=0, elapsed-ms=237.085, baseline=pass, inst/ms=0.02, bytes/ms=0.03, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: compiler-emissions=positive canonical four-op program plus targeted negative compiler emissions
    resource: fetch/decode=VliwDecoderV4 with lowered VliwBundleAnnotations
    resource: schedule=MicroOpScheduler.PackBundleIntraCoreSmt carrier placement
    resource: lane6=MTILE_LOAD/STORE scheduled as MatrixTileStreamClass on lane6
    resource: retire-only=load tile, store memory, MACC accumulator, transpose destination tile
    resource: replay=all four positive operations rollback and replay through retire-owned journal
    resource: sideband-preservation=source and decoded InstructionSlotMetadata policy identities match
    resource: fail-closed=missing/tampered/mismatched sidebands and wrong memory resource identity reject before publication
  mtile-production-stageflow-e2e-pressure: passed, contour=Production CPU stage flow / fetched compiler bundles to WB-retire, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=194.824, baseline=pass, inst/ms=0.02, bytes/ms=0.04, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: fetch-ingress=test-support stages serialized compiler-produced bundle bytes and lowered annotations into pipeIF
    resource: decode=production PipelineStage_Decode and canonical scheduler/materializer path
    resource: dispatch=production PipelineStage_Execute calls MatrixTileMicroOp.Execute
    resource: writeback-retire=production PipelineStage_WriteBack calls MatrixTileMicroOp.EmitWriteBackRetireRecords
    resource: retire-only=tile state and store memory remain unchanged until WB-retire
    resource: dataflow=MTILE_LOAD tile feeds MTILE_STORE, MTILE_MACC, and MTRANSPOSE
    resource: replay=all four WB-retired operations complete rollback and deterministic replay
  mtile-production-pc-fetch-e2e-pressure: passed, contour=Production PC fetch / canonical compiler annotation ingress, instructions=4, retire=4, compiler=10, replay=4, rejected=6, bytes=8, invalidations=0, elapsed-ms=862.225, baseline=pass, inst/ms=0.00, bytes/ms=0.01, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: ingress=NativeTransportRuntimeAdapter.EmitProgram only
    resource: transport=MainMemory -> L2 -> L1 -> pipeIF -> production decode
    resource: dataflow=loaded tile feeds store/MACC/transpose
    resource: retire-only=production WB-retire
    resource: replay=retired results rollback and replay
    resource: negatives=missing/tampered/mismatched sidebands fail before execute/retire
    resource: coherence=re-emission drops stale L1/L2 carriers and raw byte overwrite without republish rejects
  mtile-fail-closed-policy-and-resource-pressure: passed, contour=Fail-closed runtime validation, instructions=0, retire=0, compiler=0, replay=0, rejected=7, bytes=0, invalidations=0, elapsed-ms=5.414, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: retire rejects tampered policy before publication
    resource: retire rejects wrong-owner capture identity before publication
    resource: retire rejects cross-core capture identity before publication
    resource: retire rejects stale epoch capture identity before publication
    resource: retire rejects wrong MatrixTile stream resource class
    resource: retire rejects wrong MatrixTile stream direction
    resource: MatrixTileStreamClass aliases DmaStreamClass capacity on lane6
  mtile-fault-fuzz-policy-identity-pressure: passed, contour=MatrixTile fault fuzz / policy identity and descriptor negatives, instructions=0, retire=0, compiler=0, replay=0, rejected=8, bytes=0, invalidations=0, elapsed-ms=3.153, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: fuzz=missing/tampered numeric-layout policy identity
    resource: fuzz=wrong operation/opcode and zero ordinal identity
    resource: fuzz=load owner/channel/operation transfer identity
    resource: fuzz=no publication after rejected mutations
Done.