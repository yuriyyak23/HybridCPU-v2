using System.Text.Json;
using System.Text.Json.Serialization;
using CpuInterfaceBridge.Diagnostics;

namespace HybridCpuIseImageRunner;

/// <summary>Read-only CLI adapter. Never opens the image path contained in the report.</summary>
public static class ReportCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is not (2 or 4) || args[0] != "--report" || string.IsNullOrWhiteSpace(args[1]) ||
            (args.Length == 4 && (args[2] != "--image" || string.IsNullOrWhiteSpace(args[3]))))
        {
            error.WriteLine("HCISE-DIAG0001: expected --report <runner.json> [--image <package.hcexe>]");
            return 2;
        }
        try
        {
            string reportPath = Path.GetFullPath(args[1]);
            using var report = File.OpenRead(reportPath);
            using var package = args.Length == 4 ? File.OpenRead(Path.GetFullPath(args[3])) : null;
            FailureCard card = CompilerToolExample.ReadFailure(report, reportPath, package);
            bool mismatch = card.Image.Verification == DigestVerification.Mismatch;
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            output.WriteLine(JsonSerializer.Serialize(new
            {
                schema = "hybridcpu.ise-image-runner.report-diagnostics/v1",
                mode = "PassiveReportImport",
                reportSource = reportPath,
                diagnosticOutcome = mismatch ? "ImageDigestMismatch" : "ReportImported",
                executionStartedByThisCommand = false,
                qualification = "Unavailable",
                card,
                limitations = new[]
                {
                    "Exit 0 means report import succeeded; it does not mean guest success or image qualification.",
                    "SessionId identifies this import; runner/v1 provides no run identity.",
                    "Loader status is not explicit in runner/v1. Outcome and reason are reported observations.",
                    "Code records are partial; per-ECALL provider reason, assembly, GC roots and CIL mapping may be unavailable.",
                    "SHA verification binds supplied package bytes to the reported digest, not to an authenticated execution."
                }
            }, options));
            if (!mismatch) return 0;
            error.WriteLine("HCISE-DIAG0004: supplied image SHA-256 differs from report SHA-256.");
            return 4;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or FormatException or OverflowException or ArgumentException or NotSupportedException)
        {
            error.WriteLine($"HCISE-DIAG0003: {ex.Message}");
            return 3;
        }
    }
}
