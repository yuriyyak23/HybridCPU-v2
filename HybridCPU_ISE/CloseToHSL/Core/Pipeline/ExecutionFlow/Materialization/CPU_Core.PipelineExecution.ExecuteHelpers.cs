using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ExecuteMicroOpWithStableCoreIdentity(Core.MicroOp microOp)
            {
                // RF-11.31: CPU_Core contains only the reference-owned runtime identity.
                // Keep the legacy ref ABI local until that ABI is migrated independently;
                // no MicroOp.Execute implementation replaces the supplied facade.
                CPU_Core stableCoreIdentity = this;
                return microOp.Execute(ref stableCoreIdentity);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseCompletedExecuteNonScalarLane(byte laneIndex)
            {
                if (laneIndex < 4 || laneIndex >= 8)
                    return;

                ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                if (!lane.IsOccupied)
                    return;

                byte slotMask = lane.SlotIndex < 8
                    ? (byte)(1 << lane.SlotIndex)
                    : (byte)0;

                ReleaseScalarLaneBookkeeping(lane);

                ScalarExecuteLaneState clearedLane = new();
                clearedLane.Clear(laneIndex);
                pipeEX.SetLane(laneIndex, clearedLane);
                pipeEX.SelectedNonScalarSlotMask = (byte)(pipeEX.SelectedNonScalarSlotMask & ~slotMask);
                pipeEX.MaterializedScalarLaneCount = CountOccupiedScalarExecuteLanes(pipeEX);
                pipeEX.MaterializedPhysicalLaneCount = CountOccupiedPhysicalExecuteLanes(pipeEX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordLane7ConditionalBranchCompletion(bool redirected)
            {
                pipeCtrl.Lane7ConditionalBranchExecuteCompletionCount++;
                if (redirected)
                {
                    pipeCtrl.Lane7ConditionalBranchRedirectCount++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketAssistExecutionOutcome(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                bool success,
                ref int executedPhysicalLaneCount)
            {
                Core.AssistMicroOp? assistMicroOp = lane.MicroOp as Core.AssistMicroOp;

                lane.IsMemoryOp = true;
                lane.IsLoad = true;
                lane.MemoryAddress = assistMicroOp?.BaseAddress ?? 0;
                lane.MemoryAccessSize = assistMicroOp?.ElementSize ?? (byte)0;
                lane.ResultReady = success;
                lane.GeneratedEvent = null;
                lane.GeneratedCsrEffect = null;
                lane.GeneratedAtomicEffect = null;
                lane.GeneratedVmxEffect = null;
                lane.GeneratedRetireRecordCount = 0;
                lane.GeneratedRetireRecord0 = default;
                lane.GeneratedRetireRecord1 = default;
                lane.VectorComplete = success;
                lane.ResultValue = 0;

                pipeEX.SetLane(laneIndex, lane);
                executedPhysicalLaneCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketExecuteEpilogueAccounting(
                int executedPhysicalLaneCount,
                int executedScalarLaneCount)
            {
                if (executedPhysicalLaneCount > 1)
                {
                    pipeCtrl.MultiLaneExecuteCount++;

                    // Stage 7 Phase C: track partial-width issue (2 or 3 lanes, not full 4)
                    if (executedScalarLaneCount > 1 && executedScalarLaneCount < 4)
                    {
                        pipeCtrl.PartialWidthIssueCount++;
                    }
                }

                // Stage 6 Phase E: record actual issue width in histogram
                if (pipeCtrl.ScalarIssueWidthHistogram != null &&
                    executedScalarLaneCount >= 0 &&
                    executedScalarLaneCount <= 4)
                {
                    pipeCtrl.ScalarIssueWidthHistogram[executedScalarLaneCount]++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishExplicitPacketExecuteForwarding()
            {
                // Multi-lane forwarding: lowest-index occupied lane with WritesRegister && ResultReady.
                for (byte laneIndex = 0; laneIndex < 4; laneIndex++)
                {
                    ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                    if (lane.IsOccupied && lane.WritesRegister && lane.ResultReady)
                    {
                        forwardEX.Valid = true;
                        forwardEX.DestRegID = lane.DestRegID;
                        forwardEX.ForwardedValue = lane.ResultValue;
                        forwardEX.AvailableCycle = (long)pipeCtrl.CycleCount + 1;
                        forwardEX.SourceStage = PipelineStage.Execute;
                        break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryConsumeEmptyExplicitPacketAfterExecution()
            {
                if (CountOccupiedPhysicalExecuteLanes(pipeEX) != 0)
                    return false;

                pipeEX.Clear();
                ConsumeDecodeStateAfterExecuteDispatch();
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CompleteExplicitPacketExecuteDispatch(byte originalActiveLaneIndex)
            {
                pipeEX.Valid = true;
                pipeEX.ActiveLaneIndex = originalActiveLaneIndex;
                ConsumeDecodeStateAfterExecuteDispatch();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishSingleLaneExecuteForwarding(bool includeTimingMetadata)
            {
                if (!pipeEX.WritesRegister || !pipeEX.ResultReady)
                    return;

                forwardEX.Valid = true;
                forwardEX.DestRegID = pipeEX.DestRegID;
                forwardEX.ForwardedValue = pipeEX.ResultValue;

                if (includeTimingMetadata)
                {
                    // Phase 2: canonical EX->EX forwarding becomes visible on the next cycle.
                    forwardEX.AvailableCycle = (long)pipeCtrl.CycleCount + 1;
                    forwardEX.SourceStage = PipelineStage.Execute;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectSingleLaneReferenceRawFallbackEntry()
            {
                FailCloseSingleLaneExecuteAfterNonFaultException();

                throw Core.ExecutionFaultContract.CreateWrappedException(
                    Core.ExecutionFaultCategory.InvalidInternalOp,
                    $"Scalar opcode 0x{pipeID.OpCode:X} reached execute without an authoritative MicroOp. " +
                    "Production pipeline execution no longer enters reference raw fallback for null-MicroOp carriers; reference raw execution is test-only.",
                    new InvalidOperationException("Missing authoritative MicroOp for production single-lane execute."));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchSingleLaneExecuteDispatchResources()
            {
                Core.ResourceBitset idResourceMask = pipeID.MicroOp?.ResourceMask ?? Core.ResourceBitset.Zero;
                ulong idResourceToken = 0;
                if (idResourceMask.IsNonZero)
                {
                    AcquireResourcesWithToken(idResourceMask, out idResourceToken);
                }

                pipeEX.ResourceMask = idResourceMask;
                pipeEX.ResourceToken = idResourceToken;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LatchSingleLaneExecuteDispatchState()
            {
                pipeEX.PC = pipeID.PC;
                pipeEX.OpCode = pipeID.OpCode;
                pipeEX.IsMemoryOp = pipeID.IsMemoryOp;
                pipeEX.IsVectorOp = pipeID.IsVectorOp;
                pipeEX.WritesRegister = pipeID.WritesRegister;
                ushort destRegId = ResolvePrimaryWriteRegister(pipeID.MicroOp);
                if (pipeID.MicroOp is null && pipeID.WritesRegister)
                {
                    destRegId = pipeID.Reg1ID;
                }

                pipeEX.DestRegID = destRegId;
                pipeEX.MicroOp = pipeID.MicroOp;
                pipeEX.OwnerThreadId = pipeID.MicroOp?.OwnerThreadId ?? 0;
                pipeEX.VirtualThreadId = pipeID.MicroOp?.VirtualThreadId ?? 0;
                pipeEX.OwnerContextId = pipeID.MicroOp?.OwnerContextId ?? 0;
                pipeEX.WasFspInjected = pipeID.MicroOp?.IsFspInjected ?? false;
                pipeEX.OriginalThreadId = pipeID.MicroOp?.OwnerThreadId ?? 0;
                pipeEX.AdmissionExecutionMode = pipeID.AdmissionExecutionMode;
                pipeEX.DomainTag = pipeID.MicroOp?.Placement.DomainTag ?? 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReserveSingleLaneExecuteMshrScoreboardSlot()
            {
                pipeEX.MshrScoreboardSlot = -1;
                pipeEX.MshrVirtualThreadId = pipeID.MicroOp?.VirtualThreadId ?? 0;
                if (pipeID.MicroOp is Core.LoadStoreMicroOp loadStoreMicroOp && _fspScheduler != null)
                {
                    int bankId = loadStoreMicroOp.MemoryBankId;
                    int vtId = loadStoreMicroOp.VirtualThreadId;
                    Core.ScoreboardEntryType entryType = loadStoreMicroOp is Core.LoadMicroOp
                        ? Core.ScoreboardEntryType.OutstandingLoad
                        : Core.ScoreboardEntryType.OutstandingStore;
                    int slot = _fspScheduler.SetSmtScoreboardPendingTyped(
                        bankId,
                        vtId,
                        (long)pipeCtrl.CycleCount,
                        entryType,
                        bankId);
                    pipeEX.MshrScoreboardSlot = slot;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteSingleLaneMicroOp()
            {
                if (pipeEX.MicroOp == null)
                    return false;

                try
                {
                    // MicroOp.Execute() is the current execution path; future work may route
                    // all execution through ExecutionDispatcherV4.Dispatch(InstructionIR, state).
                    bool success = ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp);

                    if (success)
                    {
                        pipeEX.VectorComplete = true;
                        pipeEX.ResultReady = true;
                        pipeEX.GeneratedEvent = MaterializeLaneGeneratedEvent(pipeEX.MicroOp);
                        pipeEX.GeneratedCsrEffect = MaterializeLaneCsrEffect(pipeEX.MicroOp);
                        pipeEX.GeneratedAtomicEffect = MaterializeLaneAtomicEffect(pipeEX.MicroOp);
                        pipeEX.GeneratedVmxEffect = MaterializeLaneVmxEffect(pipeEX.MicroOp);
                        if (pipeEX.GeneratedAtomicEffect.HasValue)
                        {
                            pipeEX.WritesRegister = false;
                        }

                        if (pipeEX.MicroOp is Core.TrapMicroOp)
                        {
                            pipeEX.IsMemoryOp = false;
                            pipeEX.IsLoad = false;
                            pipeEX.MemoryAddress = 0;
                        }
                        else if (pipeEX.MicroOp is Core.VectorMicroOp)
                        {
                            // Completed vector/stream MicroOps already consumed their
                            // memory-side semantics in the authoritative EX contour.
                            // Keep legacy single-lane MEM from reopening raw scalar
                            // load/store follow-through for the same carrier.
                            pipeEX.IsMemoryOp = false;
                        }

                        pipeEX.ResultValue = TryResolveLanePrimaryWriteBackValue(
                            pipeEX.MicroOp,
                            pipeEX.GeneratedCsrEffect,
                            out ulong writeBackValue)
                            ? writeBackValue
                            : 0;
                    }
                    else
                    {
                        if (pipeEX.MicroOp is Core.LoadMicroOp suppressedLoadMicroOp &&
                            suppressedLoadMicroOp.IsSpeculativeFaultSuppressed)
                        {
                            _ = ProjectSingleLaneScalarLoadSpeculativeSuppressionOutcome(
                                suppressedLoadMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.LoadMicroOp backpressuredLoadMicroOp &&
                            backpressuredLoadMicroOp.HasControllerAdmissionBackpressure)
                        {
                            _ = ProjectSingleLaneScalarLoadAdmissionBackpressureOutcome(
                                backpressuredLoadMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.LoadMicroOp loadMicroOp &&
                            loadMicroOp.OwnsPendingMemoryCompletion)
                        {
                            // RF-10.3 preserves the RF-07.2g retry disposition
                            // with a controller-qualified accepted request.
                            _ = ProjectSingleLaneScalarLoadRetryOutcome(
                                loadMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.LoadMicroOp deniedLoadMicroOp &&
                            deniedLoadMicroOp.HasNonSpeculativeFallbackBackendDenial(this))
                        {
                            RejectSingleLaneScalarLoadFallbackBackend(
                                deniedLoadMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.StoreMicroOp suppressedStoreMicroOp &&
                            suppressedStoreMicroOp.IsSpeculativeFaultSuppressed)
                        {
                            // RF-07.2l observes the existing FSP faulted-store
                            // carrier. It intentionally preserves the legacy
                            // occupied/not-ready lane: FSP owns later squash and
                            // resource cleanup, not this execute projection.
                            _ = ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                                suppressedStoreMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.StoreMicroOp backpressuredStoreMicroOp &&
                            backpressuredStoreMicroOp.HasControllerAdmissionBackpressure)
                        {
                            _ = ProjectSingleLaneScalarStoreAdmissionBackpressureOutcome(
                                backpressuredStoreMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.StoreMicroOp storeMicroOp &&
                            storeMicroOp.OwnsPendingWriteCompletion)
                        {
                            // RF-10.5 preserves the RF-07.2i retry disposition
                            // with a controller-qualified accepted readiness request.
                            _ = ProjectSingleLaneScalarStoreRetryOutcome(
                                storeMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.StoreMicroOp invalidStoreMicroOp &&
                            invalidStoreMicroOp.HasInvalidTransferSize)
                        {
                            RejectSingleLaneScalarStoreInvalidSize(
                                invalidStoreMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.StoreMicroOp deniedStoreMicroOp &&
                            deniedStoreMicroOp.HasNonSpeculativeFallbackBackendDenial(this))
                        {
                            RejectSingleLaneScalarStoreFallbackBackend(
                                deniedStoreMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.LoadSegmentMicroOp backpressuredLoadSegmentMicroOp &&
                            backpressuredLoadSegmentMicroOp.HasControllerAdmissionBackpressure)
                        {
                            _ = ProjectSingleLaneLoadSegmentAdmissionBackpressureOutcome(
                                backpressuredLoadSegmentMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.LoadSegmentMicroOp loadSegmentMicroOp &&
                            loadSegmentMicroOp.OwnsPendingMemoryCompletion)
                        {
                            // RF-07.2f migrates only the real LoadSegment async
                            // completion wait. Every other generic false remains
                            // on its inventoried legacy adapter.
                            _ = ProjectSingleLaneLoadSegmentRetryOutcome(
                                loadSegmentMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.VectorTransferMicroOp backpressuredVectorTransferMicroOp &&
                            backpressuredVectorTransferMicroOp.HasControllerAdmissionBackpressure)
                        {
                            _ = ProjectSingleLaneVectorTransferAdmissionBackpressureOutcome(
                                backpressuredVectorTransferMicroOp,
                                success);
                        }
                        else if (pipeEX.MicroOp is Core.VectorTransferMicroOp vectorTransferMicroOp &&
                            vectorTransferMicroOp.OwnsPendingMemoryCompletion)
                        {
                            _ = ProjectSingleLaneVectorTransferRetryOutcome(
                                vectorTransferMicroOp,
                                success);
                        }

                        // Preserve the legacy not-ready carrier and timing.
                        pipeEX.ResultReady = false;
                        pipeEX.GeneratedEvent = null;
                        pipeEX.GeneratedCsrEffect = null;
                        pipeEX.GeneratedAtomicEffect = null;
                        pipeEX.GeneratedVmxEffect = null;
                        ScalarExecuteLaneState stalledLane = pipeEX.GetLane(pipeEX.ActiveLaneIndex);
                        stalledLane.GeneratedRetireRecordCount = 0;
                        stalledLane.GeneratedRetireRecord0 = default;
                        stalledLane.GeneratedRetireRecord1 = default;
                        pipeEX.SetLane(pipeEX.ActiveLaneIndex, stalledLane);
                    }

                    pipeEX.Valid = true;
                    PublishExecuteCompletionContourCertificate(
                        Core.PipelineContourOwner.SingleLaneMicroOpExecution,
                        Core.PipelineContourVisibilityStage.Execute,
                        pipeEX.PC,
                        (byte)(1 << pipeEX.ActiveLaneIndex));
                    ConsumeDecodeStateAfterExecuteDispatch();
                    PublishSingleLaneExecuteForwarding(includeTimingMetadata: true);
                    return true;
                }
                catch (Core.UnsupportedExecutionSurfaceException exception)
                    when (exception.SurfaceName == ScalarLoadFallbackBackendSurfaceName ||
                          exception.SurfaceName == ScalarStoreFallbackBackendSurfaceName)
                {
                    // The RF-07.2h/2j adapters have already projected their typed
                    // denial and performed RF-00 cleanup. Preserve that public
                    // carrier; the generic exception tail must not reclassify it.
                    throw;
                }
                catch (Core.Execution.ExecutionOutcomeContractViolationException exception)
                    when (exception.Message.Contains(ScalarStoreInvalidSizeDispositionMarker, StringComparison.Ordinal))
                {
                    // RF-07.2k has already validated the FatalInvariantViolation
                    // and performed the sole lane/MSHR cleanup. This retained
                    // InvalidInternalOp carrier must not enter the generic tail.
                    throw;
                }
                catch (Core.PageFaultException pageFaultException)
                {
                    Core.Execution.ExecutionOutcome outcome =
                        ProjectSingleLanePageFaultOutcome(pageFaultException);
                    DeliverSingleLanePageFaultOutcome(outcome, pageFaultException);
                    throw;
                }
                catch (Core.Memory.MemoryAlignmentException memoryAlignmentException)
                {
                    Core.PageFaultException alignmentFault = new(
                        memoryAlignmentException.Message,
                        memoryAlignmentException,
                        memoryAlignmentException.Address,
                        isWrite: !IsAtomicReadOnlyAlignmentFaultCarrier(pipeEX.MicroOp));
                    Core.Execution.ExecutionOutcome outcome =
                        ProjectSingleLaneAlignmentFaultOutcome(
                            memoryAlignmentException,
                            alignmentFault);
                    DeliverSingleLaneAlignmentFaultOutcome(outcome, alignmentFault);
                    throw alignmentFault;
                }
                catch (Exception ex) when (pipeID.IsVectorOp)
                {
                    Core.Execution.ExecutionOutcome outcome =
                        ProjectSingleLaneNonFaultExceptionOutcome(ex);
                    FailCloseSingleLaneExecuteAfterNonFaultException();

                    if (outcome.Diagnostic!.LegacyFaultCategory is Core.ExecutionFaultCategory category)
                    {
                        throw Core.ExecutionFaultContract.CreateWrappedException(
                            category,
                        $"Vector opcode 0x{pipeID.OpCode:X} reached reference raw execute fallback after MicroOp failure. " +
                        "Mainline vector/stream execution must not collapse into direct StreamEngine execution or silent continue outside the MicroOp-owned runtime contour; pipeline execution remains authoritative through the explicit MicroOp lane.",
                        ex);
                    }

                    throw new InvalidOperationException(
                        $"Vector opcode 0x{pipeID.OpCode:X} reached reference raw execute fallback after MicroOp failure. " +
                        "Mainline vector/stream execution must not collapse into direct StreamEngine execution or silent continue outside the MicroOp-owned runtime contour; pipeline execution remains authoritative through the explicit MicroOp lane.",
                        ex);
                }
                catch (Exception ex)
                {
                    Core.Execution.ExecutionOutcome outcome =
                        ProjectSingleLaneNonFaultExceptionOutcome(ex);
                    FailCloseSingleLaneExecuteAfterNonFaultException();

                    Core.ExecutionFaultCategory category =
                        outcome.Diagnostic!.LegacyFaultCategory ??
                        Core.ExecutionFaultCategory.InvalidInternalOp;

                    throw Core.ExecutionFaultContract.CreateWrappedException(
                        category,
                        $"Scalar opcode 0x{pipeID.OpCode:X} threw a non-fault MicroOp exception inside the authoritative pipeline runtime contour. " +
                        "Production execution no longer falls back to reference raw execution for this failure class.",
                        ex);
                }
            }

            private const string ScalarLoadFallbackBackendSurfaceName =
                "ScalarLoadFallbackBackend";

            private const string ScalarStoreFallbackBackendSurfaceName =
                "ScalarStoreFallbackBackend";

            private const string ScalarStoreInvalidSizeDispositionMarker =
                "ScalarStoreInvalidSize";

            // RF-07.2h owns only the non-speculative LoadMicroOp false for which
            // both the bound async subsystem and an exact synchronous range are
            // absent. The public exception remains a fail-closed compatibility
            // carrier after typed projection; it is not the outcome authority.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectSingleLaneScalarLoadFallbackBackend(
                Core.LoadMicroOp microOp,
                bool legacySuccess)
            {
                Core.Execution.ExecutionOutcome outcome =
                    ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                        microOp,
                        this,
                        legacySuccess);

                byte laneIndex = pipeEX.ActiveLaneIndex;
                ulong pc = pipeEX.PC;

                FailCloseSingleLaneExecuteAfterNonFaultException();

                throw new Core.UnsupportedExecutionSurfaceException(
                    ScalarLoadFallbackBackendSurfaceName,
                    laneIndex,
                    microOp.OpCode,
                    pc,
                    new InvalidOperationException(outcome.Diagnostic!.Reason));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarLoadBackendUnavailableOutcome(
                Core.LoadMicroOp microOp,
                Processor.CPU_Core core,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasNonSpeculativeFallbackBackendDenial(core))
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadMicroOp false cannot be projected as BackendUnavailable without exact non-speculative no-subsystem/no-range evidence.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownBackendUnavailable(
                        legacySuccess,
                        $"Scalar load at 0x{microOp.Address:X} for {microOp.Size} byte(s) has neither a bound asynchronous memory subsystem nor an exact synchronous main-memory range");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.BackendUnavailable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.RuntimeBackendUnavailable ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The scalar-load fallback-denial projection requires a no-effect BackendUnavailable outcome.");
                }

                return outcome;
            }

            // RF-07.2o is an additive observation of the legacy FSP faulted
            // load carrier. It neither squashes nor releases the occupied lane.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarLoadSpeculativeSuppressionOutcome(
                Core.LoadMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.IsSpeculativeFaultSuppressed || legacySuccess)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadMicroOp speculative-suppression projection requires a false valid FSP-faulted observation.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.ExecutionOutcome.StructuralBlocked(
                        Core.Execution.ExecutionDiagnostic.SpeculativeFaultSuppressed(
                            "Scalar load is faulted under the existing FSP speculative-suppression lifecycle; scheduler-owned squash remains pending."));
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.StructuralBlocked ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.SpeculativeFaultSuppressed ||
                    outcome.Result is not null || outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The scalar-load speculative-suppression projection requires a no-effect StructuralBlocked outcome.");
                }

                return outcome;
            }

            // RF-07.2j owns only the non-speculative StoreMicroOp false for
            // which both the bound async subsystem and an exact synchronous
            // range are absent. This is a bounded behavior fix: RF-00 cleanup
            // releases the occupied lane/MSHR before the retained public carrier.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectSingleLaneScalarStoreFallbackBackend(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                Core.Execution.ExecutionOutcome outcome =
                    ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                        microOp,
                        this,
                        legacySuccess);

                byte laneIndex = pipeEX.ActiveLaneIndex;
                ulong pc = pipeEX.PC;

                FailCloseSingleLaneExecuteAfterNonFaultException();

                throw new Core.UnsupportedExecutionSurfaceException(
                    ScalarStoreFallbackBackendSurfaceName,
                    laneIndex,
                    microOp.OpCode,
                    pc,
                    new InvalidOperationException(outcome.Diagnostic!.Reason));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarStoreBackendUnavailableOutcome(
                Core.StoreMicroOp microOp,
                Processor.CPU_Core core,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasNonSpeculativeFallbackBackendDenial(core))
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "StoreMicroOp false cannot be projected as BackendUnavailable without exact non-speculative no-subsystem/no-range evidence.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownBackendUnavailable(
                        legacySuccess,
                        $"Scalar store at 0x{microOp.Address:X} for {microOp.Size} byte(s) has neither a bound asynchronous memory subsystem nor an exact synchronous main-memory range");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.BackendUnavailable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.RuntimeBackendUnavailable ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The scalar-store fallback-denial projection requires a no-effect BackendUnavailable outcome.");
                }

                return outcome;
            }

            // RF-07.2k owns only the StoreMicroOp false produced by its four-way
            // transfer-size switch. A malformed runtime carrier is neither a
            // resource wait nor a backend denial; it fails closed before any
            // architectural publication. Speculation does not downgrade this
            // structural invariant violation into a silent fault suppression.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RejectSingleLaneScalarStoreInvalidSize(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                Core.Execution.ExecutionOutcome outcome =
                    ProjectSingleLaneScalarStoreInvalidSizeOutcome(microOp, legacySuccess);

                FailCloseSingleLaneExecuteAfterNonFaultException();

                throw new Core.Execution.ExecutionOutcomeContractViolationException(
                    $"{ScalarStoreInvalidSizeDispositionMarker}: {outcome.Diagnostic!.Reason}");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarStoreInvalidSizeOutcome(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasInvalidTransferSize)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "StoreMicroOp false cannot be projected as FatalInvariantViolation without invalid transfer-size evidence.");
                }

                if (legacySuccess)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "A completed StoreMicroOp observation cannot be projected as an invalid transfer-size FatalInvariantViolation.");
                }

                var malformedCarrier = new Core.Execution.ExecutionOutcomeContractViolationException(
                    $"StoreMicroOp transfer size {microOp.Size} is invalid; only 1, 2, 4, or 8 bytes are executable.");
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(malformedCarrier);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.FatalInvariantViolation ||
                    outcome.Diagnostic is not
                    {
                        Code: Core.Execution.ExecutionDiagnosticCode.ExistingExecutionFault,
                        LegacyFaultCategory: Core.ExecutionFaultCategory.InvalidInternalOp,
                    } ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The scalar-store invalid-size projection requires a no-effect InvalidInternalOp FatalInvariantViolation.");
                }

                return outcome;
            }

            // RF-07.2l owns only a valid-size scalar store that has already
            // entered the existing FSP faulted state and returned false. It does
            // not deliver an architectural fault, publish a retire effect, or
            // run FSP cleanup; the outcome is evidence for the legacy silent
            // squash owner, not a replacement lifecycle.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.IsSpeculativeFaultSuppressed)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "StoreMicroOp false cannot be projected as speculative suppression without valid-size speculative faulted evidence.");
                }

                if (legacySuccess)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "A completed StoreMicroOp observation cannot be projected as speculative suppression.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.ExecutionOutcome.StructuralBlocked(
                        Core.Execution.ExecutionDiagnostic.SpeculativeFaultSuppressed(
                            "Scalar store is faulted under the existing FSP speculative-suppression lifecycle; scheduler-owned squash remains pending."));
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.StructuralBlocked ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.SpeculativeFaultSuppressed ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The scalar-store speculative-suppression projection requires a no-effect StructuralBlocked outcome.");
                }

                return outcome;
            }

            // RF-07.2g owns only the scalar LoadMicroOp false that follows an
            // exact accepted request and remains pending. RF-07.2h owns the
            // separately proven scalar fallback denial; speculative fault
            // suppression, StoreMicroOp waits/denials and every explicit-packet/
            // MEM contour retain their separate adapters.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarLoadRetryOutcome(
                Core.LoadMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.OwnsPendingMemoryCompletion)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadMicroOp false cannot be projected as Retryable without its exact owned pending read completion.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "LoadMicroOp owns an exact asynchronous read completion that remains pending");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane scalar-load wait adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            // RF-10.3 partitions the distinct no-ID controller-admission wait.
            // Backpressure mutates no controller/request state and therefore
            // cannot borrow the accepted-request retry proof above.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarLoadAdmissionBackpressureOutcome(
                Core.LoadMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasControllerAdmissionBackpressure)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadMicroOp admission false cannot be projected as Retryable without exact no-ID controller backpressure.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "LoadMicroOp observed finite-capacity controller admission backpressure without receiving a request identity");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane scalar-load admission wait requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            // RF-10.5 partitions no-ID controller admission backpressure from
            // the accepted-request wait below.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarStoreAdmissionBackpressureOutcome(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasControllerAdmissionBackpressure)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "StoreMicroOp admission false cannot be projected as Retryable without exact no-ID controller backpressure.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "StoreMicroOp observed finite-capacity controller admission backpressure without receiving a request identity");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane scalar-store admission wait requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            // RF-07.2i now owns only StoreMicroOp false after the concrete
            // operation has accepted its exact controller readiness request.
            // Fallback denial, invalid size, speculative suppression and
            // explicit-packet stores retain separate adapters.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneScalarStoreRetryOutcome(
                Core.StoreMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.OwnsPendingWriteCompletion)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "StoreMicroOp false cannot be projected as Retryable without its exact owned pending write completion.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "StoreMicroOp owns an exact controller readiness completion that remains pending");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane scalar-store wait adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            // RF-07.2f/RF-10.6 owns only LoadSegmentMicroOp false after that
            // concrete operation has acquired its controller-qualified request.
            // The current single-lane carrier has no ScheduledOperation/
            // GeneratedStaticBinding identity, so this projection must not
            // fabricate a mutable attempt record.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneLoadSegmentRetryOutcome(
                Core.LoadSegmentMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.OwnsPendingMemoryCompletion)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadSegmentMicroOp false cannot be projected as Retryable without an owned pending memory completion.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "LoadSegmentMicroOp owns a controller vector-read completion that remains pending");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane LoadSegment wait adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneLoadSegmentAdmissionBackpressureOutcome(
                Core.LoadSegmentMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasControllerAdmissionBackpressure || legacySuccess)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "LoadSegmentMicroOp admission backpressure requires a false no-ID controller observation.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "LoadSegmentMicroOp controller ingress is full and allocated no request identity");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The LoadSegment admission-backpressure adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneVectorTransferRetryOutcome(
                Core.VectorTransferMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.OwnsPendingMemoryCompletion)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "VectorTransferMicroOp false requires an owned controller completion.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "VectorTransferMicroOp owns a controller source-read completion that remains pending");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The VectorTransfer wait adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome ProjectSingleLaneVectorTransferAdmissionBackpressureOutcome(
                Core.VectorTransferMicroOp microOp,
                bool legacySuccess)
            {
                ArgumentNullException.ThrowIfNull(microOp);
                if (!microOp.HasControllerAdmissionBackpressure || legacySuccess)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "VectorTransferMicroOp backpressure requires a false no-ID controller observation.");
                }

                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectKnownRetry(
                        legacySuccess,
                        "VectorTransferMicroOp controller ingress is full and allocated no request identity");
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.Retryable ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.ResourceWait ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The VectorTransfer backpressure adapter requires a no-effect Retryable outcome.");
                }

                return outcome;
            }

            // RF-07.1 owns only the non-fault exception tail of the legacy
            // single-lane compatibility contour. This carrier has no existing
            // ScheduledOperation/GeneratedStaticBinding, so it must not create
            // a parallel attempt identity or mutable attempt state. Except for the
            // bounded RF-07.2f LoadSegment and RF-07.2g scalar-load waits, generic
            // false, direct page/alignment delivery, and every explicit-packet or
            // other memory family retain their prior adapters until bounded slices.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectSingleLaneNonFaultExceptionOutcome(
                Exception exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.FatalInvariantViolation ||
                    outcome.Diagnostic is null)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane non-fault exception adapter requires FatalInvariantViolation.");
                }

                return outcome;
            }

            // RF-07.2a migrates only the direct PageFaultException catch in the
            // same legacy single-lane compatibility contour. Stage-aware fault
            // winner selection and delivery remain owned by the existing helper.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectSingleLanePageFaultOutcome(
                Core.PageFaultException exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic is not
                    {
                        Code: Core.Execution.ExecutionDiagnosticCode.PageFault,
                        FaultAddress: ulong faultAddress,
                        FaultIsWrite: bool faultIsWrite,
                    } ||
                    faultAddress != exception.FaultAddress ||
                    faultIsWrite != exception.IsWrite)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane page-fault adapter requires an exact ArchitecturalFault diagnostic.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverSingleLanePageFaultOutcome(
                Core.Execution.ExecutionOutcome outcome,
                Core.PageFaultException exception)
            {
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.PageFault)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Only a typed PageFault ArchitecturalFault may enter the single-lane stage-aware delivery adapter.");
                }

                DeliverStageAwareExecutePageFault(exception);
            }

            // RF-07.2b migrates only the immediately following direct
            // MemoryAlignmentException catch. Its pre-existing translation to a
            // PageFaultException remains the only delivery carrier; this adapter
            // merely fail-closes unless its typed AlignmentFault diagnostic
            // exactly agrees with that translated carrier.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectSingleLaneAlignmentFaultOutcome(
                Core.Memory.MemoryAlignmentException exception,
                Core.PageFaultException translatedFault)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(
                        exception,
                        translatedFault.IsWrite);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic is not
                    {
                        Code: Core.Execution.ExecutionDiagnosticCode.AlignmentFault,
                        FaultAddress: ulong faultAddress,
                        FaultIsWrite: bool faultIsWrite,
                    } ||
                    faultAddress != translatedFault.FaultAddress ||
                    faultIsWrite != translatedFault.IsWrite)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The single-lane alignment-fault adapter requires an exact ArchitecturalFault diagnostic.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverSingleLaneAlignmentFaultOutcome(
                Core.Execution.ExecutionOutcome outcome,
                Core.PageFaultException translatedFault)
            {
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.AlignmentFault)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Only a typed AlignmentFault ArchitecturalFault may enter the single-lane stage-aware delivery adapter.");
                }

                DeliverStageAwareExecutePageFault(translatedFault);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteExplicitPacketAssistLane(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                ref int executedPhysicalLaneCount)
            {
                if (!lane.MicroOp!.IsAssist)
                    return false;

                bool success = ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp);
                lane = pipeEX.GetLane(laneIndex);

                ApplyExplicitPacketAssistExecutionOutcome(
                    laneIndex,
                    ref lane,
                    success,
                    ref executedPhysicalLaneCount);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RecordExecuteLaneTraceEvent(byte laneIndex, ScalarExecuteLaneState lane)
            {
                HybridCPU_ISE.Core.TraceSink? traceSink = Processor.TraceSink;
                if (traceSink == null)
                    return;

                var evt = new HybridCPU_ISE.Core.TraceEvent(
                    (long)lane.PC,
                    (int)(lane.PC / 256),
                    (int)((lane.PC % 256) / 32),
                    lane.OpCode)
                {
                    Result = lane.ResultValue,
                    ExceptionCount = this.ExceptionStatus.TotalExceptions()
                };

                traceSink.Record(evt);

                if (traceSink.ShouldCaptureFullState)
                {
                    traceSink.RecordPhaseAwareState(
                        new HybridCPU_ISE.Core.FullStateTraceEvent
                        {
                            PC = (long)lane.PC,
                            BundleId = (int)(lane.PC / 256),
                            OpIndex = (int)((lane.PC % 256) / 32),
                            Opcode = lane.OpCode,
                            ThreadId = lane.WasFspInjected ? 0 : lane.OwnerThreadId,
                            CycleNumber = (long)pipeCtrl.CycleCount,
                            RegisterFile = CaptureTraceRegisterFile(lane.OwnerThreadId),
                            PredicateRegisters = CaptureTracePredicateRegisters(),
                            WasStolenSlot = lane.WasFspInjected,
                            OriginalThreadId = lane.OriginalThreadId,
                            PipelineStage = $"EX-L{laneIndex}",
                            Stalled = pipeCtrl.Stalled,
                            StallReason = PipelineStallText.Render(pipeCtrl.StallReason, PipelineStallTextStyle.Trace),
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldUseExplicitPacketGenericMicroOpExecutionContour(
                byte laneIndex,
                Core.MicroOp microOp)
            {
                return laneIndex < 4 ||
                    microOp is Core.MatrixTileMicroOp ||
                    microOp is Core.TrapMicroOp ||
                    (laneIndex == 7 &&
                     !microOp.IsMemoryOp &&
                     (!microOp.IsControlFlow || microOp is Core.TrapMicroOp));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyExplicitPacketGenericMicroOpExecutionOutcome(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                bool success,
                ref int executedPhysicalLaneCount,
                ref int executedScalarLaneCount)
            {
                lane.ResultReady = success;
                lane.GeneratedEvent = success
                    ? MaterializeLaneGeneratedEvent(lane.MicroOp)
                    : null;
                lane.GeneratedCsrEffect = success
                    ? MaterializeLaneCsrEffect(lane.MicroOp)
                    : null;
                lane.GeneratedAtomicEffect = success
                    ? MaterializeLaneAtomicEffect(lane.MicroOp)
                    : null;
                lane.GeneratedVmxEffect = success
                    ? MaterializeLaneVmxEffect(lane.MicroOp)
                    : null;
                if (lane.GeneratedAtomicEffect.HasValue)
                {
                    lane.WritesRegister = false;
                }

                if (success && lane.MicroOp is Core.TrapMicroOp)
                {
                    // Canonical-known trapped auxiliaries may carry memory/control
                    // admission facts for placement truth, but once the trap event
                    // materializes they must not reopen LSU/control follow-through
                    // in downstream MEM.
                    lane.IsMemoryOp = false;
                    lane.IsLoad = false;
                    lane.MemoryAccessSize = 0;
                    lane.MemoryAddress = 0;
                }
                else if (success && lane.MicroOp is Core.VectorMicroOp)
                {
                    // Completed vector/stream MicroOps already own their memory-side
                    // follow-through in EX. Downstream MEM must not reopen legacy
                    // scalar load/store handling for the same authoritative carrier.
                    lane.IsMemoryOp = false;
                }

                if (success)
                {
                    if (lane.MicroOp is Core.MatrixTileMicroOp)
                    {
                        // MatrixTile owns its side effects through the MicroOp capture
                        // recorded in EX and published only by WB-retire.  If the
                        // MicroOp-owned execution path mutates the core while capturing,
                        // keep the typed lane-6 carrier occupied so the existing
                        // EX->MEM->WB handoff can retire it.  This does not authorize
                        // ordinary lane-6 LSU/DMA/generic work.
                        lane.IsOccupied = true;
                        lane.LaneIndex = laneIndex;
                    }

                    lane.VectorComplete = true;
                    lane.ResultValue = TryResolveLanePrimaryWriteBackValue(
                        lane.MicroOp,
                        lane.GeneratedCsrEffect,
                        out ulong writeBackValue)
                        ? writeBackValue
                        : 0;
                }
                else
                {
                    lane.GeneratedRetireRecordCount = 0;
                    lane.GeneratedRetireRecord0 = default;
                    lane.GeneratedRetireRecord1 = default;
                }

                pipeEX.SetLane(laneIndex, lane);
                if (laneIndex < 4)
                    executedScalarLaneCount++;
                executedPhysicalLaneCount++;

                // Stage 6 Phase E: per-lane EX trace
                RecordExecuteLaneTraceEvent(laneIndex, lane);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteExplicitPacketGenericMicroOpLane(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                ref int executedPhysicalLaneCount,
                ref int executedScalarLaneCount)
            {
                if (!ShouldUseExplicitPacketGenericMicroOpExecutionContour(laneIndex, lane.MicroOp!))
                    return false;

                // MicroOp.Execute() is the current execution path; future work may route
                // all execution through ExecutionDispatcherV4.Dispatch(InstructionIR, state).
                bool success = ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!);
                lane = pipeEX.GetLane(laneIndex);

                ApplyExplicitPacketGenericMicroOpExecutionOutcome(
                    laneIndex,
                    ref lane,
                    success,
                    ref executedPhysicalLaneCount,
                    ref executedScalarLaneCount);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FailCloseSingleLaneExecuteAfterNonFaultException()
            {
                ReleaseExecuteStageLaneAwareBookkeeping();
                pipeEX.Clear();
                forwardEX.Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void FailCloseExplicitPacketLaneAfterNonFaultExecutionException(
                ref ScalarExecuteLaneState lane,
                Exception exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    ProjectExplicitPacketNonFaultExceptionOutcome(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.FatalInvariantViolation ||
                    outcome.Diagnostic is null)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The explicit-packet non-fault adapter requires FatalInvariantViolation.");
                }

                byte laneIndex = lane.LaneIndex;
                ReleaseScalarLaneBookkeeping(lane);

                ScalarExecuteLaneState clearedLane = new();
                clearedLane.Clear(laneIndex);
                lane = clearedLane;
                pipeEX.SetLane(laneIndex, clearedLane);
            }

            // RF-07.2e owns only the explicit-packet non-fault exception tail.
            // Existing RF-00 cleanup and the public exception adapter retain
            // authority after this fail-closed typed projection.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectExplicitPacketNonFaultExceptionOutcome(
                Exception exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.FatalInvariantViolation ||
                    outcome.Diagnostic is null)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The explicit-packet non-fault projection requires FatalInvariantViolation.");
                }

                return outcome;
            }
        }
    }
}
