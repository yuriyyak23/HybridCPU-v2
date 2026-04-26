using System;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Frontend profile that feeds the canonical ISA v2 backend.
    /// </summary>
    public enum FrontendMode : byte
    {
        /// <summary>
        /// Native HybridCPU VLIW bundle/instruction frontend.
        /// </summary>
        NativeVLIW = 0,

        /// <summary>
        /// RISC-V compatibility frontend lowered into the same canonical backend ISA.
        /// </summary>
        /// RvCompat = 1
    }

    /// <summary>
    /// Processor baseline mode for evaluation comparisons (Q1 Review §3).
    /// </summary>
    public enum BaselineMode : byte
    {
        /// <summary>
        /// Full FSP-enabled mode (default production behavior).
        /// VLIW slot stealing is active across all threads.
        /// </summary>
        FSP_Enabled = 0,

        /// <summary>
        /// In-order baseline mode: disables FSP and simulates classical
        /// in-order instruction issue with stall logic (ARM Cortex-A55 style).
        /// Used to establish a physically realistic IPC competitor.
        /// </summary>
        InOrder_Baseline = 1
    }

    /// <summary>
    /// Configuration parameters for HybridCPU processor.
    /// Allows customization of memory subsystem, DMA, and performance modeling.
    /// </summary>
    public class ProcessorConfig
    {
        #region Memory Subsystem Configuration

        /// <summary>
        /// Number of memory banks (default: 8)
        /// </summary>
        public int NumMemoryBanks { get; set; } = 8;

        /// <summary>
        /// Width of each memory bank in bytes (default: 64)
        /// </summary>
        public int BankWidthBytes { get; set; } = 64;

        /// <summary>
        /// Threshold for DMA routing in bytes (default: 1024)
        /// </summary>
        public int DmaThresholdBytes { get; set; } = 1024;

        /// <summary>
        /// Memory domain size per thread in bytes (default: 32MB = 16MB stack + 16MB heap)
        /// Phase 5 extension: Configurable per-thread memory isolation
        /// </summary>
        public ulong ThreadDomainSize { get; set; } = 32 * 1024 * 1024; // 32MB

        /// <summary>
        /// Custom domain sizes for specific threads (optional)
        /// Key: thread ID (0-15), Value: domain size in bytes
        /// If not specified, ThreadDomainSize is used
        /// Phase 5 extension: Different domain sizes for different thread types
        /// </summary>
        public Dictionary<int, ulong>? CustomThreadDomainSizes { get; set; } = null;

        /// <summary>
        /// AXI4 boundary alignment requirement (default: 4096 = 4KB)
        /// </summary>
        public int AxiBoundary { get; set; } = 4096;

        /// <summary>
        /// Maximum burst length in beats (default: 256 for AXI4)
        /// </summary>
        public int MaxBurstLength { get; set; } = 256;

        /// <summary>
        /// Number of memory ports (default: 2)
        /// Controls concurrent memory accesses across banks
        /// </summary>
        public int NumMemoryPorts { get; set; } = 2;

        /// <summary>
        /// Memory bandwidth in GB/s per bank (default: 25.6)
        /// Used for adaptive stall calculation
        /// </summary>
        public double BankBandwidthGBps { get; set; } = 25.6;

        /// <summary>
        /// Bank arbitration policy (default: RoundRobin)
        /// </summary>
        public Memory.BankArbitrationPolicy ArbitrationPolicy { get; set; } = Memory.BankArbitrationPolicy.RoundRobin;

        #endregion

        #region DMA Controller Configuration

        /// <summary>
        /// Number of DMA channels (default: 8)
        /// </summary>
        public int NumDmaChannels { get; set; } = 8;

        /// <summary>
        /// Minimum burst length for DMA usage (default: 256 bytes)
        /// </summary>
        public int MinDmaBurstLength { get; set; } = 256;

        #endregion

        #region Pipeline Configuration

        /// <summary>
        /// Enable cycle-accurate pipeline simulation (default: false)
        /// When disabled, uses faster functional simulation
        /// </summary>
        public bool CycleAccuratePipeline { get; set; } = false;

        /// <summary>
        /// L1 cache hit latency in cycles (default: 4)
        /// </summary>
        public int L1CacheLatency { get; set; } = 4;

        /// <summary>
        /// L2 cache hit latency in cycles (default: 12)
        /// </summary>
        public int L2CacheLatency { get; set; } = 12;

        /// <summary>
        /// DRAM access latency in cycles (default: 100)
        /// </summary>
        public int DramLatency { get; set; } = 100;

        #endregion

        #region Execution Mode Configuration

        /// <summary>
        /// Frontend profile used to produce the canonical ISA v2 instruction stream.
        /// </summary>
        public FrontendMode FrontendMode { get; set; } = FrontendMode.NativeVLIW;

        /// <summary>
        /// Processor baseline mode for evaluation comparisons (Q1 Review §3).
        /// Controls whether FSP slot stealing is active or disabled for IPC baseline measurement.
        /// </summary>
        public BaselineMode ExecutionMode { get; set; } = BaselineMode.FSP_Enabled;

        /// <summary>
        /// Enable VLIW slot stealing via FSP (Fine-Grained Slot Pilfering).
        /// Convenience property: returns true when ExecutionMode == FSP_Enabled.
        /// Set to false to simulate an in-order baseline for evaluation.
        /// </summary>
        public bool VliwStealEnabled
        {
            get => ExecutionMode == BaselineMode.FSP_Enabled;
            set => ExecutionMode = value ? BaselineMode.FSP_Enabled : BaselineMode.InOrder_Baseline;
        }

        /// <summary>
        /// Enable 2-stage pipelined FSP arbitration for HLS timing closure.
        /// When true, MicroOpScheduler uses SCHED1/SCHED2 pipeline stages
        /// instead of single-cycle combinational path (reduces LUT depth for synthesis).
        /// Default: false (single-cycle, backwards compatible).
        /// </summary>
        public bool PipelinedFspEnabled { get; set; } = false;

        /// <summary>
        /// Phase 2C: Maximum concurrent speculative operations allowed by the scheduler.
        /// When the in-flight speculative count reaches this cap, new speculative injections are blocked.
        /// HLS: 4-bit comparator threshold.
        /// Default: 4 (SCOREBOARD_SLOTS / 2).
        /// </summary>
        public int SpeculationBudgetMax { get; set; } = 4;

        /// <summary>
        /// Phase 2A: Enable credit-based deterministic fairness ranking in the scheduler.
        /// When true, VTs with higher accumulated credit are ranked first for injection.
        /// Default: true.
        /// </summary>
        public bool CreditFairnessEnabled { get; set; } = true;

        /// <summary>
        /// Phase 2B: Enable bank-pressure tie-breaking for memory-op candidates.
        /// When true, candidates targeting less-congested banks are preferred.
        /// Default: true.
        /// </summary>
        public bool BankPressureTieBreakEnabled { get; set; } = true;

        #endregion

        #region Profiling Configuration

        /// <summary>
        /// Enable performance profiling (default: false)
        /// </summary>
        public bool ProfilingEnabled { get; set; } = false;

        /// <summary>
        /// Profiling options (null = use defaults when profiling is enabled)
        /// </summary>
        public ProfilingOptions? ProfilingOptions { get; set; } = null;

        #endregion

        #region Factory Methods

        /// <summary>
        /// Create default configuration for emulation mode
        /// </summary>
        public static ProcessorConfig Default()
        {
            return new ProcessorConfig();
        }

        /// <summary>
        /// Create configuration for high-performance FPGA simulation
        /// </summary>
        public static ProcessorConfig HighPerformanceFPGA()
        {
            return new ProcessorConfig
            {
                NumMemoryBanks = 16,
                BankWidthBytes = 128,
                DmaThresholdBytes = 512,
                NumDmaChannels = 16,
                NumMemoryPorts = 4,
                BankBandwidthGBps = 51.2,
                ArbitrationPolicy = Memory.BankArbitrationPolicy.WeightedFair,
                L1CacheLatency = 2,
                L2CacheLatency = 8,
                DramLatency = 80
            };
        }

        /// <summary>
        /// Create configuration for detailed profiling
        /// </summary>
        public static ProcessorConfig Profiling()
        {
            return new ProcessorConfig
            {
                ProfilingEnabled = true,
                CycleAccuratePipeline = true,
                ProfilingOptions = ProfilingOptions.All()
            };
        }

        /// <summary>
        /// Create configuration for testing (deterministic, minimal conflicts)
        /// </summary>
        internal static ProcessorConfig Testing()
        {
            return new ProcessorConfig
            {
                NumMemoryBanks = 1,
                BankWidthBytes = 64,
                DmaThresholdBytes = 1024,
                ProfilingEnabled = false
            };
        }

        #endregion
    }

    /// <summary>
    /// Profiling options for performance monitoring
    /// </summary>
    public class ProfilingOptions
    {
        /// <summary>
        /// Collect memory subsystem statistics
        /// </summary>
        public bool CollectMemoryStats { get; set; } = true;

        /// <summary>
        /// Collect pipeline statistics (IPC, stalls, hazards)
        /// </summary>
        public bool CollectPipelineStats { get; set; } = true;

        /// <summary>
        /// Collect DMA transfer statistics
        /// </summary>
        public bool CollectDMATransfers { get; set; } = true;

        /// <summary>
        /// Collect detailed burst traces (memory intensive)
        /// </summary>
        public bool CollectBurstTraces { get; set; } = false;

        /// <summary>
        /// Collect vector ALU operation statistics
        /// </summary>
        public bool CollectVectorStats { get; set; } = true;

        /// <summary>
        /// Create profiling options with all features enabled
        /// </summary>
        public static ProfilingOptions All()
        {
            return new ProfilingOptions
            {
                CollectMemoryStats = true,
                CollectPipelineStats = true,
                CollectDMATransfers = true,
                CollectBurstTraces = true,
                CollectVectorStats = true
            };
        }

        /// <summary>
        /// Create minimal profiling options (memory only)
        /// </summary>
        public static ProfilingOptions Minimal()
        {
            return new ProfilingOptions
            {
                CollectMemoryStats = true,
                CollectPipelineStats = false,
                CollectDMATransfers = false,
                CollectBurstTraces = false,
                CollectVectorStats = false
            };
        }

        /// <summary>
        /// Create default profiling options
        /// </summary>
        public static ProfilingOptions Default()
        {
            return new ProfilingOptions
            {
                CollectMemoryStats = true,
                CollectPipelineStats = true,
                CollectDMATransfers = true,
                CollectBurstTraces = false,
                CollectVectorStats = true
            };
        }
    }
}
