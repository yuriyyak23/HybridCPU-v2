using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum MatrixTileCaptureLifecycle : byte
    {
        None = 0,
        Captured = 1,
        Retired = 2,
        FaultRetired = 3,
        Cancelled = 4,
        RolledBack = 5,
        Replayed = 6,
        FaultReplayed = 7,
        ReplayDiscarded = 8,
    }

    public readonly record struct MatrixTileMicroOpDependencyMetadata(
        MatrixTileProjectedOperationKind OperationKind,
        MatrixTileCanonicalDescriptorAbi TileDescriptor,
        MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor,
        MatrixTileCanonicalDescriptorAbi ResultTileDescriptor,
        bool HasTileMemoryDependencyMetadata,
        bool HasTileRegisterDependencyMetadata,
        bool HasAccumulatorDependencyMetadata,
        bool HasTransposePolicyDependencyMetadata,
        bool ReadsTileState,
        bool WritesTileState,
        bool ReadsAccumulator,
        bool WritesAccumulator,
        ushort SourceTileId,
        ushort SecondaryTileId,
        ushort DestinationTileId);

    public abstract class MatrixTileMicroOp : MicroOp
    {
        private MatrixTileExecutionCaptureRecord? _lastExecutionCapture;
        private MatrixTileCaptureIdentity _activeCaptureIdentity;
        private MatrixTileRetireOutcome? _lastRetireOutcome;
        private MatrixTileReplayRollbackJournal? _lastReplayRollbackJournal;
        private MatrixTileCaptureLifecycle _captureLifecycle;

        protected MatrixTileMicroOp(
            MatrixTileMaterializedInstruction materializedInstruction,
            MatrixTileProjectedOperationKind expectedOperationKind)
        {
            if (materializedInstruction.OperationKind != expectedOperationKind ||
                !materializedInstruction.IsRuntimeLegal)
            {
                throw new DecodeProjectionFaultException(
                    $"MTILE Phase08 MicroOp factory rejected {materializedInstruction.Opcode} because the Phase07 materialized runtime object is not a legal {expectedOperationKind} carrier.");
            }

            MaterializedInstruction = materializedInstruction;
            Projection = materializedInstruction.Projection;
            OperationKind = expectedOperationKind;
            RuntimeResourceClass = MatrixTileResourceContour.Classify(
                materializedInstruction.Opcode);
            DependencyMetadata = CreateDependencyMetadata(Projection);
            OpCode = (uint)materializedInstruction.Opcode;
            PredicateMask = Projection.SourcePayload.PredicateMask;
            SetClassFlexiblePlacement(
                MatrixTileResourceContour.ResolveSlotClass(RuntimeResourceClass));
        }

        public MatrixTileMaterializedInstruction MaterializedInstruction { get; }

        public MatrixTileInstructionIrProjection Projection { get; }

        public MatrixTileProjectedOperationKind OperationKind { get; }

        public MatrixTileRuntimeResourceClass RuntimeResourceClass { get; }

        public MatrixTileMicroOpDependencyMetadata DependencyMetadata { get; }

        public MatrixTileCanonicalDescriptorAbi TileDescriptor => MaterializedInstruction.TileDescriptor;

        public MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor => MaterializedInstruction.SecondaryTileDescriptor;

        public MatrixTileCanonicalDescriptorAbi ResultTileDescriptor => MaterializedInstruction.ResultTileDescriptor;

        public MatrixTileMemoryShapeContract? MemoryContract => Projection.MemoryContract;

        public MatrixTileMemoryShapeValidationResult? MemoryValidation => Projection.MemoryValidation;

        public MatrixTileMaccSemanticContract? MaccContract => Projection.MaccContract;

        public MatrixTileTransposeSemanticContract? TransposeContract => Projection.TransposeContract;

        public bool PublishesTypedTileMicroOp => true;

        public bool PublishesSchedulerLaneBinding => true;

        public bool PublishesIssueConstraints => true;

        public bool PublishesCaptureBarriers => true;

        public bool OpensExecution => true;

        public bool PublishesExecutionCaptureSemantics => true;

        public MatrixTileExecutionCaptureRecord? LastExecutionCapture => _lastExecutionCapture?.DeepClone();

        public MatrixTileRetireOutcome? LastRetireOutcome => _lastRetireOutcome;

        public MatrixTileReplayRollbackJournal? LastReplayRollbackJournal =>
            _lastReplayRollbackJournal;

        public MatrixTileCaptureLifecycle CaptureLifecycle => _captureLifecycle;

        public bool UsesFallbackPath => false;

        public byte SchedulerLaneMask => SlotClassLaneMap.GetLaneMask(Placement.RequiredSlotClass);

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public virtual void InitializeMetadata()
        {
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = BuildMemoryRanges(readsMemory: true);
            WriteMemoryRanges = BuildMemoryRanges(readsMemory: false);
            WritesRegister = false;
            IsControlFlow = false;
            Class = RuntimeResourceClass switch
            {
                MatrixTileRuntimeResourceClass.MatrixTileMemory => MicroOpClass.MatrixTileMemory,
                MatrixTileRuntimeResourceClass.MatrixTileCompute => MicroOpClass.MatrixTileCompute,
                _ => throw new DecodeProjectionFaultException(
                    $"{Projection.Mnemonic} has no MatrixTile runtime resource classification.")
            };
            InstructionClass = OperationKind is MatrixTileProjectedOperationKind.Load or MatrixTileProjectedOperationKind.Store
                ? InstructionClass.Memory
                : InstructionClass.ScalarAlu;
            SerializationClass = OperationKind == MatrixTileProjectedOperationKind.Store
                ? SerializationClass.MemoryOrdered
                : SerializationClass.Free;
            IsMemoryOp = OperationKind is MatrixTileProjectedOperationKind.Load or MatrixTileProjectedOperationKind.Store;
            HasSideEffects = true;
            Latency = GetPublishedLatency();
            ResourceMask = BuildResourceMask();
            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            EnsureNoUnretiredCapture();
            MatrixTileExecutionCaptureRecord capture = OperationKind switch
            {
                MatrixTileProjectedOperationKind.Load => CaptureLoad(ref core),
                MatrixTileProjectedOperationKind.Store => CaptureStore(ref core),
                MatrixTileProjectedOperationKind.Macc => CaptureMacc(ref core),
                MatrixTileProjectedOperationKind.Transpose => CaptureTranspose(ref core),
                _ => MatrixTileExecuteCaptureAbi.CaptureLoad(
                    Projection.Mnemonic,
                    OperationKind,
                    DependencyMetadata.SourceTileId,
                    DependencyMetadata.SecondaryTileId,
                    DependencyMetadata.DestinationTileId,
                    TileDescriptor,
                    SecondaryTileDescriptor,
                    ResultTileDescriptor,
                    memoryContract: null,
                    memoryValidation: null,
                    (_, _) => null)
            };

            ulong captureOrdinal = core.AllocateMatrixTileCaptureOrdinal();
            capture = MatrixTilePolicyBoundIdentityAbi.Bind(
                capture,
                MaterializedInstruction,
                DependencyMetadata,
                OwnerThreadId,
                memoryDomainId: OwnerThreadId,
                core.GetMatrixTileReplayInvalidationEpoch());
            _activeCaptureIdentity =
                MatrixTileRetirePublicationAbi.CreateCaptureIdentity(
                    core.CoreID,
                    OwnerThreadId,
                    OpCode,
                    OperationKind,
                    captureOrdinal,
                    capture);
            _lastExecutionCapture = capture with
            {
                CaptureIdentity = _activeCaptureIdentity
            };
            _lastRetireOutcome = null;
            _captureLifecycle = MatrixTileCaptureLifecycle.Captured;
            return true;
        }

        public MatrixTileRetireOutcome RetireCapturedResult(
            ref Processor.CPU_Core core,
            in MatrixTileExecutionCaptureRecord capture)
        {
            if (_captureLifecycle != MatrixTileCaptureLifecycle.Captured ||
                !_lastExecutionCapture.HasValue)
            {
                throw new MatrixTileRetireValidationException(
                    $"{Projection.Mnemonic} retire requires one live, uncancelled Phase09 capture.");
            }

            MatrixTileReplayRollbackJournal journal =
                MatrixTileReplayRollbackAbi.RetireWithCheckpoint(
                    ref core,
                    MaterializedInstruction,
                    capture,
                    _activeCaptureIdentity,
                    core.AllocateMatrixTileReplayCheckpointOrdinal());
            MatrixTileRetireOutcome outcome = journal.OriginalRetireOutcome;
            _lastReplayRollbackJournal = journal;

            _lastRetireOutcome = outcome;
            _lastExecutionCapture = null;
            _activeCaptureIdentity = default;
            _captureLifecycle = outcome.FaultRetired
                ? MatrixTileCaptureLifecycle.FaultRetired
                : MatrixTileCaptureLifecycle.Retired;
            return outcome;
        }

        public MatrixTileRollbackOutcome RollbackRetiredResult(
            ref Processor.CPU_Core core,
            MatrixTileReplayIdentity expectedIdentity)
        {
            MatrixTileReplayRollbackJournal journal = RequireReplayJournal();
            MatrixTileRollbackOutcome outcome =
                MatrixTileReplayRollbackAbi.Rollback(
                    ref core,
                    journal,
                    expectedIdentity);
            _captureLifecycle = MatrixTileCaptureLifecycle.RolledBack;
            return outcome;
        }

        public MatrixTileRetireOutcome ReplayRolledBackResult(
            ref Processor.CPU_Core core,
            MatrixTileReplayIdentity expectedIdentity)
        {
            MatrixTileReplayRollbackJournal journal = RequireReplayJournal();
            MatrixTileRetireOutcome outcome =
                MatrixTileReplayRollbackAbi.Replay(
                    ref core,
                    MaterializedInstruction,
                    journal,
                    expectedIdentity);
            _lastRetireOutcome = outcome;
            _captureLifecycle = outcome.FaultRetired
                ? MatrixTileCaptureLifecycle.FaultReplayed
                : MatrixTileCaptureLifecycle.Replayed;
            return outcome;
        }

        public void DiscardReplayRollbackAuthority(
            ref Processor.CPU_Core core,
            MatrixTileReplayIdentity expectedIdentity)
        {
            MatrixTileReplayRollbackJournal journal = RequireReplayJournal();
            MatrixTileReplayRollbackAbi.Discard(
                ref core,
                journal,
                expectedIdentity);
            _captureLifecycle = MatrixTileCaptureLifecycle.ReplayDiscarded;
        }

        public void CancelCapturedResult()
        {
            if (_captureLifecycle != MatrixTileCaptureLifecycle.Captured ||
                !_lastExecutionCapture.HasValue)
            {
                throw new MatrixTileRetireValidationException(
                    $"{Projection.Mnemonic} cancel requires one live Phase09 capture.");
            }

            _lastExecutionCapture = null;
            _activeCaptureIdentity = default;
            _lastRetireOutcome = null;
            _lastReplayRollbackJournal = null;
            _captureLifecycle = MatrixTileCaptureLifecycle.Cancelled;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (!_lastExecutionCapture.HasValue)
            {
                throw new MatrixTileRetireValidationException(
                    $"{Projection.Mnemonic} writeback reached retire without a Phase09 capture.");
            }

            MatrixTileExecutionCaptureRecord capture = _lastExecutionCapture.Value;
            MatrixTileRetireOutcome outcome =
                RetireCapturedResult(ref core, capture);
            if (outcome.FaultRetired)
            {
                throw new MatrixTileRetireFaultException(outcome);
            }
        }

        public override string GetDescription()
        {
            return $"{GetType().Name}: {Projection.Mnemonic}, Descriptor={TileDescriptor}, LaneMask=0x{SchedulerLaneMask:X2}";
        }

        private IReadOnlyList<(ulong Address, ulong Length)> BuildMemoryRanges(bool readsMemory)
        {
            bool matchesOperation = readsMemory
                ? OperationKind == MatrixTileProjectedOperationKind.Load
                : OperationKind == MatrixTileProjectedOperationKind.Store;
            if (!matchesOperation ||
                !MemoryValidation.HasValue ||
                !MemoryValidation.Value.IsValid)
            {
                return Array.Empty<(ulong Address, ulong Length)>();
            }

            MatrixTileMemoryShapeValidationResult validation = MemoryValidation.Value;
            return new[]
            {
                (validation.FirstByteAddress, validation.TotalByteFootprint)
            };
        }

        private MatrixTileExecutionCaptureRecord CaptureLoad(ref Processor.CPU_Core core)
        {
            MatrixTileStreamTransferSession transfer =
                MatrixTileStreamTransferAbi.BeginLoad(
                    ref core,
                    OwnerThreadId,
                    OpCode,
                    TileDescriptor.Rows);
            MatrixTileExecutionCaptureRecord capture =
                MatrixTileExecuteCaptureAbi.CaptureLoad(
                Projection.Mnemonic,
                OperationKind,
                DependencyMetadata.SourceTileId,
                DependencyMetadata.SecondaryTileId,
                DependencyMetadata.DestinationTileId,
                TileDescriptor,
                SecondaryTileDescriptor,
                ResultTileDescriptor,
                MemoryContract,
                MemoryValidation,
                transfer.ReadIngress);
            return capture with
            {
                StreamTransfer = transfer.Complete(capture.HasFault)
            };
        }

        private MatrixTileExecutionCaptureRecord CaptureStore(ref Processor.CPU_Core core)
        {
            MatrixTileTileImage sourceSnapshot = CaptureTileSnapshotOrDefault(
                ref core,
                DependencyMetadata.SourceTileId,
                TileDescriptor);
            MatrixTileStreamTransferSession transfer =
                MatrixTileStreamTransferAbi.BeginStore(
                    ref core,
                    OwnerThreadId,
                    OpCode,
                    TileDescriptor.Rows);
            MatrixTileTileImage stagedSnapshot =
                transfer.StageEgress(sourceSnapshot, MemoryValidation);

            MatrixTileExecutionCaptureRecord capture =
                MatrixTileExecuteCaptureAbi.CaptureStore(
                Projection.Mnemonic,
                OperationKind,
                DependencyMetadata.SourceTileId,
                DependencyMetadata.SecondaryTileId,
                DependencyMetadata.DestinationTileId,
                TileDescriptor,
                SecondaryTileDescriptor,
                ResultTileDescriptor,
                MemoryContract,
                MemoryValidation,
                stagedSnapshot);
            return capture with
            {
                StreamTransfer = transfer.Complete(capture.HasFault)
            };
        }

        private MatrixTileExecutionCaptureRecord CaptureMacc(ref Processor.CPU_Core core)
        {
            MatrixTileTileImage leftSnapshot = CaptureTileSnapshotOrDefault(
                ref core,
                DependencyMetadata.SourceTileId,
                TileDescriptor);
            MatrixTileTileImage rightSnapshot = CaptureTileSnapshotOrDefault(
                ref core,
                DependencyMetadata.SecondaryTileId,
                SecondaryTileDescriptor);
            MatrixTileTileImage accumulatorSnapshot = CaptureTileSnapshotOrDefault(
                ref core,
                DependencyMetadata.DestinationTileId,
                ResultTileDescriptor);

            return MatrixTileExecuteCaptureAbi.CaptureMacc(
                Projection.Mnemonic,
                OperationKind,
                DependencyMetadata.SourceTileId,
                DependencyMetadata.SecondaryTileId,
                DependencyMetadata.DestinationTileId,
                TileDescriptor,
                SecondaryTileDescriptor,
                ResultTileDescriptor,
                Projection.SemanticValidation,
                MaccContract,
                leftSnapshot,
                rightSnapshot,
                accumulatorSnapshot);
        }

        private MatrixTileExecutionCaptureRecord CaptureTranspose(ref Processor.CPU_Core core)
        {
            MatrixTileTileImage sourceSnapshot = CaptureTileSnapshotOrDefault(
                ref core,
                DependencyMetadata.SourceTileId,
                TileDescriptor);

            return MatrixTileExecuteCaptureAbi.CaptureTranspose(
                Projection.Mnemonic,
                OperationKind,
                DependencyMetadata.SourceTileId,
                DependencyMetadata.SecondaryTileId,
                DependencyMetadata.DestinationTileId,
                TileDescriptor,
                SecondaryTileDescriptor,
                ResultTileDescriptor,
                Projection.SemanticValidation,
                TransposeContract,
                sourceSnapshot);
        }

        private MatrixTileTileImage CaptureTileSnapshotOrDefault(
            ref Processor.CPU_Core core,
            ushort tileId,
            MatrixTileCanonicalDescriptorAbi expectedDescriptor)
        {
            return core.TryCaptureMatrixTileSnapshot(
                OwnerThreadId,
                tileId,
                expectedDescriptor,
                out MatrixTileTileImage snapshot)
                ? snapshot
                : default;
        }

        private void EnsureNoUnretiredCapture()
        {
            if (_lastExecutionCapture.HasValue)
            {
                throw new InvalidOperationException(
                    $"{Projection.Mnemonic} already has a Phase09 execution capture record that must reach Phase10 retire publication or be discarded before re-execution.");
            }

            if (_lastReplayRollbackJournal is
                {
                    Lifecycle:
                        MatrixTileReplayRollbackLifecycle.Retired or
                        MatrixTileReplayRollbackLifecycle.FaultRetired or
                        MatrixTileReplayRollbackLifecycle.RolledBack
                })
            {
                throw new MatrixTileReplayRollbackValidationException(
                    $"{Projection.Mnemonic} replay/rollback authority must be replayed or discarded before MicroOp re-execution.");
            }

            if (_lastReplayRollbackJournal is
                {
                    Lifecycle:
                        MatrixTileReplayRollbackLifecycle.Replayed or
                        MatrixTileReplayRollbackLifecycle.FaultReplayed or
                        MatrixTileReplayRollbackLifecycle.Discarded
                })
            {
                _lastReplayRollbackJournal = null;
            }

            if (_captureLifecycle == MatrixTileCaptureLifecycle.Cancelled)
            {
                _captureLifecycle = MatrixTileCaptureLifecycle.None;
            }
        }

        private MatrixTileReplayRollbackJournal RequireReplayJournal()
        {
            return _lastReplayRollbackJournal ??
                throw new MatrixTileReplayRollbackValidationException(
                    $"{Projection.Mnemonic} has no retire-owned replay/rollback journal.");
        }

        private ResourceBitset BuildResourceMask()
        {
            ResourceBitset mask = ResourceBitset.Zero;
            MatrixTileMicroOpDependencyMetadata dependency = DependencyMetadata;

            if (OperationKind == MatrixTileProjectedOperationKind.Load)
            {
                mask |= ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId);
                mask |= ResourceMaskBuilder.ForLoad();
                mask |= ResourceMaskBuilder.ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel);
                mask |= ResourceMaskBuilder.ForMatrixTileStreamWindow();
                mask |= ResourceMaskBuilder.ForMatrixTileIngress();
            }

            if (OperationKind == MatrixTileProjectedOperationKind.Store)
            {
                mask |= ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId);
                mask |= ResourceMaskBuilder.ForStore();
                mask |= ResourceMaskBuilder.ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel);
                mask |= ResourceMaskBuilder.ForMatrixTileStreamWindow();
                mask |= ResourceMaskBuilder.ForMatrixTileEgress();
            }

            if (dependency.ReadsTileState)
            {
                mask |= ResourceMaskBuilder.ForMatrixTileStateRead();
            }

            if (dependency.WritesTileState)
            {
                mask |= ResourceMaskBuilder.ForMatrixTileStateWrite();
            }

            if (dependency.ReadsAccumulator)
            {
                mask |= ResourceMaskBuilder.ForMatrixTileAccumulatorRead();
            }

            if (dependency.WritesAccumulator)
            {
                mask |= ResourceMaskBuilder.ForMatrixTileAccumulatorWrite();
            }

            if (dependency.HasTransposePolicyDependencyMetadata)
            {
                mask |= ResourceMaskBuilder.ForMatrixTileTransposePolicy();
            }

            return mask;
        }

        private byte GetPublishedLatency()
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(OpCode);
            return checked((byte)(info?.ExecutionLatency ?? 1));
        }

        private static MatrixTileMicroOpDependencyMetadata CreateDependencyMetadata(
            MatrixTileInstructionIrProjection projection)
        {
            VectorInstructionPayload payload = projection.SourcePayload;
            ushort sourceTileId = GetTileId(payload.PrimaryPointer);
            ushort secondaryTileId = GetTileId(payload.SecondaryPointer);
            ushort destinationTileId = projection.OperationKind == MatrixTileProjectedOperationKind.Macc
                ? GetHighTileIdOrDefault(payload.SecondaryPointer, secondaryTileId)
                : projection.TransposeContract?.DestinationTileId ?? secondaryTileId;

            return projection.OperationKind switch
            {
                MatrixTileProjectedOperationKind.Load => new MatrixTileMicroOpDependencyMetadata(
                    projection.OperationKind,
                    projection.TileDescriptor,
                    projection.SecondaryTileDescriptor,
                    projection.ResultTileDescriptor,
                    HasTileMemoryDependencyMetadata: true,
                    HasTileRegisterDependencyMetadata: true,
                    HasAccumulatorDependencyMetadata: false,
                    HasTransposePolicyDependencyMetadata: false,
                    ReadsTileState: false,
                    WritesTileState: true,
                    ReadsAccumulator: false,
                    WritesAccumulator: false,
                    SourceTileId: 0,
                    SecondaryTileId: secondaryTileId,
                    DestinationTileId: destinationTileId),

                MatrixTileProjectedOperationKind.Store => new MatrixTileMicroOpDependencyMetadata(
                    projection.OperationKind,
                    projection.TileDescriptor,
                    projection.SecondaryTileDescriptor,
                    projection.ResultTileDescriptor,
                    HasTileMemoryDependencyMetadata: true,
                    HasTileRegisterDependencyMetadata: true,
                    HasAccumulatorDependencyMetadata: false,
                    HasTransposePolicyDependencyMetadata: false,
                    ReadsTileState: true,
                    WritesTileState: false,
                    ReadsAccumulator: false,
                    WritesAccumulator: false,
                    SourceTileId: secondaryTileId,
                    SecondaryTileId: secondaryTileId,
                    DestinationTileId: destinationTileId),

                MatrixTileProjectedOperationKind.Macc => new MatrixTileMicroOpDependencyMetadata(
                    projection.OperationKind,
                    projection.TileDescriptor,
                    projection.SecondaryTileDescriptor,
                    projection.ResultTileDescriptor,
                    HasTileMemoryDependencyMetadata: false,
                    HasTileRegisterDependencyMetadata: true,
                    HasAccumulatorDependencyMetadata: true,
                    HasTransposePolicyDependencyMetadata: false,
                    ReadsTileState: true,
                    WritesTileState: true,
                    ReadsAccumulator: true,
                    WritesAccumulator: true,
                    SourceTileId: sourceTileId,
                    SecondaryTileId: secondaryTileId,
                    DestinationTileId: destinationTileId),

                MatrixTileProjectedOperationKind.Transpose => new MatrixTileMicroOpDependencyMetadata(
                    projection.OperationKind,
                    projection.TileDescriptor,
                    projection.SecondaryTileDescriptor,
                    projection.ResultTileDescriptor,
                    HasTileMemoryDependencyMetadata: false,
                    HasTileRegisterDependencyMetadata: true,
                    HasAccumulatorDependencyMetadata: false,
                    HasTransposePolicyDependencyMetadata: true,
                    ReadsTileState: true,
                    WritesTileState: true,
                    ReadsAccumulator: false,
                    WritesAccumulator: false,
                    SourceTileId: projection.TransposeContract?.SourceTileId ?? sourceTileId,
                    SecondaryTileId: secondaryTileId,
                    DestinationTileId: destinationTileId),

                _ => throw new DecodeProjectionFaultException(
                    $"Unsupported MTILE Phase08 operation kind {projection.OperationKind}.")
            };
        }

        private static ushort GetTileId(ulong pointerOrTileToken)
        {
            return (ushort)(pointerOrTileToken & ushort.MaxValue);
        }

        private static ushort GetHighTileIdOrDefault(
            ulong pointerOrTileToken,
            ushort fallbackTileId)
        {
            ushort highTileId = (ushort)((pointerOrTileToken >> 16) & ushort.MaxValue);
            return highTileId == 0
                ? fallbackTileId
                : highTileId;
        }
    }

    public sealed class MtileLoadMicroOp : MatrixTileMicroOp
    {
        public MtileLoadMicroOp(MatrixTileMaterializedInstruction materializedInstruction)
            : base(materializedInstruction, MatrixTileProjectedOperationKind.Load)
        {
        }
    }

    public sealed class MtileStoreMicroOp : MatrixTileMicroOp
    {
        public MtileStoreMicroOp(MatrixTileMaterializedInstruction materializedInstruction)
            : base(materializedInstruction, MatrixTileProjectedOperationKind.Store)
        {
        }
    }

    public sealed class MtileMaccMicroOp : MatrixTileMicroOp
    {
        public MtileMaccMicroOp(MatrixTileMaterializedInstruction materializedInstruction)
            : base(materializedInstruction, MatrixTileProjectedOperationKind.Macc)
        {
        }
    }

    public sealed class MtransposeMicroOp : MatrixTileMicroOp
    {
        public MtransposeMicroOp(MatrixTileMaterializedInstruction materializedInstruction)
            : base(materializedInstruction, MatrixTileProjectedOperationKind.Transpose)
        {
        }
    }
}
