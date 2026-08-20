using UnityEngine;

public enum QuizAnswer
{
    A,
    B
}

[CreateAssetMenu(menuName = "Battle/QuizData")]
public class QuizData : ScriptableObject
{
    [Header("問題文")]
    [TextArea(2, 5)]
    public string questionText;

    [Header("選択肢")]
    public string choiceA;
    public string choiceB;

    [Header("正解")]
    public QuizAnswer correctAnswer;
}