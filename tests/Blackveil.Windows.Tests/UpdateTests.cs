using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blackveil;
using Blackveil.Core;

internal static class UpdateTests
{
    private static string Metadata => JsonSerializer.Serialize(new { draft = false, prerelease = false,
        tag_name = "v" + UpdateCoordinator.CurrentVersion, body = "Release notes", assets = new[] {
            Asset("Blackveil.exe"), Asset($"Blackveil-Setup-{UpdateCoordinator.CurrentVersion}.exe"), Asset("SHA256SUMS.txt") } });
    private static object Asset(string name) => new { name, browser_download_url = $"https://github.com/747elias/Blackveil/releases/download/v{UpdateCoordinator.CurrentVersion}/{name}" };
    private sealed class FakeHttp(bool corrupt = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string body = path.EndsWith("/latest") ? Metadata : path.EndsWith("SHA256SUMS.txt") ?
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("valid payload"))) + "  Blackveil.exe\n" : corrupt ? "tampered payload" : "valid payload";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Blackveil-update-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            Task.Run(async () =>
            {
                using var client = new ReleaseClient(new FakeHttp());
                var release = await client.LatestAsync(CancellationToken.None);
                await client.DownloadAsync(release, false, Path.Combine(root, "valid"), CancellationToken.None);
                using var bad = new ReleaseClient(new FakeHttp(true));
                bool rejected = false;
                try { await bad.DownloadAsync(release, false, Path.Combine(root, "bad"), CancellationToken.None); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Tampered download rejected");
                using var evil = JsonDocument.Parse(Metadata.Replace("https://github.com/747elias/Blackveil/", "https://example.org/evil/"));
                rejected = false;
                try { ReleaseClient.Parse(evil.RootElement); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Foreign asset origin rejected");
                rejected = false;
                try { ReleaseClient.ExpectedHash("not a manifest", "Blackveil.exe"); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Missing checksum rejected");
            }).GetAwaiter().GetResult();
            var store = new UpdateStorage(root);
            using var updates = new UpdateCoordinator(() => true, () => throw new Exception("Unexpected install"), store, new ReleaseClient(new FakeHttp()));
            using var window = new Form();
            using var panel = new UpdateSettingsPanel(updates);
            window.Controls.Add(panel); window.Show();
            var controls = Descendants(panel).ToArray();
            controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Automatisch nach")).Checked = false;
            controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Updates automatisch")).Checked = true;
            var combos = controls.OfType<ComboBox>().ToArray();
            combos[0].SelectedIndex = (int)CheckSchedule.Daily;
            combos[1].SelectedIndex = (int)InstallSchedule.NextStartup;
            controls.OfType<Button>().Single(b => b.Text == "Update-Einstellungen speichern").PerformClick();
            var saved = store.Load<UpdatePreferences>("update-settings.json");
            Require(!saved.AutoCheck && saved.AutoInstall && saved.CheckWhen == CheckSchedule.Daily && saved.InstallWhen == InstallSchedule.NextStartup, "Independent toggles and schedules persist through GUI");
            controls.OfType<Button>().Single(b => b.Text == "Jetzt nach Updates suchen").PerformClick();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while ((updates.Busy || !updates.Status.Contains("aktuell")) && clock.ElapsedMilliseconds < 5000)
            { Application.DoEvents(); Thread.Sleep(5); }
            Require(updates.Status.Contains("aktuell"), "Manual search works with automatic search disabled");
            window.Close();
            Console.WriteLine("PASS: update metadata, origin restrictions, valid download, checksum rejection, settings GUI toggles/schedules and manual search.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
}
