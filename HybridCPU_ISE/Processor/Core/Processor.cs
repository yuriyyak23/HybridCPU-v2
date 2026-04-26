using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU
{
    public enum ProcessorMode
    {
        Emulation,
        Compiler,
        FPGA_KiWi
    }

    public partial struct Processor
    {
        // DMA Controller instance (ref2.md)
        public static YAKSys_Hybrid_CPU.Memory.DMAController? DMAController;

        // Memory Subsystem instance (ref3.md - PerfModel)
        public static YAKSys_Hybrid_CPU.Memory.MemorySubsystem? Memory;

        // Processor configuration (ref3.md - PerfModel)
        public static ProcessorConfig? Config;

        // Profiling state (ref3.md - PerfModel)
        public static bool ProfilingEnabled = false;
        public static ProfilingOptions? CurrentProfilingOptions = null;

        // ===== Clustered Pods topology (req.md §1, tech.md §1) =====

        /// <summary>Grid dimension: 8×8 = 64 Pods</summary>
        public const int POD_GRID_DIM = 8;

        /// <summary>Total number of Pods (64)</summary>
        public const int TOTAL_PODS = POD_GRID_DIM * POD_GRID_DIM;

        /// <summary>Pod controllers array: 64 Pods, each managing 16 cores</summary>
        public static PodController[] Pods = new PodController[TOTAL_PODS];

        /// <summary>2D-Mesh NoC routers: 8×8 grid for inter-Pod burst transfers (tech.md §2)</summary>
        public static NoC_XY_Router[,] NoCRouters = new NoC_XY_Router[POD_GRID_DIM, POD_GRID_DIM];

        /// <summary>
        /// Build 8×8 Pod topology with NoC mesh and per-Pod FSP schedulers (req.md §1, tech.md §1–§2).
        /// Each Pod gets 16 cores and its own MicroOpScheduler.
        /// NoC routers are connected via XY dimension-order links.
        /// Core CSRs (POD_ID, POD_AFFINITY_MASK) are initialized.
        /// </summary>
        private static void InitializePodTopology()
        {
            // 1. Create Pods and NoC routers
            for (int y = 0; y < POD_GRID_DIM; y++)
            {
                for (int x = 0; x < POD_GRID_DIM; x++)
                {
                    int podIndex = y * POD_GRID_DIM + x;
                    var scheduler = new MicroOpScheduler();
                    Pods[podIndex] = new PodController(x, y, scheduler);

                    NoCRouters[x, y] = new NoC_XY_Router(x, y);

                    // Initialize CSRs for the 16 cores belonging to this Pod
                    ushort podId = Pods[podIndex].PodId;
                    int baseCoreId = podIndex * PodController.CORES_PER_POD;
                    for (int c = 0; c < PodController.CORES_PER_POD; c++)
                    {
                        int globalId = baseCoreId + c;
                        if (globalId < CPU_Cores.Length)
                        {
                            CPU_Cores[globalId].CsrPodId = podId;
                            CPU_Cores[globalId].CsrPodAffinityMask = 0xFFFF; // all 16 cores active
                        }
                    }
                }
            }

            // 2. Wire NoC mesh: connect each router to its cardinal neighbours
            for (int y = 0; y < POD_GRID_DIM; y++)
            {
                for (int x = 0; x < POD_GRID_DIM; x++)
                {
                    var router = NoCRouters[x, y];
                    if (x + 1 < POD_GRID_DIM) router.ConnectNeighbor(NoCPort.East, NoCRouters[x + 1, y]);
                    if (x - 1 >= 0) router.ConnectNeighbor(NoCPort.West, NoCRouters[x - 1, y]);
                    if (y + 1 < POD_GRID_DIM) router.ConnectNeighbor(NoCPort.North, NoCRouters[x, y + 1]);
                    if (y - 1 >= 0) router.ConnectNeighbor(NoCPort.South, NoCRouters[x, y - 1]);
                }
            }
        }

        /// <summary>
        /// Get the PodController owning a given global core ID.
        /// </summary>
        /// <param name="globalCoreId">Global core index (0–1023)</param>
        /// <returns>PodController, or null if core ID is out of range</returns>
        public static PodController? GetPodForCore(int globalCoreId)
        {
            int podIndex = globalCoreId / PodController.CORES_PER_POD;
            if ((uint)podIndex >= (uint)TOTAL_PODS) return null;
            return Pods[podIndex];
        }

        /// <summary>
        /// Resolve a PodController by architectural Pod ID.
        /// </summary>
        public static PodController? GetPodById(ushort podId)
        {
            int podX = podId >> 8;
            int podY = podId & 0xFF;
            if ((uint)podX >= POD_GRID_DIM || (uint)podY >= POD_GRID_DIM)
                return null;

            int podIndex = (podY * POD_GRID_DIM) + podX;
            if ((uint)podIndex >= (uint)TOTAL_PODS)
                return null;

            return Pods[podIndex];
        }

        /// <summary>
        /// Get the local core index (0–15) within a Pod for a given global core ID.
        /// </summary>
        public static int GetLocalCoreId(int globalCoreId)
        {
            return globalCoreId % PodController.CORES_PER_POD;
        }


        public static CPU_Core[] CPU_Cores = new CPU_Core[1024];

        public static bool Ready_Flag;

        public static ProcessorMode CurrentProcessorMode;

        public static ProcessorCompilerBridge Compiler = new ProcessorCompilerBridge();
    }
}
