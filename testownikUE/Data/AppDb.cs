using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using testownikUE.Models;
using testownikUE.Services;

namespace testownikUE.Data;

public class AppDb: DbContext //z EF Core
{


    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<SetInDb> SetsInDb { get; set; }
    public DbSet<QuestionProgress> QuestionProgresses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppPaths.EnsureCreated();
        optionsBuilder.UseSqlite($"Data Source={AppPaths.DbPath}"); //"wkładamy" zmienną do stringa
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SetInDb>()
            .ToTable("RecentSets")
            .HasIndex(x => x.SourcePath)
            .IsUnique();

        modelBuilder.Entity<QuestionProgress>()
            .HasIndex(x => new { x.ImportBatchId, x.QuestionId })
            .IsUnique();
        modelBuilder.Entity<Question>()
            .HasIndex(x => x.ImportBatchId);

        modelBuilder.Entity<Answer>()
            .HasIndex(x => x.QuestionId);

        modelBuilder.Entity<Answer>()
            .HasIndex(x => x.ImportBatchId);
    }

    public static void EnsureTables(AppDb db)
    {
        //Utworzenie schematu z modelu EF Core.
        db.Database.EnsureCreated();
    }
}
public class JsonQuestionDto //https://medium.com/@20011002nimeth/understanding-data-transfer-objects-dtos-in-c-net-best-practices-examples-fe3e90238359
{
    public int questionId { get; set; }
    public string questionAuthor { get; set; } = string.Empty;
    public string question { get; set; } = string.Empty;
    public Dictionary<string, string> answers { get; set; } = new();
    
    public JsonElement correctAnswers { get; set; }
    public List<string> GetCorrectAnswers()
    {
        if (correctAnswers.ValueKind == JsonValueKind.String)
            return new List<string> { correctAnswers.GetString() ?? string.Empty }; //1 odpowiedź
                    //https://learn.microsoft.com/en-in/dotnet/csharp/language-reference/operators/null-coalescing-operator
        if (correctAnswers.ValueKind == JsonValueKind.Array)
            return correctAnswers.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList(); //2+ odpowiedzi

        return new List<string>();//LINQ
    }
}