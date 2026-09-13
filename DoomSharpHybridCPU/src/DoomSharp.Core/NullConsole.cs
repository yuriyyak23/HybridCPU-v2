namespace DoomSharp.Core;

public class NullConsole : IConsole
{
    public void Write(string message)
    {
    }

    public void WriteLine(string message)
    {
    }

    public void SetTitle(string title)
    {
    }

    public void Shutdown()
    {
    }
}
