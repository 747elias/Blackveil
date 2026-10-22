namespace Blackveil;

internal sealed class VeilWindow : Form
{
    private bool allowClose;
    private readonly Action lostWindow;
    internal VeilWindow(Rectangle bounds, Action lostWindow)
    {
        this.lostWindow = lostWindow;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        Text = "Blackveil";
        SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } // Tool window; no Alt+Tab entry.
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!Native.RegisterTouchWindow(Handle, 0))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Touch-Empfang konnte nicht aktiviert werden.");
    }
    protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(Color.Black);
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!allowClose) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (!allowClose) lostWindow();
        base.OnHandleDestroyed(e);
    }
    internal void CloseVeil() { allowClose = true; Close(); Dispose(); }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x20) { Native.SetCursor(0); m.Result = 1; return; }
        if (m.Msg == 0x240) { Native.CloseTouchInputHandle(m.LParam); m.Result = 0; return; }
        if (m.Msg == 0x119) { Native.CloseGestureInfoHandle(m.LParam); m.Result = 0; return; }
        if (m.Msg is 0x118 or 0x11A || m.Msg is >= 0x241 and <= 0x253 ||
            m.Msg is >= 0x100 and <= 0x109 || m.Msg is >= 0x200 and <= 0x20E ||
            m.Msg is >= 0xA0 and <= 0xAD || m.Msg == 0x10 ||
            (m.Msg == 0x112 && ((long)m.WParam & 0xFFF0) == 0xF060))
        { m.Result = 0; return; }
        base.WndProc(ref m);
        if (m.Msg == 0x2E0 && !IsDisposed)
            BeginInvoke(() => { if (!IsDisposed) Bounds = Screen.FromHandle(Handle).Bounds; });
    }
}
