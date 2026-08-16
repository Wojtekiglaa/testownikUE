using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using testownikUE.Data;
using testownikUE.Models;
using testownikUE.Services;

namespace testownikUE.ViewModels;

public partial class ExamViewModel : ViewModelBase
{
    private sealed class ScheduledQuestion
    {
        public required Question Question { get; init; }
        public int DueStep { get; init; }
    }

    private readonly Action _navigateToHomeView;
    private readonly List<Question> _questions;
    private readonly HashSet<int> _questionIds;
    private readonly Dictionary<int, QuestionProgress> _progressByQuestionId = new();
    private readonly List<ScheduledQuestion> _reviewQueue = new();
    private Question? _currentQuestion;
    private int? _lastQuestionId;
    private int _currentStep;
    private readonly Guid? _importBatchId;
    private readonly Action<Guid?, string, int>? _onSessionExit;
    private readonly int _initialStudySeconds;
    private readonly DispatcherTimer _studyTimer;
    private TimeSpan _studyElapsed;
    private readonly int _initialRepetitions;
    private readonly int _wrongAnswerPenalty;
    private readonly int _maxRepetitions;
    private bool _hasUnsavedChanges;
    private bool _awaitingAdvance;
    //Te observableproperty to "łączniki" do Viewsów aplikacji.
    [ObservableProperty]
    private string _studyTimeText = "00:00:00";
    
    [ObservableProperty]
    private bool _isDebug;

    [ObservableProperty]
    private string _questionText = string.Empty;

    [ObservableProperty]
    private int _score;

    [ObservableProperty]
    private int _reappearancesCount;

    [ObservableProperty]
    private string _srDebugText = string.Empty;

    [ObservableProperty]
    private bool _canSubmitSelection;

    [ObservableProperty]
    private bool _isAnswerInputEnabled = true;

    [ObservableProperty]
    private bool _isSelectionComplete;

    [ObservableProperty]
    private bool _isFinished;
    
    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _sourceLabel = string.Empty;
    
    [ObservableProperty]
    private string _setAuthor = string.Empty;
    
    [ObservableProperty]
    private IEnumerable<ISeries> _finalReportSeries = [];
    
    [ObservableProperty]
    private Axis[] _finalReportXAxes = [];
    
    [ObservableProperty]
    private string _finalMasteryText = string.Empty;

    [ObservableProperty]
    private string _finalAccuracyText = string.Empty;
    
    [ObservableProperty]
    private string _finalTotalsText = string.Empty;
    
    

    public ObservableCollection<AnswerOptionViewModel> CurrentAnswers { get; } = new();

    public int TotalQuestions => _questions.Count;
    // Udzielone odpowiedzi = liczba pytań, które były widziane przynajmniej raz.
    public int CurrentQuestionNumber => Math.Min(TotalQuestions, ActiveProgresses().Count(x => x.SeenCount > 0));
    public int RemainingAnswersCount => Math.Max(0, TotalQuestions - CurrentQuestionNumber);
    public int SubmittedAnswersCount => ActiveProgresses().Sum(x => x.SeenCount);
    public int IncorrectAnswersCount => ActiveProgresses().Sum(x => x.WrongCount);
    public int CorrectAnswersCount => Math.Max(0, SubmittedAnswersCount - IncorrectAnswersCount);
    public double ProgressValue => TotalQuestions == 0 ? 0 : (double)CurrentQuestionNumber / TotalQuestions * 100.0;
    public string ProgressLabel => $"{CurrentQuestionNumber}/{TotalQuestions}";
    public double FinalOverlayOpacity => IsFinished ? 1.0 : 0.0;
    public bool IsFinalOverlayHitTestVisible => IsFinished;

    public ExamViewModel(
        Action navigateToHome,
        IEnumerable<Question>? questions = null,
        string? sourcePath = null,
        string? displaySourceLabel = null,
        Guid? importBatchId = null,
        UserSettings? settings = null,
        Action<Guid?, string, int>? onSessionExit = null,
        int initialStudySeconds = 0)
    {
        #if DEBUG
            IsDebug = true;
        #else
            IsDebug = false;
        #endif
        _navigateToHomeView = navigateToHome;
        _questions = questions?.ToList() ?? BuildDemoQuestions();
        _questionIds = _questions.Select(q => q.Id).ToHashSet();
        _importBatchId = importBatchId;
        _onSessionExit = onSessionExit;
        _initialStudySeconds = Math.Max(0, initialStudySeconds);
        var examSettings = settings ?? new UserSettings();
        _initialRepetitions = Math.Clamp(examSettings.InitialRepetitions, 1, 10);
        _wrongAnswerPenalty = Math.Clamp(examSettings.WrongAnswerPenalty, 0, 10);
        _maxRepetitions = Math.Clamp(examSettings.MaxRepetitions, 1, 10);
        Log($"Init SR settings: init={_initialRepetitions}, penalty={_wrongAnswerPenalty}, max={_maxRepetitions}");
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? "brak pliku" : sourcePath;
        SourceLabel = ResolveSourceLabel(sourcePath, displaySourceLabel, importBatchId);
        _studyElapsed = TimeSpan.FromSeconds(_initialStudySeconds);
        StudyTimeText = _studyElapsed.ToString(@"hh\:mm\:ss");
        _studyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _studyTimer.Tick += (_, _) =>
        {
            _studyElapsed = _studyElapsed.Add(TimeSpan.FromSeconds(1));
            StudyTimeText = _studyElapsed.ToString(@"hh\:mm\:ss");
        };
        _studyTimer.Start();

        InitializeSpacedRepetition();
    }
    //KONSTRUKTOR

    [RelayCommand]
    private async System.Threading.Tasks.Task GoBack()
    {
        //Guzik powrót, pytamy użytkownika, czy chce wrócić do menu głównego.
        if (_hasUnsavedChanges)
        {
            var mastered = Score;
            var remaining = Math.Max(0, TotalQuestions - mastered);
            var reportText = $"Zobacz ile Ci zostało: {remaining} z {TotalQuestions} pytań (opanowane: {mastered}).";

            var decision = await DialogService.ShowSaveProgressPromptAsync(reportText, mastered, remaining);
            if (decision == SaveProgressDecision.Cancel)
                return;

            if (decision == SaveProgressDecision.Save)
            {
                if (!TrySaveAllProgress(out var errorMessage))
                {
                    await DialogService.ShowInfoAsync("Błąd zapisu", errorMessage ?? "Nie udało się zapisać progresu.");
                    return;
                }
            }
        }

        _studyTimer.Stop();
        var sessionSeconds = Math.Max(0, (int)_studyElapsed.TotalSeconds - _initialStudySeconds);
        _onSessionExit?.Invoke(_importBatchId, SourcePath, sessionSeconds);
        _navigateToHomeView();
    }
    partial void OnIsFinishedChanged(bool value)
    {
        //Przy skończeniu nauki zestawu.
        if (value) _studyTimer.Stop();
        OnPropertyChanged(nameof(FinalOverlayOpacity));
        OnPropertyChanged(nameof(IsFinalOverlayHitTestVisible));
    }
    [RelayCommand]
    private async System.Threading.Tasks.Task SubmitSelectedAnswers()
    {
        // Pierwsze kliknięcie: pokaż wynik kolorami. Drugie kliknięcie: przejdź dalej.
        if (IsFinished || _questions.Count == 0 || _currentQuestion == null || !CanSubmitSelection)
            return;

        if (_awaitingAdvance)
        {
            _awaitingAdvance = false;
            LoadNextQuestion();
            return;
        }

        if (!IsAnswerInputEnabled)
            return;

        var selected = CurrentAnswers.Where(x => x.IsSelected).ToList();
        var selectedCount = selected.Count;
        var selectedCorrectCount = selected.Count(x => x.IsCorrect);
        var correctCount = CurrentAnswers.Count(x => x.IsCorrect);
        var isCorrect = selectedCount == correctCount && selectedCorrectCount == correctCount;

        CanSubmitSelection = false;
        IsAnswerInputEnabled = false;

        foreach (var answer in CurrentAnswers)
        {
            answer.RevealState = answer.IsCorrect
                ? AnswerOptionViewModel.RevealVisualState.Correct
                : answer.IsSelected
                    ? AnswerOptionViewModel.RevealVisualState.SelectedWrong
                    : AnswerOptionViewModel.RevealVisualState.Dimmed;
            answer.IsInteractive = false;
            answer.IsRevealed = true;
        }

        Log($"Submit Q#{_currentQuestion.Id}: selected={selectedCount}, selectedCorrect={selectedCorrectCount}, correctNeeded={correctCount}, result={isCorrect}");

        ApplyAnswerResult(isCorrect);
        _awaitingAdvance = true;
        CanSubmitSelection = true;

        await System.Threading.Tasks.Task.CompletedTask;
    }

    public void ToggleAnswerByIndex(int answerIndex)
    {
        if (!IsAnswerInputEnabled || _awaitingAdvance)
            return;

        if (answerIndex < 0 || answerIndex >= CurrentAnswers.Count)
            return;

        var option = CurrentAnswers[answerIndex];
        option.IsSelected = !option.IsSelected;
    }

    public void SubmitFromKeyboard()
    {
        if (CanSubmitSelection)
            SubmitSelectedAnswersCommand.Execute(null);
    }

    private void ApplyAnswerResult(bool isCorrect)
    {
        //Sprawdzamy poprawność odpowiedzi użytkownika.
        if (_currentQuestion == null)
            return;

        var progress = _progressByQuestionId[_currentQuestion.Id];
        progress.SeenCount++;
        progress.UpdatedAtUtc = DateTime.UtcNow;
        Log($"Answer result on Q#{_currentQuestion.Id}: isCorrect={isCorrect}, beforeBox={progress.BoxLevel}, seen={progress.SeenCount}");

        if (isCorrect)
        {
            progress.ConsecutiveCorrect++;
            progress.BoxLevel = Math.Min(_maxRepetitions, progress.BoxLevel + 1);

            if (IsMastered(progress))
            {
                RemoveScheduledEntriesForQuestion(_currentQuestion.Id);
            }
            else
            {
                var delay = Math.Max(1, progress.BoxLevel * 2);
                ScheduleQuestion(_currentQuestion, _currentStep + delay);
            }
        }
        else
        {
            progress.WrongCount++;
            progress.ConsecutiveCorrect = 0;
            progress.BoxLevel = Math.Max(0, progress.BoxLevel - _wrongAnswerPenalty);

            var extraRepetitions = 1 + _wrongAnswerPenalty;
            for (var i = 1; i <= extraRepetitions; i++)
                ScheduleQuestion(_currentQuestion, _currentStep + i);
        }

        _hasUnsavedChanges = true;
        Score = ActiveProgresses().Count(IsMastered);
        ReappearancesCount = ActiveProgresses().Sum(x => Math.Max(0, x.SeenCount - 1));
        RefreshProgressIndicators();
        RefreshSrDebugState();
        UpdateFinalReport();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveProgress()
    {
        //RC dla zapisania progresu.
        if (TrySaveAllProgress(out var errorMessage))
        {
            await DialogService.ShowInfoAsync("Zapisano", "Zapisano progres nauki.");
            return;
        }

        await DialogService.ShowInfoAsync("Błąd zapisu", errorMessage ?? "Nie udało się zapisać progresu.");
    }

    private void InitializeSpacedRepetition()
    {
        //Inicjujemy algorytm SR
        if (_questions.Count == 0)
        {
            UpdateFinalReport();
            IsFinished = true;
            return;
        }

        if (_importBatchId.HasValue && _importBatchId.Value != Guid.Empty)
        {
            try
            {
                using var db = new AppDb();
                EnsureQuestionProgressTable(db);

                var existing = db.QuestionProgresses
                    .Where(x => x.ImportBatchId == _importBatchId.Value)
                    .AsNoTracking()
                    .ToList();

                foreach (var item in existing)
                {
                    if (_questionIds.Contains(item.QuestionId))
                        _progressByQuestionId[item.QuestionId] = item;
                }
            }
            catch (Exception ex)
            {
                Log($"Load SR progress failed: {ex.Message}");
            }
        }

        foreach (var question in _questions)
        {
            if (!_progressByQuestionId.TryGetValue(question.Id, out var progress))
            {
                progress = new QuestionProgress
                {
                    ImportBatchId = _importBatchId ?? Guid.Empty,
                    QuestionId = question.Id,
                    BoxLevel = 0,
                    ConsecutiveCorrect = 0,
                    SeenCount = 0,
                    WrongCount = 0,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _progressByQuestionId[question.Id] = progress;
            }

            if (!IsMastered(progress))
            {
                for (var i = 0; i < _initialRepetitions; i++)
                    ScheduleQuestion(question, _currentStep + i);
            }
        }

        Score = ActiveProgresses().Count(IsMastered);
        ReappearancesCount = ActiveProgresses().Sum(x => Math.Max(0, x.SeenCount - 1));
        RefreshProgressIndicators();
        RefreshSrDebugState();
        UpdateFinalReport();

        LoadNextQuestion();
    }

    private void LoadNextQuestion()
    {
        //Ładowanie następnego pytania
        if (_reviewQueue.Count == 0)
        {
            IsAnswerInputEnabled = false;
            QuestionText = "Koniec sesji – wszystkie pytania opanowane!";
            CurrentAnswers.Clear();
            UpdateFinalReport();
            IsFinished = true;
            OnPropertyChanged(nameof(CurrentQuestionNumber));
            OnPropertyChanged(nameof(ProgressLabel));
            OnPropertyChanged(nameof(ProgressValue));
            return;
        }

        var next = GetNextDueQuestion();
        if (next == null)
        {
            //Jezeli nie ma już scheduled pytań to kończymy
            IsAnswerInputEnabled = false;
            QuestionText = "Koniec sesji – wszystkie pytania opanowane!";
            CurrentAnswers.Clear();
            UpdateFinalReport();
            IsFinished = true;
            OnPropertyChanged(nameof(CurrentQuestionNumber));
            OnPropertyChanged(nameof(ProgressLabel));
            OnPropertyChanged(nameof(ProgressValue));
            return;
        }

        _currentQuestion = next.Question;
        _lastQuestionId = _currentQuestion.Id;
        _currentStep = Math.Max(_currentStep + 1, next.DueStep);
        _awaitingAdvance = false;
        IsAnswerInputEnabled = true;
        RefreshSrDebugState();
        Log($"Load next Q#{_currentQuestion.Id}, due={next.DueStep}, step={_currentStep}, queue={_reviewQueue.Count}");

        QuestionText = _currentQuestion.Text;
        SetAuthor = _currentQuestion.Author;

        foreach (var existing in CurrentAnswers)
            existing.PropertyChanged -= OnAnswerOptionPropertyChanged;

        CurrentAnswers.Clear();
        foreach (var a in _currentQuestion.Answers.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key))
        {
            var option = new AnswerOptionViewModel(a.Key, a.Text, a.IsCorrect);
            option.PropertyChanged += OnAnswerOptionPropertyChanged;
            CurrentAnswers.Add(option);
        }

        UpdateSelectionState();

        OnPropertyChanged(nameof(CurrentQuestionNumber));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(ProgressValue));
    }

    private bool IsMastered(QuestionProgress progress)
        => progress.BoxLevel >= _maxRepetitions;

    private void ScheduleQuestion(Question question, int dueStep)
    {
        //Planowanie pytania
        var finalDue = Math.Max(_currentStep, dueStep);
        _reviewQueue.Add(new ScheduledQuestion
        {
            Question = question,
            DueStep = finalDue
        });
        Log($"Schedule Q#{question.Id} for step={finalDue}, queue={_reviewQueue.Count}");
    }

    private ScheduledQuestion? GetNextDueQuestion()
    {
        PurgeMasteredItemsFromQueue();

        if (_reviewQueue.Count == 0)
            return null;

        var minDue = _reviewQueue.Min(x => x.DueStep);
        _currentStep = Math.Max(_currentStep, minDue);

        var dueItems = _reviewQueue
            .Where(x => x.DueStep <= _currentStep)
            .ToList();

        if (dueItems.Count == 0)
        {
            var firstFuture = _reviewQueue
                .OrderBy(x => x.DueStep)
                .First();
            _reviewQueue.Remove(firstFuture);
            return firstFuture;
        }

        var pick = dueItems.FirstOrDefault(x => x.Question.Id != _lastQuestionId)
                   ?? dueItems.First();

        _reviewQueue.Remove(pick);
        return pick;
    }

    private void RemoveScheduledEntriesForQuestion(int questionId)
    {
        var removed = _reviewQueue.RemoveAll(x => x.Question.Id == questionId);
        if (removed > 0)
            Log($"Purge queued mastered Q#{questionId}, removed={removed}");
    }

    private void PurgeMasteredItemsFromQueue()
    {
        var removed = _reviewQueue.RemoveAll(x =>
            _progressByQuestionId.TryGetValue(x.Question.Id, out var progress) && IsMastered(progress));

        if (removed > 0)
            Log($"Purge mastered items from queue, removed={removed}");
    }

    private bool TrySaveAllProgress(out string? errorMessage)
    {
        //Zapisanie dla kazdego pytania
        errorMessage = null;
        if (!_importBatchId.HasValue || _importBatchId.Value == Guid.Empty)
            return true;

        try
        {
            var batchId = _importBatchId.Value;
            using var db = new AppDb();
            EnsureQuestionProgressTable(db);

            var existingByQuestionId = db.QuestionProgresses
                .Where(x => x.ImportBatchId == batchId)
                .ToDictionary(x => x.QuestionId);

            foreach (var progress in ActiveProgresses())
            {
                if (!existingByQuestionId.TryGetValue(progress.QuestionId, out var existing))
                {
                    db.QuestionProgresses.Add(new QuestionProgress
                    {
                        ImportBatchId = batchId,
                        QuestionId = progress.QuestionId,
                        BoxLevel = progress.BoxLevel,
                        ConsecutiveCorrect = progress.ConsecutiveCorrect,
                        SeenCount = progress.SeenCount,
                        WrongCount = progress.WrongCount,
                        UpdatedAtUtc = progress.UpdatedAtUtc
                    });
                    continue;
                }

                existing.BoxLevel = progress.BoxLevel;
                existing.ConsecutiveCorrect = progress.ConsecutiveCorrect;
                existing.SeenCount = progress.SeenCount;
                existing.WrongCount = progress.WrongCount;
                existing.UpdatedAtUtc = progress.UpdatedAtUtc;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            errorMessage = $"Nie udało się zapisać progresu: {ex.Message}";
            Log($"Save SR progress failed: {ex.Message}");
            return false;
        }

        _hasUnsavedChanges = false;
        Log($"Saved SR progress: batch={_importBatchId}, mastered={Score}/{TotalQuestions}, reappearances={ReappearancesCount}");
        return true;
    }

    private static void EnsureQuestionProgressTable(AppDb db)
    {
        AppDb.EnsureTables(db);
    }

    private void RefreshSrDebugState()
    {
        //Tekst do debugowania, czy algorytm działa poprawnie.
        SrDebugText = $"SR\ninitreps={_initialRepetitions}\npenalty={_wrongAnswerPenalty}\nmax={_maxRepetitions}\nqueuecount={_reviewQueue.Count}\nstep={_currentStep}\nrepeats={ReappearancesCount}";
    }

    private void RefreshProgressIndicators()
    {
        OnPropertyChanged(nameof(CurrentQuestionNumber));
        OnPropertyChanged(nameof(RemainingAnswersCount));
        OnPropertyChanged(nameof(SubmittedAnswersCount));
        OnPropertyChanged(nameof(CorrectAnswersCount));
        OnPropertyChanged(nameof(IncorrectAnswersCount));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(ProgressValue));
    }

    private void UpdateFinalReport()
    {
        var mastered = Score;
        var notMastered = Math.Max(0, TotalQuestions - Score);

        var seen = ActiveProgresses().Sum(x => x.SeenCount);
        var wrong = ActiveProgresses().Sum(x => x.WrongCount);

        var accuracy = seen == 0
            ? 0.0
            : (seen - wrong) * 100.0 / seen;

        var masteryPercent = TotalQuestions == 0
            ? 0.0
            : mastered * 100.0 / TotalQuestions;

        FinalReportSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Wynik",
                Values = [mastered, notMastered, ReappearancesCount, wrong]
            }
        ];

        FinalReportXAxes =
        [
            new Axis
            {
                Labels = ["Opanowane", "Do opanowania", "Powtórki", "Błędy"]
            }
        ];

        FinalMasteryText = $"Opanowanie zestawu: {masteryPercent:F1}%";
        FinalAccuracyText = $"Skuteczność: {accuracy:F1}%";
        FinalTotalsText = $"Opanowane: {mastered}, do opanowania: {notMastered}, powtórki: {ReappearancesCount}, odpowiedzi: {seen}, błędy: {wrong}";
    }

    private IEnumerable<QuestionProgress> ActiveProgresses()
    {
        foreach (var pair in _progressByQuestionId)
        {
            if (_questionIds.Contains(pair.Key))
                yield return pair.Value;
        }
    }
    

    private void OnAnswerOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnswerOptionViewModel.IsSelected))
            UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var selectedCount = CurrentAnswers.Count(x => x.IsSelected);
        var correctCount = CurrentAnswers.Count(x => x.IsCorrect);

        CanSubmitSelection = _awaitingAdvance || (IsAnswerInputEnabled && selectedCount > 0);
        IsSelectionComplete = selectedCount > 0 && selectedCount == correctCount;
    }

    private static void Log(string message)
    {
        AppLog.Debug("ExamSR", message);
    }

    private static string ResolveSourceLabel(string? sourcePath, string? displaySourceLabel, Guid? importBatchId)
    {
        if (sourcePath is not null && sourcePath.StartsWith("db://", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(displaySourceLabel))
                return displaySourceLabel.Trim();

            return importBatchId is { } batchId && batchId != Guid.Empty
                ? $"Zestaw {batchId:N}"
                : "Zestaw z bazy";
        }

        if (!string.IsNullOrWhiteSpace(displaySourceLabel))
            return displaySourceLabel.Trim();

        if (string.IsNullOrWhiteSpace(sourcePath))
            return "brak źródła";

        return sourcePath.Trim();
    }


    private static List<Question> BuildDemoQuestions()
    {
        return
        [
            new Question
            {
                Id = 1,
                Author = "demo",
                Text = "Pytanie 1",
                Answers =
                [
                    new Answer { Key = "a", Text = "Opcja A", IsCorrect = false },
                    new Answer { Key = "b", Text = "Opcja B", IsCorrect = true  },
                    new Answer { Key = "c", Text = "Opcja C", IsCorrect = false },
                    new Answer { Key = "d", Text = "Opcja D", IsCorrect = false }
                ]
            },
            new Question
            {
                Id = 2,
                Author = "demo",
                Text = "Pytanie 2",
                Answers =
                [
                    new Answer { Key = "a", Text = "Opcja A", IsCorrect = true  },
                    new Answer { Key = "b", Text = "Opcja B", IsCorrect = false },
                    new Answer { Key = "c", Text = "Opcja C", IsCorrect = false },
                    new Answer { Key = "d", Text = "Opcja D", IsCorrect = false }
                ]
            }
        ];
    }
    
}