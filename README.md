# Testownik UE
  TestownikUE to aplikacja, która pomaga w przygotowaniu do nadchodzącego kolokwium lub testu. Projekt zaliczeniowy z przedmiotu *"Programowanie w technologii .NET"*.
# Wykorzystane elementy
- https://avaloniaui.github.io/icons.html
- https://commons.wikimedia.org/wiki/File:Crown_of_Stefan_the_First.svg
- https://uxwing.com/ab-testing-icon/
# Struktura JSON-a
```json
[
  {
    "questionId": 1,
    "questionAuthor": "demo",
    "question": "Pytanie 1",
    "answers": {
      "a": "Odpowiedz a",
      "b": "Odpowiedz b",
      "c": "Odpowiedz c",
      "d": "Odpowiedz d - poprawna"
    },
    "correctAnswers": "d"
  },
  {
    "questionId": 2,
    "questionAuthor": "demo",
    "question": "Pytanie 2",
    "answers": {
      "a": "Odpowiedz a - poprawna",
      "b": "Odpowiedz b",
      "c": "Odpowiedz c - poprawna"
    },
    "correctAnswers": ["c","a"]
  }
]
```
... i tak dalej
