using HybridCPU.Compiler.Core.IR;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class H17CrossProjectExitGateTests
{
    [Fact]
    public void ProductionSecure_RemainsUnavailableUntilIndependentCrossProjectMatrixPasses()
    {
        var runtime = new HybridCpuExternalRuntime();
        HybridCpuExternalFeatureDescriptor secure =
            runtime.QueryFeatures().GetFeature(HybridCpuExternalFeatureFamily.SecureDomains);

        Assert.Equal(HybridCpuExternalFeatureAvailability.Unavailable, secure.Availability);
        Assert.NotEqual(HybridCpuExternalFeatureAvailability.ProductionSecure, secure.Availability);
    }

    [Fact]
    public void CompilerHandoff_IsVersionedSemanticOnlyAndContainsNoRuntimeAuthorityIdentity()
    {
        Assert.Equal(2, IrExternalOperationSemanticContract.Version);
        string[] forbidden =
        [
            "Cxl", "SingNext", "Fabric", "Topology", "Endpoint", "Hdm", "Dpa", "Bdf",
            "PhysicalAddress", "ProviderHandle", "SecureDomainHandle", "VirtualDomainHandle"
        ];
        Type[] types =
        [
            typeof(IrExternalOperationIntent),
            typeof(IrExternalOperationLoweringMetadata),
            typeof(IrExternalOperationDescriptorIdentity)
        ];

        Assert.All(types, type => Assert.DoesNotContain(type.GetProperties(), property =>
            forbidden.Any(fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
                property.PropertyType.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void SecureCompositionContracts_AreProviderNeutralAndDoNotCreateAuthorityRoots()
    {
        Type[] composition =
        [
            typeof(ExternalSecureExecutionBinding),
            typeof(ExternalSecureGuestRegionBinding),
            typeof(ExternalSecureVirtualIoBinding),
            typeof(ExternalSecureVirtualEventAuthorizationReceipt)
        ];

        Assert.All(composition, type =>
        {
            Assert.False(type.IsInterface);
            Assert.DoesNotContain(type.GetMethods(), method =>
                method.Name.Contains("Admit", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Release", StringComparison.OrdinalIgnoreCase));
        });
        Assert.True(typeof(IExternalSecureDomainProvider).IsInterface);
        Assert.True(typeof(IExternalSecureVirtualEventProvider).IsInterface);
    }
}
