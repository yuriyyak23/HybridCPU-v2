using System;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        /// <summary>
        /// Returns the existing live facade identity. Slot replacement remains
        /// restricted to the explicit platform lifecycle operation.
        /// </summary>
        public static CPU_Core GetCoreRef(int coreId)
        {
            ref CPU_Core liveCore = ref GetCoreSlotRef(coreId);
            if (liveCore is null)
            {
                throw new InvalidOperationException(
                    $"The platform core table contains an absent core slot at index {coreId}.");
            }
            _ = liveCore.Runtime;
            return liveCore;
        }

        private static ref CPU_Core GetCoreSlotRef(int coreId)
        {
            CPU_Core[] cores = CPU_Cores ??
                throw new InvalidOperationException("The platform core table is not initialized.");
            if ((uint)coreId >= (uint)cores.Length)
                throw new ArgumentOutOfRangeException(nameof(coreId));

            return ref cores[coreId];
        }

        /// <summary>
        /// Opens an explicit platform-construction interval. Slots are absent
        /// until populated through <see cref="ReplaceCore"/> and are not valid
        /// live identities during this interval.
        /// </summary>
        private static void BeginCoreTableConstruction(int coreCount)
        {
            if (coreCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(coreCount));

            CPU_Cores = new CPU_Core[coreCount];
        }

        /// <summary>
        /// Closes platform construction only after every configured slot owns
        /// a constructed runtime identity.
        /// </summary>
        private static void ValidateCoreTableConstructionComplete()
        {
            CPU_Core[] cores = CPU_Cores ??
                throw new InvalidOperationException("The platform core table is not initialized.");
            if (cores.Length == 0)
                throw new InvalidOperationException("The platform core table has no configured cores.");

            for (int coreId = 0; coreId < cores.Length; coreId++)
            {
                if (cores[coreId] is null)
                {
                    throw new InvalidOperationException(
                        $"The platform core table contains an absent core slot at index {coreId}.");
                }

                try
                {
                    _ = cores[coreId].Runtime;
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException(
                        $"The platform core table contains an absent core slot at index {coreId}.",
                        exception);
                }
            }
        }

        /// <summary>
        /// Returns a detached, read-only diagnostic projection. The result has
        /// no live runtime owner reference and cannot be written back as a core.
        /// </summary>
        public static HybridCPU_ISE.Machine.CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId) =>
            HybridCPU_ISE.Machine.CpuCoreDiagnosticSnapshot.Capture(GetCoreRef(coreId));

        /// <summary>
        /// Explicit whole-core lifecycle replacement. Ordinary cycle execution
        /// and diagnostic mutation must use <see cref="GetCoreRef"/> instead.
        /// </summary>
        public static void ReplaceCore(int coreId, CPU_Core replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            _ = replacement.Runtime;
            ref CPU_Core liveCore = ref GetCoreSlotRef(coreId);
            liveCore = replacement;
        }

#if TESTING
        internal static void EnsureCoreTableForTesting(int requiredCoreCount)
        {
            if (requiredCoreCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCoreCount));

            int targetCount = Math.Max(requiredCoreCount, CPU_Cores.Length);
            CPU_Core[] replacement = new CPU_Core[targetCount];
            for (int coreId = 0; coreId < targetCount; coreId++)
            {
                if (coreId < CPU_Cores.Length)
                {
                    try
                    {
                        ref var existing = ref GetCoreSlotRef(coreId);
                        if (existing is null)
                            throw new InvalidOperationException();
                        _ = existing.Runtime;
                        replacement[coreId] = existing;
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                replacement[coreId] = new CPU_Core((ushort)coreId);
            }

            CPU_Cores = replacement;
        }

        internal static CPU_Core[] InstallCoreTableForTesting(params CPU_Core[] replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            if (replacement.Length == 0)
                throw new ArgumentException("A test core table must contain at least one core.", nameof(replacement));
            foreach (CPU_Core core in replacement)
                _ = core.Runtime;

            CPU_Core[] previous = CPU_Cores;
            CPU_Cores = replacement;
            return previous;
        }

        internal static void RestoreCoreTableForTesting(CPU_Core[] previous)
        {
            ArgumentNullException.ThrowIfNull(previous);
            CPU_Cores = previous;
        }
#endif
    }
}
