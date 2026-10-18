using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

public enum AcceleratorDescriptorTaxonomyStatus : byte
{
    CurrentRuntimeContour = 1,
    MetadataOnly = 2,
    Reserved = 3
}

public readonly record struct AcceleratorDescriptorTaxonomyKey(
    AcceleratorClassId AcceleratorClass,
    AcceleratorDeviceId AcceleratorId,
    AcceleratorOperationKind Operation,
    AcceleratorShapeKind Shape);

public sealed class AcceleratorDescriptorTaxonomyEntry
{
    public AcceleratorDescriptorTaxonomyEntry(
        AcceleratorDescriptorTaxonomyKey key,
        string name,
        AcceleratorDescriptorTaxonomyStatus status,
        string boundary)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Taxonomy entry name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new ArgumentException("Taxonomy boundary is required.", nameof(boundary));
        }

        Key = key;
        Name = name;
        Status = status;
        Boundary = boundary;
    }

    public AcceleratorDescriptorTaxonomyKey Key { get; }

    public string Name { get; }

    public AcceleratorDescriptorTaxonomyStatus Status { get; }

    public string Boundary { get; }

    public bool IsCurrentRuntimeContour =>
        Status == AcceleratorDescriptorTaxonomyStatus.CurrentRuntimeContour;

    public bool IsMetadataOnly =>
        Status == AcceleratorDescriptorTaxonomyStatus.MetadataOnly;

    public bool GrantsDescriptorAcceptanceAuthority => false;

    public bool GrantsCapabilityAuthority => false;

    public bool GrantsTokenAuthority => false;

    public bool GrantsExecutionAuthority => false;

    public bool GrantsCommitAuthority => false;

    public bool GrantsCompilerEmissionAuthority => false;

    public bool GrantsTopologyQueryAuthority => false;

    public bool GrantsQueueOpenAuthority => false;

    public bool GrantsQueueBindAuthority => false;

    public bool GrantsQueueLifecycleAuthority => false;
}

public static class AcceleratorDescriptorTaxonomyCatalog
{
    private static readonly AcceleratorDescriptorTaxonomyEntry[] _entries =
    {
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Matrix,
                AcceleratorDeviceId.ReferenceMatMul,
                AcceleratorOperationKind.MatMul,
                AcceleratorShapeKind.Matrix2D),
            "matrix.reference-matmul.v1",
            AcceleratorDescriptorTaxonomyStatus.CurrentRuntimeContour,
            "Current L7-SDC Matrix/ReferenceMatMul taxonomy entry. Parser acceptance still requires the guard-backed descriptor chain, capability acceptance, token admission, queue/backend execution, and staged commit."),
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Tensor,
                AcceleratorDeviceId.TensorMetadata,
                AcceleratorOperationKind.TensorContract,
                AcceleratorShapeKind.TensorND),
            "tensor.metadata.v1",
            AcceleratorDescriptorTaxonomyStatus.MetadataOnly,
            "Reserved Lane7 tensor taxonomy entry for VMX-facing descriptor planning. It is metadata-only and grants no descriptor acceptance, token, execution, commit, or compiler-emission authority."),
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.TopologyQueue,
                AcceleratorDeviceId.TopologyQueueMetadata,
                AcceleratorOperationKind.TopologyQueueContract,
                AcceleratorShapeKind.TopologyQueueMap),
            "topology.queue.metadata.v1",
            AcceleratorDescriptorTaxonomyStatus.MetadataOnly,
            "Reserved Lane7 topology/queue taxonomy contract for VMX preparation. It is metadata-only and grants no topology query, queue open, queue bind, descriptor acceptance, token, execution, commit, or compiler-emission authority."),
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.Fft,
                AcceleratorDeviceId.FftMetadata,
                AcceleratorOperationKind.FftContract,
                AcceleratorShapeKind.Fft1D),
            "fft.metadata.v1",
            AcceleratorDescriptorTaxonomyStatus.MetadataOnly,
            "Reserved Lane7 FFT descriptor taxonomy contract for richer signal-processing metadata planning. It is metadata-only and grants no descriptor acceptance, token, execution, backend, commit, or compiler-emission authority."),
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.CryptoHash,
                AcceleratorDeviceId.CryptoHashMetadata,
                AcceleratorOperationKind.CryptoHashContract,
                AcceleratorShapeKind.CryptoHashBlock),
            "crypto.hash.metadata.v1",
            AcceleratorDescriptorTaxonomyStatus.MetadataOnly,
            "Reserved Lane7 Crypto/Hash descriptor taxonomy contract for richer descriptor metadata planning. It is metadata-only and grants no descriptor acceptance, token, execution, backend, commit, or compiler-emission authority."),
        new(
            new AcceleratorDescriptorTaxonomyKey(
                AcceleratorClassId.SparseGraph,
                AcceleratorDeviceId.SparseGraphMetadata,
                AcceleratorOperationKind.SparseGraphContract,
                AcceleratorShapeKind.SparseGraphCsr),
            "sparse.graph.metadata.v1",
            AcceleratorDescriptorTaxonomyStatus.MetadataOnly,
            "Reserved Lane7 Sparse/Graph descriptor taxonomy contract for richer descriptor metadata planning. It is metadata-only and grants no descriptor acceptance, token, execution, backend, commit, or compiler-emission authority.")
    };

    public static AcceleratorDescriptorTaxonomyEntry CurrentMatMul => _entries[0];

    public static AcceleratorDescriptorTaxonomyEntry TensorMetadata => _entries[1];

    public static AcceleratorDescriptorTaxonomyEntry TopologyQueueMetadata => _entries[2];

    public static AcceleratorDescriptorTaxonomyEntry FftMetadata => _entries[3];

    public static AcceleratorDescriptorTaxonomyEntry CryptoHashMetadata => _entries[4];

    public static AcceleratorDescriptorTaxonomyEntry SparseGraphMetadata => _entries[5];

    public static IReadOnlyList<AcceleratorDescriptorTaxonomyEntry> Entries { get; } =
        Array.AsReadOnly(_entries);

    public static bool TryGetEntry(
        AcceleratorDescriptorTaxonomyKey key,
        out AcceleratorDescriptorTaxonomyEntry entry)
    {
        for (int index = 0; index < _entries.Length; index++)
        {
            AcceleratorDescriptorTaxonomyEntry candidate = _entries[index];
            if (candidate.Key.Equals(key))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public static bool TryGetEntry(
        AcceleratorClassId acceleratorClass,
        AcceleratorDeviceId acceleratorId,
        AcceleratorOperationKind operation,
        AcceleratorShapeKind shape,
        out AcceleratorDescriptorTaxonomyEntry entry) =>
        TryGetEntry(
            new AcceleratorDescriptorTaxonomyKey(
                acceleratorClass,
                acceleratorId,
                operation,
                shape),
            out entry);
}
