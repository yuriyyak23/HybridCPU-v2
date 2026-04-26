using System;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        private const int CompatBootstrapThreadDomainCount = 16;
        private const ulong CompatIdentityMapBytes = 0x10000000UL;
        private const int CompatMainMemoryBankCount = 4;
        private const ulong CompatMainMemoryBankSize = 0x4000000UL;

        /// <summary>
        /// Legacy static-runtime bootstrap preserved for diagnostics, GUI, and
        /// existing tests that still materialize the processor via constructor.
        /// New production code should prefer explicit instance-bound seams.
        /// </summary>
        [Obsolete("Use explicit runtime/context construction for new production paths. This constructor remains only as a legacy bootstrap compatibility surface.")]
        public Processor(ProcessorMode processorMode)
            : this(processorMode, ProcessorConfig.Default())
        {
        }

        /// <summary>
        /// Legacy static-runtime bootstrap with explicit configuration.
        /// Preserves the historical Processor ctor contract used by GUI,
        /// diagnostics, and compat tests.
        /// </summary>
        [Obsolete("Use explicit runtime/context construction for new production paths. This constructor remains only as a legacy bootstrap compatibility surface.")]
        public Processor(ProcessorMode processorMode, ProcessorConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            InitializeLegacyCompatRuntime(processorMode, config);
        }

        private static void InitializeLegacyCompatRuntime(
            ProcessorMode processorMode,
            ProcessorConfig config)
        {
            CurrentProcessorMode = processorMode;
            Config = config;
            Ready_Flag = false;

            Compiler = new ProcessorCompilerBridge();
            if (processorMode == ProcessorMode.Compiler)
            {
                Compiler.DeclareCompilerContractVersion(
                    CompilerContract.Version,
                    $"{nameof(Processor)}.{nameof(InitializeLegacyCompatRuntime)}");
            }
            MainMemory = new MultiBankMemoryArea(
                bankCount: CompatMainMemoryBankCount,
                bankSize: CompatMainMemoryBankSize);

            IOMMU.Initialize();
            SharedMemoryLockManager.Initialize();
            InterruptData.Initialize();

            Processor proc = default;
            DMAController = new DMAController(ref proc);
            Memory = new MemorySubsystem(ref proc, DMAController);

            CPU_Cores = new CPU_Core[1024];
            for (int coreIndex = 0; coreIndex < CPU_Cores.Length; coreIndex++)
            {
                CPU_Cores[coreIndex] = new CPU_Core((ushort)coreIndex, MainMemory);
            }

            Pods = new PodController[TOTAL_PODS];
            NoCRouters = new NoC_XY_Router[POD_GRID_DIM, POD_GRID_DIM];
            InitializePodTopology();
            SyncPrimitives.Initialize(CPU_Cores.Length);

            MapCompatIdentityWindow(Math.Max((ulong)MainMemory.Length, CompatIdentityMapBytes));
            AllocateCompatThreadDomains(config);

            Ready_Flag = true;
        }

        private static void MapCompatIdentityWindow(ulong byteCount)
        {
            if (byteCount == 0)
            {
                return;
            }

            _ = IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: byteCount,
                permissions: IOMMUAccessPermissions.ReadWrite);
        }

        private static void AllocateCompatThreadDomains(ProcessorConfig config)
        {
            for (int threadId = 0; threadId < CompatBootstrapThreadDomainCount; threadId++)
            {
                ulong domainSize = ResolveCompatThreadDomainSize(config, threadId);
                if (domainSize == 0)
                {
                    continue;
                }

                _ = IOMMU.AllocateDomain(threadId, domainSize, MemoryDomainFlags.ReadWrite);
            }
        }

        private static ulong ResolveCompatThreadDomainSize(
            ProcessorConfig config,
            int threadId)
        {
            if (config.CustomThreadDomainSizes != null &&
                config.CustomThreadDomainSizes.TryGetValue(threadId, out ulong customDomainSize) &&
                customDomainSize > 0)
            {
                return customDomainSize;
            }

            return config.ThreadDomainSize;
        }
    }
}
