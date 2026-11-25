using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.UI;

public sealed class BatteryPopupForm : Form
{
    private readonly Label _title = new() { AutoSize = true };
    private readonly Label _detail = new() { AutoSize = true };

    public BatteryPopupForm()
    {
        _title.Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DialogFont.FontFamily, 11, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Controls.Add(_title);
        Controls.Add(_detail);
        _title.Location = new Point(12, 12);
        _detail.Location = new Point(12, 40);
    }

    public void ShowState(ProximityBeaconState state, AirPodsModelDefinition definition, Point near)
    {
        _title.Text = definition.Name;
        if (state.IsMono && state.Single != null)
        {
            _detail.Text = $"电量 {state.Single.LevelPercent}%";
        }
        else
        {
            _detail.Text =
                $"L {FormatPod(state.Left)}  R {FormatPod(state.Right)}  仓 {FormatPod(state.Case)}";
        }

        Location = new Point(near.X - Width / 2, near.Y - Height - 8);
        Show();
    }

    private static string FormatPod(PodBatteryInfo? pod) =>
        pod == null || pod.IsDisconnected ? "—" : $"{pod.LevelPercent}%";
}