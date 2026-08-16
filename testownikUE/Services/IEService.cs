using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using testownikUE.Data;
using testownikUE.Models;

namespace testownikUE.Services;

public class ImportExportService
{
    //Serwis importu pliku JSON do DB.
    public Guid ImportJsonToDatabase(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new InvalidOperationException("Nie wybrano danych do importu (pusty plik JSON).");

        try
        {
            var dataRaw = JsonSerializer.Deserialize<List<JsonQuestionDto>>(jsonContent)
                          ?? throw new InvalidOperationException("JSON jest pusty albo ma nieprawidłowy format.");

            if (dataRaw.Count == 0)
                throw new InvalidOperationException("JSON nie zawiera żadnych pytań do zaimportowania.");

            using var db = new AppDb();
            AppDb.EnsureTables(db);
            var batchId = Guid.NewGuid();

            for (var questionIndex = 0; questionIndex < dataRaw.Count; questionIndex++)
            {
                var item = dataRaw[questionIndex];
                var question = new Question()
                {
                    Text = item.question,
                    Author = item.questionAuthor,
                    DisplayOrder = questionIndex,
                    ImportBatchId = batchId
                };

                var correctSet = new HashSet<string>(
                    item.GetCorrectAnswers(),
                    StringComparer.OrdinalIgnoreCase);

                var answerIndex = 0;
                foreach (var answer in item.answers)
                {
                    question.Answers.Add(new Answer
                    {
                        Key = answer.Key.ToUpperInvariant(), //ABCD duże
                        Text = answer.Value,
                        IsCorrect = correctSet.Contains(answer.Key.ToUpperInvariant()),
                        DisplayOrder = answerIndex++,
                        ImportBatchId = batchId
                    });
                }

                db.Questions.Add(question);
            }

            db.SaveChanges();
            AppLog.Info("ImportExportService", $"Imported {dataRaw.Count} questions, batch={batchId}");
            return batchId;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Nieprawidłowy format JSON: {ex.Message}", ex);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Błąd zapisu do bazy danych podczas importu: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Nie udało się zaimportować zestawu: {ex.Message}", ex);
        }
    }
}
