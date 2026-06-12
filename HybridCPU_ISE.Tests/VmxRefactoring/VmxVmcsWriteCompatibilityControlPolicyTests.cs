using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using YAKSys_Hybrid_CPU.Core.Vmx;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxVmcsWriteCompatibilityControlPolicyTests
{
    [Fact]
    public void VmcsProjectionSchema_KeepsEveryEntryWriteDenied()
    {
        Assert.NotEmpty(VmcsFieldProjectionSchema.Entries.ToArray());

        foreach (VmcsFieldProjectionSchemaEntry entry in VmcsFieldProjectionSchema.Entries)
        {
            Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));
            Assert.NotEqual(VmcsFieldProjectionAccessPolicy.Denied, entry.AccessPolicy);
        }
    }

    [Fact]
    public void CompatibilityControlFields_AreReadOnlyVocabularyButNoProjectedValues()
    {
        var service = new VmxCompatibilityAdmissionService();

        foreach (VmcsField field in CompatibilityControlFields())
        {
            Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
            Assert.Equal(VmcsFieldProjectionOwner.CompatibilityControlDescriptor, entry.Owner);
            Assert.Equal(EvidenceVisibilityClass.CompatibilityAlias, entry.EvidenceClass);
            Assert.Equal(VmcsFieldProjectionAccessPolicy.ReadOnly, entry.AccessPolicy);
            Assert.Equal(VmcsFieldProjectionMigrationPolicy.ProjectionOnly, entry.MigrationPolicy);
            Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));

            VmxCompatibilityVmReadAdmissionResult result =
                service.AdmitVmReadProjection(CreateVmReadRequest(field));

            Assert.Equal(VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied, result.Decision);
            Assert.True(result.RuntimeAdmissionAllowed);
            Assert.False(result.IsReadOnlyValueProjected);
            Assert.Equal(
                VmcsReadOnlyValueProjectionDecision.CompatibilityControlValueProjectionDenied,
                result.ValueProjection.Decision);
            Assert.Equal(VmcsV2ValidationCode.AccessDenied, result.VmcsValidation.Code);
            Assert.Equal(0, result.Value);
            Assert.Contains("no frozen VMX control-bit value projection contract", result.Reason);
        }
    }

    [Fact]
    public void CompatibilityControlDescriptor_DoesNotBecomeControlBitMapperOrWriteOwner()
    {
        CompatibilityControlDescriptor descriptor = CompatibilityControlDescriptor.FailClosedProjectionOnly;

        Assert.True(descriptor.IsRuntimeAuthoritativeControlOwner);
        Assert.True(descriptor.TryCreateReadOnlyControlView(
            out CompatibilityControlReadOnlyView view,
            out string reason));
        Assert.True(view.IsMaterialized);
        Assert.True(view.DeniesWrites);
        Assert.True(view.DeniesAuthoritativeMutation);
        Assert.True(view.KeepsControlValuesUnprojected);
        Assert.Contains("read-only control view", reason);

        string source = ReadProjectSource(
            "CloseToRTL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("CompatibilityControlValueProjectionDenied", source);
        Assert.Contains("KeepsControlValuesUnprojected", source);
        Assert.DoesNotContain("ProjectCompatibilityControl", source);
        Assert.DoesNotContain("VmcsFieldProjectionOwner.CompatibilityControlDescriptor =>", source);
        Assert.DoesNotContain("TryWriteControl", source);
        Assert.DoesNotContain("ControlBitMapper", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
    }

    [Fact]
    public void VmwriteDecodeVocabulary_DoesNotCreateSchemaWriteOrScalarWriteAuthority()
    {
        VmxCompatDecodeResult decode = new VmxCompatDecodeBoundary().Decode(
            new VmxCompatDecodeRequest(
                Opcode: IsaOpcodeValues.VMWRITE,
                Rd: 1,
                Rs1: 2,
                Rs2: 3,
                DescriptorValidated: true,
                CapabilityValidated: true,
                SchedulingValidated: true,
                NoEmissionValidated: true));

        Assert.True(decode.IsAllowed);
        Assert.Equal(VmxOperandForm.FieldSelectorAndValueRegisters, decode.Payload.OperandForm);

        foreach (VmcsField field in CompatibilityControlFields())
        {
            Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));

            VmcsFieldAliasResult writeAlias = new VmcsFieldAliasProjection().ValidateAccess(
                new VmcsFieldAliasRequest(
                    field,
                    VmcsFieldAliasAccess.Write,
                    entry.EvidenceClass,
                    entry.IsGeneratedAlias,
                    DescriptorValidated: true,
                    AllowWrite: true),
                CreateAliasEvidencePolicy());

            Assert.Equal(VmcsFieldAliasDecision.WriteDenied, writeAlias.Decision);
        }

        Assert.Null(typeof(VmcsV2Descriptor).GetMethod("TryWriteScalarField"));
    }

    [Fact]
    public void VmcsWritePolicySource_DoesNotIntroduceMutableVmcsStoreOrRuntimeManager()
    {
        string source = ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldAliasProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs",
            "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs");

        Assert.Contains("CanWrite(VmcsFieldProjectionSchemaEntry entry) => false", source);
        Assert.Contains("VmcsFieldAliasDecision.WriteDenied", source);
        Assert.Contains("VMCSv2 scalar projection cache was removed", source);
        Assert.DoesNotContain("TryWriteScalarField", source);
        Assert.DoesNotContain("WriteKnownScalar", source);
        Assert.DoesNotContain("_scalarValues", source);
        Assert.DoesNotContain("_scalarWritten", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VmxRuntimeManager", source);
    }

    private static VmcsField[] CompatibilityControlFields() =>
        new[]
        {
            VmcsField.PinBasedControls,
            VmcsField.ProcBasedControls,
            VmcsField.ExitControls,
            VmcsField.EntryControls,
            VmcsField.SecondaryProcControls,
        };

    private static VmxCompatibilityVmReadAdmissionRequest CreateVmReadRequest(
        VmcsField field) =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: CreateAliasEvidencePolicy(),
            Descriptor: null,
            FieldId: (ushort)field,
            DestinationRegister: 3,
            FieldSelectorRegister: 1,
            ReservedRegister: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: true);

    private static DomainRuntimeContext CreateContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0));

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);

    private static EvidencePolicyDescriptor CreateAliasEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false);

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
