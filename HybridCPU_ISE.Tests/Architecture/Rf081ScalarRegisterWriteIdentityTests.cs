using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf081ScalarRegisterWriteIdentityTests
{
    [Fact]
    public void Success_FreezesOneScalarWriteOntoExactExistingAttempt()
    {
        ExecutionRecord execution = CreateCompleted(
            virtualThreadId: 3,
            sourceBundleSerial: 101,
            sourceSlotIndex: 2,
            workingBundleSequence: 207,
            workingSlotIndex: 4,
            physicalLane: 1,
            effectCount: 1,
            out ScheduledOperation scheduled);
        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            execution,
            RetireVisibleEffectKind.RegisterWrite,
            effectOrdinal: 0,
            effectVirtualThreadId: 3,
            architecturalRegisterId: 11);

        ScalarRegisterWriteRetireEffect effect = ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.RegisterWrite(3, 11, 0xCAFEUL),
            identity);

        Assert.Same(identity, effect.Identity);
        Assert.Same(execution, effect.Identity.ExecutionRecord);
        Assert.Same(scheduled, effect.Identity.ScheduledOperation);
        Assert.Same(execution.GeneratedBinding, effect.Identity.GeneratedBinding);
        Assert.Equal(scheduled.OperationId, effect.Identity.OperationId);
        Assert.Equal((ulong)207, effect.Identity.WorkingBundleSequence);
        Assert.Equal(4, effect.Identity.WorkingSlotIndex);
        Assert.Equal((ulong)1, effect.Identity.OperationAttempt);
        Assert.Equal(3, effect.VirtualThreadId);
        Assert.Equal(11, effect.ArchitecturalRegisterId);
        Assert.Equal(0xCAFEUL, effect.Value);
    }

    [Fact]
    public void Negative_RejectsPcPayloadForeignVtRegisterMismatchAndX0()
    {
        ExecutionRecord execution = CreateCompleted(1, 12, 1, 30, 1, 2, 1, out _);
        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            execution,
            RetireVisibleEffectKind.RegisterWrite,
            0,
            1,
            architecturalRegisterId: 7);

        AssertViolation(() => ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.PcWrite(1, 0x1000), identity));
        AssertViolation(() => ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.RegisterWrite(2, 7, 1), identity));
        AssertViolation(() => ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.RegisterWrite(1, 8, 1), identity));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            execution,
            RetireVisibleEffectKind.RegisterWrite,
            0,
            1,
            architecturalRegisterId: 0));
    }

    [Fact]
    public void FaultAndDenial_CannotProduceScalarWriteEffect()
    {
        ExecutionRecord fault = CreateIssued(0, 15, 0, 40, 0, 0, out _);
        fault.ApplyTerminalTransition(fault.CreateTerminalTransition(
            ExecutionOutcome.ArchitecturalFault(
                ExecutionDiagnostic.PageFault(new PageFaultException(0x2000, false)))));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            fault, RetireVisibleEffectKind.RegisterWrite, 0, 0, 5));

        ExecutionRecord denial = CreateIssued(0, 16, 0, 41, 0, 0, out _);
        denial.ApplyTerminalTransition(denial.CreateTerminalTransition(
            ExecutionOutcome.BackendUnavailable(
                ExecutionDiagnostic.BackendUnavailable("RF-08.1 denial"))));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            denial, RetireVisibleEffectKind.RegisterWrite, 0, 0, 5));
    }

    [Fact]
    public void DuplicateTerminalEffect_IsRejectedByExistingAttemptOrdinalAuthority()
    {
        ExecutionRecord execution = CreateCompleted(2, 17, 3, 42, 3, 3, 1, out _);
        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            execution, RetireVisibleEffectKind.RegisterWrite, 0, 2, 6);
        ScalarRegisterWriteRetireEffect first = ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.RegisterWrite(2, 6, 10), identity);
        ScalarRegisterWriteRetireEffect duplicate = ScalarRegisterWriteRetireEffect.Freeze(
            RetireRecord.RegisterWrite(2, 6, 10), identity);

        AssertViolation(() => RetireVisibleEffectCoherence.ValidateDistinctTerminalEffects(
            [first.Identity, duplicate.Identity]));
    }

    [Fact]
    public void CarrierIsImmutableAndHasNoBackendOrPublicationAuthority()
    {
        Type carrier = typeof(ScalarRegisterWriteRetireEffect);
        Assert.DoesNotContain(
            carrier.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.SetMethod is not null);
        Assert.DoesNotContain(
            carrier.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName),
            method => method.Name.Contains("Retire", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Commit", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScalarTransportConsumesRf081CarrierButPublicationRemainsLegacy()
    {
        string root = FindRepositoryRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string carrierPath = Path.Combine(
            productionRoot,
            "Architecture", "Registers", "Retire", "Rf081ScalarRegisterWriteRetireEffect.cs");
        string[] consumers = Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, carrierPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                nameof(ScalarRegisterWriteRetireEffect), StringComparison.Ordinal))
            .ToArray();

        Assert.All(consumers, path => Assert.True(
            path.EndsWith("PostStageBIssuedAttempt.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("CPU_Core.PipelineExecution.Retire.cs", StringComparison.OrdinalIgnoreCase),
            $"Unexpected scalar-effect consumer: {path}"));
        string retire = File.ReadAllText(Path.Combine(
            productionRoot,
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs"));
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
    }

    private static ExecutionRecord CreateCompleted(
        int virtualThreadId,
        ulong sourceBundleSerial,
        int sourceSlotIndex,
        ulong workingBundleSequence,
        int workingSlotIndex,
        int physicalLane,
        int effectCount,
        out ScheduledOperation scheduled)
    {
        ExecutionRecord execution = CreateIssued(
            virtualThreadId,
            sourceBundleSerial,
            sourceSlotIndex,
            workingBundleSequence,
            workingSlotIndex,
            physicalLane,
            out scheduled);
        execution.ApplyTerminalTransition(execution.CreateTerminalTransition(
            ExecutionOutcome.Completed(
                ExecutionResultContract.WithoutScalarResult(effectCount))));
        return execution;
    }

    private static ExecutionRecord CreateIssued(
        int virtualThreadId,
        ulong sourceBundleSerial,
        int sourceSlotIndex,
        ulong workingBundleSequence,
        int workingSlotIndex,
        int physicalLane,
        out ScheduledOperation scheduled)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            (uint)IsaOpcodeValues.ADD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "rf08.1-scalar-register-write"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            readRegisters: [1, 2],
            writeRegisters: [5],
            isRetireVisible: true,
            isAssist: false);
        AdmissionRecord admission = AdmissionRecord.Create(
            new SourceOperationProvenance(
                SemanticInstructionKey.Create([1, 2, 5], "rf08.1", CanonicalDecodeContext.Unbound),
                virtualThreadId,
                sourceBundleSerial,
                SlotId.Create(sourceSlotIndex),
                fetchEpoch: 3),
            contract,
            virtualThreadId,
            ownerContextId: virtualThreadId + 20,
            domainTag: 31);
        scheduled = ScheduledOperation.CreateAfterStageB(
            admission,
            workingBundleSequence,
            workingSlotIndex,
            physicalLane,
            new OperationAttemptIssuer());
        return ExecutionRecord.Create(scheduled);
    }

    private static void AssertViolation(Action action) =>
        Assert.Throws<RetireEffectIdentityContractViolationException>(action);

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
