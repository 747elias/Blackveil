using Blackveil.Core;
namespace Blackveil;
internal sealed class UpdateSettingsPanel : UserControl
{
    private readonly UpdateCoordinator coordinator;
    private readonly CheckBox search = new() { Text = "Automatisch nach Updates suchen", AutoSize = true };
    private readonly CheckBox install = new() { Text = "Updates automatisch und ohne Rückfrage installieren", AutoSize = true };
    private readonly ComboBox checkWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox installWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly DateTimePicker checkTime = TimePicker(), installTime = TimePicker();
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(490, 0) };
    private readonly Button manual = new() { Text = "Jetzt nach Updates suchen", AutoSize = true };
    private readonly Button save = new() { Text = "Update-Einstellungen speichern", AutoSize = true };
    internal UpdateSettingsPanel(UpdateCoordinator coordinator)
    {
        this.coordinator = coordinator; Dock = DockStyle.Fill;
        var p = coordinator.Preferences;
        search.Checked = p.AutoCheck; install.Checked = p.AutoInstall;
        checkWhen.Items.AddRange(["Beim Start", "Alle 6 Stunden", "Täglich zur Uhrzeit", "Alle 7 Tage"]);
        installWhen.Items.AddRange(["Sobald ein Update gefunden wird", "Beim nächsten Start", "Täglich zur Uhrzeit"]);
        checkWhen.SelectedIndex = (int)p.CheckWhen; installWhen.SelectedIndex = (int)p.InstallWhen;
        checkTime.Value = DateTime.Today.AddMinutes(p.CheckMinute); installTime.Value = DateTime.Today.AddMinutes(p.InstallMinute);
        var layout = AppTheme.Stack();
        layout.Controls.Add(AppTheme.Title("Immer auf dem neuesten Stand"));
        layout.Controls.Add(AppTheme.Hint("Du entscheidest, wann Blackveil nach Updates sucht und sie installiert."));
        var searchCard = AppTheme.Card("Updates finden", "Automatische Suche und manuelle Suche bleiben unabhängig voneinander.");
        searchCard.Controls.Add(search);
        var searchTime = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(530,0) };
        searchTime.Controls.Add(checkWhen); searchTime.Controls.Add(checkTime); searchCard.Controls.Add(searchTime); layout.Controls.Add(searchCard);
        var installCard = AppTheme.Card("Updates installieren", "Ohne Installationsautomatik fragt Blackveil vor dem Update. Zeitpläne gelten, solange die App läuft.");
        installCard.Controls.Add(install);
        var installTimeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(530,0) };
        installTimeRow.Controls.Add(installWhen); installTimeRow.Controls.Add(installTime); installCard.Controls.Add(installTimeRow);
        installCard.Controls.Add(AppTheme.Hint(UpdateInstaller.IsMachineInstalled(Environment.ProcessPath!)
            ? "Für alle Benutzer installiert: Windows fragt bei Updates nach Administratorrechten. Diese Bestätigung kann nicht übersprungen werden."
            : "Updates werden im Hintergrund installiert. Während des Schwarzbilds wird die Installation verschoben.",530));
        layout.Controls.Add(installCard);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(580,0) };
        actions.Controls.Add(save); actions.Controls.Add(manual); layout.Controls.Add(actions);
        status.ForeColor = AppTheme.Muted; status.MaximumSize = new Size(580,0); status.Margin = new Padding(0,8,0,0);
        layout.Controls.Add(status); Controls.Add(layout); AppTheme.Polish(layout);
        search.CheckedChanged += (_, _) => EnableFields(); install.CheckedChanged += (_, _) => EnableFields();
        checkWhen.SelectedIndexChanged += (_, _) => EnableFields(); installWhen.SelectedIndexChanged += (_, _) => EnableFields();
        save.Click += (_, _) =>
        {
            try
            {
                coordinator.SavePreferences(new UpdatePreferences { AutoCheck = search.Checked, AutoInstall = install.Checked,
                    CheckWhen = (CheckSchedule)checkWhen.SelectedIndex, InstallWhen = (InstallSchedule)installWhen.SelectedIndex,
                    CheckMinute = (int)checkTime.Value.TimeOfDay.TotalMinutes, InstallMinute = (int)installTime.Value.TimeOfDay.TotalMinutes });
                status.Text = "Update-Einstellungen gespeichert. " + coordinator.Status;
            }
            catch (Exception error) { status.Text = error.Message; }
        };
        manual.Click += async (_, _) => await coordinator.CheckAsync(true);
        coordinator.Changed += RefreshStatus; RefreshStatus(); EnableFields();
    }
    private static DateTimePicker TimePicker() => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 100 };
    private void EnableFields()
    { checkWhen.Enabled = search.Checked; checkTime.Enabled = search.Checked && checkWhen.SelectedIndex == (int)CheckSchedule.Daily;
      installWhen.Enabled = install.Checked; installTime.Enabled = install.Checked && installWhen.SelectedIndex == (int)InstallSchedule.DailyAtTime; }
    private void RefreshStatus() { if (IsDisposed) return; status.Text = coordinator.Status; manual.Enabled = save.Enabled = !coordinator.Busy; }
    protected override void Dispose(bool disposing) { if (disposing) coordinator.Changed -= RefreshStatus; base.Dispose(disposing); }
}
