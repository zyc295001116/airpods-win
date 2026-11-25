namespace AirPodsWinTray.UI;

internal sealed class MessagePumpForm : Form
{
    private readonly HotKeyManager _hotKeys;

    public MessagePumpForm(HotKeyManager hotKeys)
    {
        _hotKeys = hotKeys;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
    }

    protected override void WndProc(ref Message m)
    {
        _hotKeys.ProcessMessage(ref m);
        base.WndProc(ref m);
    }
}