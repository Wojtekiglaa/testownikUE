using Avalonia.Controls;
using System;
using System.Windows.Input;
using testownikUE.Models;
using testownikUE.ViewModels;

namespace testownikUE.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void OpenSetInDb_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => RunSetInDbCommand(sender, vm => vm.OpenSetInDbCommand);

    private void EditSetInDb_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => RunSetInDbCommand(sender, vm => vm.EditSetInDbCommand);

    private void DeleteSetInDb_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => RunSetInDbCommand(sender, vm => vm.DeleteSetInDbCommand);

    private void RunSetInDbCommand(object? sender, Func<HomeViewViewModel, ICommand> commandSelector)
    {
        if (DataContext is not HomeViewViewModel vm || sender is not Button button || button.DataContext is not SetInDb setInDb)
            return;

        commandSelector(vm).Execute(setInDb);
    }
}