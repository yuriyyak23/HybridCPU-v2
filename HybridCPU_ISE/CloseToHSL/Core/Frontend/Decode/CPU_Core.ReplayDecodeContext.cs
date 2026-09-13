using System;
using System.Globalization;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            private const string ReplayDecoderEpoch = "rf09-context-epoch-v1";
            private const string ReplayDecoderVersion = "VliwDecoderV4-canonical";
            private CanonicalDecodeContext CaptureReplayDecodeContext()
            {
                int vtId = ReadActiveVirtualThreadId();
                if (ArchContexts == null ||
                    (uint)vtId >= (uint)ArchContexts.Length ||
                    ArchContexts[vtId] == null)
                {
                    return CanonicalDecodeContext.Unbound;
                }

                Processor.MainMemoryArea mainMemory = GetBoundMainMemory();
                Core.Registers.ArchContextState archContext = ArchContexts[vtId];
                string privilegeContext = string.Create(
                    CultureInfo.InvariantCulture,
                    $"vt={vtId};priv={(int)archContext.CurrentPrivilege};vmx-non-root={(archContext.IsVmxNonRoot ? 1 : 0)};vmx-root={(IsVMXRoot ? 1 : 0)}");
                string domainIdentity = string.Create(
                    CultureInfo.InvariantCulture,
                    $"core={CoreID};vt={vtId};pod={CsrPodId:X16};mem-cert={CsrMemDomainCert:X16}");
                string addressSpaceIdentity = string.Create(
                    CultureInfo.InvariantCulture,
                    $"physical-memory-instance={mainMemory.ReplayAddressSpaceIdentity};vmx-root={(IsVMXRoot ? 1 : 0)};vmx-non-root={(archContext.IsVmxNonRoot ? 1 : 0)}");
                string vectorConfiguration = string.Create(
                    CultureInfo.InvariantCulture,
                    $"vl={VectorConfig.VL:X16};vtype={VectorConfig.VTYPE:X16};tail={VectorConfig.TailAgnostic};mask={VectorConfig.MaskAgnostic};fsp-enabled={VectorConfig.FSP_Enabled};fsp-mask={VectorConfig.FSP_StealMask:X2};fsp-policy={VectorConfig.FSP_Policy}");

                return new CanonicalDecodeContext
                {
                    ManifestVersion = GeneratedIsaCatalog.ManifestVersion,
                    ManifestHash = GeneratedIsaCatalog.ManifestSha256,
                    ExtensionConfigurationFingerprint =
                        $"generated-catalog:{GeneratedIsaCatalog.ManifestSha256};frontend:VliwDecoderV4;selection:unconditional",
                    DecoderEpoch = ReplayDecoderEpoch,
                    DecoderVersion = ReplayDecoderVersion,
                    PrivilegeContext = privilegeContext,
                    DomainIdentity = domainIdentity,
                    AddressSpaceIdentity = addressSpaceIdentity,
                    VectorConfigurationFingerprint = vectorConfiguration,
                    ExecutableMemoryInvalidationEpoch = mainMemory.ReplayRelevantMutationEpoch,
                    CodeGenerationEpoch = _replayCodeGenerationEpoch,
                    IsReplayEligible = mainMemory.ReplayAddressSpaceIdentity != 0,
                };
            }

            private void AdvanceReplayCodeGenerationEpoch()
            {
                _replayCodeGenerationEpoch = checked(_replayCodeGenerationEpoch + 1UL);
                _replaySemanticShadowLookup?.Invalidate();
            }

            private ReplaySemanticShadowObservation ObserveReplaySemanticShadow(
                CanonicalBundle liveCanonicalBundle)
            {
                _replaySemanticShadowLookup ??= new ReplaySemanticShadowLookup();
                return _replaySemanticShadowLookup.ObserveLiveDecode(
                    liveCanonicalBundle);
            }

            private void SynchronizeReplayRelevantMemoryEpoch()
            {
                ulong currentEpoch = GetBoundMainMemory().ReplayRelevantMutationEpoch;
                if (currentEpoch == _observedReplayRelevantMemoryEpoch)
                {
                    return;
                }

                InvalidateAllVliwFetchState(
                    Core.ReplayPhaseInvalidationReason.CertificateMutation,
                    advanceCodeGenerationEpoch: true);
                _observedReplayRelevantMemoryEpoch = currentEpoch;
            }

            private bool IsFetchedDecodeContextCurrent()
            {
                CanonicalDecodeContext? fetchedContext = pipeIF.DecodeContext;
                return fetchedContext != null &&
                       fetchedContext.IsReplayEligible &&
                       fetchedContext == CaptureReplayDecodeContext();
            }

#if TESTING
            internal CanonicalDecodeContext TestCaptureReplayDecodeContext() =>
                CaptureReplayDecodeContext();

            internal ulong TestReplayCodeGenerationEpoch =>
                _replayCodeGenerationEpoch;

            internal ReplaySemanticShadowMetrics TestReplaySemanticShadowMetrics =>
                _replaySemanticShadowLookup?.Metrics ?? default;
#endif
        }
    }
}
