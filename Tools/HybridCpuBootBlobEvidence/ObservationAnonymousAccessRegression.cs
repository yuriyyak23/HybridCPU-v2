using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

internal static class ObservationAnonymousAccessRegression
{
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool ImpersonateAnonymousToken(IntPtr thread);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool RevertToSelf();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    public static void Verify(string pipeName)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            bool impersonated = false;
            try
            {
                if (!ImpersonateAnonymousToken(GetCurrentThread())) throw new Win32Exception(Marshal.GetLastWin32Error());
                impersonated = true;
                using var identity = WindowsIdentity.GetCurrent(true);
                if (identity?.User?.Value != "S-1-5-7") throw new InvalidOperationException("Anonymous SID unavailable.");
                using var handle = CreateFile(@"\\.\pipe\" + pipeName, 0xc0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
                int error = Marshal.GetLastWin32Error();
                if (!handle.IsInvalid || error != 5)
                    throw new InvalidOperationException($"Expected OS ACCESS_DENIED for anonymous SID; invalid={handle.IsInvalid}, error={error}.");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (impersonated && !RevertToSelf()) Environment.FailFast("Test thread failed to revert anonymous impersonation.");
            }
        });
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Anonymous admission test did not finish.");
        if (failure is not null) throw new InvalidOperationException("Anonymous pipe access test failed.", failure);
        Console.WriteLine("PASS actual anonymous SID S-1-5-7 rejected by OS pipe admission: ACCESS_DENIED (5).");
    }
}
