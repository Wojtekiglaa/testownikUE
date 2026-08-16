using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testownikUE.Models;

namespace testownikUE.ViewModels;

public partial class HomeViewViewModel : ViewModelBase
{
    private readonly Action _navigateToExam;
    private readonly Action _navigateToEditor;
    private readonly Action _navigateToSettings;
    private readonly Action _toggleTheme;
    private readonly Action _printDb;
    private readonly Func<Task> _clearDbAsync;
    private readonly Func<SetInDb, Task> _openSetInDbAsync;
    private readonly Func<SetInDb, Task> _editSetInDbAsync;
    private readonly Func<SetInDb, Task> _deleteSetInDbAsync;
    private readonly Func<Task> _searchSetAsync;
    private readonly Func<Task> _editLatestSetAsync;
    private readonly Func<Task> _showGlobalStatsAsync;
    private readonly Func<IEnumerable<SetInDb>> _setsInDbProvider;

    public HomeViewViewModel(
        string debugInfo,
        Action navigateToExam,
        Action navigateToEditor,
        Action navigateToSettings,
        Action toggleTheme,
        Action printDb,
        Func<Task> clearDbAsync,
        Func<IEnumerable<SetInDb>> setsInDbProvider,
        Func<SetInDb, Task> openSetInDbAsync,
        Func<SetInDb, Task> editSetInDbAsync,
        Func<SetInDb, Task> deleteSetInDbAsync,
        Func<Task> searchSetAsync,
        Func<Task> editLatestSetAsync,
        Func<Task> showGlobalStatsAsync,
        Func <MainWindowViewModel.FilePickerAction, Task> handleFileActionAsync)
    {
        DebugInfo = debugInfo;
        #if DEBUG
            IsDebug = true;
        #else
            IsDebug = false; //sprawdzamy, czy jestesmy w srodowisku debug, użyte do ukrywania DEBUG opcji w GUI
        #endif
        _navigateToExam = navigateToExam;
        _navigateToEditor = navigateToEditor;
        _navigateToSettings = navigateToSettings;
        _toggleTheme = toggleTheme;
        _printDb = printDb;
        _clearDbAsync = clearDbAsync;
        _openSetInDbAsync = openSetInDbAsync;
        _editSetInDbAsync = editSetInDbAsync;
        _deleteSetInDbAsync = deleteSetInDbAsync;
        _searchSetAsync = searchSetAsync;
        _editLatestSetAsync = editLatestSetAsync;
        _showGlobalStatsAsync = showGlobalStatsAsync;
        _handleFileActionAsync = handleFileActionAsync;
        _setsInDbProvider = setsInDbProvider;

        ReloadSetsInDb();
    }//KONSTRUKTOR menu głównego
    
    public string DebugInfo { get; }
    public ObservableCollection<SetInDb> SetsInDb { get; } = new();

    public void ReloadSetsInDb()
    {
        SetsInDb.Clear();

        foreach (var setInDb in _setsInDbProvider())
            SetsInDb.Add(setInDb);
    }
    //Te wszystkie relaycommand to odnośniki do głównego mainwindow, ono zarządza wszystkim

    [RelayCommand]
    private void NavigateToExamView() => _navigateToExam();

    [RelayCommand]
    private void NavigateToEditorView() => _navigateToEditor();

    [RelayCommand]
    private void NavigateToSettingsView() => _navigateToSettings();
    [RelayCommand]
    private void ToggleTheme() => _toggleTheme();
    [RelayCommand]
    private void PrintDb() => _printDb();
    [RelayCommand]
    private Task ClearDb() => _clearDbAsync();
    [RelayCommand]
    private Task NewSet() => _handleFileActionAsync(MainWindowViewModel.FilePickerAction.New);

    [RelayCommand]
    private Task OpenSet() => _handleFileActionAsync(MainWindowViewModel.FilePickerAction.Import);

    [RelayCommand]
    private Task EditSet() => _editLatestSetAsync();

    [RelayCommand]
    private Task OpenSetInDb(SetInDb? setInDb)
        => setInDb == null ? Task.CompletedTask : _openSetInDbAsync(setInDb);

    [RelayCommand]
    private Task EditSetInDb(SetInDb? setInDb)
        => setInDb == null ? Task.CompletedTask : _editSetInDbAsync(setInDb);

    [RelayCommand]
    private Task DeleteSetInDb(SetInDb? setInDb)
        => setInDb == null ? Task.CompletedTask : _deleteSetInDbAsync(setInDb);

    [RelayCommand]
    private Task SearchSet()
        => _searchSetAsync();

    [RelayCommand]
    private Task ShowGlobalStats()
        => _showGlobalStatsAsync();

    [ObservableProperty]
    private bool _isDebug;
    
    
    private readonly Func<MainWindowViewModel.FilePickerAction, Task> _handleFileActionAsync;

}