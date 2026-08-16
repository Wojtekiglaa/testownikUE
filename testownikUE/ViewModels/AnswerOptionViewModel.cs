using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace testownikUE.ViewModels;

public partial class AnswerOptionViewModel : ObservableObject
{
    public enum RevealVisualState
    {
        Default,
        Correct,
        SelectedWrong,
        Dimmed
    }

    //Odpowiedzi
    public string Key { get; }
    public string Text { get; }
    public bool IsCorrect { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRevealed;

    [ObservableProperty]
    private bool _isInteractive = true;

    [ObservableProperty]
    private IBrush? _backgroundBrush;

    [ObservableProperty]
    private IBrush? _borderBrush;

    [ObservableProperty]
    private IBrush? _textBrush;

    [ObservableProperty]
    private RevealVisualState _revealState = RevealVisualState.Default;

    public AnswerOptionViewModel(string key, string text, bool isCorrect)
    {
        Key = key;
        Text = text;
        IsCorrect = isCorrect;
        UpdateRevealVisuals();
    }

    partial void OnIsRevealedChanged(bool value) => UpdateRevealVisuals();
    partial void OnRevealStateChanged(RevealVisualState value) => UpdateRevealVisuals();
    partial void OnIsSelectedChanged(bool value) => UpdateRevealVisuals();

    private void UpdateRevealVisuals()
    {
        if (!IsRevealed)
        {
            if (IsSelected)
            {
                // Wybrana odpowiedź przed zatwierdzeniem
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(219, 234, 254));
                BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                TextBrush = Brushes.Black;
            }
            else
            {
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                TextBrush = Brushes.Black;
            }
            return;
        }

        switch (RevealState)
        {
            //Kolory odpowiedzi po zatwierdzeniu
            case RevealVisualState.Correct:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                BorderBrush = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                TextBrush = Brushes.White;
                break;
            case RevealVisualState.SelectedWrong:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                BorderBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27));
                TextBrush = Brushes.White;
                break;
            case RevealVisualState.Dimmed:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                TextBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                break;
            default:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                TextBrush = Brushes.Black;
                break;
        }
    }
}
