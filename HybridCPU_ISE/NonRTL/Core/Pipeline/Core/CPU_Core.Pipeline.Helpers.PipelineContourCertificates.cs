using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ResetPipelineContourCertificates()
            {
                decodePublicationCertificate =
                    Core.PipelineContourCertificate.CreateEmpty(
                        Core.PipelineContourKind.DecodePublication);
                executeCompletionCertificate =
                    Core.PipelineContourCertificate.CreateEmpty(
                        Core.PipelineContourKind.ExecuteCompletion);
                retireVisibilityCertificate =
                    Core.PipelineContourCertificate.CreateEmpty(
                        Core.PipelineContourKind.RetireVisibility);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishDecodePublicationContourCertificate(
                in Core.DecodedBundleTransportFacts transportFacts)
            {
                decodePublicationCertificate =
                    Core.PipelineContourCertificate.CreatePublished(
                        Core.PipelineContourKind.DecodePublication,
                        Core.PipelineContourOwner.DecodedBundleTransportPublication,
                        Core.PipelineContourVisibilityStage.Decode,
                        transportFacts.PC,
                        transportFacts.ValidNonEmptyMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishExecuteCompletionContourCertificate(
                Core.PipelineContourOwner owner,
                Core.PipelineContourVisibilityStage visibilityStage,
                ulong pc,
                byte slotMask)
            {
                if (slotMask == 0)
                {
                    return;
                }

                executeCompletionCertificate =
                    Core.PipelineContourCertificate.CreatePublished(
                        Core.PipelineContourKind.ExecuteCompletion,
                        owner,
                        visibilityStage,
                        pc,
                        slotMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishRetireVisibilityContourCertificate(
                Core.PipelineContourOwner owner,
                Core.PipelineContourVisibilityStage visibilityStage,
                ulong pc,
                byte slotMask)
            {
                if (slotMask == 0)
                {
                    return;
                }

                retireVisibilityCertificate =
                    Core.PipelineContourCertificate.CreatePublished(
                        Core.PipelineContourKind.RetireVisibility,
                        owner,
                        visibilityStage,
                        pc,
                        slotMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte BuildRetireSlotMask(
                ReadOnlySpan<byte> retireOrder,
                int retireLaneCount)
            {
                byte slotMask = 0;
                for (int laneIndex = 0; laneIndex < retireLaneCount; laneIndex++)
                {
                    slotMask |= (byte)(1 << retireOrder[laneIndex]);
                }

                return slotMask;
            }
        }
    }
}
