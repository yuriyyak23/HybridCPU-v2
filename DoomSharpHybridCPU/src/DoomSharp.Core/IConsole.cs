namespace DoomSharp.Core;

public interface IConsole
{
    void Write(string message);
    void WriteLine(string message);

    void SetTitle(string title);
    void Shutdown();
}
