SPEC-like iterations for default SPEC-like matrix [250]: 1000
Configured SPEC-like iterations: 1,000
Wall-clock budgets will be auto-scaled from the prompted iteration count.

Enable extended telemetry logging? This writes heartbeat history and partial telemetry files. [y/N]: n
Telemetry logging: Minimal
Minimal logging will keep console-equivalent stdout/stderr, manifests, result metrics, and the latest heartbeat only.

=== Default SPEC-like diagnostic matrix ===
--- Running alu [NativeVLIW] ---
>>> Starting mode: SingleThreadNoVector [NativeVLIW]
SPEC-like iterations: 1,000
Mode: SingleThreadNoVector
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwSingleThread
Frontend supported: True
Elapsed: 00:00:41.6058320
Workload shape: spec-like-single-thread-int
Reference slice iterations: 36
Slice executions: 28
Reference slice instructions: 185
Aggregate retirement target: 5139
Diagnostics run completed.
IPC (retire-normalized): 3.6182
Raw cycle IPC: 1.2282
Instructions retired: 5221
Cycle count: 4251
Pipeline stalls: 2056
Active cycles: 2195
Stall share: 48.37%
Effective issue width: 2.3786
Data hazards: 0
Memory stalls: 2056
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 83
Scalar issue width [1]: 84
Scalar issue width [2]: 167
Scalar issue width [3]: 0
Scalar issue width [4]: 861
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=4251, pipeline-stall=2056, memory-stall=2056, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=1028, completed=1028
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=1028, outstanding=0, identity-balance=Balanced
Data reads: accepted=555, completed=555, bytes=4440
Data writes: accepted=473, completed=473, committed-bytes=3784
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=711424
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=4251, read-service=555, store-readiness=473, completion-publication=1028
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
Multi-lane execute count: 1112
Cluster prepared execution choices: 861
Wide-path successes: 861
Partial-width issues: 167
Decoder prepared scalar groups: 861
VT spread per bundle: 861
Issue packet prepared lane sum: 4111
Issue packet materialized lane sum: 4111
Issue packet prepared physical lane sum: 5223
Issue packet materialized physical lane sum: 5223
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7747
Materialized scalar-lane occupancy per cluster choice: 4.7747
Prepared physical lanes per cluster choice: 6.0662
Materialized physical lanes per cluster choice: 6.0662
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 4110
Non-scalar lanes retired: 1111
Retire cycles: 1443
Retired physical lanes per retire cycle: 3.6182
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
NOP elision skips: 833
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:42.3630767
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\alu
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=120, retired=145
Last observed core focus: VT=0, PC=0x2900
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running novt [NativeVLIW] ---
>>> Starting mode: WithoutVirtualThreads [NativeVLIW]
SPEC-like iterations: 1,000
Mode: WithoutVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwVectorProbe
Frontend supported: True
Elapsed: 00:00:27.8404302
Workload shape: spec-like-single-thread-vector
Reference slice iterations: 36
Slice executions: 28
Reference slice instructions: 186
Aggregate retirement target: 5167
Diagnostics run completed.
IPC (retire-normalized): 3.6182
Raw cycle IPC: 1.2282
Instructions retired: 5221
Cycle count: 4251
Pipeline stalls: 2056
Active cycles: 2195
Stall share: 48.37%
Effective issue width: 2.3786
Data hazards: 0
Memory stalls: 2056
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 83
Scalar issue width [1]: 84
Scalar issue width [2]: 167
Scalar issue width [3]: 0
Scalar issue width [4]: 861
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=4251, pipeline-stall=2056, memory-stall=2056, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=1028, completed=1028
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=1028, outstanding=0, identity-balance=Balanced
Data reads: accepted=555, completed=555, bytes=4440
Data writes: accepted=473, completed=473, committed-bytes=3784
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=711424
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=4251, read-service=555, store-readiness=473, completion-publication=1028
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
Multi-lane execute count: 1112
Cluster prepared execution choices: 861
Wide-path successes: 861
Partial-width issues: 167
Decoder prepared scalar groups: 861
VT spread per bundle: 861
Issue packet prepared lane sum: 4111
Issue packet materialized lane sum: 4111
Issue packet prepared physical lane sum: 5223
Issue packet materialized physical lane sum: 5223
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.7747
Materialized scalar-lane occupancy per cluster choice: 4.7747
Prepared physical lanes per cluster choice: 6.0662
Materialized physical lanes per cluster choice: 6.0662
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 4110
Non-scalar lanes retired: 1111
Retire cycles: 1443
Retired physical lanes per retire cycle: 3.6182
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
NOP elision skips: 833
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:28.6624649
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\novt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=120, retired=145
Last observed core focus: VT=0, PC=0x2900
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running vt [NativeVLIW] ---
>>> Starting mode: WithVirtualThreads [NativeVLIW]
SPEC-like iterations: 1,000
Mode: WithVirtualThreads
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedScalar
Frontend supported: True
Elapsed: 00:01:23.3517735
Workload shape: spec-like-rate-packed-scalar
Reference slice iterations: 8
Slice executions: 125
Reference slice instructions: 164
Aggregate retirement target: 20500
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 1.3125
Instructions retired: 21000
Cycle count: 16000
Pipeline stalls: 8250
Active cycles: 7750
Stall share: 51.56%
Effective issue width: 2.7097
Data hazards: 0
Memory stalls: 8250
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 375
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 4250
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=16000, pipeline-stall=8250, memory-stall=8250, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=4125, completed=4125
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=4125, outstanding=0, identity-balance=Balanced
Data reads: accepted=2250, completed=2250, bytes=18000
Data writes: accepted=1875, completed=1875, committed-bytes=15000
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=2592000
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=16000, read-service=2250, store-readiness=1875, completion-publication=4125
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 2375
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 3000
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 3000
SMT legality rejects by class: ALU=3000, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 5375
Class-flexible injects: 2375
Hard-pinned injects: 0
Slack reclaim ratio: 0.4419
Flexible inject share: 1.0000
Multi-lane execute count: 4250
Cluster prepared execution choices: 4250
Wide-path successes: 4250
Partial-width issues: 0
Decoder prepared scalar groups: 4250
VT spread per bundle: 8875
Issue packet prepared lane sum: 17000
Issue packet materialized lane sum: 17000
Issue packet prepared physical lane sum: 21625
Issue packet materialized physical lane sum: 21625
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.0000
Materialized scalar-lane occupancy per cluster choice: 4.0000
Prepared physical lanes per cluster choice: 5.0882
Materialized physical lanes per cluster choice: 5.0882
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 16500
Non-scalar lanes retired: 4500
Retire cycles: 4500
Retired physical lanes per retire cycle: 4.6667
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
NOP elision skips: 1500
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x08, visible=0x08, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:24.0879966
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\vt
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=128, retired=168
Last observed core focus: VT=0, PC=0x2500
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running max [NativeVLIW] ---
>>> Starting mode: PackedMixedEnvelope [NativeVLIW]
SPEC-like iterations: 1,000
Mode: PackedMixedEnvelope
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwPackedMixedEnvelope
Frontend supported: True
Elapsed: 00:01:34.5488115
Workload shape: spec-like-rate-packed-mixed
Reference slice iterations: 8
Slice executions: 125
Reference slice instructions: 165
Aggregate retirement target: 20625
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 1.3125
Instructions retired: 21000
Cycle count: 16000
Pipeline stalls: 8250
Active cycles: 7750
Stall share: 51.56%
Effective issue width: 2.7097
Data hazards: 0
Memory stalls: 8250
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 375
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 4250
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=16000, pipeline-stall=8250, memory-stall=8250, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=4125, completed=4125
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=4125, outstanding=0, identity-balance=Balanced
Data reads: accepted=2250, completed=2250, bytes=18000
Data writes: accepted=1875, completed=1875, committed-bytes=15000
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=2592000
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=16000, read-service=2250, store-readiness=1875, completion-publication=4125
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 2375
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 3000
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 3000
SMT legality rejects by class: ALU=3000, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 5375
Class-flexible injects: 2375
Hard-pinned injects: 0
Slack reclaim ratio: 0.4419
Flexible inject share: 1.0000
Multi-lane execute count: 4250
Cluster prepared execution choices: 4250
Wide-path successes: 4250
Partial-width issues: 0
Decoder prepared scalar groups: 4250
VT spread per bundle: 8875
Issue packet prepared lane sum: 17000
Issue packet materialized lane sum: 17000
Issue packet prepared physical lane sum: 21625
Issue packet materialized physical lane sum: 21625
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.0000
Materialized scalar-lane occupancy per cluster choice: 4.0000
Prepared physical lanes per cluster choice: 5.0882
Materialized physical lanes per cluster choice: 5.0882
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 16500
Non-scalar lanes retired: 4500
Retire cycles: 4500
Retired physical lanes per retire cycle: 4.6667
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
NOP elision skips: 1500
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x08, visible=0x08, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:35.2499845
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\max
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=128, retired=168
Last observed core focus: VT=0, PC=0x2500
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running lk [NativeVLIW] ---
>>> Starting mode: Lk [NativeVLIW]
SPEC-like iterations: 1,000
Mode: Lk
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwLatencyHidingLoadKernel
Frontend supported: True
Elapsed: 00:01:30.8328322
Workload shape: spec-like-latency-hiding-memory
Reference slice iterations: 8
Slice executions: 125
Reference slice instructions: 164
Aggregate retirement target: 20500
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 1.3125
Instructions retired: 21000
Cycle count: 16000
Pipeline stalls: 8250
Active cycles: 7750
Stall share: 51.56%
Effective issue width: 2.7097
Data hazards: 0
Memory stalls: 8250
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 375
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 4250
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=16000, pipeline-stall=8250, memory-stall=8250, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=4125, completed=4125
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=4125, outstanding=0, identity-balance=Balanced
Data reads: accepted=2250, completed=2250, bytes=18000
Data writes: accepted=1875, completed=1875, committed-bytes=15000
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=2592000
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=16000, read-service=2250, store-readiness=1875, completion-publication=4125
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 7000
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 1750
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 1750
SMT legality rejects by class: ALU=1750, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 8750
Class-flexible injects: 7000
Hard-pinned injects: 0
Slack reclaim ratio: 0.8000
Flexible inject share: 1.0000
Multi-lane execute count: 4250
Cluster prepared execution choices: 4250
Wide-path successes: 4250
Partial-width issues: 0
Decoder prepared scalar groups: 4250
VT spread per bundle: 12625
Issue packet prepared lane sum: 17000
Issue packet materialized lane sum: 17000
Issue packet prepared physical lane sum: 21625
Issue packet materialized physical lane sum: 21625
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 4.0000
Materialized scalar-lane occupancy per cluster choice: 4.0000
Prepared physical lanes per cluster choice: 5.0882
Materialized physical lanes per cluster choice: 5.0882
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 16500
Non-scalar lanes retired: 4500
Retire cycles: 4500
Retired physical lanes per retire cycle: 4.6667
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
NOP elision skips: 1500
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x06, visible=0x06, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:31.8160181
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\lk
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=128, retired=168
Last observed core focus: VT=0, PC=0x2500
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running bnmcz [NativeVLIW] ---
>>> Starting mode: Bnmcz [NativeVLIW]
SPEC-like iterations: 1,000
Mode: Bnmcz
Validation surface: primary
Frontend profile: NativeVLIW
Program variant: NativeVliwBankNoConflictMixedZoo
Frontend supported: True
Elapsed: 00:01:39.7619238
Workload shape: spec-like-bank-rotated-memory
Reference slice iterations: 8
Slice executions: 125
Reference slice instructions: 164
Aggregate retirement target: 20500
Diagnostics run completed.
IPC (retire-normalized): 4.6667
Raw cycle IPC: 1.3125
Instructions retired: 21000
Cycle count: 16000
Pipeline stalls: 8250
Active cycles: 7750
Stall share: 51.56%
Effective issue width: 2.7097
Data hazards: 0
Memory stalls: 8250
Load-use bubbles: 0
WAW hazards: 0
Control hazards: 0
Branch mispredicts: 0
Frontend stalls: 0
Scalar issue width [0]: 375
Scalar issue width [1]: 0
Scalar issue width [2]: 0
Scalar issue width [3]: 0
Scalar issue width [4]: 4125
Total bursts: 0
Bytes transferred: 0
Timing/memory comparison schema: timing-memory-report/v3 / producer memory-cycle-telemetry-v2
Timing comparison policy: Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.
Cycle decomposition: total=16000, pipeline-stall=8250, memory-stall=8250, non-memory-stall=0
Fine-grained cycle breakdown: Unavailable below the exact memory/non-memory top-level partition: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. WAW is an event counter, not a general cycle bucket. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.
Memory telemetry disposition: ProducerTelemetryAvailable
Memory telemetry note: Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity.
Memory requests: accepted=4125, completed=4125
Memory request lifecycle: telemetry-baseline-outstanding=0, canceled=0, consumed=4125, outstanding=0, identity-balance=Balanced
Data reads: accepted=2250, completed=2250, bytes=18000
Data writes: accepted=1875, completed=1875, committed-bytes=15000
Instruction fetch: accepted=Unavailable, completed=Unavailable, physical-read-bytes=2592000
Admission rejects: queue-full=0, bank-conflict=Unavailable
Controller cycle bounds: edges=16000, read-service=2250, store-readiness=1875, completion-publication=4125
Scheduler diagnostic policy: LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.
Eligibility-mask policy: Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.
NOPs avoided: 6625
NOPs due to no class capacity: 0
NOPs due to pinned constraint: 0
NOPs due to resource conflict: 2000
NOPs due to dynamic state: 0
Last SMT legality reject kind: None
Last SMT legality authority source: StructuralCertificate
SMT owner-context guard rejects: 0
SMT domain guard rejects: 0
SMT boundary guard rejects: 0
SMT shared-resource certificate rejects: 0
SMT register-group certificate rejects: 2000
SMT legality rejects by class: ALU=2000, LSU=0, DMA/Stream=0, Branch/Control=0, System=0
Slack reclaim attempts: 8625
Class-flexible injects: 6625
Hard-pinned injects: 0
Slack reclaim ratio: 0.7681
Flexible inject share: 1.0000
Multi-lane execute count: 4125
Cluster prepared execution choices: 4250
Wide-path successes: 4250
Partial-width issues: 0
Decoder prepared scalar groups: 4250
VT spread per bundle: 12500
Issue packet prepared lane sum: 16500
Issue packet materialized lane sum: 16500
Issue packet prepared physical lane sum: 21000
Issue packet materialized physical lane sum: 21000
Issue packet width drops: 0
Prepared scalar-projection lanes per cluster choice: 3.8824
Materialized scalar-lane occupancy per cluster choice: 3.8824
Prepared physical lanes per cluster choice: 4.9412
Materialized physical lanes per cluster choice: 4.9412
Physical lane realization rate: 1.0000
Physical lane loss per cluster choice: 0.0000
Width-drop share: 0.0000
Scalar lanes retired: 16500
Non-scalar lanes retired: 4500
Retire cycles: 4500
Retired physical lanes per retire cycle: 4.6667
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
NOP elision skips: 1500
Eligibility masked cycles: 0
Eligibility masked ready candidates: 0
Eligibility masks: requested=0x0F, normalized=0x0F, ready=0x06, visible=0x06, masked=0x00
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:40.5574353
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\bnmcz
Last checkpoint: Completed (ExecuteMeasuredProgram completed successfully.)
Last observed progress: cycles=128, retired=168
Last observed core focus: VT=0, PC=0x2500
Likely blocked phase: the phase immediately after the last published checkpoint

--- Running replay [NativeVLIW] ---
=== Replay phase pair ===
SPEC-like iterations: 1,000
Replay pair summary:
Iterations: 1,000
Stable phase: hits=3000, misses=0, hit-rate=100.00%, checks-saved=18000, invalidations=3000
Rotating phase: hits=3000, misses=0, hit-rate=100.00%, checks-saved=18000, invalidations=3999
Replay-aware cycle delta (stable - rotating): 0
Ready-hit delta (stable - rotating): 0
Checks-saved delta (stable - rotating): 0
Phase-mismatch invalidation delta (stable - rotating): -999
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.3743819
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\replay

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
Elapsed: 00:00:00.3870282
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\safety

--- Running replay-reuse [NativeVLIW] ---
=== Replay template reuse diagnostics ===
SPEC-like iterations: 1,000
Template aggregate: attempts=4000, hits=999, misses=3001, hit-rate=24.97%
Invalidations: phase-key=999, structural=999, boundary=999, witness-accesses=4000, fallback-to-live-witness=3001
stable replay-template reuse: attempts=1000, hits=999, misses=1, warmup-misses=1, fallback-to-live-witness=1, passed=True
phase-key invalidation: attempts=1000, hits=0, misses=1000, warmup-misses=1, fallback-to-live-witness=1000, passed=True
structural-identity invalidation: attempts=1000, hits=0, misses=1000, warmup-misses=1, fallback-to-live-witness=1000, passed=True
boundary-state invalidation: attempts=1000, hits=0, misses=1000, warmup-misses=1, fallback-to-live-witness=1000, passed=True
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:00:00.5903830
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\replay-reuse

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
Elapsed: 00:00:00.3618125
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\assistant

--- Running stream-vector [NativeVLIW] ---
=== Stream/Vector SPEC-like suite ===
SPEC-like iterations: 1,000
Suite aggregate: scenarios=6, passed=6, dynamic-instructions=78,000, vector-elements=424,000, modeled-bytes=4,260,000, checksum=0xDE1EF81CFD41AF21
sgemm-4x4-stream-rows: passed=True, instructions=48,000, elements=192,000, bytes=1,792,000, error=0, checksum=0xEE7EFA71580F6465
fir-vdotf-windowed-dsp: passed=True, instructions=4,000, elements=32,000, bytes=272,000, error=0, checksum=0xE621987091CE13F5
predicate-compress-filter: passed=True, instructions=2,000, elements=32,000, bytes=256,000, error=0, checksum=0x9D826A20B34550B9
crypto-bitmix-popcount: passed=True, instructions=6,000, elements=96,000, bytes=1,088,000, error=0, checksum=0x516AB2616F98B2AE
hydro-row-stencil-5point: passed=True, instructions=16,000, elements=64,000, bytes=768,000, error=0, checksum=0xCA5B6EC3F52B463D
dma-lane6-token-contract: passed=True, instructions=2,000, elements=8,000, bytes=84,000, error=0, checksum=0x97DE91B07C909900
Run status: Succeeded
Worker exit code: 0
Elapsed: 00:01:38.1284370
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\stream-vector

--- Running matrix-tile [NativeVLIW] ---
=== MatrixTile SPEC-like pressure suite ===
SPEC-like iterations: 1,000
Suite aggregate: scenarios=17, passed=17, runtime-instructions=12,026, compiler-emissions=34, retire-publications=12,016, replay-round-trips=8,018, fail-closed-rejections=53, stream-bytes=48,032, checksum=0xBAC7445E577DBD5C
mtile-memory-lane6-roundtrip-pressure: passed=True, instructions=2,000, compiler=0, retire=2,000, replay=2,000, rejected=0, bytes=8,000, invalidations=2,000, checksum=0x8CCC7BC8BB0C63DD
mtile-memory-contour-varied-shape-pressure: passed=True, instructions=8,000, compiler=0, retire=8,000, replay=4,000, rejected=0, bytes=40,000, invalidations=10,000, checksum=0xEB86064C431054C5
mtile-lane6-scheduler-conflict-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=6, bytes=0, invalidations=0, checksum=0x4689ED1498CEC617
mtile-macc-numeric-policy-replay-pressure: passed=True, instructions=1,000, compiler=0, retire=1,000, replay=1,000, rejected=0, bytes=0, invalidations=0, checksum=0xDFCA927D4EC89A0C
mtile-numeric-layout-abi-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x385FB5510EDDEA78
mtile-golden-manifest-coverage-pressure: passed=True, instructions=0, compiler=0, retire=0, replay=0, rejected=10, bytes=0, invalidations=0, checksum=0xDA5091B269D6FECE
mtile-golden-json-corpus-loader-pressure: passed=True, instructions=7, compiler=0, retire=0, replay=0, rejected=3, bytes=0, invalidations=0, checksum=0x0983D721AA3A41D1
mtranspose-layout-policy-replay-pressure: passed=True, instructions=1,000, compiler=0, retire=1,000, replay=1,000, rejected=0, bytes=0, invalidations=0, checksum=0xA501EFE2FA876F40
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
Elapsed: 00:00:09.2114248
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\matrix-tile

Default SPEC-like diagnostic matrix summary:
Aggregate status: Succeeded
Child runs: 12
Artifacts: \HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix

=== Stream/Vector final benchmarks, telemetry, statistics ===
Suite: stream-vector-spec-suite, status=Passed, iterations=1,000, artifact=\HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\stream-vector\stream_vector_spec_report.json
Aggregate: scenarios=6/6, dynamic-instructions=78,000, vector-elements=424,000, modeled-bytes=4,260,000, elapsed-ms=96,344.871, checksum=0xDE1EF81CFD41AF21
Throughput: vector-elements/ms=4.40, modeled-bytes/ms=44.22, dynamic-instructions/ms=0.81
Benchmarks:
  sgemm-4x4-stream-rows: passed, algorithm=Dense SGEMM micro-kernel C=A*B, instructions=48,000, elements=192,000, bytes=1,792,000, elapsed-ms=15,076.735, elements/ms=12.73, error=0, opcodes=VLOAD/VMUL/VADD
  fir-vdotf-windowed-dsp: passed, algorithm=DSP FIR convolution, instructions=4,000, elements=32,000, bytes=272,000, elapsed-ms=9,317.899, elements/ms=3.43, error=0, opcodes=VDOTF
  predicate-compress-filter: passed, algorithm=Columnar analytics filter/pack, instructions=2,000, elements=32,000, bytes=256,000, elapsed-ms=15,607.705, elements/ms=2.05, error=0, opcodes=VCMPGT/VCOMPRESS
  crypto-bitmix-popcount: passed, algorithm=Crypto/hash bit-mixing round, instructions=6,000, elements=96,000, bytes=1,088,000, elapsed-ms=17,880.575, elements/ms=5.37, error=0, opcodes=VXOR/VSLL/VADD/VSRL/VOR/VPOPCNT
  hydro-row-stencil-5point: passed, algorithm=Hydrodynamics-like 5-point stencil, instructions=16,000, elements=64,000, bytes=768,000, elapsed-ms=19,300.193, elements/ms=3.32, error=0, opcodes=VADD
  dma-lane6-token-contract: passed, algorithm=Descriptor-backed memory-memory compute, instructions=2,000, elements=8,000, bytes=84,000, elapsed-ms=19,161.764, elements/ms=0.42, error=0, opcodes=DmaStreamCompute.Fma/DmaStreamCompute.Reduce
Stream telemetry:
  bursts=177,000, transferred-bytes=3,920,000, foreground-warm=0/0, foreground-reuse=0, foreground-bypass=0
  assist-warm=0/0, assist-reuse=0, assist-bypass=0, translation-rejects=0, backend-rejects=0
DMA lane6 telemetry:
  lane6-backend-used=True, direct-destination-writes=0, bytes-read=64,000, bytes-staged=20,000, read-bursts=4,000, modeled-latency-cycles=32,000, element-ops=8,000

=== MatrixTile final benchmarks, resources, and fail-closed diagnostics ===
Suite: matrix-tile-spec-pressure-suite, status=Passed, iterations=1,000, artifact=\HybridCPU ISE\Diagnostics\TestAssemblerConsoleApps\bin\Debug\net10.0\TestResults\TestAssemblerConsoleApps\20260908_002922_084_matrix\matrix-tile\matrix_tile_spec_report.json
Runtime closure: ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands
Aggregate: scenarios=17/17, instructions=12,026, compiler-emissions=34, retire=12,016, replay-round-trips=8,018, fail-closed=53, stream-bytes=48,032, stream-invalidations=12,002, elapsed-ms=5,076.678
Throughput: runtime-instructions/ms=2.37, compiler-emissions/ms=0.01, retire-publications/ms=2.37, stream-bytes/ms=9.46
Baselines: smoke-throughput=passed, runtime-instructions/ms=2.37, retire/ms=2.37, replay/ms=1.58, stream-bytes/ms=9.46
Scenarios:
  mtile-memory-lane6-roundtrip-pressure: passed, contour=MatrixTileMemory / MatrixTileStreamClass / lane6, instructions=2,000, retire=2,000, compiler=0, replay=2,000, rejected=0, bytes=8,000, invalidations=2,000, elapsed-ms=569.698, baseline=pass, inst/ms=3.51, bytes/ms=14.04, opcodes=MTILE_LOAD/MTILE_STORE
    resource: resource=MatrixTileMemory
    resource: slot=MatrixTileStreamClass
    resource: lane=6
    resource: channel=0
    resource: DmaStreamClass capacity conflict verified by runtime lane map
  mtile-memory-contour-varied-shape-pressure: passed, contour=MatrixTileMemory / varied descriptor shapes and SRF row windows, instructions=8,000, retire=8,000, compiler=0, replay=4,000, rejected=0, bytes=40,000, invalidations=10,000, elapsed-ms=1,722.627, baseline=pass, inst/ms=4.64, bytes/ms=23.22, opcodes=MTILE_LOAD/MTILE_STORE
    resource: shape=1x4/2x3/3x2/4x1
    resource: stride=canonical and padded row windows
    resource: publication=load retire only
    resource: store=all-or-none commit plus SRF invalidation
  mtile-lane6-scheduler-conflict-pressure: passed, contour=Scheduler / lane6 MatrixTileStreamClass capacity pressure, instructions=0, retire=0, compiler=0, replay=0, rejected=6, bytes=0, invalidations=0, elapsed-ms=42.272, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/TEST_SLOT_CLASS_CLAIM
    resource: foreground=MatrixTileStreamClass/lane6
    resource: candidate=DmaStreamClass/lane6 rejected
    resource: candidate=MatrixTileStreamClass/lane6 rejected
    resource: candidate=AluClass/LsuClass admitted on independent capacity
  mtile-macc-numeric-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=1,000, retire=1,000, compiler=0, replay=1,000, rejected=0, bytes=0, invalidations=0, elapsed-ms=265.880, baseline=pass, inst/ms=3.76, bytes/ms=0.00, opcodes=MTILE_MACC
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=SignedInt8ToInt32
    resource: layout=MaccCanonicalRowMajorAscendingK
    resource: publication=Accumulator
  mtile-numeric-layout-abi-pressure: passed, contour=MatrixTileNumericLayoutAbi / formal runtime arithmetic, instructions=0, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=12.026, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: numeric=all supported MatrixTileNumericPolicyAbi profiles byte-exact
    resource: numeric=signed/unsigned integer widening and final little-endian encoding
    resource: numeric=SignedInt64ToInt64 overflow traps before publication
    resource: numeric=Binary32ToBinary32 separate software IEEE rounding
    resource: numeric=Binary64ToBinary64 byte-exact software IEEE result
    resource: layout=Transpose non-square in-place and tampered destination addressing reject
  mtile-golden-manifest-coverage-pressure: passed, contour=MatrixTile golden manifest / runtime-owned corpus coverage, instructions=0, retire=0, compiler=0, replay=0, rejected=10, bytes=0, invalidations=0, elapsed-ms=4.788, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: golden=positive executable vectors for all canonical opcodes
    resource: golden=memory fault vectors for load/store identity
    resource: golden=descriptor and reserved carrier negatives fail closed
    resource: golden=no compiler-generated inputs and no fallback path
    resource: no-fallback=typed carrier, runtime memory, IL call target, and compiler boundary audit advertised
  mtile-golden-json-corpus-loader-pressure: passed, contour=MatrixTile WhiteBook golden JSON / production-path loader, instructions=7, retire=0, compiler=0, replay=0, rejected=3, bytes=0, invalidations=0, elapsed-ms=14.004, baseline=pass, inst/ms=0.50, bytes/ms=0.00, opcodes=MTILE_MACC/MTRANSPOSE
    resource: json=schema v1 and runtime ABI version binding
    resource: json=no compiler output and no private arithmetic oracle
    resource: json=positive MACC/transpose vectors validated against runtime ABI
    resource: json=execute/projection fault vectors fail closed
  mtranspose-layout-policy-replay-pressure: passed, contour=MatrixTileCompute / AluClass, instructions=1,000, retire=1,000, compiler=0, replay=1,000, rejected=0, bytes=0, invalidations=0, elapsed-ms=182.431, baseline=pass, inst/ms=5.48, bytes/ms=0.00, opcodes=MTRANSPOSE
    resource: resource=MatrixTileCompute
    resource: slot=AluClass
    resource: numeric=absent by operation contract
    resource: layout=TransposeCanonicalRowMajor
    resource: publication=TileState
  mtile-store-memory-fault-all-or-none-pressure: passed, contour=MatrixTileMemory / retire fault all-or-none, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=2, elapsed-ms=1.242, baseline=pass, inst/ms=0.81, bytes/ms=0.00, opcodes=MTILE_STORE
    resource: execute capture remains side-effect-free
    resource: retire reports MemoryCommitFault
    resource: all-or-none rollback preserves original memory
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-load-memory-fault-no-publication-pressure: passed, contour=MatrixTileMemory / load partial-row fault no-publication, instructions=1, retire=0, compiler=0, replay=1, rejected=1, bytes=0, invalidations=0, elapsed-ms=2.682, baseline=pass, inst/ms=0.37, bytes/ms=0.00, opcodes=MTILE_LOAD
    resource: execute captures PartialMemoryFault with precise row/address
    resource: retire reports CapturedExecutionFault
    resource: no partial tile publication before or after retire
    resource: fault-only rollback/replay preserves deterministic fault identity
  mtile-compiler-sideband-lowering-conformance: passed, contour=Compiler transport conformance, instructions=0, retire=0, compiler=4, replay=0, rejected=0, bytes=0, invalidations=0, elapsed-ms=426.906, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: MTILE_LOAD/STORE carry no compute numeric/layout sideband
    resource: MTILE_MACC preserves explicit numeric and layout sidebands in source and lowered InstructionSlotMetadata
    resource: MTRANSPOSE preserves layout-only sideband in source and lowered InstructionSlotMetadata
    resource: lowered MatrixTile memory transport is physically placed on lane6
  mtile-compiler-lowered-runtime-execution-pressure: passed, contour=Compiler lowered bundle / runtime carrier execution, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=641.322, baseline=pass, inst/ms=0.01, bytes/ms=0.01, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: lowered MTILE_LOAD materializes as runtime MatrixTileMicroOp and retires tile state
    resource: lowered MTILE_STORE materializes as runtime MatrixTileMicroOp and commits memory at retire
    resource: lowered MTILE_MACC executes runtime-owned numeric/layout arithmetic
    resource: lowered MTRANSPOSE executes runtime-owned layout permutation
  mtile-full-pipeline-e2e-pressure: passed, contour=Full pipeline E2E / compiler lowered MatrixTile fetch-decode-schedule-retire, instructions=5, retire=4, compiler=12, replay=4, rejected=8, bytes=8, invalidations=0, elapsed-ms=142.509, baseline=pass, inst/ms=0.04, bytes/ms=0.06, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: compiler-emissions=positive canonical four-op program plus targeted negative compiler emissions
    resource: fetch/decode=VliwDecoderV4 with lowered VliwBundleAnnotations
    resource: schedule=MicroOpScheduler.PackBundleIntraCoreSmt carrier placement
    resource: lane6=MTILE_LOAD/STORE scheduled as MatrixTileStreamClass on lane6
    resource: retire-only=load tile, store memory, MACC accumulator, transpose destination tile
    resource: replay=all four positive operations rollback and replay through retire-owned journal
    resource: sideband-preservation=source and decoded InstructionSlotMetadata policy identities match
    resource: fail-closed=missing/tampered/mismatched sidebands and wrong memory resource identity reject before publication
  mtile-production-stageflow-e2e-pressure: passed, contour=Production CPU stage flow / fetched compiler bundles to WB-retire, instructions=4, retire=4, compiler=4, replay=4, rejected=0, bytes=8, invalidations=0, elapsed-ms=134.511, baseline=pass, inst/ms=0.03, bytes/ms=0.06, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: fetch-ingress=test-support stages serialized compiler-produced bundle bytes and lowered annotations into pipeIF
    resource: decode=production PipelineStage_Decode and canonical scheduler/materializer path
    resource: dispatch=production PipelineStage_Execute calls MatrixTileMicroOp.Execute
    resource: writeback-retire=production PipelineStage_WriteBack calls MatrixTileMicroOp.EmitWriteBackRetireRecords
    resource: retire-only=tile state and store memory remain unchanged until WB-retire
    resource: dataflow=MTILE_LOAD tile feeds MTILE_STORE, MTILE_MACC, and MTRANSPOSE
    resource: replay=all four WB-retired operations complete rollback and deterministic replay
  mtile-production-pc-fetch-e2e-pressure: passed, contour=Production PC fetch / canonical compiler annotation ingress, instructions=4, retire=4, compiler=10, replay=4, rejected=6, bytes=8, invalidations=0, elapsed-ms=909.293, baseline=pass, inst/ms=0.00, bytes/ms=0.01, opcodes=MTILE_LOAD/MTILE_STORE/MTILE_MACC/MTRANSPOSE
    resource: ingress=NativeTransportRuntimeAdapter.EmitProgram only
    resource: transport=MainMemory -> L2 -> L1 -> pipeIF -> production decode
    resource: dataflow=loaded tile feeds store/MACC/transpose
    resource: retire-only=production WB-retire
    resource: replay=retired results rollback and replay
    resource: negatives=missing/tampered/mismatched sidebands fail before execute/retire
    resource: coherence=re-emission drops stale L1/L2 carriers and raw byte overwrite without republish rejects
  mtile-fail-closed-policy-and-resource-pressure: passed, contour=Fail-closed runtime validation, instructions=0, retire=0, compiler=0, replay=0, rejected=7, bytes=0, invalidations=0, elapsed-ms=1.957, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: retire rejects tampered policy before publication
    resource: retire rejects wrong-owner capture identity before publication
    resource: retire rejects cross-core capture identity before publication
    resource: retire rejects stale epoch capture identity before publication
    resource: retire rejects wrong MatrixTile stream resource class
    resource: retire rejects wrong MatrixTile stream direction
    resource: MatrixTileStreamClass aliases DmaStreamClass capacity on lane6
  mtile-fault-fuzz-policy-identity-pressure: passed, contour=MatrixTile fault fuzz / policy identity and descriptor negatives, instructions=0, retire=0, compiler=0, replay=0, rejected=8, bytes=0, invalidations=0, elapsed-ms=2.530, baseline=pass, inst/ms=0.00, bytes/ms=0.00, opcodes=MTILE_MACC/MTILE_LOAD
    resource: fuzz=missing/tampered numeric-layout policy identity
    resource: fuzz=wrong operation/opcode and zero ordinal identity
    resource: fuzz=load owner/channel/operation transfer identity
    resource: fuzz=no publication after rejected mutations
Done.
