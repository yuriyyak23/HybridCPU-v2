namespace DoomSharp.Core;

/// <summary>
/// A controlled guest-process termination. The platform bootstrap translates this
/// managed failure into process_exit; it must never be represented as a CPU trap.
/// </summary>
public sealed class DoomTerminationException : Exception
{
    public DoomTerminationException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
