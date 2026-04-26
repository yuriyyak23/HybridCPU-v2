using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentDecodedBundleRuntimeState(
                out Core.DecodedBundleRuntimeState runtimeState)
            {
                runtimeState = decodedBundleRuntimeState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentExecutionDecodedBundleRuntimeState(
                out Core.DecodedBundleRuntimeState runtimeState)
            {
                if (decodedBundleDerivedIssuePlanState.IsActive)
                {
                    decodedBundleDerivedIssuePlanState.ValidateAgainstBaseRuntimeState(
                        decodedBundleRuntimeState,
                        nameof(ReadCurrentExecutionDecodedBundleRuntimeState));
                    runtimeState = decodedBundleDerivedIssuePlanState.RuntimeState;
                    return;
                }

                runtimeState = decodedBundleRuntimeState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentForegroundDecodedBundleRuntimeState(
                out Core.DecodedBundleRuntimeState runtimeState)
            {
                ReadCurrentExecutionDecodedBundleRuntimeState(out runtimeState);
                runtimeState = runtimeState.ApplyProgressProjection(
                    ProjectCurrentDecodedBundleTransportFactsThroughProgress(
                        runtimeState.TransportFacts));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentDecodedBundleTransportFacts(
                out Core.DecodedBundleTransportFacts transportFacts)
            {
                ReadCurrentDecodedBundleRuntimeState(out Core.DecodedBundleRuntimeState runtimeState);
                transportFacts = runtimeState.TransportFacts;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentExecutionDecodedBundleTransportFacts(
                out Core.DecodedBundleTransportFacts transportFacts)
            {
                ReadCurrentExecutionDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState runtimeState);
                transportFacts = runtimeState.TransportFacts;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentForegroundDecodedBundleTransportFacts(
                out Core.DecodedBundleTransportFacts transportFacts)
            {
                ReadCurrentForegroundDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState runtimeState);
                transportFacts = runtimeState.TransportFacts;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentExecutionDecodedBundleTransportFacts(
                out ulong pc,
                out Core.DecodedBundleSlotDescriptor[] slots,
                out Core.DecodedBundleAdmissionPrep admissionPrep,
                out Core.DecodedBundleDependencySummary? dependencySummary)
            {
                ReadCurrentExecutionDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                pc = transportFacts.PC;
                slots = transportFacts.Slots;
                admissionPrep = transportFacts.AdmissionPrep;
                dependencySummary = transportFacts.DependencySummary;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentForegroundDecodedBundleTransportFacts(
                out ulong pc,
                out Core.DecodedBundleSlotDescriptor[] slots,
                out Core.DecodedBundleAdmissionPrep admissionPrep,
                out Core.DecodedBundleDependencySummary? dependencySummary)
            {
                ReadCurrentForegroundDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                pc = transportFacts.PC;
                slots = transportFacts.Slots;
                admissionPrep = transportFacts.AdmissionPrep;
                dependencySummary = transportFacts.DependencySummary;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentDecodedBundleProgressState(
                out Core.BundleProgressState progressState)
            {
                progressState = decodedBundleProgressState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReadCurrentDecodedBundleTransportFacts(
                out ulong pc,
                out Core.DecodedBundleSlotDescriptor[] slots,
                out Core.DecodedBundleAdmissionPrep admissionPrep,
                out Core.DecodedBundleDependencySummary? dependencySummary)
            {
                ReadCurrentDecodedBundleTransportFacts(
                    out Core.DecodedBundleTransportFacts transportFacts);
                pc = transportFacts.PC;
                slots = transportFacts.Slots;
                admissionPrep = transportFacts.AdmissionPrep;
                dependencySummary = transportFacts.DependencySummary;
            }

            internal Core.Decoder.DecodedInstructionBundle GetCurrentDecodedInstructionBundle()
            {
                return decodedBundleRuntimeState.CanonicalDecode;
            }

            internal Core.Legality.BundleLegalityDescriptor GetCurrentBundleLegalityDescriptor()
            {
                return decodedBundleRuntimeState.LegalityDescriptor;
            }


            internal Core.RuntimeClusterAdmissionPreparation GetDecodeStageAdmissionPreparation()
            {
                return pipeID.Valid
                    ? pipeIDAdmissionPreparation
                    : Core.RuntimeClusterAdmissionPreparation.CreateEmpty();
            }

            internal Core.RuntimeClusterAdmissionCandidateView GetDecodeStageAdmissionCandidateView()
            {
                return pipeID.Valid
                    ? pipeIDAdmissionCandidateView
                    : Core.RuntimeClusterAdmissionCandidateView.CreateEmpty(pipeID.PC);
            }

            internal Core.RuntimeClusterAdmissionDecisionDraft GetDecodeStageAdmissionDecisionDraft()
            {
                return pipeID.Valid
                    ? pipeIDAdmissionDecisionDraft
                    : Core.RuntimeClusterAdmissionDecisionDraft.CreateEmpty(pipeID.PC);
            }

            internal Core.RuntimeClusterAdmissionHandoff GetDecodeStageAdmissionHandoff()
            {
                return pipeID.Valid
                    ? pipeIDAdmissionHandoff
                    : Core.RuntimeClusterAdmissionHandoff.CreateEmpty(pipeID.PC);
            }

            internal Core.BundleIssuePacket GetDecodeStageIssuePacket()
            {
                return pipeID.Valid
                    ? pipeIDAdmissionHandoff.IssuePacket
                    : Core.BundleIssuePacket.CreateEmpty(pipeID.PC);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.DecodedBundleTransportFacts BuildDecodedBundleTransportFacts(
                ulong pc,
                IReadOnlyList<Core.MicroOp?> carrierBundle,
                Core.DecodedBundleStateKind stateKind = Core.DecodedBundleStateKind.ForegroundMutated,
                Core.DecodedBundleStateOrigin stateOrigin = Core.DecodedBundleStateOrigin.ForegroundBundlePublication)
            {
                return Core.DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                    pc,
                    carrierBundle,
                    stateKind,
                    stateOrigin);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.DecodedBundleTransportFacts BuildDecodedBundleTransportFacts(
                ulong pc,
                IReadOnlyList<Core.MicroOp?> carrierBundle,
                Core.DecodedBundleDependencySummary? dependencySummary,
                Core.DecodedBundleStateKind stateKind = Core.DecodedBundleStateKind.ForegroundMutated,
                Core.DecodedBundleStateOrigin stateOrigin = Core.DecodedBundleStateOrigin.ForegroundBundlePublication)
            {
                return Core.DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                    pc,
                    carrierBundle,
                    dependencySummary,
                    stateKind,
                    stateOrigin);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.DecodedBundleTransportFacts BuildDecodedBundleTransportFacts(
                ulong pc,
                Core.Decoder.DecodedInstructionBundle canonicalBundle,
                IReadOnlyList<Core.MicroOp?> carrierBundle,
                Core.DecodedBundleDependencySummary? dependencySummary)
            {
                return Core.DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                    pc,
                    carrierBundle,
                    canonicalBundle,
                    dependencySummary);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.DecodedBundleRuntimeState BuildCanonicalDecodedBundleState(
                System.ReadOnlySpan<VLIW_Instruction> rawSlots,
                Core.Decoder.IDecoderFrontend decoderFrontend,
                global::HybridCPU_ISE.Arch.VliwBundleAnnotations? bundleAnnotations,
                ulong pc,
                ulong bundleSerial = 0)
            {
                ArgumentNullException.ThrowIfNull(decoderFrontend);

                Core.Decoder.DecodedInstructionBundle canonicalBundle =
                    decoderFrontend.DecodeInstructionBundle(
                        rawSlots,
                        bundleAnnotations,
                        pc,
                        bundleSerial);
                Core.Legality.BundleLegalityDescriptor legalityDescriptor =
                    new Core.Legality.BundleLegalityAnalyzer().Analyze(canonicalBundle);
                Core.DecodedBundleTransportFacts transportFacts =
                    Core.Decoder.DecodedBundleTransportProjector.BuildCanonicalTransportFacts(
                        rawSlots,
                        canonicalBundle,
                        legalityDescriptor.DependencySummary);
                return Core.DecodedBundleRuntimeState.CreateCanonical(
                    canonicalBundle,
                    legalityDescriptor,
                    transportFacts);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong AllocateDecodedBundleStateEpoch()
            {
                return ++decodedBundleStateEpochCounter;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong AllocateDecodedBundleStateVersion()
            {
                return ++decodedBundleStateVersionCounter;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.DecodedBundleRuntimeState StampBaseRuntimeStatePublication(
                in Core.DecodedBundleRuntimeState runtimeState)
            {
                ulong stateEpoch = AllocateDecodedBundleStateEpoch();
                ulong stateVersion = AllocateDecodedBundleStateVersion();
                return runtimeState.WithPublicationIdentity(
                    Core.DecodedBundleStateOwnerKind.BaseRuntimePublication,
                    stateEpoch,
                    stateVersion,
                    stateVersion);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.DecodedBundleRuntimeState StampDerivedIssuePlanPublication(
                in Core.DecodedBundleRuntimeState runtimeState)
            {
                Core.DecodedBundleRuntimeState baseRuntimeState = decodedBundleRuntimeState;
                if (baseRuntimeState.StateOwnerKind != Core.DecodedBundleStateOwnerKind.BaseRuntimePublication ||
                    baseRuntimeState.StateEpoch == 0 ||
                    baseRuntimeState.StateVersion == 0)
                {
                    throw new InvalidOperationException(
                        "Derived issue-plan publication requires an already stamped base decoded-bundle runtime state.");
                }

                if (runtimeState.BundlePc != baseRuntimeState.BundlePc)
                {
                    throw new InvalidOperationException(
                        $"Derived issue-plan publication for bundle PC 0x{runtimeState.BundlePc:X} must match base bundle PC 0x{baseRuntimeState.BundlePc:X}.");
                }

                if (runtimeState.BundleSerial != baseRuntimeState.BundleSerial)
                {
                    throw new InvalidOperationException(
                        $"Derived issue-plan publication for bundle serial {runtimeState.BundleSerial} must match base bundle serial {baseRuntimeState.BundleSerial}.");
                }

                ulong stateVersion = AllocateDecodedBundleStateVersion();
                return runtimeState.WithPublicationIdentity(
                    Core.DecodedBundleStateOwnerKind.DerivedIssuePlanPublication,
                    baseRuntimeState.StateEpoch,
                    stateVersion,
                    baseRuntimeState.StateVersion);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentDecodedBundleRuntimeState(
                in Core.DecodedBundleRuntimeState runtimeState,
                bool preserveCurrentProgress = false)
            {
                Core.DecodedBundleRuntimeState publishedRuntimeState =
                    StampBaseRuntimeStatePublication(runtimeState);
                decodedBundleRuntimeState = publishedRuntimeState;
                ClearCurrentDerivedIssuePlan(publishedRuntimeState.BundlePc);
                SyncCurrentDecodedBundleProgressState(
                    publishedRuntimeState.TransportFacts,
                    preserveCurrentProgress);
                PublishDecodePublicationContourCertificate(publishedRuntimeState.TransportFacts);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentDerivedIssuePlanRuntimeState(
                in Core.DecodedBundleRuntimeState runtimeState,
                bool preserveCurrentProgress = false)
            {
                Core.DecodedBundleRuntimeState publishedRuntimeState =
                    StampDerivedIssuePlanPublication(runtimeState);
                decodedBundleDerivedIssuePlanState =
                    Core.DecodedBundleDerivedIssuePlanState.CreateActive(publishedRuntimeState);
                decodedBundleDerivedIssuePlanState.ValidateAgainstBaseRuntimeState(
                    decodedBundleRuntimeState,
                    nameof(PublishCurrentDerivedIssuePlanRuntimeState));
                SyncCurrentDecodedBundleProgressState(
                    publishedRuntimeState.TransportFacts,
                    preserveCurrentProgress);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentResidualDecodedBundleSlotCarrierFromTransportFacts(
                in Core.DecodedBundleTransportFacts transportFacts,
                bool preserveCurrentProgress = false)
            {
                PublishCurrentDecodedBundleRuntimeState(
                    decodedBundleRuntimeState.RepublishTransport(transportFacts),
                    preserveCurrentProgress);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ResetCurrentDecodedBundleSlotCarrierState(ulong pc)
            {
                PublishCurrentDecodedBundleRuntimeState(
                    Core.DecodedBundleRuntimeState.CreateEmpty(pc));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearCurrentDerivedIssuePlan(ulong bundlePc = 0)
            {
                decodedBundleDerivedIssuePlanState =
                    Core.DecodedBundleDerivedIssuePlanState.CreateEmpty(bundlePc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.DecodedBundleTransportFacts ProjectCurrentDecodedBundleTransportFactsThroughProgress(
                in Core.DecodedBundleTransportFacts transportFacts)
            {
                Core.BundleProgressState progressState = decodedBundleProgressState;
                if (progressState.BundlePc != transportFacts.PC)
                {
                    progressState = Core.BundleProgressState.CreateForCursor(
                        transportFacts.PC,
                        transportFacts.ValidNonEmptyMask,
                        pipelineBundleSlot);
                    decodedBundleProgressState = progressState;
                }

                return Core.DecodedBundleSlotCarrierBuilder.BuildTransportFactsFromProgressProjection(
                    transportFacts,
                    progressState.RemainingMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SyncCurrentDecodedBundleProgressState(
                in Core.DecodedBundleTransportFacts transportFacts,
                bool preserveCurrentProgress)
            {
                byte remainingMask = transportFacts.ValidNonEmptyMask;
                if (preserveCurrentProgress &&
                    decodedBundleProgressState.BundlePc == transportFacts.PC)
                {
                    remainingMask = (byte)(decodedBundleProgressState.RemainingMask & remainingMask);
                }

                decodedBundleProgressState = Core.BundleProgressState.CreateForCursor(
                    transportFacts.PC,
                    remainingMask,
                    pipelineBundleSlot);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentFallbackDecodedBundleState(
                ulong pc,
                in Core.DecodedBundleTransportFacts transportFacts,
                ulong bundleSerial = 0)
            {
                pipeCtrl.DecodeFaultBundleCount++;
                PublishCurrentDecodedBundleRuntimeState(
                    Core.DecodedBundleRuntimeState.CreateFallback(
                        pc,
                        transportFacts,
                        bundleSerial));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentCanonicalDecodedBundleState(
                in Core.DecodedBundleRuntimeState runtimeState)
            {
                PublishCurrentDecodedBundleRuntimeState(runtimeState);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishCurrentReplayDecodedBundleState(
                in Core.DecodedBundleTransportFacts transportFacts,
                bool preserveCurrentProgress = false)
            {
                PublishCurrentDecodedBundleRuntimeState(
                    Core.DecodedBundleRuntimeState.CreateReplay(transportFacts),
                    preserveCurrentProgress);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LoadCurrentDecodedBundleSlotCarrierFromCarriers(
                ulong pc,
                IReadOnlyList<Core.MicroOp?> carrierBundle)
            {
                ArgumentNullException.ThrowIfNull(carrierBundle);

                // Loop-buffer replay remains decoder-free in phase 03, so replayed bundles should
                // refresh only the narrow foreground transport shell while leaving canonical decode facts empty.
                RefreshCurrentDecodedBundleSlotCarrierFromCarriers(pc, carrierBundle);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RefreshCurrentDecodedBundleSlotCarrierFromCarriers(
                ulong pc,
                IReadOnlyList<Core.MicroOp?> carrierBundle)
            {
                ArgumentNullException.ThrowIfNull(carrierBundle);

                // Replay/test bundle projections must refresh the foreground transport explicitly.
                // Runtime getters must not silently recover semantic truth from a hidden transport array.
                PublishCurrentReplayDecodedBundleState(
                    BuildDecodedBundleTransportFacts(
                        pc,
                        carrierBundle,
                        Core.DecodedBundleStateKind.Replay,
                        Core.DecodedBundleStateOrigin.ReplayBundleLoad));
            }

            /// <summary>
            /// Decode the fetched bundle through the canonical frontend/analyzer seam and then
            /// project the result into the foreground transport shell without persisting
            /// a broad descriptor shell or carrier array as runtime state.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DecodeFullBundle()
            {
                if (pipeIF.VLIWBundle == null)
                {
                    ResetCurrentDecodedBundleSlotCarrierState(pipeIF.PC);
                    return;
                }

                Span<VLIW_Instruction> rawSlots =
                    stackalloc VLIW_Instruction[Core.BundleMetadata.BundleSlotCount];
                if (!TryCopyFetchedBundleInstructionsUnchecked(pipeIF.VLIWBundle, rawSlots))
                {
                    ResetCurrentDecodedBundleSlotCarrierState(pipeIF.PC);
                    return;
                }

                if (!pipeIF.HasBundleAnnotations || pipeIF.BundleAnnotations == null)
                {
                    // Live fetched decode must consume explicit sideband ownership metadata
                    // from the ingress transport contract; missing annotations fail closed.
                    ResetCurrentDecodedBundleSlotCarrierState(pipeIF.PC);
                    return;
                }

                Core.Decoder.IDecoderFrontend decoderFrontend = Core.Decoder.DecoderFeatureFlags.CreateDecoder();
                Core.Decoder.VliwDecoderV4 slotDecoder =
                    decoderFrontend as Core.Decoder.VliwDecoderV4 ?? new Core.Decoder.VliwDecoderV4();
                VliwBundleAnnotations fetchedBundleAnnotations = pipeIF.BundleAnnotations;
                try
                {
                    var canonicalState =
                        BuildCanonicalDecodedBundleState(
                            rawSlots,
                            decoderFrontend,
                            fetchedBundleAnnotations,
                            pipeIF.PC,
                            bundleSerial: 0);
                    PublishCurrentCanonicalDecodedBundleState(canonicalState);
                    return;
                }
                catch (InvalidOpcodeException decodeException)
                {
                    pipeCtrl.DecodeFallbackCount++;
                    Core.DecodedBundleTransportFacts fallbackTransportFacts =
                        Core.Decoder.DecodedBundleTransportProjector.BuildFallbackTransportFacts(
                            rawSlots,
                            slotDecoder,
                            decodeException,
                            pipeIF.PC,
                            fetchedBundleAnnotations);
                    PublishCurrentFallbackDecodedBundleState(
                        pipeIF.PC,
                        fallbackTransportFacts);
                    return;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryReadFetchedBundleInstructionUnchecked(
                ReadOnlySpan<byte> source,
                int slotIndex,
                out VLIW_Instruction instruction)
            {
                instruction = default;

                if ((uint)slotIndex >= Core.BundleMetadata.BundleSlotCount)
                {
                    return false;
                }

                int offset = slotIndex * 32;
                if (source.Length - offset < 32)
                {
                    return false;
                }

                instruction = new VLIW_Instruction
                {
                    // Keep the fetched-bundle staging path raw: canonical decode owns the
                    // production ingress validation and the bundle-level fallback trap contour.
                    Word0 = BitConverter.ToUInt64(source.Slice(offset, 8)),
                    Word1 = BitConverter.ToUInt64(source.Slice(offset + 8, 8)),
                    Word2 = BitConverter.ToUInt64(source.Slice(offset + 16, 8)),
                    Word3 = BitConverter.ToUInt64(source.Slice(offset + 24, 8))
                };
                return true;
            }

            private static bool TryCopyFetchedBundleInstructionsUnchecked(
                ReadOnlySpan<byte> source,
                Span<VLIW_Instruction> destination)
            {
                if (destination.Length < Core.BundleMetadata.BundleSlotCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(destination),
                        $"Destination span must have room for {Core.BundleMetadata.BundleSlotCount} bundle slots.");
                }

                for (int slotIndex = 0; slotIndex < Core.BundleMetadata.BundleSlotCount; slotIndex++)
                {
                    if (!TryReadFetchedBundleInstructionUnchecked(source, slotIndex, out destination[slotIndex]))
                    {
                        return false;
                    }
                }

                return true;
            }

        }
    }
}

