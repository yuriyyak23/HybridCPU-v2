using System;
using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests;

public sealed class VmcsV2MutableHelperAuthorityTests
{
    private const BindingFlags InstanceAnyVisibility =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [Fact]
    public void VmcsV2Header_LaunchAndInvalidationEpochAuthority_IsReadOnlyCompatibilityMetadata()
    {
        Type headerType = typeof(VmcsV2Header);

        foreach (string methodName in new[]
                 {
                     "MarkLaunched",
                     "ResetLaunchState",
                     "AdvanceInvalidationEpoch",
                 })
        {
            AssertNoInstanceMethod(headerType, methodName);
        }

        var header = new VmcsV2Header();
        Assert.True(header.IsReadOnlyCompatibilityProjection);
        Assert.False(header.IsLaunched);
        Assert.Equal(0UL, header.InvalidationEpoch);
        Assert.Null(headerType.GetProperty(nameof(VmcsV2Header.IsLaunched), InstanceAnyVisibility)?.SetMethod);
        Assert.Null(headerType.GetProperty(nameof(VmcsV2Header.InvalidationEpoch), InstanceAnyVisibility)?.SetMethod);
    }

    [Fact]
    public void VmcsV2Descriptor_GuestStateAndHostEvidenceMutators_AreRemovedWithoutReplacement()
    {
        Type descriptorType = typeof(VmcsV2Descriptor);

        Assert.Null(descriptorType.GetField("_hostEvidence", InstanceAnyVisibility));
        foreach (string methodName in new[]
                 {
                     "CaptureGuestStateEager",
                     "BeginLazyGuestStateSave",
                     "MaterializeLazyGuestRegisters",
                     "MaterializeVmExitGuestState",
                     "RecordHostEvidence",
                     "GuestVisibleStateContainsHostEvidence",
                     "DiscardHostEvidenceAfterRestore",
                     "ResetForClear",
                 })
        {
            AssertNoInstanceMethod(descriptorType, methodName);
        }

        var descriptor = VmcsV2Descriptor.CreateDefault();
        VmcsV2ValidationResult migration = descriptor.ValidateMigrationReadiness();
        Assert.Equal(VmcsV2ValidationCode.GuestGprPersistenceIncomplete, migration.Code);

        VmcsV2ValidationResult nested = descriptor.ValidateNestedEnablementReadiness();
        Assert.Equal(VmcsV2ValidationCode.GuestGprPersistenceIncomplete, nested.Code);

        Assert.False(descriptor.TryReadScalarField(
            (ushort)VmcsField.GuestPc,
            out long value,
            out VmcsV2ValidationResult validation));
        Assert.Equal(0, value);
        Assert.Equal(VmcsV2ValidationCode.AccessDenied, validation.Code);
    }

    [Fact]
    public void VirtualCpuBlock_CaptureAndMaterializationHelpers_AreRemovedWithoutReplacement()
    {
        Type blockType = typeof(VirtualCpuBlock);

        foreach (string methodName in new[]
                 {
                     "CaptureEager",
                     "BeginLazySave",
                     "TryMaterializeLazyRegisters",
                     "SnapshotGuestIntegerRegisters",
                 })
        {
            AssertNoInstanceMethod(blockType, methodName);
        }

        var block = new VirtualCpuBlock();
        Assert.Equal(VmcsV2GprPersistenceKind.None, block.GprPersistence);
        Assert.Equal(VirtualCpuBlock.IntegerRegisterCount, block.GuestIntegerRegisters.Count);
    }

    [Fact]
    public void VmcsV2RootNptAndBundleBindingHelpers_AreReadOnlyProjectionShells()
    {
        AssertNoInstanceMethod(typeof(VmxRootControlBlock), "BindRootDescriptor");
        AssertNoInstanceMethod(typeof(VmxRootControlBlock), "AdvanceEpoch");
        AssertNoInstanceMethod(typeof(VmxNptBlock), "BindControl");
        AssertNoInstanceMethod(typeof(BundleExecutionBlock), "BindBundle");

        var root = new VmxRootControlBlock();
        Assert.True(root.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, root.RootDescriptorAddress);
        Assert.Equal(0UL, root.OwnershipEpoch);

        var npt = new VmxNptBlock();
        Assert.True(npt.IsReadOnlyCompatibilityProjection);
        Assert.Equal(MemoryTranslationControl.Disabled, npt.LastControl);
        Assert.Equal(0UL, npt.ControlEpoch);

        var bundle = new BundleExecutionBlock();
        Assert.True(bundle.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, bundle.BundlePc);
        Assert.Equal(0UL, bundle.ExecutionEpoch);
    }

    [Fact]
    public void VmcsV2EventInjectionHelpers_AreRemovedWithoutVmcsOwnedQueueOrRemapState()
    {
        Type eventBlockType = typeof(EventInjectionBlock);
        Type interruptFabricBlockType = typeof(VirtualInterruptFabricBlock);

        Assert.Null(eventBlockType.GetField("_queue", InstanceAnyVisibility));
        Assert.Null(eventBlockType.GetField("_remap", InstanceAnyVisibility));
        Assert.Null(interruptFabricBlockType.GetProperty("Fabric", InstanceAnyVisibility));
        Assert.Null(eventBlockType.Assembly.GetType(
            "YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmxEventInjectionBlockSnapshot"));

        foreach (string methodName in new[]
                 {
                     "ConfigureInterruptRemap",
                     "RemoveInterruptRemap",
                     "ClearInterruptRemaps",
                     "TryQueue",
                     "TryDeliver",
                     "CreateSnapshot",
                     "RestoreSnapshot",
                     "AdvanceInjectionEpoch",
                 })
        {
            AssertNoInstanceMethod(eventBlockType, methodName);
        }

        var fabric = new VirtualInterruptFabricBlock();
        Assert.True(fabric.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, fabric.RoutingEpoch);

        var events = new EventInjectionBlock();
        Assert.True(events.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, events.InjectionEpoch);
        Assert.Equal(0UL, events.DeliveryEpoch);
        Assert.Equal(0, events.PendingCount);
        Assert.Equal(0UL, events.RemapPolicyEpoch);
        Assert.Equal(default, events.LastDelivered);
    }

    [Fact]
    public void VmcsV2DebugTraceHelpers_AreRemovedWithoutVmcsOwnedTracePlane()
    {
        Type blockType = typeof(DebugTraceBlock);

        Assert.Null(blockType.GetField("_plane", InstanceAnyVisibility));
        Assert.Null(blockType.GetProperty("Plane", InstanceAnyVisibility));
        foreach (string methodName in new[]
                 {
                     "ConfigureExport",
                     "RecordEvent",
                     "RecordFail",
                     "RecordAbort",
                     "RecordInvalidation",
                     "RecordDroppedPostedEvents",
                     "SnapshotCounters",
                     "ResetCounters",
                     "DiscardTraceHandles",
                     "AdvancePolicyEpoch",
                 })
        {
            AssertNoInstanceMethod(blockType, methodName);
        }

        var block = new DebugTraceBlock();
        Assert.True(block.IsReadOnlyCompatibilityProjection);
        Assert.False(block.ExportEnabled);
        Assert.Equal(0UL, block.PolicyEpoch);
        Assert.Equal(default, block.Counters);
        Assert.False(block.ContainsHostEvidence(VmcsV2HostEvidenceKind.DecodedBundleFacts));
    }

    [Fact]
    public void VmcsV2ResidualPrivateSetBlocks_AreReadOnlyProjectionShellsWithoutBackingState()
    {
        foreach (Type blockType in new[]
                 {
                     typeof(VectorStreamStateBlock),
                     typeof(DirtyLogBlock),
                     typeof(SecurityIsolationBlock),
                     typeof(CapabilityNegotiationBlock),
                 })
        {
            AssertNoInstanceFields(blockType);
            foreach (string methodName in new[]
                     {
                         "Configure",
                         "Bind",
                         "Record",
                         "Restore",
                         "Reset",
                         "AdvanceEpoch",
                     })
            {
                AssertNoInstanceMethod(blockType, methodName);
            }
        }

        var vector = new VectorStreamStateBlock();
        Assert.True(vector.IsReadOnlyCompatibilityProjection);
        Assert.False(vector.VirtualizationEnabled);
        Assert.Equal(VectorStreamSaveMask.None, vector.SaveRestoreMask);
        Assert.Equal(VectorExceptionAction.Accumulate, vector.ExceptionPolicy);
        Assert.Equal(VectorStreamStateBlock.DefaultMaxStreamLength, vector.MaxStreamLength);
        Assert.False(vector.HasDescriptorTable);
        Assert.False(vector.RequiresMigratableVectorState);
        Assert.True(vector.IsMigrationReady);
        Assert.Null(vector.LastSnapshot);
        Assert.Equal(VectorStreamDescriptorFaultInfo.None, vector.LastDescriptorFault);

        var dirty = new DirtyLogBlock();
        Assert.True(dirty.IsReadOnlyCompatibilityProjection);
        Assert.False(dirty.Enabled);
        Assert.False(dirty.Overflowed);
        Assert.Equal(VmxDirtyLogConfiguration.DefaultPageSize, dirty.PageSize);
        Assert.Equal(VmxDirtyLogConfiguration.DefaultMaxDirtyPages, dirty.MaxDirtyPages);
        Assert.Equal(VmxDirtyLogOverflowPolicy.FailClosed, dirty.OverflowPolicy);
        Assert.False(dirty.ContainsHostEvidence(VmcsV2HostEvidenceKind.TlbCacheContents));
        VmxDirtyLogStatus status = dirty.SnapshotStatus();
        Assert.False(status.Enabled);
        Assert.Equal(0UL, status.TotalAcceptedWrites);
        Assert.Equal(0, status.DirtyPageCount);

        var isolation = new SecurityIsolationBlock();
        Assert.True(isolation.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, isolation.IsolationEpoch);

        var capability = new CapabilityNegotiationBlock();
        Assert.True(capability.IsReadOnlyCompatibilityProjection);
        Assert.Equal(0UL, capability.CapabilityEpoch);
    }

    [Fact]
    public void ExitInfoBlock_RecordHelpers_AreInternalRetirePublicationProjectionOnly()
    {
        Type blockType = typeof(ExitInfoBlock);
        foreach (MethodInfo method in blockType.GetMethods(
                     BindingFlags.Instance |
                     BindingFlags.Public |
                     BindingFlags.DeclaredOnly))
        {
            Assert.True(method.IsSpecialName);
        }

        foreach (string methodName in new[]
                 {
                     "Configure",
                     "Bind",
                     "RestoreSnapshot",
                     "Reset",
                     "RecordVmxFail",
                     "RecordAbort",
                     "RecordInvalidation",
                 })
        {
            AssertNoInstanceMethod(blockType, methodName);
        }

        Assert.NotNull(blockType.GetMethod("RecordVectorException", InstanceAnyVisibility));
        Assert.NotNull(blockType.GetMethod("RecordStreamDescriptorFault", InstanceAnyVisibility));
        Assert.NotNull(blockType.GetMethod("RecordStreamReplayRequired", InstanceAnyVisibility));

        var descriptor = VmcsV2Descriptor.CreateDefault();
        var vectorInfo = new VectorStreamExceptionInfo(
            ExecutionDomainTag: 3,
            AddressSpaceTag: 4,
            OwnerVirtualThreadId: 1,
            ExceptionMask: 0x10,
            ExceptionPriority: 0x20,
            HighestExceptionIndex: 2,
            FaultingPc: 0x1000,
            FaultingLane: 7,
            FaultingOpcode: 0x44,
            VectorExceptionAction.ReflectAsCompatibilityExit,
            Sequence: 9);

        descriptor.RecordVectorExceptionExit(vectorInfo);
        Assert.Equal(VmExitReason.VectorException, descriptor.ExitInfo.ExitReason);
        Assert.Equal(vectorInfo.EncodeCompatibilityQualification(), descriptor.ExitInfo.ExitQualification);

        descriptor.RecordStreamDescriptorFaultExit(new VectorStreamDescriptorFaultInfo(
            ExecutionDomainTag: 3,
            AddressSpaceTag: 4,
            OwnerVirtualThreadId: 1,
            StreamDescriptorFaultKind.DescriptorDecodeFault,
            GuestDescriptorAddress: 0x2000,
            DescriptorLength: 64,
            StreamReplayEpoch: 5,
            Sequence: 6,
            Message: "decode fault"));
        Assert.Equal(VmExitReason.StreamDescriptorFault, descriptor.ExitInfo.ExitReason);
        Assert.Equal(0x2000UL, descriptor.ExitInfo.GuestPhysicalAddress);

        descriptor.RecordStreamReplayRequiredExit(
            ownerVirtualThreadId: 1,
            addressSpaceTag: 4,
            streamReplayEpoch: 77);
        Assert.Equal(VmExitReason.StreamReplayRequired, descriptor.ExitInfo.ExitReason);
        Assert.Equal((1UL << 16) | (4UL << 32), descriptor.ExitInfo.ExitQualification);
        Assert.Equal(77UL, descriptor.ExitInfo.GuestPhysicalAddress);
        Assert.Equal(0UL, descriptor.Header.InvalidationEpoch);
    }

    [Fact]
    public void ChildDomainIntentDescriptor_FieldStoreAndSnapshotRestore_AreRemoved()
    {
        Type descriptorType = typeof(ChildDomainIntentDescriptor);

        Assert.Null(descriptorType.GetField("_fields", InstanceAnyVisibility));
        Assert.Null(descriptorType.GetProperty("Generation", InstanceAnyVisibility));
        foreach (string methodName in new[]
                 {
                     "TryWriteIntentField",
                     "TryGetRawField",
                     "CreateSnapshot",
                     "RestoreSnapshot",
                     "AdvanceGeneration",
                 })
        {
            AssertNoInstanceMethod(descriptorType, methodName);
        }

        var descriptor = new ChildDomainIntentDescriptor(childIntentPointer: 0x4000);
        Assert.True(descriptor.IsReadOnlyCompatibilityProjection);
        Assert.False(descriptor.TryReadIntentField(
            VmcsV2BlockDirectory.CreateDefault(),
            ChildDomainIntentFieldIds.AddressSpaceTag,
            out long value,
            out ChildDomainIntentAccessResult result));
        Assert.Equal(0, value);
        Assert.Equal(ChildDomainIntentAccessDisposition.VmFail, result.Disposition);
        Assert.Contains("neutral runtime-owned nested intent state", result.Message);

        Type snapshotType = typeof(ChildDomainIntentSnapshot);
        Assert.NotNull(snapshotType.GetProperty("ChildIntentPointer"));
        Assert.Null(snapshotType.GetProperty("Generation"));
        Assert.Null(snapshotType.GetProperty("Fields"));
    }

    private static void AssertNoInstanceFields(Type type) =>
        Assert.Empty(type.GetFields(InstanceAnyVisibility));

    private static void AssertNoInstanceMethod(Type type, string methodName) =>
        Assert.DoesNotContain(
            type.GetMethods(InstanceAnyVisibility),
            method => method.Name == methodName);
}
