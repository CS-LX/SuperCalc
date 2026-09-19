using Microsoft.UI.Xaml;

namespace SuperCalc.App;

public partial class App : Application
{
    private Window? window;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SuperCalc");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "last-error.txt"), e.Exception.ToString());
            }
            catch { /* Never mask the original failure if diagnostic output cannot be written. */ }
        };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var smokeIndex = Array.IndexOf(commandLine, "--smoke-test");
        var smokeDirectory = smokeIndex >= 0 && smokeIndex + 1 < commandLine.Length ? Path.GetFullPath(commandLine[smokeIndex + 1]) : null;
        window = new MainWindow(smokeDirectory);
        window.Activate();
    }
}
