using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using InterviewTranslator.Shared.Options;
using InterviewTranslator.Subtitles;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public partial class OverlayWindow : Window
{
    private readonly SubtitleBuffer _buffer;
    private readonly OverlayOptions _options;

    private static readonly SolidColorBrush EnBrush  = Brushes.White;
    private static readonly SolidColorBrush TrBrush  = new(Color.FromRgb(0x7D, 0xD3, 0xFC));

    public OverlayWindow(SubtitleBuffer buffer, IOptions<AppOptions> options)
    {
        InitializeComponent();
        _buffer  = buffer;
        _options = options.Value.Overlay;

        ApplyOptions();
        PositionOnScreen();
        _buffer.Changed += OnSubtitleChanged;

        MouseLeftButtonDown += (_, e) => DragMove();
    }

    private void ApplyOptions()
    {
        Opacity = _options.Opacity;
    }

    public void ApplyCurrentOptions() => Dispatcher.Invoke(ApplyOptions);
    public void ResetPosition()       => Dispatcher.Invoke(PositionOnScreen);

    private void PositionOnScreen()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;
        Left   = (screenW - 960) / 2;
        Top    = screenH - (screenH * 0.16) - 280;
        Width  = 960;
        Height = 280;
    }

    private void OnSubtitleChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var recent = _buffer.GetRecent();
            TranscriptText.Inlines.Clear();

            bool first = true;
            foreach (var item in recent)
            {
                if (string.IsNullOrWhiteSpace(item.EnglishText)) continue;

                if (!first)
                    TranscriptText.Inlines.Add(new LineBreak());
                first = false;

                // İngilizce — büyük, beyaz, kalın
                TranscriptText.Inlines.Add(new Run(item.EnglishText.Trim())
                {
                    FontSize   = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = EnBrush
                });

                // Türkçe çeviri — küçük, mavi, italik
                if (!string.IsNullOrWhiteSpace(item.TurkishText)
                    && item.TurkishText.Trim() != item.EnglishText.Trim())
                {
                    TranscriptText.Inlines.Add(new LineBreak());
                    TranscriptText.Inlines.Add(new Run("→ " + item.TurkishText.Trim())
                    {
                        FontSize   = 13,
                        FontStyle  = FontStyles.Italic,
                        Foreground = TrBrush
                    });
                }
            }

            TranscriptScroller.ScrollToBottom();
        });
    }

    public void ToggleVisibility()
    {
        Dispatcher.Invoke(() => Visibility = Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible);
    }

    protected override void OnClosed(EventArgs e)
    {
        _buffer.Changed -= OnSubtitleChanged;
        base.OnClosed(e);
    }
}
