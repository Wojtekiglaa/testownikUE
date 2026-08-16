using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using testownikUE.Data;
using testownikUE.Models;

namespace testownikUE.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly Action _navigateToHomeView;
    private Guid _importBatchId;
    private string _sourcePath;

    [ObservableProperty]
    private string _setName = "Nowy zestaw";

    [ObservableProperty]
    private string _setAuthor = Environment.UserName;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private EditableQuestionViewModel? _selectedQuestion;

    public ObservableCollection<EditableQuestionViewModel> Questions { get; } = new();
    public ObservableCollection<EditableQuestionViewModel> FilteredQuestions { get; } = new();

    public string ModeLabel => _importBatchId == Guid.Empty ? "Nowy zestaw" : $"Edycja zestawu ({_importBatchId})";

    public EditorViewModel(Action navigateToHome, Guid? importBatchId = null, string sourcePath = "")
    {
        _navigateToHomeView = navigateToHome;
        _importBatchId = importBatchId ?? Guid.Empty;
        _sourcePath = sourcePath;

        if (_importBatchId != Guid.Empty)
            LoadExistingSet(_importBatchId);

        if (Questions.Count == 0)
            AddQuestion();

        RefreshQuestionOrdering();
        ApplyFilter();
    }

    [RelayCommand]
    private void GoBack() => _navigateToHomeView();

    [RelayCommand]
    private void AddQuestion()
    {
        var newQuestion = new EditableQuestionViewModel
        {
            Text = "Nowe pytanie"
        };

        newQuestion.Answers.Add(new EditableAnswerViewModel { Key = "A", Text = "Odpowiedź A - poprawna" , IsCorrect = true});
        newQuestion.Answers.Add(new EditableAnswerViewModel { Key = "B", Text = "Odpowiedź B" });
        WireQuestion(newQuestion);
        foreach (var answerVm in newQuestion.Answers)
            WireAnswer(answerVm);

        Questions.Add(newQuestion);
        RefreshQuestionOrdering();
        ApplyFilter();
        SelectedQuestion = newQuestion;
    }

    [RelayCommand]
    private async Task RemoveQuestion(EditableQuestionViewModel? question)
    {
        if (question == null)
            return;

        if (Questions.Count == 1)
        {
            await Services.DialogService.ShowInfoAsync(
                "Nie można usunąć",
                "Musi zostać przynajmniej jedno pytanie w zestawie.");
            return;
        }

        Questions.Remove(question);

        if (ReferenceEquals(SelectedQuestion, question))
            SelectedQuestion = null;

        RefreshQuestionOrdering();
        ApplyFilter();
    }

    [RelayCommand]
    private void MoveQuestionUp(EditableQuestionViewModel? question)
    {
        MoveQuestion(question, -1);
    }

    [RelayCommand]
    private void MoveQuestionDown(EditableQuestionViewModel? question)
    {
        MoveQuestion(question, 1);
    }

    [RelayCommand]
    private void AddAnswer(EditableQuestionViewModel? question)
    {
        if (question == null)
            return;

        var answerIndex = question.Answers.Count;
        question.Answers.Add(new EditableAnswerViewModel
        {
            Key = ToAnswerKey(answerIndex),
            Text = $"Odpowiedź {ToAnswerKey(answerIndex)}"
        });
        WireAnswer(question.Answers[^1]);

        RefreshAnswerOrdering(question);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RemoveAnswer(EditableAnswerViewModel? answer)
    {
        if (answer == null)
            return;

        var owner = FindOwner(answer);
        if (owner == null)
            return;

        if (owner.Answers.Count == 1)
        {
            await Services.DialogService.ShowInfoAsync(
                "Nie można usunąć",
                "Musi zostać przynajmniej jedna odpowiedź w pytaniu.");
            return;
        }

        owner.Answers.Remove(answer);
        if (owner.Answers.Count == 0)
        {
            owner.Answers.Add(new EditableAnswerViewModel { Key = "A", Text = "Odpowiedź A" });
            WireAnswer(owner.Answers[0]);
        }

        RefreshAnswerOrdering(owner);
        ApplyFilter();
    }

    [RelayCommand]
    private void MoveAnswerUp(EditableAnswerViewModel? answer)
    {
        MoveAnswer(answer, -1);
    }

    [RelayCommand]
    private void MoveAnswerDown(EditableAnswerViewModel? answer)
    {
        MoveAnswer(answer, 1);
    }

    [RelayCommand]
    private async Task SaveSet()
    {
        //Zapisujemy zestaw, sprawdzamy czy użytkownik nie popełnił błędu.
        if (string.IsNullOrWhiteSpace(SetName))
        {
            StatusMessage = "Podaj nazwę zestawu przed zapisem.";
            return;
        }

        if (Questions.Count == 0)
        {
            StatusMessage = "Dodaj przynajmniej jedno pytanie.";
            return;
        }

        if (Questions.Any(q => string.IsNullOrWhiteSpace(q.Text) || q.Answers.Count == 0 || q.Answers.Any(a => string.IsNullOrWhiteSpace(a.Text))))
        {
            StatusMessage = "Każde pytanie i odpowiedź muszą mieć treść.";
            return;
        }

        if (Questions.Any(q => !q.Answers.Any(a => a.IsCorrect)))
        {
            StatusMessage = "Każde pytanie musi mieć przynajmniej jedną poprawną odpowiedź.";
            return;
        }

        if (_importBatchId == Guid.Empty)
            _importBatchId = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(_sourcePath))
            _sourcePath = $"db://{_importBatchId}";

        try
        {
            using var db = new AppDb();
            AppDb.EnsureTables(db);

            var existingQuestions = db.Questions
                .Include(q => q.Answers)
                .Where(q => q.ImportBatchId == _importBatchId)
                .ToList();

            if (existingQuestions.Count > 0)
                db.Questions.RemoveRange(existingQuestions);

            var staleProgress = db.QuestionProgresses
                .Where(x => x.ImportBatchId == _importBatchId)
                .ToList();
            if (staleProgress.Count > 0)
                db.QuestionProgresses.RemoveRange(staleProgress);

            RefreshQuestionOrdering();

            foreach (var questionVm in Questions)
            {
                var questionEntity = new Question
                {
                    Author = string.IsNullOrWhiteSpace(SetAuthor) ? Environment.UserName : SetAuthor.Trim(),
                    Text = questionVm.Text.Trim(),
                    DisplayOrder = questionVm.DisplayOrder,
                    ImportBatchId = _importBatchId
                };

                RefreshAnswerOrdering(questionVm);
                foreach (var answerVm in questionVm.Answers)
                {
                    questionEntity.Answers.Add(new Answer
                    {
                        Key = answerVm.Key,
                        Text = answerVm.Text.Trim(),
                        IsCorrect = answerVm.IsCorrect,
                        DisplayOrder = answerVm.DisplayOrder,
                        ImportBatchId = _importBatchId
                    });
                }

                db.Questions.Add(questionEntity);
            }

            SaveOrUpdateSetInDb(db);
            db.SaveChanges();

            StatusMessage = $"Zapisano zestaw '{SetName}' do bazy ({Questions.Count} pyt.).";
            OnPropertyChanged(nameof(ModeLabel));
            await Services.DialogService.ShowInfoAsync("Zapisano", "Zapisano zestaw w bazie danych.");
            _navigateToHomeView();
        }
        catch (Exception ex)
        {
            StatusMessage = "Nie udało się zapisać zestawu do bazy.";
            await Services.DialogService.ShowInfoAsync("Błąd zapisu", $"Nie udało się zapisać zestawu:\n{ex.Message}");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void LoadExistingSet(Guid batchId)
    {
        //Ładujemy istniejący zestaw
        try
        {
            using var db = new AppDb();
            AppDb.EnsureTables(db);

            var setInDb = db.SetsInDb
                .AsNoTracking()
                .FirstOrDefault(x => x.LastImportBatchId == batchId);

            if (setInDb != null)
            {
                SetName = string.IsNullOrWhiteSpace(setInDb.SetName) ? "Zestaw z DB" : setInDb.SetName;
                _sourcePath = string.IsNullOrWhiteSpace(_sourcePath) ? setInDb.SourcePath : _sourcePath;
            }

            var entities = db.Questions
                .AsNoTracking()
                .Include(q => q.Answers)
                .Where(q => q.ImportBatchId == batchId)
                .OrderBy(q => q.DisplayOrder)
                .ThenBy(q => q.Id)
                .ToList();

            if (entities.Count == 0)
                return;

            SetAuthor = entities[0].Author;

            foreach (var entity in entities)
            {
                var vm = new EditableQuestionViewModel
                {
                    Id = entity.Id,
                    Text = entity.Text,
                    DisplayOrder = entity.DisplayOrder
                };

                foreach (var answer in entity.Answers.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id))
                {
                    vm.Answers.Add(new EditableAnswerViewModel
                    {
                        Id = answer.Id,
                        Key = string.IsNullOrWhiteSpace(answer.Key) ? "A" : answer.Key,
                        Text = answer.Text,
                        IsCorrect = answer.IsCorrect,
                        DisplayOrder = answer.DisplayOrder
                    });
                }

                if (vm.Answers.Count == 0)
                    vm.Answers.Add(new EditableAnswerViewModel { Key = "A", Text = "Odpowiedź A" });

                WireQuestion(vm);
                foreach (var answerVm in vm.Answers)
                    WireAnswer(answerVm);

                Questions.Add(vm);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Nie udało się wczytać zestawu do edycji.";
            _ = Services.DialogService.ShowInfoAsync("Błąd odczytu", $"Nie udało się wczytać zestawu:\n{ex.Message}");
        }
    }

    private void WireQuestion(EditableQuestionViewModel question)
    {
        // Intencjonalnie bez subskrypcji: odswiezanie listy przy kazdym znaku zabierało focus z TextBox.
    }

    private void WireAnswer(EditableAnswerViewModel answer)
    {
        // Intencjonalnie bez subskrypcji: odswiezanie listy przy kazdym znaku zabierało focus z TextBox.
    }

    private void SaveOrUpdateSetInDb(AppDb db)
    {
        var sourcePath = string.IsNullOrWhiteSpace(_sourcePath) ? $"db://{_importBatchId}" : _sourcePath;
        var nowUtc = DateTime.UtcNow;
        var contentHash = ComputeSetContentHash();
        var existing = db.SetsInDb.FirstOrDefault(x => x.SourcePath == sourcePath);

        if (existing == null)
        {
            db.SetsInDb.Add(new SetInDb
            {
                SetName = SetName.Trim(),
                SourcePath = sourcePath,
                ContentHash = contentHash,
                LastImportBatchId = _importBatchId,
                ImportedQuestionsCount = Questions.Count,
                TotalStudySeconds = 0,
                OpenedAtUtc = nowUtc
            });
            return;
        }

        existing.SetName = SetName.Trim();
        existing.LastImportBatchId = _importBatchId;
        existing.ImportedQuestionsCount = Questions.Count;
        existing.OpenedAtUtc = nowUtc;
        existing.ContentHash = contentHash;
    }

    private string ComputeSetContentHash()
    {
        var canonical = new StringBuilder();
        canonical.Append(SetName.Trim()).Append('|').Append(SetAuthor.Trim()).AppendLine();

        foreach (var question in Questions.OrderBy(x => x.DisplayOrder))
        {
            canonical.Append(question.DisplayOrder)
                .Append('|')
                .Append(question.Text.Trim())
                .Append('|');

            foreach (var answer in question.Answers.OrderBy(x => x.DisplayOrder))
            {
                canonical.Append(answer.DisplayOrder)
                    .Append('=')
                    .Append(answer.Key.Trim().ToUpperInvariant())
                    .Append('=')
                    .Append(answer.Text.Trim())
                    .Append('=')
                    .Append(answer.IsCorrect ? '1' : '0')
                    .Append('|');
            }

            canonical.AppendLine();
        }

        return $"DBEDIT:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))}";
    }

    private void MoveQuestion(EditableQuestionViewModel? question, int offset)
    {
        //Zmieniamy kolejność pytania
        if (question == null)
            return;

        var currentIndex = Questions.IndexOf(question);
        if (currentIndex < 0)
            return;

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= Questions.Count)
            return;

        Questions.Move(currentIndex, targetIndex);
        RefreshQuestionOrdering();
        ApplyFilter();
    }

    private void MoveAnswer(EditableAnswerViewModel? answer, int offset)
    {
        if (answer == null)
            return;

        var owner = FindOwner(answer);
        if (owner == null)
            return;

        var currentIndex = owner.Answers.IndexOf(answer);
        if (currentIndex < 0)
            return;

        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= owner.Answers.Count)
            return;

        owner.Answers.Move(currentIndex, targetIndex);
        RefreshAnswerOrdering(owner);
        ApplyFilter();
    }

    private EditableQuestionViewModel? FindOwner(EditableAnswerViewModel answer)
    {
        return Questions.FirstOrDefault(q => q.Answers.Contains(answer));
    }

    private void RefreshQuestionOrdering()
    {
        for (var i = 0; i < Questions.Count; i++)
        {
            Questions[i].DisplayOrder = i;
            RefreshAnswerOrdering(Questions[i]);
        }
    }

    private static void RefreshAnswerOrdering(EditableQuestionViewModel question)
    {
        for (var i = 0; i < question.Answers.Count; i++)
        {
            question.Answers[i].DisplayOrder = i;
            question.Answers[i].Key = ToAnswerKey(i);
        }
    }

    private void ApplyFilter()
    {
        //Filtrowanie odpowiedzi po stringu wpisanym przez użytkownika.
        var filter = (SearchText).Trim();
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? Questions
            : new ObservableCollection<EditableQuestionViewModel>(Questions.Where(q =>
                q.Text.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                q.Answers.Any(a => a.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))));

        FilteredQuestions.Clear();
        foreach (var question in filtered)
            FilteredQuestions.Add(question);
    }

    private static string ToAnswerKey(int index)
    {
        if (index < 26)
            return ((char)('A' + index)).ToString();

        return $"A{index + 1}";
    }
}