
using System;
using System.Collections.Generic;
using System.Threading;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static long _resourceMaskStructuralFallbackCount;
        private static long _placementStructuralFallbackCount;

        internal static ulong ResourceMaskStructuralFallbackCount =>
            (ulong)Interlocked.Read(ref _resourceMaskStructuralFallbackCount);

        internal static ulong PlacementStructuralFallbackCount =>
            (ulong)Interlocked.Read(ref _placementStructuralFallbackCount);

        internal static void ResetSafetyFallbackTelemetryForTesting()
        {
            Interlocked.Exchange(ref _resourceMaskStructuralFallbackCount, 0);
            Interlocked.Exchange(ref _placementStructuralFallbackCount, 0);
        }

        internal static SafetyMask128 BuildExplicitStructuralSafetyMask(MicroOp microOp)
        {
            ArgumentNullException.ThrowIfNull(microOp);

            SafetyMask128 explicitMask = new SafetyMask128(
                microOp.ResourceMask.Low,
                microOp.ResourceMask.High);

            if (microOp.Placement.RequiredSlotClass is SlotClass.BranchControl or SlotClass.SystemSingleton)
            {
                explicitMask |= new SafetyMask128(0UL, StructuralFallbackLane7SingletonBitHigh);
            }
            else if (microOp.Placement.RequiredSlotClass == SlotClass.DmaStreamClass &&
                     explicitMask.IsZero)
            {
                explicitMask |= new SafetyMask128(0UL, StructuralFallbackDmaStreamLaneBitHigh);
            }

            if (explicitMask.IsZero &&
                (microOp.SerializationClass != SerializationClass.Free ||
                 microOp.IsControlFlow ||
                 microOp.HasSideEffects))
            {
                explicitMask |= new SafetyMask128(0UL, StructuralFallbackSerializedOpBitHigh);
            }

            return explicitMask;
        }

        internal static bool RequiresExplicitStructuralSafetyMask(MicroOp microOp)
        {
            ArgumentNullException.ThrowIfNull(microOp);

            if (microOp.CanonicalDecodePublication == CanonicalDecodePublicationMode.Unspecified ||
                microOp is NopMicroOp)
            {
                return false;
            }

            bool hasMemoryVisibility =
                (microOp.ReadMemoryRanges != null && microOp.ReadMemoryRanges.Count > 0) ||
                (microOp.WriteMemoryRanges != null && microOp.WriteMemoryRanges.Count > 0);

            return microOp.IsMemoryOp ||
                   hasMemoryVisibility ||
                   microOp.IsControlFlow ||
                   microOp.HasSideEffects ||
                   microOp.SerializationClass != SerializationClass.Free ||
                   microOp.Placement.RequiredSlotClass is SlotClass.BranchControl or SlotClass.SystemSingleton or SlotClass.DmaStreamClass;
        }

        internal static InvalidOperationException CreateMissingExplicitStructuralSafetyMaskException(
            MicroOp microOp,
            string surface)
        {
            ArgumentNullException.ThrowIfNull(microOp);
            ArgumentException.ThrowIfNullOrWhiteSpace(surface);

            return new InvalidOperationException(
                $"{surface} observed {microOp.GetType().Name} (opcode 0x{microOp.OpCode:X}) without an explicit structural safety mask on a canonical non-NOP contour. " +
                "Micro-ops with memory visibility, control flow, side effects, or non-free serialization must publish explicit structural safety truth instead of relying on hidden resource/placement fallback synthesis.");
        }

        private static SysEventMicroOp CreateSystemEventMicroOp(uint opcode, Func<SysEventMicroOp> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            SysEventMicroOp microOp = factory();
            microOp.OpCode = opcode;
            microOp.InitializeMetadata();
            return microOp;
        }

        /// <summary>
        /// Blueprint Phase 7: compute a <see cref="SafetyMask128"/> for a MicroOp.
        /// Uses <see cref="DefaultSafetyMask128Generator"/> so that safety authority
        /// stays on the 128-bit surface with per-VT isolation bits in the high word.
        /// </summary>
        public static SafetyMask128 ComputeSafetyMask(uint opCode, MicroOp microOp, int memoryDomainId)
        {
            if (!_initialized)
            {
                Initialize();
            }

            IReadOnlyList<(ulong Address, ulong Length)> readMemoryRanges =
                microOp.ReadMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
            IReadOnlyList<(ulong Address, ulong Length)> normalizedReadMemoryRanges = readMemoryRanges;
            if (ReadRangeMetadataHelper.TryNormalizeContiguousReadRanges(
                readMemoryRanges,
                out IReadOnlyList<(ulong Address, ulong Length)> normalizedRanges))
            {
                normalizedReadMemoryRanges = normalizedRanges;
            }

            AssistCoalescingDescriptor assistCoalescingDescriptor =
                ReadRangeMetadataHelper.BuildAssistCoalescingDescriptor(
                    readMemoryRanges,
                    normalizedReadMemoryRanges);
            ReadRangeMetadataHelper.ValidateCoalescedRangeMetadata(
                readMemoryRanges,
                normalizedReadMemoryRanges,
                assistCoalescingDescriptor);

            var context = new SafetyMaskContext
            {
                ReadRegisters = microOp.ReadRegisters,
                WriteRegisters = microOp.WriteRegisters,
                ReadMemoryRanges = readMemoryRanges,
                NormalizedReadMemoryRanges = normalizedReadMemoryRanges,
                WriteMemoryRanges = microOp.WriteMemoryRanges,
                AssistCoalescingDescriptor = assistCoalescingDescriptor,
                MemoryDomainId = memoryDomainId,
                IsMemoryOp = microOp.IsMemoryOp,
                IsLoad = microOp is LoadMicroOp,
                IsStore = microOp is StoreMicroOp,
                IsAtomic = microOp is AtomicMicroOp || microOp.InstructionClass == InstructionClass.Atomic,
                VirtualThreadId = microOp.VirtualThreadId
            };

            SafetyMask128 computedMask = DefaultSafetyMask128Generator(context);

            return ApplyStructuralFallbackSafetyMask(computedMask, microOp);
        }

        /// <summary>
        /// Blueprint Phase 7: 128-bit safety mask generator.
        /// Encodes the baseline register, memory-domain, and LSU-class hazards
        /// directly on the 128-bit authority surface.
        /// </summary>
        public static SafetyMask128 DefaultSafetyMask128Generator(SafetyMaskContext context)
        {
            ulong low = 0;

            if (context.ReadRegisters != null)
            {
                foreach (int reg in context.ReadRegisters)
                {
                    int group = reg / 4;
                    if (group < 16)
                    {
                        low |= 1UL << group;
                    }
                }
            }

            if (context.WriteRegisters != null)
            {
                foreach (int reg in context.WriteRegisters)
                {
                    int group = reg / 4;
                    if (group < 16)
                    {
                        low |= 1UL << (16 + group);
                    }
                }
            }

            if (context.IsMemoryOp && context.MemoryDomainId >= 0 && context.MemoryDomainId < 16)
            {
                low |= 1UL << (32 + context.MemoryDomainId);
            }

            if (context.IsLoad)
            {
                low |= 1UL << 48;
            }

            if (context.IsStore)
            {
                low |= 1UL << 49;
            }

            if (context.IsAtomic)
            {
                low |= 1UL << 50;
            }

            return new SafetyMask128(low, 0UL);
        }

        private static SafetyMask128 ApplyStructuralFallbackSafetyMask(SafetyMask128 computedMask, MicroOp microOp)
        {
            if (computedMask.IsNonZero)
            {
                return computedMask;
            }

            if (!microOp.AllowsStructuralSafetyFallback)
            {
                if (RequiresExplicitStructuralSafetyMask(microOp))
                {
                    throw CreateMissingExplicitStructuralSafetyMaskException(
                        microOp,
                        "InstructionRegistry.ComputeSafetyMask()");
                }

                return SafetyMask128.Zero;
            }

            SafetyMask128 structuralMask = new SafetyMask128(microOp.ResourceMask.Low, microOp.ResourceMask.High);
            if (structuralMask.IsNonZero)
            {
                Interlocked.Increment(ref _resourceMaskStructuralFallbackCount);
                return structuralMask;
            }

            SafetyMask128 placementFallbackMask = BuildPlacementBackedStructuralSafetyMask(
                microOp.Placement,
                microOp.SerializationClass,
                microOp.IsControlFlow,
                microOp.HasSideEffects);

            if (placementFallbackMask.IsNonZero)
            {
                Interlocked.Increment(ref _placementStructuralFallbackCount);
            }

            return placementFallbackMask;
        }

        private static SafetyMask128 BuildPlacementBackedStructuralSafetyMask(
            SlotPlacementMetadata placement,
            SerializationClass serializationClass,
            bool isControlFlow,
            bool hasSideEffects)
        {
            return placement.RequiredSlotClass switch
            {
                SlotClass.BranchControl or SlotClass.SystemSingleton => new SafetyMask128(0UL, StructuralFallbackLane7SingletonBitHigh),
                SlotClass.DmaStreamClass => new SafetyMask128(0UL, StructuralFallbackDmaStreamLaneBitHigh),
                _ when serializationClass != SerializationClass.Free
                    || isControlFlow
                    || hasSideEffects => new SafetyMask128(0UL, StructuralFallbackSerializedOpBitHigh),
                _ => SafetyMask128.Zero,
            };
        }
    }
}
