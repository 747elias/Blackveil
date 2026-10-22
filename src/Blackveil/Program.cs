namespace Blackveil;

internal static class Program
{
    internal static TrayContext? Context;
    private static int uiThread;
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\Blackveil.Singleton", out bool first);
        if (!first) return;
        uiThread = Environment.CurrentManagedThreadId;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Fatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fatal(e.ExceptionObject as Exception);
        Application.ApplicationExit += (_, _) => Context?.Stop();
        try { using var context = new TrayContext(); Context = context; Application.Run(context); }
        catch (Exception error) { Fatal(error); }
        finally { Context?.Stop(); Context = null; mutex.ReleaseMutex(); }
    }
    private static void Fatal(Exception? error)
    {
        if (Environment.CurrentManagedThreadId != uiThread)
        {
            Log.Write("Fatal background failure; Windows will release process resources", error);
            Environment.Exit(7);
        }
        Context?.Stop();
        Log.Write("Unhandled failure", error);
        MessageBox.Show($"Blackveil wurde wegen eines Fehlers beendet. Der Schwarzbild-Modus wurde aufgehoben.\n\n{error?.Message}\n\nProtokoll: %LOCALAPPDATA%\\Blackveil\\logs", "Blackveil", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Exit();
    }
}
