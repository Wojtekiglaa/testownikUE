using Microsoft.EntityFrameworkCore;
using testownikUE.Models;
using testownikUE.Services;

namespace testownikUE.Data;

public class UserSettingsDb : DbContext
{
    //Ustawienia użytkownika
    public DbSet<AppSetting> AppSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppPaths.EnsureCreated();
        optionsBuilder.UseSqlite($"Data Source={AppPaths.UserSettingsDbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasIndex(x => x.SettingKey)
            .IsUnique();
    }

    public static void EnsureTables(UserSettingsDb db)
    {
        //Utworzenie schematu z modelu EF.
        db.Database.EnsureCreated();
    }
}

