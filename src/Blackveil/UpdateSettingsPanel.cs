using Blackveil.Core;
namespace Blackveil;
internal sealed class UpdateSettingsPanel : UserControl
{
    private readonly UpdateCoordinator coordinator;
    private readonly CheckBox search = new() { Text = "Automatically check for updates", AutoSize = true };
    private readonly CheckBox install = new() { Text = "Install updates automatically without asking", AutoSize = true };
    private readonly ComboBox checkWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox installWhen = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly DateTimePicker checkTime = TimePicker(), installTime = TimePicker();
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(490, 0) };
    private readonly Button manual = new() { Text = "Check for updates now", AutoSize = true };
    private readonly Button save = new() { Text = "Save update settings", AutoSize = true };
    internal UpdateSettingsPanel(UpdateCoordinator coordinator)
    {
        this.coordinator = coordinator; Dock = DockStyle.Fill;
        if (UpdateInstaller.IsMachineInstalled(Environment.ProcessPath!)) install.Text = "Install updates automatically (Windows approval required)";
        var p = coordinator.Preferences;
        search.Checked = p.AutoCheck; install.Checked = p.AutoInstall;
        checkWhen.Items.AddRange(["At startup", "Every 6 hours", "Daily at a set time", "Every 7 days"]);
        installWhen.Items.AddRange(["As soon as an update is found", "At next startup", "Daily at a set time"]);
        checkWhen.SelectedIndex = (int)p.CheckWhen; installWhen.SelectedIndex = (int)p.InstallWhen;
        checkTime.Value = DateTime.Today.AddMinutes(p.CheckMinute); installTime.Value = DateTime.Today.AddMinutes(p.InstallMinute);
        var layout = AppTheme.Stack();
        layout.Controls.Add(AppTheme.Title("Stay up to date"));
        layout.Controls.Add(AppTheme.Hint("Choose when Blackveil checks for updates and installs them."));
        var searchCard = AppTheme.Card("Find updates", "Check manually at any time, even with automatic checks turned off.");
        searchCard.Controls.Add(search);
        var searchTime = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(530,0) };
        searchTime.Controls.Add(checkWhen); searchTime.Controls.Add(checkTime); searchCard.Controls.Add(searchTime); layout.Controls.Add(searchCard);
        var installCard = AppTheme.Card("Install updates", "With automatic installation off, Blackveil asks before updating. Schedules run while the app is open.");
        installCard.Controls.Add(install);
        var installTimeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(530,0) };
        installTimeRow.Controls.Add(installWhen); installTimeRow.Controls.Add(installTime); installCard.Controls.Add(installTimeRow);
        installCard.Controls.Add(AppTheme.Hint(UpdateInstaller.IsMachineInstalled(Environment.ProcessPath!)
            ? "Installed for all users: Windows asks for administrator approval during updates. This prompt cannot be skipped."
            : "Updates install in the background. Installation is deferred while blackout is active.",530));
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
                status.Text = "Update settings saved. " + coordinator.Status;
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
