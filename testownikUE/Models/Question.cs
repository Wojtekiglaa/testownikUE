using System;
using System.Collections.Generic;

namespace testownikUE.Models;

public class Question
{
    //Struktura danych pytania
    public int Id { get; set; }
    public string Author { get; set; } = "empty";
    public string Text { get; set; } =  "empty"; 
    public int DisplayOrder { get; set; }
    //Relacja 1 do wielu pozwala mieć niestandardową liczbę pytań i odpowiedzi
    public List<Answer> Answers { get; set; } = new();
    public Guid ImportBatchId { get; set; }
}