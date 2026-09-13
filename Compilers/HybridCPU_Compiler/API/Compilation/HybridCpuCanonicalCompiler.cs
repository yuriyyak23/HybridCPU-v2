using System.Collections.Generic;
using System.Diagnostics;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Contracts;
using HybridCPU.Compiler.Core.IR.Telemetry;
using HybridCPU.Compiler.Core.IR.Resources;

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
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            return CompileProgramCore(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver,
                controlFlowTargetReferences,
                metricsCollector: null,
                resourceShadowCollector: null);
        }

        /// <summary>
        /// Compiles through the unchanged canonical pipeline and returns optional observational
        /// Phase 00 metrics. Resource samples are telemetry and never code-selection inputs.
        /// </summary>
        public static HybridCpuCompilationMetricsResultV1 CompileProgramWithMetrics(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            CompilerScheduleMetricsRequestV1 metricsRequest,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            ArgumentNullException.ThrowIfNull(metricsRequest);
            if (!metricsRequest.Enabled)
            {
                HybridCpuCompiledProgram disabledProgram = CompileProgram(
                    virtualThreadId,
                    instructions,
                    labelDeclarations,
                    entryPointDeclarations,
                    frontendMode,
                    bundleAnnotations,
                    domainTag,
                    progressObserver,
                    controlFlowTargetReferences);
                return new HybridCpuCompilationMetricsResultV1(disabledProgram, null, null);
            }

            CompilerScheduleMetricsCollectorV1 metricsCollector =
                CompilerScheduleMetricsCollectorV1.CreateForInput(
                    virtualThreadId,
                    instructions,
                    metricsRequest);
            long startTimestamp = Stopwatch.GetTimestamp();
            long startManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false);
            HybridCpuCompiledProgram compiledProgram = CompileProgramCore(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver,
                controlFlowTargetReferences,
                metricsCollector,
                resourceShadowCollector: null);
            CompilerCompileResourceTelemetryV1 resourceTelemetry =
                CompilerCompileResourceTelemetryV1.Measure(startTimestamp, startManagedMemoryBytes);
            return new HybridCpuCompilationMetricsResultV1(
                compiledProgram,
                metricsCollector.Complete(compiledProgram),
                resourceTelemetry);
        }

        /// <summary>
        /// Runs the existing compiler decision path with a default-off Phase 01 resource shadow.
        /// The shadow report is evidence only and cannot select scheduling or placement decisions.
        /// </summary>
        public static HybridCpuCompilationResourceShadowResultV1 CompileProgramWithResourceShadow(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            var collector = new CompilerResourceShadowCollectorV1();
            HybridCpuCompiledProgram compiledProgram = CompileProgramCore(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver,
                controlFlowTargetReferences,
                metricsCollector: null,
                resourceShadowCollector: collector);
            return new HybridCpuCompilationResourceShadowResultV1(
                compiledProgram,
                collector.Complete());
        }

        /// <summary>
        /// Explicit default-off Phase 02 A/B entry point. The search is bounded only by versioned
        /// counters and reuses the existing structural checker and exact placement search.
        /// </summary>
        public static HybridCpuCompiledProgram CompileProgramWithJointCycleComposition(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            HybridCpuCycleSearchOptionsV1? searchOptions = null,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            return CompileProgramCore(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver,
                controlFlowTargetReferences,
                metricsCollector: null,
                resourceShadowCollector: null,
                useJointCycleComposition: true,
                jointCycleSearchOptions: searchOptions ?? HybridCpuCycleSearchOptionsV1.Default);
        }

        /// <summary>
        /// Compiles through the exact default path, then observes the immutable Phase 03
        /// topology against the completed schedule. The report cannot select code or alter
        /// emitted artifacts and does not represent runtime legality.
        /// </summary>
        public static HybridCpuCompilationTopologyShadowResultV1 CompileProgramWithTopologyShadow(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            HybridCpuMachineTopologyV1? topology = null,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            HybridCpuCompiledProgram compiledProgram = CompileProgram(
                virtualThreadId, instructions, labelDeclarations, entryPointDeclarations,
                frontendMode, bundleAnnotations, domainTag, progressObserver,
                controlFlowTargetReferences);
            var model = new HybridCpuTopologyResourceModelV1(topology);
            CompilerTopologyShadowReportV1 report =
                CompilerTopologyShadowEvaluatorV1.Evaluate(compiledProgram.ProgramSchedule, model);
            return new HybridCpuCompilationTopologyShadowResultV1(compiledProgram, report);
        }

        private static HybridCpuCompiledProgram CompileProgramCore(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations,
            NativeFrontendMode frontendMode,
            IrBundleAnnotations? bundleAnnotations,
            ulong domainTag,
            Action<string, string>? progressObserver,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences,
            CompilerScheduleMetricsCollectorV1? metricsCollector,
            CompilerResourceShadowCollectorV1? resourceShadowCollector,
            bool useJointCycleComposition = false,
            HybridCpuCycleSearchOptionsV1? jointCycleSearchOptions = null)
        {
            ValidateFrontendMode(frontendMode);

            progressObserver?.Invoke("IrBuildStarting", $"Building IR for {instructions.Length} instruction(s).");
            var builder = new HybridCpuIrBuilder();
            var scheduler = new HybridCpuLocalListScheduler
            {
                MetricsCollector = metricsCollector,
                ResourceShadowCollector = resourceShadowCollector,
                UseJointCycleComposition = useJointCycleComposition,
                JointCycleSearchOptions = jointCycleSearchOptions ?? HybridCpuCycleSearchOptionsV1.Default
            };
            var fallbackScheduler = new HybridCpuProgramOrderLocalScheduler
            {
                ResourceShadowCollector = resourceShadowCollector
            };
            var bundler = new HybridCpuBundleFormer
            {
                MetricsCollector = metricsCollector,
                ResourceShadowCollector = resourceShadowCollector
            };
            var admissionBuilder = new HybridCpuBundleBuilder();
            var lowerer = new HybridCpuBundleLowerer();
            var serializer = new HybridCpuBundleSerializer();

            IrProgram program = builder.BuildProgram(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                bundleAnnotations: bundleAnnotations,
                domainTag: domainTag,
                controlFlowTargetReferences: controlFlowTargetReferences);
            IrFrontendAdapterResultV1 frontendBoundary = CanonicalIrFrontendBoundaryV1.Validate(program);
            if (frontendBoundary.Status != IrFrontendAdapterStatus.Success)
            {
                throw new NotSupportedException(frontendBoundary.Diagnostics[0].Message);
            }
            CompilerCapabilityValidationResult capabilityBoundary = CompilerCapabilityValidator.ValidateRequired(
                program.Contract.RequiredCapabilities,
                CompilerCrossLayerSchemaCatalogV1.NativeV1);
            if (!capabilityBoundary.IsSatisfied)
            {
                throw new NotSupportedException(capabilityBoundary.Reason);
            }
            metricsCollector?.CaptureProgramInput(program);
            progressObserver?.Invoke("IrBuild", $"IR contains {program.Instructions.Count} instruction(s) across {program.BasicBlocks.Count} basic block(s).");

            progressObserver?.Invoke("ScheduleStarting", $"Scheduling {program.BasicBlocks.Count} basic block(s).");
            IrProgramSchedule programSchedule;
            if (ShouldUseProgramOrderSchedulerFallback(program))
            {
                metricsCollector?.RecordSchedulerCapHit(GetProgramOrderSchedulingReason(program));
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
            loweredBundles = HybridCpuControlFlowRelocationResolver.ApplyRelocations(bundleLayout, loweredBundles);
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
                HybridCpuCompilerContract.Version,
                admissibilityAgreement: agreement,
                loweredBundleAnnotations: loweredBundleAnnotations);
        }

        private static void ValidateFrontendMode(NativeFrontendMode frontendMode)
        {
            if (frontendMode != NativeFrontendMode.NativeVLIW)
            {
                throw new ArgumentOutOfRangeException(nameof(frontendMode), frontendMode, "Unknown frontend mode.");
            }
        }

#if HYBRIDCPU_RUNTIME_EMISSION
        /// <summary>
        /// Compiles and emits the VLIW instruction stream to main memory at the specified base address.
        /// </summary>
        public static HybridCpuCompiledProgram CompileProgram(
            byte virtualThreadId,
            ReadOnlySpan<HybridCpuInstructionWord> instructions,
            ulong baseAddress,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            NativeFrontendMode frontendMode = NativeFrontendMode.NativeVLIW,
            IrBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0,
            Action<string, string>? progressObserver = null,
            IReadOnlyList<IrControlFlowTargetReference>? controlFlowTargetReferences = null)
        {
            HybridCpuCompiledProgram compiledProgram = CompileProgram(
                virtualThreadId,
                instructions,
                labelDeclarations,
                entryPointDeclarations,
                frontendMode,
                bundleAnnotations,
                domainTag,
                progressObserver,
                controlFlowTargetReferences);
            return EmitProgram(compiledProgram, baseAddress, progressObserver);
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
            PublishRuntimeTransportBundleAnnotations(compiledProgram, baseAddress);
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

        private static void PublishRuntimeTransportBundleAnnotations(
            HybridCpuCompiledProgram compiledProgram,
            ulong baseAddress)
        {
            for (int bundleIndex = 0; bundleIndex < compiledProgram.LoweredBundleAnnotations.Count; bundleIndex++)
            {
                IrBundleAnnotations annotations = compiledProgram.LoweredBundleAnnotations[bundleIndex];
                if (!HasRuntimeTransportSideband(annotations))
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

        private static bool HasRuntimeTransportSideband(IrBundleAnnotations annotations)
        {
            for (int slotIndex = 0; slotIndex < annotations.Count; slotIndex++)
            {
                if (!annotations.TryGetInstructionSlotMetadata(
                        slotIndex,
                        out IrInstructionSlotMetadata metadata))
                {
                    continue;
                }

                // Annotation carriers are transport-only.  A non-default slot
                // record can carry placement/VT facts as well as one of the
                // specialized sidebands below; it is not compiler authority.
                // Do not publish a carrier for an all-default (NOP-only) bundle.
                if (metadata != IrInstructionSlotMetadata.Default)
                {
                    return true;
                }
            }

            return false;
        }
#endif

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

        private static string GetProgramOrderSchedulingReason(IrProgram program)
        {
            if (program.Instructions.Count > MaxLocalListSchedulingInstructionsPerProgram)
            {
                return "SchedulerProgramInstructionCap";
            }

            return "SchedulerBasicBlockInstructionCap";
        }
    }
}
