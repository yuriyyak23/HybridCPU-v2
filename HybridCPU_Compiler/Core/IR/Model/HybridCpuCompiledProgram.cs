using System;
using System.Collections.Generic;
using HybridCPU.Compiler.Core;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Canonical Stage 5/6 compilation result for one VT-local instruction stream.
    /// </summary>
    public sealed class HybridCpuCompiledProgram
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HybridCpuCompiledProgram"/> class.
        /// </summary>
        public HybridCpuCompiledProgram(
            IrProgramSchedule programSchedule,
            IrProgramBundlingResult bundleLayout,
            IReadOnlyList<VLIW_Bundle> loweredBundles,
            byte[] programImage,
            int contractVersion,
            ulong? emissionBaseAddress = null,
            IrAdmissibilityAgreement? admissibilityAgreement = null,
            IReadOnlyList<VliwBundleAnnotations>? loweredBundleAnnotations = null)
        {
            ArgumentNullException.ThrowIfNull(programSchedule);
            ArgumentNullException.ThrowIfNull(bundleLayout);
            ArgumentNullException.ThrowIfNull(loweredBundles);
            ArgumentNullException.ThrowIfNull(programImage);

            int expectedImageLength = loweredBundles.Count * HybridCpuBundleSerializer.BundleSizeBytes;
            if (programImage.Length != expectedImageLength)
            {
                throw new ArgumentException($"Serialized image length must match the lowered bundle count ({expectedImageLength} bytes).", nameof(programImage));
            }

            IrAdmissibilityAgreement resolvedAgreement =
                admissibilityAgreement ?? new HybridCpuBundleBuilder().BuildAgreement(bundleLayout);
            if (resolvedAgreement.TotalBundleCount != loweredBundles.Count)
            {
                throw new ArgumentException(
                    $"Admissibility agreement bundle count ({resolvedAgreement.TotalBundleCount}) must match lowered bundle count ({loweredBundles.Count}).",
                    nameof(admissibilityAgreement));
            }

            IReadOnlyList<VliwBundleAnnotations> resolvedBundleAnnotations =
                ResolveLoweredBundleAnnotations(
                    loweredBundles.Count,
                    loweredBundleAnnotations);

            ProgramSchedule = programSchedule;
            BundleLayout = bundleLayout;
            LoweredBundles = loweredBundles;
            LoweredBundleAnnotations = resolvedBundleAnnotations;
            ProgramImage = programImage;
            AdmissibilityAgreement = resolvedAgreement;
            ContractVersion = contractVersion;
            EmissionBaseAddress = emissionBaseAddress;
        }

        /// <summary>
        /// Gets the Stage 5 schedule consumed by Stage 6.
        /// </summary>
        public IrProgramSchedule ProgramSchedule { get; }

        /// <summary>
        /// Gets the materialized Stage 6 bundle layout.
        /// </summary>
        public IrProgramBundlingResult BundleLayout { get; }

        /// <summary>
        /// Gets the backend-facing lowered bundles in program order.
        /// </summary>
        public IReadOnlyList<VLIW_Bundle> LoweredBundles { get; }

        /// <summary>
        /// Gets per-lowered-bundle sideband annotations aligned by physical slot.
        /// Descriptor sideband here is transport evidence only and is still revalidated by ISE decode/projector.
        /// </summary>
        public IReadOnlyList<VliwBundleAnnotations> LoweredBundleAnnotations { get; }

        /// <summary>
        /// Gets the compiled-program-level compiler/runtime agreement summary.
        /// This is diagnostic/build evidence, not runtime legality authority.
        /// </summary>
        public IrAdmissibilityAgreement AdmissibilityAgreement { get; }

        /// <summary>
        /// Gets the contiguous fetch-ready program image.
        /// </summary>
        public byte[] ProgramImage { get; }

        /// <summary>
        /// Gets the compiler/runtime contract version declared by the producer of this artifact.
        /// </summary>
        public int ContractVersion { get; }

        /// <summary>
        /// Gets the emission base address when the canonical compile path also emitted to memory.
        /// </summary>
        public ulong? EmissionBaseAddress { get; }

        /// <summary>
        /// Gets the number of lowered physical bundles.
        /// </summary>
        public int BundleCount => LoweredBundles.Count;

        /// <summary>
        /// Emits the already materialized Stage 6 bundle image into main memory at the specified address.
        /// </summary>
        public HybridCpuCompiledProgram EmitVliwBundleImage(ulong baseAddress)
        {
            return HybridCpuCanonicalCompiler.EmitProgram(this, baseAddress);
        }

        /// <summary>
        /// Validates this artifact against the active runtime compiler contract.
        /// </summary>
        public void ValidateRuntimeContractCompatibility(string consumerSurface)
        {
            CompilerContract.ThrowIfVersionMismatch(ContractVersion, consumerSurface);
        }

        internal HybridCpuCompiledProgram WithEmissionBaseAddress(ulong emissionBaseAddress)
        {
            return new HybridCpuCompiledProgram(
                ProgramSchedule,
                BundleLayout,
                LoweredBundles,
                ProgramImage,
                ContractVersion,
                emissionBaseAddress,
                AdmissibilityAgreement,
                LoweredBundleAnnotations);
        }

        private static IReadOnlyList<VliwBundleAnnotations> ResolveLoweredBundleAnnotations(
            int loweredBundleCount,
            IReadOnlyList<VliwBundleAnnotations>? loweredBundleAnnotations)
        {
            if (loweredBundleAnnotations is null)
            {
                var emptyAnnotations = new VliwBundleAnnotations[loweredBundleCount];
                for (int index = 0; index < emptyAnnotations.Length; index++)
                {
                    emptyAnnotations[index] = VliwBundleAnnotations.Empty;
                }

                return Array.AsReadOnly(emptyAnnotations);
            }

            if (loweredBundleAnnotations.Count != loweredBundleCount)
            {
                throw new ArgumentException(
                    $"Lowered bundle annotations count ({loweredBundleAnnotations.Count}) must match lowered bundle count ({loweredBundleCount}).",
                    nameof(loweredBundleAnnotations));
            }

            var copy = new VliwBundleAnnotations[loweredBundleAnnotations.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = loweredBundleAnnotations[index] ?? VliwBundleAnnotations.Empty;
            }

            return Array.AsReadOnly(copy);
        }
    }
}
