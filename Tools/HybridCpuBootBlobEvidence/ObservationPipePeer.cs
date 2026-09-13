using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Security.Principal;

internal static class ObservationPipePeer
{
    public static void RequireClientSid(NamedPipeServerStream pipe, SecurityIdentifier expected)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        ArgumentNullException.ThrowIfNull(expected);
        SecurityIdentifier? actual = null;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent(true);
            actual = identity?.User;
        });
        if (actual is null || !actual.Equals(expected))
            throw new UnauthorizedAccessException("Pipe client SID mismatch or unavailable.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    public static int ClientProcessId(NamedPipeServerStream pipe)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint id))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return checked((int)id);
    }

    public static int ServerProcessId(NamedPipeClientStream pipe)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint id))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return checked((int)id);
    }
}
