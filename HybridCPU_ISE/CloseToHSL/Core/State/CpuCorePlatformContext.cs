using System;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Explicit runtime dependencies for a live CPU core instance.
    /// New production core construction should go through this context instead
    /// of implicitly reading mutable global Processor state.
    /// </summary>
    public readonly record struct CpuCorePlatformContext
    {
        private readonly Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? _atomicMemoryUnitFactory;

        public CpuCorePlatformContext(
            Processor.MainMemoryArea mainMemory,
            ProcessorMode initialExecutionMode,
            bool trackGlobalExecutionMode = false,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null)
        {
            MainMemory = mainMemory ?? throw new ArgumentNullException(nameof(mainMemory));
            InitialExecutionMode = initialExecutionMode;
            TrackGlobalExecutionMode = trackGlobalExecutionMode;
            _atomicMemoryUnitFactory = atomicMemoryUnitFactory;
            IsConfigured = true;
        }

        public Processor.MainMemoryArea MainMemory { get; }

        public ProcessorMode InitialExecutionMode { get; }

        public bool TrackGlobalExecutionMode { get; }

        public bool IsConfigured { get; }

        public ProcessorMode ResolveExecutionMode() =>
            TrackGlobalExecutionMode ? Processor.CurrentProcessorMode : InitialExecutionMode;

        public IAtomicMemoryUnit CreateAtomicMemoryUnit()
        {
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = _atomicMemoryUnitFactory;
            if (atomicMemoryUnitFactory != null)
            {
                return atomicMemoryUnitFactory(MainMemory);
            }

            return new MainMemoryAtomicMemoryUnit(MainMemory);
        }

        public static CpuCorePlatformContext CreateFixed(
            Processor.MainMemoryArea mainMemory,
            ProcessorMode executionMode,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null) =>
            new(
                mainMemory,
                executionMode,
                trackGlobalExecutionMode: false,
                atomicMemoryUnitFactory: atomicMemoryUnitFactory);

        public static CpuCorePlatformContext CreateLegacy(
            Processor.MainMemoryArea? mainMemory = null,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null) =>
            new(
                mainMemory ?? Processor.MainMemory,
                Processor.CurrentProcessorMode,
                trackGlobalExecutionMode: true,
                atomicMemoryUnitFactory: atomicMemoryUnitFactory);
    }
}
