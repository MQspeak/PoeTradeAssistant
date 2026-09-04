using System.Windows;

namespace Poe2MarketScanner.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (System.Array.IndexOf(e.Args, "--install-chromium") >= 0)
        {
            var browserPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "browsers");
            System.Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserPath);
            System.Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT", "180000");
            try
            {
                using var log = new System.IO.StreamWriter(System.IO.Path.Combine(System.AppContext.BaseDirectory, "chromium-install.log")) { AutoFlush = true };
                System.Console.SetOut(System.IO.TextWriter.Synchronized(log));
                System.Console.SetError(System.IO.TextWriter.Synchronized(log));
                var exitCode = await System.Threading.Tasks.Task.Run(() =>
                    Microsoft.Playwright.Program.Main(new[] { "install", "chromium", "--no-shell" }));
                Shutdown(exitCode);
            }
            catch (System.Exception error)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "chromium-install-error.log"), error.ToString());
                Shutdown(1);
            }
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
