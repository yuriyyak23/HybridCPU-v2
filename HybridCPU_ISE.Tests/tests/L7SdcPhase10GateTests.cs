using System;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.CompilerTests;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcPhase10GateTests
{
    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void Phase10_AllAccelCarriersRemainFailClosedAndArchitecturalRegisterSilent(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });
        var core = new Processor.CPU_Core(0);
        const int observedRegister = 9;
        const ulong sentinel = 0xCAFE_BABE_1020_3040UL;
        core.WriteCommittedArch(0, observedRegister, sentinel);
        ulong before = core.ReadArch(0, observedRegister);

        Assert.Equal(expectedCarrierType, carrier.GetType());
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);
        Assert.Equal(expectedSerialization, carrier.SerializationClass);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));

        Assert.Equal(sentinel, before);
        Assert.Equal(sentinel, core.ReadArch(0, observedRegister));
        Assert.Contains(mnemonic, carrier.GetDescription(), StringComparison.Ordinal);
        Assert.Contains("direct execution is unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backend execution", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("staged write publication", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("architectural rd writeback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase10_SystemDeviceCarrierSourceDoesNotWireModelApisIntoExecution()
    {
        string source = ReadRepoFile(
            "HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs");

        Assert.Contains("WritesRegister = false;", source, StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = Array.Empty<int>();", source, StringComparison.Ordinal);
        Assert.Contains("direct execution is unsupported", source, StringComparison.Ordinal);

        Assert.DoesNotContain(nameof(AcceleratorRegisterAbi), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorTokenStore), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorCommandQueue), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorFenceCoordinator), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IExternalAcceleratorBackend), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FakeMatMulExternalAcceleratorBackend), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorCommitCoordinator), source, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase10_RegisterAbiPackingIsModelSideOnly()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorTokenLookupResult poll =
            fixture.Store.TryPoll(fixture.Token.Handle, fixture.Evidence);

        AcceleratorRegisterAbiResult submitAbi =
            AcceleratorRegisterAbi.FromSubmitAdmission(fixture.Admission);
        AcceleratorRegisterAbiResult pollAbi =
            AcceleratorRegisterAbi.FromStatusLookup(poll);
        AcceleratorRegisterAbiResult queryCapsAbi =
            AcceleratorRegisterAbi.FromCapabilityQuery(fixture.CapabilityAcceptance);

        var submitCarrier = new AcceleratorSubmitMicroOp(fixture.Descriptor);
        var pollCarrier = new AcceleratorPollMicroOp();
        var queryCapsCarrier = new AcceleratorQueryCapsMicroOp();

        Assert.True(submitAbi.WritesRegister);
        Assert.True(pollAbi.WritesRegister);
        Assert.True(queryCapsAbi.WritesRegister);
        Assert.NotEqual(0UL, submitAbi.RegisterValue);
        Assert.NotEqual(0UL, queryCapsAbi.RegisterValue);

        Assert.False(submitCarrier.WritesRegister);
        Assert.False(pollCarrier.WritesRegister);
        Assert.False(queryCapsCarrier.WritesRegister);
        Assert.Empty(submitCarrier.WriteRegisters);
        Assert.Empty(pollCarrier.WriteRegisters);
        Assert.Empty(queryCapsCarrier.WriteRegisters);
    }

    [Fact]
    public void Phase10_FakeMatMulBackendIsTestOnlyAndCannotPublishWithoutCommitCoordinator()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                Float32Bytes(1f, 2f, 3f, 4f));
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x2000,
                Float32Bytes(5f, 6f, 7f, 8f));
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0xCC, 16);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            MatMulDescriptor matMulDescriptor =
                L7SdcMatMulDescriptorTests.CreateMatMulDescriptor();
            AcceleratorCommandDescriptor commandDescriptor =
                L7SdcMatMulDescriptorTests.CreateCommandDescriptor(matMulDescriptor);
            AcceleratorCapabilityAcceptanceResult capability =
                CreateMatMulCapability(commandDescriptor);
            var tokenStore = new AcceleratorTokenStore();
            AcceleratorTokenAdmissionResult tokenAdmission =
                tokenStore.Create(
                    commandDescriptor,
                    capability,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            Assert.True(tokenAdmission.IsAccepted, tokenAdmission.Message);

            var disabledBackend = new FakeMatMulExternalAcceleratorBackend(
                featureSwitch: ExternalAcceleratorFeatureSwitch.BackendExecutionDisabled);
            var disabledQueue = new AcceleratorCommandQueue(
                capacity: 2,
                new AcceleratorDevice(MatMulCapabilityProvider.AcceleratorId));
            AcceleratorBackendResult disabledSubmit =
                disabledBackend.TrySubmit(
                    CreateQueueRequest(commandDescriptor, capability, tokenAdmission),
                    disabledQueue,
                    commandDescriptor.OwnerGuardDecision.Evidence);

            Assert.True(disabledBackend.IsTestOnly);
            Assert.True(disabledSubmit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.BackendExecutionUnavailable, disabledSubmit.FaultCode);
            Assert.Equal(0, disabledQueue.Count);
            Assert.Equal(AcceleratorTokenState.Created, tokenAdmission.Token!.State);

            var backend = new FakeMatMulExternalAcceleratorBackend();
            backend.RegisterDescriptor(commandDescriptor, matMulDescriptor);
            var queue = new AcceleratorCommandQueue(
                capacity: 2,
                new AcceleratorDevice(MatMulCapabilityProvider.AcceleratorId));
            var staging = new AcceleratorStagingBuffer();

            AcceleratorBackendResult submit =
                backend.TrySubmit(
                    CreateQueueRequest(commandDescriptor, capability, tokenAdmission),
                    queue,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            AcceleratorBackendResult tick =
                backend.Tick(
                    queue,
                    new MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemory),
                    staging,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            AcceleratorTokenLookupResult directCommit =
                tokenStore.TryCommitPublication(
                    tokenAdmission.Token.Handle,
                    commandDescriptor.OwnerGuardDecision.Evidence);

            Assert.True(backend.IsTestOnly);
            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsAccepted, tick.Message);
            Assert.Equal(AcceleratorBackendResultKind.DeviceCompleted, tick.Kind);
            Assert.False(tick.CanPublishArchitecturalMemory);
            Assert.False(tick.UserVisiblePublicationAllowed);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, tokenAdmission.Token.State);
            Assert.True(directCommit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, directCommit.FaultCode);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void Phase10_TokenQueueAndFenceApisAreModelSurfacesNotInstructionExecution()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue();

        AcceleratorQueueAdmissionResult queueResult =
            queue.TryEnqueue(
                fixture.CreateQueueAdmissionRequest(),
                fixture.Evidence);
        AcceleratorTokenLookupResult poll =
            fixture.Store.TryPoll(
                fixture.Token.Handle,
                fixture.Evidence);
        AcceleratorFenceResult fence =
            new AcceleratorFenceCoordinator().TryFence(
                fixture.Store,
                AcceleratorFenceScope.ForToken(fixture.Token.Handle),
                fixture.Evidence);

        Assert.True(queueResult.IsAccepted, queueResult.Message);
        Assert.False(queueResult.CanPublishArchitecturalMemory);
        Assert.False(queueResult.UserVisiblePublicationAllowed);
        Assert.False(queueResult.Command!.CanPublishArchitecturalMemory);
        Assert.True(poll.IsAllowed, poll.Message);
        Assert.False(poll.UserVisiblePublicationAllowed);
        Assert.True(fence.IsRejected);
        Assert.False(fence.CanPublishArchitecturalMemory);
        Assert.Equal(AcceleratorTokenState.Queued, fixture.Token.State);

        var core = new Processor.CPU_Core(0);
        Assert.Throws<InvalidOperationException>(
            () => new AcceleratorPollMicroOp().Execute(ref core));
        Assert.Throws<InvalidOperationException>(
            () => new AcceleratorFenceMicroOp().Execute(ref core));
    }

    [Fact]
    public void Phase10_CompilerEmissionIsCarrierSidebandNotProductionExecutableProtocol()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        HybridCpuThreadCompilerContext context =
            L7SdcCompilerEmissionTests.CreateContextForDescriptor(descriptor);

        CompilerAcceleratorLoweringDecision decision =
            context.CompileAcceleratorSubmit(
                IrAcceleratorIntent.ForMatMul(descriptor, tokenDestinationRegister: 9),
                CompilerAcceleratorCapabilityModel.ReferenceMatMul);
        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();

        Assert.True(decision.EmitsAcceleratorSubmit);
        Assert.Contains("native lane7 ACCEL_SUBMIT emission", decision.Reason, StringComparison.Ordinal);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(7);
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, (InstructionsEnum)lowered.OpCode);
        MicroOp projected = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            slotIndex: 7);
        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(projected);
        Assert.False(submit.WritesRegister);
        Assert.Empty(submit.WriteRegisters);

        var core = new Processor.CPU_Core(0);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => submit.Execute(ref core));
        Assert.Contains("fallback routing", ex.Message, StringComparison.OrdinalIgnoreCase);

        string compilerSource = ReadCombinedSources("HybridCPU_Compiler");
        Assert.DoesNotContain(nameof(AcceleratorTokenStore), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorCommandQueue), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorFenceCoordinator), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorRegisterAbi), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IExternalAcceleratorBackend), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FakeMatMulExternalAcceleratorBackend), compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AcceleratorCommitCoordinator), compilerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase10DocsAndAdrKeepCurrentContractModelOnlyAndFailClosed()
    {
        string phase10 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md");
        string adr10 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/ADR_10_External_Accelerator_L7_SDC_Gate.md");
        string combined = phase10 + Environment.NewLine + adr10;

        Assert.Contains("Current contract remains model-only", phase10, StringComparison.Ordinal);
        Assert.Contains("does not approve executable L7 ISA", adr10, StringComparison.Ordinal);
        Assert.Contains("direct execution remains fail-closed", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WritesRegister = false", combined, StringComparison.Ordinal);
        Assert.Contains("`AcceleratorRegisterAbi` is model-only result packing", combined, StringComparison.Ordinal);
        Assert.Contains("`FakeMatMulExternalAcceleratorBackend` is test-only", combined, StringComparison.Ordinal);
        Assert.Contains("Compiler/backend production lowering must not expect executable `ACCEL_*`", combined, StringComparison.Ordinal);
        Assert.Contains("Backend results must not publish memory outside an approved staged commit/retire boundary", combined, StringComparison.Ordinal);
    }

    private static AcceleratorCapabilityAcceptanceResult CreateMatMulCapability(
        AcceleratorCommandDescriptor commandDescriptor)
    {
        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new MatMulCapabilityProvider());
        return registry.AcceptCapability(
            MatMulCapabilityProvider.AcceleratorId,
            commandDescriptor.OwnerBinding,
            commandDescriptor.OwnerGuardDecision);
    }

    private static AcceleratorQueueAdmissionRequest CreateQueueRequest(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorCapabilityAcceptanceResult capability,
        AcceleratorTokenAdmissionResult tokenAdmission) =>
        new()
        {
            Descriptor = descriptor,
            CapabilityAcceptance = capability,
            TokenAdmission = tokenAdmission
        };

    private static byte[] Float32Bytes(params float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        for (int index = 0; index < values.Length; index++)
        {
            BitConverter.TryWriteBytes(
                bytes.AsSpan(index * sizeof(float), sizeof(float)),
                values[index]);
        }

        return bytes;
    }

    private static string ReadRepoFile(string relativePath)
    {
        string fullPath = Path.Combine(
            L7SdcPhase07TestFactory.ResolveRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing repository file: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    private static string ReadCombinedSources(string relativeDirectory)
    {
        string root = Path.Combine(
            L7SdcPhase07TestFactory.ResolveRepoRoot(),
            relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(root), $"Missing repository directory: {relativeDirectory}");
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
    }
}
