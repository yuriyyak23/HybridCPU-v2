namespace HybridCPU_ISE.Tests.TestHelpers;

/// <summary>Historical blob evidence requires the repository, not a source-only distribution.</summary>
internal sealed class GitHistoryFactAttribute : Xunit.FactAttribute
{
    public GitHistoryFactAttribute()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HybridCPU v2.slnx")))
            directory = directory.Parent;

        if (directory is null ||
            !(Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
              File.Exists(Path.Combine(directory.FullName, ".git"))))
        {
            Skip = "Historical Git-object evidence is unavailable in this source-only workspace; no evidence acceptance is claimed.";
            return;
        }

        string[] candidates = OperatingSystem.IsWindows() ? ["git.exe", "git.cmd"] : ["git"];
        if (!(Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Any(path => candidates.Any(name => File.Exists(Path.Combine(path.Trim('"'), name)))))
            Skip = "Historical Git-object evidence requires git on PATH; no evidence acceptance is claimed.";
    }
}
