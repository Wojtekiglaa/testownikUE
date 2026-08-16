using System;
using System.Linq;
using testownikUE.Data;
using testownikUE.Models;

namespace testownikUE.Services;

public class UserSettingsService
{
    //Serwis zajmujący się ustawieniami użytkownika.
    //Ładowanie ustawień z DB.
    public UserSettings Load()
    {
        try
        {
            AppPaths.EnsureCreated();
            using var db = new UserSettingsDb();
            UserSettingsDb.EnsureTables(db);

            var settings = new UserSettings
            {
                WrongAnswerPenalty = ReadInt(db, "WrongAnswerPenalty", 1),
                InitialRepetitions = ReadInt(db, "InitialRepetitions", 1),
                MaxRepetitions = ReadInt(db, "MaxRepetitions", 3)
            };

            var clampedDb = Clamp(settings);
            if (HasAnyDbSettings(db))
            {
                Log($"Load settings from usersettings.db: penalty={clampedDb.WrongAnswerPenalty}, init={clampedDb.InitialRepetitions}, max={clampedDb.MaxRepetitions}");
                return clampedDb;
            }

            Log($"Load default settings: penalty={clampedDb.WrongAnswerPenalty}, init={clampedDb.InitialRepetitions}, max={clampedDb.MaxRepetitions}");
            SaveToDb(db, clampedDb);
            return clampedDb;
        }
        catch (Exception ex)
        {
            Log($"Load settings error, fallback defaults: {ex.Message}");
            return new UserSettings();
        }
    }

    //Zapisywanie ustawień do DB.
    public void Save(UserSettings settings)
    {
        try
        {
            AppPaths.EnsureCreated();
            var clamped = Clamp(settings);

            using var db = new UserSettingsDb();
            UserSettingsDb.EnsureTables(db);
            SaveToDb(db, clamped);

            Log($"Save settings to usersettings.db: penalty={clamped.WrongAnswerPenalty}, init={clamped.InitialRepetitions}, max={clamped.MaxRepetitions}");
        }
        catch (Exception ex)
        {
            Log($"Save settings error: {ex.Message}");
            throw;
        }
    }

    //Funkcja sprawdzająca, czy jakieś ustawienia istnieją
    private static bool HasAnyDbSettings(UserSettingsDb db)
        => db.AppSettings.Any(x => x.SettingKey == "WrongAnswerPenalty"
                                 || x.SettingKey == "InitialRepetitions"
                                 || x.SettingKey == "MaxRepetitions");

    private static int ReadInt(UserSettingsDb db, string key, int defaultValue)
    {
        //Czytamy stringa i parsujemy go na int-a.
        var raw = db.AppSettings
            .Where(x => x.SettingKey == key)
            .Select(x => x.SettingValue)
            .FirstOrDefault();

        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static void Upsert(UserSettingsDb db, string key, int value)
    {
        //Upsert-kombinacja update i insert w SQL, czyli jeśli istnieje to aktualizujemy, a jeśli nie to tworzymy nowe.
        //https://www.geeksforgeeks.org/sql-server/upsert-operation-in-sql-server/
        //Aktualizacja ustawień.
        var existing = db.AppSettings.FirstOrDefault(x => x.SettingKey == key);
        if (existing == null)
        {
            db.AppSettings.Add(new AppSetting
            {
                SettingKey = key,
                SettingValue = value.ToString(),
                UpdatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        existing.SettingValue = value.ToString();
        existing.UpdatedAtUtc = DateTime.UtcNow;
    }

    //Zapisujemy do db.
    private static void SaveToDb(UserSettingsDb db, UserSettings settings)
    {
        Upsert(db, "WrongAnswerPenalty", settings.WrongAnswerPenalty);
        Upsert(db, "InitialRepetitions", settings.InitialRepetitions);
        Upsert(db, "MaxRepetitions", settings.MaxRepetitions);
        db.SaveChanges();
    }


    private static void Log(string message)
    {
        AppLog.Info("UserSettingsService", message);
    }

    private static UserSettings Clamp(UserSettings settings)
    {
        //Clampowanie wartości do max i min ustalonego przeze mnie
        return new UserSettings
        {
            WrongAnswerPenalty = Math.Clamp(settings.WrongAnswerPenalty, 0, 10),
            InitialRepetitions = Math.Clamp(settings.InitialRepetitions, 1, 10),
            MaxRepetitions = Math.Clamp(settings.MaxRepetitions, 1, 10)
        };
    }
}

