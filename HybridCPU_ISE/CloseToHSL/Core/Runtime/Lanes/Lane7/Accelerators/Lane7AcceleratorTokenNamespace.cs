using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

public sealed class Lane7AcceleratorTokenNamespace
{
    private readonly Lane7StateBlock _lane7;

    internal Lane7AcceleratorTokenNamespace(Lane7StateBlock lane7)
    {
        _lane7 = lane7 ?? throw new ArgumentNullException(nameof(lane7));
    }

    public bool TryMapHostToken(
        Lane7VirtualHandle handle,
        ushort addressSpaceTag,
        AcceleratorToken token,
        out Lane7VirtualToken virtualToken,
        out Lane7Fault fault) =>
        _lane7.TryMapHostToken(handle, addressSpaceTag, token, out virtualToken, out fault);

    public bool TryResolveHostToken(
        ulong virtualTokenId,
        ushort executionDomainTag,
        ushort addressSpaceTag,
        ushort ownerVirtualThreadId,
        out AcceleratorTokenHandle hostHandle,
        out Lane7VirtualToken virtualToken,
        out Lane7Fault fault) =>
        _lane7.TryResolveHostToken(
            virtualTokenId,
            executionDomainTag,
            addressSpaceTag,
            ownerVirtualThreadId,
            out hostHandle,
            out virtualToken,
            out fault);

    public bool TryResolveVirtualTokenForHost(
        AcceleratorTokenHandle hostHandle,
        out Lane7VirtualToken virtualToken) =>
        _lane7.TryResolveVirtualTokenForHost(hostHandle, out virtualToken);

    public bool Release(ulong virtualTokenId) =>
        _lane7.ReleaseVirtualToken(virtualTokenId);
}
