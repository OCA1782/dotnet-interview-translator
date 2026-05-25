using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Subtitles;

namespace InterviewTranslator.Desktop;

public partial class SuggestionOverlayWindow : Window
{
    private readonly SuggestionBuffer _buffer;
    private readonly ITtsService _tts;

    private static readonly SolidColorBrush TrBrush = new(Color.FromRgb(0xA7, 0x8B, 0xFA));
    private static readonly SolidColorBrush EnBrush = Brushes.White;

    public SuggestionOverlayWindow(SuggestionBuffer buffer, ITtsService tts)
    {
        InitializeComponent();
        _buffer = buffer;
        _tts    = tts;

        PositionOnScreen();
        _buffer.Changed += OnSuggestionChanged;

        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void PositionOnScreen()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        Left   = 20;
        Top    = 80;
        Width  = Math.Min(820, screenW - 40);
        Height = 280;
    }

    private void OnSuggestionChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var recent = _buffer.GetRecent();
            TranscriptText.Inlines.Clear();

            bool first = true;
            foreach (var item in recent)
            {
                bool hasTr = !string.IsNullOrWhiteSpace(item.TurkishText);
                bool hasEn = !string.IsNullOrWhiteSpace(item.EnglishSuggestion);
                if (!hasTr && !hasEn) continue;

                if (!first)
                    TranscriptText.Inlines.Add(new LineBreak());
                first = false;

                // Türkçe kaynak — küçük, mor, italik
                if (hasTr)
                {
                    TranscriptText.Inlines.Add(new Run(item.TurkishText!.Trim())
                    {
                        FontSize   = 13,
                        FontStyle  = FontStyles.Italic,
                        Foreground = TrBrush
                    });
                }

                // İngilizce öneri — büyük, beyaz, kalın
                if (hasEn)
                {
                    if (hasTr)
                        TranscriptText.Inlines.Add(new LineBreak());

                    TranscriptText.Inlines.Add(new Run(item.EnglishSuggestion!.Trim())
                    {
                        FontSize   = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = EnBrush
                    });
                }
            }

            TranscriptScroller.ScrollToBottom();

            // TTS: son segment
            var last = recent.LastOrDefault();
            if (last?.EnglishSuggestion is { Length: > 0 } en)
                _tts.Speak(en);
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
        _buffer.Changed -= OnSuggestionChanged;
        base.OnClosed(e);
    }
}
