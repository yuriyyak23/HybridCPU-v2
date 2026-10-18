using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

internal static class ObservationLocalAccess
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode, uint instances,
        uint outputBytes, uint inputBytes, uint timeout, ref SecurityAttributes security);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);

    public static NamedPipeServerStream CreateServer(string name, string sid, bool first)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _ = new SecurityIdentifier(sid);
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor($"D:P(A;;GA;;;{sid})", 1, out var descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            // DUPLEX | OVERLAPPED; optional FIRST_PIPE_INSTANCE; BYTE | WAIT | REJECT_REMOTE_CLIENTS.
            var handle = CreateNamedPipe(@"\\.\pipe\" + name, 3u | 0x40000000u | (first ? 0x00080000u : 0),
                0x8, 8, 65536, 65536, 5000, ref security);
            if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
            try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
            catch { handle.Dispose(); throw; }
        }
        finally { LocalFree(descriptor); }
    }

    public static void PublishManifest(string path, byte[] bytes, string sid)
    {
        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        if (File.Exists(full)) throw new IOException("Immutable manifest already exists.");
        var acl = new FileSecurity();
        var user = new SecurityIdentifier(sid);
        acl.SetOwner(user);
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
        string pending = full + ".pending-" + Guid.NewGuid().ToString("N");
        // Restrictive ACL is applied at creation, before manifest bytes become readable.
        using (var stream = FileSystemAclExtensions.Create(new FileInfo(pending), FileMode.CreateNew,
            FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, acl))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(pending, full, false);
    }

    public static byte[] ReadManifest(string path)
    {
        using var user = WindowsIdentity.GetCurrent();
        var info = new FileInfo(Path.GetFullPath(path));
        var acl = info.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        if (!acl.AreAccessRulesProtected || !acl.GetOwner(typeof(SecurityIdentifier)).Equals(user.User))
            throw new UnauthorizedAccessException("Manifest owner/ACL mismatch.");
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !rule.IdentityReference.Equals(user.User))
                throw new UnauthorizedAccessException("Manifest grants access to another SID.");
        using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Manifest size rejected.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public static void VerifyServer(NamedPipeClientStream pipe, ObservationTransportIdentity expected)
    {
        if (ObservationPipePeer.ServerProcessId(pipe) != expected.ProcessId) throw new UnauthorizedAccessException("Server PID mismatch.");
        using var process = Process.GetProcessById(expected.ProcessId);
        if (process.StartTime.ToUniversalTime() != expected.ProcessStartUtc.UtcDateTime)
            throw new UnauthorizedAccessException("Server process start mismatch.");
        if (!OpenProcessToken(process.Handle, 0x0008, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
            if (identity.User?.Value != expected.UserSid) throw new UnauthorizedAccessException("Server SID mismatch.");
        using var own = WindowsIdentity.GetCurrent();
        if (own.User?.Value != expected.UserSid) throw new UnauthorizedAccessException("Expected host SID is not current user.");
    }
}
