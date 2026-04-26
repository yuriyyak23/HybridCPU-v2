using System;
using System.Linq;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE
{
    public struct StackFlagsSnapshot
    {
        public int CallStackDepth;
        public ulong CallStackTop;
        public int InterruptStackDepth;
        public ulong InterruptStackTop;

        public bool AluZero;
        public bool AluSign;
        public bool AluOverflow;
        public bool AluParity;

        public bool BaseEnableInterrupt;
        public bool BaseIoPrivilege;
        public bool BaseOsKernel;

        public bool CommonDirection;
        public bool CommonJump;
        public bool CommonJumpOutsideVliw;

        public byte EilpOffsetVliwOpcode;
    }

    /// <summary>
    /// Thread-safe read-only observation service for GUI and bridge state reads.
    /// All methods return snapshots (immutable copies) to ensure thread safety
    /// between ISE emulation thread and GUI update thread.
    ///
    /// Usage from GUI:
    /// - Never access Processor.CPU_Cores directly from GUI thread
    /// - Always use observation-service methods
    /// - All returned data is copied, safe to use without locks
    /// </summary>
    public sealed partial class IseObservationService
    {
        private const string CompilerTelemetryUnavailableReason =
            "No live multithreaded compiler telemetry is published on the current runtime contour; only stored canonical compile artifacts remain available to specialized views.";

        #region 4-Way SMT State Access

        /// <summary>
        /// Get live program counters for all 4 virtual threads in a core.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Array of 4 live PCs (one per VT)</returns>
        public ulong[] GetVirtualThreadLivePcs(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                ReadCoreObservationSnapshot(core, out Processor.CPU_Core.PipelineObservationSnapshot observation);
                return BuildVirtualThreadLivePcs(core, observation.ActiveVirtualThreadId, observation.ActiveLivePc);
            }
        }

        /// <summary>
        /// Get stall flags for all 4 virtual threads.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Array of 4 stall flags (true = stalled)</returns>
        public bool[] GetVirtualThreadStalled(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                ReadCoreObservationSnapshot(core, out Processor.CPU_Core.PipelineObservationSnapshot observation);
                Processor.CPU_Core.PipelineControl pipeState = observation.PipelineControl;
                return BuildVirtualThreadStalledSnapshot(core, observation.ActiveVirtualThreadId, in pipeState);
            }
        }

        /// <summary>
        /// Get stall reasons for all 4 virtual threads.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Array of 4 stall reason strings</returns>
        public string[] GetVirtualThreadStallReasons(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                ReadCoreObservationSnapshot(core, out Processor.CPU_Core.PipelineObservationSnapshot observation);
                Processor.CPU_Core.PipelineControl pipeState = observation.PipelineControl;
                bool[] stalled = BuildVirtualThreadStalledSnapshot(core, observation.ActiveVirtualThreadId, in pipeState);
                return BuildVirtualThreadStallReasons(core, stalled, in observation);
            }
        }

        /// <summary>
        /// Get exception contexts for all 4 virtual threads.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Array of 4 FP exception contexts</returns>
        public Processor.CPU_Core.FPExceptionContext[] GetVirtualThreadExceptions(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                var exceptions = new Processor.CPU_Core.FPExceptionContext[4];

                if (core.ThreadFPContexts == null || core.ThreadFPContexts.Length < 4)
                {
                    for (int i = 0; i < 4; i++)
                        exceptions[i] = new Processor.CPU_Core.FPExceptionContext();
                    return exceptions;
                }

                for (int vt = 0; vt < 4; vt++)
                {
                    // Copy the struct (value type, so automatic deep copy)
                    exceptions[vt] = core.ThreadFPContexts[vt];
                }

                return exceptions;
            }
        }

        /// <summary>
        /// Get register values for a specific virtual thread.
        /// Values are read through the unified architectural-state helpers.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <param name="vtId">Virtual Thread ID (0-3)</param>
        /// <returns>Array of 32 register values</returns>
        public ulong[] GetVirtualThreadRegisters(int coreId, int vtId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                ValidateVtId(vtId);

                var core = GetCore(coreId);
                return BuildVirtualThreadRegisters(core, vtId);
            }
        }

        /// <summary>
        /// Get the active live instruction pointer for a core.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Active live PC</returns>
        public ulong GetLiveInstructionPointer(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                return GetCore(coreId).GetPipelineObservationSnapshot().ActiveLivePc;
            }
        }

        /// <summary>
        /// Get the currently active virtual thread ID for a core.
        /// </summary>
        /// <param name="coreId">Core ID (0-1023)</param>
        /// <returns>Active VT ID (0-3)</returns>
        public int GetActiveVirtualThreadId(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                return GetCore(coreId).GetPipelineObservationSnapshot().ActiveVirtualThreadId;
            }
        }

        #endregion

        #region Core State Access

        public StackFlagsSnapshot GetStackFlagsSnapshot(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                var flags = core.CoreFlagsRegister;

                return new StackFlagsSnapshot
                {
                    CallStackDepth = core.Call_Callback_Addresses?.Count ?? 0,
                    CallStackTop = core.Call_Callback_Addresses?.Count > 0 ? core.Call_Callback_Addresses[core.Call_Callback_Addresses.Count - 1] : 0,
                    InterruptStackDepth = core.Interrupt_Callback_Addresses?.Count ?? 0,
                    InterruptStackTop = core.Interrupt_Callback_Addresses?.Count > 0 ? core.Interrupt_Callback_Addresses[core.Interrupt_Callback_Addresses.Count - 1] : 0,

                    AluZero = flags.Zero_Flag,
                    AluSign = flags.Sign_Flag,
                    AluOverflow = flags.OverFlow_Flag,
                    AluParity = flags.Parity_Flag,

                    BaseEnableInterrupt = flags.EnableInterrupt_Flag,
                    BaseIoPrivilege = flags.IOPrivilege_Flag,
                    BaseOsKernel = flags.OSKernel_Flag,

                    CommonDirection = flags.Direction_Flag,
                    CommonJump = flags.Jump_Flag,
                    CommonJumpOutsideVliw = flags.JumpOutsideVLIW_Flag,

                    EilpOffsetVliwOpcode = flags.Offset_VLIWOpCode
                };
            }
        }

        /// <summary>
        /// Get basic core state snapshot.
        /// </summary>
        public CoreStateSnapshot GetCoreState(int coreId)
        {
            lock (_syncLock)
            {
                return BuildCoreStateSnapshot(_machineStateSource, coreId);
            }
        }

        /// <summary>
        /// Get total number of cores in the processor.
        /// </summary>
        public int GetTotalCores()
        {
            return GetCoreCount();
        }

        #endregion

        #region Memory State Access

        /// <summary>
        /// Read memory bytes (thread-safe).
        /// </summary>
        /// <param name="address">Start address</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Copy of memory bytes</returns>
        public byte[] ReadMemory(ulong address, int length)
        {
            lock (_syncLock)
            {
                return ReadMachineMemory(address, length);
            }
        }

        #endregion

        #region Private Helpers

        private void ValidateCoreId(int coreId)
        {
            ValidateCoreId(_machineStateSource, coreId);
        }

        private static void ValidateCoreId(IIseMachineStateSource machineStateSource, int coreId)
        {
            int coreCount = GetCoreCount(machineStateSource);
            if (coreId < 0 || coreId >= coreCount)
            {
                throw new ArgumentOutOfRangeException(nameof(coreId),
                    $"Core ID must be between 0 and {coreCount - 1}");
            }
        }

        private static void ValidateVtId(int vtId)
        {
            if (vtId < 0 || vtId >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(vtId),
                    "Virtual Thread ID must be between 0 and 3");
            }
        }

        private static void ReadCoreObservationSnapshot(
            Processor.CPU_Core core,
            out Processor.CPU_Core.PipelineObservationSnapshot observation)
        {
            observation = core.GetPipelineObservationSnapshot();
        }

        private static string DetermineStallReason(
            Processor.CPU_Core core,
            int vtId,
            in Processor.CPU_Core.PipelineObservationSnapshot observation)
        {
            if (vtId == observation.ActiveVirtualThreadId && observation.PipelineControl.Stalled)
            {
                return Processor.CPU_Core.PipelineStallText.Render(
                    observation.PipelineControl.StallReason,
                    Processor.CPU_Core.PipelineStallTextStyle.Snapshot);
            }

            if (IsVirtualThreadMemoryStalled(core, vtId))
            {
                return "Memory Wait";
            }

            switch (observation.CurrentVirtualThreadPipelineState)
            {
                case PipelineState.WaitForClusterSync:
                    return "Pod Barrier Wait";
                case PipelineState.PtwStall:
                    return "PTW Walk";
                case PipelineState.ClockGatedDonor:
                    return "Clock Gated";
                case PipelineState.WaitForEvent:
                    return "Wait For Event";
                case PipelineState.Halted:
                    return "Halted";
            }

            // Default for unknown stall
            return "Unknown Stall";
        }

        private static ulong[] BuildVirtualThreadLivePcs(
            Processor.CPU_Core core,
            int activeVtId,
            ulong activeLivePc)
        {
            ulong[] pcs = new ulong[Processor.CPU_Core.SmtWays];

            for (int vt = 0; vt < pcs.Length; vt++)
                pcs[vt] = vt == activeVtId
                    ? activeLivePc
                    : core.ReadCommittedPc(vt);

            return pcs;
        }

        private static ulong[] BuildVirtualThreadCommittedPcs(Processor.CPU_Core core)
        {
            ulong[] pcs = new ulong[Processor.CPU_Core.SmtWays];

            for (int vt = 0; vt < pcs.Length; vt++)
                pcs[vt] = core.ReadCommittedPc(vt);

            return pcs;
        }

        private static bool[] BuildVirtualThreadStalledSnapshot(
            Processor.CPU_Core core,
            int activeVtId,
            in Processor.CPU_Core.PipelineControl pipeState)
        {
            bool[] stalled = new bool[Processor.CPU_Core.SmtWays];
            if (core.VirtualThreadStalled != null)
            {
                int copyLength = Math.Min(stalled.Length, core.VirtualThreadStalled.Length);
                Array.Copy(core.VirtualThreadStalled, stalled, copyLength);
            }

            if (pipeState.Enabled && pipeState.Stalled)
            {
                if ((uint)activeVtId < (uint)stalled.Length)
                {
                    stalled[activeVtId] = true;
                }
            }

            return stalled;
        }

        private static string[] BuildVirtualThreadStallReasons(
            Processor.CPU_Core core,
            bool[] stalled,
            in Processor.CPU_Core.PipelineObservationSnapshot observation)
        {
            string[] reasons = new string[Processor.CPU_Core.SmtWays];

            for (int vt = 0; vt < reasons.Length; vt++)
            {
                if (vt >= stalled.Length)
                {
                    reasons[vt] = "Unknown";
                    continue;
                }

                reasons[vt] = stalled[vt]
                    ? DetermineStallReason(core, vt, in observation)
                    : "None";
            }

            return reasons;
        }

        private static ulong[] BuildVirtualThreadRegisters(Processor.CPU_Core core, int vtId)
        {
            ulong[] registers = new ulong[32];

            for (int i = 0; i < registers.Length; i++)
                registers[i] = core.ReadArch(vtId, i);

            return registers;
        }

        private static bool IsVirtualThreadMemoryStalled(Processor.CPU_Core core, int vtId)
        {
            if (core.VirtualThreadStalled == null)
            {
                return false;
            }

            return (uint)vtId < (uint)core.VirtualThreadStalled.Length &&
                   core.VirtualThreadStalled[vtId];
        }

        private static CoreTimebaseSnapshot BuildCoreTimebaseSnapshot(
            in Processor.CPU_Core.PipelineControl pipeState)
        {
            bool isAvailable =
                pipeState.Enabled ||
                pipeState.CycleCount != 0 ||
                pipeState.InstructionsRetired != 0 ||
                pipeState.StallCycles != 0 ||
                pipeState.Stalled;

            return isAvailable
                ? new CoreTimebaseSnapshot(
                    cycleCount: pipeState.CycleCount,
                    isStalled: pipeState.Stalled,
                    isAvailable: true,
                    unavailableReason: string.Empty)
                : new CoreTimebaseSnapshot(
                    cycleCount: 0,
                    isStalled: false,
                    isAvailable: false,
                    unavailableReason: "Pipeline timebase has not published any live timing facts yet.");
        }

        #endregion

        #region Pod and NoC Access

        /// <summary>
        /// Get Pod ID for a given core (Pod coordinates encoded as (X << 8) | Y).
        /// </summary>
        public ushort GetCorePodId(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                int podIndex = coreId / 16; // 16 cores per Pod
                if ((uint)podIndex < (uint)GetPodCount())
                {
                    PodController? pod = GetPodOrNull(podIndex);
                    if (pod != null)
                    {
                        return pod.PodId;
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Get Pod coordinates (X, Y) for a given core.
        /// </summary>
        public (int X, int Y) GetCorePodCoordinates(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                int podIndex = coreId / 16;
                if ((uint)podIndex < (uint)GetPodCount())
                {
                    PodController? pod = GetPodOrNull(podIndex);
                    if (pod != null)
                    {
                        return (pod.PodX, pod.PodY);
                    }
                }
                return (0, 0);
            }
        }

        /// <summary>
        /// Get Pod barrier synchronization state for a core's Pod.
        /// Returns true if the core is waiting at a Pod barrier.
        /// </summary>
        public bool GetPodBarrierState(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);
                return core.HasAnyVirtualThreadPipelineState(PipelineState.WaitForClusterSync);
            }
        }

        /// <summary>
        /// Get Pod controller snapshot for a given Pod index.
        /// </summary>
        public PodSnapshot GetPodSnapshot(int podIndex)
        {
            lock (_syncLock)
            {
                return BuildPodSnapshot(_machineStateSource, podIndex);
            }
        }

        #endregion

        #region GRLB (Global Resource Lock Bitmap) Access

        /// <summary>
        /// Get GRLB snapshot for a core.
        /// Returns the current state of all 128 resource lock bits.
        /// </summary>
        public GrlbSnapshot GetGrlbSnapshot(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                var core = GetCore(coreId);

                // Get GRLB banks (4 × 32-bit banks = 128 bits total)
                var banks = core.GetGrlbBanks();

                return new GrlbSnapshot
                {
                    CoreId = coreId,
                    Bank0 = banks.Length > 0 ? banks[0] : 0,
                    Bank1 = banks.Length > 1 ? banks[1] : 0,
                    Bank2 = banks.Length > 2 ? banks[2] : 0,
                    Bank3 = banks.Length > 3 ? banks[3] : 0,
                    StructuralStalls = core.StructuralStalls
                };
            }
        }

        /// <summary>
        /// Check if a specific resource bit is locked in GRLB.
        /// </summary>
        /// <param name="coreId">Core ID</param>
        /// <param name="resourceBit">Resource bit index (0-127)</param>
        /// <returns>True if resource is locked</returns>
        public bool IsGrlbResourceLocked(int coreId, int resourceBit)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                if (resourceBit < 0 || resourceBit >= 128)
                {
                    throw new ArgumentOutOfRangeException(nameof(resourceBit),
                        "Resource bit must be between 0 and 127");
                }

                var core = GetCore(coreId);
                var banks = core.GetGrlbBanks();

                int bankIndex = resourceBit / 32;
                int bitInBank = resourceBit % 32;

                if (bankIndex < banks.Length)
                {
                    return (banks[bankIndex] & (1u << bitInBank)) != 0;
                }

                return false;
            }
        }

        #endregion

        #region FSP Power Controller Access

        /// <summary>
        /// Get FSP power controller snapshot for a core's Pod.
        /// </summary>
        public FspPowerSnapshot GetFspPowerSnapshot(int coreId)
        {
            lock (_syncLock)
            {
                ValidateCoreId(coreId);
                int podIndex = coreId / 16;
                bool isClockGated =
                    GetCore(coreId).HasAnyVirtualThreadPipelineState(PipelineState.ClockGatedDonor);

                if ((uint)podIndex < (uint)GetPodCount())
                {
                    PodController? pod = GetPodOrNull(podIndex);
                    if (pod == null)
                    {
                        return new FspPowerSnapshot
                        {
                            CoreId = coreId,
                            PodIndex = podIndex,
                            CurrentGatedCount = 0,
                            TotalGatedCycles = 0,
                            GateTransitions = 0,
                            IsClockGated = isClockGated
                        };
                    }

                    var powerController = pod.PowerController;

                    return new FspPowerSnapshot
                    {
                        CoreId = coreId,
                        PodIndex = podIndex,
                        CurrentGatedCount = powerController.CurrentGatedCount,
                        TotalGatedCycles = powerController.TotalGatedCycles,
                        GateTransitions = powerController.GateTransitions,
                        IsClockGated = isClockGated
                    };
                }

                return new FspPowerSnapshot
                {
                    CoreId = coreId,
                    PodIndex = podIndex,
                    CurrentGatedCount = 0,
                    TotalGatedCycles = 0,
                    GateTransitions = 0,
                    IsClockGated = isClockGated
                };
            }
        }

        #endregion

        #region Memory Subsystem Access

        /// <summary>
        /// Get memory subsystem snapshot with bank statistics.
        /// </summary>
        public MemorySubsystemSnapshot GetMemorySubsystemSnapshot()
        {
            lock (_syncLock)
            {
                MemorySubsystem? mem = GetMemorySubsystem();
                if (mem == null)
                {
                    return new MemorySubsystemSnapshot
                    {
                        NumBanks = 0,
                        TotalBursts = 0,
                        BankConflicts = 0,
                        StallCycles = 0,
                        TotalBytesTransferred = 0,
                        CurrentQueuedRequests = 0,
                        MaxQueueDepth = 0,
                        AverageBurstLength = 0.0,
                        AverageWaitCycles = 0.0,
                        BurstEfficiency = 0.0,
                        IsChannelOverloaded = false,
                        ArbitrationPolicy = "Unknown"
                    };
                }

                return new MemorySubsystemSnapshot
                {
                    NumBanks = mem.NumBanks,
                    TotalBursts = mem.TotalBursts,
                    BankConflicts = mem.BankConflicts,
                    StallCycles = mem.StallCycles,
                    TotalBytesTransferred = mem.TotalBytesTransferred,
                    CurrentQueuedRequests = mem.CurrentQueuedRequests,
                    MaxQueueDepth = mem.MaxQueueDepth,
                    AverageBurstLength = mem.AverageBurstLength,
                    AverageWaitCycles = mem.AverageWaitCycles,
                    BurstEfficiency = mem.BurstEfficiency,
                    IsChannelOverloaded = mem.IsChannelOverloaded,
                    ArbitrationPolicy = mem.ArbitrationPolicy.ToString()
                };
            }
        }

        /// <summary>
        /// Get memory configuration details.
        /// </summary>
        public MemoryConfigSnapshot GetMemoryConfig()
        {
            lock (_syncLock)
            {
                MemorySubsystem? mem = GetMemorySubsystem();
                if (mem == null)
                {
                    return new MemoryConfigSnapshot
                    {
                        NumBanks = 0,
                        BankWidthBytes = 0,
                        NumMemoryPorts = 0,
                        BankBandwidthGBps = 0.0,
                        DmaThresholdBytes = 0,
                        AxiBoundary = 0,
                        MaxBurstLength = 0,
                        PortSwitchingPenalty = 0,
                        RowBufferHitLatency = 0,
                        RowBufferMissLatency = 0,
                        RowBufferSize = 0
                    };
                }

                return new MemoryConfigSnapshot
                {
                    NumBanks = mem.NumBanks,
                    BankWidthBytes = mem.BankWidthBytes,
                    NumMemoryPorts = mem.NumMemoryPorts,
                    BankBandwidthGBps = mem.BankBandwidthGBps,
                    DmaThresholdBytes = mem.DmaThresholdBytes,
                    AxiBoundary = mem.AxiBoundary,
                    MaxBurstLength = mem.MaxBurstLength,
                    PortSwitchingPenalty = mem.PortSwitchingPenalty,
                    RowBufferHitLatency = mem.RowBufferHitLatency,
                    RowBufferMissLatency = mem.RowBufferMissLatency,
                    RowBufferSize = mem.RowBufferSize
                };
            }
        }

        /// <summary>
        /// Get total memory size in bytes.
        /// </summary>
        public long GetTotalMemorySize()
        {
            lock (_syncLock)
            {
                return GetTotalMachineMemorySize();
            }
        }

        #endregion

        #region Multithreaded Compiler State Access

        /// <summary>
        /// Get compilation telemetry for multithreaded compilation.
        /// Returns an explicit unavailable snapshot when the current runtime contour
        /// does not publish live compiler telemetry.
        /// </summary>
        public CompilerStateSnapshot GetCompilerStateSnapshot()
        {
            lock (_syncLock)
            {
                return new CompilerStateSnapshot
                {
                    IsAvailable = false,
                    AvailabilityReason = CompilerTelemetryUnavailableReason,
                    IsCompiling = false,
                    ThreadCount = 0,
                    TotalInstructionsCompiled = 0,
                    BarriersInserted = 0,
                    CrossThreadDependencies = 0,
                    BundleUtilization = 0.0
                };
            }
        }

        /// <summary>
        /// Get per-thread compiler context state for a specific virtual thread.
        /// Returns an explicit unavailable snapshot when no live compiler context is published.
        /// </summary>
        public ThreadCompilerStateSnapshot GetThreadCompilerState(int vtId)
        {
            lock (_syncLock)
            {
                ValidateVtId(vtId);

                return new ThreadCompilerStateSnapshot
                {
                    IsAvailable = false,
                    AvailabilityReason = CompilerTelemetryUnavailableReason,
                    VirtualThreadId = vtId,
                    InstructionCount = 0,
                    DomainTag = 0,
                    RegistersAllocated = 0,
                    HasPendingBarrier = false
                };
            }
        }

        /// <summary>
        /// Get dependency graph state showing cross-thread dependencies.
        /// Returns an explicit unavailable snapshot when no live compiler dependency view is published.
        /// </summary>
        public DependencyGraphSnapshot GetDependencyGraph()
        {
            lock (_syncLock)
            {
                var dependencies = new bool[4, 4];
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        dependencies[i, j] = false;
                    }
                }

                return new DependencyGraphSnapshot
                {
                    IsAvailable = false,
                    AvailabilityReason = CompilerTelemetryUnavailableReason,
                    DependencyMatrix = dependencies,
                    MemoryAccessCount = 0,
                    HasCyclicDependency = false,
                    TotalDependencies = 0
                };
            }
        }

        /// <summary>
        /// Get barrier scheduler state showing inserted barriers and their locations.
        /// Returns an explicit unavailable snapshot when no live barrier scheduler view is published.
        /// </summary>
        public BarrierSchedulerSnapshot GetBarrierSchedulerState()
        {
            lock (_syncLock)
            {
                return new BarrierSchedulerSnapshot
                {
                    IsAvailable = false,
                    AvailabilityReason = CompilerTelemetryUnavailableReason,
                    BarriersInserted = 0,
                    ActiveBarrierCount = 0,
                    MaxBarrierTimeout = 0,
                    CoalescedBarriers = 0
                };
            }
        }

        private static CoreStateSnapshot BuildCoreStateSnapshot(IIseMachineStateSource machineStateSource, int coreId)
        {
            ValidateCoreId(machineStateSource, coreId);
            var core = GetCore(machineStateSource, coreId);
            ReadCoreObservationSnapshot(core, out Processor.CPU_Core.PipelineObservationSnapshot observation);
            Processor.CPU_Core.PipelineControl pipeState = observation.PipelineControl;
            CoreTimebaseSnapshot timebase = BuildCoreTimebaseSnapshot(in pipeState);
            ulong[] livePcs = BuildVirtualThreadLivePcs(core, observation.ActiveVirtualThreadId, observation.ActiveLivePc);
            ulong[] committedPcs = BuildVirtualThreadCommittedPcs(core);
            bool[] stalled = BuildVirtualThreadStalledSnapshot(core, observation.ActiveVirtualThreadId, in pipeState);
            string[] stallReasons = BuildVirtualThreadStallReasons(core, stalled, in observation);
            ulong[] activeVtRegisters = BuildVirtualThreadRegisters(core, observation.ActiveVirtualThreadId);
            Processor.CPU_Core.CorePowerState currentPowerState =
                PowerControlCsr.DecodePowerState(core.Csr.DirectRead(CsrAddresses.MpowerState));
            uint currentPerformanceLevel =
                PowerControlCsr.DecodePerformanceLevel(core.Csr.DirectRead(CsrAddresses.MperfLevel));

            return new CoreStateSnapshot
            {
                CoreId = (int)core.CoreID,
                LiveInstructionPointer = observation.ActiveLivePc,
                CycleCount = timebase.CycleCount,
                CurrentState = observation.CurrentVirtualThreadPipelineState.ToString(),
                CurrentPowerState = currentPowerState,
                CurrentPerformanceLevel = currentPerformanceLevel,
                IsStalled = timebase.IsStalled,
                HasExceptions = core.ExceptionStatus.HasExceptions(),
                ActiveVirtualThreadId = observation.ActiveVirtualThreadId,
                VirtualThreadLivePcs = livePcs,
                VirtualThreadCommittedPcs = committedPcs,
                VirtualThreadStalled = stalled,
                VirtualThreadStallReasons = stallReasons,
                ActiveVirtualThreadRegisters = activeVtRegisters,
                L1VliwBundleCount = core.L1_VLIWBundles?.Length ?? 0,
                L1DataEntryCount = core.L1_Data?.Length ?? 0,
                L2VliwBundleCount = core.L2_VLIWBundles?.Length ?? 0,
                L2DataEntryCount = core.L2_Data?.Length ?? 0,
                PipelineEnabled = pipeState.Enabled,
                Timebase = timebase,
                PipelineIPC = pipeState.GetIPC(),
                PipelineEfficiency = pipeState.GetEfficiency(),
                PipelineCycleCount = timebase.CycleCount,
                PipelineStallCycles = pipeState.StallCycles,
                PipelineInstructionsRetired = pipeState.InstructionsRetired,
                PipelineBranchMispredicts = pipeState.BranchMispredicts,
                PipelineDataHazards = pipeState.DataHazards,
                PipelineMemoryStalls = pipeState.MemoryStalls,
                PipelineForwardingEvents = pipeState.ForwardingEvents,
                PipelineControlHazards = pipeState.ControlHazards,
                PipelineWAWHazards = pipeState.WAWHazards,
                PipelineLoadUseBubbles = pipeState.LoadUseBubbles,
                PipelineBundleSlotIndex = observation.PipelineBundleSlotIndex,
                DecodedBundleStateOwnerKind = observation.DecodedBundleStateOwnerKind,
                DecodedBundleStateEpoch = observation.DecodedBundleStateEpoch,
                DecodedBundleStateVersion = observation.DecodedBundleStateVersion,
                DecodedBundleStateKind = observation.DecodedBundleStateKind,
                DecodedBundleStateOrigin = observation.DecodedBundleStateOrigin,
                DecodedBundlePc = observation.DecodedBundlePc,
                DecodedBundleValidMask = observation.DecodedBundleValidMask,
                DecodedBundleNopMask = observation.DecodedBundleNopMask,
                DecodedBundleHasCanonicalDecode = observation.DecodedBundleHasCanonicalDecode,
                DecodedBundleHasCanonicalLegality = observation.DecodedBundleHasCanonicalLegality,
                DecodedBundleHasDecodeFault = observation.DecodedBundleHasDecodeFault,
                DecodePublicationCertificate = observation.DecodePublicationCertificate,
                ExecuteCompletionCertificate = observation.ExecuteCompletionCertificate,
                RetireVisibilityCertificate = observation.RetireVisibilityCertificate,
                VectorLength = core.VectorConfig.VL,
                VectorTailAgnostic = core.VectorConfig.TailAgnostic,
                VectorMaskAgnostic = core.VectorConfig.MaskAgnostic
            };
        }

        private static PodSnapshot BuildPodSnapshot(IIseMachineStateSource machineStateSource, int podIndex)
        {
            int podCount = GetPodCount(machineStateSource);
            if (podIndex < 0 || podIndex >= podCount)
            {
                throw new ArgumentOutOfRangeException(nameof(podIndex),
                    $"Pod index must be between 0 and {podCount - 1}");
            }

            PodController? pod = GetPodOrNull(machineStateSource, podIndex);
            if (pod == null)
            {
                return new PodSnapshot
                {
                    PodIndex = podIndex,
                    PodX = 0,
                    PodY = 0,
                    PodId = 0,
                    DomainCertificate = 0,
                    PowerGatedCores = 0
                };
            }

            return new PodSnapshot
            {
                PodIndex = podIndex,
                PodX = pod.PodX,
                PodY = pod.PodY,
                PodId = pod.PodId,
                DomainCertificate = pod.DomainCertificate,
                PowerGatedCores = pod.PowerController.CurrentGatedCount
            };
        }

        #endregion
    }

    #region Snapshot Structures

    /// <summary>
    /// Immutable snapshot of core state for GUI display.
    /// </summary>
    public struct CoreStateSnapshot
    {
        public int CoreId;
        public ulong LiveInstructionPointer;
        public ulong CycleCount;
        public string CurrentState;
        public Processor.CPU_Core.CorePowerState CurrentPowerState;
        public uint CurrentPerformanceLevel;
        public bool IsStalled;
        public bool HasExceptions;
        public int ActiveVirtualThreadId;
        public ulong[] VirtualThreadLivePcs;
        public ulong[] VirtualThreadCommittedPcs;
        public bool[] VirtualThreadStalled;
        public string[] VirtualThreadStallReasons;
        public ulong[] ActiveVirtualThreadRegisters;
        public int L1VliwBundleCount;
        public int L1DataEntryCount;
        public int L2VliwBundleCount;
        public int L2DataEntryCount;
        public bool PipelineEnabled;
        public CoreTimebaseSnapshot Timebase;
        public double PipelineIPC;
        public double PipelineEfficiency;
        public ulong PipelineCycleCount;
        public ulong PipelineStallCycles;
        public ulong PipelineInstructionsRetired;
        public ulong PipelineBranchMispredicts;
        public ulong PipelineDataHazards;
        public ulong PipelineMemoryStalls;
        public ulong PipelineForwardingEvents;
        public ulong PipelineControlHazards;
        public ulong PipelineWAWHazards;
        public ulong PipelineLoadUseBubbles;
        public byte PipelineBundleSlotIndex;
        public DecodedBundleStateOwnerKind DecodedBundleStateOwnerKind;
        public ulong DecodedBundleStateEpoch;
        public ulong DecodedBundleStateVersion;
        public DecodedBundleStateKind DecodedBundleStateKind;
        public DecodedBundleStateOrigin DecodedBundleStateOrigin;
        public ulong DecodedBundlePc;
        public byte DecodedBundleValidMask;
        public byte DecodedBundleNopMask;
        public bool DecodedBundleHasCanonicalDecode;
        public bool DecodedBundleHasCanonicalLegality;
        public bool DecodedBundleHasDecodeFault;
        public PipelineContourCertificate DecodePublicationCertificate;
        public PipelineContourCertificate ExecuteCompletionCertificate;
        public PipelineContourCertificate RetireVisibilityCertificate;
        public ulong VectorLength;
        public byte VectorTailAgnostic;
        public byte VectorMaskAgnostic;
    }

    /// <summary>
    /// Pod controller snapshot.
    /// </summary>
    public struct PodSnapshot
    {
        public int PodIndex;
        public int PodX;
        public int PodY;
        public ushort PodId;
        public ulong DomainCertificate;
        public int PowerGatedCores;
    }

    /// <summary>
    /// GRLB (Global Resource Lock Bitmap) snapshot.
    /// 128 bits divided into 4 banks of 32 bits each.
    /// </summary>
    public struct GrlbSnapshot
    {
        public int CoreId;
        public uint Bank0;  // Bits 0-31: Register read/write groups
        public uint Bank1;  // Bits 32-63: Memory domains + LSU + DMA 0-3
        public uint Bank2;  // Bits 64-95: Extended GRLB (DMA 4-7, Stream, Accel)
        public uint Bank3;  // Bits 96-127: Reserved / extended domains
        public ulong StructuralStalls;
    }

    /// <summary>
    /// FSP Power Controller snapshot.
    /// </summary>
    public struct FspPowerSnapshot
    {
        public int CoreId;
        public int PodIndex;
        public int CurrentGatedCount;
        public ulong TotalGatedCycles;
        public ulong GateTransitions;
        public bool IsClockGated;
    }

    /// <summary>
    /// Memory subsystem performance snapshot.
    /// </summary>
    public struct MemorySubsystemSnapshot
    {
        public int NumBanks;
        public long TotalBursts;
        public long BankConflicts;
        public long StallCycles;
        public long TotalBytesTransferred;
        public int CurrentQueuedRequests;
        public int MaxQueueDepth;
        public double AverageBurstLength;
        public double AverageWaitCycles;
        public double BurstEfficiency;
        public bool IsChannelOverloaded;
        public string ArbitrationPolicy;
    }

    /// <summary>
    /// Memory configuration snapshot.
    /// </summary>
    public struct MemoryConfigSnapshot
    {
        public int NumBanks;
        public int BankWidthBytes;
        public int NumMemoryPorts;
        public double BankBandwidthGBps;
        public int DmaThresholdBytes;
        public int AxiBoundary;
        public int MaxBurstLength;
        public int PortSwitchingPenalty;
        public int RowBufferHitLatency;
        public int RowBufferMissLatency;
        public int RowBufferSize;
    }

    /// <summary>
    /// Compiler state snapshot for multithreaded compilation.
    /// </summary>
    public struct CompilerStateSnapshot
    {
        public bool IsAvailable;
        public string AvailabilityReason;
        public bool IsCompiling;
        public int ThreadCount;
        public int TotalInstructionsCompiled;
        public int BarriersInserted;
        public int CrossThreadDependencies;
        public double BundleUtilization;
    }

    /// <summary>
    /// Per-thread compiler context state snapshot.
    /// </summary>
    public struct ThreadCompilerStateSnapshot
    {
        public bool IsAvailable;
        public string AvailabilityReason;
        public int VirtualThreadId;
        public int InstructionCount;
        public ulong DomainTag;
        public int RegistersAllocated;
        public bool HasPendingBarrier;
    }

    /// <summary>
    /// Dependency graph snapshot for cross-thread dependencies.
    /// </summary>
    public struct DependencyGraphSnapshot
    {
        public bool IsAvailable;
        public string AvailabilityReason;
        public bool[,] DependencyMatrix;  // 4x4 matrix: dependsOn[i,j] = VT-i depends on VT-j
        public int MemoryAccessCount;
        public bool HasCyclicDependency;
        public int TotalDependencies;
    }

    /// <summary>
    /// Barrier scheduler snapshot.
    /// </summary>
    public struct BarrierSchedulerSnapshot
    {
        public bool IsAvailable;
        public string AvailabilityReason;
        public int BarriersInserted;
        public int ActiveBarrierCount;
        public ulong MaxBarrierTimeout;
        public int CoalescedBarriers;
    }

    #endregion
}
