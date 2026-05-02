using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcMatMulNoLegacyExecuteTests
{
    [Fact]
    public void L7SdcMatMulNoLegacyExecute_FakeBackendStagesResultOnly()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            MatMulDescriptor matMulDescriptor =
                L7SdcMatMulDescriptorTests.CreateMatMulDescriptor();
            AcceleratorCommandDescriptor commandDescriptor =
                L7SdcMatMulDescriptorTests.CreateCommandDescriptor(matMulDescriptor);

            WriteFloat32Matrix(0x1000, new[] { 1f, 2f, 3f, 4f });
            WriteFloat32Matrix(0x2000, new[] { 5f, 6f, 7f, 8f });
            byte[] originalDestination = Fill(0xCC, 16);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            var registry = new AcceleratorCapabilityRegistry();
            registry.RegisterProvider(new MatMulCapabilityProvider());
            AcceleratorCapabilityAcceptanceResult capability =
                registry.AcceptCapability(
                    MatMulCapabilityProvider.AcceleratorId,
                    commandDescriptor.OwnerBinding,
                    commandDescriptor.OwnerGuardDecision);
            var tokenStore = new AcceleratorTokenStore();
            AcceleratorTokenAdmissionResult tokenAdmission =
                tokenStore.Create(
                    commandDescriptor,
                    capability,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            Assert.True(tokenAdmission.IsAccepted, tokenAdmission.Message);

            var queue = new AcceleratorCommandQueue(
                capacity: 2,
                new AcceleratorDevice(MatMulCapabilityProvider.AcceleratorId));
            var backend = new FakeMatMulExternalAcceleratorBackend();
            backend.RegisterDescriptor(commandDescriptor, matMulDescriptor);
            var staging = new AcceleratorStagingBuffer();

            AcceleratorBackendResult submit =
                backend.TrySubmit(
                    new AcceleratorQueueAdmissionRequest
                    {
                        Descriptor = commandDescriptor,
                        CapabilityAcceptance = capability,
                        TokenAdmission = tokenAdmission
                    },
                    queue,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            AcceleratorBackendResult tick =
                backend.Tick(
                    queue,
                    new MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemory),
                    staging,
                    commandDescriptor.OwnerGuardDecision.Evidence);

            AcceleratorStagingReadResult staged =
                staging.GetStagedWriteSet(
                    tokenAdmission.Token!,
                    commandDescriptor.OwnerGuardDecision.Evidence);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsAccepted, tick.Message);
            Assert.Equal(AcceleratorBackendResultKind.DeviceCompleted, tick.Kind);
            Assert.False(tick.CanPublishArchitecturalMemory);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, tokenAdmission.Token!.State);
            Assert.True(staged.IsAccepted, staged.Message);
            Assert.Single(staged.StagedWrites);
            Assert.Equal(new[] { 19f, 22f, 43f, 50f }, ReadFloat32Vector(staged.StagedWrites[0].Data.ToArray()));
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, 16));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcMatMulNoLegacyExecute_FakeBackendRejectsDescriptorCapabilityMismatch()
    {
        MatMulDescriptor matMulDescriptor =
            L7SdcMatMulDescriptorTests.CreateMatMulDescriptor();
        AcceleratorCommandDescriptor commandDescriptor =
            L7SdcMatMulDescriptorTests.CreateCommandDescriptor(matMulDescriptor) with
            {
                CapabilityVersion = MatMulCapabilityProvider.CapabilityVersion + 1
            };

        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new MatMulCapabilityProvider());
        AcceleratorCapabilityAcceptanceResult capability =
            registry.AcceptCapability(
                MatMulCapabilityProvider.AcceleratorId,
                commandDescriptor.OwnerBinding,
                commandDescriptor.OwnerGuardDecision);
        var tokenStore = new AcceleratorTokenStore();
        AcceleratorTokenAdmissionResult tokenAdmission =
            tokenStore.Create(
                commandDescriptor,
                capability,
                commandDescriptor.OwnerGuardDecision.Evidence);
        Assert.True(tokenAdmission.IsAccepted, tokenAdmission.Message);

        var queue = new AcceleratorCommandQueue(
            capacity: 2,
            new AcceleratorDevice(MatMulCapabilityProvider.AcceleratorId));
        var backend = new FakeMatMulExternalAcceleratorBackend();

        AcceleratorBackendResult submit =
            backend.TrySubmit(
                new AcceleratorQueueAdmissionRequest
                {
                    Descriptor = commandDescriptor,
                    CapabilityAcceptance = capability,
                    TokenAdmission = tokenAdmission
                },
                queue,
                commandDescriptor.OwnerGuardDecision.Evidence);

        Assert.True(submit.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.BackendRejected, submit.FaultCode);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void L7SdcMatMulNoLegacyExecute_BuilderRejectsInexactGuardedReadBytes()
    {
        MatMulDescriptor matMulDescriptor =
            L7SdcMatMulDescriptorTests.CreateMatMulDescriptor();
        var validator = new MatMulDescriptorValidator();
        MatMulDescriptorValidationResult validation =
            validator.NormalizeFootprints(matMulDescriptor);
        Assert.True(validation.IsValid, validation.Message);

        AcceleratorMemoryRead[] reads =
        {
            new(validation.Footprint!.ARange, new byte[4]),
            new(validation.Footprint.BRange, new byte[(int)validation.Footprint.BRange.Length])
        };

        bool built = FakeMatMulExternalAcceleratorBackend.TryBuildStagedMatMulResultForTest(
            matMulDescriptor,
            validation.Footprint,
            reads,
            out byte[] stagedBytes,
            out string message);

        Assert.False(built);
        Assert.Empty(stagedBytes);
        Assert.Contains("exact source bytes", message, StringComparison.Ordinal);
    }

    [Fact]
    public void L7SdcMatMulNoLegacyExecute_ProductionPathDoesNotReferenceLegacyExecute()
    {
        string root = L7SdcPhase07TestFactory.ResolveRepoRoot();
        string externalAcceleratorPath = Path.Combine(
            root,
            "HybridCPU_ISE",
            "Core",
            "Execution",
            "ExternalAccelerators");

        string[] offenders = Directory.GetFiles(externalAcceleratorPath, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("ICustomAccelerator", StringComparison.Ordinal) ||
                       text.Contains("MatMulAccelerator", StringComparison.Ordinal) ||
                       text.Contains("YAKSys_Hybrid_CPU.Core.Accelerators", StringComparison.Ordinal) ||
                       text.Contains(".Execute(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static void WriteFloat32Matrix(ulong address, float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        for (int index = 0; index < values.Length; index++)
        {
            BitConverter.TryWriteBytes(
                bytes.AsSpan(index * sizeof(float), sizeof(float)),
                values[index]);
        }

        L7SdcPhase07TestFactory.WriteMainMemory(address, bytes);
    }

    private static float[] ReadFloat32Vector(byte[] bytes)
    {
        var values = new float[bytes.Length / sizeof(float)];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = BitConverter.ToSingle(
                bytes.AsSpan(index * sizeof(float), sizeof(float)));
        }

        return values;
    }

    private static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }
}
