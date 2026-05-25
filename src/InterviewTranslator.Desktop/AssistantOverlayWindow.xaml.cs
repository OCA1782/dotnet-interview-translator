using System.Windows;
using System.Windows.Media;
using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Desktop;

public partial class AssistantOverlayWindow : Window
{
    private static readonly Dictionary<string, (Color bg, string label)> IntentStyles = new()
    {
        ["teknik"]       = (Color.FromRgb(0x3B, 0x82, 0xF6), "Teknik"),
        ["davranışsal"]  = (Color.FromRgb(0x10, 0xB9, 0x81), "Davranışsal"),
        ["deneyim"]      = (Color.FromRgb(0xF5, 0x9E, 0x0B), "Deneyim"),
        ["maaş"]         = (Color.FromRgb(0xEF, 0x44, 0x44), "Maaş/Konum"),
        ["genel"]        = (Color.FromRgb(0x6B, 0x72, 0x80), "Genel"),
    };

    public AssistantOverlayWindow()
    {
        InitializeComponent();
        PositionOnScreen();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void PositionOnScreen()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        Left = screenW - Width - 24;
        Top  = 80;
    }

    public void ShowResult(InterviewAssistResult result)
    {
        Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(result.TurkishSummary)) return;

            // Intent badge
            var intent = result.DetectedIntent?.ToLowerInvariant() ?? "genel";
            if (!IntentStyles.TryGetValue(intent, out var style))
                style = IntentStyles["genel"];

            IntentBadge.Background = new SolidColorBrush(style.bg);
            IntentText.Text = style.label;

            // Confidence
            ConfidenceText.Text = result.Confidence > 0
                ? $"• %{result.Confidence * 100:F0}"
                : "";
            ConfidenceText.Foreground = result.Confidence >= 0.7
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
                : new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));

            // Summary
            SummaryText.Text = result.TurkishSummary;

            // Warnings
            if (result.Warnings.Count > 0)
            {
                WarningText.Text = "⚠ " + string.Join(" | ", result.Warnings);
                WarningText.Visibility = Visibility.Visible;
            }
            else
            {
                WarningText.Visibility = Visibility.Collapsed;
            }

            if (Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;
        });
    }

    public void ToggleVisibility()
    {
        Dispatcher.Invoke(() => Visibility = Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible);
    }
}
