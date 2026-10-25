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
    private bool recording;
    internal Shortcut Candidate => candidate;
    internal SettingsWindow(Shortcut current, Action<Shortcut> save)
    {
        this.save = save;
        candidate = current;
        Text = "Blackveil – Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(500, 300);
        KeyPreview = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Shortcut für Schwarzbild ein / aus", AutoSize = true });
        shortcutText.Text = current.ToString();
        layout.Controls.Add(shortcutText);
        record.Click += (_, _) => BeginRecording();
        layout.Controls.Add(record);
        status.Text = "Aufnehmen anklicken und die gewünschte Tastenkombination drücken. Mindestens Ctrl oder Alt plus Buchstabe, Ziffer oder F-Taste (ausser F12).";
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
            try { save(candidate); DialogResult = DialogResult.OK; Close(); }
            catch (Exception error) { status.Text = error.Message; }
        };
        buttons.Controls.Add(cancel); buttons.Controls.Add(apply); layout.Controls.Add(buttons);
        Controls.Add(layout);
        CancelButton = cancel;
        KeyDown += (_, e) => { if (recording) { e.Handled = true; e.SuppressKeyPress = true; } };
    }
    internal void BeginRecording()
    {
        recording = true; record.Text = "Tastenkombination drücken …";
        status.Text = "Jetzt den gewünschten Shortcut drücken. Escape bricht die Aufnahme ab.";
        record.Focus();
    }
    internal bool CaptureShortcut(Keys keyData)
    {
        if (!recording) return false;
        Keys key = keyData & Keys.KeyCode;
        if (key == Keys.Escape) { recording = false; RefreshCandidate(); return true; }
        uint modifiers = ((keyData & Keys.Control) != 0 ? 2u : 0u) |
            ((keyData & Keys.Alt) != 0 ? 1u : 0u) | ((keyData & Keys.Shift) != 0 ? 4u : 0u);
        var next = new Shortcut((int)key, modifiers);
        if (!next.IsValid || (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0) { status.Text = "Bitte Ctrl oder Alt plus Buchstabe, Ziffer oder F-Taste verwenden. F12 und Windows-Tasten sind ausgeschlossen."; return true; }
        candidate = next; recording = false; RefreshCandidate(); return true;
    }
    private void RefreshCandidate()
    {
        shortcutText.Text = candidate.ToString(); record.Text = "Shortcut aufnehmen";
        status.Text = "Mit Speichern übernehmen. Bei einem belegten Shortcut bleibt die bisherige Einstellung erhalten.";
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => CaptureShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);
}
