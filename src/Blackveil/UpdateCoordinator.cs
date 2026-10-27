using System.Reflection;
using Blackveil.Core;

namespace Blackveil;

internal sealed class UpdateCoordinator : IDisposable
{
    internal static Version CurrentVersion => Version.Parse(Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
    private readonly UpdateStorage storage;
    private readonly ReleaseClient client;
    private readonly Func<bool> canInteract;
    private readonly Action exitForUpdate;
    private readonly CancellationTokenSource cancel = new();
    private readonly System.Windows.Forms.Timer timer = new();
    private UpdateState state;
    private bool started, disposed;
    private DateTimeOffset? installDue;
    internal bool Busy { get; private set; }
    internal string Status { get; private set; } = "Noch nicht nach Updates gesucht.";
    internal event Action? Changed;
    internal UpdatePreferences Preferences { get; private set; }
    internal UpdateCoordinator(Func<bool> canInteract, Action exitForUpdate, UpdateStorage? storage = null, ReleaseClient? client = null)
    {
        this.storage = storage ?? new UpdateStorage(); this.client = client ?? new ReleaseClient();
        this.canInteract = canInteract; this.exitForUpdate = exitForUpdate;
        Preferences = this.storage.Load<UpdatePreferences>("update-settings.json");
        if (!Preferences.IsValid) Preferences = new();
        state = this.storage.Load<UpdateState>("update-state.json");
        if (state.LastCheckUtc is { } last) Status = "Letzte Suche: " + last.ToLocalTime().ToString("g");
        timer.Tick += async (_, _) => { timer.Stop(); await CheckAsync(false, false, installDue is { } due && due <= DateTimeOffset.Now.AddSeconds(2)); };
    }
    internal void SavePreferences(UpdatePreferences preferences)
    {
        if (Busy) throw new InvalidOperationException("Bitte zuerst die laufende Update-Suche abwarten.");
        if (!preferences.IsValid) throw new ArgumentException("Ungültiger Update-Zeitplan.");
        storage.Save("update-settings.json", preferences); Preferences = preferences;
        if (!preferences.AutoInstall) { state = state with { PendingVersion = null }; storage.Save("update-state.json", state); installDue = null; }
        Schedule(); Changed?.Invoke();
    }
    internal async Task StartAsync()
    {
        if (started) return;
        started = true;
        _ = Task.Run(() =>
        {
            try
            {
                string cache = Path.Combine(storage.Root, "updates");
                if (Directory.Exists(cache)) foreach (string directory in Directory.GetDirectories(cache))
                {
                    try { if (Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddDays(-7)) Directory.Delete(directory, true); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Log.Write("Old update cache could not be removed", error); }
                }
            }
            catch (Exception error) { Log.Write("Update cache cleanup failed", error); }
        });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool searchDue = Preferences.AutoCheck && (Preferences.CheckWhen == CheckSchedule.Startup ||
            Preferences.CheckWhen == CheckSchedule.EverySixHours && (state.LastCheckUtc is null || now - state.LastCheckUtc >= TimeSpan.FromHours(6)) ||
            Preferences.CheckWhen == CheckSchedule.Weekly && (state.LastCheckUtc is null || now - state.LastCheckUtc >= TimeSpan.FromDays(7)));
        if (searchDue || Preferences.AutoInstall && Preferences.InstallWhen == InstallSchedule.NextStartup && state.PendingVersion is not null)
            await CheckAsync(false, true, false);
        else Schedule();
    }
    internal async Task CheckAsync(bool manual, bool startup = false, bool scheduledInstall = false)
    {
        if (Busy || disposed) return;
        if (!canInteract()) { if (!manual) Arm(TimeSpan.FromMinutes(2)); return; }
        bool deferred = false;
        Busy = true; SetStatus("Suche nach Updates …");
        try
        {
            state = state with { LastCheckUtc = DateTimeOffset.UtcNow }; storage.Save("update-state.json", state);
            ReleaseInfo release = await client.LatestAsync(cancel.Token);
            if (disposed) return;
            if (!canInteract()) { deferred = true; SetStatus("Update-Prüfung wird nach dem Schwarzbild fortgesetzt."); return; }
            if (release.Version <= CurrentVersion)
            {
                state = state with { PendingVersion = null }; installDue = null;
                SetStatus("Blackveil ist aktuell (" + CurrentVersion + ").");
            }
            else
            {
                if ((state.FailedVersion == release.Version.ToString() || state.FailedVersion == "all") && !manual)
                { SetStatus("Letztes Update fehlgeschlagen. Bitte über die manuelle Suche erneut versuchen."); return; }
                SetStatus("Version " + release.Version + " ist verfügbar.");
                bool eligible = UpdatePolicy.ShouldInstall(Preferences, manual, startup, scheduledInstall);
                if (!eligible)
                {
                    if (Preferences.AutoInstall)
                    {
                        state = state with { PendingVersion = release.Version.ToString() };
                        SetStatus("Update " + release.Version + " wartet auf den gewählten Installationszeitpunkt.");
                    }
                    else if (!manual)
                    {
                        // Asking is the selected alternative to silent automatic installation.
                        if (MessageBox.Show($"Blackveil {release.Version} ist verfügbar. Jetzt im Hintergrund installieren und neu starten?\n\n{release.Notes[..Math.Min(6000, release.Notes.Length)]}", "Blackveil – Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            eligible = true;
                    }
                }
                else if (!Preferences.AutoInstall)
                    eligible = MessageBox.Show($"Blackveil {release.Version} ist verfügbar. Jetzt im Hintergrund installieren und neu starten?\n\n{release.Notes[..Math.Min(6000, release.Notes.Length)]}", "Blackveil – Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;
                if (eligible)
                {
                    if (!canInteract()) { deferred = true; state = state with { PendingVersion = release.Version.ToString() }; return; }
                    bool installed = UpdateInstaller.IsInstalled(Environment.ProcessPath!);
                    string directory = Path.Combine(storage.Root, "updates", Guid.NewGuid().ToString("N"));
                    SetStatus("Update wird heruntergeladen und geprüft …");
                    string hash = await client.DownloadAsync(release, installed, directory, cancel.Token);
                    if (disposed) return;
                    string package = Path.Combine(directory, installed ? $"Blackveil-Setup-{release.Version}.exe" : "Blackveil.exe");
                    if (!canInteract()) { deferred = true; state = state with { PendingVersion = release.Version.ToString() }; return; }
                    UpdateInstaller.LaunchHelper(directory, package, hash, installed, release.Version);
                    exitForUpdate();
                }
            }
            storage.Save("update-state.json", state);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (Exception error)
        {
            Log.Write("Update check/download failed", error);
            SetStatus("Update nicht möglich: " + error.Message);
            // Background failures stay in the status display and local log.
        }
        finally
        {
            Busy = false;
            if (!disposed)
            {
                try { storage.Save("update-state.json", state); }
                catch (Exception error) { Log.Write("Could not persist update state", error); }
                if (deferred) Arm(TimeSpan.FromMinutes(2)); else Schedule();
                Changed?.Invoke();
            }
        }
    }
    private void SetStatus(string text) { Status = text; if (!disposed) Changed?.Invoke(); }
    private void Schedule()
    {
        timer.Stop(); installDue = null;
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset? next = UpdatePolicy.NextCheck(Preferences, state.LastCheckUtc, now);
        if (Preferences.AutoInstall && state.PendingVersion is not null && Preferences.InstallWhen == InstallSchedule.DailyAtTime)
        {
            installDue = UpdatePolicy.AtNextLocalTime(now, Preferences.InstallMinute);
            if (next is null || installDue < next) next = installDue;
        }
        if (next is { } due) Arm(UpdatePolicy.DelayUntil(due, now));
    }
    private void Arm(TimeSpan delay)
    { if (disposed) return; timer.Stop(); timer.Interval = (int)Math.Clamp(delay.TotalMilliseconds, 1000, int.MaxValue); timer.Start(); }
    internal Form? ShowChanges()
    {
        string current = CurrentVersion.ToString();
        UpdateResult? result = null;
        string resultPath = Path.Combine(storage.Root, "update-result.json");
        if (File.Exists(resultPath))
        {
            result = storage.Load<UpdateResult>("update-result.json"); File.Delete(resultPath);
            state = state with { PendingVersion = null, FailedVersion = result.Success ? null : result.Version ?? state.PendingVersion };
            // A failed helper restart must never immediately repeat the same installer.
            if (!result.Success && state.FailedVersion is null) state = state with { FailedVersion = "all" };
            storage.Save("update-state.json", state);
        }
        if (state.LastSeenVersion == current && result is null) return null;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Blackveil.Changelog")!;
        using var reader = new StreamReader(stream);
        string changes = ReleaseNotes.Since(reader.ReadToEnd(), state.LastSeenVersion, current);
        var form = new ChangesWindow(current, (result?.Message is { } message ? message + "\r\n\r\n" : "") + changes);
        form.FormClosed += (_, _) =>
        {
            try { state = state with { LastSeenVersion = current }; storage.Save("update-state.json", state); }
            catch (Exception error) { Log.Write("Could not save shown version", error); }
        };
        form.Show(); return form;
    }
    public void Dispose()
    { if (disposed) return; disposed = true; timer.Stop(); timer.Dispose(); cancel.Cancel(); client.Dispose(); /* CTS stays alive until pending continuations finish. */ }
}
