namespace Blackveil;
internal sealed class ChangesWindow : Form
{
    internal ChangesWindow(string version, string changes)
    {
        Text = "Neu in Blackveil " + version;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(680,540); MinimumSize = new Size(460,360); AppTheme.Form(this);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        header.Controls.Add(AppTheme.Title("Das ist neu.")); header.Controls.Add(AppTheme.Hint("Blackveil " + version + " · Bereit für deinen nächsten Moment."));
        var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
            Text = changes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Margin = new Padding(0,0,0,8) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0,10,0,0) };
        var close = new Button { Text = "Schliessen", AutoSize = true }; close.Click += (_,_) => Close(); actions.Controls.Add(close);
        root.Controls.Add(header,0,0); root.Controls.Add(text,0,1); root.Controls.Add(actions,0,2); Controls.Add(root);
        AppTheme.Polish(root); AcceptButton = close; CancelButton = close;
    }
}
