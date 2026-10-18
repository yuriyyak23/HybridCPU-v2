using System.Buffers.Binary;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;

namespace HybridCPU_ISE.NonRTL.Runtime;

public sealed record HybridCpuIseConsoleOutputV1(ulong Operation, string Text, string Utf16LittleEndianHex);

/// <summary>Synchronous diagnostic console sink; snapshots guest bytes and owns no guest roots or GUI state.</summary>
public sealed class HybridCpuIseConsoleProviderV1 : IHybridCpuHostServiceProviderV1
{
    private readonly Func<ulong, int, byte[]?> _read;
    private readonly int _maximumBytes;
    private readonly int _maximumEvents;
    private readonly List<HybridCpuIseConsoleOutputV1> _output = [];
    private int _bytes;

    public HybridCpuIseConsoleProviderV1(Func<ulong, int, byte[]?> read,
        int maximumBytes = 8 * 1024 * 1024, int maximumEvents = 4096)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        if (maximumBytes <= 0 || maximumEvents <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _maximumBytes = maximumBytes;
        _maximumEvents = maximumEvents;
    }

    public IReadOnlyList<HybridCpuIseConsoleOutputV1> Output => _output.ToArray();

    public HybridCpuHostProviderResultV1 Invoke(HybridCpuExternalServiceRequestV1 request)
    {
        if (request.Service != HybridCpuHostServiceV1.Console || request.Operation is not
            (HybridCpuConsoleServiceContractV1.WriteUtf16Operation or HybridCpuConsoleServiceContractV1.SetTitleUtf16Operation))
            return new(HybridCpuExternalServiceStatusV1.MissingSymbol, 0, 2, "Diagnostic console operation is not implemented.");
        if (request.PrivilegeMode != HybridCpuPrivilegeModeV1.User || request.Arguments.Count != 0 ||
            request.BufferLength > (ulong)HybridCpuConsoleServiceContractV1.MaximumCodeUnits * 2 ||
            (request.BufferLength & 1) != 0 ||
            (request.BufferLength == 0 ? request.BufferAddress != 0 || request.BufferAccess != HybridCpuHostBufferAccessV1.None
                : request.BufferAddress == 0 || request.BufferAccess != HybridCpuHostBufferAccessV1.Read) ||
            request.BufferAddress > ulong.MaxValue - request.BufferLength)
            return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Invalid console UTF-16 envelope.");
        int length = checked((int)request.BufferLength);
        if (_output.Count >= _maximumEvents || length > _maximumBytes - _bytes)
            return new(HybridCpuExternalServiceStatusV1.ProviderFailure, 0, 12, "Diagnostic console capacity exhausted.");
        byte[]? bytes = length == 0 ? [] : _read(request.BufferAddress, length);
        if (bytes is null || bytes.Length != length)
            return new(HybridCpuExternalServiceStatusV1.ProviderFailure, 0, 5, "Console buffer snapshot failed.");
        char[] characters = new char[length / 2];
        for (int index = 0; index < characters.Length; index++)
            characters[index] = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2, 2));
        _output.Add(new(request.Operation, new string(characters), Convert.ToHexString(bytes)));
        _bytes += length;
        return new(HybridCpuExternalServiceStatusV1.Success, 0, 0, string.Empty);
    }
}
