using System.Buffers.Binary;
using System.Globalization;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;

internal static class ArgumentNullSmoke
{
    public static void Run()
    {
        var built = new HybridCpuManagedTypeSystemBuilderV1().Build([
            new("System.Exception", HybridCpuManagedTypeKindV1.Class, null, [], [
                new("_message", HybridCpuManagedStorageKindV1.ObjectReference, 8, 8, false, 0),
                new("_HResult", HybridCpuManagedStorageKindV1.Primitive, 4, 4, false, 1)]),
            new("System.SystemException", HybridCpuManagedTypeKindV1.Class, "System.Exception", [], []),
            new("System.ArgumentException", HybridCpuManagedTypeKindV1.Class, "System.SystemException", [], [
                new("_paramName", HybridCpuManagedStorageKindV1.ObjectReference, 8, 8, false, 0)]),
            new("System.ArgumentNullException", HybridCpuManagedTypeKindV1.Class, "System.ArgumentException", [], []),
            new("System.ArgumentOutOfRangeException", HybridCpuManagedTypeKindV1.Class, "System.ArgumentException", [], [
                new("_actualValue", HybridCpuManagedStorageKindV1.ObjectReference, 8, 8, false, 0)]),
            new(nameof(MessageOverride), HybridCpuManagedTypeKindV1.Class, "System.Exception", [], []),
            new(nameof(MessageInherited), HybridCpuManagedTypeKindV1.Class, nameof(MessageOverride), [], []),
            new(nameof(MessageHidden), HybridCpuManagedTypeKindV1.Class, nameof(MessageOverride), [], []),
            new(nameof(MessageHiddenOverride), HybridCpuManagedTypeKindV1.Class, nameof(MessageHidden), [], []),
            new("System.String", HybridCpuManagedTypeKindV1.String, null, [], [], null, new(16, 20, 2, true))]);
        Require(built.IsSuccess, built.Reason);
        var types = built.TypeSystem!;
        var descriptor = types.Descriptors.Single(t => t.StableIdentity == "System.ArgumentNullException");
        ulong handle = types.TypeHandle(descriptor.TypeId)!.Value;
        ulong stringHandle = types.TypeHandle(types.Descriptors.Single(t => t.Kind == HybridCpuManagedTypeKindV1.String).TypeId)!.Value;
        var kernel = new DeterministicRuntimeKernelV1();
        Require(kernel.Boot(new(HybridCpuPlatformContractV1.ContractDigest, new string('a', 64),
            0x100000, 4096, 0x100000, 0x200000, 4096, 0, 1000)).IsSuccess, "boot");
        var heapOptions = HybridCpuManagedHeapOptionsV1.Create(0x40000000, 65536, 65536, -3);
        var mismatchedMemory = new HybridCpuManagedArrayHeapMemoryV1(heapOptions.BaseAddress + 4096, heapOptions.SizeBytes);
        Require(new HybridCpuManagedHeapAllocatorV1(kernel, types, heapOptions, mismatchedMemory).Initialize().Status ==
            HybridCpuManagedHeapStatusV1.InvalidConfiguration,
            "Heap backing with a different guest address range must fail before VM reservation");
        var guestMemory = new HybridCpuManagedArrayHeapMemoryV1(heapOptions.BaseAddress, heapOptions.SizeBytes);
        var heap = new HybridCpuManagedHeapAllocatorV1(kernel, types, heapOptions, guestMemory);
        Require(heap.Initialize().IsSuccess, "heap");
        var runtime = new HybridCpuManagedArgumentNullExceptionRuntimeV1(types, heap, descriptor.TypeId, stringHandle);
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        byte[] guestHeader = new byte[HybridCpuPlatformContractV1.ManagedObjectHeaderSizeBytes];
        try
        {
            foreach (string? name in new string?[] { null, "", "clock", "такт", "a'b", "\ud800\0\udfff" })
            {
                ulong receiver = heap.Allocate(handle).ObjectReference;
                Require(guestMemory.Read(receiver, guestHeader) &&
                    BinaryPrimitives.ReadUInt64LittleEndian(guestHeader) == handle,
                    "Allocator writes the exact object header into its injected guest-visible memory backing");
                ulong param = name is null ? 0 : MakeString(name);
                var result = runtime.Construct(receiver, param);
                Require(result.IsSuccess, result.Reason);
                var expected = new ArgumentNullException(name);
                Require(runtime.ParamName(receiver).ObjectReference == param, "ParamName reference identity");
                Require(Read(runtime.ParamName(receiver).ObjectReference) == expected.ParamName, "ParamName content");
                Require(runtime.HResult(receiver).ScalarValue == expected.HResult, "HRESULT");
                var actualMessage = runtime.Message(receiver);
                Require(actualMessage.IsSuccess && Read(actualMessage.ObjectReference) == expected.Message,
                    "Invariant CoreCLR Message parity, including raw UTF-16");
                var abi = HybridCpuManagedAbiFamilyV1.Default;
                var gc = new HybridCpuManagedNonMovingGcV1(types, heap, abi.ContractDigest,
                    abi.TargetContractDigest, abi.NativeAbiDigest, HybridCpuManagedAbiFamilyV1.RuntimePackRevision);
                var collected = gc.Collect(new([], [], [new("exception", HybridCpuManagedGcRootSourceV1.Handle, receiver)]),
                    HybridCpuManagedNonMovingGcOptionsV1.Qualification);
                Require(collected.IsSuccess && Read(runtime.Message(receiver).ObjectReference) == expected.Message,
                    "Inherited exception fields must survive GC");
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
        ulong invalidTarget = heap.Allocate(handle).ObjectReference;
        byte[] before = heap.ReadObjectBytes(invalidTarget)!;
        Require(!runtime.Construct(invalidTarget, invalidTarget).IsSuccess &&
            before.SequenceEqual(heap.ReadObjectBytes(invalidTarget)!), "Invalid paramName must not mutate receiver");
        Require(!runtime.Construct(0, 0).IsSuccess && !runtime.Construct(MakeString("bad"), 0).IsSuccess,
            "Invalid receivers must fail closed");

        var outDescriptor = types.Descriptors.Single(t => t.StableIdentity == "System.ArgumentOutOfRangeException");
        ulong outHandle = types.TypeHandle(outDescriptor.TypeId)!.Value;
        var outRuntime = HybridCpuManagedArgumentNullExceptionRuntimeV1.ForArgumentOutOfRange(types, heap,
            outDescriptor.TypeId, stringHandle);
        ulong outReceiver = heap.Allocate(outHandle).ObjectReference;
        ulong outParameter = MakeString("pixelOffset");
        var outConstructed = outRuntime.Construct(outReceiver, outParameter);
        var expectedOut = new ArgumentOutOfRangeException("pixelOffset");
        Require(outConstructed.IsSuccess && outRuntime.ParamName(outReceiver).ObjectReference == outParameter &&
                outRuntime.HResult(outReceiver).ScalarValue == expectedOut.HResult &&
                Read(outRuntime.Message(outReceiver).ObjectReference) == expectedOut.Message,
            "ArgumentOutOfRangeException invariant ParamName/Message/HRESULT parity");

        byte[] pe = File.ReadAllBytes(typeof(ArgumentNullFixture).Assembly.Location);
        var formatCtor = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportImage(pe, new(typeof(ManagedPortFormatException).FullName!, ".ctor"));
        Require(formatCtor.Status == RestrictedCilImportStatusV1.Success && formatCtor.Program!.Instructions.Any(instruction =>
                instruction.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_exception_ctor_message"),
            "FormatException(string) must reuse the exact exception-message runtime constructor");
        var exceptionCtorObject = HybridCpuManagedExceptionCtorMessageEmitterV1.EmitObject(
            types.Descriptors.Single(type => type.StableIdentity == "System.Exception").InstanceFields
                .Single(field => field.Identity == "_message").OffsetBytes);
        Require(exceptionCtorObject.Status == HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success &&
                exceptionCtorObject.Symbols.Any(symbol =>
                    symbol.Name == HybridCpuManagedExceptionCtorMessageEmitterV1.Symbol && symbol.IsDefinition),
            "Exception(string) must bind an exact native field-store HCO");
        byte[] il = typeof(ArgumentNullFixture).GetMethod(nameof(ArgumentNullFixture.Make))!.GetMethodBody()!.GetILAsByteArray()!;
        int token = BitConverter.ToInt32(il, Array.IndexOf(il, (byte)0x73) + 1);
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
            allocationBindings: [new(token, descriptor, handle)]);
        var resultImport = importer.ImportImage(pe, new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Make)));
        Require(resultImport.Status == RestrictedCilImportStatusV1.Success,
            string.Join("; ", resultImport.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Require(resultImport.Program!.Instructions.Any(instruction => instruction.Annotation.BranchTargetSymbolName ==
            "__hybridcpu_managed_argument_null_ctor_param_name"), "newobj must call the qualified runtime constructor symbol");
        HybridCpuObjectArtifactV1 argumentNullCtorObject = HybridCpuManagedArgumentNullCtorEmitterV1.EmitObject();
        Require(HybridCpuManagedAbiFamilyV1.SchemaMinor == 61 &&
                HybridCpuManagedRuntimeEcallContractV1.ArgumentNullCtorParamNameOperation == 35 &&
                argumentNullCtorObject.Status == HybridCpuObjectStatusV1.Success &&
                argumentNullCtorObject.Symbols.Any(symbol => symbol.Name ==
                    HybridCpuManagedArgumentNullCtorEmitterV1.Symbol && symbol.IsDefinition),
            "ArgumentNullException(string) must bind an exact allocating/throwing ECALL HCO");
        var metadataCtorGraph = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "metadata-argument-null",
                [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Make))], [], MetadataOnlyModules:
                [new(File.ReadAllBytes(typeof(object).Assembly.Location), "argument-null-corelib"),
                 new(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
                     "argument-null-runtime-facade")]));
        Require(metadataCtorGraph.Status == RestrictedCilImportStatusV1.Success &&
                metadataCtorGraph.Methods.Single().Import.Program!.Instructions.Any(instruction =>
                    instruction.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_argument_null_ctor_param_name"),
            "Body-world metadata must bind exact CoreLib ArgumentNullException(string)");
        byte[] outIl = typeof(ArgumentNullFixture).GetMethod(nameof(ArgumentNullFixture.OutOfRange))!.GetMethodBody()!.GetILAsByteArray()!;
        int outToken = BitConverter.ToInt32(outIl, Array.IndexOf(outIl, (byte)0x73) + 1);
        var outImporter = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
            allocationBindings: [new(outToken, outDescriptor, outHandle)]);
        var outImport = outImporter.ImportImage(pe,
            new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.OutOfRange)));
        Require(outImport.Status == RestrictedCilImportStatusV1.Success && outImport.Program!.Instructions.Any(instruction =>
                instruction.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_argument_out_of_range_ctor_param_name") &&
                HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(
                    "__hybridcpu_managed_argument_out_of_range_ctor_param_name") is { Support: HybridCpuManagedAbiSupportV1.Supported },
            "ArgumentOutOfRangeException(string) must bind the exact ABI v1.24 helper");
        HybridCpuObjectArtifactV1 rangeCtorObject = HybridCpuManagedArgumentOutOfRangeCtorEmitterV1.EmitObject();
        Require(HybridCpuManagedRuntimeEcallContractV1.ArgumentOutOfRangeCtorParamNameOperation == 36 &&
                rangeCtorObject.Status == HybridCpuObjectStatusV1.Success &&
                rangeCtorObject.Symbols.Any(symbol => symbol.Name ==
                    HybridCpuManagedArgumentOutOfRangeCtorEmitterV1.Symbol && symbol.IsDefinition),
            "ArgumentOutOfRangeException(string) must bind an exact allocating/throwing ECALL HCO");
        var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "argument-null-smoke", [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Make))], []));
        Require(graph.Status == RestrictedCilImportStatusV1.Success,
            string.Join("; ", graph.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var rejected = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "argument-null-negative", [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Other))], []));
        Require(rejected.Status != RestrictedCilImportStatusV1.Success, "Other constructors remain closed");
        var virtualMessage = importer.ImportImage(pe,
            new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.VirtualMessage)));
        Require(virtualMessage.Status != RestrictedCilImportStatusV1.Success,
            "Virtual Exception.Message cannot be replaced by a direct base-field read");
        var virtualGraph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "message-dispatch-negative", [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.VirtualMessage))], [],
            MetadataOnlyModules: CoreLibMetadata()));
        Require(virtualGraph.Status == RestrictedCilImportStatusV1.Success,
            string.Join("; ", virtualGraph.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var plan = virtualGraph.VirtualSlotPlans!.Single();
        Require(virtualGraph.Methods.Any(method => method.Identity.DeclaringType == nameof(MessageOverride)) &&
            !virtualGraph.Methods.Any(method => method.Identity.DeclaringType is nameof(MessageHidden) or nameof(MessageHiddenOverride)),
            "Reachability must include the true override and exclude hidden-slot overrides");
        foreach (string type in new[] { nameof(MessageOverride), nameof(MessageInherited), nameof(MessageHidden), nameof(MessageHiddenOverride) })
            Require(plan.Targets.Single(target => target.RuntimeTypeIdentity == type).ImplementationIdentity == "MessageOverride.get_Message",
                "Inheritance/newslot must preserve exact Exception.Message slot: " + type);
        var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(virtualGraph, virtualGraph.Graph!.RootIdentities.Single());
        Require(!linked.Diagnostics.Any(d=>d.Code=="HCSCF-LINK4010"),
            "Exact virtual slot plan remained blocked at the registration gate");
        Require(ManagedDispatchObjectV1.Emit(virtualGraph).Status==HybridCpuObjectStatusV1.Success&&
                HybridCpuManagedDispatchResolverEmitterV1.EmitVirtualObject().Status==HybridCpuObjectStatusV1.Success,
            "Exact virtual metadata, type registrations and resolver must produce linkable HCO objects");
        int messageOffset = virtualGraph.TypeUniverse!.Rows.Single(row => row.StableIdentity == "System.Exception")
            .Descriptor!.InstanceFields.Single(field => field.Identity == "_message").OffsetBytes;
        var messageGetter = HybridCpuManagedExceptionGetMessageEmitterV1.EmitObject(messageOffset);
        Require(messageGetter.Status == HybridCpuObjectStatusV1.Success && messageGetter.Symbols is
                [{ Name: HybridCpuManagedExceptionGetMessageEmitterV1.Symbol, IsDefinition: true }] &&
                !linked.Diagnostics.Any(d => d.Code == "HCLINK1003") &&
                !linked.Diagnostics.Any(d => d.Code == "HCSCF-LINK4016"),
            "Base message getter and allocating formatting must pass the closed production-loader-consumer gate");
        foreach (int invalidOffset in new[] { -1, 0, 17, short.MaxValue })
        {
            bool rejectedOffset = false;
            try { HybridCpuManagedExceptionGetMessageEmitterV1.Emit(invalidOffset); }
            catch (ArgumentOutOfRangeException) { rejectedOffset = true; }
            Require(rejectedOffset, "Malformed exception message offset must fail closed: " + invalidOffset);
        }
        var again = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "different-source-path", [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.VirtualMessage))], [],
            MetadataOnlyModules: CoreLibMetadata()));
        Require(again.VirtualSlotPlans!.Single().PlanDigest == plan.PlanDigest, "Slot plan determinism");
        var dependency = typeof(ManagedPortSmoke.Dependency.ExternalMessageOverride).Assembly;
        var crossModule = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "cross-module-message", [new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.VirtualMessage))], [],
            DependencyModules: [new(File.ReadAllBytes(dependency.Location), "external-message-fixture")],
            MetadataOnlyModules: CoreLibMetadata()));
        Require(crossModule.Status == RestrictedCilImportStatusV1.Success,
            string.Join("; ", crossModule.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Require(crossModule.Methods.Any(method => method.Identity.DeclaringType ==
            typeof(ManagedPortSmoke.Dependency.ExternalMessageOverride).FullName), "Cross-module override body must be reachable");
        ulong exceptionId = types.Descriptors.Single(t => t.StableIdentity == "System.Exception").TypeId;
        ulong argumentId = types.Descriptors.Single(t => t.StableIdentity == "System.ArgumentException").TypeId;
        ulong overrideId = types.Descriptors.Single(t => t.StableIdentity == nameof(MessageOverride)).TypeId;
        ManagedVirtualSlotTargetV1 argumentTarget = new("System.ArgumentException",
            "System.ArgumentException.get_Message", "System.Private.CoreLib", null);
        ManagedCallGraphCompilationV1 argumentDispatchGraph = virtualGraph with
        {
            // Isolate the CoreLib target so this gate proves the runtime thunk;
            // the override target set is covered by the dispatch tests above.
            VirtualSlotPlans = [plan with { Targets = [argumentTarget] }]
        };
        HybridCpuObjectArtifactV1 argumentDispatch = ManagedDispatchObjectV1.Emit(argumentDispatchGraph);
        Require(argumentDispatch.Status == HybridCpuObjectStatusV1.Success &&
                argumentDispatch.Symbols.Any(symbol => symbol.Name == "__hybridcpu_managed_argument_exception_get_message" && !symbol.IsDefinition) &&
                HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(
                    "__hybridcpu_managed_argument_exception_get_message") is
                    { Support: HybridCpuManagedAbiSupportV1.Supported,
                      GcTransition: HybridCpuRuntimeHelperGcTransitionV1.RequiredSafepoint },
            "ArgumentException.Message dispatch must bind the exact allocating/safepoint runtime-helper contract");
        HybridCpuObjectArtifactV1 argumentThunk = HybridCpuManagedArgumentExceptionMessageEmitterV1.EmitObject();
        Require(argumentThunk.Status == HybridCpuObjectStatusV1.Success &&
                argumentThunk.Symbols.Any(symbol =>
                    symbol.Name == HybridCpuManagedArgumentExceptionMessageEmitterV1.Symbol && symbol.IsDefinition),
            "ArgumentException.Message must emit its CPU-callable thunk definition");
        Require(plan.SlotId == HybridCpuManagedDispatchTableBuilderV1.ComputeSlotId(exceptionId,
            plan.DeclarationIdentity, plan.SignatureIdentity), "Compiler/runtime slot identity parity");

        static ManagedBodyWorldModuleV1[] CoreLibMetadata()
        {
            string directory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            return [
                new(File.ReadAllBytes(typeof(object).Assembly.Location), "argument-null-corelib-dispatch"),
                new(File.ReadAllBytes(Path.Combine(directory, "System.Runtime.dll")), "argument-null-runtime-dispatch")];
        }
        var table = new HybridCpuManagedDispatchTableBuilderV1().Build(types, [
            new(plan.DeclarationIdentity, exceptionId, plan.SignatureIdentity, 0x1000, 0, true, true),
            new("System.ArgumentException.get_Message", argumentId, plan.SignatureIdentity, 0x2000, 0, true, false, plan.SlotId),
            new("MessageOverride.get_Message", overrideId, plan.SignatureIdentity, 0x3000, 0, true, false, plan.SlotId)], []);
        Require(table.IsSuccess, table.Reason);
        foreach (var target in plan.Targets)
        {
            var type = types.Descriptors.SingleOrDefault(t => t.StableIdentity == target.RuntimeTypeIdentity);
            if (type is null) continue;
            var row = table.Table!.VirtualEntries.Single(r => r.RuntimeTypeId == type.TypeId && r.SlotId == plan.SlotId);
            Require(table.Table.Methods.Single(m => m.MethodId == row.MethodId).StableIdentity == target.ImplementationIdentity,
                "Runtime vtable target parity: " + target.RuntimeTypeIdentity);
        }
        Console.WriteLine("PASS Exception.Message exact slot, override reachability, newslot isolation, runtime vtable parity and image gate");
        var strings = new HybridCpuManagedStringRuntimeV1(types, heap, stringHandle);
        ulong firstString = MakeString("A\0");
        ulong secondString = MakeString("Б");
        var concat2 = strings.Concat2(firstString, secondString);
        var concatLeftNull = strings.Concat2(0, secondString);
        var concatRightNull = strings.Concat2(firstString, 0);
        var concat3 = strings.Concat3(firstString, 0, secondString);
        Require(strings.MaterializeLiteral(stringHandle, "").IsSuccess && strings.Concat3(0, 0, 0).IsSuccess &&
                concat2.IsSuccess && Read(concat2.ObjectReference) == "A\0Б" &&
                concat3.IsSuccess && Read(concat3.ObjectReference) == "A\0Б" &&
                Read(concatLeftNull.ObjectReference) == "Б" && Read(concatRightNull.ObjectReference) == "A\0" &&
                strings.Concat2(outReceiver, secondString).Status == HybridCpuManagedShapeStatusV1.InvalidType &&
                strings.Concat3(firstString, outReceiver, secondString).Status == HybridCpuManagedShapeStatusV1.InvalidType,
            "Empty UTF-16 strings must respect the fixed allocation prefix");
        var concatImport = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportImage(pe, new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Concat2)));
        Require(concatImport.Status == RestrictedCilImportStatusV1.Success && concatImport.Program!.Instructions.Any(instruction =>
                instruction.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_string_concat2") &&
                HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(HybridCpuManagedStringConcat2EmitterV1.Symbol) is
                    { GcTransition: HybridCpuRuntimeHelperGcTransitionV1.RequiredSafepoint, MayThrow: true,
                      Support: HybridCpuManagedAbiSupportV1.Supported },
            "String.Concat(string,string) must bind the exact ABI v1.23 helper");
        var concatObject = HybridCpuManagedStringConcat2EmitterV1.EmitObject();
        Require(concatObject.Status == HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success &&
                concatObject.Symbols.Any(symbol => symbol.Name == HybridCpuManagedStringConcat2EmitterV1.Symbol && symbol.IsDefinition),
            "String.Concat(string,string) must bind an exact allocating/throwing HCO");
        var concat3Import = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportImage(pe, new(typeof(ArgumentNullFixture).FullName!, nameof(ArgumentNullFixture.Concat3)));
        var concat3Object = HybridCpuManagedStringConcat3EmitterV1.EmitObject();
        Require(concat3Import.Status == RestrictedCilImportStatusV1.Success && concat3Import.Program!.Instructions.Any(instruction =>
                    instruction.Annotation.BranchTargetSymbolName == HybridCpuManagedStringConcat3EmitterV1.Symbol) &&
                HybridCpuManagedAbiFamilyV1.SchemaMinor == 61 &&
                HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(HybridCpuManagedStringConcat3EmitterV1.Symbol) is
                    { GcTransition: HybridCpuRuntimeHelperGcTransitionV1.RequiredSafepoint, MayThrow: true,
                      Support: HybridCpuManagedAbiSupportV1.Supported } &&
                concat3Object.Status == HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success &&
                concat3Object.Symbols.Any(symbol => symbol.Name == HybridCpuManagedStringConcat3EmitterV1.Symbol && symbol.IsDefinition),
            "String.Concat(string,string,string) must bind an exact allocating/throwing HCO");
        Console.WriteLine("PASS ArgumentNullException CIL helper/newobj, invariant CoreCLR parity, UTF-16, HRESULT, GC and negatives");

        ulong MakeString(string value)
        {
            var allocated = heap.AllocateVariable(stringHandle,
                Math.Max(types.ResolveTypeHandle(stringHandle)!.InstanceSizeBytes, 20 + value.Length * 2));
            Require(allocated.IsSuccess, allocated.Reason);
            var bytes = new byte[4 + value.Length * 2];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value.Length);
            for (int i = 0; i < value.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + i * 2), value[i]);
            Require(heap.WriteObjectBytes(allocated.ObjectReference, 16, bytes).IsSuccess, "UTF-16 fixture write");
            return allocated.ObjectReference;
        }
        string? Read(ulong reference)
        {
            if (reference == 0) return null;
            byte[] bytes = heap.ReadObjectBytes(reference)!;
            var chars = new char[BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16))];
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(20 + i * 2));
            return new string(chars);
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}

public static class ArgumentNullFixture
{
    public static ArgumentNullException Make(string? name) => new ArgumentNullException(name);
    public static ArgumentNullException Other(string message, Exception inner) => new ArgumentNullException(message, inner);
    public static string VirtualMessage(Exception exception) => exception.Message;
    public static string Concat2(string first, string second) => string.Concat(first, second);
    public static string Concat3(string first, string second, string third) => string.Concat(first, second, third);
    public static ArgumentOutOfRangeException OutOfRange(string parameter) => new(parameter);
}

public class MessageOverride : Exception { public override string Message => null!; }
public class MessageInherited : MessageOverride { }
public class MessageHidden : MessageOverride { public new virtual string Message => null!; }
public class MessageHiddenOverride : MessageHidden { public override string Message => null!; }
public sealed class ManagedPortFormatException(string message) : FormatException(message) { }
