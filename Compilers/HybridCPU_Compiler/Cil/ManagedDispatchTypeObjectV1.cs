using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.Compiler.Core.Target.Link;
using HybridCPU.Platform.Contracts;

namespace HybridCPU.Compiler.Cil;

public sealed record ManagedDispatchTypeImageRowV1(HybridCpuManagedTypeDescriptorV1 Descriptor,
    string Symbol, int SizeBytes, ulong TypeHandle);
public sealed record ManagedDispatchTypeObjectArtifactV1(HybridCpuObjectArtifactV1 ObjectArtifact,
    IReadOnlyList<ManagedDispatchTypeImageRowV1> Rows);

// Metadata transport only. Static storage, helper binding and runtime type-system installation
// remain separate obligations; emitting these rows does not qualify dispatch execution.
public static class ManagedDispatchTypeObjectV1
{
    public const string ModuleIdentity = "hybridcpu.managed-runtime.dispatch-types/v1";

    public static HybridCpuManagedTypeRegistrationV1[] Registrations(
        ManagedDispatchTypeObjectArtifactV1 artifact, HybridCpuStaticLinkArtifactV1 linked)
    {
        if (linked.Status != HybridCpuLinkStatusV1.Success) throw new ArgumentException("Dispatch metadata requires a successful link.");
        return artifact.Rows.Select(row =>
        {
            var symbol = linked.Symbols.Single(symbol => symbol.Name == row.Symbol);
            if (symbol.Size != (ulong)row.SizeBytes || symbol.Address < linked.ImageBase ||
                symbol.Address - linked.ImageBase > (ulong)linked.ImageBytes.Length ||
                (ulong)row.SizeBytes > (ulong)linked.ImageBytes.Length - (symbol.Address - linked.ImageBase))
                throw new ArgumentException("Dispatch descriptor symbol is outside the linked image.");
            return new HybridCpuManagedTypeRegistrationV1(row.Descriptor.TypeId, row.Descriptor.StableIdentity,
                row.Descriptor.DescriptorDigest, checked((int)(symbol.Address - linked.ImageBase)), row.SizeBytes, null,
                row.TypeHandle);
        }).ToArray();
    }

    public static ManagedDispatchTypeObjectArtifactV1 Emit(ManagedCallGraphCompilationV1 compilation,
        IReadOnlyList<string>? requiredTypeIdentities = null)
    {
        var universe = compilation.TypeUniverse?.Rows ?? throw new ArgumentException("Missing dispatch type universe.");
        var byId = universe.ToDictionary(row => row.TypeId);
        var byName = universe.Where(row => row.StableIdentity.Length != 0).ToDictionary(row => row.StableIdentity, StringComparer.Ordinal);
        var required = new SortedSet<ulong>();
        void Include(ulong id)
        {
            if (!required.Add(id)) return;
            if (!byId.TryGetValue(id, out var row) || row.Descriptor is not { } descriptor ||
                descriptor.TypeId != id || descriptor.StableIdentity != row.StableIdentity ||
                (descriptor.BaseTypeId ?? 0) != row.BaseTypeId)
                throw new ArgumentException($"Dispatch type {id} lacks its exact descriptor.");
            // Array and value-type shapes are encoded exactly by managed-type-metadata/v2.
            // String objects retain their separate literal-object registration contract.
            if (descriptor.Kind is not (HybridCpuManagedTypeKindV1.Class or HybridCpuManagedTypeKindV1.Interface or HybridCpuManagedTypeKindV1.ValueType or HybridCpuManagedTypeKindV1.SzArray) ||
                descriptor.Kind == HybridCpuManagedTypeKindV1.SzArray && descriptor.ArrayShape is not { IsSzArray: true } ||
                descriptor.Kind != HybridCpuManagedTypeKindV1.SzArray && descriptor.ArrayShape is not null ||
                descriptor.StringShape is not null ||
                descriptor.Kind == HybridCpuManagedTypeKindV1.ValueType && descriptor.ValueTypeShape is null ||
                descriptor.Kind != HybridCpuManagedTypeKindV1.ValueType && descriptor.ValueTypeShape is not null)
                throw new ArgumentException($"Dispatch type '{descriptor.StableIdentity}' requires shape-aware metadata encoding.");
            if (descriptor.BaseTypeId is ulong parent && parent != 0) Include(parent);
            foreach (ulong iface in descriptor.InterfaceTypeIds) Include(iface);
        }
        foreach (var plan in compilation.DispatchCallPlans ?? [])
        {
            // Exact runtime-service plans are already lowered to admitted direct calls.
            // Their provider-local interface IDs are not image TypeIds and must never
            // enter the image vtable/type-descriptor closure.
            if (plan.Kind != RestrictedCilDispatchKindV1.Interface || !IsImageOwned(plan)) continue;
            Include(plan.InterfaceTypeId);
            foreach (var candidate in plan.Candidates)
            {
                if (!byName.TryGetValue(candidate.RuntimeTypeIdentity, out var row))
                    throw new ArgumentException($"Missing dispatch receiver '{candidate.RuntimeTypeIdentity}'.");
                Include(row.TypeId);
            }
        }
        foreach (var plan in compilation.VirtualSlotPlans ?? [])
            foreach (var target in plan.Targets)
            {
                if (!byName.TryGetValue(target.RuntimeTypeIdentity, out var row))
                    throw new ArgumentException($"Missing virtual dispatch receiver '{target.RuntimeTypeIdentity}'; " +
                        $"universe={string.Join(',', byName.Keys.Order(StringComparer.Ordinal))}.");
                Include(row.TypeId);
            }
        foreach (string identity in requiredTypeIdentities ?? [])
        {
            if (!byName.TryGetValue(identity, out var row))
                throw new ArgumentException($"Missing required managed runtime type '{identity}'.");
            Include(row.TypeId);
        }
        foreach (ManagedArrayTypeUseV1 use in (compilation.ArrayTypeUses ?? [])
            .DistinctBy(static use => (use.TypeId, use.TypeHandle))
            .OrderBy(static use => use.TypeHandle))
        {
            ManagedTypeUniverseRowV1 row = universe.SingleOrDefault(row => row.TypeHandle == use.TypeHandle && row.TypeId == use.TypeId) ??
                throw new ArgumentException($"Managed newarr at {use.CallerIdentity}@IL_{use.CilOffset:x4} references missing exact image type {use.TypeId}/{use.TypeHandle}.");
            Include(row.TypeId);
        }
        foreach (ManagedAllocationTypeUseV1 use in (compilation.AllocationTypeUses ?? [])
            .DistinctBy(static use => (use.TypeId, use.TypeHandle))
            .OrderBy(static use => use.TypeHandle))
        {
            ManagedTypeUniverseRowV1 row = universe.SingleOrDefault(row => row.TypeHandle == use.TypeHandle && row.TypeId == use.TypeId) ??
                throw new ArgumentException($"Managed newobj at {use.CallerIdentity}@IL_{use.CilOffset:x4} references missing exact image type {use.TypeId}/{use.TypeHandle}.");
            if (row.Descriptor?.Kind != HybridCpuManagedTypeKindV1.Class)
                throw new ArgumentException($"Managed newobj at {use.CallerIdentity}@IL_{use.CilOffset:x4} requires an exact class TypeDescriptor.");
            Include(row.TypeId);
        }
        if (required.Count == 0) throw new ArgumentException("Empty dispatch descriptor closure.");
        using var payload = new MemoryStream();
        var symbols = new List<HybridCpuObjectSymbolV1>();
        var rows = new List<ManagedDispatchTypeImageRowV1>();
        foreach (ulong id in required)
        {
            var descriptor = byId[id].Descriptor!;
            var encoded = new HybridCpuManagedTypeMetadataEncoderV1().Encode([descriptor]);
            if (encoded.Status != HybridCpuManagedTypeMetadataStatusV1.Encoded)
                throw new ArgumentException($"Dispatch descriptor '{descriptor.StableIdentity}' rejected: {encoded.Reason} " +
                    $"kind={descriptor.Kind}; size={descriptor.InstanceSizeBytes}; alignment={descriptor.InstanceAlignmentBytes}; " +
                    $"fields={descriptor.InstanceFields.Count}; value-shape={descriptor.ValueTypeShape is not null}; " +
                    $"digest-bound={descriptor.DescriptorDigest == HybridCpuManagedTypeDescriptorContractV1.ComputeDigest(descriptor)}.");
            while (payload.Length % 8 != 0) payload.WriteByte(0);
            string symbol = "__hybridcpu_managed_dispatch_type_" + id.ToString("x16");
            symbols.Add(new(symbol, HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Hidden,
                ".hctypes", (ulong)payload.Length, (ulong)encoded.Bytes.Length, true));
            rows.Add(new(descriptor, symbol, encoded.Bytes.Length, byId[id].TypeHandle));
            payload.Write(encoded.Bytes);
            if (payload.Length > HybridCpuManagedTypeMetadataEncoderV1.MaximumBytes)
                throw new ArgumentException("Dispatch descriptor byte budget exceeded.");
        }
        byte[] bytes = payload.ToArray();
        var artifact = new HybridCpuObjectWriterV1().Write(new(
            [new(".hctypes", HybridCpuObjectSectionKind.ReadOnlyData, 8, bytes, (ulong)bytes.Length)], symbols, [],
            HybridCpuTargetPlatformContractV1.Default.ContractDigest, HybridCpuManagedAbiFamilyV1.Default.ContractDigest));
        return new(artifact, rows);
    }

    internal static bool IsImageOwned(ManagedDispatchCallPlanV1 plan)
    {
        bool runtimeService = plan.Candidates.Any(static candidate =>
            candidate.RuntimeTypeIdentity == "runtime-service");
        if (runtimeService && (plan.Candidates.Count != 1 || plan.RuntimeExternal))
            throw new ArgumentException("Runtime-service dispatch plan has mixed or external ownership.");
        return !plan.RuntimeExternal && !runtimeService;
    }
}
