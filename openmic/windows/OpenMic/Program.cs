using Velopack;

namespace OpenMic;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Must run first: handles Velopack install/update/uninstall hooks.
        VelopackApp.Build().Run();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.Run(new MainForm());
    }
}
