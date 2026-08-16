namespace testownikUE.Models;

public class SetInDb
{
    //Struktura zestawu zapisanego w bazie
    public int Id { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public System.Guid LastImportBatchId { get; set; }
    public int ImportedQuestionsCount { get; set; }
    public int TotalStudySeconds { get; set; }
    public System.DateTime OpenedAtUtc { get; set; }

    public string DisplayLabel => $"{SetName} ({ImportedQuestionsCount} pyt.)";
}


