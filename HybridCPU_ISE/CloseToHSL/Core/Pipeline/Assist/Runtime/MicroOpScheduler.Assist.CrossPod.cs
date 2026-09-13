using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryStealCrossPodAssistTransport(
            ushort requestingPodId,
            ulong requestedDomainTag,
            out AssistInterCoreTransport transport,
            PodController?[]? pods = null)
        {
            transport = default;
            if (requestingPodId == ushort.MaxValue || pods == null)
            {
                return false;
            }

            for (int podIndex = 0; podIndex < pods.Length; podIndex++)
            {
                PodController? donorPod = pods[podIndex];
                if (donorPod == null || donorPod.PodId == requestingPodId)
                {
                    continue;
                }

                for (int coreId = 0; coreId < PodController.CORES_PER_POD; coreId++)
                {
                    if (!donorPod.TryPeekInterCoreAssistTransport(
                            coreId,
                            out AssistInterCoreTransport candidateTransport))
                    {
                        continue;
                    }

                    if (IsInterCoreAssistTransportStale(donorPod, coreId, candidateTransport))
                    {
                        RecordAssistInterCoreReject(candidateTransport, requestingPodId);
                        donorPod.ClearInterCoreAssistTransport(coreId);
                        continue;
                    }

                    if (requestedDomainTag != 0)
                    {
                        InterCoreDomainGuardDecision domainGuard =
                            _runtimeLegalityService.EvaluateInterCoreDomainGuard(
                                candidateTransport.Seed,
                                requestedDomainTag);
                        RecordDomainIsolationProbe(domainGuard.ProbeResult);
                        if (!domainGuard.IsAllowed)
                        {
                            RecordAssistInterCoreReject(
                                candidateTransport,
                                requestingPodId,
                                isDomainReject: true);
                            continue;
                        }
                    }

                    if (donorPod.TryConsumeInterCoreAssistTransport(coreId, out transport))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
