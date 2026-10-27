using System.Text.Json;
using Blackveil.Core;

namespace Blackveil;

internal sealed record UpdateState
{
    public DateTimeOffset? LastCheckUtc { get; init; }
    public string? FailedVersion { get; init; }
    public string? PendingVersion { get; init; }
    public string? LastSeenVersion { get; init; }
}
internal sealed class UpdateStorage
{
    internal string Root { get; }
    internal UpdateStorage(string? root = null) => Root = root ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blackveil");
    internal T Load<T>(string name) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root, name))) ?? new T(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { if (error is not FileNotFoundException and not DirectoryNotFoundException) Log.Write("Update settings could not be read", error); return new T(); }
    }
    internal void Save<T>(string name, T value)
    {
        Directory.CreateDirectory(Root);
        string file = Path.Combine(Root, name), temp = file + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value)); File.Move(temp, file, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
