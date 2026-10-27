namespace Blackveil;
internal sealed class ChangesWindow : Form
{
    internal ChangesWindow(string version, string changes)
    {
        Text = "Neu in Blackveil " + version;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 430); MinimumSize = new Size(400, 300);
        var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
            Text = changes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine) };
        var close = new Button { Text = "Schliessen", Dock = DockStyle.Bottom, Height = 38 };
        close.Click += (_, _) => Close(); Controls.Add(text); Controls.Add(close); AcceptButton = close; CancelButton = close;
    }
}
