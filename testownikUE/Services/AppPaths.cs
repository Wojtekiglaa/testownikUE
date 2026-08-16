using System;
using System.IO;

namespace testownikUE.Services;

public class AppPaths
{
    //Ścieżki, które używa aplikacja. Zapisuję w appdata na dany system. Na windowsie-appdata roaming a na macOS-LibrarySupport
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TestownikUE");
    public static string DbDir => Path.Combine(Root, "db");
    public static string DbPath => Path.Combine(DbDir, "testownik.db");
    public static string UserSettingsDbPath => Path.Combine(DbDir, "usersettings.db");

    public static void EnsureCreated()
    {
        if (!Directory.Exists(Root) || !Directory.Exists(DbDir))
        {
                Directory.CreateDirectory(Root);
                Directory.CreateDirectory(DbDir);
        }
        //Tworzymy directory przy pierwszym uruchomieniu aplikacji
    }
}