#if TESTING
using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        /// <summary>
        /// TEST-ONLY partial extension for CPU_Core.
        ///
        /// Purpose: Provides test helper methods for injecting synthetic micro-ops
        /// into the FSP scheduler without requiring real multi-threaded execution.
        ///
        /// This code is ONLY for testing and does NOT modify ISA, encoding, or production behavior.
        /// No changes to instruction decode, VLIW bundle packing, or register file structure.
        ///
        /// Created: 2026-03-02
        /// For: Performance test infrastructure (testPerfPlan.md Iteration 2)
        /// </summary>
        public partial struct CPU_Core
        {
            #region Test-Only Methods

            /// <summary>
            /// TEST-ONLY: Initialize FSP scheduler in test mode.
            /// Must be called before using test nomination methods.
            /// </summary>
            internal void TestInitializeFSPScheduler()
            {
                if (_fspScheduler == null)
                {
                    _fspScheduler = new YAKSys_Hybrid_CPU.Core.MicroOpScheduler();
                }
                _fspScheduler.InitializeTestMode();
            }

            /// <summary>
            /// TEST-ONLY: Nominate a synthetic micro-op from a virtual thread.
            /// Simulates background thread nomination without actual threading.
            /// </summary>
            /// <param name="vtId">Virtual thread ID (0-3 for 4-way SMT)</param>
            /// <param name="op">Micro-op to nominate</param>
            internal void TestNominateMicroOp(int vtId, YAKSys_Hybrid_CPU.Core.MicroOp op)
            {
                if (_fspScheduler == null)
                {
                    throw new InvalidOperationException(
                        "FSP Scheduler not initialized. Call TestInitializeFSPScheduler() first.");
                }

                if (!_fspScheduler.TestMode)
                {
                    throw new InvalidOperationException(
                        "FSP Scheduler not in test mode. Call TestInitializeFSPScheduler() first.");
                }

                _fspScheduler.TestEnqueueMicroOp(vtId, op);
            }

            /// <summary>
            /// TEST-ONLY: Get the FSP scheduler instance for direct test access.
            /// Allows tests to inspect scheduler state and counters.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.MicroOpScheduler? TestGetFSPScheduler()
            {
                return _fspScheduler;
            }

            /// <summary>
            /// TEST-ONLY: Override the core-owned interrupt dispatch seam for focused guardrails.
            /// </summary>
            internal void TestSetInterruptDispatcher(Func<Processor.DeviceType, ushort, ulong, byte> interruptDispatcher)
            {
                ArgumentNullException.ThrowIfNull(interruptDispatcher);
                _interruptDispatcher = interruptDispatcher;
            }

            /// <summary>
            /// TEST-ONLY: Restore the default interrupt dispatch seam.
            /// </summary>
            internal void TestResetInterruptDispatcher()
            {
                _interruptDispatcher = DefaultInterruptDispatcher;
            }

            /// <summary>
            /// TEST-ONLY: snapshot the live EX forwarding path after execute-stage work.
            /// </summary>
            internal ForwardingPath TestGetExecuteForwardingPath()
            {
                return forwardEX;
            }

            /// <summary>
            /// TEST-ONLY: snapshot the live MEM forwarding path after memory-stage work.
            /// </summary>
            internal ForwardingPath TestGetMemoryForwardingPath()
            {
                return forwardMEM;
            }

            /// <summary>
            /// TEST-ONLY: observe reference-raw execute entries after EA-03 removed the
            /// production pipeline counter from PipelineControl.
            /// </summary>
            internal ulong TestGetReferenceRawFallbackCount()
            {
                return _testReferenceRawFallbackCount;
            }

            /// <summary>
            /// TEST-ONLY: drive the production assist-candidate creation seam against one seed.
            /// </summary>
            internal bool TestTryCreateAssistCandidate(
                YAKSys_Hybrid_CPU.Core.MicroOp seed,
                int carrierVirtualThreadId,
                out YAKSys_Hybrid_CPU.Core.AssistMicroOp assistMicroOp)
            {
                return TryCreateAssistCandidate(seed, carrierVirtualThreadId, out assistMicroOp);
            }

            /// <summary>
            /// TEST-ONLY: Check if FSP scheduler is initialized and in test mode.
            /// </summary>
            internal bool TestIsFSPSchedulerReady()
            {
                return _fspScheduler != null && _fspScheduler.TestMode;
            }

            /// <summary>
            /// TEST-ONLY: drive the retire-authoritative DmaStreamCompute token commit seam.
            /// </summary>
            internal DmaStreamComputeCommitResult TestApplyDmaStreamComputeTokenCommit(
                DmaStreamComputeToken token,
                DmaStreamComputeOwnerGuardDecision commitGuardDecision)
            {
                return ApplyRetiredDmaStreamComputeTokenCommit(token, commitGuardDecision);
            }

            /// <summary>
            /// TEST-ONLY: Clear all test nominations and reset counters.
            /// </summary>
            internal void TestResetFSPScheduler()
            {
                if (_fspScheduler != null && _fspScheduler.TestMode)
                {
                    _fspScheduler.TestClearNominationQueues();
                    _fspScheduler.ResetTestCounters();
                }
            }

            /// <summary>
            /// TEST-ONLY: Get count of pending nominations across all virtual threads.
            /// </summary>
            internal int TestGetPendingNominationCount()
            {
                if (_fspScheduler != null && _fspScheduler.TestMode)
                {
                    return _fspScheduler.TestGetPendingNominationCount();
                }
                return 0;
            }

            /// <summary>
            /// TEST-ONLY: Prime the loop-buffer replay phase so runtime trace integration can be exercised.
            /// </summary>
            internal void TestPrimeReplayPhase(ulong pc, ulong totalIterations, params YAKSys_Hybrid_CPU.Core.MicroOp?[] slots)
            {
                _loopBuffer.Initialize();
                TestReloadReplayPhase(pc, totalIterations, slots);
            }

            /// <summary>
            /// TEST-ONLY: Reload the replay phase without resetting epoch history so tests can model runtime-like epoch transitions.
            /// </summary>
            internal void TestReloadReplayPhase(ulong pc, ulong totalIterations, params YAKSys_Hybrid_CPU.Core.MicroOp?[] slots)
            {
                TestReloadReplayPhaseWithoutSchedulerPublication(
                    pc,
                    totalIterations,
                    slots);

                if (_fspScheduler == null)
                {
                    _fspScheduler = new YAKSys_Hybrid_CPU.Core.MicroOpScheduler();
                }

                _fspScheduler.SetReplayPhaseContext(_loopBuffer.CurrentReplayPhase);
            }

            /// <summary>
            /// TEST-ONLY: Reload the replay phase into the loop buffer without publishing it to the
            /// scheduler so tests can exercise the real runtime republish contour separately.
            /// </summary>
            internal void TestReloadReplayPhaseWithoutSchedulerPublication(
                ulong pc,
                ulong totalIterations,
                params YAKSys_Hybrid_CPU.Core.MicroOp?[] slots)
            {
                ArgumentNullException.ThrowIfNull(slots);

                _loopBuffer.BeginLoad(pc, totalIterations);
                for (int i = 0; i < slots.Length && i < 8; i++)
                {
                    _loopBuffer.StoreSlot(i, slots[i]);
                }

                _loopBuffer.CommitLoad();
            }

            /// <summary>
            /// TEST-ONLY: Invalidate the active replay phase and publish the resulting scheduler context.
            /// </summary>
            internal void TestInvalidateReplayPhase(YAKSys_Hybrid_CPU.Core.ReplayPhaseInvalidationReason reason)
            {
                _loopBuffer.Invalidate(reason);

                if (_fspScheduler == null)
                {
                    _fspScheduler = new YAKSys_Hybrid_CPU.Core.MicroOpScheduler();
                }

                _fspScheduler.SetReplayPhaseContext(_loopBuffer.CurrentReplayPhase);
            }

            /// <summary>
            /// TEST-ONLY: Publish the current loop-buffer replay phase to the scheduler through the
            /// same production helper used by runtime replay-aware paths.
            /// </summary>
            internal void TestPublishCurrentReplayPhaseToSchedulerIfNeeded()
            {
                if (_fspScheduler == null)
                {
                    _fspScheduler = new YAKSys_Hybrid_CPU.Core.MicroOpScheduler();
                }

                PublishReplayPhaseContextIfNeeded(_fspScheduler, _loopBuffer.CurrentReplayPhase);
            }

            /// <summary>
            /// TEST-ONLY: Drive the loop-buffer replay attempt seam with an explicit fetch PC
            /// and publish any resulting replay deactivation to the scheduler through the
            /// same production helper used by fetch.
            /// </summary>
            internal bool TestTryReplayFetchAtPc(ulong requestedPc)
            {
                YAKSys_Hybrid_CPU.Core.ReplayPhaseContext replayPhaseBefore = _loopBuffer.CurrentReplayPhase;
                var replayTarget = new YAKSys_Hybrid_CPU.Core.MicroOp?[8];
                bool replayHit = _loopBuffer.TryReplay(requestedPc, replayTarget);
                PublishReplayPhaseDeactivationToSchedulerIfNeeded(replayPhaseBefore);
                return replayHit;
            }

            /// <summary>
            /// TEST-ONLY: Drive the loop-buffer end-cycle seam and publish any resulting replay
            /// deactivation to the scheduler through the same production helper used at cycle end.
            /// </summary>
            internal void TestAdvanceReplayEndCycle()
            {
                YAKSys_Hybrid_CPU.Core.ReplayPhaseContext replayPhaseBefore = _loopBuffer.CurrentReplayPhase;
                _loopBuffer.EndCycle();
                PublishReplayPhaseDeactivationToSchedulerIfNeeded(replayPhaseBefore);
            }

            /// <summary>
            /// TEST-ONLY: Drive the retired serializing-boundary replay/assist publish seam.
            /// </summary>
            internal void TestHandleRetiredSerializingBoundary(bool assistBoundaryKilledThisRetireWindow = false)
            {
                HandleRetiredSerializingBoundary(assistBoundaryKilledThisRetireWindow);
            }

            /// <summary>
            /// TEST-ONLY: drive ExecutionDispatcherV4 direct retire publication through the
            /// shared retire-window helpers without using the compat-apply contour.
            /// </summary>
            internal void TestApplyExecutionDispatcherRetireWindowPublications(
                ExecutionDispatcherV4 dispatcher,
                InstructionIR instruction,
                ICanonicalCpuState state,
                ulong bundleSerial = 0,
                byte vtId = 0)
            {
                ArgumentNullException.ThrowIfNull(dispatcher);
                ArgumentNullException.ThrowIfNull(state);

                Span<RetireRecord> retireRecords =
                    stackalloc RetireRecord[DirectRetirePublicationRetireRecordCapacity];
                Span<RetireWindowEffect> retireEffects = stackalloc RetireWindowEffect[3];
                PipelineEvent?[] pipelineEvents = new PipelineEvent?[1];
                RetireWindowBatch retireBatch = new(retireRecords, retireEffects, pipelineEvents);

                dispatcher.CaptureRetireWindowPublications(
                    instruction,
                    state,
                    ref retireBatch,
                    bundleSerial,
                    vtId);

                ApplyCapturedRetireWindowBatch(
                    ref retireBatch,
                    countRetireCycle: false);
            }

            /// <summary>
            /// TEST-ONLY: explicit direct stream compat executor used by regression suites after
            /// the public runtime helper surface was removed in EA-02.
            /// </summary>
            internal void TestExecuteDirectStreamCompat(
                in VLIW_Instruction instruction,
                int ownerThreadId = -1)
            {
                StreamExecutionRequest request =
                    StreamExecutionRequest.CreateValidatedCompatIngress(in instruction);
                int executionOwnerThreadId =
                    ownerThreadId >= 0 ? ownerThreadId : ReadActiveVirtualThreadId();

                if (request.IsUnsupportedControlHelperSurface)
                {
                    throw CreateUnsupportedDirectStreamCompatControlSurfaceException(request.OpCode);
                }

                if (request.IsUnsupportedZeroLengthHelperSurface)
                {
                    throw CreateUnsupportedZeroLengthDirectStreamCompatSurfaceException(in request);
                }

                if (request.IsUnsupportedScalarizedVectorHelperSurface)
                {
                    throw CreateUnsupportedScalarizedVectorDirectStreamCompatSurfaceException(in request);
                }

                if (request.RequiresRetireVisibleScalarCarrier ||
                    request.RequiresPredicateStateCarrier)
                {
                    Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
                    Span<RetireWindowEffect> retireEffects = stackalloc RetireWindowEffect[1];
                    PipelineEvent?[] pipelineEvents = Array.Empty<PipelineEvent?>();
                    RetireWindowBatch retireBatch = new(retireRecords, retireEffects, pipelineEvents);

                    YAKSys_Hybrid_CPU.Execution.StreamEngine.CaptureRetireWindowPublications(
                        ref this,
                        in request,
                        ref retireBatch,
                        executionOwnerThreadId);

                    ApplyCapturedRetireWindowBatch(
                        ref retireBatch,
                        countRetireCycle: false);
                    return;
                }

                if (request.RequiresMemoryVisibleCarrier)
                {
                    throw new InvalidOperationException(
                        $"Stream opcode 0x{request.OpCode:X} reached the test-only direct stream compat executor without an authoritative retire/apply contour for memory-visible publication. " +
                        "Direct callers must reject/defer non-scalar memory-to-memory stream ops until an explicit direct compat memory carrier lands; pipeline execution remains the authoritative path.");
                }

                YAKSys_Hybrid_CPU.Execution.StreamEngine.Execute(
                    ref this,
                    in request,
                    executionOwnerThreadId);
            }

            /// <summary>
            /// TEST-ONLY: drive StreamEngine direct retire publication through the
            /// shared retire-window helpers without using the compat-apply contour.
            /// </summary>
            internal void TestApplyStreamRetireWindowPublications(
                in VLIW_Instruction instruction,
                int ownerThreadId = -1)
            {
                Span<RetireRecord> retireRecords =
                    stackalloc RetireRecord[DirectRetirePublicationRetireRecordCapacity];
                Span<RetireWindowEffect> retireEffects = stackalloc RetireWindowEffect[1];
                PipelineEvent?[] pipelineEvents = Array.Empty<PipelineEvent?>();
                RetireWindowBatch retireBatch = new(retireRecords, retireEffects, pipelineEvents);

                YAKSys_Hybrid_CPU.Execution.StreamEngine.CaptureRetireWindowPublications(
                    ref this,
                    in instruction,
                    ref retireBatch,
                    ownerThreadId);

                ApplyCapturedRetireWindowBatch(
                    ref retireBatch,
                    countRetireCycle: false);
            }

            private static InvalidOperationException CreateUnsupportedDirectStreamCompatControlSurfaceException(
                uint opCode)
            {
                return opCode switch
                {
                    (uint)InstructionsEnum.STREAM_WAIT =>
                        new InvalidOperationException(
                            $"Stream-control opcode 0x{opCode:X} reached the test-only direct stream compat executor without the authoritative serializing-boundary retire/apply follow-through. " +
                            "Direct callers must decode it through the canonical stream-control surface and use ExecutionDispatcherV4.CaptureRetireWindowPublications(...) or the mainline retire path instead of relying on compat-side stream execution/no-op behavior."),

                    (uint)InstructionsEnum.STREAM_SETUP or
                    (uint)InstructionsEnum.STREAM_START =>
                        new InvalidOperationException(
                            $"Stream-control opcode 0x{opCode:X} reached the test-only direct stream compat executor without an authoritative retire/apply contour. " +
                            "Direct callers must reject/defer STREAM_SETUP/STREAM_START on this compat surface until an explicit canonical retire/apply path lands instead of relying on compat-side stream execution/no-op behavior."),

                    _ => new InvalidOperationException(
                        $"Opcode 0x{opCode:X} reached the test-only direct stream compat executor without an authoritative stream compat contour.")
                };
            }

            private static InvalidOperationException CreateUnsupportedZeroLengthDirectStreamCompatSurfaceException(
                in StreamExecutionRequest request)
            {
                return new InvalidOperationException(
                    $"Stream opcode 0x{request.OpCode:X} reached the test-only direct stream compat executor as a zero-length helper request without an authoritative retire/apply contour. " +
                    "Direct callers must not rely on compat-side StreamEngine.Execute(...) no-op behavior for this contour; use the canonical mainline path or add an explicit direct compat follow-through before reopening it.");
            }

            private static InvalidOperationException CreateUnsupportedScalarizedVectorDirectStreamCompatSurfaceException(
                in StreamExecutionRequest request)
            {
                return new InvalidOperationException(
                    $"Stream opcode 0x{request.OpCode:X} reached the test-only direct stream compat executor through streamLength == 1 / IsScalar with a vector opcode. " +
                    "This compat surface is authoritative only for scalar ALU direct-retire contours and dedicated scalar-result VPOPC; vector compute opcodes must use the canonical vector carrier path instead of synthesizing GPR retire truth from scalarized stream encoding.");
            }

            /// <summary>
            /// TEST-ONLY: Seed scheduler-side Phase 1 metrics while keeping runtime trace emission on the real path.
            /// </summary>
            internal void TestConfigureSchedulerPhaseMetrics(YAKSys_Hybrid_CPU.Core.SchedulerPhaseMetrics metrics)
            {
                if (_fspScheduler == null)
                {
                    _fspScheduler = new YAKSys_Hybrid_CPU.Core.MicroOpScheduler();
                }

                _fspScheduler.TestSetPhaseMetrics(metrics);
            }

            /// <summary>
            /// TEST-ONLY: Drive the real write-back trace path with a synthetic retired micro-op.
            /// </summary>
            internal void TestEmitWriteBackTrace(YAKSys_Hybrid_CPU.Core.MicroOp op, ulong pc, ulong resultValue)
            {
                ArgumentNullException.ThrowIfNull(op);

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();
                pipeWB.Valid = true;
                pipeWB.PC = pc;
                pipeWB.OpCode = op.OpCode;
                pipeWB.ResultValue = resultValue;
                pipeWB.WritesRegister = false;
                pipeWB.OwnerThreadId = op.OwnerThreadId;
                pipeWB.VirtualThreadId = op.VirtualThreadId;
                pipeWB.WasFspInjected = op.IsFspInjected;
                pipeWB.OriginalThreadId = op.OwnerThreadId;
                pipeCtrl.CycleCount++;

                PipelineStage_WriteBack();
            }

            /// <summary>
            /// TEST-ONLY: Emit a dense phase timeline sample using the runtime sampling helper.
            /// </summary>
            internal void TestEmitPhaseTimelineSample(bool stalled, PipelineStallKind stallReason = PipelineStallKind.None)
            {
                pipeCtrl.Stalled = stalled;
                pipeCtrl.StallReason = stallReason;
                pipeCtrl.CycleCount++;
                RecordPhaseTimelineSample(stalled ? "STALL" : "CYCLE");
            }

            /// <summary>
            /// TEST-ONLY: Drive the legacy single-lane store retire seam through the real WB loop.
            /// Uses the same deferred store-commit path as the production pipeline.
            /// </summary>
            internal void TestRetireLegacyScalarStoreThroughWriteBack(
                ulong pc,
                ulong address,
                ulong data,
                byte accessSize,
                int vtId = 0)
            {
                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();

                var storeOp = new YAKSys_Hybrid_CPU.Core.StoreMicroOp
                {
                    OwnerThreadId = vtId,
                    VirtualThreadId = vtId,
                    OwnerContextId = vtId,
                    Address = address,
                    Value = data,
                    Size = NormalizeScalarMemoryAccessSize(accessSize),
                    SrcRegID = 1
                };
                storeOp.InitializeMetadata();

                ScalarWriteBackLaneState lane = new();
                lane.Clear(0);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = (uint)InstructionsEnum.Store;
                lane.ResultValue = data;
                lane.IsMemoryOp = true;
                lane.MemoryAddress = address;
                lane.MemoryData = data;
                lane.IsLoad = false;
                lane.MemoryAccessSize = accessSize;
                lane.WritesRegister = false;
                lane.MicroOp = storeOp;
                lane.OwnerThreadId = vtId;
                lane.VirtualThreadId = vtId;
                lane.OwnerContextId = vtId;
                lane.DefersStoreCommitToWriteBack = true;

                pipeWB.SetLane(0, lane);
                pipeWB.Valid = true;
                pipeWB.ActiveLaneIndex = 0;
                pipeWB.MaterializedPhysicalLaneCount = 1;
                pipeCtrl.CycleCount++;

                PipelineStage_WriteBack();
            }

            /// <summary>
            /// TEST-ONLY: Drive a widened LSU store through the real MEM-stage queue/fallback seam
            /// up to the point where the lane is retire-ready but has not yet entered WB.
            /// </summary>
            internal void TestPrepareExplicitPacketStoreForWriteBack(
                byte laneIndex,
                ulong pc,
                ulong address,
                ulong data,
                byte accessSize,
                int vtId = 0)
            {
                if (laneIndex < 4 || laneIndex > 5)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(laneIndex),
                        "Explicit packet LSU store tests are restricted to retire-visible lanes 4..5.");
                }

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();

                var storeOp = new YAKSys_Hybrid_CPU.Core.StoreMicroOp
                {
                    OwnerThreadId = vtId,
                    VirtualThreadId = vtId,
                    OwnerContextId = vtId,
                    Address = address,
                    Value = data,
                    Size = NormalizeScalarMemoryAccessSize(accessSize),
                    SrcRegID = 1
                };
                storeOp.InitializeMetadata();

                ScalarMemoryLaneState lane = new();
                lane.Clear(laneIndex);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = (uint)InstructionsEnum.Store;
                lane.IsMemoryOp = true;
                lane.MemoryAddress = address;
                lane.MemoryData = data;
                lane.IsLoad = false;
                lane.MemoryAccessSize = accessSize;
                lane.WritesRegister = false;
                lane.MicroOp = storeOp;
                lane.OwnerThreadId = vtId;
                lane.VirtualThreadId = vtId;
                lane.OwnerContextId = vtId;

                pipeMEM.SetLane(laneIndex, lane);
                pipeMEM.Valid = true;
                pipeMEM.ActiveLaneIndex = laneIndex;
                pipeMEM.UsesExplicitPacketLanes = true;
                pipeMEM.MaterializedPhysicalLaneCount = 1;
                pipeMEM.MaterializedScalarLaneCount = 0;
                pipeCtrl.CycleCount++;

                ExecuteExplicitPacketMemoryWork();
                var storeMemSub = GetBoundMemorySubsystem();
                if (storeMemSub != null)
                {
                    storeMemSub.AdvanceCycles(1);
                    ExecuteExplicitPacketMemoryWork();
                }

                ScalarMemoryLaneState readyLane = pipeMEM.GetLane(laneIndex);
                if (!readyLane.ResultReady || readyLane.PendingMemoryRequest != null || !readyLane.DefersStoreCommitToWriteBack)
                {
                    throw new InvalidOperationException(
                        "Explicit packet store did not reach retire-ready deferred-commit state.");
                }
            }

            /// <summary>
            /// TEST-ONLY: Drive a widened LSU load through the real MEM-stage queue/fallback seam
            /// up to the point where the lane is retire-ready but has not yet entered WB.
            /// </summary>
            internal void TestPrepareExplicitPacketLoadForWriteBack(
                byte laneIndex,
                ulong pc,
                ulong address,
                ushort destRegId,
                byte accessSize,
                int vtId = 0)
            {
                if (laneIndex < 4 || laneIndex > 5)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(laneIndex),
                        "Explicit packet LSU load tests are restricted to retire-visible lanes 4..5.");
                }

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();

                byte normalizedAccessSize = NormalizeScalarMemoryAccessSize(accessSize);
                uint opcode = normalizedAccessSize switch
                {
                    1 => (uint)InstructionsEnum.LB,
                    2 => (uint)InstructionsEnum.LH,
                    4 => (uint)InstructionsEnum.LW,
                    _ => (uint)InstructionsEnum.LD
                };

                var loadOp = new YAKSys_Hybrid_CPU.Core.LoadMicroOp
                {
                    OpCode = opcode,
                    OwnerThreadId = vtId,
                    VirtualThreadId = vtId,
                    OwnerContextId = vtId,
                    Address = address,
                    Size = normalizedAccessSize,
                    BaseRegID = 1,
                    DestRegID = destRegId,
                    WritesRegister = true
                };
                loadOp.InitializeMetadata();

                ScalarMemoryLaneState lane = new();
                lane.Clear(laneIndex);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = opcode;
                lane.IsMemoryOp = true;
                lane.MemoryAddress = address;
                lane.MemoryData = 0;
                lane.IsLoad = true;
                lane.MemoryAccessSize = accessSize;
                lane.WritesRegister = true;
                lane.DestRegID = destRegId;
                lane.MicroOp = loadOp;
                lane.OwnerThreadId = vtId;
                lane.VirtualThreadId = vtId;
                lane.OwnerContextId = vtId;

                pipeMEM.SetLane(laneIndex, lane);
                pipeMEM.Valid = true;
                pipeMEM.ActiveLaneIndex = laneIndex;
                pipeMEM.UsesExplicitPacketLanes = true;
                pipeMEM.MaterializedPhysicalLaneCount = 1;
                pipeMEM.MaterializedScalarLaneCount = 0;
                pipeCtrl.CycleCount++;

                ExecuteExplicitPacketMemoryWork();
                var loadMemSub = GetBoundMemorySubsystem();
                if (loadMemSub != null)
                {
                    loadMemSub.AdvanceCycles(1);
                    ExecuteExplicitPacketMemoryWork();
                }

                ScalarMemoryLaneState readyLane = pipeMEM.GetLane(laneIndex);
                if (!readyLane.ResultReady || readyLane.PendingMemoryRequest != null)
                {
                    throw new InvalidOperationException(
                        "Explicit packet load did not reach retire-ready completion state.");
                }

                byte[] expectedBuffer = ReadBoundMainMemory(
                    address,
                    new byte[normalizedAccessSize],
                    normalizedAccessSize,
                    "Explicit packet load test expectation");
                ulong expectedValue = DecodeExplicitPacketLoadBuffer(expectedBuffer, normalizedAccessSize);
                if (readyLane.ResultValue != expectedValue)
                {
                    throw new InvalidOperationException(
                        $"Explicit packet load resolved 0x{readyLane.ResultValue:X} but memory contains 0x{expectedValue:X}.");
                }
            }

            /// <summary>
            /// TEST-ONLY: Resolve an explicit-packet Atomic micro-op and seed the WB stage with
            /// the generated retire effect so tests can exercise the real retire/apply contour.
            /// </summary>
            internal void TestPrepareExplicitPacketAtomicForWriteBack(
                byte laneIndex,
                YAKSys_Hybrid_CPU.Core.AtomicMicroOp atomicMicroOp,
                ulong pc,
                int vtId = 0)
            {
                ArgumentNullException.ThrowIfNull(atomicMicroOp);

                if (laneIndex < 4 || laneIndex > 5)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(laneIndex),
                        "Explicit packet Atomic WB tests are restricted to retire-visible LSU lanes 4..5.");
                }

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();
                pipeID.Clear();

                atomicMicroOp.OwnerThreadId = vtId;
                atomicMicroOp.VirtualThreadId = vtId;
                atomicMicroOp.OwnerContextId = vtId;
                atomicMicroOp.RefreshWriteMetadata();

                if (!atomicMicroOp.Execute(ref this))
                {
                    throw new InvalidOperationException(
                        "Explicit packet Atomic test helper could not resolve a retire effect.");
                }

                YAKSys_Hybrid_CPU.Core.AtomicRetireEffect atomicEffect = atomicMicroOp.CreateRetireEffect();
                if (!atomicEffect.IsValid)
                {
                    throw new InvalidOperationException(
                        "Explicit packet Atomic test helper expected a valid resolved Atomic retire effect.");
                }

                ScalarWriteBackLaneState lane = new();
                lane.Clear(laneIndex);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = atomicMicroOp.OpCode;
                lane.IsMemoryOp = true;
                lane.WritesRegister = false;
                lane.DestRegID = atomicMicroOp.DestRegID;
                lane.MicroOp = atomicMicroOp;
                lane.OwnerThreadId = vtId;
                lane.VirtualThreadId = vtId;
                lane.OwnerContextId = vtId;
                lane.ResultValue = 0;
                lane.GeneratedAtomicEffect = atomicEffect;
                lane.MemoryAddress = atomicEffect.Address;
                lane.MemoryData = atomicEffect.SourceValue;
                lane.MemoryAccessSize = atomicEffect.AccessSize;

                pipeWB.SetLane(laneIndex, lane);
                pipeWB.Valid = true;
                pipeWB.ActiveLaneIndex = laneIndex;
                pipeWB.UsesExplicitPacketLanes = true;
                pipeWB.MaterializedPhysicalLaneCount = 1;
                pipeWB.MaterializedScalarLaneCount = 0;
            }

            /// <summary>
            /// TEST-ONLY: Run the real WB stage against whatever retire-ready state the test prepared.
            /// </summary>
            internal void TestRunWriteBackStage()
            {
                pipeCtrl.CycleCount++;
                PipelineStage_WriteBack();
            }

            /// <summary>
            /// TEST-ONLY: Replace the live WB stage with a prepared snapshot before running the real retire loop.
            /// </summary>
            internal void TestSetWriteBackStage(WriteBackStage writeBackStage)
            {
                pipeWB = writeBackStage;
                pipeMEM.Clear();
                pipeEX.Clear();
                pipeID.Clear();
            }

            /// <summary>
            /// TEST-ONLY: Drive one explicit packet carrier lane through the real execute→memory→write-back path.
            /// </summary>
            private void SeedExplicitPacketExecuteLane(
                byte laneIndex,
                YAKSys_Hybrid_CPU.Core.MicroOp microOp,
                ulong pc,
                int vtId)
            {
                ArgumentNullException.ThrowIfNull(microOp);

                if (laneIndex >= 8)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(laneIndex),
                        laneIndex,
                        "Explicit packet test helper expects a physical lane index in [0, 7].");
                }

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();
                pipeID.Clear();

                microOp.OwnerThreadId = vtId;
                microOp.VirtualThreadId = vtId;
                microOp.OwnerContextId = vtId;
                microOp.RefreshAdmissionMetadata();

                ScalarExecuteLaneState lane = new();
                lane.Clear(laneIndex);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = microOp.OpCode;
                lane.IsMemoryOp = microOp.IsMemoryOp;
                lane.WritesRegister = microOp.WritesRegister;
                lane.DestRegID = microOp.DestRegID;
                lane.MicroOp = microOp;
                lane.OwnerThreadId = vtId;
                lane.VirtualThreadId = vtId;
                lane.OwnerContextId = vtId;

                pipeEX.SetLane(laneIndex, lane);
                pipeEX.Valid = true;
                pipeEX.ActiveLaneIndex = laneIndex;
                pipeEX.UsesExplicitPacketLanes = true;
                pipeEX.MaterializedPhysicalLaneCount = 1;
                pipeEX.MaterializedScalarLaneCount = laneIndex < 4 ? 1 : 0;
            }

            internal void TestExecuteExplicitPacketLaneMicroOp(
                byte laneIndex,
                YAKSys_Hybrid_CPU.Core.MicroOp microOp,
                ulong pc,
                int vtId = 0)
            {
                SeedExplicitPacketExecuteLane(
                    laneIndex,
                    microOp,
                    pc,
                    vtId);

                pipeCtrl.CycleCount++;
                ExecuteExplicitPacketLanes();
            }

            internal void TestExecuteExplicitPacketLanes(
                params (byte LaneIndex, YAKSys_Hybrid_CPU.Core.MicroOp MicroOp, ulong Pc, int VtId)[] lanes)
            {
                ArgumentNullException.ThrowIfNull(lanes);
                if (lanes.Length == 0)
                {
                    throw new ArgumentException(
                        "Explicit packet multi-lane test helper requires at least one seeded lane.",
                        nameof(lanes));
                }

                pipeWB.Clear();
                pipeMEM.Clear();
                pipeEX.Clear();
                pipeID.Clear();

                bool[] occupiedLaneMap = new bool[8];
                int materializedPhysicalLaneCount = 0;
                int materializedScalarLaneCount = 0;
                byte activeLaneIndex = 0;

                for (int i = 0; i < lanes.Length; i++)
                {
                    (byte laneIndex, YAKSys_Hybrid_CPU.Core.MicroOp microOp, ulong pc, int vtId) = lanes[i];
                    ArgumentNullException.ThrowIfNull(microOp);

                    if (laneIndex >= 8)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(lanes),
                            laneIndex,
                            "Explicit packet multi-lane test helper expects every physical lane index in [0, 7].");
                    }

                    if (occupiedLaneMap[laneIndex])
                    {
                        throw new ArgumentException(
                            $"Explicit packet multi-lane test helper does not allow duplicate lane {laneIndex}.",
                            nameof(lanes));
                    }

                    occupiedLaneMap[laneIndex] = true;
                    if (materializedPhysicalLaneCount == 0)
                    {
                        activeLaneIndex = laneIndex;
                    }

                    microOp.OwnerThreadId = vtId;
                    microOp.VirtualThreadId = vtId;
                    microOp.OwnerContextId = vtId;
                    microOp.RefreshAdmissionMetadata();

                    ScalarExecuteLaneState lane = new();
                    lane.Clear(laneIndex);
                    lane.IsOccupied = true;
                    lane.PC = pc;
                    lane.OpCode = microOp.OpCode;
                    lane.IsMemoryOp = microOp.IsMemoryOp;
                    lane.WritesRegister = microOp.WritesRegister;
                    lane.DestRegID = microOp.DestRegID;
                    lane.MicroOp = microOp;
                    lane.OwnerThreadId = vtId;
                    lane.VirtualThreadId = vtId;
                    lane.OwnerContextId = vtId;

                    pipeEX.SetLane(laneIndex, lane);
                    materializedPhysicalLaneCount++;
                    if (laneIndex < 4)
                    {
                        materializedScalarLaneCount++;
                    }
                }

                pipeEX.Valid = true;
                pipeEX.ActiveLaneIndex = activeLaneIndex;
                pipeEX.UsesExplicitPacketLanes = true;
                pipeEX.MaterializedPhysicalLaneCount = materializedPhysicalLaneCount;
                pipeEX.MaterializedScalarLaneCount = materializedScalarLaneCount;

                pipeCtrl.CycleCount++;
                ExecuteExplicitPacketLanes();
            }

            /// <summary>
            /// TEST-ONLY: drive the explicit-packet epilogue branch where EX has already been
            /// cleared by execute-local control-flow redirect or equivalent lane suppression.
            /// </summary>
            internal bool TestTryConsumeEmptyExplicitPacketAfterExecution(ulong decodePc = 0x2000UL)
            {
                pipeEX.Clear();
                pipeEX.Valid = true;
                pipeEX.UsesExplicitPacketLanes = true;

                pipeID.Clear();
                pipeID.Valid = true;
                pipeID.PC = decodePc;

                return TryConsumeEmptyExplicitPacketAfterExecution();
            }

            /// <summary>
            /// TEST-ONLY: drive the explicit-packet epilogue finalization branch when at least one
            /// execute lane remains live after execute-local work.
            /// </summary>
            internal void TestCompleteExplicitPacketExecuteDispatch(
                byte occupiedLaneIndex,
                byte originalActiveLaneIndex,
                ulong decodePc = 0x2000UL)
            {
                if (occupiedLaneIndex >= 8)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(occupiedLaneIndex),
                        occupiedLaneIndex,
                        "Explicit packet epilogue test helper expects occupied lane index in [0, 7].");
                }

                pipeEX.Clear();
                ScalarExecuteLaneState lane = new();
                lane.Clear(occupiedLaneIndex);
                lane.IsOccupied = true;
                pipeEX.SetLane(occupiedLaneIndex, lane);
                pipeEX.Valid = false;
                pipeEX.ActiveLaneIndex = 0;
                pipeEX.UsesExplicitPacketLanes = true;

                pipeID.Clear();
                pipeID.Valid = true;
                pipeID.PC = decodePc;

                CompleteExplicitPacketExecuteDispatch(originalActiveLaneIndex);
            }

            internal void TestRetireExplicitPacketLaneMicroOp(
                byte laneIndex,
                YAKSys_Hybrid_CPU.Core.MicroOp microOp,
                ulong pc,
                int vtId = 0)
            {
                TestExecuteExplicitPacketLaneMicroOp(
                    laneIndex,
                    microOp,
                    pc,
                    vtId);
                PipelineStage_Memory();
                PipelineStage_WriteBack();
            }

            /// <summary>
            /// TEST-ONLY: Drive a lane-7 singleton micro-op through the real explicit-packet
            /// execute→memory→write-back path and retire it in the current cycle.
            /// </summary>
            internal void TestRetireExplicitLane7SingletonMicroOp(
                YAKSys_Hybrid_CPU.Core.MicroOp microOp,
                ulong pc,
                int vtId = 0)
            {
                TestRetireExplicitPacketLaneMicroOp(
                    laneIndex: 7,
                    microOp,
                    pc,
                    vtId);
            }

            /// <summary>
            /// TEST-ONLY: Build explicit transport facts from a decoded bundle image and publish only
            /// the narrow slot-carrier shell.
            /// </summary>
            internal void TestSetDecodedBundle(params YAKSys_Hybrid_CPU.Core.MicroOp?[] slots)
            {
                ArgumentNullException.ThrowIfNull(slots);

                YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts transportFacts =
                    BuildDecodedBundleTransportFacts(
                        pipeIF.PC,
                        slots);
                PublishCurrentResidualDecodedBundleSlotCarrierFromTransportFacts(
                    transportFacts);
            }

            internal void TestLoadReplayDecodedBundleSlotCarrier(
                ulong pc,
                params YAKSys_Hybrid_CPU.Core.MicroOp?[] slots)
            {
                ArgumentNullException.ThrowIfNull(slots);

                LoadCurrentDecodedBundleSlotCarrierFromCarriers(
                    pc,
                    slots);
            }

            internal void TestRefreshCurrentFspDerivedIssuePlan()
            {
                RefreshCurrentFspDerivedIssuePlan();
            }

            /// <summary>
            /// TEST-ONLY: Seed the decode stage and run the real execute stage once.
            /// Useful for explicit execute-surface regressions on legacy raw fallback paths.
            /// </summary>
            internal void TestRunExecuteStageWithDecodedInstruction(
                VLIW_Instruction instruction,
                YAKSys_Hybrid_CPU.Core.MicroOp? microOp = null,
                bool isVectorOp = false,
                bool isMemoryOp = false,
                bool isBranchOp = false,
                bool writesRegister = false,
                ushort reg1Id = 0,
                ushort reg2Id = 0,
                ushort reg3Id = 0,
                ulong pc = 0x1000,
                YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionExecutionMode admissionExecutionMode =
                    YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionExecutionMode.Empty)
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeID.Valid = true;
                pipeID.PC = pc;
                pipeID.Instruction = instruction;
                pipeID.OpCode = instruction.OpCode;
                pipeID.Reg1ID = reg1Id;
                pipeID.Reg2ID = reg2Id;
                pipeID.Reg3ID = reg3Id;
                pipeID.IsVectorOp = isVectorOp;
                pipeID.IsMemoryOp = isMemoryOp;
                pipeID.IsBranchOp = isBranchOp;
                pipeID.WritesRegister = writesRegister || microOp?.WritesRegister == true;
                pipeID.MicroOp = microOp;
                pipeID.AdmissionExecutionMode = admissionExecutionMode;

                PipelineStage_Execute();
            }

            internal void TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
                VLIW_Instruction instruction,
                bool isVectorOp = false,
                bool isMemoryOp = false,
                bool isBranchOp = false,
                bool writesRegister = false,
                ushort reg1Id = 0,
                ushort reg2Id = 0,
                ushort reg3Id = 0,
                ulong pc = 0x1000,
                YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionExecutionMode admissionExecutionMode =
                    YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionExecutionMode.Empty)
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeID.Valid = true;
                pipeID.PC = pc;
                pipeID.Instruction = instruction;
                pipeID.OpCode = instruction.OpCode;
                pipeID.Reg1ID = reg1Id;
                pipeID.Reg2ID = reg2Id;
                pipeID.Reg3ID = reg3Id;
                pipeID.IsVectorOp = isVectorOp;
                pipeID.IsMemoryOp = isMemoryOp;
                pipeID.IsBranchOp = isBranchOp;
                pipeID.WritesRegister = writesRegister;
                pipeID.MicroOp = null;
                pipeID.AdmissionExecutionMode = admissionExecutionMode;
                pipeIDClusterPreparation = YAKSys_Hybrid_CPU.Core.ClusterIssuePreparation.CreateEmpty(pc);
                pipeIDAdmissionPreparation = YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
                pipeIDAdmissionCandidateView = YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionCandidateView.CreateEmpty(pc);
                pipeIDAdmissionDecisionDraft = YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty(pc);
                pipeIDAdmissionHandoff = YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionHandoff.CreateEmpty(pc);

                MaterializeExecuteStageLaneState();
                LatchSingleLaneExecuteDispatchResources();
                LatchSingleLaneExecuteDispatchState();
                ReserveSingleLaneExecuteMshrScoreboardSlot();
                int decodeThreadId = GetCurrentDecodeThreadId();
                ulong operand1 = GetRegisterValueWithForwarding(decodeThreadId, pipeID.Reg2ID);
                ulong operand2 = GetRegisterValueWithForwarding(decodeThreadId, pipeID.Reg3ID);
                TestExecuteSingleLaneReferenceRawFallback(operand1, operand2);
            }

            /// <summary>
            /// TEST-ONLY: retained scalar reference executor used by regression suites after
            /// EA-03 removed the production raw execute fallback from runtime helpers.
            /// </summary>
            private void TestExecuteSingleLaneReferenceRawFallback(
                ulong operand1,
                ulong operand2)
            {
                _testReferenceRawFallbackCount++;

                if (pipeID.IsVectorOp)
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();
                    throw new InvalidOperationException(
                        $"Vector opcode 0x{pipeID.OpCode:X} reached reference raw execute fallback without an authoritative mainline MicroOp. " +
                        "Mainline vector/stream execution must enter the explicit MicroOp-owned runtime contour; direct StreamEngine fallback from the reference raw execute path is no longer permitted.");
                }

                if (pipeID.IsMemoryOp)
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();
                    throw UnsupportedExecutionSurfaceException.CreateForMemory(
                        pipeID.SlotIndex,
                        pipeID.OpCode,
                        pipeID.PC);
                }

                if (pipeID.IsBranchOp)
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();
                    throw UnsupportedExecutionSurfaceException.CreateForControlFlow(
                        pipeID.SlotIndex,
                        pipeID.OpCode,
                        pipeID.PC);
                }

                if (!IsTestReferenceRawFallbackAllowed(pipeID.OpCode))
                {
                    FailCloseSingleLaneExecuteAfterNonFaultException();
                    throw new InvalidOperationException(
                        $"Opcode 0x{pipeID.OpCode:X} is not in the test-only reference raw execute allow-list. " +
                        "Production memory/control-flow surfaces must remain on the canonical MicroOp-owned runtime contour.");
                }

                pipeEX.ResultValue = Core.ScalarAluOps.Compute(
                    pipeID.OpCode,
                    operand1,
                    operand2,
                    pipeID.Instruction.Immediate);
                pipeEX.ResultReady = true;
                pipeEX.Valid = true;
                PublishExecuteCompletionContourCertificate(
                    Core.PipelineContourOwner.ReferenceExecution,
                    Core.PipelineContourVisibilityStage.Execute,
                    pipeEX.PC,
                    (byte)(1 << pipeEX.ActiveLaneIndex));
                ConsumeDecodeStateAfterExecuteDispatch();
                PublishSingleLaneExecuteForwarding(includeTimingMetadata: false);
            }

            private static bool IsTestReferenceRawFallbackAllowed(uint opCode)
            {
                return (InstructionsEnum)opCode switch
                {
                    InstructionsEnum.Nope => true,
                    InstructionsEnum.Addition => true,
                    InstructionsEnum.Subtraction => true,
                    InstructionsEnum.Multiplication => true,
                    InstructionsEnum.Division => true,
                    InstructionsEnum.Modulus => true,
                    InstructionsEnum.AND => true,
                    InstructionsEnum.OR => true,
                    InstructionsEnum.XOR => true,
                    InstructionsEnum.ShiftLeft => true,
                    InstructionsEnum.ShiftRight => true,
                    InstructionsEnum.Move_Num => true,
                    InstructionsEnum.Move => true,
                    InstructionsEnum.ADDI => true,
                    InstructionsEnum.ANDI => true,
                    InstructionsEnum.ORI => true,
                    InstructionsEnum.XORI => true,
                    InstructionsEnum.SLTI => true,
                    InstructionsEnum.SLTIU => true,
                    InstructionsEnum.SLLI => true,
                    InstructionsEnum.SRLI => true,
                    InstructionsEnum.SRAI => true,
                    InstructionsEnum.SLT => true,
                    InstructionsEnum.SLTU => true,
                    InstructionsEnum.LUI => true,
                    InstructionsEnum.AUIPC => true,
                    InstructionsEnum.MULH => true,
                    InstructionsEnum.MULHU => true,
                    InstructionsEnum.MULHSU => true,
                    InstructionsEnum.DIVU => true,
                    InstructionsEnum.REM => true,
                    InstructionsEnum.REMU => true,
                    _ => false,
                };
            }

            /// <summary>
            /// TEST-ONLY: Seed a fetched bundle and run the real decode stage once so pipeID is
            /// populated through the production fetch→decode contour.
            /// </summary>
            internal void TestRunDecodeStageWithFetchedBundle(
                VLIW_Instruction[] rawSlots,
                ulong pc = 0x1000,
                VliwBundleAnnotations? annotations = null)
            {
                TestStageFetchedBundleForDecode(rawSlots, pc, annotations);
                PipelineStage_Decode();
            }

            internal (bool CanAdvance, PipelineStallKind StallReason, bool BankConflict, byte IssuedSlots, byte RejectedSlots)
                TestRunDecodeStageWithFetchedBundleResult(
                    VLIW_Instruction[] rawSlots,
                    ulong pc = 0x1000,
                    VliwBundleAnnotations? annotations = null)
            {
                TestStageFetchedBundleForDecode(rawSlots, pc, annotations);
                DecodeStageResult result = PipelineStage_Decode();
                return (
                    result.CanAdvance,
                    result.StallReason,
                    result.BankConflict,
                    result.IssuedSlots,
                    result.RejectedSlots);
            }

            /// <summary>
            /// TEST-ONLY: Seed a fetched bundle without running decode so full-cycle tests can
            /// exercise ExecutePipelineCycle ownership of decode-discovered stalls.
            /// </summary>
            internal void TestStageFetchedBundleForDecode(
                VLIW_Instruction[] rawSlots,
                ulong pc = 0x1000,
                VliwBundleAnnotations? annotations = null)
            {
                ArgumentNullException.ThrowIfNull(rawSlots);

                pipeIF.Clear();
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                var bundle = new VLIW_Bundle();
                for (int slotIndex = 0; slotIndex < rawSlots.Length && slotIndex < Core.BundleMetadata.BundleSlotCount; slotIndex++)
                {
                    bundle.SetInstruction(slotIndex, rawSlots[slotIndex]);
                }

                pipeIF.PC = pc;
                pipeIF.Valid = true;
                pipeIF.PrefetchComplete = true;
                pipeIF.VLIWBundle = new byte[256];
                pipeIF.BundleAnnotations = annotations ?? BuildFetchedBundleAnnotationsForTesting(rawSlots);
                pipeIF.HasBundleAnnotations = true;
                if (!bundle.TryWriteBytes(pipeIF.VLIWBundle))
                {
                    throw new InvalidOperationException(
                        "Failed to serialize fetched bundle bytes for the decode-stage test seam.");
                }

                pipelineBundleSlot = 0;
                bundleDecodedAndPacked = false;
                ResetCurrentDecodedBundleSlotCarrierState(pc);
            }

            internal (bool Valid, uint OpCode, bool IsVectorOp, bool IsMemoryOp) TestReadDecodeStageStatus()
            {
                return (
                    pipeID.Valid,
                    pipeID.OpCode,
                    pipeID.IsVectorOp,
                    pipeID.IsMemoryOp);
            }

            internal YAKSys_Hybrid_CPU.Core.MicroOp? TestReadDecodeStageMicroOp()
            {
                return pipeID.MicroOp;
            }

            internal YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionHandoff
                TestReadDecodeStageAdmissionHandoff()
            {
                return pipeIDAdmissionHandoff;
            }

            internal void TestOverrideDecodeStageMicroOp(YAKSys_Hybrid_CPU.Core.MicroOp microOp)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                pipeID.MicroOp = microOp;
            }

            internal void TestRunExecuteStageFromCurrentDecodeState()
            {
                PipelineStage_Execute();
            }

            internal void TestRecordExecutableClusterAdmissionChoice(
                YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionDecisionDraft decisionDraft)
            {
                RecordExecutableClusterAdmissionChoice(decisionDraft);
            }

            internal void TestRunMemoryStageFromCurrentExecuteState()
            {
                PipelineStage_Memory();
            }

            internal void TestSeedSingleLaneExecuteForMemoryFollowThrough(
                bool isMemoryOp,
                bool isLoad,
                bool writesRegister,
                ushort destRegId,
                ulong resultValue = 0,
                ulong memoryAddress = 0,
                ulong memoryData = 0,
                byte memoryAccessSize = 8,
                ulong pc = 0x1000,
                uint opCode = 0)
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeEX.Valid = true;
                pipeEX.PC = pc;
                pipeEX.OpCode = opCode;
                pipeEX.ResultReady = true;
                pipeEX.IsMemoryOp = isMemoryOp;
                pipeEX.IsLoad = isLoad;
                pipeEX.WritesRegister = writesRegister;
                pipeEX.DestRegID = destRegId;
                pipeEX.ResultValue = resultValue;
                pipeEX.MemoryAddress = memoryAddress;
                pipeEX.MemoryData = memoryData;
                pipeEX.ActiveLaneIndex = 0;

                ScalarExecuteLaneState lane = new();
                lane.Clear(0);
                lane.IsOccupied = true;
                lane.PC = pc;
                lane.OpCode = opCode;
                lane.ResultReady = true;
                lane.IsMemoryOp = isMemoryOp;
                lane.IsLoad = isLoad;
                lane.WritesRegister = writesRegister;
                lane.DestRegID = destRegId;
                lane.ResultValue = resultValue;
                lane.MemoryAddress = memoryAddress;
                lane.MemoryData = memoryData;
                lane.MemoryAccessSize = memoryAccessSize;
                pipeEX.SetLane(0, lane);
            }

            internal void TestRunMemoryAndWriteBackStagesFromCurrentExecuteState()
            {
                PipelineStage_Memory();
                PipelineStage_WriteBack();
            }

            internal void TestLatchMemoryToWriteBackTransferState()
            {
                LatchMemoryToWriteBackTransferState();
            }

            internal bool TestApplyWriteBackStageDomainSquash()
            {
                return TryApplyWriteBackStageDomainSquash();
            }

            internal void TestHandleEmptyWriteBackRetireWindow(
                bool hasRetireWindowExceptionDecision = false,
                PipelineStage retireWindowExceptionStage = PipelineStage.None,
                byte retireWindowExceptionLaneIndex = byte.MaxValue,
                bool retireWindowShouldSuppressYoungerWork = false)
            {
                HandleEmptyWriteBackRetireWindow(
                    hasRetireWindowExceptionDecision,
                    retireWindowExceptionStage,
                    retireWindowExceptionLaneIndex,
                    retireWindowShouldSuppressYoungerWork);
            }

            internal (bool Valid, bool ResultReady, bool VectorComplete) TestReadExecuteStageStatus()
            {
                return (
                    pipeEX.Valid,
                    pipeEX.ResultReady,
                    pipeEX.VectorComplete);
            }

            /// <summary>
            /// TEST-ONLY: Publish an already prepared transport-fact snapshot without forcing
            /// a descriptor-only rebuild path.
            /// </summary>
            internal void TestSetDecodedBundleTransportFacts(
                in YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts transportFacts)
            {
                PublishCurrentResidualDecodedBundleSlotCarrierFromTransportFacts(
                    transportFacts);
            }

            /// <summary>
            /// TEST-ONLY: Reset the unified decoded-bundle runtime state to the empty/reset contour.
            /// </summary>
            internal void TestResetDecodedBundleRuntimeState(ulong pc = 0)
            {
                ResetCurrentDecodedBundleSlotCarrierState(pc);
            }

            /// <summary>
            /// TEST-ONLY: Trigger the production foreground slot-mutation seam.
            /// </summary>
            internal void TestPublishCurrentForegroundSlotMutation(
                byte slotIndex,
                YAKSys_Hybrid_CPU.Core.MicroOp microOp)
            {
                PublishCurrentForegroundSlotMutation(
                    slotIndex,
                    microOp);
            }

            /// <summary>
            /// TEST-ONLY: Trigger the production foreground progress-consume seam for a single slot.
            /// </summary>
            internal void TestConsumeForegroundBundleSlot(byte slotIndex)
            {
                ConsumeForegroundBundleSlotIfNonEmpty(slotIndex);
            }

            /// <summary>
            /// TEST-ONLY: Trigger the production foreground progress-consume seam for an issue packet.
            /// </summary>
            internal void TestConsumeForegroundBundleIssuePacketSlots(
                in YAKSys_Hybrid_CPU.Core.BundleIssuePacket issuePacket,
                byte executableNonScalarPhysicalLaneMask)
            {
                ConsumeForegroundBundleIssuePacketSlots(
                    issuePacket,
                    executableNonScalarPhysicalLaneMask);
            }

            /// <summary>
            /// TEST-ONLY: Publish decode-originated transport facts using the same canonical decode
            /// plus legality-backed path as the production fetched-bundle success flow.
            /// </summary>
            internal void TestSetCanonicalDecodedBundleTransportFacts(
                VLIW_Instruction[] rawSlots,
                VliwBundleAnnotations? annotations = null,
                ulong pc = 0x1000,
                ulong bundleSerial = 0)
            {
                ArgumentNullException.ThrowIfNull(rawSlots);

                var decoder = new YAKSys_Hybrid_CPU.Core.Decoder.VliwDecoderV4();
                var canonicalState =
                    BuildCanonicalDecodedBundleState(
                        rawSlots,
                        decoder,
                        annotations,
                        pc,
                        bundleSerial);

                PublishCurrentCanonicalDecodedBundleState(canonicalState);
            }

            /// <summary>
            /// TEST-ONLY: Drive the production fetched-bundle decode seam, including the
            /// bundle-level fallback path when canonical decode throws.
            /// </summary>
            internal void TestDecodeFetchedBundle(
                VLIW_Instruction[] rawSlots,
                ulong pc = 0x1000,
                VliwBundleAnnotations? annotations = null)
            {
                ArgumentNullException.ThrowIfNull(rawSlots);

                var bundle = new VLIW_Bundle();
                for (int slotIndex = 0; slotIndex < rawSlots.Length && slotIndex < Core.BundleMetadata.BundleSlotCount; slotIndex++)
                {
                    bundle.SetInstruction(slotIndex, rawSlots[slotIndex]);
                }

                pipeIF.PC = pc;
                pipeIF.VLIWBundle = new byte[256];
                pipeIF.BundleAnnotations = annotations ?? BuildFetchedBundleAnnotationsForTesting(rawSlots);
                pipeIF.HasBundleAnnotations = true;
                if (!bundle.TryWriteBytes(pipeIF.VLIWBundle))
                {
                    throw new InvalidOperationException(
                        "Failed to serialize fetched bundle bytes for the decode test seam.");
                }

                DecodeFullBundle();
            }

            /// <summary>
            /// TEST-ONLY: Drive the production fetched-bundle decode seam from serialized
            /// raw bytes without pre-validating the bundle through production ingress helpers.
            /// This is used for strict-ingress regressions that must still exercise the
            /// bundle-level fallback trap contour.
            /// </summary>
            internal void TestDecodeFetchedBundleBytes(
                byte[] rawBundleBytes,
                ulong pc = 0x1000,
                VliwBundleAnnotations? annotations = null)
            {
                ArgumentNullException.ThrowIfNull(rawBundleBytes);

                pipeIF.PC = pc;
                pipeIF.VLIWBundle = (byte[])rawBundleBytes.Clone();
                pipeIF.BundleAnnotations = annotations ?? VliwBundleAnnotations.Empty;
                pipeIF.HasBundleAnnotations = true;

                DecodeFullBundle();
            }

            private VliwBundleAnnotations BuildFetchedBundleAnnotationsForTesting(
                VLIW_Instruction[] rawSlots)
            {
                ArgumentNullException.ThrowIfNull(rawSlots);

                int defaultOwnerVirtualThreadId = ReadActiveVirtualThreadId();
                var slotMetadata = new InstructionSlotMetadata[Core.BundleMetadata.BundleSlotCount];
                bool hasExplicitOwner = false;

                for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
                {
                    byte explicitOwnerVirtualThreadId =
                        slotIndex < rawSlots.Length
                            ? rawSlots[slotIndex].VirtualThreadId
                            : (byte)0;
                    int slotOwnerVirtualThreadId =
                        explicitOwnerVirtualThreadId != 0
                            ? explicitOwnerVirtualThreadId
                            : defaultOwnerVirtualThreadId;

                    slotMetadata[slotIndex] = new InstructionSlotMetadata(
                        YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(slotOwnerVirtualThreadId),
                        SlotMetadata.Default);
                    hasExplicitOwner |= slotOwnerVirtualThreadId != 0;
                }

                return hasExplicitOwner
                    ? new VliwBundleAnnotations(slotMetadata)
                    : VliwBundleAnnotations.Empty;
            }

            /// <summary>
            /// TEST-ONLY: Read the currently published transport facts through the production seam.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts TestReadCurrentDecodedBundleTransportFacts()
            {
                ReadCurrentDecodedBundleTransportFacts(
                    out YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts transportFacts);
                return transportFacts;
            }

            /// <summary>
            /// TEST-ONLY: Read the currently published unified decoded-bundle runtime state.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.DecodedBundleRuntimeState TestReadCurrentDecodedBundleRuntimeState()
            {
                ReadCurrentDecodedBundleRuntimeState(
                    out YAKSys_Hybrid_CPU.Core.DecodedBundleRuntimeState runtimeState);
                return runtimeState;
            }

            /// <summary>
            /// TEST-ONLY: Read the currently visible foreground execution contour after selecting
            /// the active execution plan (derived issue plan when present, otherwise base transport)
            /// and then applying decoded-bundle progress masking.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts TestReadCurrentForegroundDecodedBundleTransportFacts()
            {
                ReadCurrentForegroundDecodedBundleTransportFacts(
                    out YAKSys_Hybrid_CPU.Core.DecodedBundleTransportFacts transportFacts);
                return transportFacts;
            }

            /// <summary>
            /// TEST-ONLY: Read the currently visible foreground runtime state after execution-plan
            /// selection and progress projection.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.DecodedBundleRuntimeState TestReadCurrentForegroundDecodedBundleRuntimeState()
            {
                ReadCurrentForegroundDecodedBundleRuntimeState(
                    out YAKSys_Hybrid_CPU.Core.DecodedBundleRuntimeState runtimeState);
                return runtimeState;
            }

            /// <summary>
            /// TEST-ONLY: Read the explicit decoded-bundle progress state.
            /// </summary>
            internal YAKSys_Hybrid_CPU.Core.BundleProgressState TestReadCurrentDecodedBundleProgressState()
            {
                ReadCurrentDecodedBundleProgressState(
                    out YAKSys_Hybrid_CPU.Core.BundleProgressState progressState);
                return progressState;
            }

            internal (bool IsSilentSpeculativeSquash, bool IsPreciseArchitecturalFault, int VirtualThreadId, ulong FaultingPC, ulong OperationDomainTag, ulong ActiveCert)
                TestResolveDecodeExceptionOrderingDecision(
                    in YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor slotDescriptor,
                    ulong faultingPc)
            {
                ScalarExceptionOrderingDecision decision =
                    ResolveDecodeExceptionOrderingDecision(in slotDescriptor, faultingPc);
                return (
                    decision.IsSilentSpeculativeSquash,
                    decision.IsPreciseArchitecturalFault,
                    decision.VirtualThreadId,
                    decision.FaultingPC,
                    decision.OperationDomainTag,
                    decision.ActiveCert);
            }

            internal (uint OpCode, bool IsMemoryOp, bool IsControlFlow, bool WritesRegister, int VirtualThreadId, int MemoryBankIntent, IReadOnlyList<int> ReadRegisters, IReadOnlyList<int> WriteRegisters)
                TestReadDecodedSlotRuntimeIssueFacts(
                    in YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor slotDescriptor)
            {
                return (
                    slotDescriptor.GetRuntimeExecutionOpCode(),
                    slotDescriptor.GetRuntimeAdmissionIsMemoryOp(),
                    slotDescriptor.GetRuntimeAdmissionIsControlFlow(),
                    slotDescriptor.GetRuntimeAdmissionWritesRegister(),
                    slotDescriptor.GetRuntimeExecutionVirtualThreadId(),
                    slotDescriptor.GetRuntimeExecutionMemoryBankIntent(),
                    slotDescriptor.GetRuntimeAdmissionReadRegisters(),
                    slotDescriptor.GetRuntimeAdmissionWriteRegisters());
            }

            internal bool TestShouldSkipDecodedSlotForForegroundIssue(
                in YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor slotDescriptor)
            {
                return ShouldSkipDecodedSlotForForegroundIssue(in slotDescriptor);
            }

            internal int TestCountInjectableGaps(
                System.Collections.Generic.IReadOnlyList<YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor> slotDescriptors)
            {
                return CountInjectableGaps(slotDescriptors);
            }

            internal bool TestHasMemoryClusteringEvent(
                System.Collections.Generic.IReadOnlyList<YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor> slotDescriptors)
            {
                return HasMemoryClusteringEvent(slotDescriptors);
            }

            internal ulong TestResolveCurrentLoopBufferMaxIterations()
            {
                ReadCurrentDecodedBundleTransportFacts(
                    out _,
                    out YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor[] slotDescriptors,
                    out _,
                    out _);

                return ResolveCurrentLoopBufferMaxIterations(slotDescriptors);
            }

            internal (int[] ScalarWriteRegisters, byte CoexistenceMask, int ConflictCount)
                TestResolveIssuePacketCoexistenceFromRuntimeAdmission(
                    System.Collections.Generic.IReadOnlyList<YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor> slotDescriptors,
                    byte scalarIssueMask,
                    byte selectedNonScalarSlotMask)
            {
                ArgumentNullException.ThrowIfNull(slotDescriptors);

                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane0 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(0);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane1 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(1);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane2 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(2);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane3 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(3);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane4 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(4);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane5 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(5);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane6 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(6);
                YAKSys_Hybrid_CPU.Core.IssuePacketLane lane7 = YAKSys_Hybrid_CPU.Core.IssuePacketLane.CreateEmpty(7);

                byte scalarLaneIndex = 0;
                for (byte slotIndex = 0; slotIndex < 8 && scalarLaneIndex < 4; slotIndex++)
                {
                    byte slotBit = (byte)(1 << slotIndex);
                    if ((scalarIssueMask & slotBit) == 0 || slotIndex >= slotDescriptors.Count)
                        continue;

                    YAKSys_Hybrid_CPU.Core.DecodedBundleSlotDescriptor slot = slotDescriptors[slotIndex];
                    if (!slot.IsValid)
                        continue;

                    YAKSys_Hybrid_CPU.Core.IssuePacketLane lane =
                        YAKSys_Hybrid_CPU.Core.IssuePacketLane.Create(
                            scalarLaneIndex,
                            slot,
                            countsTowardScalarProjection: true);

                    switch (scalarLaneIndex)
                    {
                        case 0:
                            lane0 = lane;
                            break;
                        case 1:
                            lane1 = lane;
                            break;
                        case 2:
                            lane2 = lane;
                            break;
                        default:
                            lane3 = lane;
                            break;
                    }

                    scalarLaneIndex++;
                }

                byte selectedSlotMask = (byte)(scalarIssueMask | selectedNonScalarSlotMask);
                var issuePacket = new YAKSys_Hybrid_CPU.Core.BundleIssuePacket(
                    pc: 0,
                    decodeMode: YAKSys_Hybrid_CPU.Core.DecodeMode.ClusterPreparedMode,
                    validNonEmptyMask: selectedSlotMask,
                    scalarCandidateMask: scalarIssueMask,
                    scalarIssueMask: scalarIssueMask,
                    selectedSlotMask: selectedSlotMask,
                    unmappedSelectedSlotMask: 0,
                    preparedScalarMask: scalarIssueMask,
                    refinedPreparedScalarMask: scalarIssueMask,
                    advisoryScalarIssueWidth: CountSelectedSlots(scalarIssueMask),
                    refinedAdvisoryScalarIssueWidth: CountSelectedSlots(scalarIssueMask),
                    executionMode: YAKSys_Hybrid_CPU.Core.RuntimeClusterAdmissionExecutionMode.ClusterPrepared,
                    shouldProbeClusterPath: false,
                    usesIssuePacketAsExecutionSource: true,
                    retainsReferenceSequentialPath: false,
                    lane0,
                    lane1,
                    lane2,
                    lane3,
                    lane4,
                    lane5,
                    lane6,
                    lane7,
                    YAKSys_Hybrid_CPU.Core.BundleIssueFallbackInfo.CreateEmpty());

                System.Collections.Generic.HashSet<int> scalarWriteSet =
                    ResolveScalarLaneWriteRegisterSet(issuePacket, slotDescriptors);
                int[] scalarWriteRegisters = new int[scalarWriteSet.Count];
                scalarWriteSet.CopyTo(scalarWriteRegisters);
                Array.Sort(scalarWriteRegisters);

                int conflictCount = 0;
                byte coexistenceMask = ResolveSelectedNonScalarCoexistenceMask(
                    issuePacket,
                    slotDescriptors,
                    ref conflictCount);

                return (scalarWriteRegisters, coexistenceMask, conflictCount);
            }

            internal (byte ExecutableNonScalarPhysicalLaneMask, byte ExecutableNonScalarSlotMask)
                TestResolveExecutableIssuePacketMasks(
                    in YAKSys_Hybrid_CPU.Core.BundleIssuePacket issuePacket,
                    YAKSys_Hybrid_CPU.Core.DecodedBundleDependencySummary? dependencySummary)
            {
                byte executableNonScalarPhysicalLaneMask =
                    ResolveExecutableNonScalarPhysicalLaneMask(
                        issuePacket,
                        dependencySummary);
                byte executableNonScalarSlotMask =
                    ResolveExecutableNonScalarSlotMask(
                        issuePacket,
                        executableNonScalarPhysicalLaneMask);

                return (executableNonScalarPhysicalLaneMask, executableNonScalarSlotMask);
            }

            private static int CountSelectedSlots(byte slotMask)
            {
                int count = 0;
                while (slotMask != 0)
                {
                    count += slotMask & 1;
                    slotMask >>= 1;
                }

                return count;
            }

            /// <summary>
            /// TEST-ONLY: Drive the production hazard checker through the explicit execute-lane
            /// memory-stall contour where a widened packet is still waiting on at least one lane.
            /// </summary>
            internal bool TestCheckPipelineHazardForExplicitExecuteLaneNotReady()
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                ScalarExecuteLaneState lane = new();
                lane.Clear(0);
                lane.IsOccupied = true;
                lane.ResultReady = false;
                pipeEX.SetLane(0, lane);
                pipeEX.Valid = true;
                pipeEX.UsesExplicitPacketLanes = true;
                pipeEX.MaterializedPhysicalLaneCount = 1;

                PipelineCycleStallDecision stallDecision = ResolvePipelineHazardStallDecision();
                if (stallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(stallDecision);
                }

                return stallDecision.ShouldStall;
            }

            /// <summary>
            /// TEST-ONLY: Stage the core so a real <see cref="ExecutePipelineCycle"/> hits the
            /// central interlock memory-wait contour before fetch/decode execute.
            /// </summary>
            internal void TestStageFullCycleForExplicitExecuteLaneNotReadyHazard(ulong pc = 0x6400, int activeVtId = 0)
            {
                PrepareExecutionStart(pc, activeVtId);
                ActiveVirtualThreadId = activeVtId;
                WriteVirtualThreadPipelineState(activeVtId, PipelineState.Task);

                pipeIF.Clear();
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                ScalarExecuteLaneState lane = new();
                lane.Clear(0);
                lane.PC = pc;
                lane.IsOccupied = true;
                lane.ResultReady = false;
                pipeEX.SetLane(0, lane);
                pipeEX.Valid = true;
                pipeEX.UsesExplicitPacketLanes = true;
                pipeEX.MaterializedPhysicalLaneCount = 1;
            }

            /// <summary>
            /// TEST-ONLY: Drive the production hazard checker through the explicit memory-lane
            /// structural-stall contour where MEM is still holding a widened packet.
            /// </summary>
            internal bool TestCheckPipelineHazardForExplicitMemoryLaneNotReady()
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                ScalarMemoryLaneState lane = new();
                lane.Clear(0);
                lane.IsOccupied = true;
                lane.ResultReady = false;
                pipeMEM.SetLane(0, lane);
                pipeMEM.Valid = true;
                pipeMEM.UsesExplicitPacketLanes = true;
                pipeMEM.MaterializedPhysicalLaneCount = 1;

                PipelineCycleStallDecision stallDecision = ResolvePipelineHazardStallDecision();
                if (stallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(stallDecision);
                }

                return stallDecision.ShouldStall;
            }

            /// <summary>
            /// TEST-ONLY: Drive the production hazard checker through the multi-cycle vector contour.
            /// </summary>
            internal bool TestCheckPipelineHazardForIncompleteVectorExecute()
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeEX.Valid = true;
                pipeEX.IsVectorOp = true;
                pipeEX.VectorComplete = false;
                pipeEX.MaterializedPhysicalLaneCount = 1;

                PipelineCycleStallDecision stallDecision = ResolvePipelineHazardStallDecision();
                if (stallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(stallDecision);
                }

                return stallDecision.ShouldStall;
            }

            /// <summary>
            /// TEST-ONLY: Drive the production hazard checker through an invalid EX-stage
            /// occupancy state where the stage is valid but materialized-lane count is zero.
            /// </summary>
            internal bool TestCheckPipelineHazardForInvalidExecuteOccupancyState()
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeEX.Valid = true;
                pipeEX.UsesExplicitPacketLanes = true;
                pipeEX.MaterializedPhysicalLaneCount = 0;

                PipelineCycleStallDecision stallDecision = ResolvePipelineHazardStallDecision();
                if (stallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(stallDecision);
                }

                return stallDecision.ShouldStall;
            }

            /// <summary>
            /// TEST-ONLY: Drive the production hazard checker through an invalid MEM-stage
            /// occupancy state where the stage is valid but materialized-lane count is zero.
            /// </summary>
            internal bool TestCheckPipelineHazardForInvalidMemoryOccupancyState()
            {
                pipeID.Clear();
                pipeEX.Clear();
                pipeMEM.Clear();
                pipeWB.Clear();

                pipeMEM.Valid = true;
                pipeMEM.UsesExplicitPacketLanes = true;
                pipeMEM.MaterializedPhysicalLaneCount = 0;

                PipelineCycleStallDecision stallDecision = ResolvePipelineHazardStallDecision();
                if (stallDecision.ShouldStall)
                {
                    ApplyPipelineCycleStallDecision(stallDecision);
                }

                return stallDecision.ShouldStall;
            }
            #endregion
        }
    }
}
#endif
