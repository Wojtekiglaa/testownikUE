using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using testownikUE.Data;
using testownikUE.Models;
using testownikUE.Services;

namespace testownikUE.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentPage;

    private HomeViewViewModel? _homePage;

    private string DebugInfo { get; }

    public MainWindowViewModel()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Arch: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Description: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Processarch: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"RuntimeId: {RuntimeInformation.RuntimeIdentifier}");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");
        sb.AppendLine($"Current Culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine($"AppData Root: {AppPaths.Root}");
        sb.AppendLine($"DB Directory: {AppPaths.DbDir}");
        sb.AppendLine($"Main DB: {AppPaths.DbPath}");
        sb.AppendLine($"UserSettings DB: {AppPaths.UserSettingsDbPath}");

        DebugInfo = sb.ToString();
        CurrentPage = BuildHomePage();
        AppPaths.EnsureCreated(); 
    }
    

    private void NavigateToHomeView()
    {
        CurrentPage = BuildHomePage();
    }

    private void NavigateToEditorView()
        => NavigateToEditorView(null, string.Empty);

    private void NavigateToEditorView(Guid? batchId, string sourcePath)
        => CurrentPage = new EditorViewModel(NavigateToHomeView, batchId, sourcePath);

    private void NavigateToExamView() => CurrentPage = new ExamViewModel(NavigateToHomeView, settings: _userSettingsService.Load());

    private void NavigateToSettingsView() => CurrentPage = new SettingsMenuViewModel(NavigateToHomeView, _userSettingsService);

    private async Task ShowGlobalStatsAsync()
    {
        try
        {
            await using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var settings = _userSettingsService.Load();
            var maxRepetitions = Math.Max(1, settings.MaxRepetitions);

            var setsInDb = db.SetsInDb.AsNoTracking().ToList();
            var questions = db.Questions.AsNoTracking().ToList();
            var progresses = db.QuestionProgresses.AsNoTracking().ToList();

            var totalSets = setsInDb.Count;
            var totalQuestions = questions.Count;
            var totalProgressEntries = progresses.Count;
            var totalStudySeconds = setsInDb.Sum(x => Math.Max(0, x.TotalStudySeconds));
            var totalSeen = progresses.Sum(x => Math.Max(0, x.SeenCount));
            var totalWrong = progresses.Sum(x => Math.Max(0, x.WrongCount));
            var totalMastered = progresses.Count(x => x.BoxLevel >= maxRepetitions);
            var seenQuestions = progresses.Count(x => x.SeenCount > 0);

            var snapshot = new GlobalStatsSnapshot
            {
                TotalSets = totalSets,
                TotalQuestions = totalQuestions,
                TotalProgressEntries = totalProgressEntries,
                TotalStudySeconds = totalStudySeconds,
                TotalSeen = totalSeen,
                TotalWrong = totalWrong,
                TotalMastered = totalMastered,
                QuestionsSeenAtLeastOnce = seenQuestions,
                MasteryThreshold = maxRepetitions,
                TotalStudyHours = totalStudySeconds / 3600.0,
                AverageStudyMinutesPerSet = totalSets == 0 ? 0.0 : (double)totalStudySeconds / totalSets / 60.0,
                AverageStudyMinutesPerQuestion = totalQuestions == 0 ? 0.0 : (double)totalStudySeconds / totalQuestions / 60.0,
                AccuracyPercent = totalSeen == 0 ? 0.0 : Math.Max(0.0, (totalSeen - totalWrong) * 100.0 / totalSeen),
                CoveragePercent = totalQuestions == 0 ? 0.0 : seenQuestions * 100.0 / totalQuestions,
                MasteryPercent = totalQuestions == 0 ? 0.0 : totalMastered * 100.0 / totalQuestions
            };

            await DialogService.ShowGlobalStatsAsync(snapshot);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Błąd statystyk", $"Nie udało się policzyć statystyk globalnych:\n{ex.Message}");
        }
    }
    
    private readonly FilePickerService _filePickerService = new();
    private readonly ImportExportService _importExportService = new();
    private readonly UserSettingsService _userSettingsService = new();
    



    private HomeViewViewModel BuildHomePage()
    {
        //Strona główna to Homeview, od razu podmieniamy. Zmieniając okienka podmieniamy widok w głównym oknie.
        _homePage ??= new HomeViewViewModel(
            debugInfo: DebugInfo,
            navigateToExam: NavigateToExamView,
            navigateToEditor: NavigateToEditorView,
            navigateToSettings: NavigateToSettingsView,
            toggleTheme: ToggleRuntimeTheme,
            printDb: PrintDb,
            clearDbAsync: ClearDbAsync,
            setsInDbProvider: GetAllSetsInDb,
            openSetInDbAsync: OpenSetInDbAsync,
            editSetInDbAsync: EditSetInDbAsync,
            deleteSetInDbAsync: DeleteSetInDbAsync,
            searchSetAsync: SearchSetAsync,
            editLatestSetAsync: EditLatestSetAsync,
            showGlobalStatsAsync: ShowGlobalStatsAsync,
            handleFileActionAsync: HandleFileActionAsync);

        _homePage.ReloadSetsInDb();
        return _homePage;
    }
    
    private async Task HandleFileActionAsync(FilePickerAction action)
    {
        if (action == FilePickerAction.New)
        {
            NavigateToEditorView();
            return;
        }

        if (action == FilePickerAction.Edit)
        {
            await EditLatestSetAsync();
            return;
        }

        var files = await _filePickerService.PickJsonFilesAsync();
        if (files.Count == 0) return;

        switch (action)
        {
            case FilePickerAction.Import:
                await OpenSetFromPathAsync(files[0].Path.LocalPath);
                break;
            case FilePickerAction.Exam:
                NavigateToExamView();
                break;
        }
    }

    private static Task ShowErrorAsync(string title, string message)
        => DialogService.ShowInfoAsync(title, message);

    private async Task OpenSetInDbAsync(SetInDb setInDb)
    {
        if (setInDb.LastImportBatchId == Guid.Empty)
        {
            await ShowErrorAsync("Brak danych zestawu", $"Zestaw '{setInDb.SetName}' nie ma przypisanego identyfikatora importu.");
            return;
        }

        await OpenBatchFromDbAsync(setInDb.LastImportBatchId, setInDb.SourcePath, true, setInDb.ContentHash);
    }

    private async Task OpenSetFromPathAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            await ShowErrorAsync("Nie można otworzyć pliku", "Wybrany plik nie istnieje albo ścieżka jest pusta.");
            return;
        }

        try
        {
            var normalizedSourcePath = NormalizeSourcePath(sourcePath);

            var jsonContent = await File.ReadAllTextAsync(sourcePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                await ShowErrorAsync("Pusty plik", $"Plik '{Path.GetFileName(sourcePath)}' nie zawiera danych JSON.");
                return;
            }

            var contentHash = ComputeImportSignature(jsonContent);
            if (string.IsNullOrWhiteSpace(contentHash))
            {
                await ShowErrorAsync("Nieprawidłowy JSON", $"Nie udało się odczytać zawartości pliku '{Path.GetFileName(sourcePath)}'.");
                return;
            }

            await using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var matchingImportedSets = db.SetsInDb
                .AsNoTracking()
                .Where(x => x.ContentHash == contentHash || x.SourcePath == normalizedSourcePath || x.SourcePath == sourcePath)
                .OrderByDescending(x => x.OpenedAtUtc)
                .ToList();

            var alreadyImported = matchingImportedSets.Count > 0;

            if (alreadyImported)
            {
                var shouldReimport = await DialogService.ShowReimportPromptAsync(Path.GetFileName(sourcePath));
                if (!shouldReimport)
                {
                    var existingBatchId = matchingImportedSets
                        .Select(x => x.LastImportBatchId)
                        .FirstOrDefault();

                    if (existingBatchId != Guid.Empty)
                        await OpenBatchFromDbAsync(existingBatchId, normalizedSourcePath, false, contentHash);
                    else
                        await ShowErrorAsync("Brak importu", "Zestaw został już wcześniej wykryty, ale nie ma zapisanego identyfikatora importu.");

                    return;
                }

                CleanupHistoricalDataForReimport(db, normalizedSourcePath, sourcePath, contentHash);
            }

            var batchId = _importExportService.ImportJsonToDatabase(jsonContent);

            await OpenBatchFromDbAsync(batchId, normalizedSourcePath, true, contentHash);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Błąd importu", $"Nie udało się zaimportować zestawu:\n{ex.Message}");
        }
    }

    private Task EditSetInDbAsync(SetInDb setInDb)
    {
        if (setInDb.LastImportBatchId == Guid.Empty)
            return ShowErrorAsync("Brak danych zestawu", $"Zestaw '{setInDb.SetName}' nie ma przypisanego identyfikatora importu.");

        NavigateToEditorView(setInDb.LastImportBatchId, setInDb.SourcePath);
        return Task.CompletedTask;
    }

    private async Task DeleteSetInDbAsync(SetInDb setInDb)
    {
        try
        {
            await using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var existing = db.SetsInDb.FirstOrDefault(x => x.Id == setInDb.Id || x.SourcePath == setInDb.SourcePath);
            if (existing == null)
            {
                await ShowErrorAsync("Nie znaleziono zestawu", "Wybrany zestaw nie istnieje już w bazie danych.");
                return;
            }

            var confirm = await DialogService.ShowConfirmAsync(
                "Usuń zestaw",
                $"Czy na pewno chcesz usunąć zestaw '{existing.SetName}' z bazy?\nTa operacja usunie też pytania i progres dla tego zestawu!",
                confirmLabel: "Usuń",
                cancelLabel: "Anuluj");

            if (!confirm)
                return;

            var batchId = existing.LastImportBatchId;
            db.SetsInDb.Remove(existing);

            if (batchId != Guid.Empty)
            {
                var questions = db.Questions
                    .Where(x => x.ImportBatchId == batchId)
                    .ToList();

                if (questions.Count > 0)
                    db.Questions.RemoveRange(questions);

                var progress = db.QuestionProgresses
                    .Where(x => x.ImportBatchId == batchId)
                    .ToList();

                if (progress.Count > 0)
                    db.QuestionProgresses.RemoveRange(progress);
            }

            await db.SaveChangesAsync();
            _homePage?.ReloadSetsInDb();
            await DialogService.ShowInfoAsync("Usunięto", "Zestaw został usunięty z bazy danych.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Błąd usuwania", $"Nie udało się usunąć zestawu:\n{ex.Message}");
        }
    }

    private async Task SearchSetAsync()
    {
        var sets = GetAllSetsInDb();
        if (sets.Count == 0)
        {
            await DialogService.ShowInfoAsync("Brak zestawów", "Nie ma jeszcze żadnych zestawów zapisanych w bazie.");
            return;
        }

        try
        {
            var entries = BuildSetSearchEntries(sets);
            var picked = await DialogService.ShowSetInDbSearchDialogAsync("Szukaj zestawu", entries);
            if (picked == null)
                return;

            switch (picked.Action)
            {
                case SetInDbDialogAction.Open:
                    await OpenSetInDbAsync(picked.Set);
                    break;
                case SetInDbDialogAction.Edit:
                    await EditSetInDbAsync(picked.Set);
                    break;
                case SetInDbDialogAction.Delete:
                    await DeleteSetInDbAsync(picked.Set);
                    break;
            }
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync("Błąd wyszukiwania", $"Nie udało się otworzyć wyszukiwania: {ex.Message}");
        }
    }

    private async Task EditLatestSetAsync()
    {
        try
        {
            await using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var sets = db.SetsInDb
                .AsNoTracking()
                .Where(x => x.LastImportBatchId != Guid.Empty)
                .OrderByDescending(x => x.OpenedAtUtc)
                .ToList();

            if (sets.Count == 0)
            {
                await DialogService.ShowInfoAsync("Brak zestawów", "Nie ma jeszcze żadnych zestawów zapisanych w bazie.");
                return;
            }

            var picked = await DialogService.ShowSetInDbPickerAsync("Edytuj zestawy w bazie", sets);
            if (picked == null)
                return;

            NavigateToEditorView(picked.LastImportBatchId, picked.SourcePath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Błąd edycji", $"Nie udało się otworzyć listy zestawów do edycji:\n{ex.Message}");
        }
    }

    private Task OpenBatchFromDbAsync(Guid batchId, string sourcePath, bool updateRecent, string contentHash = "")
    {
        if (batchId == Guid.Empty)
            return ShowErrorAsync("Brak danych zestawu", "Nie można otworzyć zestawu bez identyfikatora importu.");

        try
        {
            using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var importedQuestions = db.Questions
                .Include(q => q.Answers)
                .Where(q => q.ImportBatchId == batchId)
                .AsNoTracking()
                .ToList();

            if (importedQuestions.Count == 0)
                return ShowErrorAsync("Brak pytań", "W bazie nie znaleziono pytań dla tego zestawu.");

            if (updateRecent)
                SaveSetInDb(db, sourcePath, importedQuestions.Count, batchId, contentHash);

            string? displaySourceLabel = null;
            if (IsSyntheticDbSourcePath(sourcePath))
            {
                displaySourceLabel = db.SetsInDb
                    .Where(x => x.LastImportBatchId == batchId || x.SourcePath == sourcePath)
                    .OrderByDescending(x => x.OpenedAtUtc)
                    .Select(x => x.SetName)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(displaySourceLabel))
                    displaySourceLabel = $"Zestaw {batchId:N}";
            }

            var totalStudySeconds = db.SetsInDb
                .Where(x => x.SourcePath == sourcePath)
                .Select(x => x.TotalStudySeconds)
                .FirstOrDefault();

            CurrentPage = new ExamViewModel(
                NavigateToHomeView,
                importedQuestions,
                sourcePath,
                displaySourceLabel,
                batchId,
                _userSettingsService.Load(),
                onSessionExit: SaveStudyTimeOnExit,
                initialStudySeconds: totalStudySeconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return ShowErrorAsync("Błąd odczytu zestawu", $"Nie udało się otworzyć zestawu z bazy:\n{ex.Message}");
        }
    }

    private static void SaveSetInDb(AppDb db, string sourcePath, int importedQuestionsCount, Guid batchId, string contentHash)
    {
        sourcePath = NormalizeSourcePath(sourcePath);
        var nowUtc = DateTime.UtcNow;
        var isSyntheticDbSource = IsSyntheticDbSourcePath(sourcePath);
        var existing = db.SetsInDb.FirstOrDefault(x => x.LastImportBatchId == batchId)
                        ?? db.SetsInDb.FirstOrDefault(x => x.SourcePath == sourcePath);

        var setName = existing?.SetName;
        if (string.IsNullOrWhiteSpace(setName))
            setName = isSyntheticDbSource
                ? $"Zestaw {batchId:N}"
                : Path.GetFileNameWithoutExtension(sourcePath);

        if (existing == null)
        {
            db.SetsInDb.Add(new SetInDb
            {
                SetName = setName,
                SourcePath = sourcePath,
                LastImportBatchId = batchId,
                ImportedQuestionsCount = importedQuestionsCount,
                ContentHash = contentHash,
                TotalStudySeconds = 0,
                OpenedAtUtc = nowUtc
            });
        }
        else
        {
            existing.SetName = setName;
            existing.SourcePath = sourcePath;
            existing.LastImportBatchId = batchId;
            existing.ImportedQuestionsCount = importedQuestionsCount;
            existing.ContentHash = contentHash;
            existing.OpenedAtUtc = nowUtc;
        }

        var existingId = existing?.Id ?? 0;
        var duplicateRows = db.SetsInDb
            .Where(x => x.LastImportBatchId == batchId && x.Id != existingId)
            .ToList();

        if (duplicateRows.Count > 0)
            db.SetsInDb.RemoveRange(duplicateRows);

        db.SaveChanges();//synchroniczny zapis-metoda jest wywoływana z kontekstu sync (OpenBatchFromDbAsync)
    }

    private IReadOnlyList<SetInDb> GetAllSetsInDb()
    {
        try
        {
            using var db = new AppDb();
            EnsureSetsInDbTable(db);

            return db.SetsInDb
                .AsNoTracking()
                .OrderByDescending(x => x.OpenedAtUtc)
                .ToList();
        }
        catch (Exception ex)
        {
            AppLog.Error("MainWindow", "Load sets from DB failed.", ex);
            _ = DialogService.ShowInfoAsync("Błąd odczytu", $"Nie udało się wczytać listy zestawów:\n{ex.Message}");
            return [];
        }
    }

    private static IReadOnlyList<SetInDbSearchEntry> BuildSetSearchEntries(IReadOnlyList<SetInDb> sets)
    {
        try
        {
            using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var batchIds = sets
                .Where(x => x.LastImportBatchId != Guid.Empty)
                .Select(x => x.LastImportBatchId)
                .Distinct()
                .ToList();

            var searchableByBatch = new Dictionary<Guid, string>();

            if (batchIds.Count > 0)
            {
                var questions = db.Questions
                    .AsNoTracking()
                    .Include(q => q.Answers)
                    .Where(q => batchIds.Contains(q.ImportBatchId))
                    .ToList();

                searchableByBatch = questions
                    .GroupBy(q => q.ImportBatchId)
                    .ToDictionary(
                        g => g.Key,
                        g => string.Join(" ", g.SelectMany(q =>
                            new[] { q.Text }
                                .Concat(q.Answers.Select(a => a.Text)))));
            }

            return sets
                .Select(set => new SetInDbSearchEntry
                {
                    Set = set,
                    SearchText = set.LastImportBatchId != Guid.Empty && searchableByBatch.TryGetValue(set.LastImportBatchId, out var text)
                        ? text
                        : string.Empty
                })
                .ToList();
        }
        catch (Exception ex)
        {
            AppLog.Error("MainWindow", "Build search entries failed.", ex);
            return sets
                .Select(set => new SetInDbSearchEntry
                {
                    Set = set,
                    SearchText = string.Empty
                })
                .ToList();
        }
    }

    private static void EnsureSetsInDbTable(AppDb db)
    {
        AppDb.EnsureTables(db);
    }

    private static void ToggleRuntimeTheme()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var current = app.RequestedThemeVariant == ThemeVariant.Default
            ? app.ActualThemeVariant
            : app.RequestedThemeVariant;

        app.RequestedThemeVariant = current == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    private void SaveStudyTimeOnExit(Guid? batchId, string sourcePath, int sessionSeconds)
    {
        if (sessionSeconds <= 0)
            return;

        try
        {
            var normalizedSourcePath = NormalizeSourcePath(sourcePath);

            using var db = new AppDb();
            EnsureSetsInDbTable(db);

            var setInDb = db.SetsInDb
                .FirstOrDefault(x => (!string.IsNullOrWhiteSpace(normalizedSourcePath) && x.SourcePath == normalizedSourcePath)
                                  || (batchId.HasValue && batchId.Value != Guid.Empty && x.LastImportBatchId == batchId.Value));

            if (setInDb == null)
                return;

            setInDb.TotalStudySeconds += sessionSeconds;
            setInDb.OpenedAtUtc = DateTime.UtcNow;
            db.SaveChanges();

            AppLog.Info("StudyTime", $"Saved +{sessionSeconds}s for '{setInDb.SetName}', total={setInDb.TotalStudySeconds}s.");
        }
        catch (Exception ex)
        {
            AppLog.Error("StudyTime", "Save study time failed.", ex);
        }
    }

    private void PrintDb()
    {
        //Debugowy print zawartości DB do konsoli
        using var db = new AppDb();
        var questions = db.Questions
            .Include(q => q.Answers)
            .AsNoTracking()
            .OrderBy(q => q.DisplayOrder)
            .ThenBy(q => q.Id)
            .ToList();

        if (questions.Count == 0)
        {
            const string noData = "DB jest puste.";
            AppLog.Info("MainWindow", noData);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"DB: {questions.Count} pytan");

        foreach (var question in questions)
        {
            sb.AppendLine($"Q#{question.Id} [BATCH:{question.ImportBatchId}]: {question.Text} (author: {question.Author})");

            foreach (var answer in question.Answers.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Key))
            {
                sb.AppendLine($"  - [{answer.Key}] {answer.Text} (correct={answer.IsCorrect})");
            }
        }

        using (var settingsDb = new UserSettingsDb())
        {
            UserSettingsDb.EnsureTables(settingsDb);
            var settings = settingsDb.AppSettings
                .AsNoTracking()
                .OrderBy(x => x.SettingKey)
                .ToList();

            sb.AppendLine();
            sb.AppendLine("UserSettings (usersettings.db):");
            if (settings.Count == 0)
            {
                sb.AppendLine("  - brak ustawien");
            }
            else
            {
                foreach (var setting in settings)
                    sb.AppendLine($"  - {setting.SettingKey}={setting.SettingValue} (updated={setting.UpdatedAtUtc:O})");
            }
        }

        var output = sb.ToString();
        AppLog.Info("MainWindow", output);
    }

    private async Task ClearDbAsync()
    {
        //Debugowe czyszczenie zawartości DB.
        try
        {
            await using var db = new AppDb();
            var answersCount = db.Answers.Count();
            var questionsCount = db.Questions.Count();
            var setsInDbCount = db.SetsInDb.Count();
            var progressCount = db.QuestionProgresses.Count();

            db.Answers.RemoveRange(db.Answers);
            db.Questions.RemoveRange(db.Questions);
            db.QuestionProgresses.RemoveRange(db.QuestionProgresses);
            db.SetsInDb.RemoveRange(db.SetsInDb);
            await db.SaveChangesAsync();

            var message = $"Wyczyszczono DB: usunięto {questionsCount} pytań, {answersCount} odpowiedzi, {progressCount} wpisów progresu i {setsInDbCount} zapisanych zestawów.";
            AppLog.Info("MainWindow", message);

            if (CurrentPage is HomeViewViewModel homePage)
                homePage.ReloadSetsInDb();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Błąd czyszczenia DB", $"Nie udało się wyczyścić bazy danych:\n{ex.Message}");
        }
    }

    private static string ComputeImportSignature(string jsonContent)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<JsonQuestionDto>>(jsonContent);
            if (parsed is not { Count: > 0 })
                return string.Empty;

            var canonical = new StringBuilder();

            foreach (var item in parsed.OrderBy(x => x.questionId))
            {
                canonical.Append(item.questionId).Append('|')
                    .Append(string.IsNullOrWhiteSpace(item.questionAuthor) ? string.Empty : item.questionAuthor.Trim()).Append('|')
                    .Append(string.IsNullOrWhiteSpace(item.question) ? string.Empty : item.question.Trim()).Append('|');

                foreach (var answer in item.answers.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    canonical.Append(answer.Key.Trim().ToUpperInvariant())
                        .Append('=').Append(string.IsNullOrWhiteSpace(answer.Value) ? string.Empty : answer.Value.Trim()).Append('|');
                }

                foreach (var correct in item.GetCorrectAnswers().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    canonical.Append('#').Append(correct.Trim().ToUpperInvariant()).Append('|');
                }

                canonical.AppendLine();
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        }
        catch (Exception ex)
        {
            AppLog.Error("Import", "Compute import signature failed.", ex);
            return string.Empty;
        }
    }

    private static string NormalizeSourcePath(string sourcePath)
    {
        //"normalizujemy" path, czyli usuwamy leading i trailing spacje
        if (string.IsNullOrWhiteSpace(sourcePath))
            return string.Empty;

        if (IsSyntheticDbSourcePath(sourcePath))
            return sourcePath.Trim();

        try
        {
            return Path.GetFullPath(sourcePath).Trim();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Path", $"Normalize fallback for '{sourcePath}': {ex.Message}");
            return sourcePath.Trim();
        }
    }

    private static void CleanupHistoricalDataForReimport(AppDb db, string normalizedSourcePath, string sourcePath, string contentHash)
    {
        var matchingSets = db.SetsInDb
            .Where(x => x.ContentHash == contentHash || x.SourcePath == normalizedSourcePath || x.SourcePath == sourcePath)
            .ToList();

        if (matchingSets.Count == 0)
            return;

        var staleBatchIds = matchingSets
            .Select(x => x.LastImportBatchId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (staleBatchIds.Count > 0)
        {
            var staleQuestions = db.Questions.Where(x => staleBatchIds.Contains(x.ImportBatchId)).ToList();
            if (staleQuestions.Count > 0)
                db.Questions.RemoveRange(staleQuestions);

            var staleProgress = db.QuestionProgresses.Where(x => staleBatchIds.Contains(x.ImportBatchId)).ToList();
            if (staleProgress.Count > 0)
                db.QuestionProgresses.RemoveRange(staleProgress);
        }

        db.SetsInDb.RemoveRange(matchingSets);
        db.SaveChanges();

        AppLog.Info("Import", $"Reimport cleanup removed {matchingSets.Count} set rows and {staleBatchIds.Count} historical batch references.");
    }

    private static bool IsSyntheticDbSourcePath(string sourcePath)
    //Funkcja sprawdza, czy source zestawu jest "syntetyczne" czyli stworzone w edytorze zamiast zaimportowane z zewnątrz
        => sourcePath.StartsWith("db://", StringComparison.OrdinalIgnoreCase);

    
    
    
    public enum FilePickerAction
    {
        New,
        Import,
        Edit,
        Exam
    }
}




