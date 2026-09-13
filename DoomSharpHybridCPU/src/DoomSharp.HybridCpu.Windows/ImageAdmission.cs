using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using HybridCPU.Compiler.Core.Target.Runtime;

namespace DoomSharp.HybridCpu.Windows;

public sealed record AdmissionIssue(string Code, string Detail);
public sealed record WadInspection(string Kind, int LumpCount, long Bytes, string Sha256);
public sealed record AdmissionReport(HybridCpuRestrictedImageV1? Image, WadInspection? Wad,
    IReadOnlyList<AdmissionIssue> Issues)
{
    // File admission enables an explicit bounded loader run; it never grants
    // qualification. HCDOOMGUI1100/1102 are warnings, while other issues reject input.
    public bool CanExecute => Image?.Status == HybridCpuStartupStatusV1.Success && Wad is not null &&
        Issues.All(issue => issue.Code is "HCDOOMGUI1100" or "HCDOOMGUI1102");
}

public static class ImageAdmission
{
    public const long MaximumWadBytes = 256L * 1024 * 1024;
    public const int MaximumLumps = 1_000_000;

    public static AdmissionReport Inspect(string imagePath, string wadPath, CancellationToken cancellation = default)
    {
        var issues = new List<AdmissionIssue>();
        HybridCpuRestrictedImageV1? image = null;
        WadInspection? wad = null;
        if (string.IsNullOrWhiteSpace(imagePath))
            issues.Add(new("HCDOOMGUI1001", "Выберите настоящий .hcexe. DLL CoreCLR не является AOT-образом."));
        else
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (!string.Equals(Path.GetExtension(imagePath), ".hcexe", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("HCDOOMGUI1002", "Ожидается файл .hcexe, а не managed DLL или raw binary."));
                else
                {
                    using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > HybridCpuRestrictedStartupOptionsV1.Production.MaximumPackageBytes)
                        throw new InvalidDataException("Превышен размер image package по startup contract.");
                    var bytes = new byte[checked((int)stream.Length)];
                    stream.ReadExactly(bytes);
                    cancellation.ThrowIfCancellationRequested();
                    image = new HybridCpuRestrictedImageBuilderV1().Inspect(bytes);
                    foreach (var diagnostic in image.Diagnostics)
                        issues.Add(new(diagnostic.Code, diagnostic.Message));
                    if (image.Status == HybridCpuStartupStatusV1.Success && image.RuntimeBootstrap is null)
                        issues.Add(new("HCDOOMGUI1003", "Нет managed bootstrap descriptor. Scalar image не является Doom guest."));
                }
            }
            catch (Exception exception) when (IsInputFailure(exception))
            {
                issues.Add(new("HCDOOMGUI1002", exception.Message));
            }
        }
        if (string.IsNullOrWhiteSpace(wadPath))
            issues.Add(new("HCDOOMGUI1010", "Выберите локальный IWAD/PWAD. WAD не встроен в приложение."));
        else
        {
            try { wad = InspectWad(wadPath, cancellation); }
            catch (Exception exception) when (IsInputFailure(exception))
            { issues.Add(new("HCDOOMGUI1011", exception.Message)); }
        }
        cancellation.ThrowIfCancellationRequested();
        issues.Add(new("HCDOOMGUI1100", "Production managed loader и ISE ECALL bridge подключены. Результат допускает только явный bounded execution; qualification образа здесь не устанавливается."));
        issues.Add(new("HCDOOMGUI1102", "GC roots/safepoints, EH и handled/unhandled/process-exit outcome не подтверждены. Execution result остаётся unqualified."));
        return new(image, wad, issues.AsReadOnly());
    }

    public static WadInspection InspectWad(string path, CancellationToken cancellation = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = stream.Length;
        if (length < 12 || length > MaximumWadBytes) throw new InvalidDataException("WAD размер вне допустимых границ 12..256 MiB.");
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);
        string kind = System.Text.Encoding.ASCII.GetString(header[..4]);
        if (kind is not ("IWAD" or "PWAD")) throw new InvalidDataException("Неверная сигнатура WAD: ожидается IWAD/PWAD.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        int directory = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        if (count < 0 || count > MaximumLumps || directory < 12 || directory > length || (long)count * 16 > length - directory)
            throw new InvalidDataException("Некорректные count/offset или усечённый каталог WAD.");
        stream.Position = directory;
        Span<byte> entry = stackalloc byte[16];
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            stream.ReadExactly(entry);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry);
            int size = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
            if (offset < 0 || size < 0 || offset > length || size > length - offset)
                throw new InvalidDataException($"Lump {i}: диапазон данных за пределами WAD.");
        }
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        int read;
        while ((read = stream.Read(buffer)) != 0)
        {
            cancellation.ThrowIfCancellationRequested();
            hash.AppendData(buffer.AsSpan(0, read));
        }
        return new(kind, count, length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static bool IsInputFailure(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or OverflowException or NotSupportedException or System.Text.Json.JsonException;
}
