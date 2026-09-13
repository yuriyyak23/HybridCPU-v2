using System.ComponentModel;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using DoomSharp.Core;
using DoomSharp.Windows.Annotations;

namespace DoomSharp.Windows.ViewModels;

public class ConsoleViewModel : IConsole, INotifyPropertyChanged
{
    public static readonly ConsoleViewModel Instance = new();

    private ConsoleViewModel() {}

    private string _consoleOutput = "";
    private string _title = "DooM# - Console output";

    public string ConsoleOutput
    {
        get => _consoleOutput;
        set
        {
            _consoleOutput = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public void Write(string message)
    {
#if DEBUG
        if (Debugger.IsAttached)
        {
            Debug.WriteLine(message.TrimEnd('\r', '\n'));
        }
#endif
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ConsoleOutput += message;
            return;
        }

        dispatcher.Invoke(() => ConsoleOutput += message);
    }

    public void WriteLine(string message)
    {
        Write(message);
        Write(Environment.NewLine);
    }

    public void SetTitle(string title)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            UpdateTitle(title);
            return;
        }

        dispatcher.Invoke(() => UpdateTitle(title));
    }

    private void UpdateTitle(string title)
    {
        Title = $"{title} - Console output";
        MainViewModel.Instance.Title = title;
    }

    public void Shutdown()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Application.Current?.Shutdown();
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
