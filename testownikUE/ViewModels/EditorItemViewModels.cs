using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace testownikUE.ViewModels;

public partial class EditableAnswerViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isCorrect;

    [ObservableProperty]
    private int _displayOrder;
}

public partial class EditableQuestionViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private int _displayOrder;

    public ObservableCollection<EditableAnswerViewModel> Answers { get; } = new();
}

