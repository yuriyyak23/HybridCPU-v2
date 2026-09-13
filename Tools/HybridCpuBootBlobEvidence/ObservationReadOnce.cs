using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Security.Cryptography;
using HybridCPU.Compiler.Core.Target.Runtime;

internal static class ObservationReadOnce
{
    public static async Task<int> Run(string path, string? imagePath = null)
    {
        try
        {
            var manifest = ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(path));
            ObservationTransportProtocol.ValidateManifest(manifest);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(timeout.Token);
            ObservationLocalAccess.VerifyServer(pipe, manifest.Host);
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(manifest), timeout.Token);
            async Task<ObservationTransportMessage> Read(long id, Guid? connection)
            {
                var value = ObservationTransportProtocol.Decode<ObservationTransportMessage>(await ObservationFrameTransport.ReadAsync(pipe, timeout.Token));
                if (value.Kind != "Response" || value.RequestId != id || value.ConnectionId == Guid.Empty ||
                    (connection.HasValue && value.ConnectionId != connection.Value)) throw new InvalidDataException("Response identity mismatch.");
                return value;
            }
            var welcome = await Read(0, null);
            if (welcome.Status != "Welcome") throw new InvalidDataException("Handshake rejected.");
            ObservationTransportProtocol.ValidateHandshake(JsonSerializer.SerializeToUtf8Bytes(welcome.Detail), manifest);
            ObservationTransportMessage result;
            long request = 1;
            if (welcome.Binding is { } binding)
            {
                await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(
                    request++, "CaptureOnce", 5000, binding.SegmentId, binding.Generation)), timeout.Token);
                result = await Read(1, welcome.ConnectionId);
                if (result.Status == "Observed" && (result.Binding != binding || result.Capture?.Frame?.Host != binding.Bridge.Identity))
                    throw new InvalidDataException("Capture binding mismatch.");
            }
            else result = welcome with { Status = "SegmentUnavailable" };
            await ObservationFrameTransport.WriteAsync(pipe, ObservationTransportProtocol.Encode(new ObservationTransportRequest(request, "Disconnect", 5000)), timeout.Token);
            var detached = await Read(request, welcome.ConnectionId);
            if (detached.Status != "Disconnected") throw new InvalidDataException("Disconnect not acknowledged.");
            object? location = null;
            if (imagePath is not null && result.Status == "Observed")
            {
                byte[] bytes = File.ReadAllBytes(imagePath);
                if (result.Binding is null || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                    result.Binding.ImageSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Observation image SHA256 mismatch.");
                var image = new HybridCpuRestrictedImageBuilderV1().Inspect(bytes);
                if (image.RuntimeBootstrap is null) throw new InvalidDataException("Image metadata unavailable.");
                ulong pc = result.Capture!.Frame!.Snapshot.LiveInstructionPointer;
                location = new { LivePc = pc, Methods = image.RuntimeBootstrap.CodeManagerRecords
                    .Where(row => pc >= image.ImageBase + (ulong)row.CodeStartOffsetBytes &&
                        pc - image.ImageBase - (ulong)row.CodeStartOffsetBytes < (ulong)row.CodeSizeBytes)
                    .Select(row => new { row.MethodIdentity,
                        NativeOffset = pc - image.ImageBase - (ulong)row.CodeStartOffsetBytes }).ToArray() };
            }
            var snapshot = result.Capture?.Frame?.Snapshot;
            Console.WriteLine(JsonSerializer.Serialize(new { Manifest = manifest, Observation = result,
                Progress = snapshot is null ? null : new { result.Binding?.SegmentId, result.Binding?.Kind,
                    snapshot.CycleCount, snapshot.LiveInstructionPointer, snapshot.IsStalled,
                    RetirePc = snapshot.RetireVisibilityCertificate.Pc,
                    RetirePublished = snapshot.RetireVisibilityCertificate.IsPublished,
                    snapshot.PipelineInstructionsRetired,
                    snapshot.HasExceptions, snapshot.PipelineIPC, snapshot.PipelineEfficiency,
                    snapshot.PipelineStallCycles, snapshot.PipelineDataHazards, snapshot.PipelineMemoryStalls,
                    InstructionCounter = snapshot.PipelineInstructionsRetired.HasValue ? "Available" : "Unavailable", Location = location,
                    Meaning = "One owner-boundary sample; cycles are not retired instructions or semantic progress." },
                Disconnect = detached.Status, GuestCompletion = "Unavailable", Qualification = "Unavailable" },
                new JsonSerializerOptions { WriteIndented = true }));
            return result.Status == "Observed" ? 0 : 3;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or
            OperationCanceledException or JsonException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("Observation unavailable: " + error.Message);
            return 4;
        }
    }
}
