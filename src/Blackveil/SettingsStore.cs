using System.Text.Json;
using Blackveil.Core;
using Shortcut = Blackveil.Core.Shortcut;

namespace Blackveil;

internal sealed class SettingsStore
{
    private readonly string path;
    internal SettingsStore(string? path = null) => this.path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackveil", "settings.json");
    internal Shortcut Load()
    {
        try
        {
            if (!File.Exists(path)) return Shortcut.Default;
            var shortcut = JsonSerializer.Deserialize<Shortcut>(File.ReadAllText(path));
            if (shortcut?.IsValid == true) return shortcut;
            throw new InvalidDataException("Ungültige Shortcut-Einstellung.");
        }
        catch (Exception error) { Log.Write("Settings could not be loaded; using default shortcut", error); return Shortcut.Default; }
    }
    internal void Save(Shortcut shortcut)
    {
        if (!shortcut.IsValid) throw new ArgumentException("Ungültiger Shortcut.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(shortcut));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
