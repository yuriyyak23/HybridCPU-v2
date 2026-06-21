using System;
using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core
{
    public delegate void MatrixTileFullPipelineBeforeRetireCallback(
        ref Processor.CPU_Core core,
        MatrixTileFullPipelineStepEvidence step,
        MatrixTileExecutionCaptureRecord capture);

    public delegate void MatrixTileFullPipelineBeforeExecuteCallback(
        ref Processor.CPU_Core core,
        MatrixTileFullPipelineStepEvidence step);

    public delegate void MatrixTileFullPipelineAfterRetireCallback(
        ref Processor.CPU_Core core,
        MatrixTileFullPipelineStepEvidence step,
        MatrixTileExecutionCaptureRecord capture,
        MatrixTileRetireOutcome retireOutcome);

    public delegate MatrixTileExecutionCaptureRecord MatrixTileFullPipelineCaptureMutator(
        MatrixTileFullPipelineStepEvidence step,
        MatrixTileExecutionCaptureRecord capture);

    public sealed class MatrixTileFullPipelineHarnessOptions
    {
        public ulong BaseBundleAddress { get; init; } = 0xA_0000UL;

        public ulong BaseBundleSerial { get; init; } = 500UL;

        public int OwnerVirtualThreadId { get; init; }

        public bool ReplayAfterSuccessfulRetire { get; init; } = true;

        public MatrixTileFullPipelineBeforeExecuteCallback? BeforeExecute { get; init; }

        public MatrixTileFullPipelineBeforeRetireCallback? BeforeRetire { get; init; }

        public MatrixTileFullPipelineAfterRetireCallback? AfterRetire { get; init; }

        public MatrixTileFullPipelineCaptureMutator? MutateCaptureBeforeRetire { get; init; }
    }

    public sealed class MatrixTileFullPipelineReport
    {
        public MatrixTileFullPipelineReport(IReadOnlyList<MatrixTileFullPipelineStepEvidence> steps)
        {
            Steps = steps;
        }

        public IReadOnlyList<MatrixTileFullPipelineStepEvidence> Steps { get; }

        public int RuntimeInstructionCount
        {
            get
            {
                int count = 0;
                foreach (MatrixTileFullPipelineStepEvidence step in Steps)
                {
                    if (step.ExecuteObserved)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int RetirePublicationCount
        {
            get
            {
                int count = 0;
                foreach (MatrixTileFullPipelineStepEvidence step in Steps)
                {
                    if (step.RetireOutcome is { IsSuccess: true })
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ReplayRoundTripCount
        {
            get
            {
                int count = 0;
                foreach (MatrixTileFullPipelineStepEvidence step in Steps)
                {
                    if (step.RollbackOutcome.HasValue && step.ReplayOutcome.HasValue)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FailClosedRejectionCount
        {
            get
            {
                int count = 0;
                foreach (MatrixTileFullPipelineStepEvidence step in Steps)
                {
                    if (step.FailClosedRejected)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public sealed class MatrixTileFullPipelineStepEvidence
    {
        public int BundleIndex { get; init; }

        public int SlotIndex { get; init; }

        public int ScheduledLaneIndex { get; set; } = -1;

        public ulong BundleAddress { get; init; }

        public ulong BundleSerial { get; init; }

        public InstructionsEnum Opcode { get; init; }

        public bool FetchObserved { get; init; }

        public bool DecodeObserved { get; set; }

        public bool ScheduleObserved { get; set; }

        public bool ExecuteObserved { get; set; }

        public bool RetireObserved { get; set; }

        public bool ReplayRollbackObserved { get; set; }

        public InstructionSlotMetadata SourceSlotMetadata { get; init; }

        public InstructionSlotMetadata DecodedSlotMetadata { get; set; }

        public MatrixTileIrProjectionFaultKind ProjectionFaultKind { get; set; }

        public MatrixTileProjectedOperationKind OperationKind { get; set; }

        public MatrixTileRuntimeResourceClass RuntimeResourceClass { get; set; }

        public SlotClass RequiredSlotClass { get; set; }

        public byte SchedulerLaneMask { get; set; }

        public MatrixTileMicroOpDependencyMetadata DependencyMetadata { get; set; }

        public bool SidebandPreserved =>
            Nullable.Equals(SourceSlotMetadata.MatrixTileNumericPolicy, DecodedSlotMetadata.MatrixTileNumericPolicy) &&
            Nullable.Equals(SourceSlotMetadata.MatrixTileLayoutPolicy, DecodedSlotMetadata.MatrixTileLayoutPolicy);

        public MatrixTileExecutionCaptureRecord? Capture { get; set; }

        public MatrixTileRetireOutcome? RetireOutcome { get; set; }

        public MatrixTileRollbackOutcome? RollbackOutcome { get; set; }

        public MatrixTileRetireOutcome? ReplayOutcome { get; set; }

        public string? FailureMessage { get; set; }

        public bool FailClosedRejected => FailureMessage is not null;
    }

    public static class MatrixTileFullPipelineHarness
    {
        public static MatrixTileFullPipelineReport RunCompilerLoweredBundlesForTesting(
            ref Processor.CPU_Core core,
            IReadOnlyList<VLIW_Bundle> loweredBundles,
            IReadOnlyList<VliwBundleAnnotations> loweredBundleAnnotations,
            MatrixTileFullPipelineHarnessOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(loweredBundles);
            ArgumentNullException.ThrowIfNull(loweredBundleAnnotations);
            if (loweredBundles.Count != loweredBundleAnnotations.Count)
            {
                throw new ArgumentException(
                    "Lowered bundle annotation count must match lowered bundle count.",
                    nameof(loweredBundleAnnotations));
            }

            options ??= new MatrixTileFullPipelineHarnessOptions();
            var decoder = new VliwDecoderV4();
            var scheduler = new MicroOpScheduler
            {
                TypedSlotEnabled = true
            };
            var steps = new List<MatrixTileFullPipelineStepEvidence>();

            for (int bundleIndex = 0; bundleIndex < loweredBundles.Count; bundleIndex++)
            {
                ulong bundleAddress = checked(options.BaseBundleAddress + ((ulong)bundleIndex * 0x20UL));
                ulong bundleSerial = checked(options.BaseBundleSerial + (ulong)bundleIndex);
                VLIW_Bundle bundle = loweredBundles[bundleIndex];
                VliwBundleAnnotations annotations = loweredBundleAnnotations[bundleIndex] ?? VliwBundleAnnotations.Empty;
                VLIW_Instruction[] rawSlots = FetchBundleSlots(bundle);

                DecodedInstructionBundle decodedBundle = decoder.DecodeInstructionBundle(
                    rawSlots,
                    annotations,
                    bundleAddress,
                    bundleSerial);
                MicroOp?[] carrierBundle =
                    DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                        rawSlots,
                        decodedBundle);
                MicroOp[] scheduledBundle = scheduler.PackBundleIntraCoreSmt(
                    carrierBundle,
                    options.OwnerVirtualThreadId,
                    checked((int)core.CoreID),
                    eligibleVirtualThreadMask: 0);

                for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
                {
                    InstructionsEnum opcode = (InstructionsEnum)rawSlots[slotIndex].OpCode;
                    if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode))
                    {
                        continue;
                    }

                    DecodedInstruction decodedSlot = decodedBundle.GetDecodedSlot(slotIndex);
                    annotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata sourceMetadata);
                    MatrixTileFullPipelineStepEvidence step = new()
                    {
                        BundleIndex = bundleIndex,
                        SlotIndex = slotIndex,
                        BundleAddress = bundleAddress,
                        BundleSerial = bundleSerial,
                        Opcode = opcode,
                        FetchObserved = true,
                        DecodeObserved = decodedSlot.IsOccupied,
                        SourceSlotMetadata = sourceMetadata == default
                            ? InstructionSlotMetadata.Default
                            : sourceMetadata,
                        DecodedSlotMetadata = decodedSlot.SlotMetadata
                    };
                    steps.Add(step);

                    if (!decodedSlot.IsOccupied)
                    {
                        step.FailureMessage = "MatrixTile opcode decoded as an empty slot.";
                        continue;
                    }

                    if (!ValidateCompilerMatrixTileSidebandShape(
                            opcode,
                            decodedSlot.SlotMetadata,
                            out string? sidebandFailure))
                    {
                        step.FailureMessage = sidebandFailure;
                        continue;
                    }

                    InstructionIR instruction = decodedSlot.RequireInstruction();
                    if (instruction.MatrixTileProjection is not { } projection)
                    {
                        step.FailureMessage = "MatrixTile opcode did not publish a decoded MatrixTile projection.";
                        continue;
                    }

                    step.ProjectionFaultKind = projection.FaultKind;
                    step.OperationKind = projection.OperationKind;
                    if (projection.FaultKind != MatrixTileIrProjectionFaultKind.None ||
                        !projection.IsRuntimeLegal)
                    {
                        step.FailureMessage =
                            $"Decoded MatrixTile projection failed closed before execution: {projection.FaultKind}.";
                        continue;
                    }

                    if (carrierBundle[slotIndex] is not MatrixTileMicroOp matrixTileMicroOp)
                    {
                        step.FailureMessage =
                            $"Decoded MatrixTile slot materialized as {carrierBundle[slotIndex]?.GetType().Name ?? "null"} instead of MatrixTileMicroOp.";
                        continue;
                    }

                    int scheduledLane = FindScheduledLane(scheduledBundle, matrixTileMicroOp);
                    step.ScheduledLaneIndex = scheduledLane;
                    step.ScheduleObserved = scheduledLane >= 0;
                    step.RuntimeResourceClass = matrixTileMicroOp.RuntimeResourceClass;
                    step.RequiredSlotClass = matrixTileMicroOp.Placement.RequiredSlotClass;
                    step.SchedulerLaneMask = matrixTileMicroOp.SchedulerLaneMask;
                    step.DependencyMetadata = matrixTileMicroOp.DependencyMetadata;
                    if (scheduledLane < 0)
                    {
                        step.FailureMessage = "Scheduler did not preserve the decoded MatrixTile carrier.";
                        continue;
                    }

                    try
                    {
                        options.BeforeExecute?.Invoke(ref core, step);
                        if (!matrixTileMicroOp.Execute(ref core))
                        {
                            step.FailureMessage = "MatrixTile execute returned false.";
                            continue;
                        }

                        step.ExecuteObserved = true;
                        MatrixTileExecutionCaptureRecord capture =
                            matrixTileMicroOp.LastExecutionCapture
                            ?? throw new InvalidOperationException(
                                $"{opcode} did not publish a Phase09 execution capture.");
                        step.Capture = capture;

                        options.BeforeRetire?.Invoke(ref core, step, capture);
                        MatrixTileExecutionCaptureRecord retireCapture =
                            options.MutateCaptureBeforeRetire?.Invoke(step, capture) ?? capture;
                        MatrixTileRetireOutcome retireOutcome =
                            matrixTileMicroOp.RetireCapturedResult(ref core, retireCapture);
                        step.RetireObserved = true;
                        step.RetireOutcome = retireOutcome;
                        options.AfterRetire?.Invoke(ref core, step, retireCapture, retireOutcome);

                        if (options.ReplayAfterSuccessfulRetire && retireOutcome.IsSuccess)
                        {
                            MatrixTileReplayRollbackJournal journal =
                                matrixTileMicroOp.LastReplayRollbackJournal
                                ?? throw new InvalidOperationException(
                                    $"{opcode} did not publish a retire-owned replay/rollback journal.");
                            step.RollbackOutcome =
                                matrixTileMicroOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
                            step.ReplayOutcome =
                                matrixTileMicroOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
                            step.ReplayRollbackObserved = true;
                        }
                    }
                    catch (MatrixTileRetireValidationException ex)
                    {
                        step.FailureMessage = ex.Message;
                    }
                    catch (MatrixTileRetireFaultException ex)
                    {
                        step.FailureMessage = ex.Message;
                    }
                    catch (DecodeProjectionFaultException ex)
                    {
                        step.FailureMessage = ex.Message;
                    }
                }
            }

            return new MatrixTileFullPipelineReport(steps);
        }

        private static VLIW_Instruction[] FetchBundleSlots(VLIW_Bundle bundle)
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < rawSlots.Length; slotIndex++)
            {
                rawSlots[slotIndex] = bundle.GetInstruction(slotIndex);
            }

            return rawSlots;
        }

        private static int FindScheduledLane(
            IReadOnlyList<MicroOp> scheduledBundle,
            MatrixTileMicroOp matrixTileMicroOp)
        {
            for (int laneIndex = 0; laneIndex < scheduledBundle.Count; laneIndex++)
            {
                if (ReferenceEquals(scheduledBundle[laneIndex], matrixTileMicroOp))
                {
                    return laneIndex;
                }
            }

            return -1;
        }

        private static bool ValidateCompilerMatrixTileSidebandShape(
            InstructionsEnum opcode,
            InstructionSlotMetadata metadata,
            out string? failureMessage)
        {
            failureMessage = null;
            bool hasNumeric = metadata.MatrixTileNumericPolicy.HasValue;
            bool hasLayout = metadata.MatrixTileLayoutPolicy.HasValue;
            switch (opcode)
            {
                case InstructionsEnum.MTILE_LOAD:
                case InstructionsEnum.MTILE_STORE:
                    if (hasNumeric || hasLayout)
                    {
                        failureMessage =
                            $"{opcode} compiler-lowered transport carried compute numeric/layout sideband authority.";
                        return false;
                    }

                    return true;

                case InstructionsEnum.MTILE_MACC:
                    if (!hasNumeric || !hasLayout)
                    {
                        failureMessage =
                            "MTILE_MACC compiler-lowered transport did not carry explicit runtime-owned numeric and layout sidebands.";
                        return false;
                    }

                    return true;

                case InstructionsEnum.MTRANSPOSE:
                    if (hasNumeric || !hasLayout)
                    {
                        failureMessage =
                            "MTRANSPOSE compiler-lowered transport must carry layout-only sideband authority.";
                        return false;
                    }

                    return true;

                default:
                    failureMessage = $"{opcode} is not a MatrixTile compiler-positive opcode.";
                    return false;
            }
        }
    }
}
