using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GlassDock;

// Opt-in diagnostic backdrop behind the real dock, never a replacement dock UI.
internal sealed class GlassMaterialReview : Window
{
    readonly Grid surface = new();
    readonly StackPanel samples = new();

    internal GlassMaterialReview()
    {
        Title = "GlassDock material review background";
        WindowState = WindowState.Maximized;
        Content = surface;
        surface.Children.Add(samples);
        for (int row = 0; row < 50; row++) samples.Children.Add(new TextBlock
        {
            Text = $"{row:00}  Glass backdrop · 배경 글자 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            FontSize = 17, Height = 28, Margin = new Thickness(0), Opacity = .7
        });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(20) };
        foreach (string mode in new[] { "Dark", "Light", "Color" })
        {
            var button = new Button { Content = mode, Margin = new Thickness(5), Padding = new Thickness(24, 12, 24, 12) };
            button.Click += (_, _) => SetMode(mode);
            controls.Children.Add(button);
        }
        surface.Children.Add(controls);
        SetMode("Dark");
        PreviewKeyDown += (_, args) =>
        {
            string? mode = args.Key switch { Key.D1 => "Dark", Key.D2 => "Light", Key.D3 => "Color", _ => null };
            if (mode == null) return;
            SetMode(mode); WindowState = WindowState.Maximized; args.Handled = true;
        };
    }

    void SetMode(string mode)
    {
        surface.Background = mode switch
        {
            "Light" => Brushes.White,
            "Color" => new LinearGradientBrush(new GradientStopCollection
            {
                new(Color.FromRgb(20, 116, 190), 0), new(Color.FromRgb(33, 164, 136), .35),
                new(Color.FromRgb(212, 106, 162), .65), new(Color.FromRgb(92, 59, 166), 1)
            }, new Point(0, 0), new Point(.3, 1)),
            _ => new SolidColorBrush(Color.FromRgb(19, 23, 31))
        };
        samples.SetValue(TextElement.ForegroundProperty, mode == "Light" ? Brushes.Black : Brushes.White);
    }
}
