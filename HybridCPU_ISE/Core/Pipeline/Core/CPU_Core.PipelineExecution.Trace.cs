using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Trace and timeline helpers live in this partial so the main pipeline
            /// execution file can shed a naturally isolated diagnostics cluster.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordTraceEvent(
                ulong pc,
                uint opCode,
                ulong resultValue,
                int ownerThreadId,
                int virtualThreadId,
                bool wasFspInjected,
                int originalThreadId)
            {
                if (Processor.Coverage != null && opCode < 256)
                {
                    Processor.Coverage.Record(opCode);
                }

                HybridCPU_ISE.Core.TraceSink? traceSink = Processor.TraceSink;
                if (traceSink != null)
                {
                    PipelineObservationSnapshot observation = GetPipelineObservationSnapshot();
                    var evt = new HybridCPU_ISE.Core.TraceEvent((long)pc, (int)(pc / 256), (int)((pc % 256) / 32), opCode)
                    {
                        Result = resultValue,
                        ExceptionCount = this.ExceptionStatus.TotalExceptions()
                    };

                    if (this.ExceptionStatus.HasExceptions())
                    {
                        evt.Flags = new HybridCPU_ISE.Core.VectorExceptionFlags
                        {
                            Overflow = this.ExceptionStatus.OverflowCount > 0,
                            Underflow = this.ExceptionStatus.UnderflowCount > 0,
                            DivByZero = this.ExceptionStatus.DivByZeroCount > 0,
                            InvalidOp = this.ExceptionStatus.InvalidOpCount > 0,
                            Inexact = this.ExceptionStatus.InexactCount > 0
                        };

                        if (Processor.Coverage != null)
                        {
                            Processor.Coverage.RecordException(evt.Flags);
                        }
                    }

                    traceSink.Record(evt);

                    if (traceSink.ShouldCaptureFullState)
                    {
                        traceSink.RecordPhaseAwareState(
                            new HybridCPU_ISE.Core.FullStateTraceEvent
                            {
                                PC = (long)pc,
                                BundleId = (int)(pc / 256),
                                OpIndex = (int)((pc % 256) / 32),
                                Opcode = opCode,
                                ThreadId = wasFspInjected ? 0 : ownerThreadId,
                                CycleNumber = (long)pipeCtrl.CycleCount,
                                RegisterFile = CaptureTraceRegisterFile(ownerThreadId),
                                PredicateRegisters = CaptureTracePredicateRegisters(),
                                WasStolenSlot = wasFspInjected,
                                OriginalThreadId = originalThreadId,
                                PipelineStage = "WB",
                                Stalled = observation.PipelineControl.Stalled,
                                StallReason = PipelineStallText.Render(
                                    observation.PipelineControl.StallReason,
                                    PipelineStallTextStyle.Trace),
                                DecodedBundleStateOwnerKind = observation.DecodedBundleStateOwnerKind,
                                DecodedBundleStateEpoch = observation.DecodedBundleStateEpoch,
                                DecodedBundleStateVersion = observation.DecodedBundleStateVersion,
                                DecodedBundleStateKind = observation.DecodedBundleStateKind,
                                DecodedBundleStateOrigin = observation.DecodedBundleStateOrigin,
                                DecodedBundlePc = observation.DecodedBundlePc,
                                DecodedBundleValidMask = observation.DecodedBundleValidMask,
                                DecodedBundleNopMask = observation.DecodedBundleNopMask,
                                DecodedBundleHasCanonicalDecode = observation.DecodedBundleHasCanonicalDecode,
                                DecodedBundleHasCanonicalLegality = observation.DecodedBundleHasCanonicalLegality,
                                DecodedBundleHasDecodeFault = observation.DecodedBundleHasDecodeFault,
                                ActiveMemoryRequests = GetBoundMemorySubsystemCurrentQueuedRequests(),
                                MemorySubsystemCycle = 0,
                                ThreadReadyQueueDepths = _fspScheduler == null ? null : new[]
                                {
                                    _fspScheduler.GetOutstandingMemoryCount(0),
                                    _fspScheduler.GetOutstandingMemoryCount(1),
                                    _fspScheduler.GetOutstandingMemoryCount(2),
                                    _fspScheduler.GetOutstandingMemoryCount(3)
                                },
                                CurrentFSPPolicy = _loopBuffer.CurrentReplayPhase.IsActive ? "ReplayAwarePhase1" : "DeterministicFSP"
                            },
                            _loopBuffer.CurrentReplayPhase,
                            _fspScheduler?.GetPhaseMetrics() ?? default,
                            phaseCertificateTemplateReusable: _loopBuffer.CurrentReplayPhase.IsActive &&
                                (_fspScheduler?.LastPhaseCertificateInvalidationReason ?? Core.ReplayPhaseInvalidationReason.None) == Core.ReplayPhaseInvalidationReason.None);
                    }
                }
            }

            private ulong[] CaptureTraceRegisterFile(int ownerThreadId)
            {
                int vtId = NormalizePipelineStateVtId(ownerThreadId);
                var registers = new ulong[YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs];
                for (int i = 0; i < registers.Length; i++)
                {
                    registers[i] = ReadArch(vtId, i);
                }

                return registers;
            }

            private ushort[] CaptureTracePredicateRegisters()
            {
                var predicates = new ushort[16];
                for (int i = 0; i < predicates.Length; i++)
                {
                    predicates[i] = (ushort)(GetPredicateRegister(i) & 0xFFFF);
                }

                return predicates;
            }

            private void RecordPhaseTimelineSample(string pipelineStage)
            {
                HybridCPU_ISE.Core.TraceSink? traceSink = Processor.TraceSink;
                if (traceSink == null)
                    return;

                var phaseContext = _loopBuffer.CurrentReplayPhase;
                if (!phaseContext.IsActive && !pipeCtrl.Stalled)
                    return;

                ulong samplePc = GetTraceSamplePC();
                uint sampleOpCode = GetTraceSampleOpCode();
                int ownerThreadId = GetTraceSampleOwnerThreadId();
                bool wasFspInjected = GetTraceSampleWasFspInjected();
                int originalThreadId = GetTraceSampleOriginalThreadId();
                PipelineObservationSnapshot observation = GetPipelineObservationSnapshot();
                string runtimePolicy = GetTraceRuntimePolicyText(in observation, phaseContext);

                if (traceSink.ShouldCaptureFullState)
                {
                    traceSink.RecordPhaseAwareState(
                        new HybridCPU_ISE.Core.FullStateTraceEvent
                        {
                            PC = (long)samplePc,
                            BundleId = (int)(samplePc / 256),
                            OpIndex = -1,
                            Opcode = sampleOpCode,
                            ThreadId = wasFspInjected ? 0 : ownerThreadId,
                            CycleNumber = (long)pipeCtrl.CycleCount,
                            RegisterFile = CaptureTraceRegisterFile(ownerThreadId),
                            PredicateRegisters = CaptureTracePredicateRegisters(),
                            WasStolenSlot = wasFspInjected,
                            OriginalThreadId = originalThreadId,
                            PipelineStage = pipelineStage,
                            Stalled = observation.PipelineControl.Stalled,
                            StallReason = PipelineStallText.Render(
                                observation.PipelineControl.StallReason,
                                PipelineStallTextStyle.Trace),
                            DecodedBundleStateOwnerKind = observation.DecodedBundleStateOwnerKind,
                            DecodedBundleStateEpoch = observation.DecodedBundleStateEpoch,
                            DecodedBundleStateVersion = observation.DecodedBundleStateVersion,
                            DecodedBundleStateKind = observation.DecodedBundleStateKind,
                            DecodedBundleStateOrigin = observation.DecodedBundleStateOrigin,
                            DecodedBundlePc = observation.DecodedBundlePc,
                            DecodedBundleValidMask = observation.DecodedBundleValidMask,
                            DecodedBundleNopMask = observation.DecodedBundleNopMask,
                            DecodedBundleHasCanonicalDecode = observation.DecodedBundleHasCanonicalDecode,
                            DecodedBundleHasCanonicalLegality = observation.DecodedBundleHasCanonicalLegality,
                            DecodedBundleHasDecodeFault = observation.DecodedBundleHasDecodeFault,
                            BankQueueDepths = GetBoundMemorySubsystemBankQueueDepths(),
                            ActiveMemoryRequests = GetBoundMemorySubsystemCurrentQueuedRequests(),
                            MemorySubsystemCycle = GetBoundMemorySubsystemCurrentCycle(),
                            ThreadReadyQueueDepths = _fspScheduler == null ? null : new[]
                            {
                                _fspScheduler.GetOutstandingMemoryCount(0),
                                _fspScheduler.GetOutstandingMemoryCount(1),
                                _fspScheduler.GetOutstandingMemoryCount(2),
                                _fspScheduler.GetOutstandingMemoryCount(3)
                            },
                            CurrentFSPPolicy = runtimePolicy
                        },
                        phaseContext,
                        _fspScheduler?.GetPhaseMetrics() ?? default,
                        phaseCertificateTemplateReusable: phaseContext.IsActive &&
                            (_fspScheduler?.LastPhaseCertificateInvalidationReason ?? Core.ReplayPhaseInvalidationReason.None) == Core.ReplayPhaseInvalidationReason.None);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private string GetTraceRuntimePolicyText(
                in PipelineObservationSnapshot observation,
                Core.ReplayPhaseContext phaseContext)
            {
                string basePolicy = phaseContext.IsActive ? "ReplayAwarePhase1.DenseTimeline" : "DeterministicFSP";
                Core.RuntimeClusterAdmissionPreparation admissionPreparation = observation.AdmissionPreparation;
                Core.RuntimeClusterAdmissionCandidateView admissionCandidateView = observation.AdmissionCandidateView;
                Core.RuntimeClusterAdmissionDecisionDraft admissionDecisionDraft = observation.AdmissionDecisionDraft;
                Core.RuntimeClusterAdmissionHandoff admissionHandoff = observation.AdmissionHandoff;
                Core.ClusterFallbackDiagnosticsSnapshot fallbackDiagnosticsSnapshot = admissionCandidateView.FallbackDiagnosticsSnapshot;

                return $"{basePolicy}|DecodeMode={admissionPreparation.DecodeMode}|AdmissionReady={admissionPreparation.ShouldConsiderClusterAdmission}|PreparedScalarCount={admissionPreparation.PreparedScalarCount}|ScalarPrepared=0x{admissionPreparation.PreparedScalarMask:X2}|AuxReservations={admissionPreparation.AuxiliaryReservationCount}|FallbackDiagnosticsMask=0x{fallbackDiagnosticsSnapshot.FallbackDiagnosticsMask:X2}|FallbackReasonMask=0x{(byte)admissionDecisionDraft.FallbackReasonMask:X2}|SuggestsFallbackDiagnostics={fallbackDiagnosticsSnapshot.SuggestsFallbackDiagnostics}|DraftCandidate={admissionCandidateView.HasDraftCandidate}|DraftIssueWidth={admissionCandidateView.AdvisoryScalarIssueWidth}|ScalarCandidates=0x{admissionCandidateView.ScalarCandidateMask:X2}|BlockedScalar=0x{admissionCandidateView.BlockedScalarCandidateMask:X2}|AuxCandidates=0x{admissionCandidateView.AuxiliaryCandidateMask:X2}|AuxReserved=0x{admissionCandidateView.AuxiliaryReservationMask:X2}|RegisterHazards=0x{admissionCandidateView.RegisterHazardMask:X2}|OrderingHazards=0x{admissionCandidateView.OrderingHazardMask:X2}|DecisionKind={admissionDecisionDraft.DecisionKind}|DecisionScalarMask=0x{admissionDecisionDraft.ScalarIssueMask:X2}|DecisionAuxMask=0x{admissionDecisionDraft.AuxiliaryReservationMask:X2}|ProbeClusterPath={admissionDecisionDraft.ShouldProbeClusterPath}|RetainsReferenceSequential={admissionDecisionDraft.RetainsReferenceSequentialPath}|HandoffReady={admissionHandoff.IsHandoffReady}|LegalitySeed={admissionHandoff.CanSeedLegalityIntegration}|ClusterSeed={admissionHandoff.CanSeedClusterIntegration}";
            }

            private ulong GetTraceSamplePC()
            {
                if (pipeWB.Valid) return pipeWB.PC;
                if (pipeMEM.Valid) return pipeMEM.PC;
                if (pipeEX.Valid) return pipeEX.PC;
                if (pipeID.Valid) return pipeID.PC;
                if (pipeIF.Valid) return pipeIF.PC;
                return ReadActiveLivePc();
            }

            private uint GetTraceSampleOpCode()
            {
                if (pipeWB.Valid) return pipeWB.OpCode;
                if (pipeMEM.Valid) return pipeMEM.OpCode;
                if (pipeEX.Valid) return pipeEX.OpCode;
                if (pipeID.Valid) return pipeID.OpCode;
                return 0;
            }

            private int GetTraceSampleOwnerThreadId()
            {
                if (pipeWB.Valid) return pipeWB.OwnerThreadId;
                if (pipeMEM.Valid) return pipeMEM.OwnerThreadId;
                if (pipeEX.Valid) return pipeEX.OwnerThreadId;
                return 0;
            }

            private bool GetTraceSampleWasFspInjected()
            {
                if (pipeWB.Valid) return pipeWB.WasFspInjected;
                if (pipeMEM.Valid) return pipeMEM.WasFspInjected;
                if (pipeEX.Valid) return pipeEX.WasFspInjected;
                return false;
            }

            private int GetTraceSampleOriginalThreadId()
            {
                if (pipeWB.Valid) return pipeWB.OriginalThreadId;
                if (pipeMEM.Valid) return pipeMEM.OriginalThreadId;
                if (pipeEX.Valid) return pipeEX.OriginalThreadId;
                return 0;
            }
        }
    }
}
