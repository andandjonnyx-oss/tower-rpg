using UnityEngine;

public enum QuizAnswer
{
    A,
    B
}

[CreateAssetMenu(menuName = "Battle/QuizData")]
public class QuizData : ScriptableObject
{
    [Header("–â‘è•¶")]
    [TextArea(2, 5)]
    public string questionText;

    [Header("‘I‘ğˆ")]
    public string choiceA;
    public string choiceB;

    [Header("³‰ğ")]
    public QuizAnswer correctAnswer;
}