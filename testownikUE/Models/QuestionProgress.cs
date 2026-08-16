using System;

namespace testownikUE.Models;

public class QuestionProgress
{
    //Struktura progresu w pytaniach
    public int Id { get; set; }
    public Guid ImportBatchId { get; set; }
    public int QuestionId { get; set; }
    public int BoxLevel { get; set; }
    public int ConsecutiveCorrect { get; set; }
    public int SeenCount { get; set; }
    public int WrongCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

