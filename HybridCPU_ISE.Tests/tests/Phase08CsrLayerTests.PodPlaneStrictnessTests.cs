using Xunit;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor;

namespace HybridCPU_ISE.Tests.Phase08;

public sealed class PodPlaneStrictnessTests
{
    [Fact]
    public void ReadPodCSR_KnownAddress_ReturnsConfiguredValue()
    {
        var core = new CPU_Core(0)
        {
            CsrMemDomainCert = 0x1234_5678UL
        };

        Assert.Equal(0x1234_5678UL, core.ReadPodCSR(CPU_Core.CSR_MEM_DOMAIN_CERT));
    }

    [Fact]
    public void ReadPodCSR_UnknownAddress_ThrowsCsrUnknownAddressException()
    {
        var core = new CPU_Core(0);

        Assert.Throws<CsrUnknownAddressException>(
            () => core.ReadPodCSR(CPU_Core.CSR_NOC_ROUTE_CFG + 1));
    }
}
