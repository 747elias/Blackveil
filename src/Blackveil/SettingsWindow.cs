using Blackveil.Core;
using Shortcut = Blackveil.Core.Shortcut;

namespace Blackveil;

internal sealed class SettingsWindow : Form
{
    private readonly TextBox shortcutText = new() { ReadOnly = true, Dock = DockStyle.Fill, TabStop = false };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(450, 0) };
    private readonly Button record = new() { Text = "Shortcut aufnehmen", AutoSize = true };
    private readonly Action<Shortcut> save;
    private Shortcut candidate;
    private bool recording, refreshing;
    private Shortcut? pendingModifier;
    private readonly Func<string?>? warning;
    private readonly ComboBox keyPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox ctrl = new() { Text = "Ctrl", AutoSize = true }, alt = new() { Text = "Alt", AutoSize = true },
        shift = new() { Text = "Shift", AutoSize = true }, win = new() { Text = "Win", AutoSize = true };
    internal Shortcut Candidate => candidate;
    internal SettingsWindow(Shortcut current, Action<Shortcut> save, Func<string?>? warning = null, UpdateCoordinator? updates = null)
    {
        this.save = save;
        this.warning = warning;
        candidate = current;
        Text = "Blackveil – Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 440);
        KeyPreview = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Shortcut für Schwarzbild ein / aus", AutoSize = true });
        shortcutText.Text = current.ToString();
        layout.Controls.Add(shortcutText);
        record.Click += (_, _) => { if (recording) { recording = false; RefreshCandidate(); } else BeginRecording(); };
        layout.Controls.Add(record);
        status.Text = warning?.Invoke() ?? "Tastenkombination aufnehmen oder unten frei auswählen. Hinweis: Windows kann Kombinationen reservieren oder abfangen; nicht jede Auswahl funktioniert.";
        var picker = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        foreach (int code in Enumerable.Range(8, 247)) keyPicker.Items.Add(new KeyChoice(code));
        picker.Controls.Add(keyPicker); picker.Controls.Add(ctrl); picker.Controls.Add(alt); picker.Controls.Add(shift); picker.Controls.Add(win);
        layout.Controls.Add(picker);
        keyPicker.SelectedIndexChanged += (_, _) => SelectManually();
        foreach (var box in new[] { ctrl, alt, shift, win }) box.CheckedChanged += (_, _) => SelectManually();
        RefreshPicker();
        layout.Controls.Add(status);
        var reset = new Button { Text = "Standard: Ctrl + Shift + Q", AutoSize = true };
        reset.Click += (_, _) => { recording = false; candidate = Shortcut.Default; RefreshCandidate(); };
        layout.Controls.Add(reset);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        var apply = new Button { Text = "Speichern", AutoSize = true };
        apply.Click += (_, _) =>
        {
            if (recording) { status.Text = "Zuerst eine Tastenkombination aufnehmen oder mit Escape abbrechen."; return; }
            try
            {
                save(candidate);
                string? hint = warning?.Invoke();
                if (hint is not null) { status.Text = "Gespeichert. Hinweis: " + hint; cancel.Text = "Schliessen"; return; }
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception error) { status.Text = error.Message; }
        };
        buttons.Controls.Add(cancel); buttons.Controls.Add(apply); layout.Controls.Add(buttons);
        if (updates is null) Controls.Add(layout);
        else
        {
            ClientSize = new Size(590, 560);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var shortcutPage = new TabPage("Shortcut"); shortcutPage.Controls.Add(layout);
            var updatePage = new TabPage("Updates"); updatePage.Controls.Add(new UpdateSettingsPanel(updates));
            tabs.TabPages.Add(shortcutPage); tabs.TabPages.Add(updatePage); Controls.Add(tabs);
            tabs.SelectedIndexChanged += (_, _) => { recording = false; RefreshCandidate(); };
        }
        CancelButton = cancel;
        KeyUp += (_, e) =>
        {
            if (!recording || pendingModifier is null || Shortcut.ModifierBit((int)e.KeyCode) == 0) return;
            candidate = pendingModifier; recording = false; pendingModifier = null;
            RefreshCandidate(); e.Handled = true; e.SuppressKeyPress = true;
        };
        KeyDown += (_, e) =>
        {
            if (!recording) return;
            CaptureShortcut(e.KeyData);
            e.Handled = true; e.SuppressKeyPress = true;
        };
    }
    internal void BeginRecording()
    {
        recording = true; pendingModifier = null; record.Text = "Tastenkombination drücken …";
        status.Text = "Jetzt den gewünschten Shortcut drücken. Für einzelne Modifikatortasten: drücken und loslassen. Erneut auf Aufnahme klicken zum Abbrechen.";
        record.Focus();
    }
    internal bool CaptureShortcut(Keys keyData)
    {
        if (!recording) return false;
        Keys key = keyData & Keys.KeyCode;
        uint modifiers = ((keyData & Keys.Control) != 0 ? 2u : 0u) |
            ((keyData & Keys.Alt) != 0 ? 1u : 0u) | ((keyData & Keys.Shift) != 0 ? 4u : 0u) |
            (((Native.GetAsyncKeyState(0x5B) | Native.GetAsyncKeyState(0x5C)) & 0x8000) != 0 ? 8u : 0u);
        var next = new Shortcut((int)key, modifiers);
        if (!next.IsValid) return true;
        if (Shortcut.ModifierBit((int)key) != 0) { pendingModifier = next with { Modifiers = next.EffectiveModifiers }; return true; }
        candidate = next; recording = false; RefreshCandidate(); return true;
    }
    private void RefreshCandidate()
    {
        RefreshPicker();
        shortcutText.Text = candidate.ToString(); record.Text = "Shortcut aufnehmen";
        status.Text = "Mit Speichern übernehmen. Hinweis: Reservierte oder belegte Shortcuts werden ebenfalls gespeichert, können aber nicht funktionieren.";
    }
    private sealed record KeyChoice(int Code)
    {
        public override string ToString() => $"{Shortcut.KeyName(Code)} ({(Keys)Code})";
    }
    private void RefreshPicker()
    {
        refreshing = true;
        try
        {
            keyPicker.SelectedIndex = candidate.Key - 8;
            ctrl.Checked = (candidate.EffectiveModifiers & 2) != 0;
            alt.Checked = (candidate.EffectiveModifiers & 1) != 0;
            shift.Checked = (candidate.EffectiveModifiers & 4) != 0;
            win.Checked = (candidate.EffectiveModifiers & 8) != 0;
        }
        finally { refreshing = false; }
    }
    private void SelectManually()
    {
        if (refreshing || keyPicker.SelectedItem is not KeyChoice key) return;
        recording = false; pendingModifier = null;
        candidate = new Shortcut(key.Code, (ctrl.Checked ? 2u : 0u) | (alt.Checked ? 1u : 0u) | (shift.Checked ? 4u : 0u) | (win.Checked ? 8u : 0u));
        RefreshCandidate();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => CaptureShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);
}
