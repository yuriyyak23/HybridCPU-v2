using System.Globalization;

namespace HybridCPU_ISE.Tests.TestHelpers;

// Formatting fixtures must specify their culture rather than inherit the test host locale.
internal sealed class TestCultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUi = CultureInfo.CurrentUICulture;

    internal TestCultureScope(string name)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previous;
        CultureInfo.CurrentUICulture = _previousUi;
    }
}
