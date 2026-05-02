using System.Collections.Generic;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace HybridCPU.Compiler.Core
{
    /// <summary>
    /// Stateless canonical compiler: IR construction → scheduling → bundling → lowering → serialization.
    /// This is the single production entry point for all compilation paths.
    /// </summary>
    public static class HybridCpuCanonicalCompiler
    {
        private const int MaxLocalListSchedulingInstructionsPerProgram = 192;
        private const int MaxLocalListSchedulingInstructionsPerBlock = 48;

        /// <summary>
        /// Compiles a VLIW instruction stream through the full canonical pipeline.
        /// </summary>
        public static HybridCpuCompiledProgram CompileProgram(
            byte virtualThreadId,
            ReadOnlySpan<VLIW_Instruction> instructions,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            FrontendMode frontendMode = FrontendMode.NativeVLIW,
            VliwBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null)
        {
            ValidateFrontendMode(frontendMode);

            progressObserver?.Invoke("IrBuildStarting", $"Building IR for {instructions.Length} instruction(s).");
            var builder = new HybridCpuIrBuilder();
            var scheduler = new HybridCpuLocalListScheduler();
            var fallbackScheduler = new HybridCpuProgramOrderLocalScheduler();
            var bundler = new HybridCpuBundleFormer();
            var admissionBuilder = new HybridCpuBundleBuilder();
            var lowerer = new HybridCpuBundleLowerer();
            var serializer = new HybridCpuBundleSerializer();

            IrProgram program = builder.BuildProgram(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                bundleAnnotations: bundleAnnotations,
                domainTag: domainTag);
            progressObserver?.Invoke("IrBuild", $"IR contains {program.Instructions.Count} instruction(s) across {program.BasicBlocks.Count} basic block(s).");

            progressObserver?.Invoke("ScheduleStarting", $"Scheduling {program.BasicBlocks.Count} basic block(s).");
            IrProgramSchedule programSchedule;
            if (ShouldUseProgramOrderSchedulerFallback(program))
            {
                progressObserver?.Invoke(
                    "ScheduleFallback",
                    $"Program-order fallback engaged for {program.Instructions.Count} instruction(s); at least one block exceeds the bounded local-list scheduling budget.");
                programSchedule = fallbackScheduler.ScheduleProgram(program);
            }
            else
            {
                programSchedule = scheduler.ScheduleProgram(program);
            }

            progressObserver?.Invoke("Schedule", $"Schedule contains {programSchedule.BlockSchedules.Count} block schedule(s) and {CountCycleGroups(programSchedule)} cycle group(s).");

            progressObserver?.Invoke("BundleMaterializationStarting", "Materializing legal bundle placements.");
            IrProgramBundlingResult bundleLayout = bundler.BundleProgram(programSchedule);
            progressObserver?.Invoke("BundleMaterialization", $"Bundle layout contains {CountBundles(bundleLayout)} bundle(s).");

            IrAdmissibilityAgreement agreement = admissionBuilder.BuildAgreement(bundleLayout);
            progressObserver?.Invoke("AgreementSummary", FormatAgreementSummary(agreement));

            progressObserver?.Invoke("LoweringStarting", "Lowering IR bundles to backend VLIW bundles.");
            var loweredBundles = lowerer.LowerProgram(bundleLayout);
            var loweredBundleAnnotations = lowerer.EmitAnnotationsForProgram(bundleLayout);
            progressObserver?.Invoke("Lowering", $"Lowered {loweredBundles.Count} backend bundle(s).");

            progressObserver?.Invoke("SerializationStarting", "Serializing backend bundles into a fetch-ready image.");
            byte[] programImage = serializer.SerializeProgram(loweredBundles);
            progressObserver?.Invoke("Serialization", $"Serialized {programImage.Length} byte(s) of bundle image.");

            return new HybridCpuCompiledProgram(
                programSchedule,
                bundleLayout,
                loweredBundles,
                programImage,
                CompilerContract.Version,
                admissibilityAgreement: agreement,
                loweredBundleAnnotations: loweredBundleAnnotations);
        }

        /// <summary>
        /// Compiles and emits the VLIW instruction stream to main memory at the specified base address.
        /// </summary>
        public static HybridCpuCompiledProgram CompileProgram(
            byte virtualThreadId,
            ReadOnlySpan<VLIW_Instruction> instructions,
            ulong baseAddress,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            FrontendMode frontendMode = FrontendMode.NativeVLIW,
            VliwBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null)
        {
            HybridCpuCompiledProgram compiledProgram = CompileProgram(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver);
            return EmitProgram(compiledProgram, baseAddress, progressObserver);
        }

        private static void ValidateFrontendMode(FrontendMode frontendMode)
        {
            switch (frontendMode)
            {
                case FrontendMode.NativeVLIW:
                    return;
                // ISA_V4_AUDIT: FrontendMode.RvCompat removed — RISC-V compat path eliminated in Phase 01.
                // All compilation paths now go through the native VLIW frontend only.
                default:
                    throw new ArgumentOutOfRangeException(nameof(frontendMode), frontendMode, "Unknown frontend mode.");
            }
        }

        /// <summary>
        /// Emits a previously compiled program to main memory at the specified base address.
        /// </summary>
        public static HybridCpuCompiledProgram EmitProgram(
            HybridCpuCompiledProgram compiledProgram,
            ulong baseAddress,
            Action<string, string>? progressObserver = null)
        {
            ArgumentNullException.ThrowIfNull(compiledProgram);
            compiledProgram.ValidateRuntimeContractCompatibility($"{nameof(HybridCpuCanonicalCompiler)}.{nameof(EmitProgram)}");
            ValidateBundleAlignedBaseAddress(baseAddress);

            progressObserver?.Invoke("MemoryWriteStarting", $"Writing {compiledProgram.ProgramImage.Length} byte(s) to emitted program memory at IOVA 0x{baseAddress:X}.");
            Processor.MainMemory.WriteToPosition(compiledProgram.ProgramImage, baseAddress);
            PublishDescriptorBackedBundleAnnotations(compiledProgram, baseAddress);
            progressObserver?.Invoke("MemoryWrite", $"Bundle image write completed at IOVA 0x{baseAddress:X}.");

            progressObserver?.Invoke("FetchStateInvalidationStarting", $"Invalidating fetch state for {compiledProgram.BundleCount} bundle(s).");
            InvalidateEmittedFetchState(baseAddress, compiledProgram.BundleCount);
            progressObserver?.Invoke("FetchStateInvalidation", $"Fetch state invalidation completed for {compiledProgram.BundleCount} bundle(s).");
            return compiledProgram.WithEmissionBaseAddress(baseAddress);
        }

        private static void ValidateBundleAlignedBaseAddress(ulong baseAddress)
        {
            if ((baseAddress % HybridCpuBundleSerializer.BundleSizeBytes) != 0)
            {
                throw new ArgumentException($"Emission base address must be aligned to the {HybridCpuBundleSerializer.BundleSizeBytes}-byte Stage 6 bundle size.", nameof(baseAddress));
            }
        }

        private static void InvalidateEmittedFetchState(ulong baseAddress, int bundleCount)
        {
            for (int bundleIndex = 0; bundleIndex < bundleCount; bundleIndex++)
            {
                ulong bundleAddress = baseAddress + ((ulong)bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes);
                for (int coreIndex = 0; coreIndex < Processor.CPU_Cores.Length; coreIndex++)
                {
                    Processor.CPU_Cores[coreIndex].InvalidateVliwFetchState(bundleAddress);
                }
            }
        }

        private static void PublishDescriptorBackedBundleAnnotations(
            HybridCpuCompiledProgram compiledProgram,
            ulong baseAddress)
        {
            for (int bundleIndex = 0; bundleIndex < compiledProgram.LoweredBundleAnnotations.Count; bundleIndex++)
            {
                VliwBundleAnnotations annotations = compiledProgram.LoweredBundleAnnotations[bundleIndex];
                if (!HasDescriptorSideband(annotations))
                {
                    continue;
                }

                ulong bundleAddress =
                    baseAddress + ((ulong)bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes);
                Processor.MainMemory.PublishVliwBundleAnnotations(
                    bundleAddress,
                    annotations);
            }
        }

        private static bool HasDescriptorSideband(VliwBundleAnnotations annotations)
        {
            for (int slotIndex = 0; slotIndex < annotations.Count; slotIndex++)
            {
                if (!annotations.TryGetInstructionSlotMetadata(
                        slotIndex,
                        out InstructionSlotMetadata metadata))
                {
                    continue;
                }

                if (metadata.DmaStreamComputeDescriptor is not null ||
                    metadata.AcceleratorCommandDescriptor is not null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountCycleGroups(IrProgramSchedule programSchedule)
        {
            int totalCycleGroups = 0;
            for (int blockIndex = 0; blockIndex < programSchedule.BlockSchedules.Count; blockIndex++)
            {
                totalCycleGroups += programSchedule.BlockSchedules[blockIndex].CycleGroups.Count;
            }

            return totalCycleGroups;
        }

        private static int CountBundles(IrProgramBundlingResult bundleLayout)
        {
            int totalBundles = 0;
            for (int blockIndex = 0; blockIndex < bundleLayout.BlockResults.Count; blockIndex++)
            {
                totalBundles += bundleLayout.BlockResults[blockIndex].Bundles.Count;
            }

            return totalBundles;
        }

        private static string FormatAgreementSummary(IrAdmissibilityAgreement agreement)
        {
            return
                $"Agreement summary: bundles={agreement.TotalBundleCount}, " +
                $"structurally-admissible={agreement.AdmissibleBundleCount}, " +
                $"agreement failures={agreement.StructuralAgreementFailureCount}, " +
                $"typed-slot valid={agreement.TypedSlotValidBundleCount}/{agreement.TotalBundleCount}, " +
                $"typed-slot invalid={agreement.TypedSlotInvalidBundleCount}, " +
                $"safety-mask conflicts={agreement.SafetyMaskConflictCount}.";
        }

        private static bool ShouldUseProgramOrderSchedulerFallback(IrProgram program)
        {
            if (program.Instructions.Count > MaxLocalListSchedulingInstructionsPerProgram)
            {
                return true;
            }

            for (int blockIndex = 0; blockIndex < program.BasicBlocks.Count; blockIndex++)
            {
                if (program.BasicBlocks[blockIndex].Instructions.Count > MaxLocalListSchedulingInstructionsPerBlock)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
