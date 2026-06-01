namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class SecureComputeVmxPhase9NestedFenceTests
{
    [Fact]
    public void SecureComputeVmxNestedSources_KeepShadowVmcsAsCompatibilityBridgeOnly()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs");

        Assert.Contains("DeniedNestedVmcsAuthority", source);
        Assert.Contains("DeniedMutableShadowVmcsAuthority", source);
        Assert.Contains("CompatibilityBridgePath", source);
        Assert.Contains("IsRetirementFenced => true", source);
        Assert.Contains("compatibility nested admission cannot bypass", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmRead", source);
        Assert.DoesNotContain("VmWrite", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("new SecureComputeDomainDescriptor", source);
        Assert.DoesNotContain("AllowBackendExecution = true", source);
    }

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
