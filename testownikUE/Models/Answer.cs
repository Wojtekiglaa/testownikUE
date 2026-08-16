using System;

namespace testownikUE.Models;

public class Answer
{
    //Struktura danych odpowiedzi
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public Guid ImportBatchId { get; set; }
}