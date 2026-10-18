using HybridCPU.NonRTL.NominalSlotCorpus;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed partial class CpuBackedChildExecutionTests
{
    private static void ConfigureNominalBodyPresentation(System.Diagnostics.ProcessStartInfo start,
        string assemblyPath, string presentationPath)
    {
        var root = typeof(CpuBackedChildExecutionTests).GetMethod(start.Environment["HYBRIDCPU_NOMINAL_SLOT_METHOD"]!)!;
        HybridCPU.Compiler.NativeAot.NativeAotPresentedBodyV1 Row(System.Reflection.MethodBase method) =>
            new(method.DeclaringType!.IsNested ? method.DeclaringType.Name : method.DeclaringType.FullName!, method.Name, method.MetadataToken);
        var bodies = new System.Reflection.MethodBase[] { root,
            typeof(NominalDerived).GetConstructor(Type.EmptyTypes)!, typeof(NominalDerived).GetMethod(nameof(NominalDerived.Read))!,
            typeof(NominalHidingDerived).GetConstructor(Type.EmptyTypes)!, typeof(NominalHidingDerived).GetMethod(nameof(NominalHidingDerived.Read), [typeof(ForeignPayload)])!,
            typeof(ForeignPayload).GetConstructor(Type.EmptyTypes)!,
            typeof(NominalReturnFactory).GetConstructor(Type.EmptyTypes)!, typeof(NominalReturnFactory).GetMethod(nameof(NominalReturnFactory.Create))!,
            typeof(NominalNullFactory).GetConstructor(Type.EmptyTypes)!, typeof(NominalNullFactory).GetMethod(nameof(NominalNullFactory.Create))! }
            .Select(Row).OrderBy(row => row.TypeName, StringComparer.Ordinal).ThenBy(row => row.MethodName, StringComparer.Ordinal)
            .ThenBy(row => row.MetadataToken).ToArray();
        var draft = new HybridCPU.Compiler.NativeAot.NativeAotBodyPresentationV1(
            HybridCPU.Compiler.NativeAot.NativeAotBodyPresentationV1.Schema,
            HybridCPU.Compiler.NativeAot.NativeAotBodyPresentationV1.Version,
            HybridCPU.Compiler.Cil.ScalarControlFlowV2ProfileContractV1.ProfileId,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant(),
            Row(root), bodies, string.Empty);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join('|', draft.SchemaId, draft.SchemaVersion, draft.ProfileId, draft.AssemblySha256,
                $"{draft.Root.TypeName}::{draft.Root.MethodName}:0x{draft.Root.MetadataToken:x8}",
                string.Join(';', draft.PresentedBodies.Select(row => $"{row.TypeName}::{row.MethodName}:0x{row.MetadataToken:x8}")))))).ToLowerInvariant();
        File.WriteAllText(presentationPath, System.Text.Json.JsonSerializer.Serialize(draft with { ContractDigest = digest }));
        start.Environment["HYBRIDCPU_NATIVEAOT_BODY_PRESENTATION_V1"] = presentationPath;
    }
    public class NominalDerived : NominalBase
    {
        public override int Read(NominalPayload payload) => payload.Value + 2;
    }
    public sealed class ForeignPayload { }
    public class NominalHidingDerived : NominalDerived
    {
        public virtual int Read(ForeignPayload payload) => 99;
    }

    public static int ManagedNominalOverloadReturn()
    {
        var receiver = new NominalHidingDerived();
        NominalBase original = receiver;
        var payload = new NominalPayload();
        payload.Value = 40;
        return original.Read(payload) == 42 && receiver.Read(new ForeignPayload()) == 99 ? 42 : 98;
    }

    public static int ManagedNominalSlotReturn()
    {
        NominalBase receiver = new NominalDerived();
        var payload = new NominalPayload();
        payload.Value = 40;
        return receiver.Read(payload);
    }

    public sealed class NominalReturnFactory : NominalFactory
    {
        public override NominalPayload Create(int value)
        {
            var held = new NominalPayload();
            held.Value = value;
            var garbage = new NominalPayload();
            garbage.Value = 98;
            return held;
        }
    }
    public sealed class NominalNullFactory : NominalFactory
    {
        public override NominalPayload? Create(int value) => null;
    }

    public static int ManagedNominalReferenceReturn()
    {
        NominalFactory factory = new NominalReturnFactory();
        NominalPayload held = factory.Create(40)!;
        var scratch = new int[1];
        scratch[0] = 2;
        return held.Value + scratch[0];
    }

    public static int ManagedNominalNullReturn()
    {
        try
        {
            NominalFactory factory = new NominalNullFactory();
            NominalPayload? held = factory.Create(40);
            var scratch = new int[1];
            scratch[0] = 2;
            return held!.Value + scratch[0];
        }
        catch (NullReferenceException) { return 42; }
    }

    public static int ManagedForeignStaticMethodReturn() => ForeignStatics.ReadAfterAllocation();
    public static int ManagedFailedStaticReturn() => FailedStatics.Value + 2;
    public static int ManagedMultiStaticReturn()
    {
        int value = MultiSecond.Held.Value;
        var scratch = new int[1];
        scratch[0] = MultiFirst.Count + MultiSecond.Count;
        return value + scratch[0];
    }
    public static int ManagedMultiFailedStaticReturn() => MultiRemaining.Value + 21;

    [Fact]
    public void CompiledMultipleInitializersRespectDependencyOrderAndStaticRoots() =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedMultiStaticReturn), ExternalExecutionTerminalKind.Completed);
    [Fact]
    public void CompiledFailedInitializerStopsBeforeRemainingInitializersAndMain() =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedMultiFailedStaticReturn), ExternalExecutionTerminalKind.ExecutionFailure);

    [Fact]
    public void CompiledFailedInitializerRetainsOriginalTerminalWithoutMainEffect() =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedFailedStaticReturn), ExternalExecutionTerminalKind.ExecutionFailure);
    public static int ManagedForeignStaticDirectReturn()
    {
        int count = ForeignStatics.Count;
        var scratch = new int[1];
        scratch[0] = 1;
        return ForeignStatics.Held.Value + count + scratch[0];
    }

    [Theory]
    [InlineData(nameof(ManagedForeignStaticMethodReturn))]
    [InlineData(nameof(ManagedForeignStaticDirectReturn))]
    public void CompiledForeignTypeInitializerOwnsStaticRootAcrossActualGc(string method) =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(method,
            ExternalExecutionTerminalKind.Completed);

    [Theory]
    [InlineData(nameof(ManagedNominalReferenceReturn))]
    [InlineData(nameof(ManagedNominalNullReturn))]
    public void CompiledVirtualReferenceReturnSurvivesRealSafepointsAndExactRelease(string method) =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(method,
            ExternalExecutionTerminalKind.Completed);

    [Fact]
    public void VirtualReferenceReturnRejectsChangedMetadataBeforeArtifact() =>
        Compile(nameof(ManagedNominalReferenceReturn), expectSuccess: false, tamperMetadata: true,
            expectedDiagnostic: "HCNAOT3006");

    [Fact]
    public void CompiledCrossModuleNominalOverrideExecutesOriginalResultAndBackingRelease() =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedNominalSlotReturn), ExternalExecutionTerminalKind.Completed);

    [Fact]
    public void CompiledForeignNominalOverloadDoesNotHideOriginalSlot() =>
        CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedNominalOverloadReturn), ExternalExecutionTerminalKind.Completed);
}
