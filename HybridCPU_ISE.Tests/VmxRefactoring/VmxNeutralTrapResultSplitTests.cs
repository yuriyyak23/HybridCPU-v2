using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using Xunit;

namespace HybridCPU_ISE.Tests;

public sealed class VmxNeutralTrapResultSplitTests
{
    [Fact]
    public void NeutralTrapResult_DoesNotDependOnVmxExitVocabulary()
    {
        TrapRequest request = TrapRequest.ForCompatibilityOperation(
            operation: 10,
            opcode: (ushort)Processor.CPU_Core.InstructionsEnum.VMCALL,
            vtId: 0,
            executionDomainTag: 3,
            addressSpaceTag: 4);

        NeutralTrapResult result = NeutralTrapResult.Trap(
            request,
            NeutralTrapResultKind.CompatibilityOperationIntercept);

        Assert.True(result.ShouldTrap);
        Assert.Equal(NeutralTrapResultKind.CompatibilityOperationIntercept, result.Kind);
        Assert.Equal(TrapTargetKind.CompatibilityOperation, result.Request.TargetKind);

        string runtimeTrapSource = ReadProjectSource(
            "CloseToRTL/Core/Runtime/Events/Traps/NeutralTrapResult.cs",
            "CloseToRTL/Core/Runtime/Events/Traps/TrapRequest.cs",
            "CloseToRTL/Core/Runtime/Events/Traps/TrapPolicyBitmap.cs",
            "CloseToRTL/Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs");

        Assert.DoesNotContain("VmExitReason", runtimeTrapSource);
        Assert.DoesNotContain("VmxExitQualification", runtimeTrapSource);
        Assert.DoesNotContain("TrapDecision", runtimeTrapSource);
    }

    [Fact]
    public void VmxTrapProjectionMapper_MapsNeutralResultAtCompatibilityBoundaryOnly()
    {
        var bitmap = new TrapPolicyBitmap();
        bitmap.EnableCompatibilityOperation((byte)VmxOperationKind.VmCall);

        TrapRequest request = TrapRequest.ForCompatibilityOperation(
            operation: (byte)VmxOperationKind.VmCall,
            opcode: (ushort)Processor.CPU_Core.InstructionsEnum.VMCALL,
            vtId: 1);

        NeutralTrapResult neutral = bitmap.Evaluate(request);

        Assert.True(neutral.ShouldTrap);
        Assert.Equal(NeutralTrapResultKind.CompatibilityOperationIntercept, neutral.Kind);

        TrapDecision projected = VmxTrapProjectionMapper.Default.Project(neutral);

        Assert.True(projected.ShouldExit);
        Assert.Equal(VmExitReason.VmCall, projected.ExitReason);
        Assert.Equal(request, projected.Request);
    }

    [Fact]
    public void TrapPolicyService_UsesMapperInsteadOfVmExitReasonAsAuthority()
    {
        string serviceSource = ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Events/TrapPolicyService.cs");
        string mapperSource = ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs");

        Assert.Contains("NeutralTrapResult", serviceSource);
        Assert.Contains("VmxTrapProjectionMapper.Default.Project", serviceSource);
        Assert.DoesNotContain("VmExitReason.", serviceSource);

        Assert.Contains("NeutralTrapResultKind.CompatibilityOperationIntercept", mapperSource);
        Assert.Contains("VmExitReason.VmxOperationIntercept", mapperSource);
    }

    [Fact]
    public void ProductionVmxRetirePaths_DoNotUseInterceptSuccessFactoriesAsAuthority()
    {
        string productionCallers = ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");

        Assert.Contains("VmxRetireEffect.Fault", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", productionCallers);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", productionCallers);
        Assert.DoesNotContain("VmcsManager", productionCallers);
        Assert.DoesNotContain("VmxExecutionUnit", productionCallers);
    }

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            projectRoot,
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
