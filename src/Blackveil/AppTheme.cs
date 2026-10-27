using System.Drawing.Drawing2D;
namespace Blackveil;
internal static class AppTheme
{
    internal static readonly Color Canvas = Color.FromArgb(244,247,249), Ink = Color.FromArgb(24,39,51), Muted = Color.FromArgb(94,112,126),
        Navy = Color.FromArgb(15,29,42), Accent = Color.FromArgb(0,119,104), Border = Color.FromArgb(221,230,234), Tint = Color.FromArgb(229,246,241);
    internal static Icon CreateIcon()
    {
        using var stream = typeof(AppTheme).Assembly.GetManifestResourceStream("Blackveil.Icon")!;
        using var source = new Icon(stream); return (Icon)source.Clone();
    }
    internal static void Form(Form form)
    { var icon = CreateIcon(); form.Icon = icon; form.Disposed += (_,_) => icon.Dispose();
      form.BackColor = Canvas; form.ForeColor = Ink; form.Font = new Font("Segoe UI", 10); form.Padding = new Padding(0); }
    internal static void Button(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = Border;
        button.BackColor = primary ? Accent : Color.White; button.ForeColor = primary ? Color.White : Ink;
        button.Padding = new Padding(12,6,12,6); button.MinimumSize = new Size(110,38); button.Cursor = Cursors.Hand;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0,98,87) : Tint;
    }
    internal static Label Title(string text, float size = 20) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, FontStyle.Bold), ForeColor = Ink, Margin = new Padding(0,0,0,8) };
    internal static Label Hint(string text, int width = 540) => new() { Text = text, AutoSize = true, MaximumSize = new Size(width,0), ForeColor = Muted, Margin = new Padding(0,0,0,16) };
    internal static FlowLayoutPanel Stack(int padding = 24) => new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(padding), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Canvas };
    internal static FlowLayoutPanel Card(string title, string description, int width = 580)
    {
        var panel = new FlowLayoutPanel { Width = width, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(22), Margin = new Padding(0,0,0,16), MinimumSize = new Size(width,0) };
        panel.Controls.Add(Title(title,13));
        if (description.Length != 0) panel.Controls.Add(Hint(description, width - 44));
        return panel;
    }
    internal static void Polish(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button b) Button(b, b.Text is "Speichern" or "Update-Einstellungen speichern" or "Schliessen");
            else if (child is TextBox text) { text.BorderStyle = BorderStyle.FixedSingle; text.BackColor = Color.White; text.ForeColor = Ink; }
            else if (child is ComboBox combo) { combo.FlatStyle = FlatStyle.Flat; combo.BackColor = Canvas; combo.ForeColor = Ink; combo.IntegralHeight = false; combo.DropDownHeight = 260; }
            else if (child is CheckBox box) { box.ForeColor = Ink; box.Padding = new Padding(0,5,8,5); box.Cursor = Cursors.Hand; }
            Polish(child);
        }
    }
    internal static Control Shell(Control shortcuts, Control? updates, Action changed)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var nav = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Margin = new Padding(0), Padding = new Padding(20,24,20,20) };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Margin = new Padding(0) };
        var logo = new BrandMark { Location = new Point(20,24), Size = new Size(44,44) };
        nav.Controls.Add(logo);
        nav.Controls.Add(new Label { Text = "BLACKVEIL", ForeColor = Color.White, AutoSize = true, Font = new Font("Segoe UI",12,FontStyle.Bold), Location = new Point(20,84) });
        nav.Controls.Add(new Label { Text = "Dein Bildschirm.\nDein Moment.", ForeColor = Color.FromArgb(148,172,186), AutoSize = true, Location = new Point(20,116) });
        var about = Stack();
        about.Controls.Add(Title("Über Blackveil")); about.Controls.Add(Hint("Weniger Ablenkung. Mehr Privatsphäre."));
        var info = Card("Blackveil " + UpdateCoordinator.CurrentVersion, "Vorübergehender Sicht- und Eingabeschutz für deinen Windows-Desktop. Keine Windows-Sperre.");
        string exe = Environment.ProcessPath!;
        string mode = UpdateInstaller.IsMachineInstalled(exe) ? "Für alle Benutzer" : UpdateInstaller.IsInstalled(exe) ? "Nur für dich" : "Portable Version";
        info.Controls.Add(Hint("Installation: " + mode));
        info.Controls.Add(Hint("Persönliche Einstellungen und Autostart gelten nur für dein Windows-Konto. Systemweite Updates benötigen eine Windows-Bestätigung."));
        info.Controls.Add(Hint("Ctrl + Alt + Entf und geschützte Systemfunktionen bleiben verfügbar.\nMIT-Lizenz · Nicht code-signiert")); about.Controls.Add(info);
        var buttons = new List<Button>();
        bool initialized = false;
        void Navigate(Control page, Button selected)
        {
            host.Controls.Clear(); host.Controls.Add(page); page.Dock = DockStyle.Fill;
            foreach (var b in buttons) { b.BackColor = b == selected ? Color.FromArgb(31,64,76) : Navy; b.ForeColor = b == selected ? Color.FromArgb(112,238,206) : Color.FromArgb(186,204,215); }
            if (initialized) changed(); initialized = true;
        }
        void Add(string text, Control page)
        {
            var b = new Button { Name = "Nav" + text, Text = text, Size = new Size(150,44), Location = new Point(20,192 + buttons.Count * 54), TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Padding = new Padding(10,0,0,0) };
            b.FlatAppearance.BorderSize = 0; b.Click += (_,_) => Navigate(page,b); buttons.Add(b); nav.Controls.Add(b);
        }
        Add("Tastenkürzel",shortcuts); if (updates is not null) Add("Updates",updates); Add("Info",about);
        var version = new Label { Text = "VERSION " + UpdateCoordinator.CurrentVersion, ForeColor = Color.FromArgb(115,145,162), Dock = DockStyle.Bottom, Height = 28, Font = new Font("Segoe UI",8,FontStyle.Bold) };
        nav.Controls.Add(version); root.Controls.Add(nav,0,0); root.Controls.Add(host,1,0);
        root.Disposed += (_,_) => { shortcuts.Dispose(); updates?.Dispose(); about.Dispose(); };
        Navigate(shortcuts,buttons[0]); return root;
    }
}
internal sealed class BrandMark : Control
{
    internal BrandMark() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(30,58,70)); e.Graphics.FillEllipse(brush,ClientRectangle);
        using var pen = new Pen(Color.FromArgb(85,229,190), Math.Max(2,Width/12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(pen,[new PointF(Width*.28f,Height*.3f),new PointF(Width*.5f,Height*.73f),new PointF(Width*.75f,Height*.3f)]);
    }
}

internal sealed class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected => AppTheme.Tint;
    public override Color MenuItemBorder => AppTheme.Tint;
    public override Color MenuBorder => AppTheme.Border;
    public override Color ToolStripDropDownBackground => Color.White;
    public override Color ImageMarginGradientBegin => Color.White;
    public override Color ImageMarginGradientMiddle => Color.White;
    public override Color ImageMarginGradientEnd => Color.White;
}
