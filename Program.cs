using BlackStarHydra360.UI;
using BlackStarHydra360.Logging;

namespace BlackStarHydra360;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (s, e) =>
        {
            AppLogger.Critical("Unhandled UI thread exception", e.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nSee log for details.",
                "Black Star Hydra 360 — Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLogger.Critical($"Unhandled domain exception (terminating={e.IsTerminating})", ex);
        };

        AppLogger.Info("★ Black Star Hydra 360 Pro Audit starting up ★");

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            AppLogger.Critical("Fatal error during application run", ex);
            MessageBox.Show($"Critical error: {ex.Message}", "Black Star Hydra 360",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
