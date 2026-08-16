namespace testownikUE.Models;

public class UserSettings
{
    //Struktura ustawień użytkownika, tutaj ustawiam też default wartości, które później w ustawieniach można zmienić
    public int WrongAnswerPenalty { get; set; } = 1;
    public int InitialRepetitions { get; set; } = 1;
    public int MaxRepetitions { get; set; } = 3;
}

