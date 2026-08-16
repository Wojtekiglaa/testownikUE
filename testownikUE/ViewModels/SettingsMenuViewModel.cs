using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testownikUE.Models;
using testownikUE.Services;

namespace testownikUE.ViewModels;

public partial class SettingsMenuViewModel : ViewModelBase
{
    private readonly Action _navigateToHomeView;
    private readonly UserSettingsService _userSettingsService;

    //Observable property to wartości w UI
    [ObservableProperty]
    private decimal _wrongAnswerPenalty;

    [ObservableProperty]
    private decimal _initialRepetitions;

    [ObservableProperty]
    private decimal _maxRepetitions;

    public SettingsMenuViewModel(Action navigateToHome, UserSettingsService userSettingsService)
    {
        _navigateToHomeView = navigateToHome;
        _userSettingsService = userSettingsService;

        var settings = _userSettingsService.Load();
        WrongAnswerPenalty = settings.WrongAnswerPenalty;
        InitialRepetitions = settings.InitialRepetitions;
        MaxRepetitions = settings.MaxRepetitions;

        AppLog.Info("SettingsMenu", $"Loaded settings: penalty={WrongAnswerPenalty}, init={InitialRepetitions}, max={MaxRepetitions}");
    }
    


    [RelayCommand]
    private void GoBack() => _navigateToHomeView();

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveSettings()
    {
        AppLog.Info("SettingsMenu", $"Saving settings: penalty={WrongAnswerPenalty}, init={InitialRepetitions}, max={MaxRepetitions}");

        try
        {
            _userSettingsService.Save(new UserSettings
            {
                WrongAnswerPenalty = (int)WrongAnswerPenalty,
                InitialRepetitions = (int)InitialRepetitions,
                MaxRepetitions = (int)MaxRepetitions
            });

            await DialogService.ShowInfoAsync("Zapisano", "Ustawienia użytkownika zostały zapisane.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowInfoAsync("Błąd ustawień", $"Nie udało się zapisać ustawień:\n{ex.Message}");
        }
    }
}