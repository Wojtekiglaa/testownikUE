using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using testownikUE.Models;

namespace testownikUE.Services;

public enum SaveProgressDecision
{
    Save,
    Skip,
    Cancel
}

public enum SetInDbDialogAction
{
    Open,
    Edit,
    Delete
}

public sealed class SetInDbDialogResult
{
    public required SetInDb Set { get; init; }
    public required SetInDbDialogAction Action { get; init; }
}

public sealed class SetInDbSearchEntry
{
    public required SetInDb Set { get; init; }
    public string SearchText { get; init; } = string.Empty;
}

public sealed class GlobalStatsSnapshot
{
    public int TotalSets { get; init; }
    public int TotalQuestions { get; init; }
    public int TotalProgressEntries { get; init; }
    public int TotalStudySeconds { get; init; }
    public int TotalSeen { get; init; }
    public int TotalWrong { get; init; }
    public int TotalMastered { get; init; }
    public int QuestionsSeenAtLeastOnce { get; init; }
    public int MasteryThreshold { get; init; }
    public double TotalStudyHours { get; init; }
    public double AverageStudyMinutesPerSet { get; init; }
    public double AverageStudyMinutesPerQuestion { get; init; }
    public double AccuracyPercent { get; init; }
    public double CoveragePercent { get; init; }
    public double MasteryPercent { get; init; }
}

public static class DialogService
{
    public static async Task<bool> ShowConfirmAsync(string title, string message, string confirmLabel = "Tak", string cancelLabel = "Anuluj")
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null)
            return false;

        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 120,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner //Na środku okna aplikacji, CenterScreen na srodku ekranu
        };

        var confirmButton = new Button { Content = confirmLabel, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };
        var cancelButton = new Button { Content = cancelLabel, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };

        confirmButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { confirmButton, cancelButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }
    public static async Task ShowInfoAsync(string title, string message)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null)
            return;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 110,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var okButton = new Button { Content = "OK", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { okButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
    }

    public static async Task ShowGlobalStatsAsync(GlobalStatsSnapshot stats)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null)
            return;

        // Mały helper, żeby nie powielać kodu dla każdego wykresu.
        static CartesianChart BuildChart(string title, IReadOnlyList<string> labels, IReadOnlyList<double> values)
        {
            return new CartesianChart
            {
                Height = 170,
                Series = [
                    new ColumnSeries<double>
                    {
                        Name = title,
                        Values = values.ToArray()
                    }
                ],
                XAxes = [
                    new Axis
                    {
                        Labels = labels.ToArray()
                    }
                ]
            };
        }

        var dialog = new Window
        {
            Title = "Statystyki globalne",
            Width = 920,
            Height = 780,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var summary = new TextBlock
        {
            Text = $"Zestawy: {stats.TotalSets} | Pytania: {stats.TotalQuestions} | Wpisy progresu: {stats.TotalProgressEntries} | Pytania widziane co najmniej raz: {stats.QuestionsSeenAtLeastOnce} | Łączny czas nauki: {TimeSpan.FromSeconds(stats.TotalStudySeconds):hh\\:mm\\:ss} | Próg opanowania: {stats.MasteryThreshold}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeight.SemiBold
        };

        var content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Poniżej są globalne statystyki z całej bazy.",
                    TextWrapping = TextWrapping.Wrap
                },
                summary,
                new Border
                {
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Skala danych", FontWeight = FontWeight.Bold },
                            BuildChart(
                                "Skala danych",
                                ["Zestawy", "Pytania", "Progres", "Odpowiedzi", "Opanowane", "Błędy"],
                                [stats.TotalSets, stats.TotalQuestions, stats.TotalProgressEntries, stats.TotalSeen, stats.TotalMastered, stats.TotalWrong])
                        }
                    }
                },
                new Border
                {
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Jakość nauki", FontWeight = FontWeight.Bold },
                            BuildChart(
                                "Jakość nauki",
                                ["Accuracy %", "Coverage %", "Mastery %"],
                                [stats.AccuracyPercent, stats.CoveragePercent, stats.MasteryPercent])
                        }
                    }
                },
                new Border
                {
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Czas nauki", FontWeight = FontWeight.Bold },
                            BuildChart(
                                "Czas nauki",
                                ["Łącznie h", "Śr. min/zestaw", "Śr. min/pytanie"],
                                [stats.TotalStudyHours, stats.AverageStudyMinutesPerSet, stats.AverageStudyMinutesPerQuestion])
                        }
                    }
                }
            }
        };

        dialog.Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
        };

        await dialog.ShowDialog(owner);
    }
    //Picker zestawów w DB.
    public static async Task<SetInDb?> ShowSetInDbPickerAsync(string title, IReadOnlyList<SetInDb> sets)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null || sets.Count == 0)
            return null;

        SetInDb? selected = null;

        var dialog = new Window
        {
            Title = title,
            Width = 560,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var listBox = new ListBox
        {
            ItemsSource = sets,
            ItemTemplate = new FuncDataTemplate<SetInDb>((set, _) =>
                new TextBlock { Text = set.DisplayLabel })
        };

        var openButton = new Button { Content = "Edytuj", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };
        var cancelButton = new Button { Content = "Anuluj", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };

        openButton.Click += (_, _) =>
        {
            selected = listBox.SelectedItem as SetInDb;
            if (selected != null)
                dialog.Close();
        };

        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Wybierz zestaw z bazy danych:",
                    [Grid.RowProperty] = 0
                },
                new Border
                {
                    [Grid.RowProperty] = 1,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Child = listBox
                },
                new StackPanel
                {
                    [Grid.RowProperty] = 2,
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { openButton, cancelButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return selected;
    }
    //Prompt, kiedy użytkownik chce zaimportować zestaw już znajdujący się w DB.
    public static async Task<bool> ShowReimportPromptAsync(string setName)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null)
            return true;

        var result = false;

        var dialog = new Window
        {
            Title = "Ponowny import",
            Width = 460,
            Height = 120,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var yesButton = new Button { Content = "Tak", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };
        var noButton = new Button { Content = "Nie", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center  };

        yesButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        noButton.Click += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = $"Zestaw '{setName}' jest już zaimportowany. Czy chcesz wykonać ponowny import?",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { yesButton, noButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }
    //Prompt wyświetlający się, kiedy użytkownik chce wyjść z testu bez zapisania progresu
    public static async Task<SaveProgressDecision> ShowSaveProgressPromptAsync(string? reportText = null, int? mastered = null, int? notMastered = null)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null)
            return SaveProgressDecision.Save;

        var result = SaveProgressDecision.Cancel;

        var dialog = new Window
        {
            Title = "Powrót",
            Width = 460,
            Height = 300,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var saveButton = new Button
        {
            Content = "Zapisz",
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var skipButton = new Button
        {
            Content = "Nie zapisuj",
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var cancelButton = new Button
        {
            Content = "Anuluj",
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        saveButton.Click += (_, _) =>
        {
            result = SaveProgressDecision.Save;
            dialog.Close();
        };

        skipButton.Click += (_, _) =>
        {
            result = SaveProgressDecision.Skip;
            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = SaveProgressDecision.Cancel;
            dialog.Close();
        };

        var content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10
        };

        content.Children.Add(new TextBlock
        {
            Text = "Czy chcesz zapisać progres przed powrotem?",
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(reportText))
        {
            content.Children.Add(new TextBlock
            {
                Text = reportText,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (mastered.HasValue && notMastered.HasValue)
        {
            content.Children.Add(new CartesianChart
            {
                Height = 160,
                Series =
                [
                    new ColumnSeries<double>
                    {
                        Name = "Wynik",
                        Values = [mastered.Value,notMastered.Value]
                    }
                ],
                XAxes =
                [
                    new Axis
                    {
                        Labels = ["Opanowane", "Nieopanowane"]
                    }
                ]
            });
        }

        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { saveButton, skipButton, cancelButton }
        });

        dialog.Content = content;

        await dialog.ShowDialog(owner);
        return result;
    }
    public static async Task<SetInDbDialogResult?> ShowSetInDbSearchDialogAsync(string title, IReadOnlyList<SetInDbSearchEntry> entries)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner == null || entries.Count == 0)
            return null;

        var result = (SetInDbDialogResult?)null;
        var query = string.Empty;

        var dialog = new Window
        {
            Title = title,
            Width = 680,
            Height = 500,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var listBox = new ListBox();

        void RefreshItems()
        {
            var previousSelection = listBox.SelectedItem as SetInDbSearchEntry;
            // Filtruję po kilku polach naraz, bo wtedy szukanie jest po prostu wygodniejsze.
            var filtered = entries
                .Where(x => string.IsNullOrWhiteSpace(query)
                            || ContainsIgnoreCase(x.Set.SetName, query)
                            || ContainsIgnoreCase(x.Set.DisplayLabel, query)
                            || ContainsIgnoreCase(x.Set.SourcePath, query)
                            || ContainsIgnoreCase(x.SearchText, query))
                .OrderByDescending(x => x.Set.OpenedAtUtc)
                .ToList();

            listBox.ItemsSource = filtered;
            if (filtered.Count == 0)
            {
                listBox.SelectedItem = null;
                return;
            }

            // Jak dalej pasuje, to zostawiam poprzedni wybór.
            if (previousSelection != null && filtered.Contains(previousSelection))
            {
                listBox.SelectedItem = previousSelection;
                return;
            }

            listBox.SelectedIndex = 0;
        }

        var searchBox = new TextBox
        {
            Watermark = "Szukaj po nazwie lub ścieżce..."
        };
        searchBox.TextChanged += (_, _) =>
        {
            query = searchBox.Text ?? string.Empty;
            RefreshItems();
        };

        listBox.ItemTemplate = new FuncDataTemplate<SetInDbSearchEntry>((entry, _) =>
        {
            if (ReferenceEquals(entry, null))
            {
                return new TextBlock
                {
                    Text = "(brak danych)",
                    Opacity = 0.75,
                    Margin = new Thickness(2)
                };
            }

            var set = entry.Set;
            if (ReferenceEquals(set, null))
            {
                return new TextBlock
                {
                    Text = "(brak danych)",
                    Opacity = 0.75,
                    Margin = new Thickness(2)
                };
            }

            return new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(2),
                Children =
                {
                    new TextBlock { Text = set.DisplayLabel, FontWeight = FontWeight.SemiBold },
                    new TextBlock
                    {
                        Text = set.SourcePath,
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
        });

        var openButton = new Button { Content = "Otwórz", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var editButton = new Button { Content = "Edytuj", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var deleteButton = new Button { Content = "Usuń", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancelButton = new Button { Content = "Anuluj", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };

        void CloseWithAction(SetInDbDialogAction action)
        {
            if (listBox.SelectedItem is not SetInDbSearchEntry selected)
                return;

            result = new SetInDbDialogResult
            {
                Set = selected.Set,
                Action = action
            };
            dialog.Close();
        }

        openButton.Click += (_, _) => CloseWithAction(SetInDbDialogAction.Open);
        editButton.Click += (_, _) => CloseWithAction(SetInDbDialogAction.Edit);
        deleteButton.Click += (_, _) => CloseWithAction(SetInDbDialogAction.Delete);
        cancelButton.Click += (_, _) => dialog.Close();

        RefreshItems();

        dialog.Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 10,
            Children =
            {
                new TextBlock
                {
                    [Grid.RowProperty] = 0,
                    Text = "Wyszukaj zestaw i wybierz akcję",
                    FontWeight = FontWeight.SemiBold
                },
                new Border
                {
                    [Grid.RowProperty] = 1,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Child = searchBox
                },
                new Border
                {
                    [Grid.RowProperty] = 2,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Child = listBox
                },
                new StackPanel
                {
                    [Grid.RowProperty] = 3,
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { openButton, editButton, deleteButton, cancelButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }

    // Proste porównanie bez patrzenia na wielkość liter.
    private static bool ContainsIgnoreCase(string? value, string query)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}

