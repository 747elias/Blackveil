using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blackveil.Core;

namespace Blackveil;

internal sealed record ReleaseInfo(Version Version, string Notes, Uri Exe, Uri Installer, Uri Checksums);
internal sealed class ReleaseClient : IDisposable
{
    private readonly HttpClient http;
    internal ReleaseClient(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromMinutes(5);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Blackveil/" + UpdateCoordinator.CurrentVersion);
    }
    internal async Task<ReleaseInfo> LatestAsync(CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string json = await ReadTextAsync(new Uri("https://api.github.com/repos/747elias/Blackveil/releases/latest"), 2_000_000, timeout.Token);
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement);
    }
    internal static ReleaseInfo Parse(JsonElement json)
    {
        if (json.GetProperty("draft").GetBoolean() || json.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("Kein stabiles Release.");
        string tag = json.GetProperty("tag_name").GetString() ?? "";
        Version version = UpdatePolicy.StableVersion(tag) ?? throw new InvalidDataException("Ungültige Release-Version.");
        Uri Asset(string name)
        {
            var matches = json.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"Release-Datei fehlt oder ist doppelt: {name}");
            var uri = new Uri(matches[0].GetProperty("browser_download_url").GetString()!);
            if (uri.AbsoluteUri != $"https://github.com/747elias/Blackveil/releases/download/{tag}/{name}")
                throw new InvalidDataException("Unerwartete Download-Adresse.");
            return uri;
        }
        string notes = json.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        return new(version, notes, Asset("Blackveil.exe"), Asset($"Blackveil-Setup-{version}.exe"), Asset("SHA256SUMS.txt"));
    }
    private async Task<string> ReadTextAsync(Uri uri, int max, CancellationToken cancel)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var result = new MemoryStream();
        byte[] buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, cancel)) != 0)
        { if (result.Length + read > max) throw new InvalidDataException("Update-Antwort ist zu gross."); await result.WriteAsync(buffer.AsMemory(0, read), cancel); }
        return System.Text.Encoding.UTF8.GetString(result.ToArray());
    }
    internal static string ExpectedHash(string manifest, string name)
    {
        var matches = manifest.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 66 && s[64..].TrimStart(' ', '*') == name).ToArray();
        if (matches.Length != 1 || !Regex.IsMatch(matches[0][..64], "^[0-9a-fA-F]{64}$")) throw new InvalidDataException("SHA256-Prüfsumme fehlt oder ist ungültig.");
        return matches[0][..64];
    }
    internal async Task<string> DownloadAsync(ReleaseInfo release, bool installed, string directory, CancellationToken cancel)
    {
        Uri uri = installed ? release.Installer : release.Exe;
        string name = Uri.UnescapeDataString(uri.Segments[^1]);
        string hash = ExpectedHash(await ReadTextAsync(release.Checksums, 65536, cancel), name);
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, name);
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancel))
        await using (var output = File.Create(target))
        {
            byte[] buffer = new byte[81920]; int read; long size = 0;
            while ((read = await source.ReadAsync(buffer, cancel)) != 0)
            { size += read; if (size > 250_000_000) throw new InvalidDataException("Update-Datei ist zu gross."); await output.WriteAsync(buffer.AsMemory(0, read), cancel); }
        }
        await VerifyAsync(target, hash, cancel);
        return hash;
    }
    internal static async Task VerifyAsync(string path, string expected, CancellationToken cancel)
    {
        await using var file = File.OpenRead(path);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancel));
        if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA256-Prüfung fehlgeschlagen. Das Update wird nicht ausgeführt.");
    }
    public void Dispose() => http.Dispose();
}
