using Blackveil.Core;
namespace Blackveil;
internal sealed class UpdateSettingsPanel : UserControl
{
    private readonly UpdateCoordinator coordinator;
    private readonly CheckBox search = new() { Text = "Automatisch nach Updates suchen", AutoSize = true };
    private readonly CheckBox install = new() { Text = "Updates automatisch und ohne Rückfrage installieren", AutoSize = true };
    private readonly ComboBox checkWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly ComboBox installWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
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
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        layout.Controls.Add(search); layout.Controls.Add(new Label { Text = "Automatisch suchen:", AutoSize = true });
        layout.Controls.Add(checkWhen); layout.Controls.Add(checkTime); layout.Controls.Add(install);
        layout.Controls.Add(new Label { Text = "Automatisch installieren:", AutoSize = true }); layout.Controls.Add(installWhen); layout.Controls.Add(installTime);
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(490, 0), Text = "Ohne automatische Installation fragt Blackveil vor der Installation. Bei ausgeschalteter automatischer Suche wird nur auf Knopfdruck gesucht. Zeitpläne gelten, solange die App läuft; während des Schwarzbilds werden sie verschoben." });
        layout.Controls.Add(save); layout.Controls.Add(manual); layout.Controls.Add(status); Controls.Add(layout);
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
