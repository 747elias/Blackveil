namespace Blackveil;

internal static class Log
{
    private static readonly object Sync = new();
    internal static void Write(string context, Exception? error = null)
    {
        try
        {
            lock (Sync)
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackveil", "logs");
                Directory.CreateDirectory(dir);
                foreach (string old in Directory.GetFiles(dir, "*.log").Where(p => File.GetLastWriteTimeUtc(p) < DateTime.UtcNow.AddDays(-14))) File.Delete(old);
                // Only operational failures; never key codes, screen contents or user input.
                File.AppendAllText(Path.Combine(dir, $"{DateTime.UtcNow:yyyy-MM-dd}.log"), $"{DateTime.UtcNow:O} {context} {error}\n");
            }
        }
        catch { /* Logging must never prevent cleanup. */ }
    }
}
