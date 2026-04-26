using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class RetainedCompatDecoderTestFactory
{
    internal static IDecoderFrontend CreateRetainedCompatDecoder()
        => new VliwCompatDecoderV4();
}
