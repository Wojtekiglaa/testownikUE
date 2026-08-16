using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace testownikUE.Services;

public class FilePickerService
{
    // Otwiera dialog do wyboru pliku JSON (jeden lub wiele).
    // Zwraca listę wybranych plików lub empty list, jeśli anulowano.
    // https://docs.avaloniaui.net/docs/services/file-dialogs
    public async Task<List<IStorageFile>> PickJsonFilesAsync(bool allowMultiple = false)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = desktop?.MainWindow;
        var storageProvider = mainWindow?.StorageProvider;

        if (storageProvider == null)
            return new();

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz plik JSON",
            AllowMultiple = allowMultiple,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        return [..files];
    }
    
}