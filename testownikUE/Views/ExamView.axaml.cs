using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using testownikUE.ViewModels;

namespace testownikUE.Views;

public partial class ExamView : UserControl
{
    private TopLevel? _topLevel;

    public ExamView()
    {
        InitializeComponent();

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        if (Design.IsDesignMode)
        {
            DataContext = new ExamViewModel(
                navigateToHome: () => { },
                questions: null,
                sourcePath: @"TEST PATH");
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyUpEvent, OnExamViewKeyUp, RoutingStrategies.Tunnel);

        // Ustawiamy focus po podpięciu widoku, żeby skróty działały od razu.
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyUpEvent, OnExamViewKeyUp);
        _topLevel = null;
    }

    private void OnExamViewKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ExamViewModel vm)
            return;

        if (vm.IsFinished)
            return;

        if (e.KeyModifiers != KeyModifiers.None)
            return;

        var index = MapKeyToAnswerIndex(e.Key);
        if (index >= 0)
        {
            vm.ToggleAnswerByIndex(index);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            vm.SubmitFromKeyboard();
            e.Handled = true;
        }
    }

    private static int MapKeyToAnswerIndex(Key key)
    {
        return key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };
    }
}