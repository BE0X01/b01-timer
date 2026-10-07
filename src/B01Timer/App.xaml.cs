using B01Timer.Core;
using System.IO;
using System.Windows;

namespace B01Timer;

public partial class App : Application
{
    internal static Action<string>? Diagnostic;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        int diagnostic = Array.IndexOf(e.Args, "--diagnostics");
        if (diagnostic >= 0 && diagnostic + 1 < e.Args.Length)
        {
            string path = e.Args[diagnostic + 1];
            Diagnostic = message => File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            DispatcherUnhandledException += (_, args) => Diagnostic(args.Exception.ToString());
        }
        string directory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "B01Timer", "settings.json");
        int option = Array.IndexOf(e.Args, "--settings-dir");
        if (option >= 0 && option + 1 < e.Args.Length)
        {
            directory = e.Args[option + 1];
            legacy = Path.Combine(directory, "settings.json");
        }
        var store = new SettingsStore(directory, legacy); var settings = store.Load();
        ThemeManager.Apply(settings.Theme);
        MainWindow = new MainWindow(settings, store); MainWindow.Show();
    }
}
