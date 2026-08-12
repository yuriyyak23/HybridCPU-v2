using System;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            private readonly CoreRuntimeState _runtime;

            /// <summary>
            /// Stable containment identity for this constructed core. A default
            /// struct is not a live core and may not synthesize an identity.
            /// </summary>
            internal CoreRuntimeState Runtime =>
                _runtime ?? throw new InvalidOperationException(
                    "A default or uninitialized CPU_Core has no CoreRuntimeState identity.");

            private ref FetchStage pipeIF => ref Runtime.Frontend.Fetch;

            private ref byte[]? _fetchVliwBuffer =>
                ref Runtime.Frontend.FetchVliwBuffer;

            private ref BranchPredictor branchPred =>
                ref Runtime.Frontend.BranchPredictor;

            private ref Core.DifferentialTraceCapture? differentialTraceCapture =>
                ref Runtime.Telemetry.DifferentialTraceCapture;

            private ref ulong _assistRuntimeEpoch =>
                ref Runtime.Assist.RuntimeEpoch;

            private ref Core.AssistInvalidationReason _lastAssistInvalidationReason =>
                ref Runtime.Assist.LastInvalidationReason;

            private ref byte[] ScratchA => ref Runtime.Scratch.ScratchA;

            private ref byte[] ScratchB => ref Runtime.Scratch.ScratchB;

            private ref byte[] ScratchDst => ref Runtime.Scratch.ScratchDst;

            private ref byte[] ScratchIndex => ref Runtime.Scratch.ScratchIndex;

            private ref byte[] ScratchA_DB0 => ref Runtime.Scratch.ScratchA_DB0;

            private ref byte[] ScratchB_DB0 => ref Runtime.Scratch.ScratchB_DB0;

            private ref byte[] ScratchA_DB1 => ref Runtime.Scratch.ScratchA_DB1;

            private ref byte[] ScratchB_DB1 => ref Runtime.Scratch.ScratchB_DB1;

            private ref byte[] ScratchDst_DB0 => ref Runtime.Scratch.ScratchDst_DB0;

            private ref byte[] ScratchDst_DB1 => ref Runtime.Scratch.ScratchDst_DB1;

            private ref Core.ScratchBankController BankedScratchA =>
                ref Runtime.Scratch.BankedScratchA;

            private ref Core.ScratchBankController BankedScratchB =>
                ref Runtime.Scratch.BankedScratchB;

            private ref Core.ScratchBankController BankedScratchDst =>
                ref Runtime.Scratch.BankedScratchDst;

            private ref int ActiveBufferSet => ref Runtime.Scratch.ActiveBufferSet;

            public ref Cache_VLIWBundle_Object[] L1_VLIWBundles =>
                ref Runtime.Cache.L1VliwBundles;

            public ref Cache_Data_Object[] L1_Data => ref Runtime.Cache.L1Data;

            public ref Cache_VLIWBundle_Object[] L2_VLIWBundles =>
                ref Runtime.Cache.L2VliwBundles;

            public ref Cache_Data_Object[] L2_Data => ref Runtime.Cache.L2Data;

            private ref ulong ulong_MinL1Query => ref Runtime.Cache.MinimumL1Query;

            private ref ulong ulong_MinL2Query => ref Runtime.Cache.MinimumL2Query;

            private ref ulong Current_VLIWBundle_Position =>
                ref Runtime.Cache.CurrentVliwBundlePosition;

            private ref ulong Current_DataObject_Position =>
                ref Runtime.Cache.CurrentDataObjectPosition;

            private ref Core.ResourceBitset globalResourceLocks =>
                ref Runtime.Resources.GlobalResourceLocks;

            private ref ulong tokenGeneration =>
                ref Runtime.Resources.TokenGeneration;

            private ref ulong[] resourceTokens =>
                ref Runtime.Resources.ResourceTokens;

            public ulong StructuralStalls
            {
                get => Runtime.Resources.StructuralStalls;
                private set => Runtime.Resources.StructuralStalls = value;
            }

            private ref ulong[] resourceUsageCounts =>
                ref Runtime.Resources.ResourceUsageCounts;

            private ref ulong[] resourceContentionCounts =>
                ref Runtime.Resources.ResourceContentionCounts;

            private ref byte[] _readCounters =>
                ref Runtime.Resources.ReadCounters;

            private ref ulong syncCounter =>
                ref Runtime.Resources.SyncCounter;

            private ref uint[] _grlbBanks =>
                ref Runtime.Resources.GrlbBanks;

            private ref ulong[] _bankContentionCounts =>
                ref Runtime.Resources.BankContentionCounts;

            public ref bool IsVMXRoot =>
                ref Runtime.VirtualThreadControl.IsVmxRoot;

            public ref PipelineState[] VirtualThreadPipelineStates =>
                ref Runtime.VirtualThreadControl.PipelineStates;

            private ref bool _vmxExecutionPlaneWired =>
                ref Runtime.VirtualThreadControl.VmxExecutionPlaneWired;

            public ref ulong CycleCounter =>
                ref Runtime.LegacyCompatibility.CycleCounter;

            public ref int StageCycleCounter =>
                ref Runtime.LegacyCompatibility.StageCycleCounter;

            public ref bool Stalled =>
                ref Runtime.LegacyCompatibility.Stalled;

            public ref uint CoreID =>
                ref Runtime.Binding.CoreId;

            private ref CpuCorePlatformContext _platformContext =>
                ref Runtime.Binding.PlatformContext;

            private ref ProcessorMode _executionMode =>
                ref Runtime.Binding.ExecutionMode;

            private ref Func<Processor.DeviceType, ushort, ulong, byte>? _interruptDispatcher =>
                ref Runtime.Binding.InterruptDispatcher;

            private ref ulong _matrixTileStreamInvalidationCount =>
                ref Runtime.Extensions.MatrixTile.StreamInvalidationCount;

            private ref ulong _nextMatrixTileCaptureOrdinal =>
                ref Runtime.Extensions.MatrixTile.NextCaptureOrdinal;

            private ref ulong _nextMatrixTileReplayCheckpointOrdinal =>
                ref Runtime.Extensions.MatrixTile.NextReplayCheckpointOrdinal;

            private ref ulong _matrixTileReplayInvalidationEpoch =>
                ref Runtime.Extensions.MatrixTile.ReplayInvalidationEpoch;

            private ref ulong ulong_InstructionPointer =>
                ref Runtime.Frontend.ActiveLivePc;

            private ref bool _hasMaterializedVliwFetchState =>
                ref Runtime.Frontend.HasMaterializedVliwFetchState;

            private ref DecodeStage pipeID => ref Runtime.Decode.Decode;

            private ref byte pipelineBundleSlot => ref Runtime.Decode.PipelineBundleSlot;

            private ref Core.DecodedBundleRuntimeState decodedBundleRuntimeState =>
                ref Runtime.Decode.BundleRuntime;

            private ref Core.BundleProgressState decodedBundleProgressState =>
                ref Runtime.Decode.BundleProgress;

            private ref Core.DecodedBundleDerivedIssuePlanState decodedBundleDerivedIssuePlanState =>
                ref Runtime.Decode.DerivedIssuePlan;

            private ref ulong decodedBundleStateEpochCounter =>
                ref Runtime.Decode.BundleStateEpochCounter;

            private ref ulong decodedBundleStateVersionCounter =>
                ref Runtime.Decode.BundleStateVersionCounter;

            private ref Core.ClusterIssuePreparation pipeIDClusterPreparation =>
                ref Runtime.Decode.ClusterPreparation;

            private ref bool bundleDecodedAndPacked =>
                ref Runtime.Decode.BundleDecodedAndPacked;

            private ref Core.RuntimeClusterAdmissionPreparation pipeIDAdmissionPreparation =>
                ref Runtime.Admission.Preparation;

            private ref Core.RuntimeClusterAdmissionCandidateView pipeIDAdmissionCandidateView =>
                ref Runtime.Admission.CandidateView;

            private ref Core.RuntimeClusterAdmissionDecisionDraft pipeIDAdmissionDecisionDraft =>
                ref Runtime.Admission.DecisionDraft;

            private ref Core.RuntimeClusterAdmissionHandoff pipeIDAdmissionHandoff =>
                ref Runtime.Admission.Handoff;

            private ref Core.LoopBuffer _loopBuffer => ref Runtime.Replay.LoopBuffer;

            private ref ulong _replayCodeGenerationEpoch =>
                ref Runtime.Replay.CodeGenerationEpoch;

            private ref ulong _observedReplayRelevantMemoryEpoch =>
                ref Runtime.Replay.ObservedRelevantMemoryEpoch;

            private ref Core.Decoder.ReplaySemanticShadowLookup? _replaySemanticShadowLookup =>
                ref Runtime.Replay.SemanticShadowLookup;

            public ref Core.Registers.Retire.RetireCoordinator RetireCoordinator =>
                ref Runtime.Retire.Coordinator;

            internal Core.ArchitecturalCompletionCommitOwner ArchitecturalCompletionCommitOwner =>
                Runtime.Retire.CompletionCommitOwner;

            internal Core.DomainCompletionObservationOwner DomainCompletionObservationOwner =>
                Runtime.Retire.CompletionCommitOwner.ObservationOwner;

            private Core.ArchitecturalCompletionCommitOwner.ProducerRegistration
                CanonicalPipelineCompletionProducer =>
                    Runtime.Retire.CanonicalPipelineCompletionProducer;

            private ref Core.PipelineContourCertificate decodePublicationCertificate =>
                ref Runtime.Retire.DecodePublicationCertificate;

            private ref Core.PipelineContourCertificate executeCompletionCertificate =>
                ref Runtime.Retire.ExecuteCompletionCertificate;

            private ref Core.PipelineContourCertificate retireVisibilityCertificate =>
                ref Runtime.Retire.RetireVisibilityCertificate;

            public ref Core.Registers.ArchContextState[] ArchContexts =>
                ref Runtime.Architectural.Contexts;

            public ref Core.Registers.CsrFile Csr =>
                ref Runtime.Architectural.Csr;

            public ref ulong CsrPodId =>
                ref Runtime.Architectural.PodId;

            public ref ulong CsrPodAffinityMask =>
                ref Runtime.Architectural.PodAffinityMask;

            public ref ulong CsrMemDomainCert =>
                ref Runtime.Architectural.MemoryDomainCertificate;

            public ref ulong CsrNocRouteCfg =>
                ref Runtime.Architectural.NocRouteConfiguration;

            public ref FPExceptionContext[] ThreadFPContexts =>
                ref Runtime.Architectural.FloatingPointContexts;

            public ref FlagsRegister CoreFlagsRegister =>
                ref Runtime.Architectural.CoreFlags;

            public ref System.Collections.Generic.List<FlagsRegister> Core_FlagsRegisters_Stack =>
                ref Runtime.Architectural.FlagsContextStack;

            public ref System.Collections.Generic.List<ulong> Call_Callback_Addresses =>
                ref Runtime.Architectural.CallContextStack;

            public ref System.Collections.Generic.List<ulong> Interrupt_Callback_Addresses =>
                ref Runtime.Architectural.InterruptContextStack;

            private ref Processor.StackMemory Stack =>
                ref Runtime.Architectural.Stack;

            private ref ulong predReg0 => ref Runtime.Architectural.PredicateRegister0;
            private ref ulong predReg1 => ref Runtime.Architectural.PredicateRegister1;
            private ref ulong predReg2 => ref Runtime.Architectural.PredicateRegister2;
            private ref ulong predReg3 => ref Runtime.Architectural.PredicateRegister3;
            private ref ulong predReg4 => ref Runtime.Architectural.PredicateRegister4;
            private ref ulong predReg5 => ref Runtime.Architectural.PredicateRegister5;
            private ref ulong predReg6 => ref Runtime.Architectural.PredicateRegister6;
            private ref ulong predReg7 => ref Runtime.Architectural.PredicateRegister7;
            private ref ulong predReg8 => ref Runtime.Architectural.PredicateRegister8;
            private ref ulong predReg9 => ref Runtime.Architectural.PredicateRegister9;
            private ref ulong predReg10 => ref Runtime.Architectural.PredicateRegister10;
            private ref ulong predReg11 => ref Runtime.Architectural.PredicateRegister11;
            private ref ulong predReg12 => ref Runtime.Architectural.PredicateRegister12;
            private ref ulong predReg13 => ref Runtime.Architectural.PredicateRegister13;
            private ref ulong predReg14 => ref Runtime.Architectural.PredicateRegister14;
            private ref ulong predReg15 => ref Runtime.Architectural.PredicateRegister15;

            public ref RVV_Config VectorConfig =>
                ref Runtime.Architectural.VectorConfig;

            public ref VectorExceptionStatus ExceptionStatus =>
                ref Runtime.Architectural.VectorExceptionStatus;

            public ref VectorContext SavedVectorContext =>
                ref Runtime.Architectural.SavedVectorContext;

            private ref Core.MicroOpScheduler _fspScheduler =>
                ref Runtime.Scheduling.Scheduler;

            public ref bool[] VirtualThreadStalled =>
                ref Runtime.Scheduling.VirtualThreadStalled;

            public ref int ActiveVirtualThreadId =>
                ref Runtime.Scheduling.ActiveVirtualThreadId;

            private ref ExecuteStage pipeEX => ref Runtime.Execution.Execute;

            private ref PipelineControl pipeCtrl => ref Runtime.Execution.Control;

            private ref ForwardingPath forwardEX => ref Runtime.Execution.ExecuteForwarding;

            private ref ForwardingPath forwardMEM => ref Runtime.Execution.MemoryForwarding;

            private ref ForwardingPath forwardWB => ref Runtime.Execution.WriteBackForwarding;

            private Core.Decoder.OperationAttemptIssuer rf08OperationAttemptIssuer =>
                Runtime.Execution.OperationAttemptIssuer;

            private ref MemoryStage pipeMEM => ref Runtime.MemoryPipeline.Memory;

            private ref WriteBackStage pipeWB => ref Runtime.MemoryPipeline.WriteBack;

            private ref byte[]? _explicitPacketImmediateReadBuffer =>
                ref Runtime.MemoryPipeline.ExplicitPacketImmediateReadBuffer;

            private ref Processor.MainMemoryArea? _mainMemory =>
                ref Runtime.MemoryPipeline.MainMemory;

            private ref YAKSys_Hybrid_CPU.Memory.MemorySubsystem? _memorySubsystem =>
                ref Runtime.MemoryPipeline.MemorySubsystem;

            private ref bool _memorySubsystemCaptured =>
                ref Runtime.MemoryPipeline.MemorySubsystemCaptured;

            private ref Core.Memory.IAtomicMemoryUnit? _atomicMemoryUnit =>
                ref Runtime.MemoryPipeline.AtomicMemoryUnit;

            public ref Core.Registers.PhysicalRegisterFile PhysicalRegisters =>
                ref Runtime.Backend.PhysicalRegisters;

            public ref Core.Registers.RenameMap ArchRenameMap =>
                ref Runtime.Backend.RenameMap;

            public ref Core.Registers.CommitMap ArchCommitMap =>
                ref Runtime.Backend.CommitMap;

            public ref Core.Registers.FreeList PhysRegFreeList =>
                ref Runtime.Backend.FreeList;

            private ref CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MatrixTileArchitecturalTileRegisterFile? _matrixTileRegisterFile =>
                ref Runtime.Extensions.MatrixTileRegisterFile;

            private ref Memory.StreamRegisterFile? _matrixTileStreamRegisterFile =>
                ref Runtime.Extensions.MatrixTileStreamRegisterFile;

            private ref System.Collections.Generic.Dictionary<ulong, CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MatrixTileReplayRollbackJournal>? _matrixTileReplayJournals =>
                ref Runtime.Extensions.MatrixTileReplayJournals;

            private ref Core.Execution.DmaStreamCompute.DmaStreamComputeTokenStore? _dmaStreamComputeTokenStore =>
                ref Runtime.Extensions.DmaStreamComputeTokenStore;

            private ref Core.Execution.ExternalAccelerators.ExternalAcceleratorRuntime? _externalAcceleratorRuntime =>
                ref Runtime.Extensions.ExternalAcceleratorRuntime;
        }
    }
}
