using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace AirPodsWinTray.UI;

public sealed class BatteryColumn : StackPanel
{
    private const double MaxFill = 32;
    private readonly TextBlock _caption;
    private readonly TextBlock _percent;
    private readonly TextBlock _ear;
    private readonly Rectangle _fill;
    private double _level = -1;

    public BatteryColumn()
    {
        Spacing = 8;
        Width = 72;
        HorizontalAlignment = HorizontalAlignment.Center;
        _caption = Label(12);
        _percent = Label(22);
        _percent.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _percent.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
        _percent.Foreground = Brush("TextPrimaryBrush");
        _ear = Label(12);
        _ear.Height = 16;
        var glyph = new Grid { Width = 28, Height = 44, HorizontalAlignment = HorizontalAlignment.Center };
        glyph.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Brush("BatteryFillBrush"),
            Opacity = 0.85
        });
        _fill = new Rectangle
        {
            Width = 16,
            Height = 0,
            RadiusX = 3,
            RadiusY = 3,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Center,
            Fill = Brush("BatteryFillBrush")
        };
        glyph.Children.Add(_fill);
        Children.Add(_caption);
        Children.Add(glyph);
        Children.Add(_percent);
        Children.Add(_ear);
    }

    public void SetCaption(string caption) => _caption.Text = caption;

    public void Show(string percent, double level, string ear, bool dimmed, bool animate)
    {
        _percent.Text = percent;
        _ear.Text = ear;
        Opacity = dimmed ? 0.72 : 1;

        level = Math.Clamp(level, 0, 1);
        var height = MaxFill * level;
        if (!animate || _level < 0 || Math.Abs(_level - level) < 0.001)
        {
            _fill.Height = height;
        }
        else
        {
            var animation = new DoubleAnimation
            {
                To = height,
                Duration = TimeSpan.FromMilliseconds(400),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, _fill);
            Storyboard.SetTargetProperty(animation, "Height");
            var board = new Storyboard();
            board.Children.Add(animation);
            board.Begin();
        }

        _level = level;
    }

    private static TextBlock Label(double size) => new()
    {
        FontSize = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        Foreground = Brush("TextSecondaryBrush")
    };

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
